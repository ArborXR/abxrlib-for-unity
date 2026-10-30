#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>What the replayer needs from the transport and auth, so it can be driven without a backend in tests.</summary>
    internal interface IReplayHost
    {
        IEnumerator SignIn(AuthPayload payload, Action<AuthTransportResult> done);
        IEnumerator Upload(RecordChunk chunk, string token, string secret, Action<ChunkResult> done);
        /// <summary>The live session's credentials (tokens, build type, instance id), or null when there are none to replay with.</summary>
        AuthPayload CurrentCredentials();
        /// <summary>True while a live send is out for this session's records.</summary>
        bool IsBusy(string sessionId);
        bool IsRevokedInstance(AuthTransportResult result);
        void RevokePairing();
        int ChunkSize(RecordStream stream);
        float Now { get; }
        IEnumerator Wait(float seconds);
        void Acknowledge(RecordChunk chunk);
    }

    /// <summary>
    /// Signs each sealed session in again and uploads its records under the token it gets back (offline RFC D1), oldest
    /// first. It runs once the live session's device auth has succeeded, which proves the connection, and never touches
    /// the live session. Outcomes: 429 waits for Retry-After and carries on; a revoked instance revokes the pairing, which
    /// drops everything; 403 (suspended) stops and keeps everything; any other refusal skips the session this launch and
    /// drops it after three launches; offline or 5xx stops until the next try.
    /// </summary>
    internal sealed class SessionReplayer
    {
        /// <summary>How long to wait after a replay stopped because the device went offline.</summary>
        internal const float RetryAfterOfflineSeconds = 60f;

        private readonly DurableRecordQueue _queue;
        private readonly IReplayHost _host;
        /// <summary>Sessions whose sign-in was refused this launch. They're tried again next launch.</summary>
        private readonly HashSet<string> _skippedThisLaunch = new HashSet<string>();
        private float _nextAttemptAt;
        private int _runGeneration;

        internal SessionReplayer(DurableRecordQueue queue, IReplayHost host)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal bool IsRunning { get; private set; }

        /// <summary>True when there's a sealed session to replay and the last attempt's wait is over.</summary>
        internal bool ShouldRun(float now)
        {
            if (IsRunning || now < _nextAttemptAt) return false;
            foreach (var s in _queue.SealedSessions())
                if (!_skippedThisLaunch.Contains(s.SessionId)) return true;
            return false;
        }

        /// <summary>A live upload just succeeded: the device is back online, so try again without waiting.</summary>
        internal void ResumeNow() => _nextAttemptAt = 0;

        /// <summary>Stops a running replay at its next step (the queue was cleared, or the transport stopped).</summary>
        internal void Cancel() => _runGeneration++;

        internal IEnumerator Run()
        {
            if (IsRunning) yield break;
            IsRunning = true;
            int generation = _runGeneration;
            try
            {
                foreach (var session in _queue.SealedSessions())
                {
                    if (generation != _runGeneration) yield break;
                    if (_skippedThisLaunch.Contains(session.SessionId) || _host.IsBusy(session.SessionId)) continue;
                    bool keepGoing = true;
                    yield return ReplaySession(session, generation, go => keepGoing = go);
                    if (!keepGoing) yield break;
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        private IEnumerator ReplaySession(StoredSession session, int generation, Action<bool> keepGoing)
        {
            var credentials = _host.CurrentCredentials();
            if (credentials == null) { keepGoing(false); yield break; }
            if (_queue.DiscardIfNothingToSend(session)) { keepGoing(true); yield break; }
            var signIn = session.SignIn;
            if (signIn?.payload == null)
            {
                // Records from a launch where nothing ever signed in (killed in its first frames, or auth never started).
                Logcat.Info($"Dropped offline session {session.SessionId}: it never signed in, so it can't be signed in again.");
                _queue.Drop(session, AbxrObserver.DropReason.IdentityChanged);
                keepGoing(true);
                yield break;
            }

            // Offline RFC D6: only the app, org and app instance that recorded a session may send it.
            string app = SavedSignIn.AppKeyOf(credentials), org = SavedSignIn.OrgKeyOf(credentials), instance = SavedSignIn.InstanceKeyOf(credentials);
            if (!signIn.SameIdentity(app, org, instance))
            {
                Logcat.Warning($"Dropped offline session {session.SessionId}: it was recorded as a different app, organization or paired instance.");
                _queue.Drop(session, AbxrObserver.DropReason.IdentityChanged);
                keepGoing(true);
                yield break;
            }

            string token = null, secret = null;
            bool signedInAgain = false;
            while (true)
            {
                if (generation != _runGeneration) { keepGoing(false); yield break; }
                if (token == null)
                {
                    SignInOutcome outcome = default;
                    yield return SignInSession(session, credentials, o => outcome = o);
                    if (generation != _runGeneration) { keepGoing(false); yield break; }
                    switch (outcome.Kind)
                    {
                        case SignInKind.Success:
                            token = outcome.Token;
                            secret = outcome.Secret;
                            break;
                        case SignInKind.RateLimited:
                            yield return _host.Wait(outcome.RetryAfterSeconds);
                            continue;
                        case SignInKind.Revoked:
                            _host.RevokePairing();
                            keepGoing(false);
                            yield break;
                        case SignInKind.Suspended:
                        case SignInKind.Offline:
                            _nextAttemptAt = _host.Now + RetryAfterOfflineSeconds;
                            keepGoing(false);
                            yield break;
                        default: // Refused
                            _queue.MarkReplayFailed(session);
                            if (session.Summary.failedReplays >= DurableRecordQueue.MaxFailedReplays)
                            {
                                Logcat.Warning($"Dropped offline session {session.SessionId}: the backend refused its sign-in on {session.Summary.failedReplays} launches.");
                                _queue.Drop(session, AbxrObserver.DropReason.Rejected);
                            }
                            else
                            {
                                Logcat.Warning($"The backend refused the sign-in for offline session {session.SessionId}. It's tried again next launch.");
                                _skippedThisLaunch.Add(session.SessionId);
                            }
                            keepGoing(true);
                            yield break;
                    }
                }

                // Records first, then storage.
                RecordChunk chunk = _queue.Peek(session, RecordStream.Data, _host.ChunkSize(RecordStream.Data))
                    ?? _queue.Peek(session, RecordStream.Storage, _host.ChunkSize(RecordStream.Storage));
                if (chunk == null)
                {
                    // All sent, and the queue deleted it; or what's left can't be read.
                    _queue.DiscardIfNothingToSend(session);
                    keepGoing(true);
                    yield break;
                }
                ChunkResult result = default;
                yield return _host.Upload(chunk, token, secret, r => result = r);
                switch (result.Outcome)
                {
                    case ChunkOutcome.Done:
                        _host.Acknowledge(chunk);
                        break;
                    case ChunkOutcome.RateLimited:
                        yield return _host.Wait(result.RetryAfterSeconds);
                        break;
                    case ChunkOutcome.Unauthorized when !signedInAgain:
                        // The replay token expired mid-upload: sign the session in once more.
                        signedInAgain = true;
                        token = null;
                        break;
                    case ChunkOutcome.Cancelled:
                        keepGoing(false);
                        yield break;
                    default:
                        _nextAttemptAt = _host.Now + RetryAfterOfflineSeconds;
                        keepGoing(false);
                        yield break;
                }
            }
        }

        private enum SignInKind { Success, RateLimited, Offline, Revoked, Suspended, Refused }

        private struct SignInOutcome
        {
            public SignInKind Kind;
            public string Token;
            public string Secret;
            public int RetryAfterSeconds;
        }

        /// <summary>
        /// A session that signed in online replays without its login (INS-527 keeps its user; today's backend asks for the
        /// user again, and then the saved answer or user data goes too). A session that never signed in online replays with
        /// its saved answer, or its user data, as the auth mechanism.
        /// </summary>
        private IEnumerator SignInSession(StoredSession session, AuthPayload credentials, Action<SignInOutcome> done)
        {
            var signIn = session.SignIn;
            var userMechanism = UserMechanism(signIn);
            bool deviceOnlyFirst = signIn.signedInOnline || userMechanism == null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool withUser = !deviceOnlyFirst || attempt == 1;
                if (withUser && userMechanism == null) break;
                var payload = BuildPayload(signIn, credentials, withUser ? userMechanism : null);
                AuthTransportResult result = default;
                yield return _host.SignIn(payload, r => result = r);
                if (result.ServerDate.HasValue) AbxrAuthService.RecordServerDate(result.ServerDate.Value);

                if (result.Success)
                {
                    AuthResponse response = null;
                    try { response = JsonConvert.DeserializeObject<AuthResponse>(result.Body); }
                    catch (JsonException) { /* handled below */ }
                    if (!string.IsNullOrEmpty(response?.Token) && !string.IsNullOrEmpty(response.Secret))
                    {
                        done(new SignInOutcome { Kind = SignInKind.Success, Token = response.Token, Secret = response.Secret });
                        yield break;
                    }
                    // The backend wants the user before it gives a token. Try again with the saved login, if there is one.
                    continue;
                }
                if (result.StatusCode == 429)
                {
                    done(new SignInOutcome { Kind = SignInKind.RateLimited, RetryAfterSeconds = result.RetryAfterSeconds > 0 ? result.RetryAfterSeconds : 60 });
                    yield break;
                }
                if (result.NetworkError || result.StatusCode == 0 || result.StatusCode == 408 || result.StatusCode >= 500)
                {
                    done(new SignInOutcome { Kind = SignInKind.Offline });
                    yield break;
                }
                if (_host.IsRevokedInstance(result))
                {
                    done(new SignInOutcome { Kind = SignInKind.Revoked });
                    yield break;
                }
                // Offline RFC D5: a suspended instance (403) keeps its sessions on disk.
                if (result.StatusCode == 403 && !string.IsNullOrEmpty(credentials.appInstanceToken))
                {
                    done(new SignInOutcome { Kind = SignInKind.Suspended });
                    yield break;
                }
                break;
            }
            done(new SignInOutcome { Kind = SignInKind.Refused });
        }

        /// <summary>The saved login as an auth mechanism: the email or text answer, else the user data. Null when there's neither.</summary>
        internal static Dictionary<string, string> UserMechanism(SavedSignIn signIn)
        {
            if (!string.IsNullOrEmpty(signIn.loginType) && !string.IsNullOrEmpty(signIn.loginValue))
                return new Dictionary<string, string> { ["type"] = signIn.loginType, ["prompt"] = signIn.loginValue, ["inputSource"] = "user" };
            if (signIn.userData != null && signIn.userData.Count > 0)
            {
                var dict = new Dictionary<string, string> { ["type"] = "custom", ["inputSource"] = "user" };
                foreach (var kv in signIn.userData)
                    if (kv.Key != "type" && kv.Key != "prompt" && kv.Key != "inputSource") dict[kv.Key] = kv.Value;
                return dict;
            }
            return null;
        }

        /// <summary>The saved sign-in with today's credentials. The live session's payload and auth state are untouched.</summary>
        internal static AuthPayload BuildPayload(SavedSignIn signIn, AuthPayload credentials, Dictionary<string, string> authMechanism)
        {
            var payload = JsonConvert.DeserializeObject<AuthPayload>(JsonConvert.SerializeObject(signIn.payload));
            payload.appId = credentials.appId;
            payload.orgId = credentials.orgId;
            payload.authSecret = credentials.authSecret;
            payload.appToken = credentials.appToken;
            payload.orgToken = credentials.orgToken;
            payload.appInstanceToken = credentials.appInstanceToken;
            payload.SSOAccessToken = null;
            payload.buildType = credentials.buildType == "production_custom" ? "production" : credentials.buildType;
            payload.authMechanism = authMechanism;
            return payload;
        }
    }
}
#endif

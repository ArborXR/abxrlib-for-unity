using System;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Core.UI;

namespace AbxrLib.Runtime.Services.Pairing
{
    /// <summary>
    /// All that authentication reads from pairing. Auth never owns the stored credential: it asks for it while
    /// resolving identity, and reports the two bootstrap refusals that change it.
    /// </summary>
    internal interface IPairedCredential
    {
        /// <summary>The stored app instance. Answers in Managed too, so auth can send it as priorAppInstanceId.</summary>
        bool TryGetStored(out string token, out string instanceId);

        /// <summary>True after a 403 for the stored instance. Scoped to that instance, so a re-pair still authenticates.</summary>
        bool IsSuspendedForSession { get; }

        /// <summary>A paired bootstrap got a 401: the instance is gone. Ignored once the instance has been replaced.</summary>
        void Revoke(string instanceId);

        /// <summary>A paired bootstrap got a 403: keep the pairing, but send nothing more with it this session.</summary>
        void SuspendForSession(string instanceId);
    }

    /// <summary>What the pairing service needs from the SDK around it. The subsystem implements it; tests fake it.</summary>
    internal interface IPairingHost
    {
        /// <summary>False where pairing doesn't run: standalone builds, and Editor Play mode unless a test turns it on.</summary>
        bool IsPlatformSupported { get; }

        /// <summary>True when an app handler or a registered UI can show the prompt.</summary>
        bool CanPresentPrompt { get; }

        /// <summary>The App Token that proves the build. Empty when the app doesn't use app tokens.</summary>
        string AppToken { get; }

        string PairingUrl { get; }

        /// <summary>Seconds on a clock that never runs backwards, for the Retry-After gate.</summary>
        double Now { get; }

        PairingDeviceMetadata DeviceMetadata { get; }
    }

    /// <summary>
    /// Passcode pairing (SDK-60): the state machine, the stored app instance, the redeem, the prompt's lifecycle, the
    /// Retry-After gate, and the state events. Pairing is something the app asks for; nothing here prompts on its own.
    /// Main thread only, like the rest of the SDK.
    /// </summary>
    internal sealed class AbxrPairingService : IPairedCredential
    {
        internal const string InputType = "pairingPasscode";
        private const string PromptLabel = "Pairing Passcode";
        private const string SkipInput = "**skip**";
        private const string UnsupportedPlatform = "pairing runs only in Android and WebGL builds.";
        /// <summary>How many prompts an app handler may re-request from inside its own callback before the SDK stops re-asking.</summary>
        private const int MaxNestedInputRequests = 8;

        /// <summary>Raised to show the prompt, as (type, prompt, domain, error). The subsystem dispatches it like auth's requests.</summary>
        internal Action<string, string, string, string> OnInputRequested;

        /// <summary>Raised when pairing settles, with the reason. Never raised for Prompting or Redeeming.</summary>
        internal Action<Abxr.PairingState, Abxr.PairingChangeReason> OnStateChanged;

        private readonly IPairingStore _store;
        private readonly IPairingRedeemClient _client;
        private readonly IPairingHost _host;

        private string _token;
        private string _instanceId;
        private string _suspendedInstanceId;
        private double _rateLimitedUntil;
        private int _redeemAttempt;
        private bool _redeemFromPrompt;
        private bool _cancelRequested;
        private int _inputRequestDepth;

        internal Abxr.PairingState State { get; private set; } = Abxr.PairingState.Resolving;

        /// <summary>The last redeem outcome, refused calls included. The default UI reads RetryAfterSeconds from it.</summary>
        internal Abxr.PairingRedeemResult LastRedeemResult { get; private set; }

        /// <summary>True while the prompt waits for a passcode, so the subsystem knows OnInputSubmitted is for pairing.</summary>
        internal bool IsInputRequestPending => State == Abxr.PairingState.Prompting;

        internal AbxrPairingService(IPairingStore store, IPairingRedeemClient client, IPairingHost host)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _host = host ?? throw new ArgumentNullException(nameof(host));

            if (_store.TryLoad(out string token, out string instanceId))
            {
                _token = token;
                _instanceId = instanceId;
            }
        }

        private bool HasStoredPairing =>
            _host.IsPlatformSupported && !string.IsNullOrEmpty(_token) && !string.IsNullOrEmpty(_instanceId);

        // ── Identity ─────────────────────────────────────────────────

        /// <summary>
        /// Ends Resolving once auth knows whether the ArborXR client or an org token identifies the organization.
        /// If neither does, a stored pairing does, or nothing does. Fires Startup, the launch trigger for apps.
        /// </summary>
        internal void SettleIdentity(bool otherIdentityWins)
        {
            if (State != Abxr.PairingState.Resolving) return;

            if (otherIdentityWins) Settle(Abxr.PairingState.Managed, Abxr.PairingChangeReason.Startup);
            else if (HasStoredPairing) Settle(Abxr.PairingState.Paired, Abxr.PairingChangeReason.Startup);
            else Settle(Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Startup);
        }

        // ── Prompt ───────────────────────────────────────────────────

        /// <summary>Opens the pairing prompt. False, with a warning that says why, unless the app is Unpaired and can show it.</summary>
        internal bool StartPairing()
        {
            string refusal = StartRefusal();
            if (refusal != null)
            {
                Logcat.Warning($"StartPairing() ignored: {refusal}");
                return false;
            }

            State = Abxr.PairingState.Prompting;
            RequestInput("");
            return true;
        }

        /// <summary>
        /// Closes the prompt without pairing. A redeem already on the wire still finishes: a success pairs, and a
        /// failure ends the attempt instead of re-prompting.
        /// </summary>
        internal void CancelPairing()
        {
            if (State == Abxr.PairingState.Prompting)
            {
                AbxrUi.AuthUi?.Hide();
                Settle(Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed);
            }
            else if (State == Abxr.PairingState.Redeeming && _redeemFromPrompt)
            {
                _cancelRequested = true;
                AbxrUi.AuthUi?.Hide();
            }
            else
            {
                Logcat.Debug("CancelPairing() ignored: no pairing prompt is open.");
            }
        }

        /// <summary>
        /// OnInputSubmitted while the prompt is open, routed here by the subsystem. "**skip**" is the prompt's
        /// "Not now": it closes the prompt and stores nothing.
        /// </summary>
        internal void SubmitInput(string input)
        {
            if (State != Abxr.PairingState.Prompting)
            {
                Logcat.Warning("A pairing passcode was submitted, but no pairing prompt is open.");
                return;
            }

            if (input == SkipInput)
            {
                CancelPairing();
                return;
            }

            Redeem(input, null);
        }

        // ── Redeem ───────────────────────────────────────────────────

        /// <summary>
        /// The headless redeem, for an app's own UI. Valid while Unpaired or Prompting. onComplete always fires, and
        /// fires before this returns when the call is refused or the passcode is malformed.
        /// </summary>
        internal void RedeemPairingPasscode(string passcode, Action<Abxr.PairingRedeemResult> onComplete) =>
            Redeem(passcode, onComplete);

        private void Redeem(string input, Action<Abxr.PairingRedeemResult> onComplete)
        {
            string refusal = RedeemRefusal();
            if (refusal != null)
            {
                Logcat.Warning($"RedeemPairingPasscode() refused: {refusal}");
                Finish(PairingOutcomes.Refused(refusal), onComplete, reprompt: false);
                return;
            }

            bool fromPrompt = State == Abxr.PairingState.Prompting;

            string configurationProblem = ConfigurationProblem();
            if (configurationProblem != null)
            {
                Logcat.Warning($"Can't redeem a pairing passcode: {configurationProblem}");
                Finish(PairingOutcomes.Failure(Abxr.PairingRedeemError.BuildRejected), onComplete, fromPrompt);
                return;
            }

            string passcode = PairingOutcomes.NormalizePasscode(input);
            if (!PairingOutcomes.IsWellFormedPasscode(passcode))
            {
                Logcat.Debug("The pairing passcode isn't six digits, so it wasn't sent.");
                Finish(PairingOutcomes.Failure(Abxr.PairingRedeemError.InvalidPasscode), onComplete, fromPrompt);
                return;
            }

            double wait = _rateLimitedUntil - _host.Now;
            if (wait > 0)
            {
                Finish(PairingOutcomes.RateLimited((int)Math.Ceiling(wait)), onComplete, fromPrompt);
                return;
            }

            _redeemFromPrompt = fromPrompt;
            _cancelRequested = false;
            State = Abxr.PairingState.Redeeming;
            int attempt = ++_redeemAttempt;
            _client.Send(
                PairingOutcomes.RedeemUrl(_host.PairingUrl),
                PairingOutcomes.RedeemBody(_host.AppToken, passcode, _host.DeviceMetadata),
                response => OnRedeemResponse(attempt, response, onComplete));
        }

        private void OnRedeemResponse(int attempt, PairingHttpResponse response, Action<Abxr.PairingRedeemResult> onComplete)
        {
            if (attempt != _redeemAttempt || State != Abxr.PairingState.Redeeming) return;

            var result = PairingOutcomes.Classify(response, DateTime.UtcNow, out string token, out string instanceId);
            LogRedeemOutcome(result, response, instanceId);

            if (result.Success)
            {
                // Before anything else can run: the Portal returns the token only once.
                Store(token, instanceId);
                LastRedeemResult = result;
                AbxrUi.AuthUi?.Hide();
                Settle(Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired);
                onComplete?.Invoke(result);
                return;
            }

            if (result.Error == Abxr.PairingRedeemError.RateLimited)
                _rateLimitedUntil = _host.Now + result.RetryAfterSeconds;

            if (!_redeemFromPrompt)
            {
                State = Abxr.PairingState.Unpaired;
                Finish(result, onComplete, reprompt: false);
            }
            else if (_cancelRequested)
            {
                LastRedeemResult = result;
                Settle(Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed);
                onComplete?.Invoke(result);
            }
            else
            {
                State = Abxr.PairingState.Prompting;
                Finish(result, onComplete, reprompt: true);
            }
        }

        /// <summary>Records an attempt that didn't pair, re-shows the prompt with its message when asked, then reports it.</summary>
        private void Finish(Abxr.PairingRedeemResult result, Action<Abxr.PairingRedeemResult> onComplete, bool reprompt)
        {
            LastRedeemResult = result;
            if (reprompt) RequestInput(result.Message);
            onComplete?.Invoke(result);
        }

        // ── Stored pairing ───────────────────────────────────────────

        /// <summary>
        /// Stores an app instance minted elsewhere (V1b's entry point) and pairs, exactly like a redeem. While
        /// Resolving, identity settling decides the state; while Managed, the instance is kept but the other identity still wins.
        /// </summary>
        internal void SetAppInstanceToken(string token, string appInstanceId)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(appInstanceId))
            {
                Logcat.Warning("SetAppInstanceToken() ignored: it needs both the token and the app instance id.");
                return;
            }
            if (!_host.IsPlatformSupported)
            {
                Logcat.Warning($"SetAppInstanceToken() ignored: {UnsupportedPlatform}");
                return;
            }

            switch (State)
            {
                case Abxr.PairingState.Redeeming:
                    Logcat.Warning("SetAppInstanceToken() ignored: a passcode is being redeemed.");
                    return;
                case Abxr.PairingState.Resolving:
                    Store(token.Trim(), appInstanceId.Trim());
                    return;
                case Abxr.PairingState.Managed:
                    Store(token.Trim(), appInstanceId.Trim());
                    Logcat.Info("Stored the app instance, but the ArborXR client or an org token identifies this app's organization and takes precedence.");
                    return;
                default:
                    if (State == Abxr.PairingState.Prompting) AbxrUi.AuthUi?.Hide();
                    Store(token.Trim(), appInstanceId.Trim());
                    Settle(Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired);
                    return;
            }
        }

        /// <summary>Deletes the stored pairing. From Paired that means Unpaired, with no prompt; whether to ask again is the app's call.</summary>
        internal void ClearPairing()
        {
            if (State == Abxr.PairingState.Redeeming)
            {
                Logcat.Warning("ClearPairing() ignored: a passcode is being redeemed.");
                return;
            }

            Forget();
            if (State == Abxr.PairingState.Paired)
                Settle(Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Cleared);
        }

        public bool TryGetStored(out string token, out string instanceId)
        {
            bool stored = HasStoredPairing;
            token = stored ? _token : null;
            instanceId = stored ? _instanceId : null;
            return stored;
        }

        public bool IsSuspendedForSession => _suspendedInstanceId != null && _suspendedInstanceId == _instanceId;

        public void Revoke(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || instanceId != _instanceId) return;

            Logcat.Warning($"The backend no longer accepts app instance {instanceId}, so the SDK removed the pairing.");
            Forget();
            if (State == Abxr.PairingState.Paired)
                Settle(Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Revoked);
        }

        public void SuspendForSession(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || instanceId != _instanceId) return;

            _suspendedInstanceId = instanceId;
            Logcat.Warning($"Access for app instance {instanceId} is suspended. The pairing is kept, and nothing more is sent with it this session.");
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void Store(string token, string instanceId)
        {
            _store.Save(token, instanceId);
            _token = token;
            _instanceId = instanceId;
        }

        private void Forget()
        {
            _store.Clear();
            _token = null;
            _instanceId = null;
        }

        private void Settle(Abxr.PairingState state, Abxr.PairingChangeReason reason)
        {
            State = state;
            try
            {
                OnStateChanged?.Invoke(state, reason);
            }
            catch (Exception ex)
            {
                // The state has already moved, and a redeem's callback still has to fire.
                Logcat.Error($"An OnPairingStateChanged handler threw: {ex}");
            }
        }

        private void RequestInput(string error)
        {
            // A handler that answers each request from inside the callback, with a passcode that fails before any
            // request is sent, would otherwise recurse until the stack overflows.
            if (_inputRequestDepth >= MaxNestedInputRequests)
            {
                Logcat.Warning("The OnInputRequested handler keeps resubmitting a failing passcode from inside its callback. " +
                               "The prompt stays open; submit again when the person changes the passcode.");
                return;
            }

            _inputRequestDepth++;
            try
            {
                OnInputRequested?.Invoke(InputType, PromptLabel, null, error);
            }
            finally
            {
                _inputRequestDepth--;
            }
        }

        private string StartRefusal()
        {
            if (!_host.IsPlatformSupported) return UnsupportedPlatform;
            return StateRefusal(promptOpenIsValid: false)
                   ?? ConfigurationProblem()
                   ?? (_host.CanPresentPrompt
                       ? null
                       : "nothing can show the prompt. Install the AbxrLib world-space objects, or set Abxr.OnInputRequested and answer with Abxr.OnInputSubmitted.");
        }

        private string RedeemRefusal() =>
            _host.IsPlatformSupported ? StateRefusal(promptOpenIsValid: true) : UnsupportedPlatform;

        private string StateRefusal(bool promptOpenIsValid)
        {
            switch (State)
            {
                case Abxr.PairingState.Resolving: return "the SDK is still deciding this app's identity. Wait for OnPairingStateChanged.";
                case Abxr.PairingState.Managed: return "the ArborXR client or an org token already identifies this app's organization.";
                case Abxr.PairingState.Paired: return "this app is already paired.";
                case Abxr.PairingState.Redeeming: return "a passcode is already being redeemed.";
                case Abxr.PairingState.Prompting: return promptOpenIsValid ? null : "the pairing prompt is already open.";
                default: return null;
            }
        }

        private string ConfigurationProblem()
        {
            if (string.IsNullOrEmpty(_host.AppToken)) return "pairing needs an App Token, and this app has none.";
            if (!Utils.IsValidUrl(_host.PairingUrl)) return $"pairingUrl \"{_host.PairingUrl}\" isn't an HTTP or HTTPS URL.";
            return null;
        }

        private void LogRedeemOutcome(Abxr.PairingRedeemResult result, PairingHttpResponse response, string instanceId)
        {
            string serverError = PairingOutcomes.ServerError(response.Body);
            string detail = string.IsNullOrEmpty(serverError) ? "" : $": {serverError}";
            switch (result.Error)
            {
                case Abxr.PairingRedeemError.None:
                    Logcat.Info($"Paired as app instance {instanceId}.");
                    break;
                case Abxr.PairingRedeemError.InvalidPasscode:
                    Logcat.Debug("The pairing passcode wasn't accepted.");
                    break;
                case Abxr.PairingRedeemError.BuildRejected:
                    Logcat.Warning($"The pairing service refused this build (HTTP {response.StatusCode}{detail}). Check the App Token, and that pairingUrl ({_host.PairingUrl}) is the Portal API.");
                    break;
                case Abxr.PairingRedeemError.RateLimited:
                    Logcat.Warning($"Too many failed pairing attempts for this app. The pairing service asks to wait {result.RetryAfterSeconds}s.");
                    break;
                case Abxr.PairingRedeemError.Unavailable:
                    Logcat.Warning(response.NetworkError || response.StatusCode == 0
                        ? $"Couldn't reach the pairing service: {response.ErrorDetail}"
                        : $"The pairing service answered HTTP {response.StatusCode} without a usable app instance{detail}.");
                    break;
            }
        }
    }
}

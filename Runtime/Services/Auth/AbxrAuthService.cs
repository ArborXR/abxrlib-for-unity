using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.Auth
{
    public class AbxrAuthService
    {
        // ── Callbacks ────────────────────────────────────────────────
        /// <summary>
        /// Invoked when authentication needs user input (e.g. PIN or username).
        /// Handler receives (type, prompt, domain, error). Show your UI; when the user submits, call the subsystem's SubmitInput (exposed as Abxr.OnInputSubmitted).
        /// For OnInputRequested only one handler is allowed at a time; use assignment (=), not subscribe (+=).
        /// </summary>
        public Action<string, string, string, string> OnInputRequested;
        public Action OnSucceeded;
        public Action<string> OnFailed;

        /// <summary>
        /// Fired only when the re-auth triggered by SetUserData (authMechanism type=custom) completes. Not fired for normal session auth.
        /// Subsystem forwards to Abxr.OnUserDataSyncCompleted. Do not add to public documentation.
        /// </summary>
        internal Action<bool, string> OnUserDataSyncCompleted;

        // ── Public state ─────────────────────────────────────────────
        public bool Authenticated
        {
            get => _sessionState.Authenticated;
            private set => _sessionState.SetAuthenticated(value);
        }
        public AuthResponse ResponseData
        {
            get => _sessionState.ResponseData;
            private set => _sessionState.SetResponseData(value);
        }

        // ── Constants ────────────────────────────────────────────────
        private const string GenericAuthenticationFailureMessage = "Authentication Failed";
        private const string SkipUserAuthenticationInput = "**skip**";

        // ── Internal state ───────────────────────────────────────────
        private readonly RuntimeAuthContext _runtimeAuthContext;
        private readonly AuthSessionState _sessionState = new();
        private readonly AuthHandoffCoordinator _authHandoff;
        private readonly AuthRequestRunner _authRequestRunner;
        private readonly AuthConfigurationLoader _configurationLoader;
        private readonly ReAuthPoller _reAuthPoller;
        private readonly UserDataSyncCoordinator _userDataSyncCoordinator;
        private AuthPayload payload => _runtimeAuthContext.Payload;
        /// <summary>Working copy of the runtime auth mechanism for this session. Its prompt remains the configured UI prompt; submitted user input is passed per request.</summary>
        private AuthMechanism _authMechanism;
        private bool _inputRequestPending;
        /// <summary>True when the API rejected our credentials (401/403 or explicit error). No further auth attempts this session; Authenticate() will no-op and report failure.</summary>
        private bool _credentialsRejectedByApi;

        /// <summary>True when OnInputRequested was invoked and we are waiting for the app to call SubmitInput (OnInputSubmitted). Used so clients can show/hide QR-for-auth UI via IsQRScanForAuthAvailable() without tracking state themselves.</summary>
        internal bool IsInputRequestPending => _inputRequestPending;
        
        private bool _stopping;
        private bool _attemptActive;
        internal bool IsAuthenticationAttemptActive => _attemptActive;
        private bool _isAuthStarted;
        /// <summary>True after <see cref="Authenticate"/> has scheduled <c>AuthenticateCoroutine</c> at least once this process. Use to gate one-time configuration before auth.</summary>
        internal bool HasAuthenticationStarted => _isAuthStarted;
        /// <summary>Set when user auth was satisfied by MDM SSO + access token JWT before <see cref="AuthSucceeded"/> so JWT is not merged twice.</summary>
        private bool _ssoUserDataMergedBeforeAuthSucceeded;

        private readonly MonoBehaviour _runner;
        private readonly IAuthPlatformSource _platformSource;
        
        // Auth handoff for external launcher apps
#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Lets PlayMode tests exercise the receiver path without mutating process command-line args or Android intents.
        /// </summary>
        internal static string TestAuthHandoffPayload
        {
            get => AuthHandoffCoordinator.TestPayload;
            set => AuthHandoffCoordinator.TestPayload = value;
        }

        /// <summary>
        /// Lets PlayMode tests exercise WebGL/Android branches without compiling the test assembly as those player platforms
        /// </summary>
        internal static IAuthPlatformSource TestPlatformSource;
#endif
        /// <summary>After one WebGL auto-submit from the runtime auth context's assessment PIN, further attempts use the normal PIN prompt (retry flow).</summary>
        private bool _webglUrlPinAutoSubmitAttempted;

        private static IAuthPlatformSource ResolvePlatformSourceForRuntime()
        {
#if UNITY_INCLUDE_TESTS
            if (TestPlatformSource != null) return TestPlatformSource;
#endif
            return UnityAuthPlatformSource.Instance;
        }

        public AbxrAuthService(MonoBehaviour coroutineRunner, ArborMdmClient arborMdmClient)
            : this(coroutineRunner, arborMdmClient, ResolvePlatformSourceForRuntime(), new AuthApiClient()) { }

        internal AbxrAuthService(MonoBehaviour coroutineRunner, ArborMdmClient arborMdmClient, IAuthPlatformSource platformSource, IAuthApiClient authApiClient = null)
        {
            _runner = coroutineRunner;
            _platformSource = platformSource ?? UnityAuthPlatformSource.Instance;
            var apiClient = authApiClient ?? new AuthApiClient();

            _runtimeAuthContext = new RuntimeAuthContext(arborMdmClient, _platformSource);
            _authHandoff = new AuthHandoffCoordinator(_platformSource);
            _authRequestRunner = new AuthRequestRunner(_runtimeAuthContext, _sessionState, apiClient,
                () => _authMechanism, CanContinueAuthAttempt, _ => _credentialsRejectedByApi = true);
            _configurationLoader = new AuthConfigurationLoader(
                apiClient, _runtimeAuthContext, _sessionState, CanContinueAuthAttempt);
            _reAuthPoller = new ReAuthPoller(_runner, _sessionState,
                () => _stopping, () => _attemptActive, () => Authenticate());
            _userDataSyncCoordinator = new UserDataSyncCoordinator(_runner, _sessionState, _authRequestRunner,
                () => _stopping, () => _attemptActive, value => _attemptActive = value,
                (success, errorMessage) => OnUserDataSyncCompleted?.Invoke(success, errorMessage));
        }

        // ── Public API ───────────────────────────────────────────────
        
        /// <param name="clearStateFirst">If true (default), clears auth state before running. If false, caller has already cleared and set session (e.g. StartNewSession).</param>
        public void Authenticate(bool clearStateFirst = true)
        {
            if (_stopping || _attemptActive) return;
            if (_credentialsRejectedByApi)
            {
                Logcat.Warning("Authentication was rejected by the API. No further auth attempts will be made this session; app will run without data collection.");
                OnFailed?.Invoke("Authentication was rejected by the API. No further attempts will be made.");
                return;
            }
            _attemptActive = true;
            StopReAuthPolling();
            if (clearStateFirst)
                ClearAuthenticationState();

            // Resolve Configuration + platform credentials/device metadata for this auth attempt.
            var validationError = _runtimeAuthContext.PrepareForAuthentication();
            if (validationError != null)
            {
                FinishAttemptFailure(validationError);
                return;
            }

            // Auth handoff (intent / CLI): on success, session is loaded and AuthenticateCoroutine skips device auth but still runs GET config.
            CheckAuthHandoff();

            // Use the runtime auth mechanism for the user-auth stage after config is fetched
            _authMechanism = _runtimeAuthContext.CopyAuthMechanismForSession();

            _isAuthStarted = true;
            _runner.StartCoroutine(AuthenticateCoroutine());
        }
        
        public void SetSessionId(string sessionId) => _runtimeAuthContext.SetSessionId(sessionId);

        /// <summary>
        /// Submit user input when there is an outstanding OnInputRequested. Called by subsystem (Abxr.OnInputSubmitted).
        /// </summary>
        public void SubmitInput(string input) =>
            SubmitUserAuthInput(input, AuthMechanismResolver.UserInputSource);

        /// <summary>
        /// Submit user input when there is an outstanding OnInputRequested, together with the source that produced it (for example, "user" or "QRlms").
        /// This keeps the source scoped to one auth request instead of mutating the session auth mechanism.
        /// </summary>
        public void SubmitUserAuthInput(string input, string inputSource = null)
        {
            if (!_inputRequestPending)
            {
                Logcat.Warning("OnInputSubmitted was ignored: no input request is pending. Call OnInputSubmitted only once, after OnInputRequested has been invoked.");
                return;
            }

            if (input == SkipUserAuthenticationInput)
            {
                SkipUserAuthentication();
                return;
            }

            _inputRequestPending = false;
            AuthenticateUserInput(input, AuthMechanismResolver.NormalizeInputSource(inputSource));
        }

        private void SkipUserAuthentication()
        {
            _inputRequestPending = false;
            Logcat.Warning("Skipping user authentication.");
            KeyboardHandler.Destroy();
            AuthSucceeded();
        }
        
        private void PromptForInput(string error = "") =>
            PromptForInput(_authMechanism.type, _authMechanism.prompt, _authMechanism.domain, error);

        private void PromptForInput(string type, string prompt, string domain, string error = "")
        {
            _inputRequestPending = true;
            OnInputRequested?.Invoke(type, prompt, domain, error ?? "");
        }
        
        private void AuthenticateUserInput(string input, string inputSource = null)
        {
            string configuredType = _authMechanism.type;
            string configuredPrompt = _authMechanism.prompt;
            string configuredDomain = _authMechanism.domain;
            string submittedAuthPrompt = AuthRequestBuilder.BuildSubmittedAuthPrompt(_authMechanism, input) ?? "";
            string submittedInputSource = AuthMechanismResolver.NormalizeInputSource(inputSource);

            _runner.StartCoroutine(AuthRequestCoroutine(AuthRequestStage.UserInput, (success, errorMessage) =>
            {
                if (success)
                {
                    _runtimeAuthContext.ClearWebGlAssessmentPin();
                    KeyboardHandler.Destroy();
                    AuthSucceeded();
                }
                else
                {
                    KeyboardHandler.StopProcessing();
                    KeyboardHandler.ShowPinPad();

                    string completedError = !string.IsNullOrWhiteSpace(errorMessage) ? errorMessage : GenericAuthenticationFailureMessage;

                    OnFailed?.Invoke(completedError);
                    PromptForInput(configuredType, configuredPrompt, configuredDomain, GenericAuthenticationFailureMessage);
                }
            }, submittedAuthPrompt: submittedAuthPrompt, submittedInputSource: submittedInputSource));
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null) =>
            AuthHeaderSigner.TrySetAuthHeaders(request, ResponseData, json);

        private void StopReAuthPolling() => _reAuthPoller.Stop();

        private bool CanContinueAuthAttempt() => !_stopping && _attemptActive;

        private void FinishAttemptFailure(string message)
        {
            _attemptActive = false;
            OnFailed?.Invoke(message);
        }

        private void FinishAttemptSuccess()
        {
            _attemptActive = false;
            OnSucceeded?.Invoke();
        }

        public void Shutdown()
        {
            _stopping = true;
            StopReAuthPolling();
            _attemptActive = false;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Clear auth/session state without destroying the runner.
        /// </summary>
        internal void ResetForTest()
        {
            TestAuthHandoffPayload = null;
            TestPlatformSource = null;
            _stopping = false;
            _attemptActive = false;
            _isAuthStarted = false;
            StopReAuthPolling();
            ClearSessionAndPrepareForNew();
        }
        
        internal string GetAuthMechanismPromptForTest() => _authMechanism?.prompt;

        internal void SetTokenExpiryForTest(DateTime tokenExpiryUtc) => _sessionState.SetTokenExpiryUtc(tokenExpiryUtc);

        internal ReAuthPoller ReAuthPollerForTest => _reAuthPoller;
#endif
        
        // ── Core auth flow (coroutine) ───────────────────────────────

        private IEnumerator AuthenticateCoroutine()
        {
            bool authOk;
            string authError = null;
            if (_authHandoff.TryConsumeDeferredDeviceAuth())
            {
                authOk = true;
            }
            else
            {
                authOk = false;
                yield return _runner.StartCoroutine(AuthRequestCoroutine(AuthRequestStage.Device, (ok, err) =>
                {
                    authOk = ok;
                    authError = err;
                }));
                if (!authOk)
                {
                    string message = !string.IsNullOrEmpty(authError)
                        ? authError
                        : "Initial authentication request failed";
                    FinishAttemptFailure(message);
                    yield break;
                }
            }

            // Start re-auth polling
            StartReAuthPolling();

            // Fetch config (non-auth fields + optional authMechanism). Handoff and config-failure paths treat user auth as not required.
            bool configOk = false;
            string configFailureDetail = null;
            yield return _runner.StartCoroutine(GetConfigurationCoroutine((ok, detail) => { configOk = ok; configFailureDetail = detail; }));
            if (!configOk)
            {
                Logcat.Warning(string.IsNullOrEmpty(configFailureDetail)
                    ? "GET config failed; continuing with Configuration defaults and no user auth prompt (authMechanism cleared)."
                    : $"GET config failed ({configFailureDetail}); continuing with Configuration defaults and no user auth prompt (authMechanism cleared).");
                ClearUserAuthMechanismForSession();
            }
            else if (_authHandoff.SessionUsedHandoff)
            {
                // Session identity came from the launcher; do not require a second PIN/email step from GET config.
                ClearUserAuthMechanismForSession();
            }
            else if (_platformSource.IsWebGlPlayer)
            {
                ApplyAssessmentPinFromUrlQueryIfPresent();
            }

            if (_stopping || !_attemptActive)
            {
                FinishAttemptFailure("Auth stopped or attempt inactive");
                yield break;
            }
            
            if (AuthMechanismResolver.NeedsUserAuthentication(_authMechanism))
            {
                if (_platformSource.IsWebGlPlayer && !_webglUrlPinAutoSubmitAttempted && _runtimeAuthContext.HasWebGlAssessmentPin)
                {
                    _webglUrlPinAutoSubmitAttempted = true;
                    Logcat.Info("User authentication: submitting pre-filled assessment PIN (org token JWT or URL query, first attempt).");
                    AuthenticateUserInput(_runtimeAuthContext.WebGlAssessmentPin, AuthMechanismResolver.UserInputSource);
                }
                else
                {
                    if (TryCompleteUserAuthUsingMdmSsoIdentity())
                        AuthSucceeded();
                    else
                        PromptForInput();
                }
            }
            else
            {
                AuthSucceeded();
            }
        }

        /// <summary>Attempts auth via REST. Invokes onComplete(success, errorMessage). Device auth can retry transport/server transient failures; user-auth and SetUserData re-auth are one-shot.</summary>
        private IEnumerator AuthRequestCoroutine(AuthRequestStage stage, Action<bool, string> onComplete, string submittedAuthPrompt = null, string submittedInputSource = null) =>
            _authRequestRunner.Run(stage, onComplete, submittedAuthPrompt, submittedInputSource);

        /// <summary>
        /// When <see cref="Abxr.GetIsAuthenticated"/> is true and <see cref="Abxr.GetAccessToken"/> is a JWT with usable identity claims,
        /// merges SSO claims into <see cref="ResponseData"/>, clears the auth mechanism for this step, and returns true so the caller can call <see cref="AuthSucceeded"/> without prompting.
        /// Skipped when <see cref="Configuration.enableLearnerLauncherMode"/> is on so assessment PIN / <see cref="Abxr.OnInputSubmitted"/> is not bypassed.
        /// </summary>
        private bool TryCompleteUserAuthUsingMdmSsoIdentity()
        {
            if (Configuration.Instance != null && Configuration.Instance.enableLearnerLauncherMode)
                return false;

            string token = GetAuthenticatedAccessToken();
            if (!SsoUserDataMerger.AccessTokenHasUsableIdentity(token)) return false;

            if (!_sessionState.TryMergeAccessTokenIntoUserData(token))
            {
                Logcat.Warning("MDM SSO: access token did not merge into userData; continuing with auth mechanism prompt.");
                return false;
            }

            ClearUserAuthMechanismForSession();
            _ssoUserDataMergedBeforeAuthSucceeded = true;
            Logcat.Info("MDM SSO user identity applied; skipping auth mechanism prompt (GET config authMechanism ignored for this session).");
            return true;
        }

        private static string GetAuthenticatedAccessToken()
        {
            if (!Abxr.GetIsAuthenticated()) return null;

            string token = Abxr.GetAccessToken();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        /// <summary>
        /// Parses and applies an auth response. REST auth success requires both token and secret,
        /// and handoff uses the same validation path so session data has one source of truth.
        /// </summary>
        /// <param name="responseText"></param>
        /// <param name="stageLabel">Optional label for logging, e.g. "device-auth", "user-auth", or "handoff".</param>
        /// <param name="handoff">When true, marks the session as supplied by an external handoff after validation.</param>
        private bool ApplyAuthResponse(string responseText, string stageLabel = null, bool handoff = false)
        {
            if (!_sessionState.TryApply(responseText, stageLabel)) return false;
            if (handoff) _authHandoff.MarkApplied(ResponseData);
            return true;
        }

        // ── GET /v1/storage/config ───

        private IEnumerator GetConfigurationCoroutine(Action<bool, string> onComplete)
        {
            yield return _configurationLoader.Load((ok, detail, authMechanism) =>
            {
                if (ok) _authMechanism = authMechanism;
                onComplete(ok, detail);
            });
        }

        private void StartReAuthPolling() => _reAuthPoller.Start();

        private void AuthSucceeded()
        {
            bool ssoUserDataChanged = _sessionState.MarkAuthenticatedAndMergeSsoUserData(
                _ssoUserDataMergedBeforeAuthSucceeded, GetAuthenticatedAccessToken());
            _ssoUserDataMergedBeforeAuthSucceeded = false;

            FinishAttemptSuccess();
            Logcat.Info("Authenticated successfully");
            // Push merged MDM SSO claims to the API via the same REST auth path as SetUserData (custom re-auth); completion is OnUserDataSyncCompleted only.
            if (ssoUserDataChanged) SetUserData();
        }

        /// <summary>For handoff receivers and GET-config failure: no keyboard/PIN; keep Configuration asset defaults for other fields.</summary>
        private void ClearUserAuthMechanismForSession()
        {
            _authMechanism = null;
            _runtimeAuthContext.ClearAuthMechanismForSession();
        }

        /// <summary>
        /// WebGL: when a pre-filled PIN was resolved (org token JWT <c>pin</c> claim, or <c>assessment_pin</c>/<c>assessmentPin</c> in the page URL), force user authentication to type <c>assessmentPin</c> after GET config succeeds (non-handoff).
        /// Skipped for production_custom by the runtime auth context.</summary>
        private void ApplyAssessmentPinFromUrlQueryIfPresent()
        {
            if (!_runtimeAuthContext.TryForceWebGlAssessmentPinAuthMechanism(out var sessionMechanism)) return;

            _authMechanism = sessionMechanism;
            _webglUrlPinAutoSubmitAttempted = false;
            Logcat.Debug("User authentication: pre-filled assessment PIN available; mechanism set to assessmentPin (auto-submit on first attempt).");
        }

        private void ClearAuthenticationState()
        {
            _sessionState.Clear();
            _runtimeAuthContext.ClearAuthenticationState();
            _authHandoff.Clear();
            _authMechanism = null;
            _inputRequestPending = false;
            _credentialsRejectedByApi = false;
            _ssoUserDataMergedBeforeAuthSucceeded = false;
            _webglUrlPinAutoSubmitAttempted = false;
        }

        /// <summary>
        /// Clears all auth/session state and assigns a new session ID. Used by StartNewSession before re-authenticating.
        /// Call Authenticate(clearStateFirst: false) after this so the new session ID is preserved.
        /// </summary>
        internal void ClearSessionAndPrepareForNew()
        {
            ClearAuthenticationState();
            _runtimeAuthContext.PrepareNewSession();
        }
        
        /// <summary>
        /// Check for authentication handoff from external launcher apps.
        /// Invalid payload: logs and returns (same as if no handoff); normal device authentication runs in AuthenticateCoroutine.
        /// </summary>
        private void CheckAuthHandoff()
        {
            if (!_authHandoff.TryReadIncomingPayload(out string normalized)) return;

            Logcat.Info("Processing authentication handoff from external launcher");
            if (!ApplyAuthResponse(normalized, AuthHandoffPayload.StageLabel, handoff: true))
                Logcat.Warning("auth_handoff was present but the session could not be applied; continuing with device authentication.");
        }

        public bool SessionUsedAuthHandoff() => _authHandoff.SessionUsedHandoff;

        /// <summary>
        /// Builds the JSON payload passed via the auth_handoff Android intent extra.
        /// Includes all session credentials plus re-auth fields so the receiving app can adopt the authenticated session.
        /// </summary>
        internal string GetHandoffJson(bool includeReturnToPackage = false) =>
            AuthHandoffCoordinator.BuildOutgoingPayload(ResponseData, payload, _sessionState.TokenExpiryUtc, Authenticated,
                includeReturnToPackage ? Application.identifier ?? "" : null);

        /// <summary>Returns the stored returnToPackage from the handoff (so the assessment app can launch back to the launcher), then clears it so it is only used once.</summary>
        internal string GetAndClearReturnToPackage() => _authHandoff.GetAndClearReturnToPackage();
        
        /// <summary>
        /// Update user data (userData only) and reauthenticate to sync with server.
        /// Session userId is read-only and set only by the backend. Merges existing UserData with the optional id (userData.id) and additionalUserData, then sends via re-auth.
        /// </summary>
        /// <param name="id">Optional primary user identifier (maps to userData.id); can be null to clear or when only updating additional fields.</param>
        /// <param name="additionalUserData">Optional key-value pairs to merge with existing UserData (overwrites existing keys). May be empty to clear all userData.</param>
        public void SetUserData(string id = null, Dictionary<string, string> additionalUserData = null) =>
            _userDataSyncCoordinator.SetUserData(id, additionalUserData);
        
        /// <summary>Returns enableAutoStartModules from runtime auth (loaded from Configuration/runtime sources).</summary>
        internal bool GetEffectiveEnableAutoStartModules() => _runtimeAuthContext.GetEffectiveEnableAutoStartModules();

        /// <summary>Returns enableAutoAdvanceModules from runtime auth (loaded from Configuration/runtime sources).</summary>
        internal bool GetEffectiveEnableAutoAdvanceModules() => _runtimeAuthContext.GetEffectiveEnableAutoAdvanceModules();

        /// <summary>Returns enableReturnTo from runtime auth (loaded from Configuration/runtime sources).</summary>
        internal bool GetEffectiveEnableReturnTo() => _runtimeAuthContext.GetEffectiveEnableReturnTo();

        /// <summary>Returns enableAutoStartAuthentication from runtime auth.</summary>
        internal bool GetEnableAutoStartAuthentication() => _runtimeAuthContext.GetEnableAutoStartAuthentication();

        // ── Runtime auth overrides (Abxr.SetOrgId / SetAuthSecret / SetDeviceId) ─────

        /// <summary>Updates runtime auth orgId. Called by subsystem when Abxr.SetOrgId() is used.</summary>
        internal void SetRuntimeAuthOrgId(string value) => _runtimeAuthContext.SetRuntimeAuthOrgId(value);

        /// <summary>Updates runtime auth authSecret. Called by subsystem when Abxr.SetAuthSecret() is used.</summary>
        internal void SetRuntimeAuthAuthSecret(string value) => _runtimeAuthContext.SetRuntimeAuthAuthSecret(value);

        /// <summary>Updates runtime auth deviceId. Called by subsystem when Abxr.SetDeviceId() is used.</summary>
        internal void SetRuntimeAuthDeviceId(string value) => _runtimeAuthContext.SetRuntimeAuthDeviceId(value);
    }
}

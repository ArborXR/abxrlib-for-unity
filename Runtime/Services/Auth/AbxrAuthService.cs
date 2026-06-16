using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Services;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.UI.Keyboard;
using Newtonsoft.Json;
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
        public bool Authenticated { get; private set; }
        public AuthResponse ResponseData { get; private set; } = new();

        // ── Constants ────────────────────────────────────────────────
        private const float ReAuthPollSeconds = 60f;
        private const int ReAuthThresholdSeconds = 120;
        private const string GenericAuthenticationFailureMessage = "Authentication Failed";
        private static readonly WaitForSeconds ReAuthWait = new(ReAuthPollSeconds);

        // ── Internal state ───────────────────────────────────────────
        private readonly AuthPayload _payload;
        /// <summary>Runtime auth values loaded from Configuration and updated by GetArborData, WebGL/desktop query data, intent, and SetOrgId/SetAuthSecret.</summary>
        private readonly RuntimeAuthConfig _runtimeAuth = new();
        /// <summary>Working copy of _runtimeAuth.authMechanism for this session. Its prompt remains the configured UI prompt; submitted user input is passed per request.</summary>
        private AuthMechanism _authMechanism;
        private DateTime _tokenExpiry = DateTime.MinValue;
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
        private Coroutine _reAuthCoroutine;
        private Dictionary<string, string> _userData;
        /// <summary>True while the auth request is from SetUserData re-auth; ensures we send type=custom with userData instead of current _authMechanism (e.g. email).</summary>
        private bool _setUserDataReAuthActive;

        /// <summary>Set when user auth was satisfied by MDM SSO + access token JWT before <see cref="AuthSucceeded"/> so JWT is not merged twice.</summary>
        private bool _ssoUserDataMergedBeforeAuthSucceeded;

        private readonly MonoBehaviour _runner;
        private readonly ArborMdmClient _arborMdmClient;
        private readonly IAuthPlatformSource _platformSource;
        private AbxrRestService _restService;
        
        // Auth handoff for external launcher apps
#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Lets PlayMode tests exercise the receiver path without mutating process command-line args or Android intents
        /// </summary>
        internal static string TestAuthHandoffPayload;

        /// <summary>
        /// Lets PlayMode tests exercise WebGL/Android branches without compiling the test assembly as those player platforms
        /// </summary>
        internal static IAuthPlatformSource TestPlatformSource;
#endif
        private bool _sessionUsedAuthHandoff;
        private string _returnToPackage;
        /// <summary>True when a valid auth_handoff was parsed; AuthenticateCoroutine skips device AuthRequestCoroutine and completes after GET config (same as normal flow).</summary>
        private bool _deviceAuthDeferredByHandoff;

        /// <summary>WebGL: pre-filled assessment PIN from org token JWT claim <c>pin</c> (preferred) or from <c>assessment_pin</c>/<c>assessmentPin</c> URL query. When set, GET config can be overridden to assessmentPin and the first user-auth attempt auto-submits this value.</summary>
        private string _webglQueryAssessmentPin;
        /// <summary>After one auto-submit from <see cref="_webglQueryAssessmentPin"/>, further attempts use the normal PIN prompt (retry flow).</summary>
        private bool _webglUrlPinAutoSubmitAttempted;

        private static IAuthPlatformSource ResolvePlatformSourceForRuntime()
        {
#if UNITY_INCLUDE_TESTS
            if (TestPlatformSource != null) return TestPlatformSource;
#endif
            return UnityAuthPlatformSource.Instance;
        }

        public AbxrAuthService(MonoBehaviour coroutineRunner, ArborMdmClient arborMdmClient)
            : this(coroutineRunner, arborMdmClient, ResolvePlatformSourceForRuntime()) { }

        internal AbxrAuthService(MonoBehaviour coroutineRunner, ArborMdmClient arborMdmClient, IAuthPlatformSource platformSource)
        {
            _runner = coroutineRunner;
            _arborMdmClient = arborMdmClient;
            _platformSource = platformSource ?? UnityAuthPlatformSource.Instance;

            _payload = new AuthPayload
            {
                partner = "none",
                deviceId = SystemInfo.deviceUniqueIdentifier,
                sessionId = Guid.NewGuid().ToString(),
                osVersion = SystemInfo.operatingSystem,
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                abxrLibType = "unity",
                abxrLibVersion = AbxrLibVersion.Version
            };
            
            GetConfigData();
            if (_platformSource.IsAndroidPlayer)
            {
                GetArborData();
                ApplyAndroidIntentOrgTokenIfAvailable();
            }
            else if (_platformSource.IsWebGlPlayer)
            {
                GetWebGlQueryData();
                ApplyWebGlDeviceIdFromPlatform();
            }
            else if (_platformSource.IsStandalonePlayer)
            {
                GetDesktopQueryData();
            }
            SetSessionData();
        }

        internal void SetRestService(AbxrRestService restService) => _restService = restService;

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

            // Load runtime auth from Configuration, then apply GetArborData/query data/intent so runtime config reflects all sources.
            LoadRuntimeAuthFromConfig();
            if (_platformSource.IsAndroidPlayer) GetArborData();
            
            // Apply Abxr.SetOrgId/SetAuthSecret/SetDeviceId into runtime auth (after config load so overrides win; after GetArborData so MDM can have set org token first).
            ApplyAbxrOverridesToRuntimeAuth();

            // When using app tokens with no org token yet, build dynamic org token from overrides (SetOrgId/SetAuthSecret) or from MDM (already set in GetArborData). Same logic as GetArborData but for when MDM is not connected—overrides supply orgId and authSecret (fingerprint) to sign the JWT.
            TrySetDynamicOrgToken(_runtimeAuth.orgId, _runtimeAuth.authSecret);

            if (_platformSource.IsAndroidPlayer) ApplyAndroidIntentOrgTokenIfAvailable(copyToPayload: false);
            else if (_platformSource.IsWebGlPlayer) GetWebGlQueryData();
            else if (_platformSource.IsStandalonePlayer) GetDesktopQueryData();
            
            var validationError = _runtimeAuth.PreparePayloadForAuth(_payload);
            if (validationError != null)
            {
                _attemptActive = false;
                OnFailed?.Invoke(validationError);
                return;
            }

            // Auth handoff (intent / CLI): on success, session is loaded and AuthenticateCoroutine skips device auth but still runs GET config.
            CheckAuthHandoff();

            // Use the runtime auth mechanism for the user-auth stage after config is fetched
            _authMechanism = CopyAuthMechanism(_runtimeAuth.authMechanism);

            _isAuthStarted = true;
            _runner.StartCoroutine(AuthenticateCoroutine());
        }
        
        public void SetSessionId(string sessionId) => _payload.sessionId = sessionId;

        /// <summary>
        /// Submit user input when there is an outstanding OnInputRequested. Called by subsystem (Abxr.OnInputSubmitted).
        /// If no input was requested, this is a no-op. Empty or whitespace-only input is rejected and OnInputRequested is re-invoked with an error (no REST call).
        /// </summary>
        public void SubmitInput(string input)
        {
            if (!_inputRequestPending)
            {
                Logcat.Warning("OnInputSubmitted was ignored: no input request is pending. Call OnInputSubmitted only once, after OnInputRequested has been invoked.");
                return;
            }

            if (input == "**skip**")
            {
                _inputRequestPending = false;
                Logcat.Warning("Skipping user authentication.");
                KeyboardHandler.Destroy();
                AuthSucceeded();
                return;
            }
            
            _inputRequestPending = false;
            KeyboardAuthenticate(input);
        }
        
        private void RequestKeyboardInput(bool firstAttempt = true)
        {
            _inputRequestPending = true;
            OnInputRequested?.Invoke(_authMechanism.type, _authMechanism.prompt, _authMechanism.domain, firstAttempt ? "" : GenericAuthenticationFailureMessage);
        }
        
        public void KeyboardAuthenticate(string input)
        {
            string configuredType = _authMechanism.type;
            string configuredPrompt = _authMechanism.prompt;
            string configuredDomain = _authMechanism.domain;
            string submittedAuthPrompt = BuildSubmittedAuthPrompt(input) ?? "";

            _runner.StartCoroutine(AuthRequestCoroutine((success, errorMessage) =>
            {
                if (success)
                {
                    _webglQueryAssessmentPin = null;
                    KeyboardHandler.Destroy();
                    AuthSucceeded();
                }
                else
                {
                    KeyboardHandler.StopProcessing();
                    KeyboardHandler.ShowPinPad();
                    SetInputSource("user");  // In case it was changed by QR Scanner
                    
                    string completedError = !string.IsNullOrWhiteSpace(errorMessage) ? errorMessage : GenericAuthenticationFailureMessage;

                    OnFailed?.Invoke(completedError);
                    _inputRequestPending = true;
                    OnInputRequested?.Invoke(configuredType, configuredPrompt, configuredDomain, GenericAuthenticationFailureMessage);
                }
            }, withRetry: false, submittedAuthPrompt: submittedAuthPrompt));
        }

        private string BuildSubmittedAuthPrompt(string input)
        {
            // Server does not use domain from payload. Domain is client-only for prompting and building this value.
            if (_authMechanism != null && _authMechanism.type == "email" &&
                !string.IsNullOrEmpty(_authMechanism.domain) && input != null && !input.Contains("@"))
            {
                return input + "@" + _authMechanism.domain;
            }

            return input;
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null)
        {
            if (ResponseData == null || string.IsNullOrEmpty(ResponseData.Token) || string.IsNullOrEmpty(ResponseData.Secret))
            {
                Logcat.Error("Cannot set auth headers - authentication tokens are missing");
                return;
            }

            request.SetRequestHeader("Authorization", "Bearer " + ResponseData.Token);
        
            string unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            request.SetRequestHeader("x-abxrlib-timestamp", unixTimeSeconds);
        
            string hashString = ResponseData.Token + ResponseData.Secret + unixTimeSeconds;
            if (!string.IsNullOrEmpty(json))
            {
                uint crc = Utils.ComputeCRC(json);
                hashString += crc;
            }
        
            request.SetRequestHeader("x-abxrlib-hash", Utils.ComputeSha256Hash(hashString));
        }

        private void StopReAuthPolling()
        {
            if (_reAuthCoroutine != null && _runner != null)
            {
                _runner.StopCoroutine(_reAuthCoroutine);
                _reAuthCoroutine = null;
            }
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
#endif
        
        // ── Core auth flow (coroutine) ───────────────────────────────

        private IEnumerator AuthenticateCoroutine()
        {
            bool authOk;
            string authError = null;
            if (_deviceAuthDeferredByHandoff)
            {
                _deviceAuthDeferredByHandoff = false;
                authOk = true;
            }
            else
            {
                authOk = false;
                yield return _runner.StartCoroutine(AuthRequestCoroutine((ok, err) =>
                {
                    authOk = ok;
                    authError = err;
                }, withRetry: true));
                if (!authOk)
                {
                    _attemptActive = false;
                    string message = !string.IsNullOrEmpty(authError)
                        ? authError
                        : "Initial authentication request failed";
                    OnFailed?.Invoke(message);
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
            else if (_sessionUsedAuthHandoff)
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
                _attemptActive = false;
                OnFailed?.Invoke("Auth stopped or attempt inactive");
                yield break;
            }
            
            if (NeedsUserAuthentication(_authMechanism))
            {
                if (_platformSource.IsWebGlPlayer && !_webglUrlPinAutoSubmitAttempted && !string.IsNullOrEmpty(_webglQueryAssessmentPin))
                {
                    _webglUrlPinAutoSubmitAttempted = true;
                    Logcat.Info("User authentication: submitting pre-filled assessment PIN (org token JWT or URL query, first attempt).");
                    KeyboardAuthenticate(_webglQueryAssessmentPin);
                }
                else
                {
                    if (TryCompleteUserAuthUsingMdmSsoIdentity())
                        AuthSucceeded();
                    else
                        RequestKeyboardInput();
                }
            }
            else
            {
                AuthSucceeded();
            }
        }

        private static string DescribeAuthFailure(RestAuthResult result)
        {
            if (result == null) return "Authentication request failed.";
            return AuthResponseParser.DescribeFailure(result.Body, result.StatusCode);
        }

        /// <summary>Attempts auth via REST. Invokes onComplete(success, errorMessage). Device auth can retry transport/server transient failures; user-auth and SetUserData re-auth are one-shot.</summary>
        private IEnumerator AuthRequestCoroutine(Action<bool, string> onComplete, bool withRetry = true, string submittedAuthPrompt = null)
        {
            if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }
            if (_restService == null) { onComplete(false, "REST service not set"); yield break; }

            var validationError = _runtimeAuth.PreparePayloadForAuth(_payload);
            if (validationError != null) { onComplete(false, validationError); yield break; }

            if (string.IsNullOrEmpty(_payload.sessionId)) _payload.sessionId = Guid.NewGuid().ToString();
            var authMech = CreateAuthMechanismDict(submittedAuthPrompt);
            // Device authentication (withRetry): never send authMechanism. User auth and SetUserData re-auth send only explicit supported request shapes.
            _payload.authMechanism = withRetry ? null : IsAuthMechanismMeaningful(authMech) ? authMech : null;

            int retryIntervalSeconds = Math.Max(1, Configuration.Instance.sendRetryIntervalSeconds);
            int maxRetries = Math.Max(0, Configuration.Instance.sendRetriesOnFailure);
            int retriesAttempted = 0;
            var restService = _restService;

            while (true)
            {
                if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }

                bool isDeviceAuth = _payload.authMechanism == null;
                string stageLabel = isDeviceAuth ? "device-auth" : "user-auth";
                if (_payload.authMechanism != null)
                {
                    var authMechLog = string.Join(", ", _payload.authMechanism.Select(kvp => kvp.Key + "=" + (string.IsNullOrEmpty(kvp.Value) ? "(empty)" : kvp.Value)));
                    string configuredPrompt = _authMechanism?.prompt ?? "(null)";
                    Logcat.Debug($"Auth request ({stageLabel}): authMechanism=[{authMechLog}], configuredPrompt={configuredPrompt} (submittedPromptLength={submittedAuthPrompt?.Length ?? 0})");
                }
                else
                    Logcat.Debug($"Auth request ({stageLabel}): no auth_mechanism");

                RestAuthResult result = null;
                yield return restService.AuthRequestCoroutine(_payload, r => result = r);

                if (result != null && result.Success && ApplyAuthResponse(result.Response, stageLabel))
                {
                    onComplete(true, null);
                    yield break;
                }

                string message = DescribeAuthFailure(result);

                if (!withRetry)
                {
                    onComplete(false, message);
                    yield break;
                }

                if (result != null && result.AuthRejected)
                {
                    _credentialsRejectedByApi = true;
                    Logcat.Warning($"AuthRequest failed: {message} No further auth attempts will be made this session.");
                    onComplete(false, message);
                    yield break;
                }

                if (result == null || !result.Retryable)
                {
                    onComplete(false, message);
                    yield break;
                }

                if (retriesAttempted >= maxRetries)
                {
                    onComplete(false, message);
                    yield break;
                }

                retriesAttempted++;
                Logcat.Warning($"AuthRequest failed: {message} Retrying in {retryIntervalSeconds} seconds...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

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

            if (ResponseData == null) return false;
            ResponseData.UserData ??= new Dictionary<string, string>();
            if (!SsoUserDataMerger.TryMergeAccessTokenIntoUserData(token, ResponseData.UserData))
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
            if (!AuthResponseParser.TryParseSuccess(responseText, out AuthResponse postResponse, out string parseError))
            {
                if (AuthResponseParser.IsParseFailure(parseError))
                    Logcat.Error($"Authentication response handling failed: {parseError}");
                return false;
            }

            return ApplyAuthResponse(postResponse, stageLabel, handoff);
        }

        /// <summary>
        /// Applies an already-parsed auth response. REST transport parses normal responses before invoking
        /// this method, so successful REST auth does not deserialize the same body twice.
        /// </summary>
        private bool ApplyAuthResponse(AuthResponse postResponse, string stageLabel = null, bool handoff = false)
        {
            if (!AuthResponse.IsValidSuccess(postResponse)) return false;

            try
            {
                if (!TrySetTokenExpiryFromJwt(postResponse.Token))
                    return false;

                ResponseData = postResponse;
                if (ResponseData.Modules?.Count > 1)
                    ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
                ResponseData.UserData ??= new Dictionary<string, string>();
                _userData = new Dictionary<string, string>(ResponseData.UserData);

                string stagePrefix = !string.IsNullOrEmpty(stageLabel) ? $" ({stageLabel})" : "";
                var userDataLog = ResponseData.UserData == null
                    ? "(null)"
                    : string.Join(", ", ResponseData.UserData.Select(kvp => kvp.Key + "=" + kvp.Value));
                Logcat.Debug($"Auth response{stagePrefix}: userId={ResponseData.UserId ?? "(null)"}, userData=[{userDataLog}], token={(!string.IsNullOrEmpty(ResponseData.Token) ? "present" : "(null)")}, appId={ResponseData.AppId ?? "(null)"}, modules={ResponseData.Modules?.Count ?? 0}");

                if (handoff)
                {
                    Logcat.Info($"Auth handoff applied. Modules: {ResponseData.Modules?.Count ?? 0}");
                    _sessionUsedAuthHandoff = true;
                    _returnToPackage = ResponseData.ReturnToPackage;
                    _deviceAuthDeferredByHandoff = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logcat.Error($"Authentication response handling failed: {ex.Message}");
                return false;
            }
        }

        private bool TrySetTokenExpiryFromJwt(string token)
        {
            Dictionary<string, object> decodedJwt = Utils.DecodeJwt(token);
            if (decodedJwt == null)
            {
                Logcat.Error("Failed to decode JWT token");
                return false;
            }
            if (!decodedJwt.TryGetValue("exp", out var expValue) || expValue == null)
            {
                Logcat.Error("JWT token missing expiration field");
                return false;
            }
            try
            {
                _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(expValue)).UtcDateTime;
                return true;
            }
            catch (Exception ex)
            {
                Logcat.Error($"Invalid JWT token expiration: {ex.Message}");
                return false;
            }
        }

        // ── GET /v1/storage/config ───

        private IEnumerator GetConfigurationCoroutine(Action<bool, string> onComplete)
        {
            if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }
            if (_restService == null) { onComplete(false, "REST service not set"); yield break; }

            string configJson = null;
            string failureDetail = null;
            yield return _restService.GetConfigCoroutine((ok, json) =>
            {
                if (ok) configJson = json; else failureDetail = json;
            });

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<ConfigPayload>(configJson);
                    if (config != null)
                    {
                        Configuration.Instance.ApplyConfigPayload(config);

                        _runtimeAuth.authMechanism = CopyAuthMechanism(config.authMechanism);
                        if (Configuration.Instance.enableLearnerLauncherMode && !IsAuthMechanismType(_runtimeAuth.authMechanism, "assessmentPin"))
                        {
                            _runtimeAuth.authMechanism = new AuthMechanism
                            {
                                type = "assessmentPin",
                                prompt = config.authMechanism?.prompt ?? "",
                                domain = config.authMechanism?.domain ?? "",
                                inputSource = "user"
                            };
                        }

                        _authMechanism = CopyAuthMechanism(_runtimeAuth.authMechanism);
                        if (NeedsUserAuthentication(_authMechanism))
                        {
                            string authType = _authMechanism?.type ?? "";
                            Logcat.Info("User Authentication Required.");
                            Logcat.Debug($" - Type: {authType} & Prompt: {(_authMechanism?.prompt ?? "")}");
                        }
                        else
                            Logcat.Info("User authentication not required. Using anonymous session.");
                        onComplete(true, null);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    failureDetail = ex.Message;
                    Logcat.Error($"GetConfiguration response handling failed: {ex.Message}");
                }
            }

            onComplete(false, failureDetail ?? "no config returned");
        }

        private void StartReAuthPolling()
        {
            StopReAuthPolling();
            _reAuthCoroutine = _runner.StartCoroutine(ReAuthPollCoroutine());
        }

        private IEnumerator ReAuthPollCoroutine()
        {
            while (!_stopping)
            {
                yield return ReAuthWait;

                if (_tokenExpiry == DateTime.MinValue || _attemptActive) continue;
                if (_tokenExpiry - DateTime.UtcNow <= TimeSpan.FromSeconds(ReAuthThresholdSeconds))
                {
                    Authenticate();
                }
            }
        }

        /// <summary>Load auth-related values from Configuration into _runtimeAuth. GetArborData/query data/intent will update _runtimeAuth next.</summary>
        private void LoadRuntimeAuthFromConfig()
        {
            var s = Configuration.Instance;
            _runtimeAuth.useAppTokens = s.useAppTokens;
            _runtimeAuth.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : "production";
            if (s.useAppTokens)
            {
                _runtimeAuth.appToken = s.appToken;
                _runtimeAuth.orgToken = string.Equals(s.buildType, "production", StringComparison.OrdinalIgnoreCase) ? null : s.orgToken;
            }
            else
            {
                _runtimeAuth.appId = s.appID;
                if (string.Equals(s.buildType, "production", StringComparison.OrdinalIgnoreCase))
                {
                    _runtimeAuth.orgId = null;
                    _runtimeAuth.authSecret = null;
                }
                else
                {
                    _runtimeAuth.orgId = s.orgID;
                    _runtimeAuth.authSecret = s.authSecret;
                }
            }
            SetDefaultRuntimeDeviceContext();
        }

        private void SetDefaultRuntimeDeviceContext()
        {
            string deviceIdFromSubsystem = _platformSource.GetCurrentDeviceId();
            _runtimeAuth.deviceId = !string.IsNullOrEmpty(deviceIdFromSubsystem) ? deviceIdFromSubsystem : _payload.deviceId;
            _runtimeAuth.partner = "none";
            _runtimeAuth.tags = null;
        }

        private void ApplyWebGlDeviceIdFromPlatform()
        {
            string webGlDeviceId = _platformSource.GetOrCreateWebGlDeviceId();
            if (string.IsNullOrEmpty(webGlDeviceId)) return;

            _payload.deviceId = webGlDeviceId;
            _runtimeAuth.deviceId = webGlDeviceId;
        }

        private bool TrySetDynamicOrgToken(string orgId, string authSecret, bool overwriteExisting = false)
        {
            if (!_runtimeAuth.useAppTokens) return false;
            if (!overwriteExisting && !string.IsNullOrEmpty(_runtimeAuth.orgToken)) return false;
            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(authSecret)) return false;

            string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, authSecret);
            if (string.IsNullOrEmpty(dynamicToken)) return false;

            _runtimeAuth.orgToken = dynamicToken;
            return true;
        }

        /// <summary>Returns a mutable copy of a supported user-auth mechanism, or null when user auth is not required.</summary>
        private static AuthMechanism CopyAuthMechanism(AuthMechanism source)
        {
            if (source == null) return null;

            string type = (source.type ?? "").Trim();
            if (string.IsNullOrEmpty(type) || string.Equals(type, "none", StringComparison.OrdinalIgnoreCase))
                return null;

            string normalizedType = NormalizeUserAuthType(type);
            if (string.IsNullOrEmpty(normalizedType))
            {
                Logcat.Warning($"Unsupported authMechanism.type '{type}' from configuration; continuing without user authentication.");
                return null;
            }

            return new AuthMechanism
            {
                type = normalizedType,
                prompt = source.prompt ?? "",
                domain = source.domain ?? "",
                inputSource = !string.IsNullOrEmpty(source.inputSource) ? source.inputSource : "user",
                allowGuest = source.allowGuest
            };
        }

        private static string NormalizeUserAuthType(string type)
        {
            if (string.Equals(type, "assessmentPin", StringComparison.OrdinalIgnoreCase)) return "assessmentPin";
            if (string.Equals(type, "email", StringComparison.OrdinalIgnoreCase)) return "email";
            if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase)) return "text";
            return null;
        }

        private static bool IsSupportedUserAuthType(string type) => !string.IsNullOrEmpty(NormalizeUserAuthType(type));

        private static bool IsAuthMechanismType(AuthMechanism mechanism, string type) =>
            mechanism != null && string.Equals(mechanism.type, type, StringComparison.OrdinalIgnoreCase);

        private static bool NeedsUserAuthentication(AuthMechanism mechanism) =>
            mechanism != null && IsSupportedUserAuthType(mechanism.type);

        private void AuthSucceeded()
        {
            _attemptActive = false;
            Authenticated = true;
            bool ssoUserDataChanged = false;
            if (ResponseData != null)
            {
                ResponseData.UserData ??= new Dictionary<string, string>();
                if (_ssoUserDataMergedBeforeAuthSucceeded)
                {
                    _ssoUserDataMergedBeforeAuthSucceeded = false;
                    ssoUserDataChanged = true;
                }
                else
                    ssoUserDataChanged = SsoUserDataMerger.TryMergeAccessTokenIntoUserData(GetAuthenticatedAccessToken(), ResponseData.UserData);
                _userData = new Dictionary<string, string>(ResponseData.UserData);
            }
            OnSucceeded?.Invoke();
            Logcat.Info("Authenticated successfully");
            // Push merged MDM SSO claims to the API via the same REST auth path as SetUserData (custom re-auth); completion is OnUserDataSyncCompleted only.
            if (ssoUserDataChanged) SetUserData();
        }

        /// <summary>For handoff receivers and GET-config failure: no keyboard/PIN; keep Configuration asset defaults for other fields.</summary>
        private void ClearUserAuthMechanismForSession()
        {
            _authMechanism = null;
            _runtimeAuth.authMechanism = null;
        }

        /// <summary>
        /// WebGL: when a pre-filled PIN was resolved (org token JWT <c>pin</c> claim, or <c>assessment_pin</c>/<c>assessmentPin</c> in the page URL), force user authentication to type <c>assessmentPin</c> after GET config succeeds (non-handoff).
        /// Matches <see cref="GetWebGlQueryData"/> for org_token (skipped for production_custom).</summary>
        private void ApplyAssessmentPinFromUrlQueryIfPresent()
        {
            if (string.IsNullOrEmpty(_webglQueryAssessmentPin))
                return;

            if (_runtimeAuth.authMechanism == null)
                _runtimeAuth.authMechanism = new AuthMechanism();
            _runtimeAuth.authMechanism.type = "assessmentPin";
            if (string.IsNullOrEmpty(_runtimeAuth.authMechanism.inputSource))
                _runtimeAuth.authMechanism.inputSource = "user";

            _authMechanism = CopyAuthMechanism(_runtimeAuth.authMechanism);

            _webglUrlPinAutoSubmitAttempted = false;
            Logcat.Debug("User authentication: pre-filled assessment PIN available; mechanism set to assessmentPin (auto-submit on first attempt).");
        }

        /// <summary>Returns the <c>pin</c> string from a JWT org token payload, or null if missing or not a JWT.</summary>
        private static string TryGetAssessmentPinFromOrgTokenPayload(string orgToken)
        {
            if (string.IsNullOrEmpty(orgToken)) return null;
            var payload = Utils.TryDecodeJwtPayload(orgToken);
            if (payload == null || !payload.TryGetValue("pin", out var pinObj) || pinObj == null)
                return null;
            string s = Utils.JwtPayloadValueToString(pinObj);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }
        private void ClearAuthenticationState()
        {
            Authenticated = false;
            ResponseData = new AuthResponse();
            _tokenExpiry = DateTime.MinValue;
            _payload.sessionId = null;
            _authMechanism = null;
            _runtimeAuth.authMechanism = null;
            _sessionUsedAuthHandoff = false;
            _returnToPackage = null;
            _inputRequestPending = false;
            _userData = null;
            _credentialsRejectedByApi = false;
            _deviceAuthDeferredByHandoff = false;
            _ssoUserDataMergedBeforeAuthSucceeded = false;
            _webglQueryAssessmentPin = null;
            _webglUrlPinAutoSubmitAttempted = false;
        }

        /// <summary>
        /// Clears all auth/session state and assigns a new session ID. Used by StartNewSession before re-authenticating.
        /// Call Authenticate(clearStateFirst: false) after this so the new session ID is preserved.
        /// </summary>
        internal void ClearSessionAndPrepareForNew()
        {
            ClearAuthenticationState();
            _payload.sessionId = Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Check for authentication handoff from external launcher apps
        /// Looks for auth_handoff parameter in command line args, Android intents, or WebGL query params.
        /// Invalid payload: logs and returns (same as if no handoff); normal device authentication runs in AuthenticateCoroutine.
        /// </summary>
        private void CheckAuthHandoff()
        {
            string handoffPayload = _platformSource.GetAndroidIntentParam("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload))
                handoffPayload = _platformSource.GetCommandLineArg("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload) && _platformSource.IsWebGlPlayer)
                handoffPayload = Utils.GetQueryParam("auth_handoff", _platformSource.AbsoluteUrl ?? "");
#if UNITY_INCLUDE_TESTS
            string testHandoffPayload = TestAuthHandoffPayload;
            TestAuthHandoffPayload = null;

            if (string.IsNullOrEmpty(handoffPayload)) handoffPayload = testHandoffPayload;
#endif
            if (string.IsNullOrEmpty(handoffPayload)) return;
            string normalized = NormalizeHandoffPayload(handoffPayload);
            if (string.IsNullOrEmpty(normalized))
            {
                Logcat.Warning("auth_handoff was present but could not be normalized to JSON; continuing with device authentication.");
                return;
            }
            Logcat.Info("Processing authentication handoff from external launcher");
            if (!ApplyAuthResponse(normalized, "handoff", handoff: true))
                Logcat.Warning("auth_handoff was present but the session could not be applied; continuing with device authentication.");
        }

        /// <summary>
        /// Returns the JSON string to use for handoff: if the value is raw JSON (starts with '{') use as-is;
        /// if it is base64-encoded JSON, decode and return the decoded string. Returns null if decoding fails or result is not JSON.
        /// </summary>
        private static string NormalizeHandoffPayload(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string s = value.Trim();
            if (s.StartsWith("{")) return s;
            try
            {
                byte[] bytes = Convert.FromBase64String(s);
                string decoded = Encoding.UTF8.GetString(bytes);
                if (string.IsNullOrEmpty(decoded)) return null;
                decoded = decoded.Trim();
                Logcat.Info("Normalized handoff payload from base64");
                if (decoded.StartsWith("{")) return decoded;
            }
            catch
            {
                // Not valid base64; treat as raw and let ApplyAuthResponse validate
                return s;
            }
            return null;
        }

        public bool SessionUsedAuthHandoff() => _sessionUsedAuthHandoff;

        /// <summary>
        /// Builds the JSON payload passed via the auth_handoff Android intent extra.
        /// Includes all session credentials plus re-auth fields (AppToken, OrgToken, OrgId, DeviceId)
        /// so the receiving app can adopt the REST-authenticated session.
        /// When includeReturnToPackage is true, adds ReturnToPackage (current app's identifier) so the receiving app can return the session when assessment completes.
        /// </summary>
        internal string GetHandoffJson(bool includeReturnToPackage = false)
        {
            if (ResponseData == null || !Authenticated) return null;

            // Use real token expiry from JWT decode; fall back to 24h if not set
            long expiryMs = _tokenExpiry > DateTime.UtcNow
                ? ((DateTimeOffset)_tokenExpiry).ToUnixTimeMilliseconds()
                : ((DateTimeOffset)DateTime.UtcNow.AddHours(24)).ToUnixTimeMilliseconds();

            var handoff = new Dictionary<string, object>
            {
                ["Token"]             = ResponseData.Token ?? "",
                ["Secret"]            = ResponseData.Secret ?? "",
                ["AppId"]             = ResponseData.AppId ?? _payload?.appId ?? "",
                ["UserId"]            = ResponseData.UserId?.ToString() ?? "",
                ["UserData"]          = ResponseData.UserData != null ? new Dictionary<string, string>(ResponseData.UserData) : new Dictionary<string, string>(),
                ["DeviceId"]          = _payload?.deviceId ?? "",
                ["AppToken"]          = _payload?.appToken ?? "",
                ["OrgToken"]          = _payload?.orgToken ?? "",
                ["OrgId"]             = _payload?.orgId ?? "",
                ["TokenExpirationMs"] = expiryMs,
            };
            if (includeReturnToPackage)
                handoff["ReturnToPackage"] = Application.identifier ?? "";
            return JsonConvert.SerializeObject(handoff);
        }

        /// <summary>Returns the stored returnToPackage from the handoff (so the assessment app can launch back to the launcher), then clears it so it is only used once.</summary>
        internal string GetAndClearReturnToPackage()
        {
            var value = _returnToPackage;
            _returnToPackage = null;
            return value;
        }
        
        /// <summary>
        /// Update user data (userData only) and reauthenticate to sync with server.
        /// Session userId is read-only and set only by the backend. Merges existing UserData with the optional id (userData.id) and additionalUserData, then sends via re-auth.
        /// </summary>
        /// <param name="id">Optional primary user identifier (maps to userData.id); can be null to clear or when only updating additional fields.</param>
        /// <param name="additionalUserData">Optional key-value pairs to merge with existing UserData (overwrites existing keys). May be empty to clear all userData.</param>
        public void SetUserData(string id = null, Dictionary<string, string> additionalUserData = null)
        {
            if (!Authenticated)
            {
                Logcat.Warning("Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            if (_stopping || _attemptActive)
            {
                Logcat.Warning("Authentication in progress. Unable to sync user data.");
                return;
            }

            // Build merged user data: start from current response, then apply id (userData.id) and additionalUserData. Do not set session userId (read-only, set by backend).
            var merged = ResponseData?.UserData != null
                ? new Dictionary<string, string>(ResponseData.UserData)
                : new Dictionary<string, string>();

            // Do not send id when it equals the session userId (anonymizedUserId); that would cause the server to hash an already-hashed value.
            string anonymizedUserId = ResponseData?.UserId?.ToString();
            if (!string.IsNullOrEmpty(id) && id != anonymizedUserId)
                merged["id"] = id;

            if (additionalUserData != null)
            {
                foreach (var kvp in additionalUserData)
                    merged[kvp.Key] = kvp.Value;
            }

            _userData = merged;

            // Reauthenticate to sync with server. Do not fire OnSucceeded/OnAuthCompleted (users think they are just updating user reference).
            // Completion is reported via OnUserDataSyncCompleted only; optional app code can subscribe there.
            _attemptActive = true;
            _runner.StartCoroutine(CoSetUserDataReAuth());
        }

        /// <summary>Runs the re-auth for SetUserData; on completion invokes OnUserDataSyncCompleted only (not AuthSucceeded/OnAuthCompleted).</summary>
        private IEnumerator CoSetUserDataReAuth()
        {
            _setUserDataReAuthActive = true;
            yield return AuthRequestCoroutine((success, errorMsg) =>
            {
                _setUserDataReAuthActive = false;
                _attemptActive = false;
                OnUserDataSyncCompleted?.Invoke(success, errorMsg ?? "");
            }, withRetry: false);
        }
        
        /// <summary>True when the dict has a non-empty "type".</summary>
        private static bool IsAuthMechanismMeaningful(Dictionary<string, string> dict) =>
            dict != null && dict.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type);

        private Dictionary<string, string> CreateAuthMechanismDict(string submittedAuthPrompt = null)
        {
            var dict = new Dictionary<string, string>();

            // SetUserData re-auth is the only client-originated custom auth path
            if (_setUserDataReAuthActive)
            {
                dict["type"] = "custom";
                dict["inputSource"] = "user";
                if (_userData == null) return dict;

                foreach (var item in _userData)
                {
                    if (item.Key != "type" && item.Key != "prompt" && item.Key != "inputSource")
                        dict[item.Key] = item.Value;
                }
                return dict;
            }

            // User-input auth supports only the backend-defined types returned by config
            if (!NeedsUserAuthentication(_authMechanism)) return dict;

            dict["type"] = _authMechanism.type;
            dict["prompt"] = submittedAuthPrompt ?? _authMechanism.prompt ?? "";
            if (!string.IsNullOrEmpty(_authMechanism.inputSource))
                dict["inputSource"] = _authMechanism.inputSource;
            return dict;
        }
        
        /// <summary>
        /// Set the input source for authentication (e.g., "user", "QRlms")
        /// This indicates how the authentication value was provided
        /// </summary>
        /// <param name="inputSource">The input source value (defaults to "user" if not set)</param>
        public void SetInputSource(string inputSource)
        {
            if (_authMechanism != null) _authMechanism.inputSource = inputSource;
        }

        private void GetConfigData()
        {
            var config = Configuration.Instance;
            _runtimeAuth.enableAutoStartAuthentication = config?.enableAutoStartAuthentication ?? true;
            _runtimeAuth.enableReturnTo = config?.enableReturnTo ?? true;
            _runtimeAuth.enableAutoStartModules = config?.enableAutoStartModules ?? true;
            _runtimeAuth.enableAutoAdvanceModules = config?.enableAutoAdvanceModules ?? true;

            var configData = Utils.ExtractConfigData(config);
            if (!configData.isValid) return;

            SetDefaultRuntimeDeviceContext();

            _runtimeAuth.useAppTokens = configData.useAppTokens;
            _runtimeAuth.buildType = configData.buildType ?? "production";
            if (configData.useAppTokens)
            {
                _runtimeAuth.appToken = configData.appToken;
                _runtimeAuth.orgToken = configData.orgToken;
            }
            else
            {
                _runtimeAuth.appId = configData.appId;
                _runtimeAuth.orgId = configData.orgId;
                _runtimeAuth.authSecret = configData.authSecret;
            }
            _runtimeAuth.CopyAuthFieldsTo(_payload);
        }
        /// <summary>Applies an Android org_token intent extra when app-token auth has no org token yet</summary>
        private void ApplyAndroidIntentOrgTokenIfAvailable(bool copyToPayload = true)
        {
            if (!_runtimeAuth.useAppTokens || !string.IsNullOrEmpty(_runtimeAuth.orgToken)) return;

            string orgTokenIntent = _platformSource.GetAndroidIntentParam("org_token");
            if (string.IsNullOrEmpty(orgTokenIntent)) return;

            _runtimeAuth.orgToken = orgTokenIntent;
            if (copyToPayload) _runtimeAuth.CopyAuthFieldsTo(_payload);
        }

        /// <summary>
        /// When Arbor MDM is available and connected: updates deviceId, partner, tags from MDM; for production_custom that is all we accept (org credentials stay from config). For other build types, updates orgToken (app tokens) or orgId/authSecret (legacy) from MDM.
        /// When MDM is not available, returns immediately (runtime auth is updated by Abxr.SetOrgId/SetAuthSecret/SetDeviceId directly).
        /// </summary>
        private void GetArborData()
        {
            if (!_platformSource.IsArborMdmConnected(_arborMdmClient)) return;

            // MDM available: always accept deviceId, partner, tags from Arbor.
            _runtimeAuth.partner = "arborxr";
            _runtimeAuth.deviceId = _platformSource.GetCurrentDeviceId();
            _runtimeAuth.tags = _platformSource.GetCurrentDeviceTags();

            // production_custom: only deviceId/partner/tags from MDM; org credentials stay from config.
            if (_runtimeAuth.buildType == "production_custom")
            {
                _runtimeAuth.CopyAuthFieldsTo(_payload);
                return;
            }

            // Non-production_custom: update auth from MDM (dynamic org token or orgId/authSecret).
            if (_runtimeAuth.useAppTokens)
            {
                TrySetDynamicOrgToken(
                    _platformSource.GetCurrentOrgId(),
                    _platformSource.GetCurrentFingerprint(),
                    overwriteExisting: true);
            }
            else
            {
                _runtimeAuth.orgId = _platformSource.GetCurrentOrgId();
                _runtimeAuth.authSecret = _platformSource.GetCurrentFingerprint();
            }

            _runtimeAuth.CopyAuthFieldsTo(_payload);
        }

        private void GetWebGlQueryData()
        {
            if (_runtimeAuth.buildType == "production_custom") return;

            string absoluteUrl = _platformSource.AbsoluteUrl ?? "";
            string orgTokenQuery = Utils.GetQueryParam("org_token", absoluteUrl);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                _runtimeAuth.orgToken = orgTokenQuery;
                _runtimeAuth.CopyAuthFieldsTo(_payload);
            }

            _webglQueryAssessmentPin = null;
            string pinFromOrgJwt = TryGetAssessmentPinFromOrgTokenPayload(_runtimeAuth.orgToken);
            if (!string.IsNullOrEmpty(pinFromOrgJwt))
            {
                _webglQueryAssessmentPin = pinFromOrgJwt;
                return;
            }

            string pinQuery = Utils.GetQueryParam("assessment_pin", absoluteUrl);
            if (string.IsNullOrEmpty(pinQuery))
                pinQuery = Utils.GetQueryParam("assessmentPin", absoluteUrl);
            if (!string.IsNullOrWhiteSpace(pinQuery))
                _webglQueryAssessmentPin = pinQuery.Trim();
        }

        private void GetDesktopQueryData()
        {
            if (_runtimeAuth.buildType == "production_custom") return;

            string orgToken = _platformSource.GetDesktopOrgToken();
            if (!string.IsNullOrEmpty(orgToken))
            {
                _runtimeAuth.orgToken = orgToken;
                _runtimeAuth.CopyAuthFieldsTo(_payload);
            }
        }

        private void SetSessionData()
        {
            _payload.deviceModel = DeviceModel.deviceModel;
            if (_platformSource.IsAndroidPlayer)
            {
                if (Configuration.Instance.recordIpAddress)
                    _payload.ipAddress = _platformSource.GetIpAddress();

                // Read build_fingerprint from Android manifest
                _payload.buildFingerprint = _platformSource.GetAndroidManifestMetadata("com.arborxr.abxrlib.build_fingerprint");

                string xrdmVersion = _platformSource.GetXrdmVersion();
                if (!string.IsNullOrEmpty(xrdmVersion))
                    _payload.xrdmVersion = xrdmVersion;
            }
            //TODO Geolocation
        }
        
        /// <summary>Returns enableAutoStartModules from runtime auth (loaded from Configuration in GetConfigData).</summary>
        internal bool GetEffectiveEnableAutoStartModules() =>
            _runtimeAuth.enableAutoStartModules ?? Configuration.Instance?.enableAutoStartModules ?? true;

        /// <summary>Returns enableAutoAdvanceModules from runtime auth (loaded from Configuration in GetConfigData).</summary>
        internal bool GetEffectiveEnableAutoAdvanceModules() =>
            _runtimeAuth.enableAutoAdvanceModules ?? Configuration.Instance?.enableAutoAdvanceModules ?? true;

        /// <summary>Returns enableReturnTo from runtime auth (loaded from Configuration in GetConfigData).</summary>
        internal bool GetEffectiveEnableReturnTo() =>
            _runtimeAuth.enableReturnTo ?? Configuration.Instance?.enableReturnTo ?? true;

        /// <summary>Returns enableAutoStartAuthentication from the runtime auth config (loaded from Configuration in GetConfigData).</summary>
        internal bool GetEnableAutoStartAuthentication() =>
            _runtimeAuth.enableAutoStartAuthentication ?? true;

        // ── Runtime auth overrides (Abxr.SetOrgId / SetAuthSecret / SetDeviceId) ─────

        /// <summary>Updates runtime auth orgId. Called by subsystem when Abxr.SetOrgId() is used.</summary>
        internal void SetRuntimeAuthOrgId(string value) => _runtimeAuth.orgId = value ?? "";

        /// <summary>Updates runtime auth authSecret. Called by subsystem when Abxr.SetAuthSecret() is used.</summary>
        internal void SetRuntimeAuthAuthSecret(string value) => _runtimeAuth.authSecret = value ?? "";

        /// <summary>Updates runtime auth deviceId. Called by subsystem when Abxr.SetDeviceId() is used.</summary>
        internal void SetRuntimeAuthDeviceId(string value) => _runtimeAuth.deviceId = value ?? "";

        /// <summary>Applies current platform/subsystem getters (GetOrgId, GetFingerprint, GetDeviceId, GetDeviceTags) to _runtimeAuth so values set via Abxr setters (or from MDM via GetDeviceTags) are used.
        /// Only overwrites when the getter returns a non-empty value so we do not wipe configured credentials with empty values (e.g. Editor with no MDM).</summary>
        private void ApplyAbxrOverridesToRuntimeAuth()
        {
            if (_runtimeAuth == null) return;
            string orgId = _platformSource.GetCurrentOrgId();
            if (!string.IsNullOrEmpty(orgId))
                _runtimeAuth.orgId = orgId;
            string authSecret = _platformSource.GetCurrentFingerprint();
            if (!string.IsNullOrEmpty(authSecret))
                _runtimeAuth.authSecret = authSecret;
            string deviceId = _platformSource.GetCurrentDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
                _runtimeAuth.deviceId = deviceId;
            string[] tags = _platformSource.GetCurrentDeviceTags();
            if (tags != null && tags.Length > 0)
                _runtimeAuth.tags = tags;
        }
    }
}
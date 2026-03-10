using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Services.Transport;
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
        private static readonly WaitForSeconds ReAuthWait = new WaitForSeconds(ReAuthPollSeconds);

        // ── Internal state ───────────────────────────────────────────
        private readonly AuthPayload _payload;
        /// <summary>Runtime auth values: loaded from Configuration then updated by GetArborData, GetQueryData, intent, SetOrgId/SetAuthSecret. Holds authMechanism (from GET config when not already set, or set by tests); copied to _authMechanism so we can mutate prompt for user input.</summary>
        private readonly RuntimeAuthConfig _runtimeAuth = new RuntimeAuthConfig();
        /// <summary>Working copy of _runtimeAuth.authMechanism for this session; prompt is temporarily set to user input in KeyboardAuthenticate. All code uses this.</summary>
        private AuthMechanism _authMechanism;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private int _failedAuthAttempts;
        private bool _inputRequestPending;

        /// <summary>True when OnInputRequested was invoked and we are waiting for the app to call SubmitInput (OnInputSubmitted). Used so clients can show/hide QR-for-auth UI via IsQRScanForAuthAvailable() without tracking state themselves.</summary>
        internal bool IsInputRequestPending => _inputRequestPending;

        private string _lastInputError;
        private bool _stopping;
        private bool _attemptActive;
        private Coroutine _reAuthCoroutine;
        private Coroutine _retryCoroutine;
        private Dictionary<string, string> _userData;
        /// <summary>True while the auth request is from SetUserData re-auth; ensures we send type=custom with userData instead of current _authMechanism (e.g. email).</summary>
        private bool _setUserDataReAuthActive;

        private readonly MonoBehaviour _runner;
        private readonly ArborMdmClient _ArborMdmClient;
        private Func<IAbxrTransport> _getTransport;
        
        private const string DeviceIdKey = "abxrlib_device_id";
        
        // Store entered email/text value for email and text auth methods
        private string _enteredAuthValue;
        
        // Auth handoff for external launcher apps
        private bool _sessionUsedAuthHandoff;
        private string _returnToPackage;

        /// <summary>
        /// True only when we completed authentication via ArborInsightsClient this session.
        /// Set once when auth succeeds through the service transport. Used only to skip re-auth polling
        /// (ReAuthPollCoroutine) when the service handles re-auth; data routing is via the transport, not this flag.
        /// </summary>
        private bool _usedArborInsightsClientForSession = false;

        /// <summary>
        /// True when this session authenticated via ArborInsightsClient. When true, re-auth polling is skipped
        /// (the service handles token refresh). Data/events/telemetry/storage routing is via the current transport.
        /// </summary>
        public bool UsingArborInsightsClientForData() => _usedArborInsightsClientForSession;

        /// <summary>When true, Authenticate() skips LoadRuntimeAuthFromConfig() and uses the already-set _runtimeAuth (set via SetRuntimeAuthForTesting). Testing only.</summary>
        private bool _useInjectedRuntimeAuthForTesting;

        public AbxrAuthService(MonoBehaviour coroutineRunner, ArborMdmClient ArborMdmClient)
        {
            _runner = coroutineRunner;
            _ArborMdmClient = ArborMdmClient;

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
#if UNITY_ANDROID && !UNITY_EDITOR
            GetArborData();
            if (_runtimeAuth.useAppTokens && string.IsNullOrEmpty(_runtimeAuth.orgToken))
            {
                string orgTokenIntent = Utils.GetAndroidIntentParam("org_token");
                if (!string.IsNullOrEmpty(orgTokenIntent))
                {
                    _runtimeAuth.orgToken = orgTokenIntent;
                    _runtimeAuth.CopyAuthFieldsTo(_payload);
                }
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
            _payload.deviceId = GetOrCreateDeviceId();
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            GetQueryData();
#endif
            SetSessionData();

            // Do not block on ArborInsightsClient here: bind is started before this constructor runs,
            // and auth waits for service ready in a coroutine so the scene can load without lag.
        }

        internal void SetTransportGetter(Func<IAbxrTransport> getter) => _getTransport = getter;

        // ── Public API ───────────────────────────────────────────────
        
        /// <param name="clearStateFirst">If true (default), clears auth state before running. If false, caller has already cleared and set session (e.g. StartNewSession).</param>
        public void Authenticate(bool clearStateFirst = true)
        {
            if (_stopping || _attemptActive) return;
            _attemptActive = true;
            StopReAuthPolling();
            if (clearStateFirst)
                ClearAuthenticationState();

            // Load runtime auth from Configuration, then apply GetArborData/GetQueryData/intent so runtime config reflects all sources.
            // When tests inject runtime auth, skip loading from Configuration so the injected values are used.
            if (!_useInjectedRuntimeAuthForTesting)
                LoadRuntimeAuthFromConfig();

            // GetArborData() no-ops when ArborMdmClient is not available; when available it applies device/org from MDM.
            GetArborData();

            // Apply Abxr.SetOrgId/SetAuthSecret/SetDeviceId into runtime auth (after config load so overrides win; after GetArborData so MDM can have set org token first).
            ApplyAbxrOverridesToRuntimeAuth();

            // When using app tokens with no org token yet, build dynamic org token from overrides (SetOrgId/SetAuthSecret) or from MDM (already set in GetArborData). Same logic as GetArborData but for when MDM is not connected—overrides supply orgId and authSecret (fingerprint) to sign the JWT.
            if (_runtimeAuth.useAppTokens && string.IsNullOrEmpty(_runtimeAuth.orgToken) && !string.IsNullOrEmpty(_runtimeAuth.orgId) && !string.IsNullOrEmpty(_runtimeAuth.authSecret))
            {
                try
                {
                    string dynamicToken = Utils.BuildOrgTokenDynamic(_runtimeAuth.orgId, _runtimeAuth.authSecret);
                    if (!string.IsNullOrEmpty(dynamicToken))
                        _runtimeAuth.orgToken = dynamicToken;
                }
                catch (Exception ex)
                {
                    Logcat.Error($"BuildOrgTokenDynamic from overrides failed: {ex.Message}");
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_runtimeAuth.useAppTokens && string.IsNullOrEmpty(_runtimeAuth.orgToken))
            {
                string orgTokenIntent = Utils.GetAndroidIntentParam("org_token");
                if (!string.IsNullOrEmpty(orgTokenIntent))
                    _runtimeAuth.orgToken = orgTokenIntent;
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            GetQueryData();
#endif
            var validationError = _runtimeAuth.IsValidToSend();
            if (validationError != null)
            {
                _attemptActive = false;
                OnFailed?.Invoke(validationError);
                return;
            }
            _runtimeAuth.CopyAuthFieldsTo(_payload);

            // Check auth handoff (command-line / intent)
            if (CheckAuthHandoff())
            {
                _attemptActive = false;
                return;
            }

            _runner.StartCoroutine(AuthenticateCoroutine());
        }
        
        public void SetSessionId(string sessionId) => _payload.sessionId = sessionId;

        /// <summary>
        /// Submit user input when there is an outstanding OnInputRequested. Called by subsystem (Abxr.OnInputSubmitted).
        /// If no input was requested, this is a no-op.
        /// </summary>
        public void SubmitInput(string input)
        {
            if (!_inputRequestPending)
            {
                Logcat.Warning("OnInputSubmitted was ignored: no input request is pending. Call OnInputSubmitted only once, after OnInputRequested has been invoked.");
                return;
            }
            _inputRequestPending = false;

            if (input == "**skip**")
            {
                Logcat.Warning("Skipping user authentication.");
                KeyboardHandler.Destroy();
                AuthSucceeded();
                return;
            }

            KeyboardAuthenticate(input);
        }
        
        public void KeyboardAuthenticate(string input)
        {
            string originalPrompt = _authMechanism.prompt;
            // For email type: put full email (userInput + "@" + domain) into prompt for the auth request; server does not use domain from payload. Domain is client-only for prompting and building this value.
            if (_authMechanism.type == "email" && !string.IsNullOrEmpty(_authMechanism.domain) && input != null && !input.Contains("@"))
                _authMechanism.prompt = input + "@" + _authMechanism.domain;
            else
                _authMechanism.prompt = input;

            _runner.StartCoroutine(AuthRequestCoroutineWithError((success, errorMessage) =>
            {
                // Store the entered value only for email and text so we can add it to UserData. PIN is never stored in UserData—only used as auth prompt.
                if (_authMechanism.type == "email" || _authMechanism.type == "text")
                {
                    _enteredAuthValue = input;
                    if (_authMechanism.type == "email" && !string.IsNullOrEmpty(_authMechanism.domain) && input != null && !input.Contains("@"))
                        _enteredAuthValue += "@" + _authMechanism.domain;
                }

                _authMechanism.prompt = originalPrompt;
                if (success)
                {
                    KeyboardHandler.Destroy();
                    AuthSucceeded();
                }
                else
                {
                    _lastInputError = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "Authentication failed";
                    OnFailed?.Invoke(_lastInputError);
                    // Re-invoke OnInputRequested with the error so the built-in keyboard (or custom handler) can show the message and let the user try again.
                    string normalizedType = NormalizeAuthMechanismTypeForInput(_authMechanism.type);
                    string displayError = ShortenPinErrorForDisplay(normalizedType, _lastInputError);
                    OnInputRequested?.Invoke(normalizedType, originalPrompt, _authMechanism.domain ?? "", displayError);
                }
            }, withRetry: false));
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null)
        {
            // When using ArborInsightsClient for data, Token/Secret are not set; only REST path should call this.
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
        
        public void StopReAuthPolling()
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
            if (_retryCoroutine != null && _runner != null)
                _runner.StopCoroutine(_retryCoroutine);
            _retryCoroutine = null;
            _attemptActive = false;
        }
        
        // ── Core auth flow (coroutine) ───────────────────────────────

        private IEnumerator AuthenticateCoroutine()
        {
            bool authOk = false;
            yield return _runner.StartCoroutine(AuthRequestCoroutine(ok => authOk = ok));
            if (!authOk)
            {
                _attemptActive = false;
                OnFailed?.Invoke("Initial authentication request failed");
                yield break;
            }

            // Start re-auth polling
            StartReAuthPolling();

            // Fetch config (which may contain an auth mechanism / pin prompt)
            bool configOk = false;
            string configFailureDetail = null;
            yield return _runner.StartCoroutine(GetConfigurationCoroutine((ok, detail) => { configOk = ok; configFailureDetail = detail; }));
            if (!configOk)
            {
                _attemptActive = false;
                string message = string.IsNullOrEmpty(configFailureDetail)
                    ? "Config request failed"
                    : $"Config request failed: {configFailureDetail}";
                OnFailed?.Invoke(message);
                yield break;
            }

            if (_stopping || !_attemptActive)
            {
                _attemptActive = false;
                OnFailed?.Invoke("Auth stopped or attempt inactive");
                yield break;
            }

            // If no auth mechanism or its type does not require user input → no keyboard. Otherwise show keyboard (prompt/domain may be empty).
            bool needsInput = _authMechanism != null && RequiresUserInputType(_authMechanism.type);
            if (needsInput)
            {
                RequestKeyboardInput();
            }
            else
            {
                AuthSucceeded();
            }
        }

        private class AuthRequestResultHolder
        {
            public bool Success;
            public string Response;
            public long ResponseCode;
        }

        /// <summary>Extract a user-facing error string from auth failure JSON. Same keys for REST and service so behavior is identical.</summary>
        private static string ExtractAuthErrorMessage(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson)) return null;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);
                if (obj == null) return responseJson.Length <= 200 ? responseJson : responseJson.Substring(0, 200) + "...";
                // Backend (e.g. FastAPI) uses "detail" (string or array); other APIs use "message" or "error". Check all so REST and service behave the same.
                string FromValue(object v)
                {
                    if (v == null) return null;
                    if (v is string s) return s;
                    // Newtonsoft deserializes to JValue/JArray; JArray.ToString() is not useful; try first element.
                    var jarr = v as Newtonsoft.Json.Linq.JArray;
                    if (jarr != null && jarr.Count > 0 && jarr[0] != null)
                        return jarr[0].ToString();
                    return v.ToString();
                }
                if (obj.TryGetValue("detail", out var detail)) { var s = FromValue(detail); if (!string.IsNullOrEmpty(s)) return s; }
                if (obj.TryGetValue("message", out var msg)) { var s = FromValue(msg); if (!string.IsNullOrEmpty(s)) return s; }
                if (obj.TryGetValue("error", out var err)) { var s = FromValue(err); if (!string.IsNullOrEmpty(s)) return s; }
            }
            catch { /* ignore */ }
            return responseJson.Length <= 200 ? responseJson : responseJson.Substring(0, 200) + "...";
        }

        /// <summary>Attempts auth via current transport. When withRetry is true, retries until success. When false (e.g. keyboard auth), one attempt only.</summary>
        private IEnumerator AuthRequestCoroutineWithError(Action<bool, string> onComplete, bool withRetry = true)
        {
            if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }
            if (_getTransport == null) { onComplete(false, "Transport not set"); yield break; }

            if (string.IsNullOrEmpty(_payload.sessionId)) _payload.sessionId = Guid.NewGuid().ToString();
            var authMech = CreateAuthMechanismDict();
            _payload.authMechanism = IsAuthMechanismMeaningful(authMech) ? authMech : null;

            string savedBuildType = _payload.buildType;
            if (_payload.buildType == "production_custom")
                _payload.buildType = "production";

            if (Abxr.GetIsAuthenticated())
                _payload.SSOAccessToken = Abxr.GetAccessToken();

            int retryIntervalSeconds = Math.Max(1, Configuration.Instance.sendRetryIntervalSeconds);
            var transport = _getTransport();
            const int maxConsecutiveEmptyFromService = 5;
            int consecutiveEmptyFromService = 0;

            while (true)
            {
                if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }

                // Send one mode only to REST/backend: app tokens OR legacy (app_id/org_id/auth_secret). Use _runtimeAuth so injected test config and runtime overrides are respected.
                if (_runtimeAuth.useAppTokens)
                {
                    _payload.appId = null;
                    _payload.orgId = null;
                    _payload.authSecret = null;
                }
                else
                {
                    _payload.appToken = null;
                    _payload.orgToken = null;
                }

                // Debug: log what we send in this auth request. When authMechanism is null we omit it from the payload (first-stage); only log when present.
                if (_payload.authMechanism != null)
                {
                    var authMechLog = string.Join(", ", _payload.authMechanism.Select(kvp => kvp.Key + "=" + (string.IsNullOrEmpty(kvp.Value) ? "(empty)" : kvp.Value)));
                    //Logcat.Debug
                    Logcat.Info($"Auth request: authMechanism=[{authMechLog}], _authMechanism.prompt={(_authMechanism?.prompt ?? "(null)")} (length={_authMechanism?.prompt?.Length ?? 0})");
                }

                var holder = new AuthRequestResultHolder();
                yield return transport.AuthRequestCoroutine(_payload, (ok, json, code) =>
                {
                    holder.Success = ok;
                    holder.Response = json;
                    holder.ResponseCode = code;
                });

                if (holder.ResponseCode >= 400 && holder.ResponseCode < 500)
                {
                    _payload.buildType = savedBuildType;
                    onComplete(false, ExtractAuthErrorMessage(holder.Response));
                    yield break;
                }

                if (ApplyAuthResponse(holder.Response))
                {
                    if (transport.IsServiceTransport)
                        _usedArborInsightsClientForSession = true;
                    _payload.buildType = savedBuildType;
                    onComplete(true, null);
                    yield break;
                }

                if (!withRetry)
                {
                    _payload.buildType = savedBuildType;
                    onComplete(false, ExtractAuthErrorMessage(holder.Response));
                    yield break;
                }

                // First auth (withRetry): explicit backend error (e.g. "Invalid assessment pin or the assessment is already active.") — do not retry; fail so OnFailed runs and tests don't hang.
                string explicitError = ExtractAuthErrorMessage(holder.Response);
                if (!string.IsNullOrEmpty(holder.Response) && !string.IsNullOrEmpty(explicitError))
                {
                    _payload.buildType = savedBuildType;
                    Logcat.Warning($"AuthRequest failed: {explicitError}");
                    onComplete(false, explicitError);
                    yield break;
                }

                bool emptyFromService = transport.IsServiceTransport && string.IsNullOrEmpty(holder.Response);
                if (emptyFromService)
                {
                    consecutiveEmptyFromService++;
                    if (consecutiveEmptyFromService >= maxConsecutiveEmptyFromService)
                    {
                        _payload.buildType = savedBuildType;
                        string msg = $"ArborInsightsClient returned empty after {maxConsecutiveEmptyFromService} attempts (check service/logcat for backend error).";
                        Logcat.Warning($"AuthRequest failed: {msg}");
                        onComplete(false, msg);
                        yield break;
                    }
                }
                else
                {
                    consecutiveEmptyFromService = 0;
                }

                string logDetail = !string.IsNullOrEmpty(holder.Response)
                    ? ExtractAuthErrorMessage(holder.Response)
                    : (transport.IsServiceTransport
                        ? "ArborInsightsClient returned empty (check service/logcat for backend error)."
                        : "No response body.");
                Logcat.Warning($"AuthRequest failed: {logDetail} Retrying in {retryIntervalSeconds} seconds...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

        /// <summary>Attempts auth via service (when available) or REST. When withRetry is true, retries the same path until success (parity with main). When false (e.g. keyboard auth), one attempt only.</summary>
        private IEnumerator AuthRequestCoroutine(Action<bool> onComplete, bool withRetry = true)
        {
            yield return AuthRequestCoroutineWithError((ok, _) => onComplete(ok), withRetry);
        }

        /// <summary>Parses auth response and applies it. Uses the same success rule as both transports (AuthResponse.IsValidSuccess): token or modules or appId-only (second-stage required). When token is present (REST), validates JWT and sets expiry; service responses have token stripped so we skip that. Single place for ResponseData, UserData, Modules.</summary>
        private bool ApplyAuthResponse(string responseText)
        {
            if (string.IsNullOrEmpty(responseText)) return false;
            try
            {
                var postResponse = JsonConvert.DeserializeObject<AuthResponse>(responseText);
                if (postResponse == null) return false;

                if (!AuthResponse.IsValidSuccess(postResponse))
                    return false;

                // When we have a token (REST path), validate JWT and set expiry. Service strips token from response so this only runs for REST.
                if (!string.IsNullOrEmpty(postResponse.Token))
                {
                    Dictionary<string, object> decodedJwt = Utils.DecodeJwt(postResponse.Token);
                    if (decodedJwt == null)
                    {
                        Logcat.Error("Failed to decode JWT token");
                        return false;
                    }
                    if (!decodedJwt.ContainsKey("exp"))
                    {
                        Logcat.Error("JWT token missing expiration field");
                        return false;
                    }
                    try
                    {
                        _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
                    }
                    catch (Exception ex)
                    {
                        Logcat.Error($"Invalid JWT token expiration: {ex.Message}");
                        return false;
                    }
                }

                ResponseData = postResponse;
                // Debug: log full auth response after deserialization.
                var userDataLog = ResponseData.UserData == null ? "(null)" : string.Join(", ", ResponseData.UserData.Select(kvp => kvp.Key + "=" + kvp.Value));
                //Logcat.Debug
                Logcat.Info($"Auth response: userId={ResponseData.UserId ?? "(null)"}, userData=[{userDataLog}], token={(!string.IsNullOrEmpty(ResponseData.Token) ? "present" : "(null)")}, appId={ResponseData.AppId ?? "(null)"}, modules={ResponseData.Modules?.Count ?? 0}");
                if (ResponseData.Modules?.Count > 1)
                    ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
                // Keep ResponseData.UserId for read-only use (GetAnonymizedUserId). Sync UserData into _userData.
                if (ResponseData.UserData != null)
                    _userData = new Dictionary<string, string>(ResponseData.UserData);
                if (!string.IsNullOrEmpty(_enteredAuthValue))
                {
                    ResponseData.UserData ??= new Dictionary<string, string>();
                    var keyName = _authMechanism?.type == "email" ? "email" : "text";
                    ResponseData.UserData[keyName] = _enteredAuthValue;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logcat.Error($"Authentication response handling failed: {ex.Message}");
                return false;
            }
        }

        // ── GET /v1/storage/config (or from service when auth was via ArborInsightsClient) ───

        private IEnumerator GetConfigurationCoroutine(Action<bool, string> onComplete)
        {
            if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }
            if (_getTransport == null) { onComplete(false, "Transport not set"); yield break; }

            string configJson = null;
            string failureDetail = null;
            yield return _getTransport().GetConfigCoroutine((ok, json) =>
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
                        // Use GET config only when _runtimeAuth.authMechanism not already set (e.g. by tests). Apply learner launcher only when we just filled from config.
                        bool filledFromConfig = _runtimeAuth.authMechanism == null || string.IsNullOrEmpty(_runtimeAuth.authMechanism.type);
                        if (filledFromConfig)
                        {
                            _runtimeAuth.authMechanism = config.authMechanism ?? new AuthMechanism();
                            if (string.IsNullOrEmpty(_runtimeAuth.authMechanism.inputSource))
                                _runtimeAuth.authMechanism.inputSource = "user";
                            if (Configuration.Instance.enableLearnerLauncherMode && !string.Equals(_runtimeAuth.authMechanism.type ?? "", "assessmentPin", StringComparison.OrdinalIgnoreCase))
                            {
                                _runtimeAuth.authMechanism.type = "assessmentPin";
                                _runtimeAuth.authMechanism.prompt = "Enter your 6-digit PIN";
                            }
                        }
                        _authMechanism = CopyAuthMechanism(_runtimeAuth.authMechanism);
                        if (!string.IsNullOrEmpty(_authMechanism.type) && string.IsNullOrEmpty(_authMechanism.prompt))
                        {
                            string defaultPrompt = GetDefaultPromptForAuthMechanismType(_authMechanism.type);
                            _authMechanism.prompt = defaultPrompt;
                            if (_runtimeAuth.authMechanism != null)
                                _runtimeAuth.authMechanism.prompt = defaultPrompt;
                        }
                        string authType = _authMechanism?.type ?? "";
                        if (!string.IsNullOrEmpty(authType) && !string.Equals(authType, "none", StringComparison.OrdinalIgnoreCase))
                        {
                            Logcat.Info($"User Authentication Required.");
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
            // When using ArborInsightsClient for data, re-auth is the service's responsibility; exit the loop.
            while (!_stopping && !UsingArborInsightsClientForData())
            {
                yield return ReAuthWait;

                if (_tokenExpiry == DateTime.MinValue || _attemptActive) continue;
                if (_tokenExpiry - DateTime.UtcNow <= TimeSpan.FromSeconds(ReAuthThresholdSeconds))
                {
                    Authenticate();
                }
            }
        }

        /// <summary>Load auth-related values from Configuration into _runtimeAuth. GetArborData/GetQueryData/intent will update _runtimeAuth next.</summary>
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
            // Establish subsystem defaults for device/partner/tags whenever we load runtime auth (e.g. each Authenticate call).
            string deviceIdFromSubsystem = Abxr.GetDeviceId();
            _runtimeAuth.deviceId = !string.IsNullOrEmpty(deviceIdFromSubsystem) ? deviceIdFromSubsystem : _payload.deviceId;
            _runtimeAuth.partner = "none";
            _runtimeAuth.tags = null;
        }

        /// <summary>Normalize backend auth mechanism type to "text" | "pin" | "email" for OnInputRequested, or "" if no input required (e.g. "none", empty). Single source of truth for pin variants (assessmentPin, assessment_pin → "pin").</summary>
        private static string NormalizeAuthMechanismTypeForInput(string type)
        {
            if (string.IsNullOrEmpty(type)) return "";
            var t = type.Trim().ToLowerInvariant();
            if (t == "none") return "";
            if (t == "email") return "email";
            if (t == "pin" || t == "assessmentpin" || t == "assessment_pin") return "pin";
            return "text";
        }

        /// <summary>For PIN/pinpad display, shorten long backend error messages to "Invalid PIN" so they fit on the pinpad.</summary>
        private static string ShortenPinErrorForDisplay(string normalizedInputType, string errorMessage)
        {
            if (normalizedInputType != "pin" || string.IsNullOrEmpty(errorMessage)) return errorMessage;
            if (errorMessage.IndexOf("assessment pin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorMessage.IndexOf("assessment is already active", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorMessage.Length > 40)
                return "Invalid PIN";
            return errorMessage;
        }

        /// <summary>Default prompt when auth mechanism type is set (e.g. by tests or enableLearnerLauncherMode). Used for consistent prompts.</summary>
        private static string GetDefaultPromptForAuthMechanismType(string type)
        {
            if (string.IsNullOrEmpty(type)) return "";
            var t = type.Trim();
            if (string.Equals(t, "assessmentPin", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "pin", StringComparison.OrdinalIgnoreCase))
                return "Enter your 6-digit PIN";
            if (string.Equals(t, "email", StringComparison.OrdinalIgnoreCase))
                return "Enter your email address";
            if (string.Equals(t, "text", StringComparison.OrdinalIgnoreCase))
                return "Enter your Employee ID";
            if (string.Equals(t, "none", StringComparison.OrdinalIgnoreCase))
                return "";
            return "";
        }

        /// <summary>Returns a mutable copy of the given auth mechanism so we can set prompt to user input without mutating _runtimeAuth.</summary>
        private static AuthMechanism CopyAuthMechanism(AuthMechanism source)
        {
            if (source == null) return new AuthMechanism();
            return new AuthMechanism
            {
                type = source.type ?? "",
                prompt = source.prompt ?? "",
                domain = source.domain ?? "",
                inputSource = !string.IsNullOrEmpty(source.inputSource) ? source.inputSource : "user"
            };
        }

        /// <summary>True if auth mechanism type indicates the user must enter text/PIN/email (so we should show keyboard). Uses normalized type so pin variants are one place.</summary>
        private static bool RequiresUserInputType(string type)
        {
            return !string.IsNullOrEmpty(NormalizeAuthMechanismTypeForInput(type));
        }

        private void RequestKeyboardInput()
        {
            string error = _lastInputError ?? "";
            _lastInputError = null;
            string type = NormalizeAuthMechanismTypeForInput(_authMechanism?.type);
            if (string.IsNullOrEmpty(type)) type = "text"; // defensive; we should only be here when input is required
            string prompt = _authMechanism?.prompt ?? "";
            string domain = _authMechanism?.domain ?? "";

            _inputRequestPending = true;
            OnInputRequested?.Invoke(type, prompt, domain, error);
            _failedAuthAttempts++;
        }

        private void AuthSucceeded()
        {
            _attemptActive = false;
            Authenticated = true;
            OnSucceeded?.Invoke();
            Logcat.Info("Authenticated successfully");
        }

        /// <summary>For testing only. Applies the given auth response and invokes OnSucceeded so subsystem and Abxr.OnAuthCompleted behave as after a real auth.</summary>
        internal void SimulateAuthSuccess(AuthResponse response)
        {
            if (response == null) return;
            ResponseData = response;
            if (ResponseData.Modules?.Count > 1)
                ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
            // Keep ResponseData.UserId for read-only use (GetAnonymizedUserId). Sync UserData into _userData.
            ResponseData.UserData ??= new Dictionary<string, string>();
            if (ResponseData.UserData != null)
                _userData = new Dictionary<string, string>(ResponseData.UserData);
            Authenticated = true;
            OnSucceeded?.Invoke();
        }

        private void ClearAuthenticationState()
        {
            Authenticated = false;
            ResponseData = new AuthResponse();
            _tokenExpiry = DateTime.MinValue;
            _payload.sessionId = null;
            _authMechanism = new AuthMechanism();
            // Preserve test-injected authMechanism so GetConfigurationCoroutine does not overwrite with server config.
            if (!_useInjectedRuntimeAuthForTesting)
                _runtimeAuth.authMechanism = null;
            _failedAuthAttempts = 0;
            _enteredAuthValue = null;
            _sessionUsedAuthHandoff = false;
            _returnToPackage = null;
            _usedArborInsightsClientForSession = false;
            _inputRequestPending = false;
            _lastInputError = null;
            _userData = null;
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
        /// For TestRunner: tests can inject payload via SetAuthHandoffForTesting so the flow can be asserted without a real intent.
        /// </summary>
        private bool CheckAuthHandoff()
        {
            string handoffPayload = null;
            if (!string.IsNullOrEmpty(_authHandoffForTesting))
            {
                handoffPayload = _authHandoffForTesting;
                _authHandoffForTesting = null;
            }
            if (string.IsNullOrEmpty(handoffPayload))
                handoffPayload = Utils.GetAndroidIntentParam("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload))
                handoffPayload = Utils.GetCommandLineArg("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                handoffPayload = Utils.GetQueryParam("auth_handoff", Application.absoluteURL);
#endif
            }
            if (string.IsNullOrEmpty(handoffPayload)) return false;
            string normalized = NormalizeHandoffPayload(handoffPayload);
            if (string.IsNullOrEmpty(normalized)) return false;
            Logcat.Info("Processing authentication handoff from external launcher");
            return ParseAuthResponse(normalized, true);
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
                // Not valid base64; treat as raw and let ParseAuthResponse validate
                return s;
            }
            return null;
        }

        /// <summary>Testing only. Injects the next auth_handoff payload so the next Authenticate() sees it (e.g. to simulate App 2 receiving handoff from App 1). Cleared after one use.</summary>
        internal static void SetAuthHandoffForTesting(string handoffJson)
        {
            _authHandoffForTesting = handoffJson;
        }

        /// <summary>Testing only. Clears any injected auth_handoff payload.</summary>
        internal static void ClearAuthHandoffForTesting()
        {
            _authHandoffForTesting = null;
        }

        private static string _authHandoffForTesting;

        private static bool ShouldRetry(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;

            long code = request.responseCode;
            if (code == 408 || code == 429) return true;
            if (code >= 500 && code <= 599) return true;

            return false;
        }
        
        public bool SessionUsedAuthHandoff() => _sessionUsedAuthHandoff;

        /// <summary>
        /// Builds the JSON payload passed via the auth_handoff Android intent extra.
        /// Includes all session credentials plus re-auth fields (AppToken, OrgToken, OrgId, DeviceId)
        /// so the receiving app and its ArborInsightsClient service can fully adopt the session.
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

            if (!string.IsNullOrEmpty(id))
                merged["id"] = id;

            if (additionalUserData != null)
            {
                foreach (var kvp in additionalUserData)
                    merged[kvp.Key] = kvp.Value;
            }

            _userData = merged;

            // Reauthenticate to sync with server. Do not fire OnSucceeded/OnAuthCompleted (users think they are just updating user reference).
            // Completion is reported via OnUserDataSyncCompleted only; tests and optional app code can subscribe there.
            _attemptActive = true;
            _runner.StartCoroutine(CoSetUserDataReAuth());
        }

        /// <summary>Runs the re-auth for SetUserData; on completion invokes OnUserDataSyncCompleted only (not AuthSucceeded/OnAuthCompleted).</summary>
        private IEnumerator CoSetUserDataReAuth()
        {
            _setUserDataReAuthActive = true;
            yield return AuthRequestCoroutineWithError((success, errorMsg) =>
            {
                _setUserDataReAuthActive = false;
                _attemptActive = false;
                OnUserDataSyncCompleted?.Invoke(success, errorMsg ?? "");
            });
        }
        
        /// <summary>True when the dict has a non-empty "type" (second-stage or custom). Without type, we omit authMechanism so first-stage sends no auth_mechanism (prompt/inputSource alone are not meaningful).</summary>
        private static bool IsAuthMechanismMeaningful(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return false;
            return dict.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type);
        }

        private Dictionary<string, string> CreateAuthMechanismDict()
        {
            var dict = new Dictionary<string, string>();

            // SetUserData re-auth must send type=custom with userData; do not use current _authMechanism (e.g. email).
            if (_setUserDataReAuthActive && _userData != null)
            {
                dict["type"] = "custom";
                dict["inputSource"] = "user";
                foreach (var item in _userData)
                {
                    if (item.Key != "type" && item.Key != "prompt" && item.Key != "inputSource")
                        dict[item.Key] = item.Value;
                }
                return dict;
            }

            // When we have an explicit auth mechanism (assessmentPin, email, text), use it so second-stage auth sends the correct type and prompt. Exclude "none" so that after anonymous session we still send type=custom when _userData is set (SetUserData re-auth).
            bool useExplicitMechanism = _authMechanism != null && !string.IsNullOrEmpty(_authMechanism.type) && _authMechanism.type != "custom" && !string.Equals(_authMechanism.type, "none", StringComparison.OrdinalIgnoreCase);
            if (useExplicitMechanism)
            {
                if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
                dict["prompt"] = _authMechanism.prompt ?? "";
                // Domain is client-only (prompting and building full email for prompt); server does not use it in the request.
                if (!string.IsNullOrEmpty(_authMechanism.inputSource)) dict["inputSource"] = _authMechanism.inputSource;
                return dict;
            }

            // Custom auth when we have a userData dictionary to send (may be empty so developers can clear all userData). Session userId is not set by client. Only when _userData is non-null (set from response or SetUserData); when null we skip so first request does not send type=custom.
            if (_userData != null)
            {
                dict["type"] = "custom";
                dict["inputSource"] = "user";
                foreach (var item in _userData)
                {
                    if (item.Key != "type" && item.Key != "prompt" && item.Key != "inputSource")
                        dict[item.Key] = item.Value;
                }
                return dict;
            }

            if (_authMechanism == null) return dict;
            if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
            if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
            if (!string.IsNullOrEmpty(_authMechanism.inputSource)) dict["inputSource"] = _authMechanism.inputSource;
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
            _runtimeAuth.enableAutoStartAuthentication = config != null ? config.enableAutoStartAuthentication : true;
            _runtimeAuth.enableReturnTo = config != null ? config.enableReturnTo : true;
            _runtimeAuth.enableAutoStartModules = config != null ? config.enableAutoStartModules : true;
            _runtimeAuth.enableAutoAdvanceModules = config != null ? config.enableAutoAdvanceModules : true;

            var configData = Utils.ExtractConfigData(config);
            if (!configData.isValid)
                return;

            // Establish subsystem defaults for device/partner/tags when runtime auth is first loaded (e.g. constructor / Awake sequence).
            string deviceIdFromSubsystem = Abxr.GetDeviceId();
            _runtimeAuth.deviceId = !string.IsNullOrEmpty(deviceIdFromSubsystem) ? deviceIdFromSubsystem : _payload.deviceId;
            _runtimeAuth.partner = "none";
            _runtimeAuth.tags = null;

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

        /// <summary>
        /// When ArborMdmClient is available and connected: updates deviceId, partner, tags from MDM; for production_custom that is all we accept (org credentials stay from config). For other build types, updates orgToken (app tokens) or orgId/authSecret (legacy) from MDM.
        /// When MDM is not available, returns immediately (runtime auth is updated by Abxr.SetOrgId/SetAuthSecret/SetDeviceId directly).
        /// </summary>
        private void GetArborData()
        {
            if (_ArborMdmClient == null || !_ArborMdmClient.IsConnected())
                return;

            // MDM available: always accept deviceId, partner, tags from Arbor.
            _runtimeAuth.partner = "arborxr";
            _runtimeAuth.deviceId = Abxr.GetDeviceId();
            _runtimeAuth.tags = Abxr.GetDeviceTags();

            // production_custom: only deviceId/partner/tags from MDM; org credentials stay from config.
            if (_runtimeAuth.buildType == "production_custom")
            {
                _runtimeAuth.CopyAuthFieldsTo(_payload);
                return;
            }

            // Non-production_custom: update auth from MDM (dynamic org token or orgId/authSecret).
            if (_runtimeAuth.useAppTokens)
            {
                try
                {
                    string fingerprint = Abxr.GetFingerprint();
                    string orgId = Abxr.GetOrgId();
                    string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, fingerprint);
                    if (!string.IsNullOrEmpty(dynamicToken))
                        _runtimeAuth.orgToken = dynamicToken;
                }
                catch (Exception ex)
                {
                    Logcat.Error($"BuildOrgTokenDynamic failed: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
            }
            else
            {
                _runtimeAuth.orgId = Abxr.GetOrgId();
                try
                {
                    _runtimeAuth.authSecret = Abxr.GetFingerprint();
                }
                catch (Exception ex)
                {
                    Logcat.Error($"Authentication initialization failed: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
            }

            _runtimeAuth.CopyAuthFieldsTo(_payload);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void GetQueryData()
        {
            if (_runtimeAuth.buildType == "production_custom")
                return;
            string orgTokenQuery = Utils.GetQueryParam("org_token", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                _runtimeAuth.orgToken = orgTokenQuery;
                _runtimeAuth.CopyAuthFieldsTo(_payload);
            }
        }

        private static string GetOrCreateDeviceId()
        {
            if (PlayerPrefs.HasKey(DeviceIdKey))
                return PlayerPrefs.GetString(DeviceIdKey);
            string newGuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DeviceIdKey, newGuid);
            PlayerPrefs.Save();
            return newGuid;
        }
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        private void GetQueryData()
        {
            if (_runtimeAuth.buildType == "production_custom")
                return;
            string orgToken = Utils.GetOrgTokenFromDesktopSources();
            if (!string.IsNullOrEmpty(orgToken))
            {
                _runtimeAuth.orgToken = orgToken;
                _runtimeAuth.CopyAuthFieldsTo(_payload);
            }
        }
#endif

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        private void SetSessionData()
        {
            _payload.deviceModel = DeviceModel.deviceModel;
#if UNITY_ANDROID && !UNITY_EDITOR
            _payload.ipAddress = Utils.GetIPAddress();
            
            // Read build_fingerprint from Android manifest
            _payload.buildFingerprint = Utils.GetAndroidManifestMetadata("com.arborxr.abxrlib.build_fingerprint");
            
            var currentAssembly = Assembly.GetExecutingAssembly();
            AssemblyName[] referencedAssemblies = currentAssembly.GetReferencedAssemblies();
            foreach (AssemblyName assemblyName in referencedAssemblies)
            {
                if (assemblyName.Name == "XRDM.SDK.External.Unity")
                {
                    _payload.xrdmVersion = assemblyName.Version.ToString();
                    break;
                }
            }
#endif
            //TODO Geolocation
        }
        
        private bool ParseAuthResponse(string responseText, bool handoff = false)
        {
            try
            {
                ResponseData = JsonConvert.DeserializeObject<AuthResponse>(responseText);
                // For non-handoff paths, Token is required. For handoff, it may be absent when App A used the
                // ArborInsightsClient transport (which doesn't require Token in its auth response).
                if (!handoff && string.IsNullOrEmpty(ResponseData.Token))
                {
                    throw new Exception("Invalid authentication response - missing token");
                }
            }
            catch (Exception e)
            {
                Logcat.Error($"Failed to parse auth response: {e.Message}");
                return false;
            }
            
            if (!string.IsNullOrEmpty(_enteredAuthValue))
            {
                // Initialize UserData if it's null
                ResponseData.UserData ??= new Dictionary<string, string>();
                string keyName = _authMechanism?.type == "email" ? "email" : "text";
                ResponseData.UserData[keyName] = _enteredAuthValue;
            }

            if (ResponseData.Modules?.Count > 1)
            {
                ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
            }
            
            if (handoff)
            {
                // Set token expiry to far in the future since we're trusting the handoff
                _tokenExpiry = DateTime.UtcNow.AddHours(24);
                Logcat.Info($"Auth handoff successful. Modules: {ResponseData.Modules?.Count ?? 0}");
                _sessionUsedAuthHandoff = true;
                _returnToPackage = ResponseData?.ReturnToPackage;

                // For the ArborInsightsClient transport: the normal AuthRequestCoroutine is bypassed by handoff,
                // so the service has no knowledge of this session. Pass the full auth response JSON so the
                // service can set apiToken/apiSecret/restUrl atomically and start accepting events immediately.
                var transport = _getTransport?.Invoke();
                if (transport?.IsServiceTransport == true)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    try
                    {
                        string handoffRestUrl = Configuration.Instance?.restUrl ?? "";
                        ArborInsightsClient.SetAuthFromHandoff(responseText, handoffRestUrl);
                    }
                    catch (Exception ex) { Logcat.Warning($"SetAuthFromHandoff failed: {ex.Message}"); }
#endif
                    // Mark as service-authenticated so re-auth polling is skipped (service manages token refresh)
                    _usedArborInsightsClientForSession = true;
                }

                StartReAuthPolling();
                AuthSucceeded();
                return true;
            }
            
            // Decode JWT with error handling
            Dictionary<string, object> decodedJwt = Utils.DecodeJwt(ResponseData.Token);
            if (decodedJwt == null)
            {
                Logcat.Error("Failed to decode JWT token");
                return false;
            }
                    
            if (!decodedJwt.ContainsKey("exp"))
            {
                Logcat.Error("JWT token missing expiration field");
                return false;
            }
                    
            try
            {
                _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
            }
            catch (Exception e)
            {
                Logcat.Error($"Invalid JWT token expiration {e.Message}");
                return false;
            }
            
            return true;
        }

        // ── Testing only (For TestRunner) ───────────────────────────────

        /// <summary>Testing only. Overwrites runtime auth with the given config so Authenticate() uses it instead of loading from Configuration. ApplyAbxrOverridesToRuntimeAuth still runs and applies Abxr.SetOrgId/SetAuthSecret/SetDeviceId. Only overwrites enableAutoStartAuthentication when config has it set (HasValue). Mirrors ExtractConfigData: when buildType is "production" (not production_custom), orgToken/orgId/authSecret are not accepted and are cleared.</summary>
        internal void SetRuntimeAuthForTesting(RuntimeAuthConfig config)
        {
            if (config == null) return;
            _runtimeAuth.useAppTokens = config.useAppTokens;
            _runtimeAuth.appToken = config.appToken;
            _runtimeAuth.orgToken = config.orgToken;
            _runtimeAuth.appId = config.appId;
            _runtimeAuth.orgId = config.orgId;
            _runtimeAuth.authSecret = config.authSecret;
            _runtimeAuth.buildType = config.buildType ?? "production";
            if (config.enableAutoStartAuthentication.HasValue)
                _runtimeAuth.enableAutoStartAuthentication = config.enableAutoStartAuthentication;
            if (config.enableReturnTo.HasValue)
                _runtimeAuth.enableReturnTo = config.enableReturnTo;
            if (config.enableAutoStartModules.HasValue)
                _runtimeAuth.enableAutoStartModules = config.enableAutoStartModules;
            if (config.enableAutoAdvanceModules.HasValue)
                _runtimeAuth.enableAutoAdvanceModules = config.enableAutoAdvanceModules;
            _runtimeAuth.authMechanism = config.authMechanism;
            // Production (non-custom) does not accept org credentials from config; they must come from device/MDM at runtime (same as ExtractConfigData).
            if (_runtimeAuth.buildType == "production")
            {
                _runtimeAuth.orgToken = null;
                _runtimeAuth.orgId = null;
                _runtimeAuth.authSecret = null;
            }
            _runtimeAuth.CopyAuthFieldsTo(_payload);
            _useInjectedRuntimeAuthForTesting = true;
        }

        /// <summary>Testing only. Applies only the fields set on overrides (e.g. enableAutoStartAuthentication) without replacing auth credentials. Used when a pending config is applied at subsystem creation so the Configuration asset is not modified.</summary>
        internal void ApplyRuntimeAuthOverridesForTesting(RuntimeAuthConfig overrides)
        {
            if (overrides == null) return;
            if (overrides.enableAutoStartAuthentication.HasValue)
                _runtimeAuth.enableAutoStartAuthentication = overrides.enableAutoStartAuthentication;
            if (overrides.enableReturnTo.HasValue)
                _runtimeAuth.enableReturnTo = overrides.enableReturnTo;
            if (overrides.enableAutoStartModules.HasValue)
                _runtimeAuth.enableAutoStartModules = overrides.enableAutoStartModules;
            if (overrides.enableAutoAdvanceModules.HasValue)
                _runtimeAuth.enableAutoAdvanceModules = overrides.enableAutoAdvanceModules;
            if (overrides.authMechanism != null)
                _runtimeAuth.authMechanism = overrides.authMechanism;
        }

        /// <summary>Returns enableAutoStartModules from runtime auth (loaded from Configuration in GetConfigData, or set via SetRuntimeAuthForTesting/ApplyRuntimeAuthOverridesForTesting).</summary>
        internal bool GetEffectiveEnableAutoStartModules()
        {
            return _runtimeAuth.enableAutoStartModules ?? Configuration.Instance?.enableAutoStartModules ?? true;
        }

        /// <summary>Returns enableAutoAdvanceModules from runtime auth (loaded from Configuration in GetConfigData, or set via SetRuntimeAuthForTesting/ApplyRuntimeAuthOverridesForTesting).</summary>
        internal bool GetEffectiveEnableAutoAdvanceModules()
        {
            return _runtimeAuth.enableAutoAdvanceModules ?? Configuration.Instance?.enableAutoAdvanceModules ?? true;
        }

        /// <summary>Returns enableReturnTo from runtime auth (loaded from Configuration in GetConfigData, or set via SetRuntimeAuthForTesting/ApplyRuntimeAuthOverridesForTesting).</summary>
        internal bool GetEffectiveEnableReturnTo()
        {
            return _runtimeAuth.enableReturnTo ?? Configuration.Instance?.enableReturnTo ?? true;
        }

        /// <summary>Returns enableAutoStartAuthentication from the runtime auth config (loaded from Configuration in GetConfigData, or set via SetRuntimeAuthForTesting).</summary>
        internal bool GetEnableAutoStartAuthentication()
        {
            return _runtimeAuth.enableAutoStartAuthentication ?? true;
        }

        /// <summary>Testing only. Clears the injected runtime auth flag so the next Authenticate() loads from Configuration again.</summary>
        internal void ClearRuntimeAuthInjectionForTesting()
        {
            _useInjectedRuntimeAuthForTesting = false;
        }

        // ── Runtime auth overrides (Abxr.SetOrgId / SetAuthSecret / SetDeviceId) ─────

        /// <summary>Updates runtime auth orgId. Called by subsystem when Abxr.SetOrgId() is used.</summary>
        internal void SetRuntimeAuthOrgId(string value)
        {
            if (_runtimeAuth != null)
                _runtimeAuth.orgId = value ?? "";
        }

        /// <summary>Updates runtime auth authSecret. Called by subsystem when Abxr.SetAuthSecret() is used.</summary>
        internal void SetRuntimeAuthAuthSecret(string value)
        {
            if (_runtimeAuth != null)
                _runtimeAuth.authSecret = value ?? "";
        }

        /// <summary>Updates runtime auth deviceId. Called by subsystem when Abxr.SetDeviceId() is used.</summary>
        internal void SetRuntimeAuthDeviceId(string value)
        {
            if (_runtimeAuth != null)
                _runtimeAuth.deviceId = value ?? "";
        }

        /// <summary>Applies current Abxr getters (GetOrgId, GetFingerprint, GetDeviceId, GetDeviceTags) to _runtimeAuth so values set via Abxr setters (or from MDM via GetDeviceTags) are used. Only overwrites when the getter returns a non-empty value so we do not wipe config/injected credentials with empty (e.g. Editor with no MDM).</summary>
        private void ApplyAbxrOverridesToRuntimeAuth()
        {
            if (_runtimeAuth == null) return;
            string orgId = Abxr.GetOrgId();
            if (!string.IsNullOrEmpty(orgId))
                _runtimeAuth.orgId = orgId;
            string authSecret = Abxr.GetFingerprint();
            if (!string.IsNullOrEmpty(authSecret))
                _runtimeAuth.authSecret = authSecret;
            string deviceId = Abxr.GetDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
                _runtimeAuth.deviceId = deviceId;
            string[] tags = Abxr.GetDeviceTags();
            if (tags != null && tags.Length > 0)
                _runtimeAuth.tags = tags;
        }
    }
}
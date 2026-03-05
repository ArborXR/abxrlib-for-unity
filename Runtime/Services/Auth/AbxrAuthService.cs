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

        // ── Public state ─────────────────────────────────────────────
        public bool Authenticated { get; private set; }
        public AuthResponse ResponseData { get; private set; } = new();

        // ── Constants ────────────────────────────────────────────────
        private const float ReAuthPollSeconds = 60f;
        private const int ReAuthThresholdSeconds = 120;
        private static readonly WaitForSeconds ReAuthWait = new WaitForSeconds(ReAuthPollSeconds);

        // ── Internal state ───────────────────────────────────────────
        private readonly AuthPayload _payload;
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
        private Dictionary<string, string> _userData = new();
        
        private readonly MonoBehaviour _runner;
        private readonly ArborMdmClient _ArborMdmClient;
        private Func<IAbxrTransport> _getTransport;
        
        private const string DeviceIdKey = "abxrlib_device_id";
        
        // Store entered email/text value for email and text auth methods
        private string _enteredAuthValue;
        
        // Auth handoff for external launcher apps
        private bool _sessionUsedAuthHandoff;

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
            // On non-XRDM devices, org_token can be provided via launch intent (e.g. adb shell am start --es org_token "JWT...")
            if (Configuration.Instance.useAppTokens && string.IsNullOrEmpty(_payload.orgToken))
            {
                string orgTokenIntent = Utils.GetAndroidIntentParam("org_token");
                if (!string.IsNullOrEmpty(orgTokenIntent))
                    _payload.orgToken = orgTokenIntent;
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

            // Load config into payload first, then re-apply Arbor/device overrides so they are not lost.
            LoadConfigIntoPayload();
#if UNITY_ANDROID && !UNITY_EDITOR
            GetArborData();
            if (Configuration.Instance.useAppTokens && string.IsNullOrEmpty(_payload.orgToken))
            {
                string orgTokenIntent = Utils.GetAndroidIntentParam("org_token");
                if (!string.IsNullOrEmpty(orgTokenIntent))
                    _payload.orgToken = orgTokenIntent;
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            GetQueryData();
#endif

            if (!ValidateConfigValues())
            {
                _attemptActive = false;
                OnFailed?.Invoke("Abxr settings are invalid");
                return;
            }

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
                Debug.LogWarning("[AbxrLib] OnInputSubmitted was ignored: no input request is pending. Call OnInputSubmitted only once, after OnInputRequested has been invoked.");
                return;
            }
            _inputRequestPending = false;

            if (input == "**skip**")
            {
                Debug.LogWarning("[AbxrLib] Skipping user authentication.");
                KeyboardHandler.Destroy();
                AuthSucceeded();
                return;
            }

            KeyboardAuthenticate(input);
        }
        
        public void KeyboardAuthenticate(string input)
        {
            string originalPrompt = _authMechanism.prompt;
            _authMechanism.prompt = input;

            _runner.StartCoroutine(AuthRequestCoroutineWithError((success, errorMessage) =>
            {
                // Store the entered value only for email and text so we can add it to UserData. PIN is never stored in UserData—only used as auth prompt.
                if (_authMechanism.type == "email" || _authMechanism.type == "text")
                {
                    _enteredAuthValue = input;

                    // For email type, combine with domain if provided
                    if (_authMechanism.type == "email" && !string.IsNullOrEmpty(_authMechanism.domain))
                    {
                        _enteredAuthValue += $"@{_authMechanism.domain}";
                    }
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
                    RequestKeyboardInput();
                }
            }, withRetry: false));
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null)
        {
            // When using ArborInsightsClient for data, Token/Secret are not set; only REST path should call this.
            if (ResponseData == null || string.IsNullOrEmpty(ResponseData.Token) || string.IsNullOrEmpty(ResponseData.Secret))
            {
                Debug.LogError("[AbxrLib] Cannot set auth headers - authentication tokens are missing");
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
                OnFailed?.Invoke("[AbxrLib] Initial authentication request failed");
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
                    ? "[AbxrLib] Config request failed"
                    : $"[AbxrLib] Config request failed: {configFailureDetail}";
                OnFailed?.Invoke(message);
                yield break;
            }

            if (_stopping || !_attemptActive)
            {
                _attemptActive = false;
                OnFailed?.Invoke("[AbxrLib] Auth stopped or attempt inactive");
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

        private static string ExtractAuthErrorMessage(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson)) return null;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);
                if (obj != null && obj.TryGetValue("message", out var msg) && msg != null)
                    return msg.ToString();
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
            _payload.authMechanism = CreateAuthMechanismDict();

            string savedBuildType = _payload.buildType;
            if (_payload.buildType == "production_custom")
                _payload.buildType = "production";

            if (Abxr.GetIsAuthenticated())
                _payload.SSOAccessToken = Abxr.GetAccessToken();

            int retryIntervalSeconds = Math.Max(1, Configuration.Instance.sendRetryIntervalSeconds);
            var transport = _getTransport();

            while (true)
            {
                if (_stopping || !_attemptActive) { onComplete(false, null); yield break; }

                // Send one mode only to REST/backend: app tokens OR legacy (app_id/org_id/auth_secret).
                if (Configuration.Instance.useAppTokens)
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

                if (ApplyAuthResponse(holder.Response, fromService: transport.IsServiceTransport))
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

                string logDetail = !string.IsNullOrEmpty(holder.Response)
                    ? ExtractAuthErrorMessage(holder.Response)
                    : (transport.IsServiceTransport
                        ? "ArborInsightsClient returned empty (check service/logcat for backend error)."
                        : "No response body.");
                Debug.LogWarning($"[AbxrLib] AuthRequest failed: {logDetail} Retrying in {retryIntervalSeconds} seconds...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

        /// <summary>Attempts auth via service (when available) or REST. When withRetry is true, retries the same path until success (parity with main). When false (e.g. keyboard auth), one attempt only.</summary>
        private IEnumerator AuthRequestCoroutine(Action<bool> onComplete, bool withRetry = true)
        {
            yield return AuthRequestCoroutineWithError((ok, _) => onComplete(ok), withRetry);
        }

        /// <summary>Parses auth response and applies it. When fromService: no token/expiry validation. When !fromService: require Token and set expiry from JWT. Single place for ResponseData, UserData, Modules, and (when fromService) _usedArborInsightsClientForSession.</summary>
        private bool ApplyAuthResponse(string responseText, bool fromService)
        {
            if (string.IsNullOrEmpty(responseText)) return false;
            try
            {
                var postResponse = JsonConvert.DeserializeObject<AuthResponse>(responseText);
                if (postResponse == null) return false;

                if (!fromService)
                {
                    if (string.IsNullOrEmpty(postResponse.Token))
                    {
                        Debug.LogError("[AbxrLib] Invalid authentication response: missing token");
                        return false;
                    }
                    Dictionary<string, object> decodedJwt = Utils.DecodeJwt(postResponse.Token);
                    if (decodedJwt == null)
                    {
                        Debug.LogError("[AbxrLib] Failed to decode JWT token");
                        return false;
                    }
                    if (!decodedJwt.ContainsKey("exp"))
                    {
                        Debug.LogError("[AbxrLib] JWT token missing expiration field");
                        return false;
                    }
                    try
                    {
                        _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AbxrLib] Invalid JWT token expiration: {ex.Message}");
                        return false;
                    }
                }

                ResponseData = postResponse;
                if (ResponseData.Modules?.Count > 1)
                    ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
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
                Debug.LogError($"[AbxrLib] Authentication response handling failed: {ex.Message}");
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
                        _authMechanism = config.authMechanism ?? new AuthMechanism();
                        if (string.IsNullOrEmpty(_authMechanism.inputSource)) _authMechanism.inputSource = "user";
                        if (Configuration.Instance.enableLearnerLauncherMode && !string.Equals(_authMechanism?.type ?? "", "assessmentPin", StringComparison.OrdinalIgnoreCase))
                        {
                            _authMechanism.type = "assessmentPin";
                            _authMechanism.prompt = "LMS PIN";
                        }
                        string authType = _authMechanism?.type ?? "";
                        if (!string.IsNullOrEmpty(authType) && !string.Equals(authType, "none", StringComparison.OrdinalIgnoreCase))
                            Debug.Log($"[AbxrLib] User Authentication Required. Type: {authType} & Prompt: {(_authMechanism?.prompt ?? "")}");
                        else
                            Debug.Log("[AbxrLib] User authentication not required. Using anonymous session.");
                        onComplete(true, null);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    failureDetail = ex.Message;
                    Debug.LogError($"[AbxrLib] GetConfiguration response handling failed: {ex.Message}");
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

        private void LoadConfigIntoPayload()
        {
            var s = Configuration.Instance;
            if (s.useAppTokens)
            {
                _payload.appToken = s.appToken;
                // Production: org token must come from runtime (MDM, intent, query), not config — match ExtractConfigData behavior so Editor/standalone fail when no runtime source.
                _payload.orgToken = string.Equals(s.buildType, "production", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : s.orgToken;
                _payload.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : "production";
            }
            else
            {
                _payload.appId = s.appID;
                // Production (non–custom): orgId/authSecret must come from runtime (MDM), not config — match ExtractConfigData so Editor/standalone fail when no runtime source.
                if (string.Equals(s.buildType, "production", StringComparison.OrdinalIgnoreCase))
                {
                    _payload.orgId = null;
                    _payload.authSecret = null;
                }
                else
                {
                    _payload.orgId = s.orgID;
                    _payload.authSecret = s.authSecret;
                }
                _payload.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : "production";
            }
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
            Debug.Log("[AbxrLib] Authenticated successfully");
        }

        /// <summary>For testing only. Applies the given auth response and invokes OnSucceeded so subsystem and Abxr.OnAuthCompleted behave as after a real auth.</summary>
        internal void SimulateAuthSuccess(AuthResponse response)
        {
            if (response == null) return;
            ResponseData = response;
            if (ResponseData.Modules?.Count > 1)
                ResponseData.Modules = ResponseData.Modules.OrderBy(m => m.Order).ToList();
            ResponseData.UserData ??= new Dictionary<string, string>();
            var userIdStr = ResponseData.UserId?.ToString();
            if (!string.IsNullOrEmpty(userIdStr))
                ResponseData.UserData["userId"] = userIdStr;
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
            _failedAuthAttempts = 0;
            _enteredAuthValue = null;
            _sessionUsedAuthHandoff = false;
            _usedArborInsightsClientForSession = false;
            _inputRequestPending = false;
            _lastInputError = null;
            _userData?.Clear();
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
        /// Looks for auth_handoff parameter in command line args, Android intents, or WebGL query params
        /// </summary>
        private bool CheckAuthHandoff()
        {
            string handoffJson = Utils.GetAndroidIntentParam("auth_handoff");

            if (string.IsNullOrEmpty(handoffJson))
            {
                handoffJson = Utils.GetCommandLineArg("auth_handoff");
            }

            if (string.IsNullOrEmpty(handoffJson))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                handoffJson = Utils.GetQueryParam("auth_handoff", Application.absoluteURL);
#endif
            }

            if (string.IsNullOrEmpty(handoffJson)) return false;
            
            Debug.Log("[AbxrLib] Processing authentication handoff from external launcher");
            return ParseAuthResponse(handoffJson, true);
        }

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
        /// </summary>
        internal string GetHandoffJson()
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
                ["DeviceId"]          = _payload?.deviceId ?? "",
                ["AppToken"]          = _payload?.appToken ?? "",
                ["OrgToken"]          = _payload?.orgToken ?? "",
                ["OrgId"]             = _payload?.orgId ?? "",
                ["TokenExpirationMs"] = expiryMs,
            };
            return JsonConvert.SerializeObject(handoff);
        }
        
        /// <summary>
        /// Update user data (UserId and UserData) and reauthenticate to sync with server.
        /// Merges existing UserData with the optional userId and additionalUserData, then sends the updated list via re-auth (REST or ArborInsightsClient as appropriate).
        /// </summary>
        /// <param name="userId">Optional user ID to set or update</param>
        /// <param name="additionalUserData">Optional key-value pairs to merge with existing UserData (overwrites existing keys)</param>
        public void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            if (!Authenticated)
            {
                Debug.LogWarning("[AbxrLib] Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            if (_stopping || _attemptActive)
            {
                Debug.LogWarning("[AbxrLib] Authentication in progress. Unable to sync user data.");
                return;
            }

            // Build merged user data: start from current response, then apply userId and additionalUserData
            var merged = ResponseData?.UserData != null
                ? new Dictionary<string, string>(ResponseData.UserData)
                : new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(userId))
            {
                _payload.userId = userId;
                merged["userId"] = userId;
            }
            else
            {
                // Retain original: do not overwrite _payload.userId; keep merged["userId"] from existing data when present
                if (merged.TryGetValue("userId", out var existingUserId) && !string.IsNullOrEmpty(existingUserId))
                    merged["userId"] = existingUserId;
                else
                    merged["userId"] = ResponseData?.UserId?.ToString() ?? _payload.userId ?? "";
            }

            if (additionalUserData != null)
            {
                foreach (var kvp in additionalUserData)
                    merged[kvp.Key] = kvp.Value;
            }

            _userData = merged;

            // Reauthenticate to sync with server (REST or service as appropriate)
            _attemptActive = true;
            _runner.StartCoroutine(AuthRequestCoroutine(_ =>
            {
                _attemptActive = false;
            }));
        }
        
        private Dictionary<string, string> CreateAuthMechanismDict()
        {
            var dict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(_payload.userId))
            {
                dict["type"] = "custom";
                dict["prompt"] = _payload.userId;
                if (_userData != null)
                {
                    foreach (var item in _userData)
                    {
                        if (item.Key != "type" && item.Key != "prompt")
                        {
                            dict[item.Key] = item.Value;
                        }
                    }
                }

                // For custom auth, use "user" as default inputSource if not provided
                dict["inputSource"] = "user";
                return dict;
            }

            if (_authMechanism == null) return dict;
            if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
            if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
            if (!string.IsNullOrEmpty(_authMechanism.domain)) dict["domain"] = _authMechanism.domain;
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

            var configData = Utils.ExtractConfigData(config);

            if (!configData.isValid)
            {
                Debug.LogError($"[AbxrLib] {configData.errorMessage}");
                return;
            }

            if (configData.useAppTokens)
            {
                _payload.appToken = configData.appToken;
                _payload.orgToken = configData.orgToken;
                _payload.buildType = configData.buildType;
            }
            else
            {
                _payload.appId = configData.appId;
                _payload.orgId = configData.orgId;
                _payload.authSecret = configData.authSecret;
                _payload.buildType = configData.buildType;
            }
        }

        private void GetArborData()
        {
            // Partner/deviceId/tags when we have a connected client; when not connected, still apply deviceId from subsystem if set (e.g. Abxr.SetDeviceId).
            if (_ArborMdmClient != null && _ArborMdmClient.IsConnected())
            {
                _payload.partner = "arborxr";
                _payload.deviceId = Abxr.GetDeviceId();
                _payload.tags = Abxr.GetDeviceTags();
            }
            else
            {
                string deviceIdFromSubsystem = Abxr.GetDeviceId();
                if (!string.IsNullOrEmpty(deviceIdFromSubsystem))
                    _payload.deviceId = deviceIdFromSubsystem;
            }

            if (_payload.buildType == "production_custom")
                return;

            // Always apply orgId/authSecret/orgToken from subsystem (GetOrgId/GetFingerprint) when not production_custom,
            // so developer overrides or Configuration are used when client is null or not connected.
            if (Configuration.Instance.useAppTokens)
            {
                try
                {
                    string fingerprint = Abxr.GetFingerprint();
                    string orgId = Abxr.GetOrgId();
                    string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, fingerprint);
                    if (!string.IsNullOrEmpty(dynamicToken))
                        _payload.orgToken = dynamicToken;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AbxrLib] BuildOrgTokenDynamic failed: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
            }
            else
            {
                _payload.orgId = Abxr.GetOrgId();
                try
                {
                    _payload.authSecret = Abxr.GetFingerprint();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AbxrLib] Authentication initialization failed: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void GetQueryData()
        {
            if (_payload.buildType == "production_custom")
                return;
            string orgTokenQuery = Utils.GetQueryParam("org_token", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgTokenQuery))
                _payload.orgToken = orgTokenQuery;
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
            if (_payload.buildType == "production_custom")
                return;
            string orgToken = Utils.GetOrgTokenFromDesktopSources();
            if (!string.IsNullOrEmpty(orgToken))
                _payload.orgToken = orgToken;
        }
#endif

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        private bool ValidateConfigValues()
        {
            var config = Configuration.Instance;

            if (config.useAppTokens)
            {
                if (string.IsNullOrEmpty(_payload.appToken))
                {
                    Debug.LogError("[AbxrLib] Authentication error: App identification not set.");
                    return false;
                }
                if (!LooksLikeJwt(_payload.appToken))
                {
                    Debug.LogError("[AbxrLib] Authentication error: App identification not set.");
                    return false;
                }
                if (_payload.buildType == "development" && string.IsNullOrEmpty(_payload.orgToken))
                    _payload.orgToken = _payload.appToken;
                if (string.IsNullOrEmpty(_payload.orgToken))
                {
                    Debug.LogError("[AbxrLib] Authentication error: Organization identification unavailable.");
                    return false;
                }
                if (!LooksLikeJwt(_payload.orgToken))
                {
                    Debug.LogError("[AbxrLib] Authentication error: Organization identification unavailable.");
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(_payload.appId))
                {
                    Debug.LogError("[AbxrLib] Authentication error: App identification not set.");
                    return false;
                }
                if (string.IsNullOrEmpty(_payload.orgId))
                {
                    Debug.LogError("[AbxrLib] Authentication error: Organization identification unavailable.");
                    return false;
                }
                if (string.IsNullOrEmpty(_payload.authSecret))
                {
                    Debug.LogError("[AbxrLib] Authentication error: Organization identification unavailable.");
                    return false;
                }
            }

            if (!config.IsValid())
                return false;
            return true;
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
                Debug.LogError($"[AbxrLib] Failed to parse auth response: {e.Message}");
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
                Debug.Log($"[AbxrLib] Auth handoff successful. Modules: {ResponseData.Modules?.Count ?? 0}");
                _sessionUsedAuthHandoff = true;

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
                    catch (Exception ex) { Debug.LogWarning($"[AbxrLib] SetAuthFromHandoff failed: {ex.Message}"); }
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
                Debug.LogError("[AbxrLib] Failed to decode JWT token");
                return false;
            }
                    
            if (!decodedJwt.ContainsKey("exp"))
            {
                Debug.LogError("[AbxrLib] JWT token missing expiration field");
                return false;
            }
                    
            try
            {
                _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbxrLib] Invalid JWT token expiration {e.Message}");
                return false;
            }
            
            return true;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
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
        public Action<AuthMechanism> OnInputRequested;
        public Action OnSucceeded;
        public Action<string> OnFailed;

        // ── Public state ─────────────────────────────────────────────
        public bool Authenticated { get; private set; }
        public AuthResponse ResponseData { get; private set; } = new();

        // ── Constants ────────────────────────────────────────────────
        private const float ReAuthPollSeconds = 60f;
        private const int ReAuthThresholdSeconds = 120;

        // ── Internal state ───────────────────────────────────────────
        private readonly AuthPayload _payload;
        private AuthMechanism _authMechanism;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private int _failedAuthAttempts;
        private bool _stopping;
        private bool _attemptActive;
        private Coroutine _reAuthCoroutine;
        private Coroutine _retryCoroutine;
        private Dictionary<string, string> _userData = new();
        
        private readonly MonoBehaviour _runner;
        private readonly ArborServiceClient _arborServiceClient;
        
        private const string DeviceIdKey = "abxrlib_device_id";
        
        // Store entered email/text value for email and text auth methods
        private static string _enteredAuthValue;
        
        // Auth handoff for external launcher apps
        private bool _sessionUsedAuthHandoff;

        /// <summary>
        /// True only when we completed authentication via ArborInsightService this session.
        /// Set once when auth succeeds through the service; never switches to true later.
        /// Data (events, telemetry, logs) use the service only when this is true.
        /// </summary>
        private bool _usedArborInsightServiceForSession = false;

        /// <summary>
        /// True when this session authenticated via ArborInsightService. When true, DataBatcher and
        /// other data paths use the service only (no Unity HTTP). When false, we operate in standalone mode
        /// for the whole session and do not switch to the service later.
        /// </summary>
        public bool UsingArborInsightServiceForData() => _usedArborInsightServiceForSession;

        public AbxrAuthService(MonoBehaviour coroutineRunner, ArborServiceClient arborServiceClient)
        {
            _runner = coroutineRunner;
            _arborServiceClient = arborServiceClient;

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

#if UNITY_ANDROID && !UNITY_EDITOR
            if (Configuration.Instance.enableArborInsightServiceClient)
                ArborInsightServiceClient.WaitForServiceReady();
#endif
        }
        
        // ── Public API ───────────────────────────────────────────────
        
        public void Authenticate()
        {
            if (_stopping || _attemptActive) return;
            _attemptActive = true;
            StopReAuthPolling();
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
        
        public void KeyboardAuthenticate(string input)
        {
            string originalPrompt = _authMechanism.prompt;
            _authMechanism.prompt = input;

            _runner.StartCoroutine(AuthRequestCoroutine(success =>
            {
                // Store the entered value for email and text auth methods so we can add it to UserData
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
                    RequestKeyboardInput();
                }
            }, withRetry: false));
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null)
        {
            // When using ArborInsightService for data, Token/Secret are not set; only REST path should call this.
            if (ResponseData == null || string.IsNullOrEmpty(ResponseData.Token) || string.IsNullOrEmpty(ResponseData.Secret))
            {
                Debug.LogError("AbxrLib: Cannot set auth headers - authentication tokens are missing");
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
            if (_reAuthCoroutine != null)
            {
                _runner.StopCoroutine(_reAuthCoroutine);
                _reAuthCoroutine = null;
            }
        }

        public void Shutdown()
        {
            _stopping = true;
            StopReAuthPolling();
            if (_retryCoroutine != null) _runner.StopCoroutine(_retryCoroutine);
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
                OnFailed?.Invoke("AbxrLib: Initial authentication request failed");
                yield break;
            }

            // Start re-auth polling
            StartReAuthPolling();

            // Fetch config (which may contain an auth mechanism / pin prompt)
            bool configOk = false;
            yield return _runner.StartCoroutine(GetConfigurationCoroutine(ok => configOk = ok));
            if (!configOk)
            {
                _attemptActive = false;
                OnFailed?.Invoke("AbxrLib: Config request failed");
                yield break;
            }

            if (_stopping || !_attemptActive) yield break;

            if (!string.IsNullOrEmpty(_authMechanism.prompt))
            {
                RequestKeyboardInput();
            }
            else
            {
                AuthSucceeded();
            }
        }

        // ── POST /v1/auth/token ──────────────────────────────────────

        /// <summary>Holds the response body from a single REST auth attempt (used so coroutine can "return" it).</summary>
        private class AuthResponseHolder
        {
            public string Response;
        }

        /// <summary>Attempts auth via service (when available) or REST. When withRetry is true, retries the same path until success (parity with main). When false (e.g. keyboard auth), one attempt only.</summary>
        private IEnumerator AuthRequestCoroutine(Action<bool> onComplete, bool withRetry = true)
        {
            if (_stopping || !_attemptActive) { onComplete(false); yield break; }

            if (string.IsNullOrEmpty(_payload.sessionId)) _payload.sessionId = Guid.NewGuid().ToString();
            _payload.authMechanism = CreateAuthMechanismDict();

            // API receives buildType "production" when config is Production (Custom APK)
            string savedBuildType = _payload.buildType;
            if (_payload.buildType == "production_custom")
                _payload.buildType = "production";

            if (Abxr.GetIsAuthenticated())
                _payload.SSOAccessToken = Abxr.GetAccessToken();

            string json = JsonConvert.SerializeObject(_payload);
            int retryIntervalSeconds = Math.Max(1, Configuration.Instance.sendRetryIntervalSeconds);

            while (true)
            {
                if (_stopping || !_attemptActive) { onComplete(false); yield break; }

                bool useService = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                useService = Configuration.Instance.enableArborInsightServiceClient && ArborInsightServiceClient.ServiceIsFullyInitialized();
#endif

                string responseJson = null;
                if (useService)
                {
                    try
                    {
                        var config = Configuration.Instance;
                        ArborInsightServiceClient.SetAuthPayloadForRequest(config.restUrl ?? "https://lib-backend.xrdm.app/", _payload);
                        responseJson = ArborInsightServiceClient.AuthRequest(_payload.userId ?? "", Utils.DictToString(_payload.authMechanism));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AbxrLib: Failed to set service properties for auth: {ex.Message}");
                    }
                }
                else
                {
                    var holder = new AuthResponseHolder();
                    yield return SendAuthRequestRest(json, holder);
                    responseJson = holder.Response;
                }

                if (ApplyAuthResponse(responseJson, fromService: useService))
                {
                    if (useService)
                        _usedArborInsightServiceForSession = true;
                    _payload.buildType = savedBuildType;
                    _attemptActive = false;
                    onComplete(true);
                    yield break;
                }

                if (!withRetry)
                {
                    _payload.buildType = savedBuildType;
                    onComplete(false);
                    yield break;
                }

                Debug.LogWarning($"AbxrLib: AuthRequest failed, retrying in {retryIntervalSeconds} seconds...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

        /// <summary>Performs one POST to /v1/auth/token and sets holder.Response to the response body or null on failure.</summary>
        private IEnumerator SendAuthRequestRest(string json, AuthResponseHolder holder)
        {
            holder.Response = null;
            string url = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token").ToString();
            using var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
                holder.Response = request.downloadHandler?.text;
            else if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                Debug.LogWarning($"AbxrLib: AuthRequest REST failed: {request.responseCode} - {request.downloadHandler.text}");
        }

        /// <summary>Parses auth response and applies it. When fromService: no token/expiry validation. When !fromService: require Token and set expiry from JWT. Single place for ResponseData, UserData, Modules, and (when fromService) _usedArborInsightServiceForSession.</summary>
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
                        Debug.LogError("AbxrLib: Invalid authentication response: missing token");
                        return false;
                    }
                    Dictionary<string, object> decodedJwt = Utils.DecodeJwt(postResponse.Token);
                    if (decodedJwt == null)
                    {
                        Debug.LogError("AbxrLib: Failed to decode JWT token");
                        return false;
                    }
                    if (!decodedJwt.ContainsKey("exp"))
                    {
                        Debug.LogError("AbxrLib: JWT token missing expiration field");
                        return false;
                    }
                    try
                    {
                        _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AbxrLib: Invalid JWT token expiration: {ex.Message}");
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
                Debug.LogError($"AbxrLib: Authentication response handling failed: {ex.Message}");
                return false;
            }
        }

        // ── GET /v1/storage/config (or from service when auth was via ArborInsightService) ───

        private IEnumerator GetConfigurationCoroutine(Action<bool> onComplete)
        {
            if (_stopping || !_attemptActive) { onComplete(false); yield break; }

            string configJson = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Configuration.Instance.enableArborInsightServiceClient && ArborInsightServiceClient.ServiceIsFullyInitialized()) {
                configJson = ArborInsightServiceClient.GetAppConfig();
                if (string.IsNullOrEmpty(configJson))
                {
                    Debug.LogWarning("AbxrLib: GetAppConfig returned empty; not falling back to REST when using ArborInsightService.");
                    onComplete(false);
                    yield break;
                }
            }
#endif
            if (string.IsNullOrEmpty(configJson)) // If the service is not fully initialized, fall back to REST.
            {
                string restJson = null;
                yield return SendConfigRequestRest(j => restJson = j);
                configJson = restJson;
            }

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
                        Debug.Log("AbxrLib: GetConfiguration successful");
                        onComplete(true);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AbxrLib: GetConfiguration response handling failed: {ex.Message}");
                }
            }

            onComplete(false);
        }

        /// <summary>Performs one GET to /v1/storage/config with auth headers. Invokes onComplete with the response body JSON (or null on failure).</summary>
        private IEnumerator SendConfigRequestRest(Action<string> onComplete)
        {
            string url = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/storage/config").ToString();
            UnityWebRequest request = null;
            try
            {
                request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                SetAuthHeaders(request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration request creation failed: {ex.Message}");
                onComplete(null);
                yield break;
            }

            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMessage = request.result switch
                    {
                        UnityWebRequest.Result.ConnectionError => $"Connection error: {request.error}",
                        UnityWebRequest.Result.DataProcessingError => $"Data processing error: {request.error}",
                        UnityWebRequest.Result.ProtocolError => $"Protocol error ({request.responseCode}): {request.error}",
                        _ => $"Unknown error: {request.error}"
                    };
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                        errorMessage += $" - Response: {request.downloadHandler.text}";
                    Debug.LogWarning($"AbxrLib: GetConfiguration failed: {errorMessage}");
                    onComplete(null);
                    yield break;
                }

                string responseJson = request.downloadHandler?.text;
                if (string.IsNullOrEmpty(responseJson))
                    Debug.LogWarning("AbxrLib: Empty configuration response, using default configuration");
                onComplete(responseJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration response handling failed: {ex.Message}");
                onComplete(null);
            }
            finally
            {
                request?.Dispose();
            }
        }
        
        // ────────────────────────────────────────────────────────────────

        private void StartReAuthPolling()
        {
            StopReAuthPolling();
            _reAuthCoroutine = _runner.StartCoroutine(ReAuthPollCoroutine());
        }

        private IEnumerator ReAuthPollCoroutine()
        {
            // When using ArborInsightService for data, re-auth is the service's responsibility; exit the loop.
            while (!_stopping && !UsingArborInsightServiceForData())
            {
                yield return new WaitForSeconds(ReAuthPollSeconds);

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
                _payload.orgToken = s.orgToken;
                _payload.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : "production";
            }
            else
            {
                _payload.appId = s.appID;
                _payload.orgId = s.orgID;
                _payload.authSecret = s.authSecret;
                _payload.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : "production";
            }
        }

        private void RequestKeyboardInput()
        {
            string prompt = "";
            if (_failedAuthAttempts > 0)
            {
                prompt = $"Authentication Failed ({_failedAuthAttempts})\n";
            }

            prompt += _authMechanism.prompt;
            
            OnInputRequested?.Invoke(new AuthMechanism
            {
                type = _authMechanism.type,
                prompt = prompt,
                domain = _authMechanism.domain
            });

            _failedAuthAttempts++;
        }

        private void AuthSucceeded()
        {
            _attemptActive = false;
            Authenticated = true;
            OnSucceeded?.Invoke();
            Debug.Log("AbxrLib: Authenticated successfully");
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
            _usedArborInsightServiceForSession = false;
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
            
            Debug.Log("AbxrLib: Processing authentication handoff from external launcher");
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
        /// Update user data (UserId and UserData) and reauthenticate to sync with server.
        /// Merges existing UserData with the optional userId and additionalUserData, then sends the updated list via re-auth (REST or ArborInsightService as appropriate).
        /// </summary>
        /// <param name="userId">Optional user ID to set or update</param>
        /// <param name="additionalUserData">Optional key-value pairs to merge with existing UserData (overwrites existing keys)</param>
        public void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            if (!Authenticated)
            {
                Debug.LogWarning("AbxrLib: Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            if (_stopping || _attemptActive)
            {
                Debug.LogWarning("AbxrLib: Authentication in progress. Unable to sync user data.");
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
                Debug.LogError($"AbxrLib: {configData.errorMessage} Cannot authenticate.");
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
            // Only apply Arbor SDK overrides when we have a connected client (avoid overwriting config with empty when null/disconnected).
            if (_arborServiceClient?.IsConnected() != true) return;

            _payload.partner = "arborxr";
            _payload.deviceId = Abxr.GetDeviceId();
            _payload.tags = Abxr.GetDeviceTags();

            if (_payload.buildType == "production_custom")
                return;

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
                    Debug.LogError($"AbxrLib: BuildOrgTokenDynamic failed: {ex.Message}\n" +
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
                    Debug.LogError($"AbxrLib: Authentication initialization failed: {ex.Message}\n" +
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
                    Debug.LogError("AbxrLib: App Token is missing. Cannot authenticate.");
                    return false;
                }
                if (!LooksLikeJwt(_payload.appToken))
                {
                    Debug.LogError("AbxrLib: App Token does not look like a JWT (expected three dot-separated segments). Cannot authenticate.");
                    return false;
                }
                if (_payload.buildType == "development" && string.IsNullOrEmpty(_payload.orgToken))
                    _payload.orgToken = _payload.appToken;
                if (string.IsNullOrEmpty(_payload.orgToken))
                {
                    Debug.LogError("AbxrLib: Organization Token is missing. Set it in config, connect via ArborXR device management service for a dynamic token, pass org_token in the URL (WebGL), use --org_token or arborxr_org_token.key (desktop), pass org_token as Android intent extra (APK), or set in config. Cannot authenticate.");
                    return false;
                }
                if (!LooksLikeJwt(_payload.orgToken))
                {
                    Debug.LogError("AbxrLib: Organization Token does not look like a JWT (expected three dot-separated segments). Cannot authenticate.");
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(_payload.appId))
                {
                    Debug.LogError("AbxrLib: Application ID is missing. Cannot authenticate.");
                    return false;
                }
                if (string.IsNullOrEmpty(_payload.orgId))
                {
                    Debug.LogError("AbxrLib: Organization ID is missing. Cannot authenticate.");
                    return false;
                }
                if (string.IsNullOrEmpty(_payload.authSecret))
                {
                    Debug.LogError("AbxrLib: Authentication Secret is missing. Cannot authenticate.");
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
                if (string.IsNullOrEmpty(ResponseData.Token))
                {
                    throw new Exception("Invalid authentication response - missing token");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"AbxrLib: Failed to parse auth response: {e.Message}");
                return false;
            }
            
            if (!string.IsNullOrEmpty(_enteredAuthValue))
            {
                // Initialize UserData if it's null
                ResponseData.UserData ??= new Dictionary<string, string>();
                        
                // Determine the key name based on auth type
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
                Debug.Log($"AbxrLib: Auth handoff successful. Modules: {ResponseData.Modules?.Count ?? 0}");
                _sessionUsedAuthHandoff = true;
                OnSucceeded?.Invoke();
                return true;
            }
            
            // Decode JWT with error handling
            Dictionary<string, object> decodedJwt = Utils.DecodeJwt(ResponseData.Token);
            if (decodedJwt == null)
            {
                Debug.LogError("AbxrLib: Failed to decode JWT token");
                return false;
            }
                    
            if (!decodedJwt.ContainsKey("exp"))
            {
                Debug.LogError("AbxrLib: JWT token missing expiration field");
                return false;
            }
                    
            try
            {
                _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
            }
            catch (Exception e)
            {
                Debug.LogError($"AbxrLib: Invalid JWT token expiration {e.Message}");
                return false;
            }
            
            return true;
        }
    }
}
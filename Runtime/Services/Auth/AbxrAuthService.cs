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
        private const int RetryMaxAttempts = 3;
        private const float RetryDelaySeconds = 1f;
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
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
            _payload.deviceId = GetOrCreateDeviceId();
#endif
            SetSessionData();

#if UNITY_ANDROID && !UNITY_EDITOR
            // Only wait for service readiness if the ArborInsightService APK is installed; otherwise fail fast and use standalone mode.
            if (ArborInsightServiceClient.IsServicePackageInstalled())
            {
                for (int i = 0; i < 40; i++)
                {
                    try
                    {
                        if (ArborInsightServiceClient.ServiceIsFullyInitialized()) break;
                    }
                    catch { }
                    System.Threading.Thread.Sleep(250);
                }
            }
#endif
        }
        
        // ── Public API ───────────────────────────────────────────────
        
        public void Authenticate()
        {
            if (_stopping || _attemptActive) return;
            _attemptActive = true;
            StopReAuthPolling();
            ClearAuthenticationState();

            if (!ValidateConfigValues())
            {
                _attemptActive = false;
                OnFailed?.Invoke("Abxr settings are invalid");
                return;
            }

            LoadConfigIntoPayload();

            // Check auth handoff (command-line / intent)
            if (CheckAuthHandoff()) return;

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
            }));
        }
        
        public void SetAuthHeaders(UnityWebRequest request, string json = null)
        {
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

        private IEnumerator AuthRequestCoroutine(Action<bool> onComplete)
        {
            if (_stopping || !_attemptActive) { onComplete(false); yield break; }
            
            if (string.IsNullOrEmpty(_payload.sessionId)) _payload.sessionId = Guid.NewGuid().ToString();
            
            _payload.authMechanism = CreateAuthMechanismDict();

            string json = JsonConvert.SerializeObject(_payload);
            string url = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token").ToString();
            
            for (int attempt = 1; attempt <= RetryMaxAttempts; attempt++)
            {
                if (_stopping || !_attemptActive) { onComplete(false); yield break; }

                using var request = new UnityWebRequest(url, "POST");
                byte[] body = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (_stopping || !_attemptActive) { onComplete(false); yield break; }
                
                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    if (!ParseAuthResponse(request.downloadHandler.text))
                    {
                        onComplete(false);
                        yield break;
                    }
                    onComplete(true);
                    yield break;
                }

                Debug.LogWarning($"AbxrLib: AuthRequest attempt {attempt} failed: {request.responseCode} - {request.downloadHandler?.text}");
                if (ShouldRetry(request) && attempt < RetryMaxAttempts)
                {
                    yield return new WaitForSeconds(RetryDelaySeconds);
                    continue;
                }

                break;
            }

            onComplete(false);
        }

        // ── GET /v1/storage/config ───────────────────────────────────

        private IEnumerator GetConfigurationCoroutine(Action<bool> onComplete)
        {
            if (_stopping || !_attemptActive) { onComplete(false); yield break; }
            
            string url = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/storage/config").ToString();

            for (int attempt = 1; attempt <= RetryMaxAttempts; attempt++)
            {
                if (_stopping || !_attemptActive) { onComplete(false); yield break; }

                using var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Content-Type", "application/json");
                SetAuthHeaders(request);

                yield return request.SendWebRequest();

                if (_stopping || !_attemptActive) { onComplete(false); yield break; }

                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    var config = JsonConvert.DeserializeObject<ConfigPayload>(request.downloadHandler.text);
                    Configuration.Instance.ApplyConfigPayload(config);
                    _authMechanism = config.authMechanism ?? new AuthMechanism();
                    if (string.IsNullOrEmpty(_authMechanism.inputSource)) _authMechanism.inputSource = "user";
                    
                    Debug.Log("AbxrLib: GetConfiguration successful");
                    onComplete(true);
                    yield break;
                }

                Debug.LogWarning($"AbxrLib: GetConfiguration attempt {attempt} failed: {request.downloadHandler?.text}");
                if (ShouldRetry(request) && attempt < RetryMaxAttempts)
                {
                    yield return new WaitForSeconds(RetryDelaySeconds);
                    continue;
                }

                break;
            }

            Debug.LogError("[AbxrLib] GetConfiguration failed (no more retries)");
            onComplete(false);
        }
        
        // ────────────────────────────────────────────────────────────────

        private void StartReAuthPolling()
        {
            StopReAuthPolling();
            _reAuthCoroutine = _runner.StartCoroutine(ReAuthPollCoroutine());
        }

        private IEnumerator ReAuthPollCoroutine()
        {
            while (!_stopping)
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
            _payload.appId = s.appID;
            _payload.orgId = s.orgID;
            _payload.authSecret = s.authSecret;
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
        /// Update user data (UserId and UserData) and reauthenticate to sync with server
        /// Updates the authentication response with new user information without clearing authentication state
        /// The server API allows reauthenticate to update these values
        /// </summary>
        /// <param name="userId">Optional user ID to update</param>
        /// <param name="additionalUserData">Optional additional user data dictionary to merge with existing UserData</param>
        public void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            // Update _responseData with new values before reauthenticating
            if (!Authenticated)
            {
                Debug.LogWarning("AbxrLib: Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            _payload.userId = userId;
            _userData = additionalUserData;

            if (_stopping || _attemptActive)
            {
                Debug.LogWarning("AbxrLib: Authentication in progress. Unable to sync user data.");
                return;
            }

            // Reauthenticate to sync with server
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
            if (_payload.authMechanism != null) _authMechanism.inputSource = inputSource;
        }

        private void GetConfigData()
        {
            var config = Configuration.Instance;
            
            // Extract all config data using centralized helper
            var configData = Utils.ExtractConfigData(config);
            
            if (!configData.isValid)
            {
                Debug.LogError($"AbxrLib: {configData.errorMessage} Cannot authenticate.");
                return;
            }
            
            // Set Authentication static fields from extracted config data
            _payload.appId = configData.appId;
            _payload.appToken = configData.appToken;
            _payload.orgId = configData.orgId;
            _payload.authSecret = configData.authSecret;
            
            // Note: orgId and authSecret will still be overridden by GetArborData() if ArborServiceClient is connected
        }
    
        private void GetArborData()
        {
            if (!_arborServiceClient?.IsConnected() == true) return;
        
            _payload.partner = "arborxr";
            _payload.orgId = Abxr.GetOrgId();
            _payload.deviceId = Abxr.GetDeviceId();
            _payload.tags = Abxr.GetDeviceTags();
            try
            {
                var authSecret = Abxr.GetFingerprint();
                _payload.authSecret = authSecret;
            }
            catch (Exception ex)
            {
                // Log error with consistent format and include authentication context
                Debug.LogError($"AbxrLib: Authentication initialization failed: {ex.Message}\n" +
                              $"Exception Type: {ex.GetType().Name}\n" +
                              $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        private void GetQueryData()
        {
            string orgIdQuery = Utils.GetQueryParam("abxr_orgid", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgIdQuery))
            {
                _payload.orgId = orgIdQuery;
            }
            
            string authSecretQuery = Utils.GetQueryParam("abxr_auth_secret", Application.absoluteURL);
            if (!string.IsNullOrEmpty(authSecretQuery))
            {
                _payload.authSecret = authSecretQuery;
            }
        }
        
        private static string GetOrCreateDeviceId()
        {
            if (PlayerPrefs.HasKey(DeviceIdKey))
            {
                return PlayerPrefs.GetString(DeviceIdKey);
            }

            string newGuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DeviceIdKey, newGuid);
            PlayerPrefs.Save();
            return newGuid;
        }
#endif
        private bool ValidateConfigValues()
        {
            var config = Configuration.Instance;
            if (!config.useAppTokens && !Configuration.Instance.IsValid()) return false;
            
            if (string.IsNullOrEmpty(_payload.appId) && string.IsNullOrEmpty(_payload.appToken))
            {
                Debug.LogError("AbxrLib: Need Application ID or Application Token. Cannot authenticate.");
                return false;
            }
            
            if (string.IsNullOrEmpty(_payload.orgId))
            {
                Debug.LogError("AbxrLib: Organization ID is missing. Cannot authenticate.");
                return false;
            }
            
            if (string.IsNullOrEmpty(_payload.authSecret))
            {
                Debug.LogError("AbxrLib: Authentication Secret is missing. Cannot authenticate");
                return false;
            }
            
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
/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Authentication System
 * 
 * This file handles user authentication, device identification, and session management
 * for AbxrLib. It provides comprehensive authentication capabilities including:
 * - Device fingerprinting and identification
 * - User authentication with LMS integration support
 * - Authentication handoff mechanisms for seamless user experience
 * - Session management and token handling
 * - Keyboard-based authentication UI
 * 
 * The authentication system supports both device-level and user-level authentication,
 * with automatic fallback mechanisms and robust error handling.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.ServiceClient;
using AbxrLib.Runtime.ServiceClient.ArborInsightService;
using AbxrLib.Runtime.UI.Keyboard;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace AbxrLib.Runtime.Authentication
{
    [DefaultExecutionOrder(1)]
    public class Authentication : MonoBehaviour
    {
        private static string _appId; //legacy only
        private static string _orgId; //legacy only
        private static string _authSecret; //legacy only
        private static string _appToken;
        private static string _orgToken;
        private static string _buildType;
        private static string _deviceId;
        private static Partner _partner = Partner.None;
        private static string _deviceModel;
        private static string[] _deviceTags;
        private static string _xrdmVersion;
        private static string _ipAddress;
        private static string _sessionId;
        private static string _buildFingerprint;
        private static int _failedAuthAttempts;
        
        private static AuthMechanism _authMechanism;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        
        private static Dictionary<string, string> _dictAuthMechanism;
        
        private static AuthResponse _responseData;
        public static AuthResponse GetAuthResponse() => _responseData;
        
        // Store entered email/text value for email and text auth methods
        private static string _enteredAuthValue;
    
        private const string DeviceIdKey = "abxrlib_device_id";

        private static bool? _keyboardAuthSuccess;
        private static bool _initialized;
        
        // Auth handoff for external launcher apps
        private static bool _sessionUsedAuthHandoff = false;

        /// <summary>
        /// True only when we completed authentication via ArborInsightService this session.
        /// Set once when auth succeeds through the service; never switches to true later.
        /// Data (events, telemetry, logs) use the service only when this is true.
        /// </summary>
        private static bool _usedArborInsightServiceForSession = false;

        /// <summary>
        /// True when this session authenticated via ArborInsightService. When true, DataBatcher and
        /// other data paths use the service only (no Unity HTTP). When false, we operate in standalone mode
        /// for the whole session and do not switch to the service later.
        /// </summary>
        public static bool UsingArborInsightServiceForData() => _usedArborInsightServiceForSession;

        public static bool Authenticated()
        {
            if (_keyboardAuthSuccess != true) return false;
            if (UsingArborInsightServiceForData())
                return _responseData != null;
            return !string.IsNullOrEmpty(_responseData?.Token) &&
                   !string.IsNullOrEmpty(_responseData?.Secret) &&
                   DateTime.UtcNow <= _tokenExpiry;
        }
        
        public static bool SessionUsedAuthHandoff() => _sessionUsedAuthHandoff;
        
        /// <summary>
        /// Gets the authentication mechanism type, or null if no mechanism is set
        /// </summary>
        public static string GetAuthMechanismType() => _authMechanism?.type;

        /// <summary>
        /// Returns true when the ArborInsight (Kotlin) service is fully initialized and ready for calls.
        /// </summary>
        public static bool ServiceIsFullyInitialized() => ArborInsightServiceClient.ServiceIsFullyInitialized();
        
        /// <summary>
        /// Clears authentication state and stops data transmission.
        /// Used by ReAuthenticate() and by Abxr.StartNewSession() before starting a fresh session.
        /// </summary>
        internal static void ClearAuthenticationState()
        {
            _tokenExpiry = DateTime.MinValue;
            _keyboardAuthSuccess = null;
            _sessionId = null;
            _authMechanism = null;
            
            // Clear cached user data
            _responseData = null;
            
            // Clear stored auth value
            _enteredAuthValue = null;
            
            // Reset failed authentication attempts counter
            _failedAuthAttempts = 0;
            
            // Reset auth handoff tracking
            _sessionUsedAuthHandoff = false;

            // Session is no longer using ArborInsightService for data
            _usedArborInsightServiceForSession = false;
            
            Debug.LogWarning("AbxrLib: Authentication state cleared - data transmission stopped");
        }

        private void Start()
        {
            GetConfigData();
            _deviceId = SystemInfo.deviceUniqueIdentifier;
#if UNITY_ANDROID && !UNITY_EDITOR
            GetArborData();
            // On non-XRDM devices, org_token can be provided via launch intent (e.g. adb shell am start --es org_token "JWT...")
            if (Configuration.Instance.useAppTokens && string.IsNullOrEmpty(_orgToken))
            {
                string orgTokenIntent = Utils.GetAndroidIntentParam("org_token");
                if (!string.IsNullOrEmpty(orgTokenIntent))
                    _orgToken = orgTokenIntent;
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
            _deviceId = GetOrCreateDeviceId();
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            GetQueryData();
#endif
            SetSessionData();
            _initialized = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            ArborInsightServiceClient.WaitForServiceReady();
#endif

            // Start the deferred authentication system
            StartCoroutine(DeferredAuthenticationSystem());
            StartCoroutine(PollForReAuth());
        }

        private static IEnumerator DeferredAuthenticationSystem()
        {
            if (!Configuration.Instance.disableAutoStartAuthentication)
            {
                if (Configuration.Instance.authenticationStartDelay > 0)
                {
                    yield return new WaitForSeconds(Configuration.Instance.authenticationStartDelay);
                }
                
                yield return Authenticate();
            }
            else
            {
                Debug.Log("AbxrLib: Auto-start authentication is disabled. Call Abxr.StartAuthentication() manually when ready.");
            }
        }


        /// <summary>Used internally by Abxr.StartNewSession() to set a new session ID before re-auth. Affects standalone auth payload only; when using ArborInsightService, the service owns and generates session ID.</summary>
        public static void SetSessionId(string sessionId) => _sessionId = sessionId;

        public static IEnumerator Authenticate()
        {
            // Wait here if Start hasn't finished
            while (!_initialized) yield return null;
            
            if (!ValidateConfigValues())
            {
                // No API requests are made; DataBatcher/StorageBatcher will not send (Authenticated() is false).
                // Notify subscribers so the app can continue; use GetUserData() to check if the user actually authenticated.
                _keyboardAuthSuccess = false;
                Abxr.OnAuthCompleted?.Invoke(false, null);
                yield break;
            }
            
            // Check for auth handoff first before doing normal authentication
            if (CheckAuthHandoff()) yield break;  // Auth handoff handled everything, we're done
            
            yield return AuthRequest();
            yield return GetConfiguration();
            if (_authMechanism != null)
            {
                yield return KeyboardAuthenticate();
                // Note: KeyboardAuthenticate calls NotifyAuthCompleted when it succeeds
            }
            else
            {
                // No additional auth needed - notify completion now
                Abxr.NotifyAuthCompleted();
                _keyboardAuthSuccess = true;  // So FullyAuthenticated() returns true
                Debug.Log("AbxrLib: Authentication fully completed");
            }
        }

        public static void ReAuthenticate()
        {
            // Clear authentication state to stop data transmission
            ClearAuthenticationState();
        
            CoroutineRunner.Instance.StartCoroutine(Authenticate());
        }

        /// <summary>
        /// Update user data (UserId and UserData) and reauthenticate to sync with server
        /// Merges additionalUserData into existing UserData and sends the full updated list to the REST API or service.
        /// If userId is not provided, the current UserId is retained and not overwritten.
        /// </summary>
        /// <param name="userId">Optional user ID to update; when null, the current value is retained</param>
        /// <param name="additionalUserData">Optional additional user data dictionary to merge with existing UserData</param>
        public static void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            // Update _responseData with new values before reauthenticating
            if (_responseData == null)
            {
                Debug.LogWarning("AbxrLib: Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            // Update UserId only if provided; otherwise retain current value
            if (!string.IsNullOrEmpty(userId))
            {
                _responseData.UserId = userId;
                _responseData.UserData ??= new Dictionary<string, string>();
                _responseData.UserData["userId"] = userId;
            }

            // Merge additionalUserData into existing UserData (append/update keys)
            if (additionalUserData != null && additionalUserData.Count > 0)
            {
                _responseData.UserData ??= new Dictionary<string, string>();
                foreach (var kvp in additionalUserData)
                {
                    _responseData.UserData[kvp.Key] = kvp.Value;
                }
            }

            // Reauthenticate with effective userId (current if not provided) and full merged UserData so API gets updated list
            string effectiveUserId = !string.IsNullOrEmpty(userId) ? userId : _responseData?.UserId?.ToString();
            var fullUserData = _responseData?.UserData;
            CoroutineRunner.Instance.StartCoroutine(ReAuthenticateWithUserData(effectiveUserId, fullUserData));
        }

        private static IEnumerator ReAuthenticateWithUserData(string effectiveUserId, Dictionary<string, string> fullUserData)
        {
            // Send full merged user data to REST or service so the backend receives the updated list
            yield return AuthRequest(true, effectiveUserId, fullUserData);
            // AuthRequest will update _responseData with server response
        }

        /// <summary>
        /// Set the input source for authentication (e.g., "user", "QRlms")
        /// This indicates how the authentication value was provided
        /// </summary>
        /// <param name="inputSource">The input source value (defaults to "user" if not set)</param>
        public static void SetInputSource(string inputSource)
        {
            if (_authMechanism != null)
            {
                _authMechanism.inputSource = inputSource;
            }
        }

        private static IEnumerator PollForReAuth()
        {
            while (!UsingArborInsightServiceForData())
            {
                yield return new WaitForSeconds(60);
                if (_tokenExpiry - DateTime.UtcNow <= TimeSpan.FromMinutes(2))
                {
                    yield return AuthRequest();
                }
            }
        }
    
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Utils.SendAllData();
        }

        private static void GetConfigData()
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
            if(configData.useAppTokens)
            {
                _appToken = configData.appToken;
                _orgToken = configData.orgToken;
            }
            else //legacy AppID/OrgID/AuthSecret approach
            {
                _appId = configData.appId;
                _orgId = configData.orgId;
                _authSecret = configData.authSecret;
            }
            _buildType = configData.buildType;
            
            // Note: orgId and authSecret will still be overridden by GetArborData() if ArborServiceClient is connected (legacy path).
            // When using app tokens and XRDM, GetArborData() sets _orgToken to dynamic token.
        }
    
        private static void GetArborData()
        {
            if (!ArborServiceClient.IsConnected()) return;
        
            _partner = Partner.ArborXR;
            _deviceId = Abxr.GetDeviceId();
            _deviceTags = Abxr.GetDeviceTags();
            if(_buildType == "production_custom")
            {
               return; //Production Custom APK does not need an org token
            }

            if (Configuration.Instance.useAppTokens)
            {
                try
                {
                    string fingerprint = Abxr.GetFingerprint();
                    string orgId = Abxr.GetOrgId();
                    string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, fingerprint);
                    if (!string.IsNullOrEmpty(dynamicToken))
                        _orgToken = dynamicToken;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AbxrLib: BuildOrgTokenDynamic failed: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
            }
            else //legacy AppID/OrgID/AuthSecret approach
            {
                try
                {
                    _orgId = Abxr.GetOrgId();
                    _authSecret = Abxr.GetFingerprint();
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
        private static void GetQueryData()
        {
            if(_buildType == "production_custom")
            {
               return; //Production Custom APK does not need an org token
            }
            string orgTokenQuery = Utils.GetQueryParam("org_token", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                _orgToken = orgTokenQuery;
            }
        }
#elif (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        private static void GetQueryData()
        {
            if (_buildType == "production_custom")
            {
                return; // Production Custom does not need an org token
            }
            string orgToken = Utils.GetOrgTokenFromDesktopSources();
            if (!string.IsNullOrEmpty(orgToken))
            {
                _orgToken = orgToken;
            }
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
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
        /// <summary>
        /// Validates that we have the required auth data for the current mode before calling the API.
        /// When useAppTokens: need both appToken and orgToken (orgToken may come from config, XRDM dynamic token, or URL).
        /// When not useAppTokens: need appId, orgId, and authSecret. If validation fails, authentication fails immediately and no data is collected.
        /// </summary>
        private static bool ValidateConfigValues()
        {
            var config = Configuration.Instance;

            if (config.useAppTokens)
            {
                // App-token mode: require both appToken and orgToken; appId/orgId/authSecret are ignored.
                if (string.IsNullOrEmpty(_appToken))
                {
                    Debug.LogError("AbxrLib: App Token is missing. Cannot authenticate.");
                    return false;
                }
                if (!LooksLikeJwt(_appToken))
                {
                    Debug.LogError("AbxrLib: App Token does not look like a JWT (expected three dot-separated segments). Cannot authenticate.");
                    return false;
                }
                // Development: use App Token as org token when none set from config/Arbor/URL
                if (_buildType == "development" && string.IsNullOrEmpty(_orgToken))
                {
                    _orgToken = _appToken;
                }
                // API requires both appToken and orgToken when useAppTokens; _orgToken must be set by here (config, Arbor, URL, or development fallback)
                if (string.IsNullOrEmpty(_orgToken))
                {
                    Debug.LogError("AbxrLib: Organization Token is missing. Set it in config, connect via ArborXR device management service for a dynamic token, pass org_token in the URL (WebGL), use --org_token or arborxr_org_token.key (desktop), pass org_token as Android intent extra (APK), or set in config. Cannot authenticate.");
                    return false;
                }
                if (!LooksLikeJwt(_orgToken))
                {
                    Debug.LogError("AbxrLib: Organization Token does not look like a JWT (expected three dot-separated segments). Cannot authenticate.");
                    return false;
                }
            }
            else //legacy AppID/OrgID/AuthSecret approach: require appId, orgId, authSecret.
            {
                if (string.IsNullOrEmpty(_appId))
                {
                    Debug.LogError("AbxrLib: Application ID is missing. Cannot authenticate.");
                    return false;
                }
                if (string.IsNullOrEmpty(_orgId))
                {
                    Debug.LogError("AbxrLib: Organization ID is missing. Cannot authenticate.");
                    return false;
                }
                if (string.IsNullOrEmpty(_authSecret))
                {
                    Debug.LogError("AbxrLib: Authentication Secret is missing. Cannot authenticate.");
                    return false;
                }
            }

            // Validate restUrl, numeric ranges, and auth field formats (IsValid handles both useAppTokens and legacy).
            if (!config.IsValid())
            {
                return false;
            }
            return true;
        }

        /// <summary>Lightweight check that a string looks like a JWT (three base64url segments separated by dots). Does not verify signature or payload.</summary>
        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        public static IEnumerator KeyboardAuthenticate(string keyboardInput = null, bool invalidQrCode = false)
        {
            _keyboardAuthSuccess = false;
            _enteredAuthValue = null;
            
            if (keyboardInput != null)
            {
                string originalPrompt = _authMechanism.prompt;
                _authMechanism.prompt = keyboardInput;
                
                // Store the entered value for email and text auth methods so we can add it to UserData
                if (_authMechanism.type == "email" || _authMechanism.type == "text")
                {
                    _enteredAuthValue = keyboardInput;
                    
                    // For email type, combine with domain if provided
                    if (_authMechanism.type == "email" && !string.IsNullOrEmpty(_authMechanism.domain))
                    {
                        _enteredAuthValue += $"@{_authMechanism.domain}";
                    }
                }
                
                yield return AuthRequest(false);
                _enteredAuthValue = null;  // only need this in AuthRequest
                if (_keyboardAuthSuccess == true)
                {
                    KeyboardHandler.Destroy();
                    _failedAuthAttempts = 0;
                    
                    // Notify completion for keyboard authentication success
                    Abxr.NotifyAuthCompleted();
                    
                    yield break;
                }

                _authMechanism.prompt = originalPrompt;
            }
        
            string prompt = _failedAuthAttempts > 0 ? $"Authentication Failed ({_failedAuthAttempts})\n" : "";
            if (invalidQrCode) prompt = "Invalid QR Code\n";
            prompt += _authMechanism.prompt;
            Abxr.PresentKeyboard(prompt, _authMechanism.type, _authMechanism.domain);
            _failedAuthAttempts++;
        }

        private static void SetSessionData()
        {
            _deviceModel = DeviceModel.deviceModel;
#if UNITY_ANDROID && !UNITY_EDITOR
            _ipAddress = Utils.GetIPAddress();
            
            // Read build_fingerprint from Android manifest
            _buildFingerprint = Utils.GetAndroidManifestMetadata("com.arborxr.abxrlib.build_fingerprint");
            
            var currentAssembly = Assembly.GetExecutingAssembly();
            AssemblyName[] referencedAssemblies = currentAssembly.GetReferencedAssemblies();
            foreach (AssemblyName assemblyName in referencedAssemblies)
            {
                if (assemblyName.Name == "XRDM.SDK.External.Unity")
                {
                    _xrdmVersion = assemblyName.Version.ToString();
                    break;
                }
            }
#endif
            //TODO Geolocation
        }

        private static IEnumerator AuthRequest(bool withRetry = true, string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            if (string.IsNullOrEmpty(_sessionId)) _sessionId = Guid.NewGuid().ToString();
        
            var config = Configuration.Instance;
            
            // Get SSO access token if SSO is active
            string ssoAccessToken = null;
            if (Abxr.GetIsAuthenticated())
            {
                ssoAccessToken = Abxr.GetAccessToken();
            }
            
            // When userId param is null, use current response userId so we do not overwrite it on the wire (e.g. SetUserData(null, additionalUserData))
            string payloadUserId = !string.IsNullOrEmpty(userId) ? userId : _responseData?.UserId?.ToString();
            
            // useAppTokens: _orgToken is resolved in ValidateConfigValues (required for API); we always send both appToken and orgToken when useAppTokens.
            string payloadBuildType = (_buildType == "production_custom") ? "production" : _buildType;
            var data = new AuthPayload
            {
                appId = config.useAppTokens ? null : _appId, //legacy only
                orgId = config.useAppTokens ? null : _orgId, //legacy only
                authSecret = config.useAppTokens ? null : _authSecret, //legacy only
                appToken = config.useAppTokens ? _appToken : null,
                orgToken = config.useAppTokens ? _orgToken : null,
                buildType = payloadBuildType, // Production (Custom APK) sends "production" to API
                deviceId = _deviceId,
                userId = payloadUserId,
                tags = _deviceTags,
                sessionId = _sessionId,
                partner = _partner.ToString().ToLower(),
                ipAddress = _ipAddress,
                deviceModel = _deviceModel,
                geolocation = new Dictionary<string, string>(),
                osVersion = SystemInfo.operatingSystem,
                xrdmVersion = _xrdmVersion,
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                abxrLibType = "unity",
                abxrLibVersion = AbxrLibVersion.Version,
                buildFingerprint = _buildFingerprint,
                SSOAccessToken = ssoAccessToken,
                authMechanism = CreateAuthMechanismDict(payloadUserId, additionalUserData)
            };

            int nRetrySeconds = Configuration.Instance.sendRetryIntervalSeconds;
            bool bSuccess = false;
            while (!bSuccess)
            {
                bool useService = ServiceIsFullyInitialized();
                string responseJson = null;

                if (useService)
                {
                    try
                    {
                        ArborInsightServiceClient.SetAuthPayloadForRequest(config.restUrl ?? "https://lib-backend.xrdm.app/", data, (int)_partner);
                        responseJson = ArborInsightServiceClient.AuthRequest(payloadUserId ?? "", Utils.DictToString(data.authMechanism));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AbxrLib: Failed to set service properties for auth: {ex.Message}");
                    }
                }
                else
                {
                    var restHolder = new AuthResponseHolder();
                    yield return SendAuthRequestRest(JsonConvert.SerializeObject(data), restHolder);
                    responseJson = restHolder.Response;
                }

                if (HandleAuthResponse(responseJson, fromService: useService, handoff: false))
                {
                    bSuccess = true;
                    break;
                }
                if (!withRetry)
                {
                    Abxr.OnAuthCompleted?.Invoke(false, null);
                    break;
                }
                Debug.LogWarning($"AbxrLib: Authentication attempt failed, retrying in {nRetrySeconds} seconds...");
                yield return new WaitForSeconds(nRetrySeconds);
            }
        }

        /// <summary>Holds the response body from a single REST auth attempt (used so coroutine can "return" it).</summary>
        private class AuthResponseHolder
        {
            public string Response;
        }

        /// <summary>Performs one POST to /v1/auth/token and sets holder.Response to the response body or null on failure.</summary>
        private static IEnumerator SendAuthRequestRest(string json, AuthResponseHolder holder)
        {
            holder.Response = null;
            var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token");
            UnityWebRequest request = null;
            try
            {
                request = new UnityWebRequest(fullUri.ToString(), "POST");
                Utils.BuildRequest(request, json);
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Authentication request creation failed: {ex.Message}");
                yield break;
            }

            yield return request.SendWebRequest();

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                    holder.Response = request.downloadHandler?.text;
                else
                {
                    string errorMessage = HandleNetworkError(request);
                    Debug.LogWarning($"AbxrLib: AuthRequest failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Authentication response handling failed: {ex.Message}");
            }
            finally
            {
                request?.Dispose();
            }
        }

        /// <summary>Parses auth response text and applies it. If fromService: no JWT validation, set safe expiry. Else: validate token and set expiry (handoff or JWT). All response data (Modules, UserData, etc.) is applied the same in one place.</summary>
        private static bool HandleAuthResponse(string responseText, bool fromService, bool handoff = false)
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
                    if (handoff)
                        _tokenExpiry = DateTime.UtcNow.AddHours(24);
                    else
                    {
                        var decodedJwt = Utils.DecodeJwt(postResponse.Token);
                        if (decodedJwt == null)
                        {
                            Debug.LogError("AbxrLib: Failed to decode JWT token - authentication cannot proceed");
                            return false;
                        }
                        if (!decodedJwt.ContainsKey("exp"))
                        {
                            Debug.LogError("AbxrLib: Invalid JWT token: missing expiration field");
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
                }

                _responseData = postResponse;
                if (_responseData?.Modules?.Count > 1)
                    _responseData.Modules = _responseData.Modules.OrderBy(m => m.Order).ToList();
                if (!string.IsNullOrEmpty(_enteredAuthValue) && _responseData != null)
                {
                    _responseData.UserData ??= new Dictionary<string, string>();
                    var keyName = _authMechanism?.type == "email" ? "email" : "text";
                    _responseData.UserData[keyName] = _enteredAuthValue;
                }
                if (_keyboardAuthSuccess == false) _keyboardAuthSuccess = true;
                if (fromService)
                    _usedArborInsightServiceForSession = true;

                if (!fromService && handoff)
                {
                    Debug.Log($"AbxrLib: Authentication handoff successful. Modules: {_responseData?.Modules?.Count}");
                    Abxr.NotifyAuthCompleted();
                    _keyboardAuthSuccess = true;
                    _sessionUsedAuthHandoff = true;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Authentication response handling failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles network errors and determines appropriate error messages
        /// </summary>
        private static string HandleNetworkError(UnityWebRequest request)
        {
            string errorMessage;
            
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    errorMessage = $"Connection error: {request.error}";
                    break;
                case UnityWebRequest.Result.DataProcessingError:
                    errorMessage = $"Data processing error: {request.error}";
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    errorMessage = $"Protocol error ({request.responseCode}): {request.error}";
                    break;
                default:
                    errorMessage = $"Unknown error: {request.error}";
                    break;
            }
            
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                errorMessage += $" - Response: {request.downloadHandler.text}";
            }
            
            return errorMessage;
        }

        private static IEnumerator GetConfiguration()
        {
            string configJson = null;
            if (ServiceIsFullyInitialized())
                configJson = ArborInsightServiceClient.GetAppConfig();
            else
            {
                string restJson = null;
                yield return SendConfigRequestRest(j => restJson = j);
                configJson = restJson;
            }

            if (string.IsNullOrEmpty(configJson)) yield break;

            try
            {
                var payload = JsonConvert.DeserializeObject<ConfigPayload>(configJson);
                if (payload == null)
                {
                    Debug.LogWarning("AbxrLib: Failed to deserialize configuration response, using default configuration");
                    yield break;
                }
                SetConfigFromPayload(payload);
                if (payload.authMechanism != null)
                {
                    _authMechanism = new AuthMechanism
                    {
                        type = payload.authMechanism.type,
                        prompt = payload.authMechanism.prompt,
                        domain = payload.authMechanism.domain,
                        inputSource = string.IsNullOrEmpty(payload.authMechanism.inputSource) ? "user" : payload.authMechanism.inputSource
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration response handling failed: {ex.Message}");
            }
        }

        /// <summary>Performs one GET to /v1/storage/config with auth headers. Invokes onComplete with the response body JSON (or null on failure).</summary>
        private static IEnumerator SendConfigRequestRest(System.Action<string> onComplete)
        {
            var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/storage/config");

            UnityWebRequest request = null;
            try
            {
                request = UnityWebRequest.Get(fullUri.ToString());
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
                    string errorMessage = HandleNetworkError(request);
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
    
        public static void SetAuthHeaders(UnityWebRequest request, string json = "")
        {
            // Check if we have valid authentication tokens
            if (_responseData == null || string.IsNullOrEmpty(_responseData.Token) || string.IsNullOrEmpty(_responseData.Secret))
            {
                Debug.LogError("AbxrLib: Cannot set auth headers - authentication tokens are missing");
                return;
            }
            
            request.SetRequestHeader("Authorization", "Bearer " + _responseData.Token);
        
            string unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            request.SetRequestHeader("x-abxrlib-timestamp", unixTimeSeconds);
        
            string hashString = _responseData.Token + _responseData.Secret + unixTimeSeconds;
            if (!string.IsNullOrEmpty(json))
            {
                uint crc = Utils.ComputeCRC(json);
                hashString += crc;
            }
        
            request.SetRequestHeader("x-abxrlib-hash", Utils.ComputeSha256Hash(hashString));
        }

        private static Dictionary<string, string> CreateAuthMechanismDict(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            var dict = new Dictionary<string, string>();
            bool hasUserData = additionalUserData != null && additionalUserData.Count > 0;
            if (_authMechanism == null && string.IsNullOrEmpty(userId) && !hasUserData) return dict;

            if (!string.IsNullOrEmpty(userId))
            {
                dict["type"] = "custom";
                dict["prompt"] = userId;
                if (additionalUserData != null)
                {
                    foreach (var item in additionalUserData)
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

            if (_authMechanism != null)
            {
                if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
                if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
                if (!string.IsNullOrEmpty(_authMechanism.domain)) dict["domain"] = _authMechanism.domain;
                if (!string.IsNullOrEmpty(_authMechanism.inputSource)) dict["inputSource"] = _authMechanism.inputSource;
            }
            // Include full user data when provided (e.g. SetUserData reauth with no userId change) so API gets updated list
            if (additionalUserData != null)
            {
                foreach (var item in additionalUserData)
                {
                    if (item.Key != "type" && item.Key != "prompt")
                    {
                        dict[item.Key] = item.Value;
                    }
                }
            }
            return dict;
        }
    
        private static void SetConfigFromPayload(ConfigPayload payload)
        {
            if (!string.IsNullOrEmpty(payload.restUrl)) Configuration.Instance.restUrl = payload.restUrl;
            if (!string.IsNullOrEmpty(payload.sendRetriesOnFailure)) Configuration.Instance.sendRetriesOnFailure = Convert.ToInt32(payload.sendRetriesOnFailure);
            if (!string.IsNullOrEmpty(payload.sendRetryInterval)) Configuration.Instance.sendRetryIntervalSeconds = Convert.ToInt32(payload.sendRetryInterval);
            if (!string.IsNullOrEmpty(payload.sendNextBatchWait)) Configuration.Instance.sendNextBatchWaitSeconds = Convert.ToInt32(payload.sendNextBatchWait);
            if (!string.IsNullOrEmpty(payload.stragglerTimeout)) Configuration.Instance.stragglerTimeoutSeconds = Convert.ToInt32(payload.stragglerTimeout);
            if (!string.IsNullOrEmpty(payload.dataEntriesPerSendAttempt)) Configuration.Instance.dataEntriesPerSendAttempt = Convert.ToInt32(payload.dataEntriesPerSendAttempt);
            if (!string.IsNullOrEmpty(payload.storageEntriesPerSendAttempt)) Configuration.Instance.storageEntriesPerSendAttempt = Convert.ToInt32(payload.storageEntriesPerSendAttempt);
            if (!string.IsNullOrEmpty(payload.pruneSentItemsOlderThan)) Configuration.Instance.pruneSentItemsOlderThanHours = Convert.ToInt32(payload.pruneSentItemsOlderThan);
            if (!string.IsNullOrEmpty(payload.maximumCachedItems)) Configuration.Instance.maximumCachedItems = Convert.ToInt32(payload.maximumCachedItems);
            if (!string.IsNullOrEmpty(payload.retainLocalAfterSent)) Configuration.Instance.retainLocalAfterSent = Convert.ToBoolean(payload.retainLocalAfterSent);
            // Performance / tracking periods (backend may send as numeric strings, e.g. "1", "0.5", "10")
            if (!string.IsNullOrEmpty(payload.positionCapturePeriod) && float.TryParse(payload.positionCapturePeriod, out float positionPeriod))
                Configuration.Instance.positionTrackingPeriodSeconds = Mathf.Clamp(positionPeriod, 0.1f, 60f);
            if (!string.IsNullOrEmpty(payload.frameRateCapturePeriod) && float.TryParse(payload.frameRateCapturePeriod, out float frameRatePeriod))
                Configuration.Instance.frameRateTrackingPeriodSeconds = Mathf.Clamp(frameRatePeriod, 0.1f, 60f);
            if (!string.IsNullOrEmpty(payload.telemetryCapturePeriod) && float.TryParse(payload.telemetryCapturePeriod, out float telemetryPeriod))
                Configuration.Instance.telemetryTrackingPeriodSeconds = Mathf.Clamp(telemetryPeriod, 1f, 300f);
        }

        /// <summary>
        /// Check for authentication handoff from external launcher apps
        /// Looks for auth_handoff parameter in command line args, Android intents, or WebGL query params
        /// </summary>
        private static bool CheckAuthHandoff()
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
            return HandleAuthResponse(handoffJson, fromService: false, handoff: true);
        }
    
        [Preserve]
        private class AuthMechanism
        {
            public string type;
            public string prompt;
            public string domain;
            public string inputSource = "user";

            [Preserve]
            public AuthMechanism() {}
        }

        [Preserve]
        private class ConfigPayload
        {
            public AuthMechanism authMechanism;
            public string frameRateCapturePeriod;
            public string telemetryCapturePeriod;
            public string restUrl;
            public string sendRetriesOnFailure;
            public string sendRetryInterval;
            public string sendNextBatchWait;
            public string stragglerTimeout;
            public string dataEntriesPerSendAttempt;
            public string storageEntriesPerSendAttempt;
            public string pruneSentItemsOlderThan;
            public string maximumCachedItems;
            public string retainLocalAfterSent;
            public string positionCapturePeriod;
        
            [Preserve]
            public ConfigPayload() {}
        }

        internal class AuthPayload
        {
            public string appId; //legacy only
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string orgId; //legacy only
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string authSecret; //legacy only
            public string appToken;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string orgToken;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string buildType;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string deviceId;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string userId;
            public string SSOAccessToken;  // optional - SSO access token when SSO is active
            public string[] tags;
            public string sessionId;
            public string partner;
            public string ipAddress;
            public string deviceModel;
            public Dictionary<string, string> geolocation;
            public string osVersion;
            public string xrdmVersion;
            public string appVersion;
            public string unityVersion;
            public string abxrLibType;
            public string abxrLibVersion;
            public string buildFingerprint;  // optional - set on Android devices
            public Dictionary<string, string> authMechanism;
        }

        [Preserve]
        public class AuthResponse
        {
            public string Token;
            public string Secret;
            public Dictionary<string, string> UserData;
            public object UserId;
            public string AppId;
            public string PackageName;
            public List<ModuleData> Modules;

            [Preserve]
            public AuthResponse() {}
        }
        
        public class ModuleData
        {
            public string Id;
            public string Name;
            public string Target;
            public int Order;
        }

        private enum Partner
        {
            None,
            ArborXR
        }
    }
}
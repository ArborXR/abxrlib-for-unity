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
            // Check if we have a valid token and it hasn't expired
            return !string.IsNullOrEmpty(_responseData?.Token) && 
                   !string.IsNullOrEmpty(_responseData?.Secret) && 
                   DateTime.UtcNow <= _tokenExpiry &&
                   _keyboardAuthSuccess == true;
        }
        
        public static bool SessionUsedAuthHandoff() => _sessionUsedAuthHandoff;
        
        /// <summary>
        /// Gets the authentication mechanism type, or null if no mechanism is set
        /// </summary>
        public static string GetAuthMechanismType() => _authMechanism?.type;

        /// <summary>
        /// Returns true when the ArborInsight (Kotlin) service is fully initialized and ready for calls.
        /// </summary>
        public static bool ServiceIsFullyInitialized()
        {
            try
            {
                return ArborInsightServiceClient.ServiceIsFullyInitialized();
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Clears authentication state and stops data transmission
        /// </summary>
        private static void ClearAuthenticationState()
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
#elif UNITY_WEBGL && !UNITY_EDITOR
            GetQueryData();
            _deviceId = GetOrCreateDeviceId();
#endif
            SetSessionData();
            _initialized = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Only wait for service readiness if the ArborInsightService APK is installed; otherwise fail fast and use standalone mode.
            if (ArborInsightServiceClient.IsServicePackageInstalled())
            {
                for (int i = 0; i < 40; i++)
                {
                    try
                    {
                        if (ServiceIsFullyInitialized()) break;
                    }
                    catch { }
                    System.Threading.Thread.Sleep(250);
                }
            }
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
        /// Updates the authentication response with new user information without clearing authentication state
        /// The server API allows reauthenticate to update these values
        /// </summary>
        /// <param name="userId">Optional user ID to update</param>
        /// <param name="additionalUserData">Optional additional user data dictionary to merge with existing UserData</param>
        public static void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
        {
            // Update _responseData with new values before reauthenticating
            if (_responseData == null)
            {
                Debug.LogWarning("AbxrLib: Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            // Update UserId if provided
            if (!string.IsNullOrEmpty(userId))
            {
                _responseData.UserId = userId;
            }

            // Merge additionalUserData into existing UserData
            if (additionalUserData != null && additionalUserData.Count > 0)
            {
                _responseData.UserData ??= new Dictionary<string, string>();
                foreach (var kvp in additionalUserData)
                {
                    _responseData.UserData[kvp.Key] = kvp.Value;
                }
            }

            // Reauthenticate to sync with server (without clearing authentication state)
            CoroutineRunner.Instance.StartCoroutine(ReAuthenticateWithUserData(userId, additionalUserData));
        }

        private static IEnumerator ReAuthenticateWithUserData(string userId, Dictionary<string, string> additionalUserData)
        {
            // Call AuthRequest with user data parameters
            yield return AuthRequest(true, userId, additionalUserData);
            
            // Note: AuthRequest will update _responseData with server response
            // The server should return the updated UserId and UserData
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
            while (true)
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
            string orgTokenQuery = Utils.GetQueryParam("org_token", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                _orgToken = orgTokenQuery;
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
                    // Development: use App Token as org token when none set from config/Arbor/URL
                    _orgToken = _appToken;
                }
                // API requires both appToken and orgToken when useAppTokens; _orgToken must be set by here (config, Arbor, URL, or development fallback)
                if (string.IsNullOrEmpty(_orgToken))
                {
                    Debug.LogError("AbxrLib: Organization Token is missing. Set it in config, connect via ArborXR device management service for a dynamic token, or pass org_token in the URL. Cannot authenticate.");
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
            
            // useAppTokens: _orgToken is resolved in ValidateConfigValues (required for API); we always send both appToken and orgToken when useAppTokens.
            string payloadBuildType = (_buildType == "production_custom") ? "production" : _buildType;
            var data = new AuthPayload
            {
                appId = config.useAppTokens ? null : _appId, //legacy only
                orgId = config.useAppTokens ? null : _orgId, //legacy only
                authSecret = config.useAppTokens ? null : _authSecret, //legacy only
                appToken = config.useAppTokens ? null : _appToken, 
                orgToken = config.useAppTokens ? _orgToken : null,
                buildType = payloadBuildType, // Production (Custom APK) sends "production" to API
                deviceId = _deviceId,
                userId = userId,
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
                authMechanism = CreateAuthMechanismDict(userId, additionalUserData)
            };

            if (ServiceIsFullyInitialized())
            {
                try
                {
                    ArborInsightServiceClient.set_RestUrl(config.restUrl ?? "https://lib-backend.xrdm.app/");
                    foreach (var fi in typeof(AuthPayload).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var field = fi.Name;
                        switch (field)
                        {
                            case nameof(AuthPayload.appId):
                                if (data.appId != null) ArborInsightServiceClient.set_AppID(data.appId);
                                break;
                            case nameof(AuthPayload.orgId):
                                if (data.orgId != null) ArborInsightServiceClient.set_OrgID(data.orgId);
                                break;
                            case nameof(AuthPayload.authSecret):
                                if (data.authSecret != null) ArborInsightServiceClient.set_AuthSecret(data.authSecret);
                                break;
                            case nameof(AuthPayload.appToken):
                                if (data.appToken != null) ArborInsightServiceClient.set_AppToken(data.appToken);
                                break;
                            case nameof(AuthPayload.orgToken):
                                if (data.orgToken != null) ArborInsightServiceClient.set_OrgToken(data.orgToken);
                                break;
                            case nameof(AuthPayload.buildType):
                                if (data.buildType != null) ArborInsightServiceClient.set_BuildType(data.buildType);
                                break;
                            case nameof(AuthPayload.deviceId):
                                if (data.deviceId != null) ArborInsightServiceClient.set_DeviceID(data.deviceId);
                                break;
                            case nameof(AuthPayload.userId):
                                if (data.userId != null) ArborInsightServiceClient.set_UserID(data.userId);
                                break;
                            case nameof(AuthPayload.tags):
                                if (data.tags != null) ArborInsightServiceClient.set_Tags(data.tags.ToList());
                                break;
                            case nameof(AuthPayload.partner):
                                ArborInsightServiceClient.set_Partner((int)_partner);
                                break;
                            case nameof(AuthPayload.ipAddress):
                                if (data.ipAddress != null) ArborInsightServiceClient.set_IpAddress(data.ipAddress);
                                break;
                            case nameof(AuthPayload.deviceModel):
                                if (data.deviceModel != null) ArborInsightServiceClient.set_DeviceModel(data.deviceModel);
                                break;
                            case nameof(AuthPayload.geolocation):
                                ArborInsightServiceClient.set_GeoLocation(data.geolocation ?? new Dictionary<string, string>());
                                break;
                            case nameof(AuthPayload.osVersion):
                                if (data.osVersion != null) ArborInsightServiceClient.set_OsVersion(data.osVersion);
                                break;
                            case nameof(AuthPayload.xrdmVersion):
                                if (data.xrdmVersion != null) ArborInsightServiceClient.set_XrdmVersion(data.xrdmVersion);
                                break;
                            case nameof(AuthPayload.appVersion):
                                if (data.appVersion != null) ArborInsightServiceClient.set_AppVersion(data.appVersion);
                                break;
                            case nameof(AuthPayload.unityVersion):
                                if (data.unityVersion != null) ArborInsightServiceClient.set_UnityVersion(data.unityVersion);
                                break;
                            case nameof(AuthPayload.abxrLibType):
                                if (data.abxrLibType != null) ArborInsightServiceClient.set_AbxrLibType(data.abxrLibType);
                                break;
                            case nameof(AuthPayload.abxrLibVersion):
                                if (data.abxrLibVersion != null) ArborInsightServiceClient.set_AbxrLibVersion(data.abxrLibVersion);
                                break;
                            default:
                                // AuthPayload fields without a service setter are ignored
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AbxrLib: Failed to set service properties for auth: {ex.Message}");
                }

                var authMechanismDict = CreateAuthMechanismDict(userId, additionalUserData);
                int nRetrySeconds = Configuration.Instance.sendRetryIntervalSeconds;
                bool bSuccess = false;
                while (!bSuccess)
                {
                    var eRet = (AbxrResult)ArborInsightServiceClient.AuthRequest(userId ?? "", Utils.DictToString(authMechanismDict));
                    if (eRet == AbxrResult.OK)
                    {
                        _keyboardAuthSuccess = true;
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

                if (bSuccess)
                {
                    try
                    {
                        string token = ArborInsightServiceClient.get_ApiToken();
                        string secret = ArborInsightServiceClient.get_ApiSecret();
                        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(secret))
                        {
                            _responseData = new AuthResponse { Token = token, Secret = secret };
                            _tokenExpiry = DateTime.UtcNow.AddHours(24);
                            _usedArborInsightServiceForSession = true; // This session uses the service for data; never switch to service later if we had started standalone
                        }
                    }
                    catch { }
                }
                yield break;
            }

            string json = JsonConvert.SerializeObject(data);
            var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token");
            
            bool success = false;
            while (!success)
            {
                // Create request and handle creation errors
                UnityWebRequest request;
                
                // Request creation with error handling (no yield statements)
                try
                {
                    request = new UnityWebRequest(fullUri.ToString(), "POST");
                    Utils.BuildRequest(request, json);
                    
                    // Set timeout to prevent hanging requests
                    request.timeout = Configuration.Instance.requestTimeoutSeconds;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AbxrLib: Authentication request creation failed: {ex.Message}");
                    break; // Exit retry loop - this is a non-retryable error
                }
                
                // Send request (yield outside try-catch)
                yield return request.SendWebRequest();
                
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        if (HandleAuthResponse(request.downloadHandler.text))
                        {
                            success = true;
                            break; // don't invoke completion yet; additional auth may be required
                        }
                    }
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
                    // Always dispose of request
                    request?.Dispose();
                }

                if (!withRetry)
                {
                    Abxr.OnAuthCompleted?.Invoke(false, null);
                    break;
                }

                int retrySeconds = Configuration.Instance.sendRetryIntervalSeconds;
                Debug.LogWarning($"AbxrLib: Authentication attempt failed, retrying in {retrySeconds} seconds...");
                yield return new WaitForSeconds(retrySeconds);
            }
        }
        
        private static bool HandleAuthResponse(string responseText, bool handoff = false)
        {
            try
            {
                AuthResponse postResponse = JsonConvert.DeserializeObject<AuthResponse>(responseText);

                // Validate response data
                if (postResponse == null || string.IsNullOrEmpty(postResponse.Token))
                {
                    throw new Exception("Invalid authentication response: missing token");
                }

                if (handoff)
                {
                    // Set token expiry to far in the future since we're trusting the handoff
                    _tokenExpiry = DateTime.UtcNow.AddHours(24);
                }
                else
                {
                    // Decode JWT with error handling
                    Dictionary<string, object> decodedJwt = Utils.DecodeJwt(postResponse.Token);
                    if (decodedJwt == null)
                    {
                        throw new Exception("Failed to decode JWT token - authentication cannot proceed");
                    }
                        
                    if (!decodedJwt.ContainsKey("exp"))
                    {
                        throw new Exception("Invalid JWT token: missing expiration field");
                    }
                        
                    try
                    {
                        _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Invalid JWT token expiration: {ex.Message}");
                    }
                }

                _responseData = postResponse;
                if (_responseData.Modules?.Count > 1)
                {
                    _responseData.Modules = _responseData.Modules.OrderBy(m => m.Order).ToList();
                }
                
                // Add entered email/text value to UserData if we have one stored
                if (!string.IsNullOrEmpty(_enteredAuthValue))
                {
                    // Initialize UserData if it's null
                    _responseData.UserData ??= new Dictionary<string, string>();
                            
                    // Determine the key name based on auth type
                    string keyName = _authMechanism?.type == "email" ? "email" : "text";
                    _responseData.UserData[keyName] = _enteredAuthValue;
                }

                if (_keyboardAuthSuccess == false) _keyboardAuthSuccess = true;

                if (handoff)
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
            if (ServiceIsFullyInitialized())
            {
                _dictAuthMechanism = ArborInsightServiceClient.get_AppConfigAuthMechanism();
                if (_dictAuthMechanism != null && _dictAuthMechanism.Count > 0)
                {
                    _authMechanism = new AuthMechanism();
                    if (_dictAuthMechanism.TryGetValue("type", out var type)) _authMechanism.type = type;
                    if (_dictAuthMechanism.TryGetValue("prompt", out var prompt)) _authMechanism.prompt = prompt;
                    if (_dictAuthMechanism.TryGetValue("domain", out var domain)) _authMechanism.domain = domain;
                    if (_dictAuthMechanism.TryGetValue("inputSource", out var inputSource)) _authMechanism.inputSource = inputSource;
                    if (string.IsNullOrEmpty(_authMechanism.inputSource)) _authMechanism.inputSource = "user";
                }
                yield break;
            }

            var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/storage/config");
            
            // Create request and handle creation errors
            UnityWebRequest request;
            
            // Request creation with error handling (no yield statements)
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
                yield break;
            }
            
            // Send request (yield outside try-catch)
            yield return request.SendWebRequest();
            
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    if (string.IsNullOrEmpty(response))
                    {
                        Debug.LogWarning("AbxrLib: Empty configuration response, using default configuration");
                    }
                    else
                    {
                        var config = JsonConvert.DeserializeObject<ConfigPayload>(response);
                        if (config == null)
                        {
                            Debug.LogWarning("AbxrLib: Failed to deserialize configuration response, using default configuration");
                        }
                        else
                        {
                            SetConfigFromPayload(config);
                            _authMechanism = config.authMechanism;
                            // Ensure inputSource is initialized to "user" if not set
                            if (_authMechanism != null && string.IsNullOrEmpty(_authMechanism.inputSource))
                            {
                                _authMechanism.inputSource = "user";
                            }
                        }
                    }
                }
                else
                {
                    string errorMessage = HandleNetworkError(request);
                    Debug.LogWarning($"AbxrLib: GetConfiguration failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration response handling failed: {ex.Message}");
            }
            
            // Always dispose of request
            request?.Dispose();
        }
    
        public static void SetAuthHeaders(UnityWebRequest request, string json = "")
        {
            // Check if we have valid authentication tokens
            if (string.IsNullOrEmpty(_responseData.Token) || string.IsNullOrEmpty(_responseData.Secret))
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
            if (_authMechanism == null && string.IsNullOrEmpty(userId)) return dict;

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

            if (_authMechanism == null) return dict;
            if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
            if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
            if (!string.IsNullOrEmpty(_authMechanism.domain)) dict["domain"] = _authMechanism.domain;
            if (!string.IsNullOrEmpty(_authMechanism.inputSource)) dict["inputSource"] = _authMechanism.inputSource;
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
            return HandleAuthResponse(handoffJson, true);
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

        private class AuthPayload
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
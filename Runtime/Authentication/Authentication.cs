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
using System.Reflection;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.ServiceClient;
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
        private static string _orgId;
        private static string _deviceId;
        private static string _authSecret;
        private static string _appId;
        private static Partner _partner = Partner.None;
        private static string _deviceModel;
        private static string[] _deviceTags;
        private static string _xrdmVersion;
        private static string _ipAddress;
        private static string _sessionId;
        private static int _failedAuthAttempts;
        
        private static AuthMechanism _authMechanism;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        
        private static AuthResponse _responseData;
        public static AuthResponse GetAuthResponse() => _responseData;
        
        // Store entered email/text value for email and text auth methods
        private static string _enteredAuthValue;
        
        private static AuthResponse _authHandoffData;
        public static AuthResponse GetAuthHandoffData() => _authHandoffData;
        
        // Complete authentication response data
        private static List<Abxr.ModuleData> _authResponseModuleData;
    
        private const string DeviceIdKey = "abxrlib_device_id";

        private static bool? _keyboardAuthSuccess;
        private static bool _initialized;
        
        // Auth handoff for external launcher apps
        private static bool _authHandoffCompleted = false;
        private static bool _sessionUsedAuthHandoff = false;

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
            _authResponseModuleData = null;
            
            // Clear stored auth value
            _enteredAuthValue = null;
            
            // Reset failed authentication attempts counter
            _failedAuthAttempts = 0;
            
            // Reset auth handoff tracking
            _sessionUsedAuthHandoff = false;
            
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
                Abxr.OnAuthCompleted?.Invoke(false, null);
                yield break;
            }
            
            // Check for auth handoff first before doing normal authentication
            yield return CheckAuthHandoff();
            if (_authHandoffCompleted)
            {
                yield break; // Auth handoff handled everything, we're done
            }
            
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
            
            // Reset auth handoff state
            _authHandoffCompleted = false;
        
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
            // Reset auth handoff state (but don't clear authentication state)
            _authHandoffCompleted = false;
            
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
            _appId = Configuration.Instance.appID;
            _orgId = Configuration.Instance.orgID;
            _authSecret = Configuration.Instance.authSecret;
        }
    
        private static void GetArborData()
        {
            if (!ArborServiceClient.IsConnected()) return;
        
            _partner = Partner.ArborXR;
            _orgId = Abxr.GetOrgId();
            _deviceId = Abxr.GetDeviceId();
            _deviceTags = Abxr.GetDeviceTags();
            try
            {
                var authSecret = Abxr.GetFingerprint();
                _authSecret = authSecret;
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
        private static void GetQueryData()
        {
            string orgIdQuery = Utils.GetQueryParam("abxr_orgid", Application.absoluteURL);
            if (!string.IsNullOrEmpty(orgIdQuery))
            {
                _orgId = orgIdQuery;
            }
            
            string authSecretQuery = Utils.GetQueryParam("abxr_auth_secret", Application.absoluteURL);
            if (!string.IsNullOrEmpty(authSecretQuery))
            {
                _authSecret = authSecretQuery;
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
        private static bool ValidateConfigValues()
        {
            if (!Configuration.Instance.IsValid()) return false;
            
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
                Debug.LogError("AbxrLib: Authentication Secret is missing. Cannot authenticate");
                return false;
            }
            
            return true;
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
        
            var data = new AuthPayload
            {
                appId = _appId,
                orgId = _orgId,
                authSecret = _authSecret,
                deviceId = _deviceId,
                userId = userId, // Include userId in payload if provided
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
                authMechanism = CreateAuthMechanismDict(userId, additionalUserData)
            };
        
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
                        AuthResponse postResponse = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
                        
                        // Validate response data
                        if (postResponse == null || string.IsNullOrEmpty(postResponse.Token))
                        {
                            throw new Exception("Invalid authentication response: missing token");
                        }
                        
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
                        
                        _responseData = postResponse;
                        _authResponseModuleData = Utils.ConvertToModuleDataList(postResponse.Modules);
                        
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
                        
                        // Don't notify completion yet since additional auth may be required
                        success = true;
                        break;
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

            if (_authMechanism == null && !string.IsNullOrEmpty(userId))
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

        public static List<Abxr.ModuleData> GetModuleData() => _authResponseModuleData;

        /// <summary>
        /// Check for authentication handoff from external launcher apps
        /// Looks for auth_handoff parameter in command line args, Android intents, or WebGL query params
        /// </summary>
        private static IEnumerator CheckAuthHandoff()
        {
            // Check Android intent parameters first
            string handoffJson = Utils.GetAndroidIntentParam("auth_handoff");

            // If not found, check command line arguments
            if (string.IsNullOrEmpty(handoffJson))
            {
                handoffJson = Utils.GetCommandLineArg("auth_handoff");
            }
            
            // If not found, check WebGL query parameters (for consistency)
            if (string.IsNullOrEmpty(handoffJson))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                handoffJson = Utils.GetQueryParam("auth_handoff", Application.absoluteURL);
#endif
            }
            
            if (!string.IsNullOrEmpty(handoffJson))
            {
                yield return ProcessAuthHandoff(handoffJson);
            }
            
            yield return null;
        }

        /// <summary>
        /// Process authentication handoff JSON data and set up authentication state
        /// </summary>
        private static IEnumerator ProcessAuthHandoff(string handoffJson)
        {
            bool success = false;
            
            try
            {
                Debug.Log("AbxrLib: Processing authentication handoff from external launcher");
                
                // Parse the handoff JSON
                AuthResponse handoffData = null;
                try 
                {
                    handoffData = JsonConvert.DeserializeObject<AuthResponse>(handoffJson);
                }
                catch (Exception ex)
                {
                    // Log error with consistent format and include JSON parsing context
                    Debug.LogError($"AbxrLib: Failed to parse handoff JSON: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                    yield break;
                }
                
                // Validate that we have the required fields
                if (handoffData == null || string.IsNullOrEmpty(handoffData.Token) || string.IsNullOrEmpty(handoffData.Secret))
                {
                    Debug.LogWarning($"AbxrLib: Authentication handoff missing required fields (handoffData null: {handoffData == null}, Token empty: {string.IsNullOrEmpty(handoffData?.Token)}, Secret empty: {string.IsNullOrEmpty(handoffData?.Secret)}), falling back to normal auth");
                    yield break;
                }
                
                // Cache user data from handoff
                _responseData = new AuthResponse
                {
                    Token = handoffData.Token,
                    Secret = handoffData.Secret,
                    UserId = handoffData.UserId,
                    AppId = handoffData.AppId,
                    PackageName = handoffData.PackageName,
                    UserData = handoffData.UserData,
                    Modules = handoffData.Modules
                };
                _authResponseModuleData = new List<Abxr.ModuleData>();
                
                // Convert modules if provided
                if (handoffData.Modules != null)
                {
                    _authResponseModuleData = Utils.ConvertToModuleDataList(handoffData.Modules);
                }
                
                // Set token expiry to far in the future since we're trusting the handoff
                _tokenExpiry = DateTime.UtcNow.AddHours(24);
                
                // Mark handoff as completed
                _authHandoffCompleted = true;
                _sessionUsedAuthHandoff = true;
                
                Debug.Log($"AbxrLib: Authentication handoff successful. Modules: {_authResponseModuleData?.Count ?? 0}");
                
                Abxr.NotifyAuthCompleted();
                _keyboardAuthSuccess = true;
                
                success = true;
                _authHandoffData = handoffData;
            }
            catch (Exception ex)
            {
                // Log error with consistent format and include handoff processing context
                Debug.LogError($"AbxrLib: Failed to process authentication handoff: {ex.Message}\n" +
                              $"Exception Type: {ex.GetType().Name}\n" +
                              $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                _authHandoffCompleted = false;
            }
            
            // Yield outside of try-catch block
            if (success)
            {
                yield return null;
            }
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
            public string appId;
            public string orgId;
            public string authSecret;
            public string deviceId;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string userId;
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
            public List<Dictionary<string, object>> Modules;

            [Preserve]
            public AuthResponse() {}
        }

        private enum Partner
        {
            None,
            ArborXR
        }
    }
}
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
using System.Text.RegularExpressions;
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

        private static string _authToken;
        private static string _apiSecret;
        private static AuthMechanism _authMechanism;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        
        private static AuthResponse _responseData;
        public static AuthResponse GetAuthResponse() => _responseData;
        
        private static AuthHandoffData _authHandoffData;
        public static AuthHandoffData GetAuthHandoffData() => _authHandoffData;
        
        // Complete authentication response data
        private static List<Abxr.ModuleData> _authResponseModuleData;
    
        private const string DeviceIdKey = "abxrlib_device_id";

        private static bool? _keyboardAuthSuccess;
        
        // Auth handoff for external launcher apps
        private static bool _authHandoffCompleted = false;

        public static bool Authenticated() 
        {
            // Check if we have a valid token and it hasn't expired
            return !string.IsNullOrEmpty(_authToken) && 
                   !string.IsNullOrEmpty(_apiSecret) && 
                   DateTime.UtcNow <= _tokenExpiry;
        }

        public static bool FullyAuthenticated() => Authenticated() && _keyboardAuthSuccess == true;
        
        /// <summary>
        /// Clears authentication state and stops data transmission
        /// </summary>
        private static void ClearAuthenticationState()
        {
            _authToken = null;
            _apiSecret = null;
            _tokenExpiry = DateTime.MinValue;
            _keyboardAuthSuccess = null;
            _sessionId = null;
            _authMechanism = null;
            
            // Clear cached user data
            _responseData = null;
            _authResponseModuleData = null;
            
            // Reset failed authentication attempts counter
            _failedAuthAttempts = 0;
            
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
            if (!ValidateConfigValues()) return;

            SetSessionData();
            
            // Start the deferred authentication system
            StartCoroutine(DeferredAuthenticationSystem());
            StartCoroutine(PollForReAuth());
        }

        private IEnumerator DeferredAuthenticationSystem()
        {
            // Wait for the end of the frame to allow all other Start() methods to run
            yield return new WaitForEndOfFrame();
            
            // Wait one more frame to ensure all Awake() and Start() methods have completed
            yield return null;
            
            // Check if auto-start authentication is enabled in configuration
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
            // Check for auth handoff first before doing normal authentication
            yield return CheckAuthHandoff();
            if (_authHandoffCompleted)
            {
                yield break; // Auth handoff handled everything, we're done
            }
            
            yield return AuthRequest();
            
            // AuthRequest() now guarantees success due to infinite retry loop
            yield return GetConfiguration();
            if (!string.IsNullOrEmpty(_authMechanism?.prompt))
            {
                Debug.Log("AbxrLib: Additional user authentication required (PIN/credentials)");
                yield return KeyboardAuthenticate();
                // Note: KeyboardAuthenticate calls NotifyAuthCompleted when it succeeds
            }
            else
            {
                Debug.Log("AbxrLib: Authentication fully completed");
                // No additional auth needed - notify completion now
                Abxr.NotifyAuthCompleted(true);
                _keyboardAuthSuccess = true;  // So FullyAuthenticated() returns true
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
            // First check basic configuration validation
            if (!Configuration.Instance.IsValid())
            {
                Debug.LogError("AbxrLib: Configuration validation failed. Cannot authenticate.");
                return false;
            }
            
            // Additional format validation for appID (UUID format)
            const string appIdPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
            if (!Regex.IsMatch(_appId, appIdPattern))
            {
                Debug.LogError("AbxrLib: Invalid Application ID format. Must be a valid UUID. Cannot authenticate.");
                return false;
            }
        
            // Allow empty orgId, but validate format if provided
            if (!string.IsNullOrEmpty(_orgId))
            {
                const string orgIdPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
                if (!Regex.IsMatch(_orgId, orgIdPattern))
                {
                    Debug.LogError("AbxrLib: Invalid Organization ID format. Must be a valid UUID. Cannot authenticate.");
                    return false;
                }
            }

            return true;
        }

        public static IEnumerator KeyboardAuthenticate(string keyboardInput = null)
        {
            Debug.Log($"AbxrLib: KeyboardAuthenticate called with input: {(keyboardInput != null ? "provided" : "null")}");
            _keyboardAuthSuccess = false;
            
            // Check if _authMechanism is null (can happen after failed authentication)
            if (_authMechanism == null)
            {
                Debug.LogError("AbxrLib: Authentication mechanism is null. Cannot proceed with keyboard authentication.");
                // Don't notify failure - keep user locked in until they authenticate properly
                yield break;
            }
            
            Debug.Log($"AbxrLib: Starting keyboard authentication loop, _keyboardAuthSuccess = {_keyboardAuthSuccess}");
            
            // Keep asking for authentication until successful
            while (_keyboardAuthSuccess != true)
            {
                Debug.Log($"AbxrLib: Keyboard auth loop iteration, keyboardInput = {(keyboardInput != null ? "provided" : "null")}");
                
                if (keyboardInput != null)
                {
                    Debug.Log($"AbxrLib: Processing keyboard input: {keyboardInput}");
                    string originalPrompt = _authMechanism.prompt;
                    _authMechanism.prompt = keyboardInput;
                    yield return AuthRequest(retryOnFailure: false);
                    Debug.Log($"AbxrLib: AuthRequest completed, _keyboardAuthSuccess = {_keyboardAuthSuccess}");
                    
                    if (_keyboardAuthSuccess == true)
                    {
                        Debug.Log("AbxrLib: Final authentication successful");
                        KeyboardHandler.Destroy();
                        _failedAuthAttempts = 0;
                        
                        // Notify completion for keyboard authentication success
                        Abxr.NotifyAuthCompleted(true);
                        
                        yield break;
                    }

                    Debug.Log("AbxrLib: Authentication failed, restoring original prompt");
                    _authMechanism.prompt = originalPrompt;
                    keyboardInput = null; // Clear the input so we show the keyboard next time
                }
            
                Debug.Log($"AbxrLib: Showing keyboard again, failed attempts: {_failedAuthAttempts}");
                string prompt = _failedAuthAttempts > 0 ? $"Authentication Failed ({_failedAuthAttempts})\n" : "";
                prompt += _authMechanism.prompt;
                
                // Destroy existing keyboard before recreating to ensure fresh instance
                Debug.Log("AbxrLib: Destroying existing keyboard before recreating");
                KeyboardHandler.Destroy();
                
                Debug.Log("AbxrLib: Presenting keyboard");
                Abxr.PresentKeyboard(prompt, _authMechanism.type, _authMechanism.domain);
                _failedAuthAttempts++;
                
                Debug.Log("AbxrLib: Keyboard updated, yielding to wait for user input");
                // Wait for user input - this will be called again with the new input
                yield break;
            }
            
            Debug.Log("AbxrLib: Keyboard authentication loop completed");
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

        private static IEnumerator AuthRequest(bool retryOnFailure = true)
        {
            if (string.IsNullOrEmpty(_sessionId)) _sessionId = Guid.NewGuid().ToString();
        
            var data = new AuthPayload
            {
                appId = _appId,
                orgId = _orgId,
                authSecret = _authSecret,
                deviceId = _deviceId,
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
                authMechanism = CreateAuthMechanismDict()
            };
        
            string json = JsonConvert.SerializeObject(data);
            var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token");
            
            if (retryOnFailure)
            {
                yield return AuthRequestWithRetry(fullUri, json);
            }
            else
            {
                yield return AuthRequestSingleAttempt(fullUri, json);
            }
        }
        
        /// <summary>
        /// Performs a single authentication attempt without retry logic
        /// Used for keyboard authentication where we don't want to retry with the same invalid credentials
        /// </summary>
        private static IEnumerator AuthRequestSingleAttempt(Uri fullUri, string json)
        {
            // Create request and handle creation errors
            UnityWebRequest request = null;
            
            // Request creation with error handling (no yield statements)
            try
            {
                request = new UnityWebRequest(fullUri.ToString(), "POST");
                Utils.BuildRequest(request, json);
                
                // Set timeout to prevent hanging requests
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Authentication request creation failed: {ex.Message}");
                yield break;
            }
            
            // Send request (yield outside try-catch)
            yield return request.SendWebRequest();
            
            // Handle response (no yield statements in try-catch)
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    AuthResponse postResponse = null;
                    try
                    {
                        postResponse = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AbxrLib: Failed to deserialize authentication response: {ex.Message}");
                    }
                    
                    // Validate response data
                    if (postResponse == null || string.IsNullOrEmpty(postResponse.Token))
                    {
                        Debug.LogError("AbxrLib: Invalid authentication response: missing token");
                    }
                    else
                    {
                        _authToken = postResponse.Token;
                        _apiSecret = postResponse.Secret;
                        
                        // Decode JWT with error handling
                        Dictionary<string, object> decodedJwt = Utils.DecodeJwt(_authToken);
                        if (decodedJwt == null)
                        {
                            Debug.LogError("AbxrLib: Failed to decode JWT token - authentication cannot proceed");
                        }
                        else if (!decodedJwt.ContainsKey("exp"))
                        {
                            Debug.LogError("AbxrLib: Invalid JWT token: missing expiration field");
                        }
                        else
                        {
                            try
                            {
                                // Safely cast exp to long, handling different numeric types
                                long expValue;
                                if (decodedJwt["exp"] is long longVal)
                                    expValue = longVal;
                                else if (decodedJwt["exp"] is int intVal)
                                    expValue = intVal;
                                else if (decodedJwt["exp"] is double doubleVal)
                                    expValue = (long)doubleVal;
                                else
                                    throw new InvalidCastException($"JWT exp field is not a valid number type: {decodedJwt["exp"]?.GetType()}");
                                
                                _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds(expValue).UtcDateTime;
                                
                                _responseData = postResponse;
                                _authResponseModuleData = Utils.ConvertToModuleDataList(postResponse.Modules ?? new List<Dictionary<string, object>>());

                                if (_keyboardAuthSuccess == false) _keyboardAuthSuccess = true;
                                
                                // Log success for keyboard authentication
                                Debug.Log("AbxrLib: Keyboard authentication successful");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"AbxrLib: Invalid JWT token expiration: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    // Handle different types of network errors
                    string errorMessage = HandleNetworkError(request, 0);
                    Debug.LogError($"AbxrLib: Keyboard authentication failed: {errorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Keyboard authentication response handling failed: {ex.Message}");
            }
            finally
            {
                // Always dispose of request
                request?.Dispose();
            }
        }
        
        /// <summary>
        /// Performs authentication request with continuous retry until success
        /// </summary>
        private static IEnumerator AuthRequestWithRetry(Uri fullUri, string json)
        {
            int retryCount = 0;
            bool success = false;
            
            while (!success)
            {
                // Store the current authentication state to check if it changed
                string tokenBefore = _authToken;
                
                // Attempt authentication
                yield return AuthRequestSingleAttempt(fullUri, json);
                
                // Check if authentication was successful
                if (!string.IsNullOrEmpty(_authToken) && _authToken != tokenBefore)
                {
                    success = true;
                    Debug.Log("AbxrLib: API connection established");
                }
                else
                {
                    retryCount++;
                    Debug.LogError($"AbxrLib: Authentication attempt {retryCount} failed, retrying...");
                    
                    // Wait before retrying to avoid overwhelming the server
                    yield return new WaitForSeconds(1.0f);
                }
            }
        }
        
        /// <summary>
        /// Handles network errors and determines appropriate error messages
        /// </summary>
        private static string HandleNetworkError(UnityWebRequest request, int retryCount)
        {
            string errorMessage = "";
            
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
            UnityWebRequest request = null;
            
            // Request creation with error handling (no yield statements)
            bool requestCreated = false;
            try
            {
                request = UnityWebRequest.Get(fullUri.ToString());
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                SetAuthHeaders(request);
                requestCreated = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration request creation failed: {ex.Message}");
                Debug.LogWarning("AbxrLib: GetConfiguration request creation failed, using default configuration");
            }
            
            if (!requestCreated)
            {
                yield break;
            }
            
            // Send request (yield outside try-catch)
            yield return request.SendWebRequest();
            
            // Handle response (no yield statements in try-catch)
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
                            Debug.Log("AbxrLib: Configuration loaded successfully");
                        }
                    }
                }
                else
                {
                    string errorMessage = HandleNetworkError(request, 0);
                    Debug.LogWarning($"AbxrLib: GetConfiguration failed: {errorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: GetConfiguration response handling failed: {ex.Message}");
            }
            finally
            {
                // Always dispose of request
                request?.Dispose();
            }
        }
    
        public static void SetAuthHeaders(UnityWebRequest request, string json = "")
        {
            // Check if we have valid authentication tokens
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_apiSecret))
            {
                Debug.LogError("AbxrLib: Cannot set auth headers - authentication tokens are missing");
                return;
            }
            
            request.SetRequestHeader("Authorization", "Bearer " + _authToken);
        
            string unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            request.SetRequestHeader("x-abxrlib-timestamp", unixTimeSeconds);
        
            string hashString = _authToken + _apiSecret + unixTimeSeconds;
            if (!string.IsNullOrEmpty(json))
            {
                uint crc = Utils.ComputeCRC(json);
                hashString += crc;
            }
        
            request.SetRequestHeader("x-abxrlib-hash", Utils.ComputeSha256Hash(hashString));
        }

        private static Dictionary<string, string> CreateAuthMechanismDict()
        {
            var dict = new Dictionary<string, string>();
            if (_authMechanism == null) return dict;
        
            if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
            if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
            if (!string.IsNullOrEmpty(_authMechanism.domain)) dict["domain"] = _authMechanism.domain;
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
            try
            {
                Debug.Log("AbxrLib: Processing authentication handoff from external launcher");
                
                // Parse the handoff JSON
                AuthHandoffData handoffData = null;
                bool parseSuccess = false;
                try 
                {
                    handoffData = JsonConvert.DeserializeObject<AuthHandoffData>(handoffJson);
                    parseSuccess = true;
                }
                catch (Exception ex)
                {
                    // Log error with consistent format and include JSON parsing context
                    Debug.LogError($"AbxrLib: Failed to parse handoff JSON: {ex.Message}\n" +
                                  $"Exception Type: {ex.GetType().Name}\n" +
                                  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
                }
                
                if (!parseSuccess)
                {
                    yield break;
                }
                
                // Validate that we have the required fields
                if (handoffData == null || string.IsNullOrEmpty(handoffData.Token) || string.IsNullOrEmpty(handoffData.Secret))
                {
                    Debug.LogWarning($"AbxrLib: Authentication handoff missing required fields (handoffData null: {handoffData == null}, Token empty: {string.IsNullOrEmpty(handoffData?.Token)}, Secret empty: {string.IsNullOrEmpty(handoffData?.Secret)}), falling back to normal auth");
                    yield break;
                }
                
                // Set authentication state from handoff data
                _authToken = handoffData.Token;
                _apiSecret = handoffData.Secret;
                
                // Cache user data from handoff
                _responseData = new AuthResponse
                {
                    Token = _authToken,
                    Secret = _apiSecret,
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
                
                Debug.Log($"AbxrLib: Authentication handoff successful. Modules: {_authResponseModuleData?.Count ?? 0}");
                
                Abxr.NotifyAuthCompleted(true);
                _keyboardAuthSuccess = true;
                
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
        }
    
        [Preserve]
        private class AuthMechanism
        {
            public string type;
            public string prompt;
            public string domain;
        
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
            public Dictionary<string, object> UserData;
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

        /// <summary>
        /// Data structure for authentication handoff JSON from external launcher apps
        /// Now matches AuthResponse format with case-insensitive property mapping
        /// </summary>
        [Preserve]
        public class AuthHandoffData
        {
            [JsonProperty("Token")]
            public string Token;
            
            [JsonProperty("Secret")]
            public string Secret;
            
            [JsonProperty("UserData")]
            public Dictionary<string, object> UserData;
            
            [JsonProperty("UserId")]
            public object UserId;
            
            [JsonProperty("AppId")]
            public string AppId;
            
            [JsonProperty("PackageName")]
            public string PackageName;
            
            [JsonProperty("Modules")]
            public List<Dictionary<string, object>> Modules;

            [Preserve]
            public AuthHandoffData() { }
        }

    }
}
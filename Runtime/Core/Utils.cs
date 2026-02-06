/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Utility Functions
 * 
 * This file contains utility functions and helper methods used throughout AbxrLib:
 * - Cryptographic functions (SHA256, CRC32)
 * - JWT token decoding and validation
 * - Network utilities (IP address detection, URL building)
 * - Command line and Android intent parameter parsing
 * - Data conversion and formatting utilities
 * 
 * These utilities provide low-level functionality that supports the main AbxrLib API.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Data;
using AbxrLib.Runtime.Storage;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Utility functions and helper methods for AbxrLib
    /// 
    /// This class provides low-level utility functions used throughout the AbxrLib system,
    /// including cryptographic operations, network utilities, data parsing, and formatting.
    /// All methods are static and designed for internal use within the AbxrLib framework.
    /// </summary>
    public static class Utils
    {
        public static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
	
        static readonly uint[] _table = GenerateTable();

        public static uint ComputeCRC(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            uint crc = 0xFFFFFFFF;

            foreach (byte b in bytes)
            {
                byte index = (byte)((crc ^ b) & 0xFF);
                crc = (crc >> 8) ^ _table[index];
            }

            return ~crc;
        }

        private static uint[] GenerateTable()
        {
            uint[] retTable = new uint[256];
            const uint polynomial = 0xEDB88320;

            for (uint i = 0; i < retTable.Length; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? (polynomial ^ (c >> 1)) : (c >> 1);
                retTable[i] = c;
            }

            return retTable;
        }
    
        public static Dictionary<string, object> DecodeJwt(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    Debug.LogError("AbxrLib: JWT token is null or empty");
                    return null;
                }

                string[] parts = token.Split('.');
                if (parts.Length != 3)
                {
                    Debug.LogError($"AbxrLib: Invalid JWT token format - expected 3 parts, got {parts.Length}");
                    return null;
                }

                string payload = parts[1];
                if (string.IsNullOrEmpty(payload))
                {
                    Debug.LogError("AbxrLib: JWT payload is empty");
                    return null;
                }

                payload = PadBase64(payload); // Ensure padding is correct
                byte[] bytes = Convert.FromBase64String(Base64UrlDecode(payload));
                string json = Encoding.UTF8.GetString(bytes);

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError("AbxrLib: JWT payload decoded to empty JSON");
                    return null;
                }

                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (result == null)
                {
                    Debug.LogError("AbxrLib: Failed to deserialize JWT payload JSON");
                    return null;
                }

                return result;
            }
            catch (FormatException ex)
            {
                Debug.LogError($"AbxrLib: JWT token format error: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"AbxrLib: JWT JSON parsing error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: JWT decoding error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts authentication data from an App Token (JWT).
        /// Production tokens include: appId, buildType
        /// Development tokens include: appId, orgId, authSecret, buildType
        /// </summary>
        /// <param name="appToken">The App Token JWT string</param>
        /// <returns>Dictionary containing extracted fields (appId, orgId, authSecret, buildType), or null if token is invalid</returns>
        public static Dictionary<string, string> ExtractAppTokenData(string appToken)
        {
            if (string.IsNullOrEmpty(appToken))
            {
                return null;
            }

            Dictionary<string, object> jwtPayload = DecodeJwt(appToken);
            if (jwtPayload == null)
            {
                return null;
            }

            var result = new Dictionary<string, string>();

            // Extract appId (required in both production and development tokens)
            if (jwtPayload.ContainsKey("appId") && jwtPayload["appId"] != null)
            {
                result["appId"] = jwtPayload["appId"].ToString();
            }

            // Extract buildType (required in both production and development tokens)
            if (jwtPayload.ContainsKey("buildType") && jwtPayload["buildType"] != null)
            {
                result["buildType"] = jwtPayload["buildType"].ToString();
            }

            // Extract orgId (only in development tokens)
            if (jwtPayload.ContainsKey("orgId") && jwtPayload["orgId"] != null)
            {
                result["orgId"] = jwtPayload["orgId"].ToString();
            }

            // Extract authSecret (only in development tokens)
            if (jwtPayload.ContainsKey("authSecret") && jwtPayload["authSecret"] != null)
            {
                result["authSecret"] = jwtPayload["authSecret"].ToString();
            }

            return result;
        }

        /// <summary>
        /// Result structure containing all authentication configuration data extracted from config or tokens.
        /// Internal API - not intended for external use.
        /// </summary>
        internal struct AuthConfigData
        {
            public string appId;
            public string appToken;
            public string orgId;
            public string authSecret;
            public string buildType;
            public bool isValid;
            public string errorMessage;
        }

        /// <summary>
        /// Extracts all authentication configuration data from Configuration, handling both App Tokens and traditional config.
        /// Internal API - not intended for external use.
        /// </summary>
        /// <param name="config">The Configuration instance</param>
        /// <returns>AuthConfigData containing extracted values and validation status</returns>
        internal static AuthConfigData ExtractConfigData(Configuration config)
        {
            var result = new AuthConfigData { isValid = false };

            if (config == null)
            {
                result.errorMessage = "Configuration instance is null";
                return result;
            }

            // Get buildType from config (will be overridden from token if using app tokens)
            result.buildType = !string.IsNullOrEmpty(config.buildType) ? config.buildType : "production";

            // Check if using App Tokens
            if (config.useAppTokens)
            {
                // Get the appropriate token based on buildType
                string tokenToUse = config.buildType == "production" 
                    ? config.appTokenProduction 
                    : config.appTokenDevelopment;
                
                if (string.IsNullOrEmpty(tokenToUse))
                {
                    result.errorMessage = $"App Token for {config.buildType} build is not set.";
                    return result;
                }
                
                // Store the token
                result.appToken = tokenToUse;
                
                // Extract data from the JWT token
                var tokenData = ExtractAppTokenData(tokenToUse);
                if (tokenData == null)
                {
                    result.errorMessage = "Failed to decode App Token.";
                    return result;
                }
                
                // Extract appId (required)
                if (tokenData.ContainsKey("appId"))
                {
                    result.appId = tokenData["appId"];
                }
                else
                {
                    result.errorMessage = "App Token missing appId.";
                    return result;
                }
                
                // Extract buildType from token (overrides config value)
                if (tokenData.ContainsKey("buildType"))
                {
                    result.buildType = tokenData["buildType"];
                }
                
                // Extract orgId and authSecret (only in development tokens)
                result.orgId = tokenData.ContainsKey("orgId") ? tokenData["orgId"] : null;
                result.authSecret = tokenData.ContainsKey("authSecret") ? tokenData["authSecret"] : null;
            }
            else
            {
                // Use traditional appID/orgID/authSecret approach
                result.appId = config.appID;
                result.appToken = null;
               
                // Only include orgID and authSecret if buildType is development
                // In production builds, these should be empty to avoid including credentials
                if (config.buildType == "development")
                {
                    result.orgId = config.orgID;
                    result.authSecret = config.authSecret;
                }
                else
                {
                    // Production build - use empty values
                    result.orgId = null;
                    result.authSecret = null;
                }
            }

            result.isValid = true;
            return result;
        }

        private static string Base64UrlDecode(string input)
        {
            return input.Replace('-', '+').Replace('_', '/');
        }

        private static string PadBase64(string input)
        {
            switch (input.Length % 4)
            {
                case 2: return input + "==";
                case 3: return input + "=";
                default: return input;
            }
        }
    
        public static string GetIPAddress()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // Check for IPv4 addresses
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error with consistent format and include network context
                Debug.LogError($"AbxrLib: Failed to get local IP address: {ex.Message}\n" +
                              $"Exception Type: {ex.GetType().Name}\n" +
                              $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
            }

            return "0.0.0.0";
        }
    
        public static void BuildRequest(UnityWebRequest request, string json)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }
    
        public static string BuildUrlWithParams(string baseUrl, Dictionary<string, string> queryParams)
        {
            var builder = new StringBuilder(baseUrl);
            if (queryParams != null && queryParams.Count > 0)
            {
                builder.Append("?");
                foreach (var queryParam in queryParams)
                {
                    builder.AppendFormat("{0}={1}&",
                        UnityWebRequest.EscapeURL(queryParam.Key),
                        UnityWebRequest.EscapeURL(queryParam.Value));
                }

                // Remove trailing '&'
                builder.Length -= 1;
            }
            return builder.ToString();
        }
    
        public static string GetQueryParam(string key, string url)
        {
            var questionMarkIndex = url.IndexOf('?');
            if (questionMarkIndex < 0) return "";
            var queryString = url.Substring(questionMarkIndex + 1);
            foreach (var keyValuePair in queryString.Split('&'))
            {
                var keyValueArray = keyValuePair.Split('=');
                if (keyValueArray.Length == 2 && Uri.UnescapeDataString(keyValueArray[0]) == key)
                {
                    return Uri.UnescapeDataString(keyValueArray[1]);
                }
            }
            return "";
        }

        /// <summary>
        /// Get command line argument value by key
        /// Searches through Unity's command line arguments for key=value pairs
        /// </summary>
        /// <param name="key">The argument key to search for</param>
        /// <returns>The argument value if found, empty string otherwise</returns>
        public static string GetCommandLineArg(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            
            // Pre-build the search strings to avoid repeated concatenation
            string keyEquals = key + "=";
            string doubleDashKey = "--" + key;
            string singleDashKey = "-" + key;
            int keyLength = key.Length;
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(keyEquals))
                {
                    return args[i].Substring(keyLength + 1);
                }
                // Also check for space-separated arguments (--key value or -key value)
                if ((args[i] == doubleDashKey || args[i] == singleDashKey) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return "";
        }

        /// <summary>
        /// Get Android intent parameter value by key
        /// Uses Unity's Android activity to retrieve intent extras
        /// </summary>
        /// <param name="key">The intent parameter key to search for</param>
        /// <returns>The intent parameter value if found, empty string otherwise</returns>
        public static string GetAndroidIntentParam(string key)
        {
    #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                
                // Check if the intent has the specified extra
                bool hasExtra = intent.Call<bool>("hasExtra", key);
                if (hasExtra)
                {
                    return intent.Call<string>("getStringExtra", key);
                }
            }
            catch (System.Exception ex)
            {
                // Log warning with consistent format and include Android context
                Debug.LogWarning($"AbxrLib: Failed to get Android intent parameter '{key}': {ex.Message}\n" +
                                $"Exception Type: {ex.GetType().Name}\n" +
                                $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
            }
    #endif
            return "";
        }

        /// <summary>
        /// Get Android manifest metadata value by key
        /// Reads metadata from AndroidManifest.xml using ApplicationInfo
        /// </summary>
        /// <param name="key">The metadata key to search for</param>
        /// <returns>The metadata value if found, empty string otherwise</returns>
        public static string GetAndroidManifestMetadata(string key)
        {
    #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
                var packageName = activity.Call<string>("getPackageName");
                
                // Get ApplicationInfo with GET_META_DATA flag (0x00000080)
                using var appInfo = packageManager.Call<AndroidJavaObject>("getApplicationInfo", packageName, 0x00000080);
                using var metaData = appInfo.Get<AndroidJavaObject>("metaData");
                
                if (metaData != null)
                {
                    // Try to get as string first (most common case)
                    string stringValue = metaData.Call<string>("getString", key);
                    if (!string.IsNullOrEmpty(stringValue))
                    {
                        return stringValue;
                    }
                    
                    // Fallback: get as object and convert to string
                    object value = metaData.Call<object>("get", key);
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Log warning with consistent format and include Android context
                Debug.LogWarning($"AbxrLib: Failed to get Android manifest metadata '{key}': {ex.Message}\n" +
                                $"Exception Type: {ex.GetType().Name}\n" +
                                $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
            }
    #endif
            return "";
        }
    
        public static long GetUnityTime() => (long)(Time.time * 1000f) + Initialize.StartTimeMs;

        public static void SendAllData()
        {
            // Send all pending data from all batcher types
            if (CoroutineRunner.Instance != null)
            {
                CoroutineRunner.Instance.StartCoroutine(DataBatcher.Send());
                CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Send());
            }
            else
            {
                Debug.LogWarning("AbxrLib: Cannot send data - CoroutineRunner.Instance is null");
            }
        }
        
        /// <summary>
        /// Convert raw module dictionaries to typed ModuleData objects
        /// Internal helper method for processing authentication response modules
        /// Modules are automatically sorted by their order field
        /// </summary>
        /// <param name="rawModules">Raw module data from authentication response</param>
        /// <returns>List of typed ModuleData objects sorted by order</returns>
        public static List<Abxr.ModuleData> ConvertToModuleDataList(List<Dictionary<string, object>> rawModules)
        {
            var moduleDataList = new List<Abxr.ModuleData>();
            if (rawModules == null) return moduleDataList;

            try
            {
                var tempList = new List<Abxr.ModuleData>();
			
                foreach (var rawModule in rawModules)
                {
                    var moduleId = rawModule.ContainsKey("id") ? rawModule["id"]?.ToString() : "";
                    var moduleName = rawModule.ContainsKey("name") ? rawModule["name"]?.ToString() : "";
                    var moduleTarget = rawModule.ContainsKey("target") ? rawModule["target"]?.ToString() : "";
                    var moduleOrder = 0;
				
                    if (rawModule.ContainsKey("order") && rawModule["order"] != null)
                    {
                        int.TryParse(rawModule["order"].ToString(), out moduleOrder);
                    }

                    tempList.Add(new Abxr.ModuleData(moduleId, moduleName, moduleTarget, moduleOrder));
                }

                // Sort modules by order field
                moduleDataList = tempList.OrderBy(m => m.order).ToList();
            }
            catch (Exception ex)
            {
                // Log error with consistent format and include data conversion context
                Debug.LogError($"AbxrLib: Failed to convert module data: {ex.Message}\n" +
                              $"Exception Type: {ex.GetType().Name}\n" +
                              $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
            }

            return moduleDataList;
        }
        
        /// <summary>
        /// Validates that a string is a valid HTTP/HTTPS URL
        /// </summary>
        /// <param name="url">The URL string to validate</param>
        /// <returns>True if the URL is valid, false otherwise</returns>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
                
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "http" || uri.Scheme == "https";
            }
            catch
            {
                return false;
            }
        }
    }
}
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
	/// Extension methods for string serialization and deserialization
	/// </summary>
	public static class StringExtensions
	{
		/// <summary>
		/// Extension method for escaping strings in StringList and AbxrDictStrings for serializing into comma-separated single string.
		/// </summary>
		/// <param name="str">The string to escape</param>
		/// <returns>Escaped string</returns>
		public static string EscapeForSerialization(this string str)
		{
			StringBuilder	sOut = new StringBuilder();

			foreach (char c in str)
			{
				switch (c)
				{
					case '\\':
						sOut.Append("\\\\");
						break;
					case '\"':
						sOut.Append("\\\"");
						break;
					case ',':
						sOut.Append(',');
						break;
					case '=':
						sOut.Append('=');
						break;
					default:
						sOut.Append(c);
						break;
				}
			}
			return sOut.ToString();
		}
		/// <summary>
		/// Extension method for de-escaping strings into StringList from serialized comma-separated single string.
		/// </summary>
		/// <param name="str">The string to deserialize</param>
		/// <param name="addStringAction">Action to add each deserialized string</param>
		public static void UnescapeAndDeserialize(this string str, Action<string> addStringAction)
		{
			StringBuilder	sOut = new StringBuilder();
			bool			bEscapedState = false;

			foreach (char c in str)
			{
				switch (c)
				{
					case '\\':
						if (bEscapedState)
						{
							sOut.Append(c);
							bEscapedState = false;
						}
						else
						{
							bEscapedState = true;
						}
						break;
					case ',':
						if (bEscapedState)
						{
							sOut.Append(c);
						}
						else
						{
							addStringAction(sOut.ToString());
							sOut.Clear();
						}
						bEscapedState = false;
						break;
					default:
						sOut.Append(c);
						bEscapedState = false;
						break;
				}
			}
			if (sOut.Length > 0)
			{
				addStringAction(sOut.ToString());
			}
		}
		/// <summary>
		/// Extension method for de-escaping strings into AbxrDictStrings from serialized comma-separated single string.
		/// </summary>
		/// <param name="str">The string to deserialize</param>
		/// <param name="addKeyValueAction">Action to add each deserialized key-value pair</param>
		public static void UnescapeAndDeserialize(this string str, Action<string, string> addKeyValueAction)
		{
			StringBuilder	sFirst = new StringBuilder();
			StringBuilder	sSecond = new StringBuilder();
			bool			bEscapedState = false;
			bool			bOnFirst = true;

			foreach (char c in str)
			{
				switch (c)
				{
				case '\\':
					if (bEscapedState)
					{
						if (bOnFirst) sFirst.Append(c); else sSecond.Append(c);
						bEscapedState = false;
					}
					else
					{
						bEscapedState = true;
					}
					break;
				case '=':
					if (bEscapedState)
					{
						if (bOnFirst) sFirst.Append(c); else sSecond.Append(c);
					}
					else
					{
						bOnFirst = false;
					}
					bEscapedState = false;
					break;
				case ',':
					if (bEscapedState)
					{
						if (bOnFirst) sFirst.Append(c); else sSecond.Append(c);
					}
					else
					{
						addKeyValueAction(sFirst.ToString(), sSecond.ToString());
						sFirst.Clear();
						sSecond.Clear();
						bOnFirst = true;
					}
					bEscapedState = false;
					break;
				default:
					if (bOnFirst) sFirst.Append(c); else sSecond.Append(c);
					bEscapedState = false;
					break;
				}
			}
			if (sFirst.Length > 0 && sSecond.Length > 0 && !bOnFirst)
			{
				addKeyValueAction(sFirst.ToString(), sSecond.ToString());
			}
		}
		/// <summary>
		/// Extension method for converting Dictionary<string, string> to useful-for-debug-output form.
		/// </summary>
		/// <param name="dict">dict to convert.</param>
		/// <returns></returns>
		public static string Stringify(this Dictionary<string, string> dict)
		{
			StringBuilder	sb = new StringBuilder();
			bool			bFirst = true;

			sb.Append("{");
			foreach (KeyValuePair<string, string> kvp in dict)
			{
				if (!bFirst)
				{
					sb.Append(",");
				}
				else
				{
					bFirst = false;
				}
				sb.Append($"{kvp.Key}={kvp.Value}");
			}
			sb.Append("}");
			// ---
			return sb.ToString();
		}
		public static List<string> ToList(this string[] asz)
		{
			List<string>	lszRet = new List<string>();

			foreach (string s in asz)
			{
				lszRet.Add(s);
			}
			return lszRet;
		}
	}
    /// <summary>
    /// Utility functions and helper methods for AbxrLib
    /// 
    /// This class provides low-level utility functions used throughout the AbxrLib system,
    /// including cryptographic operations, network utilities, data parsing, and formatting.
    /// All methods are static and designed for internal use within the AbxrLib framework.
    /// </summary>
    public static class Utils
    {
		// --- Dewonkification helpers for enabling C# layer to present/accept nice clean C# types.
		public static string StringListToString(List<string> lsz)
		{
			string result = "";

			if (lsz != null)
			{
				foreach (string sz in lsz)
				{
					if (!string.IsNullOrEmpty(result))
					{
						result += ",";
					}
					result += sz.EscapeForSerialization();
				}
			}
			return result;
		}
		public static List<string> StringToStringList(string szList)
		{
			List<string> lszRet = new List<string>();

			if (szList != null)
			{
				szList.UnescapeAndDeserialize((s) => { lszRet.Add(s); });
			}
			// ---
			return lszRet;
		}
		public static string StringArrayToString(string[] asz)
		{
			string result = "";

			if (asz != null)
			{
				foreach (string sz in asz)
				{
					if (!string.IsNullOrEmpty(result))
					{
						result += ",";
					}
					result += sz.EscapeForSerialization();
				}
			}
			return result;
		}
		public static string[] StringToStringArray(string szList)
		{
			return StringToStringList(szList).ToArray();
		}
		public static string DictToString(Dictionary<string, string> dict)
		{
			string result = "";

			if (dict != null)
			{
				foreach (KeyValuePair<string, string> kvp in dict)
				{
					if (!string.IsNullOrEmpty(result))
					{
						result += ",";
					}
					result += $"{kvp.Key.EscapeForSerialization()}={kvp.Value.EscapeForSerialization()}";
				}
			}
			return result;
		}
		public static Dictionary<string, string> StringToDict(string szDict)
		{
			Dictionary<string, string> dictRet = new Dictionary<string, string>();

			if (szDict != null)
			{
				szDict.UnescapeAndDeserialize((k, v) => dictRet.Add(k, v));
			}
			// ---
			return dictRet;
		}
		public static long filetime_to_timet(long ft)
		{
			return ft / 10000000L - 11644473600L;
		}
		public static long timet_to_filetime(long tt)
		{
			return (tt + 11644473600L) * 10000000L;
		}
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Events;
using AbxrLib.Runtime.Logs;
using AbxrLib.Runtime.Storage;
using AbxrLib.Runtime.Telemetry;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Core
{
    public static class Utils
    {
        public static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
	
        static readonly uint[] Table = GenerateTable();

        public static uint ComputeCRC(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            uint crc = 0xFFFFFFFF;

            foreach (byte b in bytes)
            {
                byte index = (byte)((crc ^ b) & 0xFF);
                crc = (crc >> 8) ^ Table[index];
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
            string[] parts = token.Split('.');
            string payload = parts[1];
            payload = PadBase64(payload); // Ensure padding is correct
            byte[] bytes = Convert.FromBase64String(Base64UrlDecode(payload));
            string json = Encoding.UTF8.GetString(bytes);

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
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
                Debug.LogError("AbxrLib - Failed to get local IP address: " + ex.Message);
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
                foreach (var param in queryParams)
                {
                    builder.AppendFormat("{0}={1}&",
                        UnityWebRequest.EscapeURL(param.Key),
                        UnityWebRequest.EscapeURL(param.Value));
                }

                // Remove trailing '&'
                builder.Length -= 1;
            }
            return builder.ToString();
        }
    
    public static string GetQueryParam(string key, string url)
    {
        var question = url.IndexOf('?');
        if (question < 0) return "";
        var query = url.Substring(question + 1);
        foreach (var pair in query.Split('&'))
        {
            var kv = pair.Split('=');
            if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]) == key)
            {
                return Uri.UnescapeDataString(kv[1]);
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
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(key + "="))
            {
                return args[i].Substring(key.Length + 1);
            }
            // Also check for space-separated arguments (--key value or -key value)
            if ((args[i] == "--" + key || args[i] == "-" + key) && i + 1 < args.Length)
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
            Debug.LogWarning($"AbxrLib - Failed to get Android intent parameter '{key}': {ex.Message}");
        }
#endif
        return "";
    }
    
        public static long GetUnityTime() => (long)(Time.time * 1000f) + Initialize.StartTimeMs;

        public static void SendAllData()
        {
            // Use safe coroutine starts with redundancy for all batcher types
            CoroutineRunner.SafeStartCoroutine(EventBatcher.Send());
            CoroutineRunner.SafeStartCoroutine(TelemetryBatcher.Send());
            CoroutineRunner.SafeStartCoroutine(LogBatcher.Send());
            CoroutineRunner.SafeStartCoroutine(StorageBatcher.Send());
            
            // Schedule backup actions in case main thread is interrupted
            CoroutineRunner.ScheduleBackupAction(() => {
                CoroutineRunner.SafeStartCoroutine(EventBatcher.Send());
                CoroutineRunner.SafeStartCoroutine(TelemetryBatcher.Send());
                CoroutineRunner.SafeStartCoroutine(LogBatcher.Send());
                CoroutineRunner.SafeStartCoroutine(StorageBatcher.Send());
            });
        }
    }
}
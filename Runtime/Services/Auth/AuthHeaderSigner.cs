using System;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.Auth
{
    internal static class AuthHeaderSigner
    {
        internal static bool TrySetAuthHeaders(UnityWebRequest request, AuthResponse responseData, string json = null)
        {
            if (responseData == null || string.IsNullOrEmpty(responseData.Token) || string.IsNullOrEmpty(responseData.Secret))
            {
                Logcat.Error("Cannot set auth headers - authentication tokens are missing");
                return false;
            }

            request.SetRequestHeader("Authorization", "Bearer " + responseData.Token);

            string unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            request.SetRequestHeader("x-abxrlib-timestamp", unixTimeSeconds);

            string hashString = responseData.Token + responseData.Secret + unixTimeSeconds;
            if (!string.IsNullOrEmpty(json))
            {
                uint crc = Utils.ComputeCRC(json);
                hashString += crc;
            }

            request.SetRequestHeader("x-abxrlib-hash", Utils.ComputeSha256Hash(hashString));
            return true;
        }
    }
}

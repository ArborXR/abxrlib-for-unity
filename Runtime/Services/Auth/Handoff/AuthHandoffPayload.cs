using System;
using System.Collections.Generic;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Normalizes incoming auth_handoff values and builds outgoing auth_handoff payloads.
    /// </summary>
    internal static class AuthHandoffPayload
    {
        internal const string StageLabel = "handoff";

        /// <summary>
        /// Returns JSON suitable for auth response parsing. Raw JSON is returned as-is; base64-encoded JSON is decoded;
        /// malformed non-base64 strings are returned so normal auth-response validation can log the parse failure.
        /// </summary>
        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("{")) return trimmed;

            try
            {
                byte[] bytes = Convert.FromBase64String(trimmed);
                string decoded = Encoding.UTF8.GetString(bytes);
                if (string.IsNullOrEmpty(decoded)) return null;

                decoded = decoded.Trim();
                Logcat.Info("Normalized handoff payload from base64");
                return decoded.StartsWith("{") ? decoded : null;
            }
            catch
            {
                // Not valid base64; treat as raw so ApplyAuthResponse/AuthResponseParser validates it.
                return trimmed;
            }
        }

        /// <summary>
        /// Builds the JSON payload passed via auth_handoff. Includes current session credentials plus
        /// re-auth fields so the receiving app can adopt the session without running device auth.
        /// </summary>
        internal static string Build(AuthResponse response, AuthPayload payload, DateTime tokenExpiryUtc, string returnToPackage = null)
        {
            if (response == null) return null;

            long expiryMs = tokenExpiryUtc > DateTime.UtcNow
                ? ((DateTimeOffset)tokenExpiryUtc).ToUnixTimeMilliseconds()
                : ((DateTimeOffset)DateTime.UtcNow.AddHours(24)).ToUnixTimeMilliseconds();

            var handoff = new Dictionary<string, object>
            {
                ["Token"] = response.Token ?? "",
                ["Secret"] = response.Secret ?? "",
                ["AppId"] = response.AppId ?? payload?.appId ?? "",
                ["UserId"] = response.UserId?.ToString() ?? "",
                ["UserData"] = response.UserData != null
                    ? new Dictionary<string, string>(response.UserData)
                    : new Dictionary<string, string>(),
                ["DeviceId"] = payload?.deviceId ?? "",
                ["AppToken"] = payload?.appToken ?? "",
                ["OrgToken"] = payload?.orgToken ?? "",
                ["OrgId"] = payload?.orgId ?? "",
                ["TokenExpirationMs"] = expiryMs,
            };

            if (returnToPackage != null) handoff["ReturnToPackage"] = returnToPackage;

            return JsonConvert.SerializeObject(handoff);
        }
    }
}

using System.Collections.Generic;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Helpers for applying XRDM/MDM SSO access-token JWT claims to auth user data.
    /// </summary>
    internal static class SsoUserDataMerger
    {
        private static readonly string[] IdentityClaimKeys =
        {
            "sub",
            "oid",
            "preferred_username",
            "upn",
            "unique_name"
        };

        /// <summary>JWT claim names that may carry an email. First valid match wins when copying into <c>email</c>.</summary>
        private static readonly string[] EmailClaimKeys =
        {
            "email",                // OIDC / Google ID token
            "email_address",
            "user_email",
            "mail",                 // AD / Graph
            "preferred_username",   // Azure AD (often UPN / email)
            "upn",
            "unique_name"
        };

        /// <summary>Returns true when the JWT access token has at least one identity-oriented claim suitable for skipping a configured auth prompt.</summary>
        internal static bool AccessTokenHasUsableIdentity(string accessToken)
        {
            var payload = DecodePayload(accessToken);
            return PayloadHasUsableIdentity(payload);
        }

        /// <summary>
        /// Merges decodable JWT access-token claims into <paramref name="userData"/>.
        /// Conflicting claim keys are stored as <c>sso_</c>… with numeric suffixes as needed.
        /// If <c>email</c> is still empty, it is copied from the first plausible email claim.
        /// </summary>
        /// <returns>True if any key was added or updated in <paramref name="userData"/>.</returns>
        internal static bool TryMergeAccessTokenIntoUserData(string accessToken, Dictionary<string, string> userData)
        {
            if (userData == null) return false;

            var payload = DecodePayload(accessToken);
            if (payload == null || payload.Count == 0) return false;

            return MergePayloadIntoUserData(payload, userData);
        }

        internal static bool PayloadHasUsableIdentity(Dictionary<string, object> payload)
        {
            if (payload == null || payload.Count == 0) return false;

            foreach (var claimKey in IdentityClaimKeys)
            {
                if (!payload.TryGetValue(claimKey, out var raw) || raw == null) continue;
                string value = Utils.JwtPayloadValueToString(raw);
                if (!string.IsNullOrWhiteSpace(value)) return true;
            }

            foreach (var claimKey in EmailClaimKeys)
            {
                if (!payload.TryGetValue(claimKey, out var raw) || raw == null) continue;
                string value = Utils.JwtPayloadValueToString(raw);
                if (!string.IsNullOrWhiteSpace(value)) return true;
            }

            return false;
        }

        internal static bool MergePayloadIntoUserData(Dictionary<string, object> payload, Dictionary<string, string> userData)
        {
            if (payload == null || payload.Count == 0 || userData == null) return false;

            bool changed = false;
            foreach (var kvp in payload)
            {
                string value = Utils.JwtPayloadValueToString(kvp.Value);
                if (string.IsNullOrEmpty(value)) continue;

                if (userData.TryAdd(kvp.Key, value))
                {
                    changed = true;
                    continue;
                }

                userData[NextSsoConflictKey(userData, kvp.Key)] = value;
                changed = true;
            }

            if (EnsureEmailFromJwtClaims(userData, payload)) changed = true;

            return changed;
        }

        internal static bool EnsureEmailFromJwtClaims(Dictionary<string, string> userData, Dictionary<string, object> payload)
        {
            if (userData == null || payload == null) return false;
            if (userData.TryGetValue("email", out var existing) && !string.IsNullOrWhiteSpace(existing)) return false;

            foreach (var claimKey in EmailClaimKeys)
            {
                if (!payload.TryGetValue(claimKey, out var raw) || raw == null) continue;
                string value = Utils.JwtPayloadValueToString(raw);
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (!Utils.TryNormalizePlausibleEmail(value, out var normalized)) continue;

                userData["email"] = normalized;
                return true;
            }

            return false;
        }

        private static Dictionary<string, object> DecodePayload(string accessToken) =>
            string.IsNullOrWhiteSpace(accessToken) ? null : Utils.TryDecodeJwtPayload(accessToken);

        private static string NextSsoConflictKey(Dictionary<string, string> userData, string key)
        {
            string candidate = "sso_" + key;
            int suffix = 0;
            while (userData.ContainsKey(candidate))
            {
                suffix++;
                candidate = "sso_" + key + "_" + suffix;
            }

            return candidate;
        }
    }
}

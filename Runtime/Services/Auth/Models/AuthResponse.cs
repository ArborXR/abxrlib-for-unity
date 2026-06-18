using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Types
{
    // ── Auth response received FROM the backend ──────────────────────

    [Serializable]
    public class AuthResponse
    {
        public string Token;
        public string Secret;
        [JsonProperty("userData")]
        public Dictionary<string, string> UserData;
        [JsonProperty("userId")]
        public object UserId;
        public string AppId;
        public string PackageName;
        /// <summary>When set in auth_handoff payload, the app that receives it should call LaunchAppWithAuthHandoff(this value) when assessment completes (return-to-launcher flow). Cleared after use.</summary>
        public string ReturnToPackage;
        public List<ModuleData> Modules;

        /// <summary>Single rule for REST: a successful auth response must include the session token and API secret returned by /v1/auth/token.</summary>
        public static bool IsValidSuccess(AuthResponse r)
        {
            return r != null
                   && !string.IsNullOrEmpty(r.Token)
                   && !string.IsNullOrEmpty(r.Secret);
        }
    }
}

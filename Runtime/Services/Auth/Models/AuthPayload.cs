using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Types
{
    // ── Auth payload sent TO the backend ──────────────────────────────

    [Serializable]
    public class AuthPayload
    {
        /// <summary>
        /// Creates a request-scoped copy of the shared session payload.
        /// The source authMechanism is intentionally not copied; callers must pass
        /// the request-specific auth mechanism to avoid leaking stale user/custom auth.
        /// </summary>
        internal AuthPayload CopyForRequest(Dictionary<string, string> requestAuthMechanism = null)
        {
            return new AuthPayload
            {
                appId = appId,
                orgId = orgId,
                authSecret = authSecret,
                appToken = appToken,
                orgToken = orgToken,
                deviceId = deviceId,
                userId = userId,
                tags = tags != null ? (string[])tags.Clone() : null,
                sessionId = sessionId,
                partner = partner,
                ipAddress = ipAddress,
                deviceModel = deviceModel,
                geolocation = geolocation != null ? new Dictionary<string, string>(geolocation) : null,
                osVersion = osVersion,
                xrdmVersion = xrdmVersion,
                appVersion = appVersion,
                unityVersion = unityVersion,
                abxrLibType = abxrLibType,
                abxrLibVersion = abxrLibVersion,
                buildFingerprint = buildFingerprint,
                authMechanism = requestAuthMechanism != null ? new Dictionary<string, string>(requestAuthMechanism) : null
            };
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string appId; // legacy only; omit when using app tokens
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string orgId; // legacy only
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string authSecret; // legacy only
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string appToken; // omit when using legacy credentials
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string orgToken;
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
        public string buildFingerprint;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> authMechanism;
    }
}

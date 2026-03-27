using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Types
{
    [Serializable]
    public class AuthMechanism
    {
        public string type;
        public string prompt;
        public string domain;
        public string inputSource = "user";
        public bool? allowGuest;
    }

    /// <summary>
    /// A single LMS-provided module from the auth response.
    /// </summary>
    [Serializable]
    public class ModuleData
    {
        public string Id;
        public string Name;
        public string Target;
        public int Order;
    }

    /// <summary>
    /// Runtime auth configuration: auth-related values copied from Configuration and updated by GetArborData, GetQueryData, intent, and Abxr.SetOrgId/SetAuthSecret/SetDeviceId.
    /// Validated via IsValid() before building the auth request; does not touch the Configuration asset.
    /// </summary>
    public class RuntimeAuthConfig
    {
        /// <summary>When set, overrides Configuration.enableAutoStartAuthentication so the asset is not modified. Null = use Configuration.</summary>
        public bool? enableAutoStartAuthentication;
        /// <summary>When set, overrides Configuration.enableReturnTo so the asset is not modified. Null = use Configuration.</summary>
        public bool? enableReturnTo;
        /// <summary>When set, overrides Configuration.enableAutoStartModules. Null = use Configuration.</summary>
        public bool? enableAutoStartModules;
        /// <summary>When set, overrides Configuration.enableAutoAdvanceModules. Null = use Configuration.</summary>
        public bool? enableAutoAdvanceModules;

        public bool useAppTokens;
        public string appToken;
        public string orgToken;
        public string appId;
        public string orgId;
        public string authSecret;
        public string buildType;
        /// <summary>Device id from subsystem (GetDeviceId) or MDM when connected.</summary>
        public string deviceId;
        /// <summary>Partner identifier; "none" when not from MDM, "arborxr" when from ArborMdmClient.</summary>
        public string partner;
        /// <summary>Device tags from MDM when connected; otherwise null/empty.</summary>
        public string[] tags;

        /// <summary>Auth mechanism (type, prompt, domain). When null or empty type, filled from GET config when received; when set (e.g. by tests) before config is fetched, that value is used and not overwritten. Learner launcher is applied only when we just filled from config.</summary>
        public AuthMechanism authMechanism;

        /// <summary>
        /// Validates the current runtime auth values. Returns null if valid, or an error message if invalid.
        /// Call after loading from Configuration and applying GetArborData/GetQueryData/overrides.
        /// </summary>
        public string IsValid()
        {
            return ValidateAuthFields(useAppTokens, buildType, appId, orgId, authSecret, appToken, orgToken);
        }

        /// <summary>
        /// Call when about to send an auth request. Runs IsValid(), then enforces that credentials are complete (orgToken for app tokens, orgId/authSecret for legacy) so we never send without them. Use this after GetArborData/overrides have run; IsValid() alone allows empty org for non-production_custom because Configuration asset validation does not require them.
        /// </summary>
        public string IsValidToSend()
        {
            var err = IsValid();
            if (err != null) return err;
            if (useAppTokens && string.IsNullOrEmpty(orgToken))
                return "Organization identification unavailable.";
            if (!useAppTokens && (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(authSecret)))
                return "Organization identification unavailable.";
            return null;
        }

        /// <summary>
        /// Shared auth-field validation used by both Configuration.IsValid() and RuntimeAuthConfig.IsValid().
        /// Returns null if valid, or a short error message (e.g. "App identification not set."). Configuration prefixes with "Authentication error: " when setting LastValidationErrorMessage.
        /// </summary>
        public static string ValidateAuthFields(bool useAppTokens, string buildType, string appId, string orgId, string authSecret, string appToken, string orgToken)
        {
            if (useAppTokens)
            {
                if (string.IsNullOrEmpty(appToken))
                    return "App identification not set.";
                if (!LooksLikeJwt(appToken))
                    return "App identification not set.";
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgToken))
                        return "Organization identification unavailable.";
                    if (!LooksLikeJwt(orgToken))
                        return "Organization identification unavailable.";
                }
                else if (!string.IsNullOrEmpty(orgToken) && !LooksLikeJwt(orgToken))
                    return "Organization identification unavailable.";
            }
            else
            {
                if (string.IsNullOrEmpty(appId))
                    return "App identification not set.";
                if (!LooksLikeUuid(appId))
                    return "App identification not set.";
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgId))
                        return "Organization identification unavailable.";
                    if (string.IsNullOrEmpty(authSecret) || string.IsNullOrWhiteSpace(authSecret))
                        return "Organization identification unavailable.";
                }
                if (!string.IsNullOrEmpty(orgId) && !LooksLikeUuid(orgId))
                    return "Organization identification unavailable.";
                if (!string.IsNullOrEmpty(authSecret) && string.IsNullOrWhiteSpace(authSecret))
                    return "Organization identification unavailable.";
            }

            return null;
        }

        private static bool LooksLikeUuid(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            const string uuidPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
            return Regex.IsMatch(value, uuidPattern);
        }

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        /// <summary>
        /// Copy auth and device/partner fields from this runtime config into the given payload. Only sets the auth fields appropriate for the current mode (useAppTokens vs legacy); the other mode's fields are cleared.
        /// </summary>
        public void CopyAuthFieldsTo(AuthPayload payload)
        {
            if (payload == null) return;
            payload.buildType = buildType;
            payload.deviceId = deviceId;
            payload.partner = partner ?? "none";
            payload.tags = tags;
            if (useAppTokens)
            {
                payload.appToken = appToken;
                payload.orgToken = orgToken;
                payload.appId = null;
                payload.orgId = null;
                payload.authSecret = null;
            }
            else
            {
                payload.appId = appId;
                payload.orgId = orgId;
                payload.authSecret = authSecret;
                payload.appToken = null;
                payload.orgToken = null;
            }
        }
    }

    // ── Auth payload sent TO the backend ──────────────────────────────

    [Serializable]
    public class AuthPayload
    {
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
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string buildType; // Production (Custom APK) sends "production" to API
        public string deviceId;
        public string userId;
        public string SSOAccessToken;
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

        /// <summary>Single rule for both REST and service transports: response is a valid auth success (full success or second-stage required). Full success = Token or Modules present. Second-stage required = AppId present but no token/modules (proceed to config and PIN prompt). Error payloads (e.g. {"message":"..."}) have no AppId/Token/Modules.</summary>
        public static bool IsValidSuccess(AuthResponse r)
        {
            if (r == null) return false;
            bool hasTokenOrModules = !string.IsNullOrEmpty(r.Token) || (r.Modules != null && r.Modules.Count > 0);
            bool secondStageRequired = !hasTokenOrModules && !string.IsNullOrEmpty(r.AppId);
            return hasTokenOrModules || secondStageRequired;
        }
    }

    // ── Config payload received from /v1/storage/config ──────────────

    /// <summary>
    /// GET /v1/storage/config response shape. The API may include any keys; Newtonsoft ignores JSON properties that do not map to members.
    /// <see cref="AbxrLib.Runtime.Core.Configuration.ApplyConfigPayload"/> merges only a subset; credentials, token mode, build type, module timing/sequence, auth UI, AbxrTarget defaults, learner launcher, ArborInsightsClient/ArborMdmClient (build-time from Unity asset), and unit-test fields are deserialized but not applied.
    /// </summary>
    [Serializable]
    public class ConfigPayload
    {
        public AuthMechanism authMechanism;

        // Network / batching (values are often string-encoded in merged portal config)
        public string restUrl;
        public string sendRetriesOnFailure;
        public string sendRetryInterval;
        public string sendNextBatchWait;
        public string stragglerTimeout;
        public string requestTimeoutSeconds;
        public string maxCallFrequencySeconds;
        public string dataEntriesPerSendAttempt;
        public string storageEntriesPerSendAttempt;
        public string pruneSentItemsOlderThan;
        public string maximumCachedItems;
        public string retainLocalAfterSent;

        public string positionCapturePeriod;
        public string frameRateCapturePeriod;
        public string telemetryCapturePeriod;

        // Identity
        public string launcherAppID;

        // UI / tracking
        public bool? headsetTracking;

        // Auth flow / modules
        public bool? enableReturnTo;
        public bool? enablePinPadGuestAccess;

        // Platform / feature flags
        public bool? enableAutomaticTelemetry;
        public bool? enableSceneEvents;
        public string maxDictionarySize;

        // ── Also accepted in GET /v1/storage/config JSON; deserialized but NOT merged into Configuration (developer-controlled in Unity) ──
        public bool? enableArborInsightsClient;
        public bool? enableArborMdmClient;
        public bool? useAppTokens;
        public string buildType;
        public string authenticationStartDelay;
        public bool? enableAutoStartModules;
        public bool? enableAutoAdvanceModules;
        public string appID;
        public string orgID;
        public string authSecret;
        public string appToken;
        public string orgToken;
        public bool? authUIFollowCamera;
        public bool? enableDirectTouchInteraction;
        public string authUIDistanceFromCamera;
        public string defaultMaxDistanceLimit;
        public bool? defaultAutoCreateTriggerCollider;
        public bool? enableAutoStartAuthentication;
        public bool? enableLearnerLauncherMode;
        public bool? unitTestConfigEnabled;
        public string unitTestAuthPin;
        public string unitTestAuthBadPin;
        public string unitTestAuthText;
        public string unitTestAuthEmail;
        public string unitTestAuthEmailDomain;
        public string unitTestDeviceId;
        public string unitTestFingerprint;
    }

    // ── Data payloads for /v1/collect/data ────────────────────────────
    
    [Serializable]
    public class EventPayload
    {
        public string timestamp;
        public long preciseTimestamp;
        public string name;
        public Dictionary<string, string> meta;
    }

    [Serializable]
    public class TelemetryPayload
    {
        public string timestamp;
        public long preciseTimestamp;
        public string name;
        public Dictionary<string, string> meta;
    }

    [Serializable]
    public class LogPayload
    {
        public string timestamp;
        public long preciseTimestamp;
        public string logLevel;
        public string text;
        public Dictionary<string, string> meta;
    }

    [Serializable]
    public class DataPayloadWrapper
    {
        public List<EventPayload> @event;
        public List<TelemetryPayload> telemetry;
        public List<LogPayload> basicLog;
    }
    
    [Serializable]
    public class AIPromptPayload
    {
        public string prompt;
        public string llmProvider;
        public List<string> pastMessages;
    }
    
    [Serializable]
    public class StoragePayload
    {
        public string timestamp;
        public string keepPolicy;
        public string name;
        public List<Dictionary<string, string>> data;
        public string scope;
    }
    
    [Serializable]
    public class StoragePayloadWrapper
    {
        public List<StoragePayload> data;
    }
}
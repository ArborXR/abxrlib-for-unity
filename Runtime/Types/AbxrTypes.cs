using System;
using System.Collections.Generic;

namespace AbxrLib.Runtime.Types
{
    [Serializable]
    public class AuthMechanism
    {
        public string type;
        public string prompt;
        public string domain;
        public string inputSource = "user";
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

    // ── Auth payload sent TO the backend ──────────────────────────────

    [Serializable]
    public class AuthPayload
    {
        public string appToken;  // optional - either appToken or appId will be set
        public string appId;  // optional - either appToken or appId will be set
        public string orgId;
        public string authSecret;
        public string deviceId;
        public string userId;
        public string SSOAccessToken;  // optional - SSO access token when SSO is active
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
        public string buildFingerprint;  // optional - set on Android devices
        public Dictionary<string, string> authMechanism;
    }

    // ── Auth response received FROM the backend ──────────────────────

    [Serializable]
    public class AuthResponse
    {
        public string Token;
        public string Secret;
        public Dictionary<string, string> UserData;
        public object UserId;
        public string AppId;
        public string PackageName;
        public List<ModuleData> Modules;
    }

    // ── Config payload received from /v1/storage/config ──────────────

    [Serializable]
    public class ConfigPayload
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
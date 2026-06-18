using System;
using System.Collections.Generic;

namespace AbxrLib.Runtime.Types
{
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

    // ── Config payload received from /v1/storage/config ──────────────

    /// <summary>
    /// GET /v1/storage/config response shape. The API may include any keys; Newtonsoft ignores JSON properties that do not map to members.
    /// <see cref="AbxrLib.Runtime.Core.Configuration.ApplyConfigPayload"/> merges only a subset; credentials, token mode, build type, module timing/sequence, auth UI, AbxrTarget defaults, learner launcher, and ArborMdmClient settings remain build-time values from the Unity asset.
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
        public bool? recordIpAddress;

        // Platform / feature flags
        public bool? enableAutomaticTelemetry;
        public bool? enableSceneEvents;
        public string maxDictionarySize;

        // Developer-controlled values that may appear in GET /v1/storage/config JSON but are not merged into Configuration
        public bool? enableArborMdmClient;
        public bool? useAppTokens;
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

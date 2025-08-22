using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public class Configuration : ScriptableObject
    {
        private static Configuration _instance;
        private const string CONFIG_NAME = "AbxrLib";
    
        public static Configuration Instance
        {
            get
            {
                if (_instance) return _instance;
            
                _instance = Resources.Load<Configuration>(CONFIG_NAME);
                if (!_instance)
                {
                    _instance = CreateInstance<Configuration>();
                }
            
                return _instance;
            }
        }
    
        [Tooltip("Required")] public string appID;
        [Tooltip("Optional")] public string orgID;
        [Tooltip("Optional")] public string authSecret;
    
        public bool headsetTracking = true;
        public float positionTrackingPeriodSeconds = 1f;
    
        public string restUrl = "https://lib-backend.xrdm.app/";

        public float frameRateTrackingPeriodSeconds = 0.5f;
        public float telemetryTrackingPeriodSeconds = 10f;
        public int sendRetriesOnFailure = 3;
        public int sendRetryIntervalSeconds = 3;
        public int sendNextBatchWaitSeconds = 30;
        public int stragglerTimeoutSeconds = 15;
        public int eventsPerSendAttempt = 16;
        public int logsPerSendAttempt = 16;
        public int telemetryEntriesPerSendAttempt = 16;
        public int storageEntriesPerSendAttempt = 16;
        public int pruneSentItemsOlderThanHours = 12;
        public int maximumCachedItems = 1024;
        public bool retainLocalAfterSent;
        public bool disableAutomaticTelemetry;
        public bool disableSceneEvents;
    }
}
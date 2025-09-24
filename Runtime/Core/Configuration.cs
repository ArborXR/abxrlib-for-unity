/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Configuration Management
 * 
 * This file contains the Configuration ScriptableObject that manages all AbxrLib settings:
 * - Application and organization identifiers
 * - Network configuration (REST URLs, retry settings)
 * - Telemetry and tracking settings
 * - Data batching and caching parameters
 * - UI prefab references for authentication
 * 
 * Configuration is loaded from Resources/AbxrLib.asset and can be modified
 * through the Unity Inspector or programmatically at runtime.
 */

using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Configuration ScriptableObject for AbxrLib settings and parameters
    /// 
    /// This class manages all configurable aspects of AbxrLib, including network settings,
    /// telemetry parameters, data batching configuration, and UI component references.
    /// Configuration is automatically loaded from Resources/AbxrLib.asset at runtime.
    /// </summary>
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

        [Header("Authentication Control")]
        [Tooltip("When enabled, authentication will NOT start automatically on app launch. You must manually call Abxr.StartAuthentication()")]
        public bool disableAutoStartAuthentication = false;
        
        [Tooltip("Delay in seconds before starting authentication (only applies when auto-start is enabled)")]
        public float authenticationStartDelay = 0f;

        [Header("Authentication Prefabs")]
        public GameObject KeyboardPrefab;
        public GameObject PinPrefab;
        public GameObject PanelPrefab;
    }
}
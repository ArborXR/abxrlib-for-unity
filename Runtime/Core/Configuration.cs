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
        
        /// <summary>
        /// Validates that the configuration has the required fields set properly.
        /// appID is required and must not be empty if set.
        /// orgID and authSecret are optional but must not be empty if set.
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool IsValid()
        {
            // appID is required and must not be empty if set
            if (string.IsNullOrEmpty(appID))
            {
                Debug.LogError("AbxrLib: Configuration validation failed - appID is required but not set");
                return false;
            }
            
            // orgID is optional but must not be empty if set
            if (!string.IsNullOrEmpty(orgID) && string.IsNullOrWhiteSpace(orgID))
            {
                Debug.LogError("AbxrLib: Configuration validation failed - orgID cannot be empty if set");
                return false;
            }
            
            // authSecret is optional but must not be empty if set
            if (!string.IsNullOrEmpty(authSecret) && string.IsNullOrWhiteSpace(authSecret))
            {
                Debug.LogError("AbxrLib: Configuration validation failed - authSecret cannot be empty if set");
                return false;
            }
            
            return true;
        }
    
        public bool headsetTracking = true;
        public float positionTrackingPeriodSeconds = 1f;
    
        public string restUrl = "https://lib-backend.xrdm.app/";

        public float frameRateTrackingPeriodSeconds = 0.5f;
        public float telemetryTrackingPeriodSeconds = 10f;
        public int sendRetriesOnFailure = 3;
        public int sendRetryIntervalSeconds = 3;
        public int sendNextBatchWaitSeconds = 30;
        public int requestTimeoutSeconds = 30;
        public int stragglerTimeoutSeconds = 15;
        public float maxCallFrequencySeconds = 1f;
        public int dataEntriesPerSendAttempt = 32;
        public int storageEntriesPerSendAttempt = 16;
        public int pruneSentItemsOlderThanHours = 12;
        public int maximumCachedItems = 1024;
        [HideInInspector] public int maxDictionarySize = 50;
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

        [Header("UI Behavior Control")]
        [Tooltip("When enabled, UI panels will follow the camera. When disabled, panels will remain in fixed positions.")]
        public bool authUIFollowCamera = true;
        
        [Tooltip("When enabled, direct touch interaction will be used for UI elements instead of ray casting.")]
        public bool enableDirectTouchInteraction = true;
    }
}
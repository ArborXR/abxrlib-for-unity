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
    
        [Header("Application Identity")]
        [Tooltip("Required")] public string appID;
        [Tooltip("Optional")] public string orgID;
        [Tooltip("Optional")] public string authSecret;
        
        /// <summary>
        /// Validates that the configuration has the required fields set properly.
        /// Validates appID, orgID, authSecret, restUrl format, and numeric ranges for timeouts.
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
            
            // Validate restUrl format - must be a valid HTTP/HTTPS URL
            if (string.IsNullOrEmpty(restUrl))
            {
                Debug.LogError("AbxrLib: Configuration validation failed - restUrl is required but not set");
                return false;
            }
            
            if (!IsValidUrl(restUrl))
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - restUrl '{restUrl}' is not a valid HTTP/HTTPS URL");
                return false;
            }
            
            // Validate numeric ranges for timeouts and intervals
            if (sendRetriesOnFailure < 0 || sendRetriesOnFailure > 10)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - sendRetriesOnFailure must be between 0 and 10, got {sendRetriesOnFailure}");
                return false;
            }
            
            if (sendRetryIntervalSeconds < 1 || sendRetryIntervalSeconds > 300)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - sendRetryIntervalSeconds must be between 1 and 300 seconds, got {sendRetryIntervalSeconds}");
                return false;
            }
            
            if (sendNextBatchWaitSeconds < 1 || sendNextBatchWaitSeconds > 3600)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - sendNextBatchWaitSeconds must be between 1 and 3600 seconds, got {sendNextBatchWaitSeconds}");
                return false;
            }
            
            if (requestTimeoutSeconds < 5 || requestTimeoutSeconds > 300)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - requestTimeoutSeconds must be between 5 and 300 seconds, got {requestTimeoutSeconds}");
                return false;
            }
            
            if (stragglerTimeoutSeconds < 0 || stragglerTimeoutSeconds > 3600)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - stragglerTimeoutSeconds must be between 0 and 3600 seconds, got {stragglerTimeoutSeconds}");
                return false;
            }
            
            if (maxCallFrequencySeconds < 0.1f || maxCallFrequencySeconds > 60f)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - maxCallFrequencySeconds must be between 0.1 and 60 seconds, got {maxCallFrequencySeconds}");
                return false;
            }
            
            if (dataEntriesPerSendAttempt < 1 || dataEntriesPerSendAttempt > 1000)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - dataEntriesPerSendAttempt must be between 1 and 1000, got {dataEntriesPerSendAttempt}");
                return false;
            }
            
            if (storageEntriesPerSendAttempt < 1 || storageEntriesPerSendAttempt > 1000)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - storageEntriesPerSendAttempt must be between 1 and 1000, got {storageEntriesPerSendAttempt}");
                return false;
            }
            
            if (pruneSentItemsOlderThanHours < 0 || pruneSentItemsOlderThanHours > 8760) // Max 1 year
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - pruneSentItemsOlderThanHours must be between 0 and 8760 hours (1 year), got {pruneSentItemsOlderThanHours}");
                return false;
            }
            
            if (maximumCachedItems < 10 || maximumCachedItems > 10000)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - maximumCachedItems must be between 10 and 10000, got {maximumCachedItems}");
                return false;
            }
            
            if (maxDictionarySize < 5 || maxDictionarySize > 1000)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - maxDictionarySize must be between 5 and 1000, got {maxDictionarySize}");
                return false;
            }
            
            // Validate tracking periods
            if (positionTrackingPeriodSeconds < 0.1f || positionTrackingPeriodSeconds > 60f)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - positionTrackingPeriodSeconds must be between 0.1 and 60 seconds, got {positionTrackingPeriodSeconds}");
                return false;
            }
            
            if (frameRateTrackingPeriodSeconds < 0.1f || frameRateTrackingPeriodSeconds > 60f)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - frameRateTrackingPeriodSeconds must be between 0.1 and 60 seconds, got {frameRateTrackingPeriodSeconds}");
                return false;
            }
            
            if (telemetryTrackingPeriodSeconds < 1f || telemetryTrackingPeriodSeconds > 300f)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - telemetryTrackingPeriodSeconds must be between 1 and 300 seconds, got {telemetryTrackingPeriodSeconds}");
                return false;
            }
            
            if (authenticationStartDelay < 0f || authenticationStartDelay > 60f)
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - authenticationStartDelay must be between 0 and 60 seconds, got {authenticationStartDelay}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates that a string is a valid HTTP/HTTPS URL
        /// </summary>
        /// <param name="url">The URL string to validate</param>
        /// <returns>True if the URL is valid, false otherwise</returns>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
                
            try
            {
                var uri = new System.Uri(url);
                return uri.Scheme == "http" || uri.Scheme == "https";
            }
            catch
            {
                return false;
            }
        }

        [Header("Service Provider")]
        public string restUrl = "https://lib-backend.xrdm.app/";

        [Header("UI Behavior Control")]
        [Tooltip("When enabled, UI panels will follow the camera. When disabled, panels will remain in fixed positions.")]
        public bool authUIFollowCamera = true;
        
        [Tooltip("When enabled, direct touch interaction will be used for UI elements instead of ray casting.")]
        public bool enableDirectTouchInteraction = true;

        [Header("Player Tracking")]
        public bool headsetTracking = true;
        public float positionTrackingPeriodSeconds = 1f;

        [Header("Authentication Control")]
        [Tooltip("When enabled, authentication will NOT start automatically on app launch. You must manually call Abxr.StartAuthentication()")]
        public bool disableAutoStartAuthentication = false;
        
        [Tooltip("Delay in seconds before starting authentication (only applies when auto-start is enabled)")]
        public float authenticationStartDelay = 0f;

        [Header("Authentication Prefabs")]
        public GameObject KeyboardPrefab;
        public GameObject PinPrefab;

        [Header("Data Sending Rules")]
        public float telemetryTrackingPeriodSeconds = 10f;
        public float frameRateTrackingPeriodSeconds = 0.5f;
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
        public bool retainLocalAfterSent;
        public bool disableAutomaticTelemetry;
        public bool disableSceneEvents;

        [HideInInspector] 
        public int maxDictionarySize = 50;
    }
}
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

using System;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Types;
using UnityEngine;
using UnityEngine.Serialization;

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
        private static bool _validatedOnce;
        private const string CONFIG_NAME = "AbxrLib";

        /// <summary>
        /// When true, validation logs warnings instead of errors for missing app/org token (e.g. during Editor build when config may be incomplete).
        /// Set by Editor build scripts only.
        /// </summary>
        internal static bool PreferValidationWarnings { get; set; }

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

                _instance.MigrateIfNeeded();

                // Run validation once on first load so numeric ranges are clamped early; return value is ignored here.
                if (!_validatedOnce)
                {
                    _instance.IsValid();
                    _validatedOnce = true;
                }
                return _instance;
            }
        }
    
        [Header("Build Type")]
        [Tooltip("Production: OrgID and AuthSecret will NOT be included in builds (secure for 3rd party distribution). Development: OrgID and AuthSecret will be included in builds (for custom APKs only). Production (Custom APK): like Production for the API but requires and includes Organization Token for single-customer builds.")]
        public string buildType = "production";
        
        [Header("Application Identity")]
        [Tooltip("Required")] public string appID;
        [Tooltip("Optional - Only used when Build Type is Development")] public string orgID;
        [Tooltip("Optional - Only used when Build Type is Development")] public string authSecret;
        [HideInInspector]
        [Tooltip("Optional")] public string launcherAppID;
        
        [Header("App Tokens")]
        [Tooltip("When enabled, use App Tokens instead of appID/orgID/authSecret. Defaults to false so existing config assets without this field use legacy auth.")] public bool useAppTokens = false;
        [Tooltip("App Token (JWT) from ArborXR Portal – identifies app and publisher. Required when Use App Tokens is on.")] public string appToken;
        [Tooltip("Optional. Organization Token (JWT) from ArborXR Portal. Leave empty for shared production builds; set for single-org or dev builds.")] public string orgToken;
        
        /// <summary>
        /// Validates configuration: required auth fields and restUrl can cause return false; numeric settings are clamped to valid range (log and continue).
        /// When useAppTokens: requires appToken (non-empty, JWT shape). orgToken may be empty (can come from runtime); if set, validates JWT shape.
        /// When not useAppTokens: requires appID (non-empty, UUID). orgID and authSecret may be empty; if set, validates format.
        /// restUrl must be set and valid (return false otherwise). Numeric values below min or above max are clamped to range and a warning is logged; validation still returns true.
        /// Called automatically on first access to Configuration.Instance so ranges are applied early in the loading process.
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool IsValid()
        {
            const string uuidPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";

            if (useAppTokens)
            {
                // App-token mode: appToken is required and must be valid format. orgToken can come from runtime — only validate format when set.
                if (string.IsNullOrEmpty(appToken))
                {
                    if (PreferValidationWarnings)
                        Debug.LogWarning("AbxrLib: Configuration validation - appToken is required when using app tokens. Set App Token in AbxrLib configuration or authentication will fail at runtime.");
                    else
                        Debug.LogError("AbxrLib: Configuration validation failed - appToken is required when using app tokens.");
                    return false;
                }
                if (!LooksLikeJwt(appToken))
                {
                    Debug.LogError("AbxrLib: Configuration validation failed - appToken does not look like a JWT (expected three dot-separated segments).");
                    return false;
                }
                // Production (Custom APK) requires orgToken to be set and JWT-shaped
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgToken))
                    {
                        if (PreferValidationWarnings)
                            Debug.LogWarning("AbxrLib: Configuration validation - Organization Token is required when Build Type is Production (Custom APK). Set the customer's org token in AbxrLib configuration.");
                        else
                            Debug.LogError("AbxrLib: Configuration validation failed - Organization Token is required when Build Type is Production (Custom APK).");
                        return false;
                    }
                    if (!LooksLikeJwt(orgToken))
                    {
                        Debug.LogError("AbxrLib: Configuration validation failed - orgToken does not look like a JWT (expected three dot-separated segments).");
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(orgToken) && !LooksLikeJwt(orgToken))
                {
                    Debug.LogError("AbxrLib: Configuration validation failed - orgToken does not look like a JWT (expected three dot-separated segments).");
                    return false;
                }
            }
            else
            {
                // Legacy mode: appID is required and must be valid format. orgID and authSecret can come from runtime — only validate format when set. Production (Custom APK) requires both.
                if (string.IsNullOrEmpty(appID))
                {
                    Debug.LogError("AbxrLib: Configuration validation failed - Application ID is required when not using app tokens.");
                    return false;
                }
                if (!Regex.IsMatch(appID, uuidPattern))
                {
                    Debug.LogError("AbxrLib: Invalid Application ID format. Must be a valid UUID. Cannot authenticate.");
                    return false;
                }
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgID))
                    {
                        if (PreferValidationWarnings)
                            Debug.LogWarning("AbxrLib: Configuration validation - Organization ID is required when Build Type is Production (Custom APK) with legacy auth. Set the customer's org ID in AbxrLib configuration.");
                        else
                            Debug.LogError("AbxrLib: Configuration validation failed - Organization ID is required when Build Type is Production (Custom APK).");
                        return false;
                    }
                    if (string.IsNullOrEmpty(authSecret) || string.IsNullOrWhiteSpace(authSecret))
                    {
                        if (PreferValidationWarnings)
                            Debug.LogWarning("AbxrLib: Configuration validation - Authorization Secret is required when Build Type is Production (Custom APK) with legacy auth. Set it in AbxrLib configuration.");
                        else
                            Debug.LogError("AbxrLib: Configuration validation failed - Authorization Secret is required when Build Type is Production (Custom APK).");
                        return false;
                    }
                }
                if (!string.IsNullOrEmpty(orgID) && !Regex.IsMatch(orgID, uuidPattern))
                {
                    Debug.LogError("AbxrLib: Invalid Organization ID format. Must be a valid UUID. Cannot authenticate.");
                    return false;
                }
                if (!string.IsNullOrEmpty(authSecret) && string.IsNullOrWhiteSpace(authSecret))
                {
                    Debug.LogError("AbxrLib: Configuration validation failed - authSecret cannot be empty if set");
                    return false;
                }
            }

            // Validate restUrl and numeric ranges for both modes
            if (string.IsNullOrEmpty(restUrl))
            {
                Debug.LogError("AbxrLib: Configuration validation failed - restUrl is required but not set");
                return false;
            }
            
            if (!Utils.IsValidUrl(restUrl))
            {
                Debug.LogError($"AbxrLib: Configuration validation failed - restUrl '{restUrl}' is not a valid HTTP/HTTPS URL");
                return false;
            }
            
            // Clamp numeric ranges: empty/invalid (e.g. 0 from cleared Inspector) becomes default; out-of-range becomes min/max
            ClampInt(nameof(sendRetriesOnFailure), ref sendRetriesOnFailure, 0, 10, 3);
            ClampInt(nameof(sendRetryIntervalSeconds), ref sendRetryIntervalSeconds, 1, 300, 3);
            ClampInt(nameof(sendNextBatchWaitSeconds), ref sendNextBatchWaitSeconds, 1, 3600, 30);
            ClampInt(nameof(requestTimeoutSeconds), ref requestTimeoutSeconds, 5, 300, 30);
            ClampInt(nameof(stragglerTimeoutSeconds), ref stragglerTimeoutSeconds, 0, 3600, 15);
            ClampFloat(nameof(maxCallFrequencySeconds), ref maxCallFrequencySeconds, 0.1f, 60f, 1f);
            ClampInt(nameof(dataEntriesPerSendAttempt), ref dataEntriesPerSendAttempt, 1, 1000, 32);
            ClampInt(nameof(storageEntriesPerSendAttempt), ref storageEntriesPerSendAttempt, 1, 1000, 16);
            ClampInt(nameof(pruneSentItemsOlderThanHours), ref pruneSentItemsOlderThanHours, 0, 8760, 12);
            ClampInt(nameof(maximumCachedItems), ref maximumCachedItems, 10, 10000, 1024);
            ClampInt(nameof(maxDictionarySize), ref maxDictionarySize, 5, 1000, 50);
            ClampFloat(nameof(positionTrackingPeriodSeconds), ref positionTrackingPeriodSeconds, 0.1f, 60f, 1f);
            ClampFloat(nameof(frameRateTrackingPeriodSeconds), ref frameRateTrackingPeriodSeconds, 0.1f, 60f, 0.5f);
            ClampFloat(nameof(telemetryTrackingPeriodSeconds), ref telemetryTrackingPeriodSeconds, 1f, 300f, 10f);
            ClampFloat(nameof(authenticationStartDelay), ref authenticationStartDelay, 0f, 60f, 0f);
            ClampFloat(nameof(defaultMaxDistanceLimit), ref defaultMaxDistanceLimit, 0f, 10000f, 50f);

            return true;
        }

        private static void ClampInt(string fieldName, ref int value, int min, int max, int defaultValue)
        {
            if (value < min)
            {
                int applied = Mathf.Clamp(defaultValue, min, max);
                Debug.LogWarning($"AbxrLib: Configuration {fieldName} was {value} (empty or below min), set to default {applied}.");
                value = applied;
            }
            else if (value > max)
            {
                Debug.LogWarning($"AbxrLib: Configuration {fieldName} was {value}, clamped to maximum {max}.");
                value = max;
            }
        }

        private static void ClampFloat(string fieldName, ref float value, float min, float max, float defaultValue)
        {
            if (value < min)
            {
                float applied = Mathf.Clamp(defaultValue, min, max);
                Debug.LogWarning($"AbxrLib: Configuration {fieldName} was {value} (empty or below min), set to default {applied}.");
                value = applied;
            }
            else if (value > max)
            {
                Debug.LogWarning($"AbxrLib: Configuration {fieldName} was {value}, clamped to maximum {max}.");
                value = max;
            }
        }

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        public void ApplyConfigPayload(ConfigPayload payload)
        {
            if (!string.IsNullOrEmpty(payload.restUrl)) restUrl = payload.restUrl;
            if (!string.IsNullOrEmpty(payload.sendRetriesOnFailure)) sendRetriesOnFailure = Convert.ToInt32(payload.sendRetriesOnFailure);
            if (!string.IsNullOrEmpty(payload.sendRetryInterval)) sendRetryIntervalSeconds = Convert.ToInt32(payload.sendRetryInterval);
            if (!string.IsNullOrEmpty(payload.sendNextBatchWait)) sendNextBatchWaitSeconds = Convert.ToInt32(payload.sendNextBatchWait);
            if (!string.IsNullOrEmpty(payload.stragglerTimeout)) stragglerTimeoutSeconds = Convert.ToInt32(payload.stragglerTimeout);
            if (!string.IsNullOrEmpty(payload.dataEntriesPerSendAttempt)) dataEntriesPerSendAttempt = Convert.ToInt32(payload.dataEntriesPerSendAttempt);
            if (!string.IsNullOrEmpty(payload.storageEntriesPerSendAttempt)) storageEntriesPerSendAttempt = Convert.ToInt32(payload.storageEntriesPerSendAttempt);
            if (!string.IsNullOrEmpty(payload.pruneSentItemsOlderThan)) pruneSentItemsOlderThanHours = Convert.ToInt32(payload.pruneSentItemsOlderThan);
            if (!string.IsNullOrEmpty(payload.maximumCachedItems)) maximumCachedItems = Convert.ToInt32(payload.maximumCachedItems);
            if (!string.IsNullOrEmpty(payload.retainLocalAfterSent)) retainLocalAfterSent = Convert.ToBoolean(payload.retainLocalAfterSent);
            // Performance / tracking periods (backend may send as numeric strings, e.g. "1", "0.5", "10")
            if (!string.IsNullOrEmpty(payload.positionCapturePeriod) && float.TryParse(payload.positionCapturePeriod, out float positionPeriod))
                positionTrackingPeriodSeconds = Mathf.Clamp(positionPeriod, 0.1f, 60f);
            if (!string.IsNullOrEmpty(payload.frameRateCapturePeriod) && float.TryParse(payload.frameRateCapturePeriod, out float frameRatePeriod))
                frameRateTrackingPeriodSeconds = Mathf.Clamp(frameRatePeriod, 0.1f, 60f);
            if (!string.IsNullOrEmpty(payload.telemetryCapturePeriod) && float.TryParse(payload.telemetryCapturePeriod, out float telemetryPeriod))
                telemetryTrackingPeriodSeconds = Mathf.Clamp(telemetryPeriod, 1f, 300f);
        }

        [Header("Service Provider")]
        public string restUrl = "https://lib-backend.xrdm.app/";

        [Header("UI Behavior Control")]
        [Tooltip("When enabled, UI panels will follow the camera. When disabled, panels will remain in fixed positions.")]
        public bool authUIFollowCamera = true;
        
        [Tooltip("When enabled, direct touch interaction will be used for UI elements instead of ray casting.")]
        public bool enableDirectTouchInteraction = true;
        
        [Tooltip("How far in front of the camera the UI panel should float (in meters).")]
        public float authUIDistanceFromCamera = 1.0f;

        [Header("Player Tracking")]
        public bool headsetTracking = true;
        public float positionTrackingPeriodSeconds = 1f;

        [Header("Target Gaze Tracking")]
        [Tooltip("Global default maximum distance for AbxrTarget occlusion checks (meters). 0 = unlimited. Individual AbxrTarget components can override.")]
        public float defaultMaxDistanceLimit = 50f;
        [Tooltip("Global default for auto-creating trigger colliders on AbxrTarget. Individual AbxrTarget components can override.")]
        public bool defaultAutoCreateTriggerCollider = true;

        [Header("Authentication Control")]
        [Tooltip("When enabled, authentication will start automatically on app launch. When disabled, you must manually call Abxr.StartAuthentication()")]
        [FormerlySerializedAs("disableAutoStartAuthentication")]
        public bool enableAutoStartAuthentication = true;

        [Tooltip("Delay in seconds before starting authentication (only applies when auto-start is enabled)")]
        public float authenticationStartDelay = 0f;
        
        [Tooltip("When enabled, the first module will start automatically on successful authentication")]
        public bool enableAutoStartModules = true;
        
        [Tooltip("When enabled, the next module in the sequence will automatically start after completion of a module")]
        public bool enableAutoAdvanceModules = true;
        
        [Tooltip("When enabled, the app will return to the launcher after an assessment is complete. When disabled, the app will stay open after an assessment is complete. Specifically used with Learner Launcher.")]
        public bool returnToLauncherAfterAssessmentComplete = true;

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

        [Tooltip("When enabled, the app will use the ArborInsightService device APK for auth and data on Android when installed. When disabled, only REST/cloud is used.")]
        public bool enableArborInsightServiceClient = true;

        [FormerlySerializedAs("disableAutomaticTelemetry")]
        public bool enableAutomaticTelemetry = true;

        [FormerlySerializedAs("disableSceneEvents")]
        public bool enableSceneEvents = true;

        [SerializeField, HideInInspector]
        private int _configSerializedVersion = 0;

        [HideInInspector]
        public int maxDictionarySize = 50;

        private void MigrateIfNeeded()
        {
            if (_configSerializedVersion >= 1) return;

            enableAutoStartAuthentication = !enableAutoStartAuthentication;
            enableAutomaticTelemetry = !enableAutomaticTelemetry;
            enableSceneEvents = !enableSceneEvents;
            _configSerializedVersion = 1;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}

/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * Serialized AbxrLib settings (Resources/AbxrLib.asset). This file keeps the original script GUID so
 * existing projects' assets deserialize as <see cref="AppConfig"/>. Editor and custom inspectors
 * edit this type only. At runtime, values are copied into <see cref="Configuration"/> (see RuntimeConfiguration.cs).
 */

using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// ScriptableObject authored in the Unity Editor (Inspector / config UI). Runtime code uses <see cref="Configuration.Instance"/> instead.
    /// </summary>
    public class AppConfig : ScriptableObject
    {
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
        [Tooltip("When enabled, authenticate with App Token and Org Token (JWT) instead of App ID, Org ID, and Auth Secret. Defaults to true; disable only for legacy integrations.")] public bool useAppTokens = true;
        [Tooltip("App Token (JWT) from ArborXR Portal – identifies app and publisher. Required when Use App Tokens is on.")] public string appToken;
        [Tooltip("Optional. Organization Token (JWT) from ArborXR Portal. Leave empty for shared production builds; set for single-org or dev builds.")] public string orgToken;

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
        public bool enableAutoStartAuthentication = true;

        [Tooltip("Delay in seconds before starting authentication (only applies when auto-start is enabled)")]
        public float authenticationStartDelay = 0f;

        [Tooltip("When enabled, the first module will start automatically on successful authentication")]
        public bool enableAutoStartModules = true;

        [Tooltip("When enabled, the next module in the sequence will automatically start after completion of a module")]
        public bool enableAutoAdvanceModules = true;

        [Tooltip("Allow returnTo Launcher. When enabled, the app will either exit after EventAssessmentComplete() or support returning the session back to the app that launched it with Auth Handoff.")]
        public bool enableReturnTo = true;

        [Tooltip("When enabled, the PIN pad shows Guest Access (skip user identification). When disabled, KeyboardManager.skipButton is hidden at runtime. Custom PIN prefabs should assign skipButton like the default.")]
        public bool enablePinPadGuestAccess = true;

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

        [Tooltip("When enabled, the app will use the ArborInsightsClient device APK for auth and data on Android when installed. When disabled, only REST/cloud is used. Set in the Unity asset only; not overridden by GET /v1/storage/config.")]
        public bool enableArborInsightsClient = true;

        [Tooltip("When enabled on Android, ArborMdmClient is created and used (GetOrgId, GetFingerprint, deviceId, etc.). When disabled, ArborMdmClient is not created; auth uses Configuration or Abxr.SetOrgId/SetAuthSecret only. Set in the Unity asset only; not overridden by GET /v1/storage/config.")]
        public bool enableArborMdmClient = true;

        [Tooltip("When enabled (Auth Handoff Launcher flow), the auth mechanism from config is overridden: if type is not already assessmentPin, it is set to assessmentPin with prompt \"Enter your 6-digit PIN\". Use when a launcher collects PIN before StartAuthentication and submits via OnInputSubmitted. Default false.")]
        public bool enableLearnerLauncherMode = false;

        [Tooltip("When enabled, automatic telemetry collection runs. Defaults to true for new configs.")]
        public bool enableAutomaticTelemetry = true;

        [Tooltip("When enabled, scene change events are recorded. Defaults to true for new configs.")]
        public bool enableSceneEvents = true;

        [HideInInspector]
        public int maxDictionarySize = 50;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [HideInInspector] public bool unitTestConfigEnabled = false;
        [HideInInspector] public string unitTestAuthPin = "";
        [HideInInspector] public string unitTestAuthBadPin = "";
        [HideInInspector] public string unitTestAuthText = "";
        [HideInInspector] public string unitTestAuthEmail = "";
        [HideInInspector] public string unitTestAuthEmailDomain = "";
        [HideInInspector] public string unitTestDeviceId = "";
        [HideInInspector] public string unitTestFingerprint = "";
#endif

        /// <summary>Edit-mode and unit tests: validates this asset in memory (Inspector / EditMode tests).</summary>
        public bool IsValid() => Configuration.ValidateAppConfig(this);

        /// <summary>Clamps numeric fields on this serialized asset (Editor authoring / EditMode tests).</summary>
        public void ClampNumericSettings()
        {
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
        }

        private static void ClampInt(string fieldName, ref int value, int min, int max, int defaultValue)
        {
            if (value < min) value = Mathf.Clamp(defaultValue, min, max);
            else if (value > max) value = max;
        }

        private static void ClampFloat(string fieldName, ref float value, float min, float max, float defaultValue)
        {
            if (value < min) value = Mathf.Clamp(defaultValue, min, max);
            else if (value > max) value = max;
        }
    }
}

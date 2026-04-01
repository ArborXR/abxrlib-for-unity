/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * Runtime configuration POCO: <see cref="Instance"/> is populated from Resources/AbxrLib.asset (<see cref="AppConfig"/>).
 * GET /v1/storage/config merges into this instance only; the serialized asset is not modified at runtime.
 * The ScriptableObject type <see cref="AppConfig"/> lives in Configuration.cs (same GUID as legacy Configuration assets).
 */

using System;
using System.Globalization;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Runtime AbxrLib settings. <see cref="Instance"/> is populated from Resources/AbxrLib.asset (<see cref="AppConfig"/>).
    /// </summary>
    public sealed class Configuration
    {
        private static Configuration _instance;
        private static AppConfig _authoringAsset;
        private static bool _validatedOnce;
        private const string CONFIG_NAME = "AbxrLib";

        internal static bool PreferValidationWarnings { get; set; }

        private static string _lastValidationErrorMessage;
        internal static string LastValidationErrorMessage => _lastValidationErrorMessage;

        public static Configuration Instance
        {
            get
            {
                EnsureLoaded();
                return _instance;
            }
        }

        private static void EnsureLoaded()
        {
            if (_instance != null) return;

            var asset = Resources.Load<AppConfig>(CONFIG_NAME);
            if (!asset)
                asset = ScriptableObject.CreateInstance<AppConfig>();

            _authoringAsset = asset;
            _instance = CopyFromAppConfig(asset);

            if (!_validatedOnce)
            {
                _instance.IsValid();
                _validatedOnce = true;
            }
        }

        /// <summary>Used by <see cref="AppConfig.IsValid"/> (EditMode tests / Inspector).</summary>
        internal static bool ValidateAppConfig(AppConfig a)
        {
            if (a == null) return false;
            if (!TryValidateAuthAndUrl(a.useAppTokens, a.buildType, a.appID, a.orgID, a.authSecret, a.appToken, a.orgToken, a.restUrl, out _))
                return false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                a.ClampNumericSettings();
#endif
            return true;
        }

        private static bool TryValidateAuthAndUrl(bool useAppTokens, string buildType, string appID, string orgID, string authSecret, string appToken, string orgToken, string restUrl, out string lastError)
        {
            lastError = null;
            var authError = RuntimeAuthConfig.ValidateAuthFields(useAppTokens, buildType, appID, orgID, authSecret, appToken, orgToken);
            if (authError != null)
            {
                lastError = "Authentication error: " + authError;
                return false;
            }

            if (!TryValidateRestUrl(restUrl, out lastError))
                return false;

            return true;
        }

        /// <summary>Validates a REST base URL for <see cref="restUrl"/> (same rules as full config validation).</summary>
        internal static bool TryValidateRestUrl(string restUrl, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(restUrl))
            {
                errorMessage = "Configuration validation failed - restUrl is required but not set.";
                return false;
            }

            if (!Utils.IsValidUrl(restUrl))
            {
                errorMessage = $"Configuration validation failed - restUrl '{restUrl}' is not a valid HTTP/HTTPS URL";
                return false;
            }

            return true;
        }

        public string buildType = "production";
        public string appID;
        public string orgID;
        public string authSecret;
        public string launcherAppID;
        public bool useAppTokens = true;
        public string appToken;
        public string orgToken;
        public string restUrl = "https://lib-backend.xrdm.app/";
        public bool authUIFollowCamera = true;
        public bool enableDirectTouchInteraction = true;
        public float authUIDistanceFromCamera = 1.0f;
        public bool headsetTracking = true;
        public float positionTrackingPeriodSeconds = 1f;
        public float defaultMaxDistanceLimit = 50f;
        public bool defaultAutoCreateTriggerCollider = true;
        public bool enableAutoStartAuthentication = true;
        public float authenticationStartDelay = 0f;
        public bool enableAutoStartModules = true;
        public bool enableAutoAdvanceModules = true;
        public bool enableReturnTo = true;
        public bool enablePinPadGuestAccess = true;
        public GameObject KeyboardPrefab;
        public GameObject PinPrefab;
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
        public bool enableArborInsightsClient = true;
        public bool enableArborMdmClient = true;
        public bool enableLearnerLauncherMode = false;
        public bool enableAutomaticTelemetry = true;
        public bool enableSceneEvents = true;
        public int maxDictionarySize = 50;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool unitTestConfigEnabled = false;
        public string unitTestAuthPin = "";
        public string unitTestAuthBadPin = "";
        public string unitTestAuthText = "";
        public string unitTestAuthEmail = "";
        public string unitTestAuthEmailDomain = "";
        public string unitTestDeviceId = "";
        public string unitTestFingerprint = "";
        public string unitTestSsoAccessToken = "";
#endif

        /// <summary>Validates runtime settings (auth + URL + numeric clamp).</summary>
        public bool IsValid()
        {
            if (!TryValidateAuthAndUrl(useAppTokens, buildType, appID, orgID, authSecret, appToken, orgToken, restUrl, out var err))
            {
                _lastValidationErrorMessage = err;
                return false;
            }

            ClampNumericSettingsCore();
            _lastValidationErrorMessage = null;
            return true;
        }

        /// <summary>Clamps numeric fields on this runtime instance.</summary>
        public void ClampNumericSettings() => ClampNumericSettingsCore();

        private void ClampNumericSettingsCore()
        {
            ClampInt(ref sendRetriesOnFailure, 0, 10, 3);
            ClampInt(ref sendRetryIntervalSeconds, 1, 300, 3);
            ClampInt(ref sendNextBatchWaitSeconds, 1, 3600, 30);
            ClampInt(ref requestTimeoutSeconds, 5, 300, 30);
            ClampInt(ref stragglerTimeoutSeconds, 0, 3600, 15);
            ClampFloat(ref maxCallFrequencySeconds, 0.1f, 60f, 1f);
            ClampInt(ref dataEntriesPerSendAttempt, 1, 1000, 32);
            ClampInt(ref storageEntriesPerSendAttempt, 1, 1000, 16);
            ClampInt(ref pruneSentItemsOlderThanHours, 0, 8760, 12);
            ClampInt(ref maximumCachedItems, 10, 10000, 1024);
            ClampInt(ref maxDictionarySize, 5, 1000, 50);
            ClampFloat(ref positionTrackingPeriodSeconds, 0.1f, 60f, 1f);
            ClampFloat(ref frameRateTrackingPeriodSeconds, 0.1f, 60f, 0.5f);
            ClampFloat(ref telemetryTrackingPeriodSeconds, 1f, 300f, 10f);
            ClampFloat(ref authenticationStartDelay, 0f, 60f, 0f);
            ClampFloat(ref defaultMaxDistanceLimit, 0f, 10000f, 50f);
        }

        private static void ClampInt(ref int value, int min, int max, int defaultValue)
        {
            if (value < min) value = Mathf.Clamp(defaultValue, min, max);
            else if (value > max) value = max;
        }

        private static void ClampFloat(ref float value, float min, float max, float defaultValue)
        {
            if (value < min) value = Mathf.Clamp(defaultValue, min, max);
            else if (value > max) value = max;
        }

        private static Configuration CopyFromAppConfig(AppConfig a)
        {
            var c = new Configuration();
            CopyFromAppConfigInto(c, a);
            c.ClampNumericSettingsCore();
            return c;
        }

        private static void CopyFromAppConfigInto(Configuration c, AppConfig a)
        {
            c.buildType = a.buildType;
            c.appID = a.appID;
            c.orgID = a.orgID;
            c.authSecret = a.authSecret;
            c.launcherAppID = a.launcherAppID;
            c.useAppTokens = a.useAppTokens;
            c.appToken = a.appToken;
            c.orgToken = a.orgToken;
            c.restUrl = a.restUrl;
            c.authUIFollowCamera = a.authUIFollowCamera;
            c.enableDirectTouchInteraction = a.enableDirectTouchInteraction;
            c.authUIDistanceFromCamera = a.authUIDistanceFromCamera;
            c.headsetTracking = a.headsetTracking;
            c.positionTrackingPeriodSeconds = a.positionTrackingPeriodSeconds;
            c.defaultMaxDistanceLimit = a.defaultMaxDistanceLimit;
            c.defaultAutoCreateTriggerCollider = a.defaultAutoCreateTriggerCollider;
            c.enableAutoStartAuthentication = a.enableAutoStartAuthentication;
            c.authenticationStartDelay = a.authenticationStartDelay;
            c.enableAutoStartModules = a.enableAutoStartModules;
            c.enableAutoAdvanceModules = a.enableAutoAdvanceModules;
            c.enableReturnTo = a.enableReturnTo;
            c.enablePinPadGuestAccess = a.enablePinPadGuestAccess;
            c.KeyboardPrefab = a.KeyboardPrefab;
            c.PinPrefab = a.PinPrefab;
            c.telemetryTrackingPeriodSeconds = a.telemetryTrackingPeriodSeconds;
            c.frameRateTrackingPeriodSeconds = a.frameRateTrackingPeriodSeconds;
            c.sendRetriesOnFailure = a.sendRetriesOnFailure;
            c.sendRetryIntervalSeconds = a.sendRetryIntervalSeconds;
            c.sendNextBatchWaitSeconds = a.sendNextBatchWaitSeconds;
            c.requestTimeoutSeconds = a.requestTimeoutSeconds;
            c.stragglerTimeoutSeconds = a.stragglerTimeoutSeconds;
            c.maxCallFrequencySeconds = a.maxCallFrequencySeconds;
            c.dataEntriesPerSendAttempt = a.dataEntriesPerSendAttempt;
            c.storageEntriesPerSendAttempt = a.storageEntriesPerSendAttempt;
            c.pruneSentItemsOlderThanHours = a.pruneSentItemsOlderThanHours;
            c.maximumCachedItems = a.maximumCachedItems;
            c.retainLocalAfterSent = a.retainLocalAfterSent;
            c.enableArborInsightsClient = a.enableArborInsightsClient;
            c.enableArborMdmClient = a.enableArborMdmClient;
            c.enableLearnerLauncherMode = a.enableLearnerLauncherMode;
            c.enableAutomaticTelemetry = a.enableAutomaticTelemetry;
            c.enableSceneEvents = a.enableSceneEvents;
            c.maxDictionarySize = a.maxDictionarySize;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            c.unitTestConfigEnabled = a.unitTestConfigEnabled;
            c.unitTestAuthPin = a.unitTestAuthPin;
            c.unitTestAuthBadPin = a.unitTestAuthBadPin;
            c.unitTestAuthText = a.unitTestAuthText;
            c.unitTestAuthEmail = a.unitTestAuthEmail;
            c.unitTestAuthEmailDomain = a.unitTestAuthEmailDomain;
            c.unitTestDeviceId = a.unitTestDeviceId;
            c.unitTestFingerprint = a.unitTestFingerprint;
            c.unitTestSsoAccessToken = a.unitTestSsoAccessToken;
#endif
        }

        /// <summary>Merges GET /v1/storage/config into the runtime instance only. Not applied: credentials, token mode, build type, module timing, auth UI, prefabs, ArborInsightsClient/ArborMdmClient (build-time from AppConfig only).</summary>
        public void ApplyConfigPayload(ConfigPayload payload)
        {
            if (payload == null) return;

            if (!string.IsNullOrEmpty(payload.restUrl)) restUrl = payload.restUrl;
            if (!string.IsNullOrEmpty(payload.sendRetriesOnFailure)) sendRetriesOnFailure = Convert.ToInt32(payload.sendRetriesOnFailure, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.sendRetryInterval)) sendRetryIntervalSeconds = Convert.ToInt32(payload.sendRetryInterval, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.sendNextBatchWait)) sendNextBatchWaitSeconds = Convert.ToInt32(payload.sendNextBatchWait, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.stragglerTimeout)) stragglerTimeoutSeconds = Convert.ToInt32(payload.stragglerTimeout, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.requestTimeoutSeconds)) requestTimeoutSeconds = Convert.ToInt32(payload.requestTimeoutSeconds, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.maxCallFrequencySeconds) && float.TryParse(payload.maxCallFrequencySeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out float maxCallFreq))
                maxCallFrequencySeconds = maxCallFreq;
            if (!string.IsNullOrEmpty(payload.dataEntriesPerSendAttempt)) dataEntriesPerSendAttempt = Convert.ToInt32(payload.dataEntriesPerSendAttempt, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.storageEntriesPerSendAttempt)) storageEntriesPerSendAttempt = Convert.ToInt32(payload.storageEntriesPerSendAttempt, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.pruneSentItemsOlderThan)) pruneSentItemsOlderThanHours = Convert.ToInt32(payload.pruneSentItemsOlderThan, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.maximumCachedItems)) maximumCachedItems = Convert.ToInt32(payload.maximumCachedItems, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(payload.retainLocalAfterSent)) retainLocalAfterSent = Convert.ToBoolean(payload.retainLocalAfterSent, CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(payload.launcherAppID)) launcherAppID = payload.launcherAppID;

            if (payload.headsetTracking.HasValue) headsetTracking = payload.headsetTracking.Value;
            if (payload.enableReturnTo.HasValue) enableReturnTo = payload.enableReturnTo.Value;
            if (payload.enablePinPadGuestAccess.HasValue) enablePinPadGuestAccess = payload.enablePinPadGuestAccess.Value;
            else if (payload.authMechanism != null && payload.authMechanism.allowGuest.HasValue)
                enablePinPadGuestAccess = payload.authMechanism.allowGuest.Value;

            if (payload.enableAutomaticTelemetry.HasValue) enableAutomaticTelemetry = payload.enableAutomaticTelemetry.Value;
            if (payload.enableSceneEvents.HasValue) enableSceneEvents = payload.enableSceneEvents.Value;
            if (!string.IsNullOrEmpty(payload.maxDictionarySize)) maxDictionarySize = Convert.ToInt32(payload.maxDictionarySize, CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(payload.positionCapturePeriod) && float.TryParse(payload.positionCapturePeriod, NumberStyles.Float, CultureInfo.InvariantCulture, out float positionPeriod))
                positionTrackingPeriodSeconds = positionPeriod;
            if (!string.IsNullOrEmpty(payload.frameRateCapturePeriod) && float.TryParse(payload.frameRateCapturePeriod, NumberStyles.Float, CultureInfo.InvariantCulture, out float frameRatePeriod))
                frameRateTrackingPeriodSeconds = frameRatePeriod;
            if (!string.IsNullOrEmpty(payload.telemetryCapturePeriod) && float.TryParse(payload.telemetryCapturePeriod, NumberStyles.Float, CultureInfo.InvariantCulture, out float telemetryPeriod))
                telemetryTrackingPeriodSeconds = telemetryPeriod;

            ClampNumericSettingsCore();
        }

        internal static void ClearRuntimeConfig()
        {
            if (_authoringAsset != null && _instance != null)
            {
                CopyFromAppConfigInto(_instance, _authoringAsset);
                _instance.ClampNumericSettingsCore();
            }
        }

        internal static void ResetForTesting()
        {
            _instance = null;
            _authoringAsset = null;
            _validatedOnce = false;
            _lastValidationErrorMessage = null;
        }
    }
}

using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace AbxrLib.Editor
{
    [CustomEditor(typeof(Configuration))]
    public class ConfigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (Configuration)target;
            EditorGUILayout.LabelField("Application Identity", EditorStyles.boldLabel);
            
            string[] buildTypeValues = { "production", "development", "production_custom" };
            string[] buildTypeDisplayNames = { "Production", "Development", "Production (Custom APK)" };
            int currentSelection = config.buildType == "production" ? 0 : (config.buildType == "development" ? 1 : 2);
            int newSelection = EditorGUILayout.Popup(new GUIContent(
                "Build Type", "Production: OrgID and AuthSecret will NOT be included in builds (secure for 3rd party distribution).\nDevelopment: OrgID and AuthSecret will be included in builds (for custom APKs only).\nProduction (Custom APK): For single-customer builds; requires Organization Token; API receives buildType Production."),
                currentSelection, buildTypeDisplayNames);
            
            config.buildType = buildTypeValues[newSelection];
                        
            EditorGUILayout.Space();
            
            bool useAppTokens = config.useAppTokens;
            bool isProduction = config.buildType == "production";
            
            if (useAppTokens)
            {
                config.appToken = EditorGUILayout.TextField(new GUIContent(
                    "App Token", "App Token (JWT) from ArborXR Portal – identifies app and publisher. Required when Use App Tokens is on."), config.appToken);
                
                bool isProductionCustom = config.buildType == "production_custom";
                EditorGUI.BeginDisabledGroup(isProduction);
                string orgTokenLabel = isProductionCustom ? "Organization Token (required)" : "Organization Token (optional)";
                string orgTokenTooltip = isProductionCustom
                    ? "Required for Production (Custom APK). Set the customer's org token from ArborXR Portal."
                    : "Optional. In Development: use this or leave empty to use App Token as org token. In Production this field is not used.";
                config.orgToken = EditorGUILayout.TextField(new GUIContent(orgTokenLabel, orgTokenTooltip), config.orgToken);
                EditorGUI.EndDisabledGroup();
                if (isProduction)
                    EditorGUILayout.HelpBox("In Production, Organization Token from config is not sent. The field is disabled for shared production builds.", MessageType.Info);
                else if (isProductionCustom)
                    EditorGUILayout.HelpBox(
                        "Production (Custom APK): For custom APKs per customer. Organization Token is required. The API receives buildType Production.",
                        MessageType.Info);
                else
                    EditorGUILayout.HelpBox(
                        "In Development you can set an Organization Token, or leave empty to use the App Token as the org token.",
                        MessageType.Info);
            }
            else
            {
                config.appID = EditorGUILayout.TextField(new GUIContent(
                    "Application ID (required)", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"), config.appID);
                
                bool isProductionCustomLegacy = config.buildType == "production_custom";
                EditorGUI.BeginDisabledGroup(isProduction);
                string orgIdLabel = isProductionCustomLegacy ? "Organization ID (required)" : "Organization ID (*)";
                string orgIdTooltip = isProductionCustomLegacy
                    ? "Required for Production (Custom APK). Set the customer's organization ID from ArborXR Portal."
                    : "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                config.orgID = EditorGUILayout.TextField(new GUIContent(orgIdLabel, orgIdTooltip), config.orgID);
                string authSecretLabel = isProductionCustomLegacy ? "Authorization Secret (required)" : "Authorization Secret (*)";
                config.authSecret = EditorGUILayout.TextField(new GUIContent(authSecretLabel, "Required for Production (Custom APK) when using legacy auth."), config.authSecret);
                EditorGUI.EndDisabledGroup();
                
                if (isProduction)
                {
                    EditorGUILayout.HelpBox("OrgID and AuthSecret are disabled in Production builds. These values will NOT be included in builds.", MessageType.Info);
                }
                else if (isProductionCustomLegacy)
                {
                    EditorGUILayout.HelpBox(
                        "Production (Custom APK): For custom APKs per customer. Organization ID and Authorization Secret are required. The API receives buildType Production.",
                        MessageType.Info);
                }
            }
                    // Use App Tokens checkbox right under buildType dropdown
            EditorGUILayout.Space(5);
            config.useAppTokens = EditorGUILayout.Toggle(new GUIContent(
                "Use App Tokens", "When enabled, use App Tokens (JWT) instead of appID/orgID/authSecret combination"), config.useAppTokens);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Service Provider", EditorStyles.boldLabel);
            string newRestUrl = EditorGUILayout.TextField(new GUIContent(
                "REST URL", "Should most likely be\nhttps://lib-backend.xrdm.app/ during Beta"), config.restUrl);
            
            // Validate URL format
            if (!string.IsNullOrEmpty(newRestUrl))
            {
                try
                {
                    var uri = new System.Uri(newRestUrl);
                    if (uri.Scheme == "http" || uri.Scheme == "https")
                    {
                        config.restUrl = newRestUrl;
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("URL must start with http:// or https://", MessageType.Warning);
                    }
                }
                catch
                {
                    EditorGUILayout.HelpBox("Invalid URL format", MessageType.Warning);
                }
            }
            else
            {
                config.restUrl = newRestUrl;
            }
            //if (config.restUrl == "https://lib-backend.xrdm.dev/") config.restUrl = "https://lib-backend.xrdm.app/"; //TODO remove
            EditorGUILayout.Space();
        
            // Warning about production usage
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚠️ PRODUCTION BUILD WARNING", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("* Fields marked with asterisk should NOT be set when building for 3rd parties.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• Application ID should ALWAYS be set", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Organization ID and Authorization Secret should ONLY be set for custom APKs", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Setting these values inappropriately may violate Terms of Service with ArborXR or Meta", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Only use these fields when building for a specific 3rd party who is aware and approves", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Behavior Control", EditorStyles.boldLabel);
            config.authUIFollowCamera = EditorGUILayout.Toggle(new GUIContent(
                "Auth UI Follow Camera", "When enabled, UI panels will follow the camera. When disabled, panels will remain in fixed positions."), config.authUIFollowCamera);
            
            config.enableDirectTouchInteraction = EditorGUILayout.Toggle(new GUIContent(
                "Enable Direct Touch Interaction", "When enabled, direct touch interaction will be used for UI elements instead of ray casting."), config.enableDirectTouchInteraction);
            
            config.authUIDistanceFromCamera = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                "Auth UI Distance From Camera (meters)", "How far in front of the camera the UI panel should float."), config.authUIDistanceFromCamera), 0.1f, 10f);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Player Tracking", EditorStyles.boldLabel);
        
            // Disable headset tracking UI when automatic telemetry is off
            config.enableAutomaticTelemetry = EditorGUILayout.Toggle("Enable Automatic Telemetry", config.enableAutomaticTelemetry);
            EditorGUI.BeginDisabledGroup(!config.enableAutomaticTelemetry);
                config.headsetTracking = EditorGUILayout.Toggle(new GUIContent(
                    "Headset/Controller Tracking", "Track the Headset and Controllers"), config.headsetTracking);
                config.positionTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                    "Position Capture Period (seconds)", config.positionTrackingPeriodSeconds), 0.1f, 60f);
                config.enableSceneEvents = EditorGUILayout.Toggle("Enable Scene Events", config.enableSceneEvents);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Gaze Tracking", EditorStyles.boldLabel);
            config.defaultMaxDistanceLimit = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                "Default Max Distance (meters)", "Global default maximum distance for AbxrTarget occlusion checks. 0 = unlimited. Individual AbxrTarget components can override."), config.defaultMaxDistanceLimit), 0f, 10000f);
            config.defaultAutoCreateTriggerCollider = EditorGUILayout.Toggle(new GUIContent(
                "Default Auto Create Trigger Collider", "Global default for auto-creating trigger colliders on AbxrTarget. Individual AbxrTarget components can override."), config.defaultAutoCreateTriggerCollider);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authentication Control", EditorStyles.boldLabel);
            config.enableAutoStartAuthentication = EditorGUILayout.Toggle(new GUIContent(
                "Enable Auto Start Authentication", "When enabled, authentication will start automatically on app launch. When disabled, you must manually call Abxr.StartAuthentication()"), config.enableAutoStartAuthentication);

            EditorGUI.BeginDisabledGroup(!config.enableAutoStartAuthentication);
                config.authenticationStartDelay = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                    "Authentication Start Delay (seconds)", "Delay in seconds before starting authentication (only applies when auto-start is enabled)"), config.authenticationStartDelay), 0f, 60f);
            EditorGUI.EndDisabledGroup();
            config.returnToLauncherAfterAssessmentComplete = !EditorGUILayout.Toggle(new GUIContent(
                "Return to LL after Assessment Complete", "When enabled, the app will return to the Learner Launcher after an assessment is complete. When disabled, the app will stay open after an assessment is complete. Specifically used with Learner Launcher."), !config.returnToLauncherAfterAssessmentComplete);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authentication Prefabs", EditorStyles.boldLabel);
            
            // Help box explaining how prefab references work
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ℹ️ Prefab Reference Usage", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Leave empty to use default prefabs from Resources/Prefabs/", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Assign custom prefabs to override the default behavior", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• Custom prefabs must have the same components as the default ones", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            
            config.KeyboardPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent(
                "Keyboard Prefab", "Custom keyboard prefab. Leave empty to use default AbxrKeyboard prefab."), 
                config.KeyboardPrefab, typeof(GameObject));
            config.PinPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent(
                "Pin Prefab", "Custom PIN pad prefab. Leave empty to use default AbxrPinPad prefab."), 
                config.PinPrefab, typeof(GameObject));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Network Configuration", EditorStyles.boldLabel);
            config.telemetryTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Telemetry Tracking Period (seconds)", config.telemetryTrackingPeriodSeconds), 1f, 300f);
            config.frameRateTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Frame Rate Tracking Period (seconds)", config.frameRateTrackingPeriodSeconds), 0.1f, 60f);
            config.sendRetriesOnFailure = Mathf.Clamp(EditorGUILayout.IntField("Send Retries On Failure", config.sendRetriesOnFailure), 0, 10);
            config.sendRetryIntervalSeconds = Mathf.Clamp(EditorGUILayout.IntField("Send Retry Interval (seconds)", config.sendRetryIntervalSeconds), 1, 300);
            config.sendNextBatchWaitSeconds = Mathf.Clamp(EditorGUILayout.IntField("Send Next Batch Wait (seconds)", config.sendNextBatchWaitSeconds), 1, 3600);
            config.requestTimeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Request Timeout (seconds)", "How long to wait before giving up on network requests"), config.requestTimeoutSeconds), 5, 300);
            config.stragglerTimeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Straggler Timeout (seconds)", "0 = Infinite, i.e. Never send remainders = Always send exactly DataEntriesPerSendAttempt"), config.stragglerTimeoutSeconds), 0, 3600);
            config.maxCallFrequencySeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Maximum Data Send Frequency (seconds)", config.maxCallFrequencySeconds), 0.1f, 60f);
            config.dataEntriesPerSendAttempt = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Data Entries Per Send Attempt", "Total count of events, logs, and telemetry entries to batch before sending (0 = Send all not already sent)"), config.dataEntriesPerSendAttempt), 1, 1000);
        
            config.storageEntriesPerSendAttempt = Mathf.Clamp(EditorGUILayout.IntField("Storage Entries Per Send Attempt", config.storageEntriesPerSendAttempt), 1, 1000);
            config.pruneSentItemsOlderThanHours = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Prune Sent Items Older Than (hours)", "0 = Infinite, i.e. Never Prune"), config.pruneSentItemsOlderThanHours), 0, 8760);
            config.maximumCachedItems = Mathf.Clamp(EditorGUILayout.IntField("Maximum Cached Items", config.maximumCachedItems), 10, 10000);

            config.enableArborInsightServiceClient = EditorGUILayout.Toggle(new GUIContent(
                "Enable Insights Device Service Usage", "When enabled, the app will use the ArborInsightService device APK for auth and data on Android when installed. When disabled, only REST/cloud is used."), config.enableArborInsightServiceClient);

            if (GUILayout.Button("Reset To Sending Rule Defaults"))
            {
                // Create a temporary instance to get the default values
                var defaultConfig = CreateInstance<Configuration>();
                
                // Service Provider
                config.restUrl = defaultConfig.restUrl;
                
                // UI Behavior Control
                config.authUIFollowCamera = defaultConfig.authUIFollowCamera;
                config.enableDirectTouchInteraction = defaultConfig.enableDirectTouchInteraction;
                config.authUIDistanceFromCamera = defaultConfig.authUIDistanceFromCamera;
                
                // Player Tracking
                config.headsetTracking = defaultConfig.headsetTracking;
                config.positionTrackingPeriodSeconds = defaultConfig.positionTrackingPeriodSeconds;
                
                // Target Gaze Tracking
                config.defaultMaxDistanceLimit = defaultConfig.defaultMaxDistanceLimit;
                config.defaultAutoCreateTriggerCollider = defaultConfig.defaultAutoCreateTriggerCollider;
                
                // Authentication Control
                config.enableAutoStartAuthentication = defaultConfig.enableAutoStartAuthentication;
                config.authenticationStartDelay = defaultConfig.authenticationStartDelay;
                config.returnToLauncherAfterAssessmentComplete = defaultConfig.returnToLauncherAfterAssessmentComplete;
                
                // Authentication Prefabs
                config.KeyboardPrefab = defaultConfig.KeyboardPrefab;
                config.PinPrefab = defaultConfig.PinPrefab;
                
                // Data Sending Rules
                config.telemetryTrackingPeriodSeconds = defaultConfig.telemetryTrackingPeriodSeconds;
                config.frameRateTrackingPeriodSeconds = defaultConfig.frameRateTrackingPeriodSeconds;
                config.sendRetriesOnFailure = defaultConfig.sendRetriesOnFailure;
                config.sendRetryIntervalSeconds = defaultConfig.sendRetryIntervalSeconds;
                config.sendNextBatchWaitSeconds = defaultConfig.sendNextBatchWaitSeconds;
                config.requestTimeoutSeconds = defaultConfig.requestTimeoutSeconds;
                config.stragglerTimeoutSeconds = defaultConfig.stragglerTimeoutSeconds;
                config.maxCallFrequencySeconds = defaultConfig.maxCallFrequencySeconds;
                config.dataEntriesPerSendAttempt = defaultConfig.dataEntriesPerSendAttempt;
                config.storageEntriesPerSendAttempt = defaultConfig.storageEntriesPerSendAttempt;
                config.pruneSentItemsOlderThanHours = defaultConfig.pruneSentItemsOlderThanHours;
                config.maximumCachedItems = defaultConfig.maximumCachedItems;
                config.retainLocalAfterSent = defaultConfig.retainLocalAfterSent;
                config.enableArborInsightServiceClient = defaultConfig.enableArborInsightServiceClient;
                config.enableAutomaticTelemetry = defaultConfig.enableAutomaticTelemetry;
                config.enableSceneEvents = defaultConfig.enableSceneEvents;
                
                // Clean up the temporary instance
                DestroyImmediate(defaultConfig);
            }

            if (GUI.changed) EditorUtility.SetDirty(config);
        }
    
    }
}
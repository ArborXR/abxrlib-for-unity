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
        
            config.appID = EditorGUILayout.TextField(new GUIContent(
                "Application ID (required)", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"), config.appID);
            config.orgID = EditorGUILayout.TextField(new GUIContent(
                "Organization ID (*)", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"), config.orgID);
            config.authSecret = EditorGUILayout.TextField("Authorization Secret (*)", config.authSecret);
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Tracking", EditorStyles.boldLabel);
        
            // Disable headset tracking UI if telemetry is disabled
            EditorGUI.BeginDisabledGroup(config.disableAutomaticTelemetry);
            config.headsetTracking = EditorGUILayout.Toggle(new GUIContent(
                "Headset/Controller Tracking", "Track the Headset and Controllers"), config.headsetTracking);
            config.positionTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Position Capture Period (seconds)", config.positionTrackingPeriodSeconds), 0.1f, 60f);
            EditorGUI.EndDisabledGroup();
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Network", EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField("Data Sending Rules", EditorStyles.boldLabel);
            config.telemetryTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Telemetry Tracking Period (seconds)", config.telemetryTrackingPeriodSeconds), 1f, 300f);
            config.frameRateTrackingPeriodSeconds = Mathf.Clamp(EditorGUILayout.FloatField(
                "Frame Rate Tracking Period (seconds)", config.frameRateTrackingPeriodSeconds), 0.1f, 60f);
            config.sendRetriesOnFailure = Mathf.Clamp(EditorGUILayout.IntField("Send Retries On Failure", config.sendRetriesOnFailure), 0, 10);
            config.sendRetryIntervalSeconds = Mathf.Clamp(EditorGUILayout.IntField("Send Retry Interval Seconds", config.sendRetryIntervalSeconds), 1, 300);
            config.sendNextBatchWaitSeconds = Mathf.Clamp(EditorGUILayout.IntField("Send Next Batch Wait Seconds", config.sendNextBatchWaitSeconds), 1, 3600);
            config.requestTimeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Request Timeout Seconds", "How long to wait before giving up on network requests"), config.requestTimeoutSeconds), 5, 300);
            config.stragglerTimeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Straggler Timeout Seconds", "0 = Infinite, i.e. Never send remainders = Always send exactly DataEntriesPerSendAttempt"), config.stragglerTimeoutSeconds), 0, 3600);
            config.dataEntriesPerSendAttempt = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Data Entries Per Send Attempt", "Total count of events, logs, and telemetry entries to batch before sending (0 = Send all not already sent)"), config.dataEntriesPerSendAttempt), 1, 1000);
        
            config.storageEntriesPerSendAttempt = Mathf.Clamp(EditorGUILayout.IntField("Storage Entries Per Send Attempt", config.storageEntriesPerSendAttempt), 1, 1000);
            config.pruneSentItemsOlderThanHours = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(
                "Prune Sent Items Older Than Hours", "0 = Infinite, i.e. Never Prune"), config.pruneSentItemsOlderThanHours), 0, 8760);
            config.maximumCachedItems = Mathf.Clamp(EditorGUILayout.IntField("Maximum Cached Items", config.maximumCachedItems), 10, 10000);
            config.retainLocalAfterSent = EditorGUILayout.Toggle("Retain Local After Sent", config.retainLocalAfterSent);
            config.disableAutomaticTelemetry = EditorGUILayout.Toggle("Disable Automatic Telemetry", config.disableAutomaticTelemetry);
            config.disableSceneEvents = EditorGUILayout.Toggle("Disable Scene Events", config.disableSceneEvents);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authentication Control", EditorStyles.boldLabel);
            config.disableAutoStartAuthentication = EditorGUILayout.Toggle(new GUIContent(
                "Disable Auto Start Authentication", "When enabled, authentication will NOT start automatically on app launch. You must manually call Abxr.StartAuthentication()"), config.disableAutoStartAuthentication);
            
            // Only show delay field if auto-start is enabled
            if (!config.disableAutoStartAuthentication)
            {
                config.authenticationStartDelay = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                    "Authentication Start Delay (seconds)", "Delay in seconds before starting authentication (only applies when auto-start is enabled)"), config.authenticationStartDelay), 0f, 60f);
            }

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
            EditorGUILayout.LabelField("UI Behavior Control", EditorStyles.boldLabel);
            config.authUIFollowCamera = EditorGUILayout.Toggle(new GUIContent(
                "Auth UI Follow Camera", "When enabled, UI panels will follow the camera. When disabled, panels will remain in fixed positions."), config.authUIFollowCamera);
            
            config.enableDirectTouchInteraction = EditorGUILayout.Toggle(new GUIContent(
                "Enable Direct Touch Interaction", "When enabled, direct touch interaction will be used for UI elements instead of ray casting."), config.enableDirectTouchInteraction);


            if (GUILayout.Button("Reset To Sending Rule Defaults"))
            {
                // Create a temporary instance to get the default values
                var defaultConfig = CreateInstance<Configuration>();
                
                config.positionTrackingPeriodSeconds = defaultConfig.positionTrackingPeriodSeconds;
                config.telemetryTrackingPeriodSeconds = defaultConfig.telemetryTrackingPeriodSeconds;
                config.frameRateTrackingPeriodSeconds = defaultConfig.frameRateTrackingPeriodSeconds;
                config.sendRetriesOnFailure = defaultConfig.sendRetriesOnFailure;
                config.sendRetryIntervalSeconds = defaultConfig.sendRetryIntervalSeconds;
                config.sendNextBatchWaitSeconds = defaultConfig.sendNextBatchWaitSeconds;
                config.requestTimeoutSeconds = defaultConfig.requestTimeoutSeconds;
                config.stragglerTimeoutSeconds = defaultConfig.stragglerTimeoutSeconds;
                config.dataEntriesPerSendAttempt = defaultConfig.dataEntriesPerSendAttempt;
                config.storageEntriesPerSendAttempt = defaultConfig.storageEntriesPerSendAttempt;
                config.pruneSentItemsOlderThanHours = defaultConfig.pruneSentItemsOlderThanHours;
                config.maximumCachedItems = defaultConfig.maximumCachedItems;
                config.retainLocalAfterSent = defaultConfig.retainLocalAfterSent;
                config.disableAutomaticTelemetry = defaultConfig.disableAutomaticTelemetry;
                config.disableSceneEvents = defaultConfig.disableSceneEvents;
                config.disableAutoStartAuthentication = defaultConfig.disableAutoStartAuthentication;
                config.authenticationStartDelay = defaultConfig.authenticationStartDelay;
                config.authUIFollowCamera = defaultConfig.authUIFollowCamera;
                config.enableDirectTouchInteraction = defaultConfig.enableDirectTouchInteraction;
                
                // Clean up the temporary instance
                DestroyImmediate(defaultConfig);
            }

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

            if (GUI.changed) EditorUtility.SetDirty(config);
        }
    
    }
}
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Configuration))]
public class ConfigInspector : Editor
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
        config.positionTrackingPeriodSeconds = EditorGUILayout.FloatField(
            "Position Capture Period (seconds)", config.positionTrackingPeriodSeconds);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Network", EditorStyles.boldLabel);
        config.restUrl = EditorGUILayout.TextField(new GUIContent(
            "REST URL", "Should most likely be\nhttps://lib-backend.xrdm.app/ during Beta"), config.restUrl);
        //if (config.restUrl == "https://lib-backend.xrdm.dev/") config.restUrl = "https://lib-backend.xrdm.app/"; //TODO remove
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Data Sending Rules", EditorStyles.boldLabel);
        config.telemetryTrackingPeriodSeconds = EditorGUILayout.FloatField(
            "Telemetry Tracking Period (seconds)", config.telemetryTrackingPeriodSeconds);
        config.frameRateTrackingPeriodSeconds = EditorGUILayout.FloatField(
            "Frame Rate Tracking Period (seconds)", config.frameRateTrackingPeriodSeconds);
        config.sendRetriesOnFailure = EditorGUILayout.IntField("Send Retries On Failure", config.sendRetriesOnFailure);
        config.sendRetryIntervalSeconds = EditorGUILayout.IntField("Send Retry Interval Seconds", config.sendRetryIntervalSeconds);
        config.sendNextBatchWaitSeconds = EditorGUILayout.IntField("Send Next Batch Wait Seconds", config.sendNextBatchWaitSeconds);
        config.stragglerTimeoutSeconds = EditorGUILayout.IntField(new GUIContent(
            "Straggler Timeout Seconds", "0 = Infinite, i.e. Never send remainders = Always send exactly EventsPerSendAttempt"), config.stragglerTimeoutSeconds);
        config.eventsPerSendAttempt = EditorGUILayout.IntField(new GUIContent(
            "Events Per Send Attempt", "0 = Send all not already sent"), config.eventsPerSendAttempt);
        config.logsPerSendAttempt = EditorGUILayout.IntField("Logs Per Send Attempt", config.logsPerSendAttempt);
        
        // Disable telemetry entries field if telemetry is disabled
        EditorGUI.BeginDisabledGroup(config.disableAutomaticTelemetry);
        config.telemetryEntriesPerSendAttempt = EditorGUILayout.IntField("Telemetry Entries Per Send Attempt", config.telemetryEntriesPerSendAttempt);
        EditorGUI.EndDisabledGroup();
        
        config.storageEntriesPerSendAttempt = EditorGUILayout.IntField("Storage Entries Per Send Attempt", config.storageEntriesPerSendAttempt);
        config.pruneSentItemsOlderThanHours = EditorGUILayout.IntField(new GUIContent(
            "Prune Sent Items Older Than Hours", "0 = Infinite, i.e. Never Prune"), config.pruneSentItemsOlderThanHours);
        config.maximumCachedItems = EditorGUILayout.IntField("Maximum Cached Items", config.maximumCachedItems);
        config.retainLocalAfterSent = EditorGUILayout.Toggle("Retain Local After Sent", config.retainLocalAfterSent);
        config.disableAutomaticTelemetry = EditorGUILayout.Toggle("Disable Automatic Telemetry", config.disableAutomaticTelemetry);
        config.disableSceneEvents = EditorGUILayout.Toggle("Disable Scene Events", config.disableSceneEvents);

        if (GUILayout.Button("Reset To Sending Rule Defaults"))
        {
            config.positionTrackingPeriodSeconds = 1f;
            config.telemetryTrackingPeriodSeconds = 10f;
            config.frameRateTrackingPeriodSeconds = 0.5f;
            config.sendRetriesOnFailure = 3;
            config.sendRetryIntervalSeconds = 3;
            config.sendNextBatchWaitSeconds = 30;
            config.stragglerTimeoutSeconds = 15;
            config.eventsPerSendAttempt = 16;
            config.logsPerSendAttempt = 16;
            config.telemetryEntriesPerSendAttempt = 16;
            config.storageEntriesPerSendAttempt = 16;
            config.pruneSentItemsOlderThanHours = 12;
            config.maximumCachedItems = 1024;
            config.retainLocalAfterSent = false;
            config.disableAutomaticTelemetry = false;
            config.disableSceneEvents = false;
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
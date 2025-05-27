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
            "Organization ID (optional)", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"), config.orgID);
        config.authSecret = EditorGUILayout.TextField("Authorization Secret (optional)", config.authSecret);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Tracking", EditorStyles.boldLabel);
        
        // Disable headset tracking UI if telemetry is disabled
        EditorGUI.BeginDisabledGroup(config.disableTelemetry);
        config.headsetTracking = EditorGUILayout.Toggle(new GUIContent(
            "Headset/Controller Tracking", "Track the Headset and Controllers"), config.headsetTracking);
        config.trackingUpdatesPerMinute = EditorGUILayout.IntField(
            "Tracking Updates Per Minute", config.trackingUpdatesPerMinute);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Network", EditorStyles.boldLabel);
        config.restUrl = EditorGUILayout.TextField(new GUIContent(
            "REST URL", "Should most likely be\nhttps://lib-backend.xrdm.dev/ during Beta"), config.restUrl);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Data Sending Rules", EditorStyles.boldLabel);
        config.sendRetriesOnFailure = EditorGUILayout.IntField("Send Retries On Failure", config.sendRetriesOnFailure);
        config.sendRetryIntervalSeconds = EditorGUILayout.IntField("Send Retry Interval Seconds", config.sendRetryIntervalSeconds);
        config.sendNextBatchWaitSeconds = EditorGUILayout.IntField("Send Next Batch Wait Seconds", config.sendNextBatchWaitSeconds);
        config.stragglerTimeoutSeconds = EditorGUILayout.IntField(new GUIContent(
            "Straggler Timeout Seconds", "0 = Infinite, i.e. Never send remainders = Always send exactly EventsPerSendAttempt"), config.stragglerTimeoutSeconds);
        config.eventsPerSendAttempt = EditorGUILayout.IntField(new GUIContent(
            "Events Per Send Attempt", "0 = Send all not already sent"), config.eventsPerSendAttempt);
        config.logsPerSendAttempt = EditorGUILayout.IntField("Logs Per Send Attempt", config.logsPerSendAttempt);
        
        // Disable telemetry entries field if telemetry is disabled
        EditorGUI.BeginDisabledGroup(config.disableTelemetry);
        config.telemetryEntriesPerSendAttempt = EditorGUILayout.IntField("Telemetry Entries Per Send Attempt", config.telemetryEntriesPerSendAttempt);
        EditorGUI.EndDisabledGroup();
        
        config.storageEntriesPerSendAttempt = EditorGUILayout.IntField("Storage Entries Per Send Attempt", config.storageEntriesPerSendAttempt);
        config.pruneSentItemsOlderThanHours = EditorGUILayout.IntField(new GUIContent(
            "Prune Sent Items Older Than Hours", "0 = Infinite, i.e. Never Prune"), config.pruneSentItemsOlderThanHours);
        config.maximumCachedItems = EditorGUILayout.IntField("Maximum Cached Items", config.maximumCachedItems);
        config.retainLocalAfterSent = EditorGUILayout.Toggle("Retain Local After Sent", config.retainLocalAfterSent);
        config.disableTelemetry = EditorGUILayout.Toggle("Disable Telemetry", config.disableTelemetry);

        if (GUILayout.Button("Reset To Sending Rule Defaults"))
        {
            config.sendRetriesOnFailure = 3;
            config.sendRetryIntervalSeconds = 3;
            config.sendNextBatchWaitSeconds = 30;
            config.stragglerTimeoutSeconds = 15;
            config.eventsPerSendAttempt = 4;
            config.logsPerSendAttempt = 4;
            config.telemetryEntriesPerSendAttempt = 4;
            config.storageEntriesPerSendAttempt = 4;
            config.pruneSentItemsOlderThanHours = 12;
            config.maximumCachedItems = 1024;
            config.retainLocalAfterSent = false;
            config.disableTelemetry = false;
        }

        if (GUI.changed) EditorUtility.SetDirty(config);
    }
    
}
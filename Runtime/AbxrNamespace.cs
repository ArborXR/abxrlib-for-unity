// Simple namespace bridge compatible with C# 9.0
// This file provides a global Abxr class that users can access directly

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Create a global Abxr class that users can access directly
// We'll rename the original Core class to avoid conflicts
public static class Abxr
{
    // Enums that users can access as Abxr.ResultOptions, etc.
    public enum ResultOptions
    {
        Null = 0,
        Pass = 1,
        Fail = 2,
        Complete = 3,
        Incomplete = 4,
        Browsed = 5
    }

    public enum EventStatus
    {
        Pass = 0,
        Fail = 1,
        Complete = 2,
        Incomplete = 3,
        Browsed = 4
    }

    public enum InteractionType
    {
        Null = 0,
        Bool = 1,
        Select = 2,
        Text = 3,
        Rating = 4,
        Number = 5,
        Matching = 6,
        Performance = 7,
        Sequencing = 8
    }

    public enum StoragePolicy
    {
        keepLatest = 0,
        appendHistory = 1
    }

    public enum StorageScope
    {
        device = 0,
        user = 1
    }

    // Forward all static properties
    public static Action onHeadsetPutOnNewSession
    {
        get => Abxr.Runtime.Core.AbxrCore.onHeadsetPutOnNewSession;
        set => Abxr.Runtime.Core.AbxrCore.onHeadsetPutOnNewSession = value;
    }
    
    public static Action<bool, string> onAuthCompleted
    {
        get => Abxr.Runtime.Core.AbxrCore.onAuthCompleted;
        set => Abxr.Runtime.Core.AbxrCore.onAuthCompleted = value;
    }

    // Core Methods
    public static void TrackAutoTelemetry() => Abxr.Runtime.Core.AbxrCore.TrackAutoTelemetry();

    // Logging Methods
    public static void LogDebug(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.LogDebug(text, meta);
    
    public static void LogInfo(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.LogInfo(text, meta);
    
    public static void LogWarn(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.LogWarn(text, meta);
    
    public static void LogError(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.LogError(text, meta);
    
    public static void LogCritical(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.LogCritical(text, meta);

    // Event Methods
    public static void Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true) => 
        Abxr.Runtime.Core.AbxrCore.Event(name, meta, sendTelemetry);
    
    public static void Event(string name, Vector3 position, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.Event(name, position, meta);

    public static void TelemetryEntry(string name, Dictionary<string, string> meta) => 
        Abxr.Runtime.Core.AbxrCore.TelemetryEntry(name, meta);

    // Storage Methods
    public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.AbxrCore.StorageGetDefaultEntry((Abxr.Runtime.Core.AbxrCore.StorageScope)scope, callback);
    
    public static IEnumerator StorageGetEntry(string name, StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.AbxrCore.StorageGetEntry(name, (Abxr.Runtime.Core.AbxrCore.StorageScope)scope, callback);
    
    public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.AbxrCore.StorageSetDefaultEntry(entry, (Abxr.Runtime.Core.AbxrCore.StorageScope)scope, (Abxr.Runtime.Core.AbxrCore.StoragePolicy)policy);
    
    public static void StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.AbxrCore.StorageSetEntry(name, entry, (Abxr.Runtime.Core.AbxrCore.StorageScope)scope, (Abxr.Runtime.Core.AbxrCore.StoragePolicy)policy);
    
    public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.AbxrCore.StorageRemoveDefaultEntry((Abxr.Runtime.Core.AbxrCore.StorageScope)scope);
    
    public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.AbxrCore.StorageRemoveEntry(name, (Abxr.Runtime.Core.AbxrCore.StorageScope)scope);
    
    public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.AbxrCore.StorageRemoveMultipleEntries((Abxr.Runtime.Core.AbxrCore.StorageScope)scope);

    // AI Methods
    public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.AbxrCore.AIProxy(prompt, llmProvider, callback);
    
    public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.AbxrCore.AIProxy(prompt, pastMessages, llmProvider, callback);

    // Event Wrapper Functions - The main ones causing the compilation errors
    public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventAssessmentStart(assessmentName, meta);
    
    public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.AbxrCore.ResultOptions)result, meta);
    
    public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.AbxrCore.EventStatus)status, meta);

    public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventObjectiveStart(objectiveName, meta);
    
    public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.AbxrCore.ResultOptions)result, meta);
    
    public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.AbxrCore.EventStatus)status, meta);

    public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventInteractionStart(interactionName, meta);
    
    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventInteractionComplete(interactionName, result, resultOptions, (Abxr.Runtime.Core.AbxrCore.InteractionType)interactionType, meta);
    
    public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventInteractionComplete(interactionName, (Abxr.Runtime.Core.AbxrCore.InteractionType)interactionType, response, meta);

    public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventLevelStart(levelName, meta);
    
    public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventLevelComplete(levelName, score, meta);

    public static void EventCritical(string label, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.AbxrCore.EventCritical(label, meta);

    // UI Methods
    public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null) => 
        Abxr.Runtime.Core.AbxrCore.PresentKeyboard(promptText, keyboardType, emailDomain);

    public static void PollUser(string prompt, Abxr.Runtime.UI.ExitPoll.ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) => 
        Abxr.Runtime.Core.AbxrCore.PollUser(prompt, pollType, responses, callback);

    // Authentication Methods
    public static void ReAuthenticate() => Abxr.Runtime.Core.AbxrCore.ReAuthenticate();
    public static void StartNewSession() => Abxr.Runtime.Core.AbxrCore.StartNewSession();
    public static void ContinueSession(string sessionId) => Abxr.Runtime.Core.AbxrCore.ContinueSession(sessionId);

    // Device Information Methods
    public static string GetDeviceId() => Abxr.Runtime.Core.AbxrCore.GetDeviceId();
    public static string GetDeviceSerial() => Abxr.Runtime.Core.AbxrCore.GetDeviceSerial();
    public static string GetDeviceTitle() => Abxr.Runtime.Core.AbxrCore.GetDeviceTitle();
    public static string[] GetDeviceTags() => Abxr.Runtime.Core.AbxrCore.GetDeviceTags();
    public static string GetOrgId() => Abxr.Runtime.Core.AbxrCore.GetOrgId();
    public static string GetOrgTitle() => Abxr.Runtime.Core.AbxrCore.GetOrgTitle();
    public static string GetOrgSlug() => Abxr.Runtime.Core.AbxrCore.GetOrgSlug();
    public static string GetMacAddressFixed() => Abxr.Runtime.Core.AbxrCore.GetMacAddressFixed();
    public static string GetMacAddressRandom() => Abxr.Runtime.Core.AbxrCore.GetMacAddressRandom();
    public static bool GetIsAuthenticated() => Abxr.Runtime.Core.AbxrCore.GetIsAuthenticated();
    public static string GetAccessToken() => Abxr.Runtime.Core.AbxrCore.GetAccessToken();
    public static string GetRefreshToken() => Abxr.Runtime.Core.AbxrCore.GetRefreshToken();
    public static DateTime? GetExpiresDateUtc() => Abxr.Runtime.Core.AbxrCore.GetExpiresDateUtc();
    public static string GetFingerprint() => Abxr.Runtime.Core.AbxrCore.GetFingerprint();
}
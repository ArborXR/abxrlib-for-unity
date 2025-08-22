// Simple namespace bridge compatible with C# 9.0
// This file provides a global Abxr class that users can access directly

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Note: This creates a new global Abxr class that shadows the one in Abxr.Runtime.Core
// Users will access this one instead of the original
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
        get => Abxr.Runtime.Core.Abxr.onHeadsetPutOnNewSession;
        set => Abxr.Runtime.Core.Abxr.onHeadsetPutOnNewSession = value;
    }
    
    public static Action<bool, string> onAuthCompleted
    {
        get => Abxr.Runtime.Core.Abxr.onAuthCompleted;
        set => Abxr.Runtime.Core.Abxr.onAuthCompleted = value;
    }

    // Core Methods
    public static void TrackAutoTelemetry() => Abxr.Runtime.Core.Abxr.TrackAutoTelemetry();

    // Logging Methods
    public static void LogDebug(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.LogDebug(text, meta);
    
    public static void LogInfo(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.LogInfo(text, meta);
    
    public static void LogWarn(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.LogWarn(text, meta);
    
    public static void LogError(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.LogError(text, meta);
    
    public static void LogCritical(string text, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.LogCritical(text, meta);

    // Event Methods
    public static void Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true) => 
        Abxr.Runtime.Core.Abxr.Event(name, meta, sendTelemetry);
    
    public static void Event(string name, Vector3 position, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.Event(name, position, meta);

    public static void TelemetryEntry(string name, Dictionary<string, string> meta) => 
        Abxr.Runtime.Core.Abxr.TelemetryEntry(name, meta);

    // Storage Methods
    public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.Abxr.StorageGetDefaultEntry((Abxr.Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static IEnumerator StorageGetEntry(string name, StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.Abxr.StorageGetEntry(name, (Abxr.Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.Abxr.StorageSetDefaultEntry(entry, (Abxr.Runtime.Core.Abxr.StorageScope)scope, (Abxr.Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.Abxr.StorageSetEntry(name, entry, (Abxr.Runtime.Core.Abxr.StorageScope)scope, (Abxr.Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveDefaultEntry((Abxr.Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveEntry(name, (Abxr.Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveMultipleEntries((Abxr.Runtime.Core.Abxr.StorageScope)scope);

    // AI Methods
    public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.Abxr.AIProxy(prompt, llmProvider, callback);
    
    public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.Abxr.AIProxy(prompt, pastMessages, llmProvider, callback);

    // Event Wrapper Functions - The main ones causing the compilation errors
    public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentStart(assessmentName, meta);
    
    public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveStart(objectiveName, meta);
    
    public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventInteractionStart(interactionName, meta);
    
    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventInteractionComplete(interactionName, result, resultOptions, (Abxr.Runtime.Core.Abxr.InteractionType)interactionType, meta);
    
    public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventInteractionComplete(interactionName, (Abxr.Runtime.Core.Abxr.InteractionType)interactionType, response, meta);

    public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventLevelStart(levelName, meta);
    
    public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventLevelComplete(levelName, score, meta);

    public static void EventCritical(string label, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventCritical(label, meta);

    // UI Methods
    public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null) => 
        Abxr.Runtime.Core.Abxr.PresentKeyboard(promptText, keyboardType, emailDomain);

    public static void PollUser(string prompt, Abxr.Runtime.UI.ExitPoll.ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) => 
        Abxr.Runtime.Core.Abxr.PollUser(prompt, pollType, responses, callback);

    // Authentication Methods
    public static void ReAuthenticate() => Abxr.Runtime.Core.Abxr.ReAuthenticate();
    public static void StartNewSession() => Abxr.Runtime.Core.Abxr.StartNewSession();
    public static void ContinueSession(string sessionId) => Abxr.Runtime.Core.Abxr.ContinueSession(sessionId);

    // Device Information Methods
    public static string GetDeviceId() => Abxr.Runtime.Core.Abxr.GetDeviceId();
    public static string GetDeviceSerial() => Abxr.Runtime.Core.Abxr.GetDeviceSerial();
    public static string GetDeviceTitle() => Abxr.Runtime.Core.Abxr.GetDeviceTitle();
    public static string[] GetDeviceTags() => Abxr.Runtime.Core.Abxr.GetDeviceTags();
    public static string GetOrgId() => Abxr.Runtime.Core.Abxr.GetOrgId();
    public static string GetOrgTitle() => Abxr.Runtime.Core.Abxr.GetOrgTitle();
    public static string GetOrgSlug() => Abxr.Runtime.Core.Abxr.GetOrgSlug();
    public static string GetMacAddressFixed() => Abxr.Runtime.Core.Abxr.GetMacAddressFixed();
    public static string GetMacAddressRandom() => Abxr.Runtime.Core.Abxr.GetMacAddressRandom();
    public static bool GetIsAuthenticated() => Abxr.Runtime.Core.Abxr.GetIsAuthenticated();
    public static string GetAccessToken() => Abxr.Runtime.Core.Abxr.GetAccessToken();
    public static string GetRefreshToken() => Abxr.Runtime.Core.Abxr.GetRefreshToken();
    public static DateTime? GetExpiresDateUtc() => Abxr.Runtime.Core.Abxr.GetExpiresDateUtc();
    public static string GetFingerprint() => Abxr.Runtime.Core.Abxr.GetFingerprint();
}
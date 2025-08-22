// Namespace bridge to make Abxr.Runtime.Core.Abxr accessible as just Abxr
// This resolves the Git URL vs local installation namespace resolution differences
// Compatible with C# 9.0 (no global using directives needed)

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Abxr.Runtime.UI.ExitPoll;

// Create a global static class Abxr that forwards to Abxr.Runtime.Core.Abxr
public static class Abxr
{
    // Forward all the enums as nested types
    public enum ResultOptions
    {
        Null = Runtime.Core.Abxr.ResultOptions.Null,
        Pass = Runtime.Core.Abxr.ResultOptions.Pass,
        Fail = Runtime.Core.Abxr.ResultOptions.Fail,
        Complete = Runtime.Core.Abxr.ResultOptions.Complete,
        Incomplete = Runtime.Core.Abxr.ResultOptions.Incomplete,
        Browsed = Runtime.Core.Abxr.ResultOptions.Browsed
    }

    public enum EventStatus
    {
        Pass = Runtime.Core.Abxr.EventStatus.Pass,
        Fail = Runtime.Core.Abxr.EventStatus.Fail,
        Complete = Runtime.Core.Abxr.EventStatus.Complete,
        Incomplete = Runtime.Core.Abxr.EventStatus.Incomplete,
        Browsed = Runtime.Core.Abxr.EventStatus.Browsed
    }

    public enum InteractionType
    {
        Null = Runtime.Core.Abxr.InteractionType.Null,
        Bool = Runtime.Core.Abxr.InteractionType.Bool,
        Select = Runtime.Core.Abxr.InteractionType.Select,
        Text = Runtime.Core.Abxr.InteractionType.Text,
        Rating = Runtime.Core.Abxr.InteractionType.Rating,
        Number = Runtime.Core.Abxr.InteractionType.Number,
        Matching = Runtime.Core.Abxr.InteractionType.Matching,
        Performance = Runtime.Core.Abxr.InteractionType.Performance,
        Sequencing = Runtime.Core.Abxr.InteractionType.Sequencing
    }

    public enum StoragePolicy
    {
        keepLatest = Runtime.Core.Abxr.StoragePolicy.keepLatest,
        appendHistory = Runtime.Core.Abxr.StoragePolicy.appendHistory
    }

    public enum StorageScope
    {
        device = Runtime.Core.Abxr.StorageScope.device,
        user = Runtime.Core.Abxr.StorageScope.user
    }

    // Forward all static properties
    public static Action onHeadsetPutOnNewSession
    {
        get => Runtime.Core.Abxr.onHeadsetPutOnNewSession;
        set => Runtime.Core.Abxr.onHeadsetPutOnNewSession = value;
    }
    
    public static Action<bool, string> onAuthCompleted
    {
        get => Runtime.Core.Abxr.onAuthCompleted;
        set => Runtime.Core.Abxr.onAuthCompleted = value;
    }

    // Forward all static methods
    public static void TrackAutoTelemetry() => Runtime.Core.Abxr.TrackAutoTelemetry();

    // Logging Methods
    public static void LogDebug(string text, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.LogDebug(text, meta);
    
    public static void LogInfo(string text, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.LogInfo(text, meta);
    
    public static void LogWarn(string text, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.LogWarn(text, meta);
    
    public static void LogError(string text, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.LogError(text, meta);
    
    public static void LogCritical(string text, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.LogCritical(text, meta);

    // Event Methods
    public static void Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true) => 
        Runtime.Core.Abxr.Event(name, meta, sendTelemetry);
    
    public static void Event(string name, Vector3 position, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.Event(name, position, meta);

    public static void TelemetryEntry(string name, Dictionary<string, string> meta) => 
        Runtime.Core.Abxr.TelemetryEntry(name, meta);

    // Storage Methods
    public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Runtime.Core.Abxr.StorageGetDefaultEntry((Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static IEnumerator StorageGetEntry(string name, StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Runtime.Core.Abxr.StorageGetEntry(name, (Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Runtime.Core.Abxr.StorageSetDefaultEntry(entry, (Runtime.Core.Abxr.StorageScope)scope, (Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        Runtime.Core.Abxr.StorageSetEntry(name, entry, (Runtime.Core.Abxr.StorageScope)scope, (Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user) => 
        Runtime.Core.Abxr.StorageRemoveDefaultEntry((Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user) => 
        Runtime.Core.Abxr.StorageRemoveEntry(name, (Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user) => 
        Runtime.Core.Abxr.StorageRemoveMultipleEntries((Runtime.Core.Abxr.StorageScope)scope);

    // AI Methods
    public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) => 
        Runtime.Core.Abxr.AIProxy(prompt, llmProvider, callback);
    
    public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) => 
        Runtime.Core.Abxr.AIProxy(prompt, pastMessages, llmProvider, callback);

    // Event Wrapper Functions - The main ones causing the compilation errors
    public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventAssessmentStart(assessmentName, meta);
    
    public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventObjectiveStart(objectiveName, meta);
    
    public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventInteractionStart(interactionName, meta);
    
    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventInteractionComplete(interactionName, result, resultOptions, (Runtime.Core.Abxr.InteractionType)interactionType, meta);
    
    public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventInteractionComplete(interactionName, (Runtime.Core.Abxr.InteractionType)interactionType, response, meta);

    public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventLevelStart(levelName, meta);
    
    public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventLevelComplete(levelName, score, meta);

    public static void EventCritical(string label, Dictionary<string, string> meta = null) => 
        Runtime.Core.Abxr.EventCritical(label, meta);

    // UI Methods
    public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null) => 
        Runtime.Core.Abxr.PresentKeyboard(promptText, keyboardType, emailDomain);

    public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) => 
        Runtime.Core.Abxr.PollUser(prompt, pollType, responses, callback);

    // Authentication Methods
    public static void ReAuthenticate() => Runtime.Core.Abxr.ReAuthenticate();
    public static void StartNewSession() => Runtime.Core.Abxr.StartNewSession();
    public static void ContinueSession(string sessionId) => Runtime.Core.Abxr.ContinueSession(sessionId);

    // Device Information Methods
    public static string GetDeviceId() => Runtime.Core.Abxr.GetDeviceId();
    public static string GetDeviceSerial() => Runtime.Core.Abxr.GetDeviceSerial();
    public static string GetDeviceTitle() => Runtime.Core.Abxr.GetDeviceTitle();
    public static string[] GetDeviceTags() => Runtime.Core.Abxr.GetDeviceTags();
    public static string GetOrgId() => Runtime.Core.Abxr.GetOrgId();
    public static string GetOrgTitle() => Runtime.Core.Abxr.GetOrgTitle();
    public static string GetOrgSlug() => Runtime.Core.Abxr.GetOrgSlug();
    public static string GetMacAddressFixed() => Runtime.Core.Abxr.GetMacAddressFixed();
    public static string GetMacAddressRandom() => Runtime.Core.Abxr.GetMacAddressRandom();
    public static bool GetIsAuthenticated() => Runtime.Core.Abxr.GetIsAuthenticated();
    public static string GetAccessToken() => Runtime.Core.Abxr.GetAccessToken();
    public static string GetRefreshToken() => Runtime.Core.Abxr.GetRefreshToken();
    public static DateTime? GetExpiresDateUtc() => Runtime.Core.Abxr.GetExpiresDateUtc();
    public static string GetFingerprint() => Runtime.Core.Abxr.GetFingerprint();
}
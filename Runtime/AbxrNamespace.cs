// Namespace bridge to expose Abxr.Runtime.Core classes in the Abxr namespace
// This resolves the Git URL vs local installation namespace resolution differences

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Abxr.Runtime.UI.ExitPoll;

namespace Abxr
{
    // Forward the enums from Abxr.Runtime.Core.Abxr to the Abxr namespace
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
}

// Create a static class with the exact methods expected by users
public static class Abxr
{
    // Events
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
    public static IEnumerator StorageGetDefaultEntry(Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.Abxr.StorageGetDefaultEntry((Abxr.Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static IEnumerator StorageGetEntry(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        Abxr.Runtime.Core.Abxr.StorageGetEntry(name, (Abxr.Runtime.Core.Abxr.StorageScope)scope, callback);
    
    public static void StorageSetDefaultEntry(Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy = Abxr.StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.Abxr.StorageSetDefaultEntry(entry, (Abxr.Runtime.Core.Abxr.StorageScope)scope, (Abxr.Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageSetEntry(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy = Abxr.StoragePolicy.keepLatest) => 
        Abxr.Runtime.Core.Abxr.StorageSetEntry(name, entry, (Abxr.Runtime.Core.Abxr.StorageScope)scope, (Abxr.Runtime.Core.Abxr.StoragePolicy)policy);
    
    public static void StorageRemoveDefaultEntry(Abxr.StorageScope scope = Abxr.StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveDefaultEntry((Abxr.Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveEntry(string name, Abxr.StorageScope scope = Abxr.StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveEntry(name, (Abxr.Runtime.Core.Abxr.StorageScope)scope);
    
    public static void StorageRemoveMultipleEntries(Abxr.StorageScope scope = Abxr.StorageScope.user) => 
        Abxr.Runtime.Core.Abxr.StorageRemoveMultipleEntries((Abxr.Runtime.Core.Abxr.StorageScope)scope);

    // AI Methods
    public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.Abxr.AIProxy(prompt, llmProvider, callback);
    
    public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) => 
        Abxr.Runtime.Core.Abxr.AIProxy(prompt, pastMessages, llmProvider, callback);

    // Event Wrapper Functions - The main ones causing the compilation errors
    public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentStart(assessmentName, meta);
    
    public static void EventAssessmentComplete(string assessmentName, string score, Abxr.ResultOptions result = Abxr.ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventAssessmentComplete(string assessmentName, int score, Abxr.EventStatus status = Abxr.EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventAssessmentComplete(assessmentName, score, (Abxr.Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveStart(objectiveName, meta);
    
    public static void EventObjectiveComplete(string objectiveName, string score, Abxr.ResultOptions result = Abxr.ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.Abxr.ResultOptions)result, meta);
    
    public static void EventObjectiveComplete(string objectiveName, int score, Abxr.EventStatus status = Abxr.EventStatus.Complete, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventObjectiveComplete(objectiveName, score, (Abxr.Runtime.Core.Abxr.EventStatus)status, meta);

    public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventInteractionStart(interactionName, meta);
    
    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", Abxr.InteractionType interactionType = Abxr.InteractionType.Null, Dictionary<string, string> meta = null) => 
        Abxr.Runtime.Core.Abxr.EventInteractionComplete(interactionName, result, resultOptions, (Abxr.Runtime.Core.Abxr.InteractionType)interactionType, meta);
    
    public static void EventInteractionComplete(string interactionName, Abxr.InteractionType interactionType, string response = "", Dictionary<string, string> meta = null) => 
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

    public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) => 
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

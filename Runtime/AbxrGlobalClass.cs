// Global Abxr class that forwards to AbxrLib.Runtime.Core.AbxrCore
// This allows users to access Abxr.EventAssessmentStart() without using statements
// Compatible with C# 9.0
// 
// Note: This is separate from Runtime/Core/Abxr.cs which contains AbxrCore class

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Type aliases to reference enums from AbxrCore (single source of truth)
using ResultOptions = AbxrLib.Runtime.Core.AbxrCore.ResultOptions;
using EventStatus = AbxrLib.Runtime.Core.AbxrCore.EventStatus;
using InteractionType = AbxrLib.Runtime.Core.AbxrCore.InteractionType;
using StoragePolicy = AbxrLib.Runtime.Core.AbxrCore.StoragePolicy;
using StorageScope = AbxrLib.Runtime.Core.AbxrCore.StorageScope;

// Create a global Abxr class that forwards to AbxrLib.Runtime.Core.AbxrCore
public static class Abxr
{
    // Nested enums that reference the source enums from AbxrCore (single source of truth)
    public enum ResultOptions
    {
        Null = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Null,
        Pass = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Pass,
        Fail = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Fail,
        Complete = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Complete,
        Incomplete = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Incomplete,
        Browsed = (int)AbxrLib.Runtime.Core.AbxrCore.ResultOptions.Browsed
    }

    public enum EventStatus
    {
        Pass = (int)AbxrLib.Runtime.Core.AbxrCore.EventStatus.Pass,
        Fail = (int)AbxrLib.Runtime.Core.AbxrCore.EventStatus.Fail,
        Complete = (int)AbxrLib.Runtime.Core.AbxrCore.EventStatus.Complete,
        Incomplete = (int)AbxrLib.Runtime.Core.AbxrCore.EventStatus.Incomplete,
        Browsed = (int)AbxrLib.Runtime.Core.AbxrCore.EventStatus.Browsed
    }

    public enum InteractionType
    {
        Null = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Null,
        Bool = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Bool,
        Select = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Select,
        Text = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Text,
        Rating = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Rating,
        Number = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Number,
        Matching = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Matching,
        Performance = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Performance,
        Sequencing = (int)AbxrLib.Runtime.Core.AbxrCore.InteractionType.Sequencing
    }

    public enum StoragePolicy
    {
        keepLatest = (int)AbxrLib.Runtime.Core.AbxrCore.StoragePolicy.keepLatest,
        appendHistory = (int)AbxrLib.Runtime.Core.AbxrCore.StoragePolicy.appendHistory
    }

    public enum StorageScope
    {
        device = (int)AbxrLib.Runtime.Core.AbxrCore.StorageScope.device,
        user = (int)AbxrLib.Runtime.Core.AbxrCore.StorageScope.user
    }

    // Forward all static properties
    public static Action onHeadsetPutOnNewSession
    {
        get => AbxrLib.Runtime.Core.AbxrCore.onHeadsetPutOnNewSession;
        set => AbxrLib.Runtime.Core.AbxrCore.onHeadsetPutOnNewSession = value;
    }
    
    public static Action<bool, string> onAuthCompleted
    {
        get => AbxrLib.Runtime.Core.AbxrCore.onAuthCompleted;
        set => AbxrLib.Runtime.Core.AbxrCore.onAuthCompleted = value;
    }

    // Core Methods
    public static void TrackAutoTelemetry() => AbxrLib.Runtime.Core.AbxrCore.TrackAutoTelemetry();

    // Logging Methods
    public static void LogDebug(string text, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.LogDebug(text, meta);
    
    public static void LogInfo(string text, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.LogInfo(text, meta);
    
    public static void LogWarn(string text, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.LogWarn(text, meta);
    
    public static void LogError(string text, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.LogError(text, meta);
    
    public static void LogCritical(string text, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.LogCritical(text, meta);

    // Event Methods
    public static void Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true) => 
        AbxrLib.Runtime.Core.AbxrCore.Event(name, meta, sendTelemetry);
    
    public static void Event(string name, Vector3 position, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.Event(name, position, meta);

    public static void TelemetryEntry(string name, Dictionary<string, string> meta) => 
        AbxrLib.Runtime.Core.AbxrCore.TelemetryEntry(name, meta);

    // Storage Methods
    public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageGetDefaultEntry((AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope, callback);
    
    public static IEnumerator StorageGetEntry(string name, StorageScope scope, Action<List<Dictionary<string, string>>> callback) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageGetEntry(name, (AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope, callback);
    
    public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageSetDefaultEntry(entry, (AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope, (AbxrLib.Runtime.Core.AbxrCore.StoragePolicy)policy);
    
    public static void StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageSetEntry(name, entry, (AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope, (AbxrLib.Runtime.Core.AbxrCore.StoragePolicy)policy);
    
    public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageRemoveDefaultEntry((AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope);
    
    public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageRemoveEntry(name, (AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope);
    
    public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user) => 
        AbxrLib.Runtime.Core.AbxrCore.StorageRemoveMultipleEntries((AbxrLib.Runtime.Core.AbxrCore.StorageScope)scope);

    // AI Methods
    public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) => 
        AbxrLib.Runtime.Core.AbxrCore.AIProxy(prompt, llmProvider, callback);
    
    public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) => 
        AbxrLib.Runtime.Core.AbxrCore.AIProxy(prompt, pastMessages, llmProvider, callback);

    // Event Wrapper Functions - The main ones causing the compilation errors
    public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventAssessmentStart(assessmentName, meta);
    
    public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventAssessmentComplete(assessmentName, score, (AbxrLib.Runtime.Core.AbxrCore.ResultOptions)result, meta);
    
    public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventAssessmentComplete(assessmentName, score, (AbxrLib.Runtime.Core.AbxrCore.EventStatus)status, meta);

    public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventObjectiveStart(objectiveName, meta);
    
    public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventObjectiveComplete(objectiveName, score, (AbxrLib.Runtime.Core.AbxrCore.ResultOptions)result, meta);
    
    public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventObjectiveComplete(objectiveName, score, (AbxrLib.Runtime.Core.AbxrCore.EventStatus)status, meta);

    public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventInteractionStart(interactionName, meta);
    
    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventInteractionComplete(interactionName, result, resultOptions, (AbxrLib.Runtime.Core.AbxrCore.InteractionType)interactionType, meta);
    
    public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventInteractionComplete(interactionName, (AbxrLib.Runtime.Core.AbxrCore.InteractionType)interactionType, response, meta);

    public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventLevelStart(levelName, meta);
    
    public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventLevelComplete(levelName, score, meta);

    public static void EventCritical(string label, Dictionary<string, string> meta = null) => 
        AbxrLib.Runtime.Core.AbxrCore.EventCritical(label, meta);

    // UI Methods
    public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null) => 
        AbxrLib.Runtime.Core.AbxrCore.PresentKeyboard(promptText, keyboardType, emailDomain);

    public static void PollUser(string prompt, AbxrLib.Runtime.UI.ExitPoll.ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) => 
        AbxrLib.Runtime.Core.AbxrCore.PollUser(prompt, pollType, responses, callback);

    // Authentication Methods
    public static void ReAuthenticate() => AbxrLib.Runtime.Core.AbxrCore.ReAuthenticate();
    public static void StartNewSession() => AbxrLib.Runtime.Core.AbxrCore.StartNewSession();
    public static void ContinueSession(string sessionId) => AbxrLib.Runtime.Core.AbxrCore.ContinueSession(sessionId);

    // Device Information Methods
    public static string GetDeviceId() => AbxrLib.Runtime.Core.AbxrCore.GetDeviceId();
    public static string GetDeviceSerial() => AbxrLib.Runtime.Core.AbxrCore.GetDeviceSerial();
    public static string GetDeviceTitle() => AbxrLib.Runtime.Core.AbxrCore.GetDeviceTitle();
    public static string[] GetDeviceTags() => AbxrLib.Runtime.Core.AbxrCore.GetDeviceTags();
    public static string GetOrgId() => AbxrLib.Runtime.Core.AbxrCore.GetOrgId();
    public static string GetOrgTitle() => AbxrLib.Runtime.Core.AbxrCore.GetOrgTitle();
    public static string GetOrgSlug() => AbxrLib.Runtime.Core.AbxrCore.GetOrgSlug();
    public static string GetMacAddressFixed() => AbxrLib.Runtime.Core.AbxrCore.GetMacAddressFixed();
    public static string GetMacAddressRandom() => AbxrLib.Runtime.Core.AbxrCore.GetMacAddressRandom();
    public static bool GetIsAuthenticated() => AbxrLib.Runtime.Core.AbxrCore.GetIsAuthenticated();
    public static string GetAccessToken() => AbxrLib.Runtime.Core.AbxrCore.GetAccessToken();
    public static string GetRefreshToken() => AbxrLib.Runtime.Core.AbxrCore.GetRefreshToken();
    public static DateTime? GetExpiresDateUtc() => AbxrLib.Runtime.Core.AbxrCore.GetExpiresDateUtc();
    public static string GetFingerprint() => AbxrLib.Runtime.Core.AbxrCore.GetFingerprint();
}
// Global Abxr class that forwards calls to the namespaced version
// This allows developers to use Abxr.Event() without any using statements
// while still keeping our namespace isolation

/// <summary>
/// Global static class that provides direct access to AbxrLib methods without requiring using statements.
/// This forwards all calls to the namespaced AbxrLib.Abxr class.
/// </summary>
public static class Abxr
{
    // Events
    public static event System.Action OnAuthenticationSuccess
    {
        add => global::AbxrLib.Abxr.OnAuthenticationSuccess += value;
        remove => global::AbxrLib.Abxr.OnAuthenticationSuccess -= value;
    }
    
    public static event System.Action<string> OnAuthenticationFailed
    {
        add => global::AbxrLib.Abxr.OnAuthenticationFailed += value;
        remove => global::AbxrLib.Abxr.OnAuthenticationFailed -= value;
    }

    // Core Methods
    public static void Event(string name, System.Collections.Generic.Dictionary<string, string> meta = null, bool sendTelemetry = true)
        => global::AbxrLib.Abxr.Event(name, meta, sendTelemetry);

    public static void Event(string name, UnityEngine.Vector3 position, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.Event(name, position, meta);

    // Logging Methods
    public static void LogDebug(string text, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.LogDebug(text, meta);

    public static void LogInfo(string text, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.LogInfo(text, meta);

    public static void LogWarn(string text, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.LogWarn(text, meta);

    public static void LogError(string text, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.LogError(text, meta);

    public static void LogCritical(string text, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.LogCritical(text, meta);

    // Assessment Methods
    public static void EventAssessmentStart(string assessmentName, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventAssessmentStart(assessmentName, meta);

    public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventAssessmentComplete(assessmentName, score, (global::AbxrLib.Abxr.ResultOptions)result, meta);

    public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventAssessmentComplete(assessmentName, score, (global::AbxrLib.Abxr.EventStatus)status, meta);

    // Objective Methods
    public static void EventObjectiveStart(string objectiveName, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventObjectiveStart(objectiveName, meta);

    public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventObjectiveComplete(objectiveName, score, (global::AbxrLib.Abxr.ResultOptions)result, meta);

    public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventObjectiveComplete(objectiveName, score, (global::AbxrLib.Abxr.EventStatus)status, meta);

    // Interaction Methods
    public static void EventInteractionStart(string interactionName, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventInteractionStart(interactionName, meta);

    public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventInteractionComplete(interactionName, result, resultOptions, (global::AbxrLib.Abxr.InteractionType)interactionType, meta);

    public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventInteractionComplete(interactionName, (global::AbxrLib.Abxr.InteractionType)interactionType, response, meta);

    // Level Methods
    public static void EventLevelStart(string levelName, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventLevelStart(levelName, meta);

    public static void EventLevelComplete(string levelName, string score, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventLevelComplete(levelName, score, meta);

    public static void EventCritical(string label, System.Collections.Generic.Dictionary<string, string> meta = null)
        => global::AbxrLib.Abxr.EventCritical(label, meta);

    // Authentication Methods
    public static void SetUserId(string userId)
        => global::AbxrLib.Abxr.SetUserId(userId);

    public static void SetUserMeta(string metaString)
        => global::AbxrLib.Abxr.SetUserMeta(metaString);

    public static void ReAuthenticate()
        => global::AbxrLib.Abxr.ReAuthenticate();

    public static void StartNewSession()
        => global::AbxrLib.Abxr.StartNewSession();

    public static void ContinueSession(string sessionId)
        => global::AbxrLib.Abxr.ContinueSession(sessionId);

    // Storage Methods
    public static System.Collections.IEnumerator StorageGetDefaultEntry(StorageScope scope, System.Action<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>> callback)
        => global::AbxrLib.Abxr.StorageGetDefaultEntry((global::AbxrLib.Abxr.StorageScope)scope, callback);

    public static System.Collections.IEnumerator StorageGetEntry(string name, StorageScope scope, System.Action<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>> callback)
        => global::AbxrLib.Abxr.StorageGetEntry(name, (global::AbxrLib.Abxr.StorageScope)scope, callback);

    public static void StorageSetDefaultEntry(System.Collections.Generic.Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest)
        => global::AbxrLib.Abxr.StorageSetDefaultEntry(entry, (global::AbxrLib.Abxr.StorageScope)scope, (global::AbxrLib.Abxr.StoragePolicy)policy);

    public static void StorageSetEntry(string name, System.Collections.Generic.Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest)
        => global::AbxrLib.Abxr.StorageSetEntry(name, entry, (global::AbxrLib.Abxr.StorageScope)scope, (global::AbxrLib.Abxr.StoragePolicy)policy);

    public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user)
        => global::AbxrLib.Abxr.StorageRemoveDefaultEntry((global::AbxrLib.Abxr.StorageScope)scope);

    public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user)
        => global::AbxrLib.Abxr.StorageRemoveEntry(name, (global::AbxrLib.Abxr.StorageScope)scope);

    public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user)
        => global::AbxrLib.Abxr.StorageRemoveMultipleEntries((global::AbxrLib.Abxr.StorageScope)scope);

    // AI Methods
    public static System.Collections.IEnumerator AIProxy(string prompt, string llmProvider, System.Action<string> callback)
        => global::AbxrLib.Abxr.AIProxy(prompt, llmProvider, callback);

    public static System.Collections.IEnumerator AIProxy(string prompt, System.Collections.Generic.List<string> pastMessages, string llmProvider, System.Action<string> callback)
        => global::AbxrLib.Abxr.AIProxy(prompt, pastMessages, llmProvider, callback);

    // UI Methods
    public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null)
        => global::AbxrLib.Abxr.PresentKeyboard(promptText, keyboardType, emailDomain);

    public static void PollUser(string prompt, global::AbxrLib.ExitPollHandler.PollType pollType, System.Collections.Generic.List<string> responses = null, System.Action<string> callback = null)
        => global::AbxrLib.Abxr.PollUser(prompt, pollType, responses, callback);

    // Telemetry Methods
    public static void TrackAutoTelemetry()
        => global::AbxrLib.Abxr.TrackAutoTelemetry();

    public static void TelemetryEntry(string name, System.Collections.Generic.Dictionary<string, string> meta)
        => global::AbxrLib.Abxr.TelemetryEntry(name, meta);

    // Device Info Methods
    public static string GetDeviceId()
        => global::AbxrLib.Abxr.GetDeviceId();

    public static string GetDeviceSerial()
        => global::AbxrLib.Abxr.GetDeviceSerial();

    public static string GetDeviceTitle()
        => global::AbxrLib.Abxr.GetDeviceTitle();

    public static string[] GetDeviceTags()
        => global::AbxrLib.Abxr.GetDeviceTags();

    public static string GetOrgId()
        => global::AbxrLib.Abxr.GetOrgId();

    public static string GetOrgTitle()
        => global::AbxrLib.Abxr.GetOrgTitle();

    public static string GetOrgSlug()
        => global::AbxrLib.Abxr.GetOrgSlug();

    public static string GetMacAddressFixed()
        => global::AbxrLib.Abxr.GetMacAddressFixed();

    public static string GetMacAddressRandom()
        => global::AbxrLib.Abxr.GetMacAddressRandom();

    public static bool GetIsAuthenticated()
        => global::AbxrLib.Abxr.GetIsAuthenticated();

    public static string GetAccessToken()
        => global::AbxrLib.Abxr.GetAccessToken();

    public static string GetRefreshToken()
        => global::AbxrLib.Abxr.GetRefreshToken();

    public static System.DateTime? GetExpiresDateUtc()
        => global::AbxrLib.Abxr.GetExpiresDateUtc();

    public static string GetFingerprint()
        => global::AbxrLib.Abxr.GetFingerprint();

    // Enums (forward from namespaced version)
    public enum ResultOptions
    {
        Null = global::AbxrLib.Abxr.ResultOptions.Null,
        Pass = global::AbxrLib.Abxr.ResultOptions.Pass,
        Fail = global::AbxrLib.Abxr.ResultOptions.Fail,
        Complete = global::AbxrLib.Abxr.ResultOptions.Complete,
        Incomplete = global::AbxrLib.Abxr.ResultOptions.Incomplete,
        Browsed = global::AbxrLib.Abxr.ResultOptions.Browsed
    }

    public enum EventStatus
    {
        Pass = global::AbxrLib.Abxr.EventStatus.Pass,
        Fail = global::AbxrLib.Abxr.EventStatus.Fail,
        Complete = global::AbxrLib.Abxr.EventStatus.Complete,
        Incomplete = global::AbxrLib.Abxr.EventStatus.Incomplete,
        Browsed = global::AbxrLib.Abxr.EventStatus.Browsed
    }

    public enum InteractionType
    {
        Null = global::AbxrLib.Abxr.InteractionType.Null,
        Bool = global::AbxrLib.Abxr.InteractionType.Bool,
        Select = global::AbxrLib.Abxr.InteractionType.Select,
        Text = global::AbxrLib.Abxr.InteractionType.Text,
        Rating = global::AbxrLib.Abxr.InteractionType.Rating,
        Number = global::AbxrLib.Abxr.InteractionType.Number,
        Matching = global::AbxrLib.Abxr.InteractionType.Matching,
        Performance = global::AbxrLib.Abxr.InteractionType.Performance,
        Sequencing = global::AbxrLib.Abxr.InteractionType.Sequencing
    }

    public enum StoragePolicy
    {
        keepLatest = global::AbxrLib.Abxr.StoragePolicy.keepLatest,
        appendHistory = global::AbxrLib.Abxr.StoragePolicy.appendHistory
    }

    public enum StorageScope
    {
        device = global::AbxrLib.Abxr.StorageScope.device,
        user = global::AbxrLib.Abxr.StorageScope.user
    }
}

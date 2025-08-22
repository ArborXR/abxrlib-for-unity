using System;
using System.Collections;
using System.Collections.Generic;
using Abxr.Runtime.Common;
using Abxr.Runtime.Events;
using Abxr.Runtime.Logs;
using Abxr.Runtime.ServiceClient;
using Abxr.Runtime.Storage;
using Abxr.Runtime.Telemetry;
using Abxr.Runtime.UI.ExitPoll;
using Abxr.Runtime.UI.Keyboard;
using UnityEngine;

//namespace Abxr.Runtime.Core
//{
	public static class Abxr
	{
		private static readonly Dictionary<string, DateTime> AssessmentStartTimes = new();
		private static readonly Dictionary<string, DateTime> ObjectiveStartTimes = new();
		private static readonly Dictionary<string, DateTime> InteractionStartTimes = new();
		private static readonly Dictionary<string, DateTime> LevelStartTimes = new();
	
		public static Action onHeadsetPutOnNewSession;
		
		// 'true' for success and 'false' for failure
		public static Action<bool, string> onAuthCompleted;
	
		public enum ResultOptions // Only here for backwards compatibility
		{
			Null,
			Pass,
			Fail,
			Complete,
			Incomplete,
			Browsed
		}
	
		public static EventStatus ToEventStatus(this ResultOptions options) => options switch // Only here for backwards compatibility
		{
			ResultOptions.Null => EventStatus.Complete,
			ResultOptions.Pass => EventStatus.Pass,
			ResultOptions.Fail => EventStatus.Fail,
			ResultOptions.Complete => EventStatus.Complete,
			ResultOptions.Incomplete => EventStatus.Incomplete,
			ResultOptions.Browsed => EventStatus.Browsed,
			_ => EventStatus.Complete // Default case for any undefined enum values
		};

		public enum EventStatus
		{
			Pass,
			Fail,
			Complete,
			Incomplete,
			Browsed
		}
	
		public enum InteractionType
		{
			Null,
			Bool,
			Select,
			Text,
			Rating,
			Number,
			Matching,
			Performance,
			Sequencing
		}

		public enum StoragePolicy
		{
			keepLatest,
			appendHistory
		}

		public enum StorageScope
		{
			device,
			user
		}

		/// <summary>
		/// If you select 'Disable Automatic Telemetry' in the config,
		/// you can manually start tracking this telemetry with this function call
		/// </summary>
		public static void TrackAutoTelemetry() => TrackSystemInfo.StartTracking();

		/// <summary>
		/// Add log information at the 'Debug' level
		/// </summary>
		/// <param name="text">The log text</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void LogDebug(string text, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			LogBatcher.Add("debug", text, meta);
		}
    
		/// <summary>
		/// Add log information at the 'Informational' level
		/// </summary>
		/// <param name="text">The log text</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void LogInfo(string text, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			LogBatcher.Add("info", text, meta);
		}
    
		/// <summary>
		/// Add log information at the 'Warning' level
		/// </summary>
		/// <param name="text">The log text</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void LogWarn(string text, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			LogBatcher.Add("warn", text, meta);
		}
    
		/// <summary>
		/// Add log information at the 'Error' level
		/// </summary>
		/// <param name="text">The log text</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void LogError(string text, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			LogBatcher.Add("error", text, meta);
		}
    
		/// <summary>
		/// Add log information at the 'Critical' level
		/// </summary>
		/// <param name="text">The log text</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void LogCritical(string text, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			LogBatcher.Add("critical", text, meta);
		}

		/// <summary>
		/// Add event information
		/// </summary>
		/// <param name="name">Name of the event</param>
		/// <param name="meta">Any additional information (optional)</param>
		/// <param name="sendTelemetry">Send telemetry with the event (optional)</param>
		public static void Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			EventBatcher.Add(name, meta);
			if (sendTelemetry)
			{
				TrackSystemInfo.SendAll();
				TrackInputDevices.SendLocationData();
			}
		}

		/// <summary>
		/// Add event information
		/// </summary>
		/// <param name="name">Name of the event</param>
		/// <param name="position">Adds position tracking of the object</param>
		/// <param name="meta">Any additional information (optional)</param>
		public static void Event(string name, Vector3 position, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["position_x"] = position.x.ToString();
			meta["position_y"] = position.y.ToString();
			meta["position_z"] = position.z.ToString();
			Event(name, meta);
		}
	
		/// <summary>
		/// Add telemetry information
		/// </summary>
		/// <param name="name">Name of the telemetry</param>
		/// <param name="meta">Any additional information</param>
		public static void TelemetryEntry(string name, Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
			TelemetryBatcher.Add(name, meta);
		}

		/// <summary>
		/// Get the session data with the default name 'state'
		/// Call this as follows:
		/// StartCoroutine(StorageGetDefaultEntry(scope, result => {
		///	    Debug.Log("Result: " + result);
		/// }));
		/// </summary>
		/// <param name="scope">Get from 'device' or 'user'</param>
		/// <param name="callback">Return value when finished</param>
		/// <returns>All the session data stored under the default name 'state'</returns>
		public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			yield return StorageBatcher.Get("state", scope, callback);
		}

		/// <summary>
		/// Get the session data with the given name
		/// Call this as follows:
		/// StartCoroutine(StorageGetDefaultEntry(scope, result => {
		///	    Debug.Log("Result: " + result);
		/// }));
		/// </summary>
		/// <param name="name">The name of the entry to retrieve</param>
		/// <param name="scope">Get from 'device' or 'user'</param>
		/// <param name="callback">Return value when finished</param>
		/// <returns>All the session data stored under the given name</returns>
		public static IEnumerator StorageGetEntry(string name, StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			yield return StorageBatcher.Get(name, scope, callback);
		}

		/// <summary>
		/// Set the session data with the default name 'state'
		/// </summary>
		/// <param name="entry">The data to store</param>
		/// <param name="scope">Store under 'device' or 'user'</param>
		/// <param name="policy">How should this be stored, 'keep latest' or 'append history' (defaults to 'keep latest')</param>
		public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest)
		{
			StorageBatcher.Add("state", entry, scope, policy);
		}
	
		/// <summary>
		/// Set the session data with the given name
		/// </summary>
		/// <param name="name">The name of the entry to store</param>
		/// <param name="entry">The data to store</param>
		/// <param name="scope">Store under 'device' or 'user'</param>
		/// <param name="policy">How should this be stored, 'keep latest' or 'append history' (defaults to 'keep latest')</param>
		public static void StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest)
		{
			StorageBatcher.Add(name, entry, scope, policy);
		}

		/// <summary>
		/// Remove the session data stored under the default name 'state'
		/// </summary>
		/// <param name="scope">Remove from 'device' or 'user' (defaults to 'user')</param>
		public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user)
		{
			CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(scope, "state"));
		}

		/// <summary>
		/// Remove the session data stored under the given name
		/// </summary>
		/// <param name="name">The name of the entry to remove</param>
		/// <param name="scope">Remove from 'device' or 'user' (defaults to 'user')</param>
		public static void StorageRemoveEntry(string name, StorageScope scope = StorageScope.user)
		{
			CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(scope, name));
		}

		/// <summary>
		/// Remove all the session data stored on the device or for the current user
		/// </summary>
		/// <param name="scope">Remove all from 'device' or 'user' (defaults to 'user')</param>
		public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user)
		{
			CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(scope));
		}

		/// <summary>
		/// Send a prompt to the LLM provider
		/// StartCoroutine(AIProxy(prompt, llmProvider, result => {
		///	    Debug.Log("Result: " + result);
		/// }));
		/// </summary>
		/// <param name="prompt">The prompt to send</param>
		/// <param name="llmProvider">The LLM being used</param>
		/// <param name="callback">Return value when finished</param>
		/// <returns>The string returned by the LLM</returns>
		public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback)
		{
			yield return AIProxyApi.SendPrompt(prompt, llmProvider, null, callback);
		}

		///  <summary>
		///  Send a prompt to the LLM provider
		///  StartCoroutine(AIProxy(prompt, llmProvider, result => {
		/// 	    Debug.Log("Result: " + result);
		///  }));
		///  </summary>
		///  <param name="prompt">The prompt to send</param>
		///  <param name="pastMessages">Previous messages sent to the LLM</param>
		///  <param name="llmProvider">The LLM being used</param>
		///  <param name="callback">Return value when finished</param>
		///  <returns>The string returned by the LLM</returns>
		public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback)
		{
			yield return AIProxyApi.SendPrompt(prompt, llmProvider, pastMessages, callback);
		}

		// Event wrapper functions
		public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "assessment";
			meta["verb"] = "started";
			AssessmentStartTimes[assessmentName] = DateTime.UtcNow;
			Event(assessmentName, meta);
		}
		public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) =>
			EventAssessmentComplete(assessmentName, int.Parse(score), ToEventStatus(result), meta);  // just here for backwards compatibility
		public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "assessment";
			meta["verb"] = "completed";
			meta["score"] = score.ToString();
			meta["status"] = status.ToString();
			AddDuration(AssessmentStartTimes, assessmentName, meta);
			Event(assessmentName, meta);
			CoroutineRunner.Instance.StartCoroutine(EventBatcher.Send());
		}
	
		public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "objective";
			meta["verb"] = "started";
			ObjectiveStartTimes[objectiveName] = DateTime.UtcNow;
			Event(objectiveName, meta);
		}
		public static void EventObjectiveComplete(string objectiveName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) =>
			EventObjectiveComplete(objectiveName, int.Parse(score), ToEventStatus(result), meta);  // just here for backwards compatibility
		public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "objective";
			meta["verb"] = "completed";
			meta["score"] = score.ToString();
			meta["status"] = status.ToString();
			AddDuration(ObjectiveStartTimes, objectiveName, meta);
			Event(objectiveName, meta);
		}
	
		public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "interaction";
			meta["verb"] = "started";
			InteractionStartTimes[interactionName] = DateTime.UtcNow;
			Event(interactionName, meta);
		}
		public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) =>
			EventInteractionComplete(interactionName, interactionType, result, meta); // Just here for backwards compatability
		public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "interaction";
			meta["verb"] = "completed";
			meta["interaction"] =  interactionType.ToString();
			if (!string.IsNullOrEmpty(response)) meta["response"] = response;
			AddDuration(InteractionStartTimes, interactionName, meta);
			Event(interactionName, meta);
		}

		public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["verb"] = "started";
			meta["id"] = levelName;
			LevelStartTimes[levelName] = DateTime.UtcNow;
			Event("level_start", meta);
		}
		public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["verb"] = "completed";
			meta["id"] = levelName;
			meta["score"] = score;
			AddDuration(LevelStartTimes, levelName, meta);
			Event("level_complete", meta);
		}

		public static void EventCritical(string label, Dictionary<string, string> meta = null)
		{
			string taggedName = $"CRITICAL_ABXR_{label}";
			Event(taggedName, meta);
		}

		// ---
		public static void PresentKeyboard(string promptText = null, string keyboardType = null, string emailDomain = null)
		{
			if (keyboardType is "text" or null)
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
				KeyboardHandler.SetPrompt(promptText ?? "Please Enter Your Login");
			}
			else if (keyboardType == "assessmentPin")
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);
				KeyboardHandler.SetPrompt(promptText ?? "Enter your 6-digit PIN");
			}
			else if (keyboardType == "email")
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
				KeyboardHandler.SetPrompt(promptText != null ?
					$"{promptText} (<u>username</u>@{emailDomain})" : 
					$"Enter your email username (<u>username</u>@{emailDomain})");
			}
		}

		/// <summary>
		/// Get feedback from the user with a Poll
		/// </summary>
		/// <param name="prompt">The question being asked</param>
		/// <param name="pollType">What kind of poll would you like</param>
		/// <param name="responses">If a multiple choice poll, you need to provide between 2 and 8 possible responses</param>
		/// <param name="callback">Optional callback that will be called with the selected string value (Multiple-choice poll only)</param>
		public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null)
		{
			if (pollType == ExitPollHandler.PollType.MultipleChoice)
			{
				if (responses == null)
				{
					Debug.LogError("AbxrLib - List of responses required for multiple choice poll");
					return;
				}

				if (responses.Count is < 2 or > 8)
				{
					Debug.LogError("AbxrLib - Multiple choice poll must have at least two and no more than 8 responses");
					return;
				}
			}
		
			ExitPollHandler.AddPoll(prompt, pollType, responses, callback);
		}

		public static void ReAuthenticate()
		{
			CoroutineRunner.Instance.StartCoroutine(Authentication.Authentication.Authenticate());
		}

		public static void StartNewSession()
		{
			Authentication.Authentication.SetSessionId(Guid.NewGuid().ToString());
			CoroutineRunner.Instance.StartCoroutine(Authentication.Authentication.Authenticate());
		}

		public static void ContinueSession(string sessionId)
		{
			Authentication.Authentication.SetSessionId(sessionId);
			CoroutineRunner.Instance.StartCoroutine(Authentication.Authentication.Authenticate());
		}
	
		private static void AddDuration(Dictionary<string, DateTime> startTimes, string name, Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			if (startTimes.ContainsKey(name))
			{
				double duration = (DateTime.UtcNow - startTimes[name]).TotalSeconds; //TODO do we want seconds?
				meta["duration"] = duration.ToString();
				startTimes.Remove(name);
			}
			else
			{
				meta["duration"] = "0";
			}
		}

		/// <summary>Gets the UUID assigned to device by ArborXR.</summary>
		/// <returns>UUID is provided as a string.</returns>
		public static string GetDeviceId() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetDeviceId() : "";

		/// <summary>Gets the serial number assigned to device by OEM.</summary>
		/// <returns>Serial number is provided as a string.</returns>
		public static string GetDeviceSerial() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetDeviceSerial() : "";

		/// <summary>Gets the title given to device by admin through the ArborXR Web Portal.</summary>
		public static string GetDeviceTitle() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetDeviceTitle() : "";

		/// <summary>Gets the tags added to device by admin through the ArborXR Web Portal.</summary>
		/// <returns>
		///   Tags are represented as a string array. Array will be empty if no tags are assigned to device.
		/// </returns>
		public static string[] GetDeviceTags() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetDeviceTags() : null;

		/// <summary>
		///   Gets the UUID of the organization where the device is assigned. Organizations are created in the
		///   ArborXR Web Portal.
		/// </summary>
		/// <returns>UUID is provided as a string.</returns>
		public static string GetOrgId() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetOrgId() : Configuration.Instance.orgID;

		/// <summary>Gets the name assigned to organization by admin through the ArborXR Web Portal.</summary>
		public static string GetOrgTitle() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetOrgTitle() : "";

		/// <summary>Gets the identifier generated by ArborXR when admin assigns title to organization.</summary>
		public static string GetOrgSlug() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetOrgSlug() : "";

		/// <summary>Gets the physical MAC address assigned to device by OEM.</summary>
		/// <returns>MAC address is provided as a string.</returns>
		public static string GetMacAddressFixed() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetMacAddressFixed() : "";

		/// <summary>Gets the randomized MAC address for the current WiFi connection.</summary>
		/// <returns>MAC address is provided as a string.</returns>
		public static string GetMacAddressRandom() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetMacAddressRandom() : "";

		/// <summary>Gets whether the device is SSO authenticated.</summary>
		/// <returns>Whether the device is SSO authenticated.</returns>
		public static bool GetIsAuthenticated() =>
			ArborServiceClient.IsConnected() && ArborServiceClient.ServiceWrapper != null && ArborServiceClient.ServiceWrapper.GetIsAuthenticated();

		/// <summary>Gets SSO access token.</summary>
		/// <returns>SSO access token.</returns>
		public static string GetAccessToken() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetAccessToken() : "";

		/// <summary>Gets SSO refresh token.</summary>
		/// <returns>SSO refresh token.</returns>
		public static string GetRefreshToken() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetRefreshToken() : "";

		/// <summary>Gets SSO token remaining lifetime.</summary>
		/// <returns>The remaining lifetime of the access token in seconds.</returns>
		public static DateTime? GetExpiresDateUtc() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetExpiresDateUtc() : DateTime.MinValue;

		// <summary>Gets the device fingerprint.</summary>
		/// <returns>The device fingerprint.</returns>
		public static string GetFingerprint() =>
			ArborServiceClient.IsConnected() ? ArborServiceClient.ServiceWrapper?.GetFingerprint() : "";
	}
//}
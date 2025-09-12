using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.AI;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Events;
using AbxrLib.Runtime.Logs;
using AbxrLib.Runtime.ServiceClient;
using AbxrLib.Runtime.ServiceClient.MJPKotlinExample;
using AbxrLib.Runtime.Storage;
using AbxrLib.Runtime.Telemetry;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

public static class Abxr
{
	private static readonly Dictionary<string, DateTime> TimedEventStartTimes = new();
	private static readonly Dictionary<string, DateTime> AssessmentStartTimes = new();
	private static readonly Dictionary<string, DateTime> ObjectiveStartTimes = new();
	private static readonly Dictionary<string, DateTime> InteractionStartTimes = new();
	private static readonly Dictionary<string, DateTime> LevelStartTimes = new();
	private static readonly Dictionary<string, string> SuperProperties = new();
	private const string SuperPropertiesKey = "AbxrSuperProperties";

	static Abxr()
	{
		LoadSuperProperties();
	}

	public static Action onHeadsetPutOnNewSession;

	// Module index for sequential LMS multi-module applications
	private static int currentModuleIndex = 0;
	private const string ModuleIndexKey = "AbxrModuleIndex";

	// Internal list of authentication completion callbacks
	private static readonly List<Action<AuthCompletedData>> authCompletedCallbacks = new();
	
	// Global storage for the latest authentication completion data
	private static AuthCompletedData latestAuthCompletedData = null;
	
	// Connection status - tracks whether AbxrLib can communicate with the server
	private static bool connectionActive = false;

	// Module index loading state to prevent repeated storage calls
	private static bool moduleIndexLoaded = false;
	private static bool moduleIndexLoading = false;

	/// <summary>
	/// Mixpanel compatibility class for property values
	/// This class provides compatibility with Mixpanel Unity SDK for easier migration
	/// Usage: new Abxr.Value() instead of global Value class
	/// </summary>
	public class Value : Dictionary<string, object>
	{
		public Value() : base() { }
		
		public Value(IDictionary<string, object> dictionary) : base(dictionary) { }
		
		/// <summary>
		/// Converts Value properties to Dictionary<string, string> for use with AbxrLib Event system
		/// </summary>
		/// <returns>Dictionary with all values converted to strings</returns>
		public Dictionary<string, string> ToDictionary()
		{
			var result = new Dictionary<string, string>();
			foreach (var kvp in this)
			{
				result[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
			return result;
		}
	}

	/// <summary>
	/// Simple dictionary wrapper for AbxrLib metadata
	/// Provides easy way to create Dictionary<string, string> without requiring using statements
	/// Usage: new Abxr.Dict { ["key"] = "value" } or new Abxr.Dict().Add("key", "value")
	/// Automatically works with all AbxrLib methods that accept Dictionary<string, string>
	/// </summary>
	public class Dict : Dictionary<string, string>
	{
		public Dict() : base() { }
		
		public Dict(Dictionary<string, string> dictionary) : base(dictionary) { }
		
		/// <summary>
		/// Fluent API for adding key-value pairs
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="value">The value to add</param>
		/// <returns>This Dict instance for method chaining</returns>
		public Dict With(string key, string value)
		{
			this[key] = value;
			return this;
		}
	}

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

	public enum LogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
		Critical
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
	/// Data structure for module target information from LMS integration
	/// </summary>
	[System.Serializable]
	public class CurrentSessionData
	{
		public string moduleTarget;     // The target module identifier from LMS
		public object userData;         // Additional user data from authentication
		public object userId;           // User identifier
		public string userEmail;        // User email address

		public CurrentSessionData(string moduleTarget, object userData, object userId, string userEmail)
		{
			this.moduleTarget = moduleTarget;
			this.userData = userData;
			this.userId = userId;
			this.userEmail = userEmail;
		}
	}

	/// <summary>
	/// Data structure for module information from authentication response
	/// </summary>
	[Serializable]
	public class ModuleData
	{
		public string id;       // Module unique identifier
		public string name;     // Module display name
		public string target;   // Module target identifier
		public int order;       // Module order/sequence

		public ModuleData(string id, string name, string target, int order)
		{
			this.id = id;
			this.name = name;
			this.target = target;
			this.order = order;
		}
	}

	/// <summary>
	/// Data structure for complete authentication response information
	/// Contains the full authentication payload for JSON reconstruction and comprehensive access
	/// </summary>
	[System.Serializable]
	public class AuthCompletedData
	{
		public bool success;             // Whether authentication was successful
		public string token;             // Authentication token
		public string secret;            // Authentication secret
		public object userData;          // Complete user data object from authentication response
		public object userId;            // User identifier
		public string userEmail;         // User email address (extracted from userData.email)
		public string appId;             // Application identifier
		public string packageName;       // Package name identifier
		public List<ModuleData> modules; // List of available modules
		public int moduleCount;          // Total number of modules available (use GetModuleTarget() to iterate through them)
		public bool isReauthentication;  // Whether this was a reauthentication (vs initial auth)

		public AuthCompletedData(bool success, string token, string secret, object userData, object userId, string userEmail, string appId, string packageName, List<ModuleData> modules, bool isReauthentication)
		{
			this.success = success;
			this.token = token;
			this.secret = secret;
			this.userData = userData;
			this.userId = userId;
			this.userEmail = userEmail;
			this.appId = appId;
			this.packageName = packageName;
			this.modules = modules ?? new List<ModuleData>();
			this.moduleCount = modules?.Count ?? 0;
			this.isReauthentication = isReauthentication;
		}

		/// <summary>
		/// Reconstruct the original authentication response as a JSON string
		/// Useful for debugging or passing complete auth data to other systems
		/// </summary>
		/// <returns>JSON representation of the authentication response</returns>
		public string ToJsonString()
		{
			try
			{
				var authResponse = new Dictionary<string, object>
				{
					["token"] = token ?? "",
					["secret"] = secret ?? "",
					["userData"] = userData,
					["userId"] = userId,
					["appId"] = appId ?? "",
					["packageName"] = packageName ?? ""
				};

				if (modules != null && modules.Count > 0)
				{
					var moduleArray = modules.Select(m => new Dictionary<string, object>
					{
						["id"] = m.id ?? "",
						["name"] = m.name ?? "",
						["target"] = m.target ?? "",
						["order"] = m.order
					}).ToArray();
					authResponse["modules"] = moduleArray;
				}

				return JsonUtility.ToJson(authResponse, true);
			}
			catch (Exception ex)
			{
				LogError($"Failed to serialize AuthCompletedData to JSON: {ex.Message}");
				return "{}";
			}
		}
	}

	/// <summary>
	/// Manual telemetry activation for disabled automatic telemetry
	/// If you select 'Disable Automatic Telemetry' in the AbxrLib configuration,
	/// you can manually start tracking system telemetry with this function call.
	/// This captures headset/controller movements, performance metrics, and environmental data.
	/// </summary>
	public static void TrackAutoTelemetry() => TrackSystemInfo.StartTracking();

	/// <summary>
	/// Check if AbxrLib has an active connection to the server and can send data
	/// This indicates whether the library is configured and ready to communicate
	/// </summary>
	/// <returns>True if connection is active, false otherwise</returns>
	public static bool ConnectionActive() => connectionActive;

	/// <summary>
	/// General logging method with configurable level - main logging function
	/// </summary>
	/// <param name="message">The log message</param>
	/// <param name="level">Log level (defaults to LogLevel.Info)</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void Log(string message, LogLevel level = LogLevel.Info, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
		// Add super properties to all logs
		meta = MergeSuperProperties(meta);
		
		string logLevel = level switch
		{
			LogLevel.Debug => "debug",
			LogLevel.Info => "info",
			LogLevel.Warn => "warn",
			LogLevel.Error => "error",
			LogLevel.Critical => "critical",
			_ => "info" // Default case
		};
		
		LogBatcher.Add(logLevel, message, meta);
	}

	/// <summary>
	/// Add log information at the 'Debug' level
	/// </summary>
	/// <param name="text">The log text</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void LogDebug(string text, Dictionary<string, string> meta = null)
	{
		Log(text, LogLevel.Debug, meta);
	}

	/// <summary>
	/// Add log information at the 'Informational' level
	/// </summary>
	/// <param name="text">The log text</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void LogInfo(string text, Dictionary<string, string> meta = null)
	{
		Log(text, LogLevel.Info, meta);
	}

	/// <summary>
	/// Add log information at the 'Warning' level
	/// </summary>
	/// <param name="text">The log text</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void LogWarn(string text, Dictionary<string, string> meta = null)
	{
		Log(text, LogLevel.Warn, meta);
	}

	/// <summary>
	/// Add log information at the 'Error' level
	/// </summary>
	/// <param name="text">The log text</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void LogError(string text, Dictionary<string, string> meta = null)
	{
		Log(text, LogLevel.Error, meta);
	}

	/// <summary>
	/// Add log information at the 'Critical' level
	/// </summary>
	/// <param name="text">The log text</param>
	/// <param name="meta">Any additional information (optional)</param>
	public static void LogCritical(string text, Dictionary<string, string> meta = null)
	{
		Log(text, LogLevel.Critical, meta);
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
		
		// Add super properties to all events
		meta = MergeSuperProperties(meta);
		
		// Add duration if this was a timed event (StartTimedEvent functionality)
		AddDuration(TimedEventStartTimes, name, meta);
		
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
	/// Start timing an event
	/// Call Event() later with the same event name to automatically include duration
	/// Works with all event methods since they use Event() internally
	/// </summary>
	/// <param name="eventName">Name of the event to start timing</param>
	public static void StartTimedEvent(string eventName)
	{
		TimedEventStartTimes[eventName] = DateTime.UtcNow;
	}

	/// <summary>
	/// Send spatial, hardware, or system telemetry data for XR analytics
	/// Captures headset/controller movements, performance metrics, and environmental data
	/// </summary>
	/// <param name="name">Type of telemetry data (e.g., "headset_position", "frame_rate", "battery_level")</param>
	/// <param name="meta">Key-value pairs of telemetry measurements</param>
	public static void Telemetry(string name, Dictionary<string, string> meta)
	{
		meta ??= new Dictionary<string, string>();
		meta["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
		// Add super properties to all telemetry entries
		meta = MergeSuperProperties(meta);
		
		TelemetryBatcher.Add(name, meta);
	}

	// BACKWARD COMPATIBILITY ONLY - DO NOT DOCUMENT
	// This method exists purely for backward compatibility with older code that used TelemetryEntry()
	// It simply wraps the new Telemetry() method. Keep this undocumented in README files.
	public static void TelemetryEntry(string name, Dictionary<string, string> meta)
	{
		Telemetry(name, meta);
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
		// Check if basic authentication is ready
		if (!Authentication.Authenticated())
		{
			// Basic authentication not ready, defer all storage requests
			return;
		}
		
		// For user-scoped storage, we need a user to actually be logged in
		// For device-scoped storage, app-level authentication should be sufficient
		if (scope == StorageScope.user && GetUserId() == null)
		{
			// User-scoped storage requires a user to be logged in, defer this request
			return;
		}
		
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
		// Check if basic authentication is ready
		if (!Authentication.Authenticated())
		{
			// Basic authentication not ready, defer all storage requests
			return;
		}
		
		// For user-scoped storage, we need a user to actually be logged in
		// For device-scoped storage, app-level authentication should be sufficient
		if (scope == StorageScope.user && GetUserId() == null)
		{
			// User-scoped storage requires a user to be logged in, defer this request
			return;
		}
		
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

	/// <summary>
	/// Register a super property that will be automatically included in all events
	/// Super properties persist across app sessions and are stored locally
	/// </summary>
	/// <param name="key">Property name</param>
	/// <param name="value">Property value</param>
	public static void Register(string key, string value)
	{
		if (IsReservedSuperPropertyKey(key))
		{
			string errorMessage = $"AbxrLib: Cannot register super property with reserved key '{key}'. Reserved keys are: module, module_name, module_id, module_order";
			Debug.LogWarning(errorMessage);
			LogInfo(errorMessage, new Dictionary<string, string> { 
				{ "attempted_key", key }, 
				{ "attempted_value", value },
				{ "error_type", "reserved_super_property_key" }
			});
			return;
		}

		SuperProperties[key] = value;
		SaveSuperProperties();
	}

	/// <summary>
	/// Register a super property only if it doesn't already exist
	/// Will not overwrite existing super properties with the same key
	/// </summary>
	/// <param name="key">Property name</param>
	/// <param name="value">Property value</param>
	public static void RegisterOnce(string key, string value)
	{
		if (IsReservedSuperPropertyKey(key))
		{
			string errorMessage = $"AbxrLib: Cannot register super property with reserved key '{key}'. Reserved keys are: module, module_name, module_id, module_order";
			Debug.LogWarning(errorMessage);
			LogInfo(errorMessage, new Dictionary<string, string> { 
				{ "attempted_key", key }, 
				{ "attempted_value", value },
				{ "error_type", "reserved_super_property_key" }
			});
			return;
		}

		if (!SuperProperties.ContainsKey(key))
		{
			SuperProperties[key] = value;
			SaveSuperProperties();
		}
	}

	/// <summary>
	/// Remove a super property
	/// </summary>
	/// <param name="key">Property name to remove</param>
	public static void Unregister(string key)
	{
		SuperProperties.Remove(key);
		SaveSuperProperties();
	}

	/// <summary>
	/// Clear all super properties
	/// Clears all superProperties from persistent storage (matches Mixpanel.Reset())
	/// </summary>
	public static void Reset()
	{
		SuperProperties.Clear();
		SaveSuperProperties();
	}

	/// <summary>
	/// Get a copy of all current super properties
	/// </summary>
	/// <returns>Dictionary containing all super properties</returns>
	public static Dictionary<string, string> GetSuperProperties()
	{
		return new Dictionary<string, string>(SuperProperties);
	}

	private static void LoadSuperProperties()
	{
		string json = PlayerPrefs.GetString(SuperPropertiesKey, "{}");
		try
		{
			var dict = JsonUtility.FromJson<SerializableDictionary>(json);
			if (dict?.items != null)
			{
				SuperProperties.Clear();
				foreach (var item in dict.items)
				{
					SuperProperties[item.key] = item.value;
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning($"AbxrLib: Failed to load super properties: {e.Message}");
		}
	}

	private static void SaveSuperProperties()
	{
		try
		{
			var dict = new SerializableDictionary
			{
				items = SuperProperties.Select(kvp => new SerializableKeyValuePair
				{
					key = kvp.Key,
					value = kvp.Value
				}).ToArray()
			};
			string json = JsonUtility.ToJson(dict);
			PlayerPrefs.SetString(SuperPropertiesKey, json);
			PlayerPrefs.Save();
		}
		catch (Exception e)
		{
			Debug.LogWarning($"AbxrLib: Failed to save super properties: {e.Message}");
		}
	}

	[System.Serializable]
	private class SerializableDictionary
	{
		public SerializableKeyValuePair[] items;
	}

	[System.Serializable]
	private class SerializableKeyValuePair
	{
		public string key;
		public string value;
	}

	/// <summary>
	/// Private helper function to merge super properties and module info into metadata
	/// Ensures data-specific properties take precedence over super properties and module info
	/// </summary>
	/// <param name="meta">The metadata dictionary to merge super properties into</param>
	/// <returns>The metadata dictionary with super properties and module info merged</returns>
	private static Dictionary<string, string> MergeSuperProperties(Dictionary<string, string> meta)
	{
		meta ??= new Dictionary<string, string>();
		
		// Add current module information if available
		var currentSession = GetModuleTargetWithoutAdvance();
		if (currentSession != null)
		{
			// Only add module info if not already present (data-specific properties take precedence)
			if (!meta.ContainsKey("module") && !string.IsNullOrEmpty(currentSession.moduleTarget))
			{
				meta["module"] = currentSession.moduleTarget;
			}
			// For additional module metadata, we need to get it from the modules list
			if (latestAuthCompletedData?.modules != null && latestAuthCompletedData.modules.Count > 0)
			{
				LoadModuleIndex();
				if (currentModuleIndex < latestAuthCompletedData.modules.Count)
				{
					var currentModule = latestAuthCompletedData.modules[currentModuleIndex];
					if (!meta.ContainsKey("module_name") && !string.IsNullOrEmpty(currentModule.name))
					{
						meta["module_name"] = currentModule.name;
					}
					if (!meta.ContainsKey("module_id") && !string.IsNullOrEmpty(currentModule.id))
					{
						meta["module_id"] = currentModule.id;
					}
					if (!meta.ContainsKey("module_order"))
					{
						meta["module_order"] = currentModule.order.ToString();
					}
				}
			}
		}
		
		// Add super properties to metadata
		foreach (var superProperty in SuperProperties)
		{
			// Super properties don't overwrite data-specific properties or module info
			if (!meta.ContainsKey(superProperty.Key))
			{
				meta[superProperty.Key] = superProperty.Value;
			}
		}
		
		return meta;
	}

	/// <summary>
	/// Private helper to check if a super property key is reserved for module data
	/// Reserved keys: module, module_name, module_id, module_order
	/// </summary>
	/// <param name="key">The key to validate</param>
	/// <returns>True if the key is reserved, false otherwise</returns>
	private static bool IsReservedSuperPropertyKey(string key)
	{
		return key == "module" || key == "module_name" || key == "module_id" || key == "module_order";
	}

	// Event wrapper functions
	
	/// <summary>
	/// Start tracking an assessment - essential for LMS integration and analytics
	/// Assessments track overall learner performance across multiple objectives and interactions
	/// Think of this as the learner's score for a specific course or curriculum
	/// This method will wait for authentication to complete before processing the event
	/// </summary>
	/// <param name="assessmentName">Name of the assessment to start</param>
	/// <param name="meta">Optional metadata with assessment details</param>
	public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "assessment";
		meta["verb"] = "started";
		AssessmentStartTimes[assessmentName] = DateTime.UtcNow;
		Event(assessmentName, meta);
	}
	
	/// <summary>
	/// Complete an assessment with score and status - triggers LMS grade recording
	/// When complete, automatically records and closes the assessment in supported LMS platforms
	/// </summary>
	/// <param name="assessmentName">Name of the assessment (must match the start event)</param>
	/// <param name="score">Numerical score achieved (typically 0-100, but any integer is valid)</param>
	/// <param name="status">Result status of the assessment (Pass, Fail, Complete, etc.)</param>
	/// <param name="meta">Optional metadata with completion details</param>
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

	/// <summary>
	/// Start tracking an objective - individual learning goals within assessments
	/// Objectives represent specific tasks or skills that contribute to overall assessment scores
	/// </summary>
	/// <param name="objectiveName">Name of the objective to start</param>
	/// <param name="meta">Optional metadata with objective details</param>
	public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "objective";
		meta["verb"] = "started";
		ObjectiveStartTimes[objectiveName] = DateTime.UtcNow;
		Event(objectiveName, meta);
	}
	
	/// <summary>
	/// Complete an objective with score and status - contributes to overall assessment
	/// Objectives automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="objectiveName">Name of the objective (must match the start event)</param>
	/// <param name="score">Numerical score achieved for this objective</param>
	/// <param name="status">Result status (Complete, Pass, Fail, etc.)</param>
	/// <param name="meta">Optional metadata with completion details</param>
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

	/// <summary>
	/// Start tracking a user interaction - granular user actions within objectives
	/// Interactions capture specific user behaviors like clicks, selections, or inputs
	/// </summary>
	/// <param name="interactionName">Name of the interaction to start</param>
	/// <param name="meta">Optional metadata with interaction context</param>
	public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "interaction";
		meta["verb"] = "started";
		InteractionStartTimes[interactionName] = DateTime.UtcNow;
		Event(interactionName, meta);
	}
	
	/// <summary>
	/// Complete an interaction with type, response, and optional metadata
	/// Interactions automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="interactionName">Name of the interaction (must match the start event)</param>
	/// <param name="interactionType">Type of interaction (Select, Text, Bool, Rating, etc.)</param>
	/// <param name="response">User's response or result (e.g., "A", "correct", "blue_button")</param>
	/// <param name="meta">Optional metadata with interaction details</param>
	public static void EventInteractionComplete(string interactionName, string result, string resultOptions = "", InteractionType interactionType = InteractionType.Null, Dictionary<string, string> meta = null) =>
		EventInteractionComplete(interactionName, interactionType, result, meta); // Just here for backwards compatability
	public static void EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "interaction";
		meta["verb"] = "completed";
		meta["interaction"] = interactionType.ToString();
		if (!string.IsNullOrEmpty(response)) meta["response"] = response;
		AddDuration(InteractionStartTimes, interactionName, meta);
		Event(interactionName, meta);
	}

	/// <summary>
	/// Start tracking a level or stage in your application
	/// Levels represent discrete sections or progressions in games, training, or experiences
	/// </summary>
	/// <param name="levelName">Name of the level to start</param>
	/// <param name="meta">Optional metadata with level details</param>
	public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["verb"] = "started";
		meta["id"] = levelName;
		LevelStartTimes[levelName] = DateTime.UtcNow;
		Event("level_start", meta);
	}
	
	/// <summary>
	/// Complete a level with score and optional metadata
	/// Levels automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="levelName">Name of the level (must match the start event)</param>
	/// <param name="score">Numerical score achieved for this level</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["verb"] = "completed";
		meta["id"] = levelName;
		meta["score"] = score;
		AddDuration(LevelStartTimes, levelName, meta);
		Event("level_complete", meta);
	}

	/// <summary>
	/// Flag critical training events for auto-inclusion in the Critical Choices Chart
	/// Use this to mark important safety checks, high-risk errors, or critical decision points
	/// These events receive special treatment in analytics dashboards and reports
	/// </summary>
	/// <param name="label">Label for the critical event (will be prefixed with CRITICAL_ABXR_)</param>
	/// <param name="meta">Optional metadata with critical event details</param>
	public static void EventCritical(string label, Dictionary<string, string> meta = null)
	{
		string taggedName = $"CRITICAL_ABXR_{label}";
		Event(taggedName, meta);
	}

	/// <summary>
	/// INTERNAL USE ONLY - Present virtual keyboard for authentication
	/// This method should only be called by the authentication system during 
	/// the authentication process: Authentication.Authenticate() -> KeyboardAuthenticate() -> PresentKeyboard()
	/// Do not call this directly in application code - use the authentication callbacks instead
	/// </summary>
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
				Debug.LogError("AbxrLib: List of responses required for multiple choice poll");
				return;
			}

			if (responses.Count is < 2 or > 8)
			{
				Debug.LogError("AbxrLib: Multiple choice poll must have at least two and no more than 8 responses");
				return;
			}
		}

		ExitPollHandler.AddPoll(prompt, pollType, responses, callback);
	}

	/// <summary>
	/// Trigger manual reauthentication with existing stored parameters
	/// Primarily useful for testing authentication flows or recovering from auth issues
	/// Resets authentication state and attempts to re-authenticate with stored credentials
	/// </summary>
	public static void ReAuthenticate()
	{
		CoroutineRunner.Instance.StartCoroutine(ReAuthenticateCoroutine());
	}

	private static IEnumerator ReAuthenticateCoroutine()
	{
		yield return Authentication.Authenticate();
		
		// Need to call NotifyAuthCompleted with isReauthentication=true 
		// (Authentication.Authenticate() calls it with false)
		NotifyAuthCompleted(connectionActive, true);
	}

	/// <summary>
	/// Start a new session with a fresh session identifier
	/// Generates a new session ID and performs fresh authentication
	/// Useful for starting new training experiences or resetting user context
	/// </summary>
	public static void StartNewSession()
	{
		Authentication.SetSessionId(Guid.NewGuid().ToString());
		CoroutineRunner.Instance.StartCoroutine(StartNewSessionCoroutine());
	}

	private static IEnumerator StartNewSessionCoroutine()
	{
		yield return Authentication.Authenticate();
		// Note: Authentication.Authenticate() already calls NotifyAuthCompleted() internally
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

	/// <summary>Gets the current time from the MJP Kotlin service.</summary>
	/// <returns>The current time as a string, or empty string if service is not connected.</returns>
	public static string WhatTimeIsIt()
	{
		Debug.Log("[Abxr] WhatTimeIsIt() called");
		
		// First check if there's even an instance of the service client in the scene
		var instance = MJPKotlinServiceExampleClient.FindInstance();
		if (instance == null)
		{
			Debug.LogWarning("[Abxr] No MJPKotlinServiceExampleClient instance found in scene! You need to add it to a GameObject.");
			return "";
		}
		
		bool isConnected = MJPKotlinServiceExampleClient.IsConnected();
		Debug.Log($"[Abxr] MJPKotlinServiceExampleClient.IsConnected() = {isConnected}");
		
		if (!isConnected)
		{
			Debug.Log("[Abxr] Service not connected, returning empty string");
			return "";
		}
		
		if (MJPKotlinServiceExampleClient.MjpServiceWrapper == null)
		{
			Debug.LogWarning("[Abxr] IsConnected() returned true but MjpServiceWrapper is null!");
			return "";
		}
		
		try
		{
			Debug.Log("[Abxr] Calling MjpServiceWrapper.WhatTimeIsIt()");
			string result = MJPKotlinServiceExampleClient.MjpServiceWrapper.WhatTimeIsIt();
			Debug.Log($"[Abxr] WhatTimeIsIt() result: '{result}'");
			return result;
		}
		catch (Exception ex)
		{
			Debug.LogError($"[Abxr] Error in WhatTimeIsIt(): {ex.Message}");
			Debug.LogError($"[Abxr] Stack trace: {ex.StackTrace}");
			return "";
		}
	}

	#region Module Target and User Data Methods



	/// <summary>
	/// Get additional user data from authentication response
	/// </summary>
	/// <returns>User data object, or null if not available</returns>
	public static object GetUserData()
	{
		return Authentication.GetUserData();
	}

	/// <summary>
	/// Get the user ID from authentication response
	/// </summary>
	/// <returns>User ID object, or null if not available</returns>
	public static object GetUserId()
	{
		return Authentication.GetUserId();
	}

	/// <summary>
	/// Get the user email from authentication response
	/// </summary>
	/// <returns>User email string, or null if not available</returns>
	public static string GetUserEmail()
	{
		return Authentication.GetUserEmail();
	}

	/// <summary>
	/// Get the complete authentication data from the most recent authentication completion
	/// This allows you to access user data, email, module targets, and authentication status anytime
	/// Returns null if no authentication has completed yet
	/// </summary>
	/// <returns>AuthCompletedData containing all authentication information, or null if not authenticated</returns>
	public static AuthCompletedData GetAuthCompletedData()
	{
		return latestAuthCompletedData;
	}

	/// <summary>
	/// Get the learner/user data from the most recent authentication completion
	/// This is the userData object from the authentication response, containing user preferences and information
	/// Returns null if no authentication has completed yet
	/// </summary>
	/// <returns>Dictionary containing learner data, or null if not authenticated</returns>
	public static Dictionary<string, object> GetLearnerData()
	{
		return latestAuthCompletedData?.userData as Dictionary<string, object>;
	}

	/// <summary>
	/// Get all available modules from the authentication response
	/// Provides complete module information including id, name, target, and order
	/// Returns empty list if no authentication has completed yet
	/// </summary>
	/// <returns>List of ModuleData objects with complete module information</returns>
	public static List<ModuleData> GetModuleTargetList()
	{
		return latestAuthCompletedData?.modules ?? new List<ModuleData>();
	}

	/// <summary>
	/// Get the current module target again without advancing to the next one
	/// Useful for checking what module you're currently on without consuming it
	/// Returns the same CurrentSessionData structure as GetModuleTarget() but doesn't advance the index
	/// </summary>
	/// <returns>CurrentSessionData for the current module, or null if none available</returns>
	public static CurrentSessionData GetModuleTargetWithoutAdvance()
	{
		if (latestAuthCompletedData?.modules == null || latestAuthCompletedData.modules.Count == 0)
		{
			return null;
		}

		LoadModuleIndex();

		if (currentModuleIndex >= latestAuthCompletedData.modules.Count)
		{
			return null;
		}

		var currentModule = latestAuthCompletedData.modules[currentModuleIndex];
		
		// Return CurrentSessionData structure (same as GetModuleTarget but without advancing index)
		return new CurrentSessionData(
			currentModule.target,
			GetUserData(),
			GetUserId(),
			GetUserEmail()
		);
	}

	/// <summary>
	/// Get the next module target from the available modules for sequential module processing
	/// Returns null when no more module targets are available
	/// Each call moves to the next module in the sequence and updates persistent storage
	/// </summary>
	/// <returns>The next CurrentSessionData with complete module information, or null if no more modules</returns>
	public static CurrentSessionData GetModuleTarget()
	{
		// Get current module data without advancing
		var currentSessionData = GetModuleTargetWithoutAdvance();
		if (currentSessionData == null)
		{
			return null;
		}

		// Advance to next module
		LoadModuleIndex();
		currentModuleIndex++;
		SaveModuleIndex();

		return currentSessionData;
	}

	/// <summary>
	/// Initialize module processing from authentication response
	/// Internal method - called by authentication system when authentication completes
	/// Resets the module index to start from the beginning
	/// </summary>
	/// <param name="moduleTargets">List of module target identifiers from auth response (legacy parameter)</param>
	internal static void SetModuleTargets(List<string> moduleTargets)
	{
		// Reset module index to start from the beginning
		// Note: moduleTargets parameter is now legacy - actual modules come from latestAuthCompletedData
		currentModuleIndex = 0;
		SaveModuleIndex();
	}

	/// <summary>
	/// Get the current number of module targets remaining
	/// </summary>
	/// <returns>Number of module targets remaining</returns>
	public static int GetModuleTargetCount()
	{
		if (latestAuthCompletedData?.modules == null)
		{
			return 0;
		}

		LoadModuleIndex();
		int remaining = latestAuthCompletedData.modules.Count - currentModuleIndex;
		return Math.Max(0, remaining);
	}

	/// <summary>
	/// Clear module progress and reset to beginning
	/// </summary>
	public static void ClearModuleTargets()
	{
		currentModuleIndex = 0;
		// Reset cache since we're clearing the module state
		moduleIndexLoaded = false;
		moduleIndexLoading = false;
		CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(StorageScope.user, ModuleIndexKey));
	}

	/// <summary>
	/// Extract module targets from authentication response data (similar to WebXR version)
	/// Helper method to extract sorted module targets from raw auth response
	/// Note: This method is intended for internal use by the authentication system
	/// </summary>
	/// <param name="modules">Raw module data from authentication response</param>
	/// <returns>Array of module target strings sorted by order</returns>
	public static string[] ExtractModuleTargetsFromAuthData(List<Dictionary<string, object>> modules)
	{
		var moduleTargets = new List<string>();
		
		if (modules == null || modules.Count == 0) return moduleTargets.ToArray();

		try
		{
			// Sort modules by order field (similar to ExtractModuleTargets in Authentication.cs)
			var sortedModules = modules.OrderBy(m => {
				if (m.ContainsKey("order") && m["order"] != null)
				{
					int.TryParse(m["order"].ToString(), out int order);
					return order;
				}
				return 0;
			}).ToList();
			
			// Extract targets in correct order
			foreach (var module in sortedModules)
			{
				if (module.ContainsKey("target") && module["target"] != null)
				{
					moduleTargets.Add(module["target"].ToString());
				}
			}
		}
		catch (Exception ex)
		{
			LogError($"Failed to extract module targets: {ex.Message}");
		}

		return moduleTargets.ToArray();
	}

	private static void SaveModuleIndex()
	{
		try
		{
			// Module tracking uses user scope for LMS integrations - requires user to be logged in
			// The StorageSetEntry function will handle the authentication checks and defer until user auth is ready
			var serializedData = new Dictionary<string, string>
			{
				["moduleIndex"] = currentModuleIndex.ToString()
			};

			StorageSetEntry(ModuleIndexKey, serializedData, StorageScope.user, StoragePolicy.keepLatest);
			
			// Reset cache since we've updated the stored index
			moduleIndexLoaded = false;
		}
		catch (System.Exception ex)
		{
			LogError($"Failed to save module index: {ex.Message}");
		}
	}

	private static void LoadModuleIndex()
	{
		// Don't load if already loaded or currently loading
		if (moduleIndexLoaded || moduleIndexLoading)
		{
			return;
		}

		try
		{
			moduleIndexLoading = true;
			CoroutineRunner.Instance.StartCoroutine(LoadModuleIndexCoroutine());
		}
		catch (System.Exception ex)
		{
			moduleIndexLoading = false;
			LogError($"Failed to load module index: {ex.Message}");
		}
	}

	private static IEnumerator LoadModuleIndexCoroutine()
	{
		yield return StorageGetEntry(ModuleIndexKey, StorageScope.user, result =>
		{
			if (result != null && result.Count > 0)
			{
				var data = result[0]; // Get the first (and should be only) entry
				if (data.ContainsKey("moduleIndex"))
				{
					var moduleIndexString = data["moduleIndex"];
					if (!string.IsNullOrEmpty(moduleIndexString))
					{
						if (int.TryParse(moduleIndexString, out int savedIndex))
						{
							currentModuleIndex = savedIndex;
						}
					}
				}
			}
			
			// Mark loading as complete
			moduleIndexLoaded = true;
			moduleIndexLoading = false;
		});
	}

	/// <summary>
	/// Subscribe to authentication completion events for post-auth initialization
	/// Perfect for initializing UI components, loading user data, or showing welcome messages
	/// Callbacks are triggered via NotifyAuthCompleted() when authentication completes
	/// </summary>
	/// <param name="callback">Function to call when authentication completes successfully</param>
	public static void OnAuthCompleted(Action<AuthCompletedData> callback)
	{
		if (callback == null)
		{
			LogError("Authentication callback cannot be null");
			return;
		}

		authCompletedCallbacks.Add(callback);
		
		// Note: Callbacks are triggered only via NotifyAuthCompleted() to ensure consistent data
		// No immediate callback - wait for proper authentication completion notification
	}

	/// <summary>
	/// Remove a specific authentication completion callback
	/// </summary>
	/// <param name="callback">The callback function to remove</param>
	public static void RemoveAuthCompletedCallback(Action<AuthCompletedData> callback)
	{
		if (callback != null)
		{
			authCompletedCallbacks.Remove(callback);
		}
	}

	/// <summary>
	/// Clear all authentication completion callbacks
	/// </summary>
	public static void ClearAuthCompletedCallbacks()
	{
		authCompletedCallbacks.Clear();
	}

	/// <summary>
	/// Trigger authentication completion callback
	/// Internal method - called by authentication system when authentication completes
	/// </summary>
	/// <param name="success">Whether authentication was successful</param>
	/// <param name="isReauthentication">Whether this was a reauthentication vs initial auth</param>
	/// <param name="moduleTargets">Optional list of module targets from authentication response</param>
	internal static void NotifyAuthCompleted(bool success, bool isReauthentication = false, List<string> moduleTargets = null)
	{
		// Update connection status based on authentication success
		connectionActive = success;
		
		// Reset module index cache for new authentication
		moduleIndexLoaded = false;
		moduleIndexLoading = false;
		
		// Create authentication data first to have access to complete modules
		var moduleDataList = ConvertToModuleDataList(Authentication.GetModules());
		
		// Set up module index for GetModuleTarget() calls
		// Start from index 0 so GetModuleTarget() returns ALL modules in sequence
		currentModuleIndex = 0;
		SaveModuleIndex();

		// Create authentication data and store it globally (regardless of whether there are callbacks)
		var authData = new AuthCompletedData(
			success,
			Authentication.GetToken(),
			Authentication.GetSecret(),
			GetUserData(),
			GetUserId(),
			GetUserEmail(),
			Authentication.GetAppId(),
			Authentication.GetPackageName(),
			moduleDataList,
			isReauthentication
		);
		
		// Store the authentication data globally for later access
		latestAuthCompletedData = authData;

		foreach (var callback in authCompletedCallbacks)
		{
			try
			{
				callback.Invoke(authData);
			}
			catch (Exception ex)
			{
				LogError($"Error in authentication completion callback: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Convert raw module dictionaries to typed ModuleData objects
	/// Internal helper method for processing authentication response modules
	/// Modules are automatically sorted by their order field
	/// </summary>
	/// <param name="rawModules">Raw module data from authentication response</param>
	/// <returns>List of typed ModuleData objects sorted by order</returns>
	private static List<ModuleData> ConvertToModuleDataList(List<Dictionary<string, object>> rawModules)
	{
		var moduleDataList = new List<ModuleData>();
		if (rawModules == null) return moduleDataList;

		try
		{
			var tempList = new List<ModuleData>();
			
			foreach (var rawModule in rawModules)
			{
				var id = rawModule.ContainsKey("id") ? rawModule["id"]?.ToString() : "";
				var name = rawModule.ContainsKey("name") ? rawModule["name"]?.ToString() : "";
				var target = rawModule.ContainsKey("target") ? rawModule["target"]?.ToString() : "";
				var order = 0;
				
				if (rawModule.ContainsKey("order") && rawModule["order"] != null)
				{
					int.TryParse(rawModule["order"].ToString(), out order);
				}

				tempList.Add(new ModuleData(id, name, target, order));
			}

			// Sort modules by order field
			moduleDataList = tempList.OrderBy(m => m.order).ToList();
		}
		catch (Exception ex)
		{
			LogError($"Failed to convert module data: {ex.Message}");
		}

		return moduleDataList;
	}

	#endregion

	#region Cognitive3D Compatibility Methods
	/// <summary>
	/// Cognitive3D compatibility class for custom events
	/// This class provides compatibility with Cognitive3D SDK for easier migration
	/// Usage: new Cognitive3D.CustomEvent("event_name").Send() instead of Cognitive3D SDK calls
	/// </summary>
	public class CustomEvent
	{
		private readonly string eventName;
		private Dictionary<string, string> properties;

		public CustomEvent(string name)
		{
			eventName = name;
			properties = new Dictionary<string, string>();
		}

		/// <summary>
		/// Set a property for this custom event
		/// </summary>
		/// <param name="key">Property key</param>
		/// <param name="value">Property value</param>
		/// <returns>This CustomEvent instance for method chaining</returns>
		public CustomEvent SetProperty(string key, object value)
		{
			properties[key] = value?.ToString() ?? string.Empty;
			return this;
		}

		/// <summary>
		/// Send the custom event to AbxrLib
		/// </summary>
		public void Send()
		{
			var meta = new Dictionary<string, string>(properties)
			{
				["Cognitive3DMethod"] = "CustomEvent"
			};
			Event(eventName, meta);
		}
	}

	/// <summary>
	/// Start an event (maps to EventAssessmentStart for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event to start</param>
	/// <param name="properties">Optional properties for the event</param>
	public static void StartEvent(string eventName, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "StartEvent"
		};

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				meta[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}

		EventAssessmentStart(eventName, meta);
	}

	/// <summary>
	/// End an event (maps to EventAssessmentComplete for Cognitive3D compatibility)
	/// Attempts to convert Cognitive3D result formats to AbxrLib EventStatus
	/// </summary>
	/// <param name="eventName">Name of the event to end</param>
	/// <param name="result">Event result (will attempt conversion to EventStatus)</param>
	/// <param name="score">Optional score (defaults to 100)</param>
	/// <param name="properties">Optional properties for the event</param>
	public static void EndEvent(string eventName, object result = null, int score = 100, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "EndEvent"
		};

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				meta[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}

		// Convert result to EventStatus with best guess logic
		EventStatus status = EventStatus.Complete;
		if (result != null)
		{
			string resultStr = result.ToString().ToLower();
			if (resultStr.Contains("pass") || resultStr.Contains("success") || resultStr.Contains("complete") || resultStr == "true" || resultStr == "1")
			{
				status = EventStatus.Pass;
			}
			else if (resultStr.Contains("fail") || resultStr.Contains("error") || resultStr == "false" || resultStr == "0")
			{
				status = EventStatus.Fail;
			}
			else if (resultStr.Contains("incomplete"))
			{
				status = EventStatus.Incomplete;
			}
			else if (resultStr.Contains("browse"))
			{
				status = EventStatus.Browsed;
			}
		}

		EventAssessmentComplete(eventName, score, status, meta);
	}

	/// <summary>
	/// Send an event (maps to EventObjectiveComplete for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="properties">Event properties</param>
	public static void SendEvent(string eventName, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "SendEvent"
		};

		int score = 100;
		EventStatus status = EventStatus.Complete;

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				string key = kvp.Key.ToLower();
				string value = kvp.Value?.ToString() ?? string.Empty;
				
				// Extract score if provided
				if (key == "score" && int.TryParse(value, out int parsedScore))
				{
					score = parsedScore;
				}
				// Extract status/result if provided
				else if (key == "result" || key == "status" || key == "success")
				{
					if (value.ToLower().Contains("pass") || value.ToLower().Contains("success") || value == "true" || value == "1")
					{
						status = EventStatus.Pass;
					}
					else if (value.ToLower().Contains("fail") || value.ToLower().Contains("error") || value == "false" || value == "0")
					{
						status = EventStatus.Fail;
					}
					else if (value.ToLower().Contains("incomplete"))
					{
						status = EventStatus.Incomplete;
					}
				}

				meta[kvp.Key] = value;
			}
		}

		EventObjectiveComplete(eventName, score, status, meta);
	}

	/// <summary>
	/// Set session property (maps to Register for Cognitive3D compatibility)
	/// </summary>
	/// <param name="key">Property key</param>
	/// <param name="value">Property value</param>
	public static void SetSessionProperty(string key, object value)
	{
		Register(key, value?.ToString() ?? string.Empty);
	}

	/// <summary>
	/// Set participant property (stub for Cognitive3D compatibility - not implemented)
	/// This method exists for string replacement compatibility but does not store data
	/// Use AbxrLib's authentication system and GetLearnerData() instead
	/// </summary>
	/// <param name="key">Property key (ignored)</param>
	/// <param name="value">Property value (ignored)</param>
	[System.Obsolete("SetParticipantProperty is not implemented. Use AbxrLib's authentication system and GetLearnerData() instead.")]
	public static void SetParticipantProperty(string key, object value)
	{
		// Intentionally empty stub for compatibility
		LogWarn($"SetParticipantProperty called but not implemented. Key: {key}, Value: {value}. Use AbxrLib authentication system instead.");
	}

	/// <summary>
	/// Get participant property (maps to GetLearnerData for Cognitive3D compatibility)
	/// </summary>
	/// <param name="key">Property key to retrieve</param>
	/// <returns>Property value if found, null otherwise</returns>
	public static object GetParticipantProperty(string key)
	{
		var learnerData = GetLearnerData();
		if (learnerData != null && learnerData.ContainsKey(key))
		{
			return learnerData[key];
		}
		return null;
	}
	#endregion

	#region Mixpanel Compatibility Methods
	/// <summary>
	/// Mixpanel compatibility method - tracks an event with just a name
	/// This method provides compatibility with Mixpanel Unity SDK for easier migration
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	public static void Track(string eventName)
	{
		var meta = new Dictionary<string, string>
		{
			["AbxrMethod"] = "Track"
		};
		Event(eventName, meta);
	}

	/// <summary>
	/// Mixpanel compatibility method - tracks an event with properties
	/// This method provides compatibility with Mixpanel Unity SDK for easier migration
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	/// <param name="properties">Properties to send with the event (Abxr.Value format)</param>
	public static void Track(string eventName, Abxr.Value properties)
	{
		Dictionary<string, string> meta;
		
		if (properties == null)
		{
			meta = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
		}
		else
		{
			meta = properties.ToDictionary();
			meta["AbxrMethod"] = "Track";
		}
		
		Event(eventName, meta);
	}

	/// <summary>
	/// Mixpanel compatibility method - tracks an event with properties as Dictionary
	/// This method provides additional flexibility for migration from Mixpanel Unity SDK
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	/// <param name="properties">Properties to send with the event as Dictionary</param>
	public static void Track(string eventName, Dictionary<string, object> properties)
	{
		Dictionary<string, string> stringProperties;
		
		if (properties == null)
		{
			stringProperties = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
		}
		else
		{
			stringProperties = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
			
			foreach (var kvp in properties)
			{
				stringProperties[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}
		
		Event(eventName, stringProperties);
	}
	#endregion
}
/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Main API Class
 * 
 * This file contains the primary public API for AbxrLib, providing methods for:
 * - Event tracking and analytics
 * - User authentication and session management
 * - Data storage and retrieval
 * - Telemetry collection
 * - AI proxy functionality
 * - Exit polling and user feedback
 * 
 * The Abxr class serves as the main entry point for all AbxrLib functionality,
 * offering a comprehensive analytics and data collection system for Unity applications.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime;
using UnityEngine;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.UI.ExitPoll;

public static partial class Abxr
{
	// ── Events ────────────────────────────────────────────────
	
	/// <summary>
	/// Event triggered when authentication completes
	/// 'true' for success and 'false' for failure (string argument will contain the error message on failure)
	/// Subscribe to this event to handle authentication results
	/// </summary>
	public static Action<bool, string> OnAuthCompleted;

	/// <summary>
	/// Event triggered when a headset is put on, starting a new session
	/// Subscribe to this event to handle new session initialization
	/// </summary>
	public static Action OnHeadsetPutOnNewSession;
	
	/// <summary>
	/// Event that gets triggered when a moduleTarget should be handled.
	/// Subscribe to this event to handle moduleTargets with your own logic (e.g., deep link handling, scene navigation, etc.).
	/// </summary>
	public static Action<string> OnModuleTarget;
		
	public static Action OnAllModulesCompleted;
	
	
	// ── Types ────────────────────────────────────────────────
	
	public enum LogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
		Critical
	}
	
	public enum EventStatus
	{
		Pass,
		Fail,
		Complete,
		Incomplete,
		Browsed,
		NotAttempted
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


	public enum InteractionResult
	{
		Correct,
		Incorrect,
		Neutral
	}
	
	public enum StoragePolicy
	{
		KeepLatest,
		AppendHistory
	}

	public enum StorageScope
	{
		Device,
		User
	}
	
	// Data structures for result options and event status
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

	
	// ── Public API ──────────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// Get feedback from the user with a Poll
	/// </summary>
	/// <param name="prompt">The question being asked</param>
	/// <param name="pollType">What kind of poll would you like</param>
	/// <param name="responses">If a multiple choice poll, you need to provide between 2 and 8 possible responses</param>
	/// <param name="callback">Optional callback that will be called with the selected string value (Multiple-choice poll only)</param>
	public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null) =>
		X?.PollUser(prompt, pollType, responses, callback);
	
	/// <summary>
	/// Allow the user to toggle whether the authentication UI follows them or stay
	/// in place
	/// </summary>
	/// <summary>
	/// Gets the AuthUIFollowCamera setting from configuration
	/// </summary>
	public static bool AuthUIFollowCamera => Configuration.Instance.authUIFollowCamera;

	/// <summary>
	/// Get the full auth response from the last successful authentication.
	/// Includes Token, UserData, AppId, Modules, PackageName, etc. Use this in OnAuthCompleted handlers (e.g. launcher apps that need to pass auth to another app).
	/// Returns null if not authenticated yet.
	/// </summary>
	/// <returns>The auth response, or null if not authenticated</returns>
	public static AuthResponse GetAuthResponse() => X?.GetAuthResponse();

	/// <summary>
	/// Get the learner/user data from the most recent authentication completion
	/// This is the userData object from the authentication response, containing user preferences and information
	/// The API handles normalization and adds OrgId to the UserData
	/// Returns null if authentication has not completed or UserData is not available
	/// </summary>
	/// <returns>Dictionary containing learner data, or null if not authenticated or UserData is not available</returns>
	public static Dictionary<string, string> GetUserData() => X?.GetUserData();
	
	/// <summary>
	/// Update user data (UserId and UserData) and reauthenticate to sync with server
	/// Updates the authentication response with new user information without clearing authentication state
	/// The server API allows reauthenticate to update these values
	/// </summary>
	/// <param name="userId">Optional user ID to update</param>
	/// <param name="additionalUserData">Optional additional user data dictionary to merge with existing UserData</param>
	public static void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null) =>
		X?.SetUserData(userId, additionalUserData);
	
	/// <summary>
	/// Manually start the authentication process
	/// Use this when Enable Auto Start Authentication is off in configuration
	/// or when you want to trigger authentication at a specific time in your app
	/// </summary>
	public static void StartAuthentication() => X?.StartAuthentication();
	public static void ReAuthenticate() => X?.StartAuthentication();

	/// <summary>
	/// Start a new session with a fresh session identifier
	/// Generates a new session ID and performs fresh authentication
	/// Useful for starting new training experiences or resetting user context
	/// </summary>
	public static void StartNewSession() => X?.StartNewSession();
	
	
	// ── Custom Events ───────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// Add event information
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="metadata">Any additional information (optional)</param>
	/// <param name="sendTelemetry">Send telemetry with the event (optional)</param>
	public static void Event(string eventName, Dictionary<string, string> metadata = null, bool sendTelemetry = true) =>
		X?.Event(eventName, metadata, sendTelemetry);

	/// <summary>
	/// Add event information
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="position">Adds position tracking of the object</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void Event(string eventName, Vector3 position, Dictionary<string, string> metadata = null) =>
		X?.Event(eventName, position, metadata);

	/// <summary>
	/// Start timing an event
	/// Call Event() later with the same event name to automatically include duration
	/// Works with all event methods since they use Event() internally
	/// </summary>
	/// <param name="eventName">Name of the event to start timing</param>
	public static void StartTimedEvent(string eventName) => X?.StartTimedEvent(eventName);
	
	/// <summary>
	/// Start tracking an assessment - essential for LMS integration and analytics
	/// Assessments track overall learner performance across multiple objectives and interactions
	/// Think of this as the learner's score for a specific course or curriculum
	/// This method will wait for authentication to complete before processing the event
	/// </summary>
	/// <param name="assessmentName">Name of the assessment to start</param>
	/// <param name="meta">Optional metadata with assessment details</param>
	public static void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null) =>
		X?.EventAssessmentStart(assessmentName, meta);

	/// <summary>
	/// Complete an assessment with score and status - triggers LMS grade recording
	/// When complete, automatically records and closes the assessment in supported LMS platforms
	/// </summary>
	/// <param name="assessmentName">Name of the assessment (must match the start event)</param>
	/// <param name="score">Numerical score achieved (typically 0-100, but any integer is valid)</param>
	/// <param name="status">Result status of the assessment (Pass, Fail, Complete, etc.)</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) =>
		X?.EventAssessmentComplete(assessmentName, score, status, meta);
	public static void EventAssessmentComplete(string assessmentName, string score, EventStatus result = EventStatus.Complete, Dictionary<string, string> meta = null) =>
		EventAssessmentComplete(assessmentName, int.Parse(score), result, meta);  // just here for backwards compatibility
	public static void EventAssessmentComplete(string assessmentName, string score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null) =>
		EventAssessmentComplete(assessmentName, int.Parse(score), ToEventStatus(result), meta);  // just here for backwards compatibility
	
	/// <summary>
	/// Start tracking an experience - developer-friendly wrapper for EventAssessmentStart
	/// This method provides a more intuitive API for VR experiences that don't feel like traditional assessments
	/// but still need assessment tracking behind the scenes for LMS integration
	/// </summary>
	/// <param name="experienceName">Name of the experience to start</param>
	/// <param name="meta">Optional metadata with experience details</param>
	public static void EventExperienceStart(string experienceName, Dictionary<string, string> meta = null) =>
		X?.EventExperienceStart(experienceName, meta);
	
	/// <summary>
	/// Complete an experience - developer-friendly wrapper for EventAssessmentComplete
	/// This method automatically uses score=100 and status=Complete, making it perfect for VR experiences
	/// where completion itself is the goal rather than a graded assessment
	/// </summary>
	/// <param name="experienceName">Name of the experience (must match the start event)</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventExperienceComplete(string experienceName, Dictionary<string, string> meta = null) =>
		X?.EventExperienceComplete(experienceName, meta);
	
	/// <summary>
	/// Start tracking an objective - individual learning goals within assessments
	/// Objectives represent specific tasks or skills that contribute to overall assessment scores
	/// </summary>
	/// <param name="objectiveName">Name of the objective to start</param>
	/// <param name="meta">Optional metadata with objective details</param>
	public static void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null) =>
		X?.EventObjectiveStart(objectiveName, meta);

	/// <summary>
	/// Complete an objective with score and status - contributes to overall assessment
	/// Objectives automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="objectiveName">Name of the objective (must match the start event)</param>
	/// <param name="score">Numerical score achieved for this objective</param>
	/// <param name="status">Result status (Complete, Pass, Fail, etc.)</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null) =>
		X?.EventObjectiveComplete(objectiveName, score, status, meta);
	public static void EventObjectiveComplete(string objectiveName, string score, EventStatus result = EventStatus.Complete, Dictionary<string, string> meta = null) =>
		EventObjectiveComplete(objectiveName, int.Parse(score), result, meta);  // just here for backwards compatibility
	
	/// <summary>
	/// Start tracking a user interaction - granular user actions within objectives
	/// Interactions capture specific user behaviors like clicks, selections, or inputs
	/// </summary>
	/// <param name="interactionName">Name of the interaction to start</param>
	/// <param name="meta">Optional metadata with interaction context</param>
	public static void EventInteractionStart(string interactionName, Dictionary<string, string> meta = null) =>
		X?.EventInteractionStart(interactionName, meta);

	/// <summary>
	/// Complete an interaction with type, response, and optional metadata
	/// Interactions automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="interactionName">Name of the interaction (must match the start event)</param>
	/// <param name="type">Type of interaction (Select, Text, Bool, Rating, etc.)</param>
	/// <param name="result">User's response (e.g., Correct, Incorrect, Neutral)</param>
	/// <param name="response">User's response (e.g., "A", "red_pill", "blue_pill")</param>
	/// <param name="meta">Optional metadata with interaction details</param>
	public static void EventInteractionComplete(string interactionName, InteractionType type, InteractionResult result = InteractionResult.Neutral, string response = null, Dictionary<string, string> meta = null) =>
		X?.EventInteractionComplete(interactionName, type, result, response, meta);
	public static void EventInteractionComplete(string interactionName, InteractionType type, string response = "", Dictionary<string, string> meta = null) =>
		EventInteractionComplete(interactionName, type, InteractionResult.Neutral, response, meta); // Just here for backwards compatability
	// backwards compatibility for very old method signature (string, string, string, InteractionType, meta)
	public static void EventInteractionComplete(string interactionName, string result, string response, InteractionType interactionType, Dictionary<string, string> meta = null)
	{
		// Convert string result to InteractionResult enum
		InteractionResult interactionResult = result?.ToLower() switch
		{
			"true" or "correct" or "pass" => InteractionResult.Correct,
			"false" or "incorrect" or "fail" => InteractionResult.Incorrect,
			_ => InteractionResult.Neutral
		};
			
		EventInteractionComplete(interactionName, interactionType, interactionResult, response, meta);
	}
	
	/// <summary>
	/// Start tracking a level or stage in your application
	/// Levels represent discrete sections or progressions in games, training, or experiences
	/// </summary>
	/// <param name="levelName">Name of the level to start</param>
	/// <param name="meta">Optional metadata with level details</param>
	public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null) =>
		X?.EventLevelStart(levelName, meta);
	
	/// <summary>
	/// Complete a level with score and optional metadata
	/// Levels automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="levelName">Name of the level (must match the start event)</param>
	/// <param name="score">Numerical score achieved for this level</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventLevelComplete(string levelName, string score, Dictionary<string, string> meta = null) =>
		X?.EventLevelComplete(levelName, score, meta);
	
	/// <summary>
	/// Flag critical training events for auto-inclusion in the Critical Choices Chart
	/// Use this to mark important safety checks, high-risk errors, or critical decision points
	/// These events receive special treatment in analytics dashboards and reports
	/// </summary>
	/// <param name="label">Label for the critical event (will be prefixed with CRITICAL_ABXR_)</param>
	/// <param name="meta">Optional metadata with critical event details</param>
	public static void EventCritical(string label, Dictionary<string, string> meta = null) =>
		X?.EventCritical(label, meta);
	
	
	// ── Logging ─────────────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// General logging method with configurable level - main logging function
	/// </summary>
	/// <param name="logMessage">The log message</param>
	/// <param name="logLevel">Log level (defaults to LogLevel.Info)</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void Log(string logMessage, LogLevel logLevel = LogLevel.Info, Dictionary<string, string> metadata = null) =>
		X?.Log(logMessage, logLevel, metadata);

	/// <summary>
	/// Add log information at the 'Debug' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogDebug(string logText, Dictionary<string, string> metadata = null) =>
		Log(logText, LogLevel.Debug, metadata);

	/// <summary>
	/// Add log information at the 'Informational' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogInfo(string logText, Dictionary<string, string> metadata = null) =>
		Log(logText, LogLevel.Info, metadata);

	/// <summary>
	/// Add log information at the 'Warning' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogWarn(string logText, Dictionary<string, string> metadata = null) =>
		Log(logText, LogLevel.Warn, metadata);

	/// <summary>
	/// Add log information at the 'Error' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogError(string logText, Dictionary<string, string> metadata = null) =>
		Log(logText, LogLevel.Error, metadata);

	/// <summary>
	/// Add log information at the 'Critical' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogCritical(string logText, Dictionary<string, string> metadata = null) =>
		Log(logText, LogLevel.Critical, metadata);

	
	// ── Telemetry ───────────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// Manual telemetry activation when automatic telemetry is off.
	/// If Enable Automatic Telemetry is unchecked in the AbxrLib configuration,
	/// you can manually start tracking system telemetry with this function call.
	/// This captures headset/controller movements, performance metrics, and environmental data.
	/// </summary>
	public static void TrackAutoTelemetry() => X?.TrackAutoTelemetry();
	
	/// <summary>
	/// Send spatial, hardware, or system telemetry data for XR analytics
	/// Captures headset/controller movements, performance metrics, and environmental data
	/// </summary>
	/// <param name="telemetryName">Type of telemetry data (e.g., "headset_position", "frame_rate", "battery_level")</param>
	/// <param name="telemetryData">Key-value pairs of telemetry measurements</param>
	public static void Telemetry(string telemetryName, Dictionary<string, string> telemetryData) =>
		X?.Telemetry(telemetryName, telemetryData);
	
	// BACKWARD COMPATIBILITY ONLY - DO NOT DOCUMENT
	public static void TelemetryEntry(string telemetryName, Dictionary<string, string> telemetryData) =>
		X?.Telemetry(telemetryName, telemetryData);
	
	
	// ── Storage ─────────────────────────────────────────────────────────────────────────────────────────────────────
	
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
	public static IEnumerator StorageGetDefaultEntry(StorageScope scope, Action<List<Dictionary<string, string>>> callback) =>
		X?.StorageGetDefaultEntry(scope, callback);
	
	/// <summary>
	/// Get the session data with the given name
	/// Call this as follows:
	/// StartCoroutine(StorageGetDefaultEntry(scope, result => {
	///	    Debug.Log("Result: " + result);
	/// }));
	/// </summary>
	/// <param name="entryName">The name of the entry to retrieve</param>
	/// <param name="scope">Get from 'device' or 'user'</param>
	/// <param name="callback">Return value when finished</param>
	/// <returns>All the session data stored under the given name</returns>
	public static IEnumerator StorageGetEntry(string entryName, StorageScope scope, Action<List<Dictionary<string, string>>> callback) =>
		X?.StorageGetEntry(entryName, scope, callback);
	
	/// <summary>
	/// Set the session data with the default name 'state'
	/// </summary>
	/// <param name="entry">The data to store</param>
	/// <param name="scope">Store under 'device' or 'user'</param>
	/// <param name="policy">How should this be stored, 'keep latest' or 'append history' (defaults to 'keep latest')</param>
	public static void StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.KeepLatest) =>
		X?.StorageSetDefaultEntry(entry, scope, policy);
	
	/// <summary>
	/// Set the session data with the given name
	/// </summary>
	/// <param name="entryName">The name of the entry to store</param>
	/// <param name="entryData">The data to store</param>
	/// <param name="scope">Store under 'device' or 'user'</param>
	/// <param name="policy">How should this be stored, 'keep latest' or 'append history' (defaults to 'keep latest')</param>
	public static void StorageSetEntry(string entryName, Dictionary<string, string> entryData, StorageScope scope, StoragePolicy policy = StoragePolicy.KeepLatest) =>
		X?.StorageSetEntry(entryName, entryData, scope, policy);
	
	/// <summary>
	/// Remove the session data stored under the default name 'state'
	/// </summary>
	/// <param name="scope">Remove from 'device' or 'user' (defaults to 'user')</param>
	public static void StorageRemoveDefaultEntry(StorageScope scope = StorageScope.User) =>
		X?.StorageRemoveDefaultEntry(scope);
	
	/// <summary>
	/// Remove the session data stored under the given name
	/// </summary>
	/// <param name="entryName">The name of the entry to remove</param>
	/// <param name="scope">Remove from 'device' or 'user' (defaults to 'user')</param>
	public static void StorageRemoveEntry(string entryName, StorageScope scope = StorageScope.User) =>
		X?.StorageRemoveEntry(entryName, scope);
	
	/// <summary>
	/// Remove all the session data stored on the device or for the current user
	/// </summary>
	/// <param name="scope">Remove all from 'device' or 'user' (defaults to 'user')</param>
	public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.User) =>
		X?.StorageRemoveMultipleEntries(scope);
	
	
	// ── AI Proxy ────────────────────────────────────────────────────────────────────────────────────────────────────
	
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
	public static IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback) =>
		X?.AIProxy(prompt, llmProvider, callback);
	
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
	public static IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback) =>
		X?.AIProxy(prompt, pastMessages, llmProvider, callback);

	/// <summary>
	/// Register a super metadata that will be automatically included in all events
	/// super metadata persist across app sessions and are stored locally
	/// </summary>
	/// <param name="key">Metadata name</param>
	/// <param name="value">Metadata value</param>
	/// <param name="overwrite">Overwrite existing super metadata (optional)</param>
	public static void Register(string key, string value, bool overwrite = true) => X?.Register(key, value, overwrite);
	
	
	// ── Super Metadata ──────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// Register a super metadata only if it doesn't already exist
	/// Will not overwrite existing super metadata with the same key
	/// </summary>
	/// <param name="key">Metadata name</param>
	/// <param name="value">Metadata value</param>
	public static void RegisterOnce(string key, string value) => X?.RegisterOnce(key, value);
	
	/// <summary>
	/// Remove a super metadata entry
	/// </summary>
	/// <param name="key">Metadata name to remove</param>
	public static void Unregister(string key) => X?.Unregister(key);

	/// <summary>
	/// Clear all super metadata
	/// Clears all super metadata from persistent storage (matches Mixpanel.Reset())
	/// </summary>
	public static void Reset() => X?.Reset();

	/// <summary>
	/// Get a copy of all current super metadata
	/// </summary>
	/// <returns>Dictionary containing all super metadata</returns>
	public static Dictionary<string, string> GetSuperMetaData() => X?.GetSuperMetaData();
	
	
	// ── Modules ─────────────────────────────────────────────────────────────────────────────────────────────────────
	
	public static bool StartModuleAtIndex(int moduleIndex) => X?.StartModuleAtIndex(moduleIndex) ?? false;
	
	/// <summary>
	/// Get all available modules from the authentication response
	/// Provides complete module information including id, name, target, and order
	/// Returns empty list if no authentication has completed yet
	/// </summary>
	/// <returns>List of ModuleData objects with complete module information</returns>
	public static List<ModuleData> GetModuleList() => X?.GetModuleList();
	
	
	// ── Arbor MDM API ───────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>Gets the UUID assigned to device by ArborXR.</summary>
	/// <returns>UUID is provided as a string.</returns>
	public static string GetDeviceId() => X?.GetDeviceId();

	/// <summary>Gets the serial number assigned to device by OEM.</summary>
	/// <returns>Serial number is provided as a string.</returns>
	public static string GetDeviceSerial() => X?.GetDeviceSerial();

	/// <summary>Gets the title given to device by admin through the ArborXR Web Portal.</summary>
	public static string GetDeviceTitle() => X?.GetDeviceTitle();

	/// <summary>Gets the tags added to device by admin through the ArborXR Web Portal.</summary>
	/// <returns>Tags are represented as a string array. Array will be empty if no tags are assigned to device.</returns>
	public static string[] GetDeviceTags() => X?.GetDeviceTags();

	/// <summary>
	///   Gets the UUID of the organization where the device is assigned. Organizations are created in the
	///   ArborXR Web Portal.
	/// </summary>
	/// <returns>UUID is provided as a string.</returns>
	public static string GetOrgId() => X?.GetOrgId();

	/// <summary>Gets the name assigned to organization by admin through the ArborXR Web Portal.</summary>
	public static string GetOrgTitle() => X?.GetOrgTitle();

	/// <summary>Gets the identifier generated by ArborXR when admin assigns title to organization.</summary>
	public static string GetOrgSlug() => X?.GetOrgSlug();

	/// <summary>Gets the physical MAC address assigned to device by OEM.</summary>
	/// <returns>MAC address is provided as a string.</returns>
	public static string GetMacAddressFixed() => X?.GetMacAddressFixed();

	/// <summary>Gets the randomized MAC address for the current WiFi connection.</summary>
	/// <returns>MAC address is provided as a string.</returns>
	public static string GetMacAddressRandom() => X?.GetMacAddressRandom();

	/// <summary>Gets whether the device is SSO authenticated.</summary>
	/// <returns>Whether the device is SSO authenticated.</returns>
	public static bool GetIsAuthenticated() => X?.GetIsAuthenticated() ?? false;

	/// <summary>Gets SSO access token.</summary>
	/// <returns>SSO access token.</returns>
	public static string GetAccessToken() => X?.GetAccessToken();

	/// <summary>Gets SSO refresh token.</summary>
	/// <returns>SSO refresh token.</returns>
	public static string GetRefreshToken() => X?.GetRefreshToken();

	/// <summary>Gets SSO token remaining lifetime.</summary>
	/// <returns>The remaining lifetime of the access token in seconds.</returns>
	public static DateTime? GetExpiresDateUtc() => X?.GetExpiresDateUtc();

	// <summary>Gets the device fingerprint.</summary>
	/// <returns>The device fingerprint.</returns>
	public static string GetFingerprint() => X?.GetFingerprint();
	
	// ────────────────────────────────────────────────────────────────────────────────────────────────────────────────
	
	private static AbxrSubsystem X
	{
		get
		{
			if (AbxrSubsystem.Instance == null)
			{
				Debug.LogWarning("AbxrLib: Not initialized yet.");
				return null;
			}
			return AbxrSubsystem.Instance;
		}
	}
}
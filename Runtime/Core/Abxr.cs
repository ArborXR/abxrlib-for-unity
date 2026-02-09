/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
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

// System namespaces
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Unity namespaces
using UnityEngine;

// AbxrLib namespaces
using AbxrLib.Runtime.AI;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Data;
using AbxrLib.Runtime.ServiceClient;
using AbxrLib.Runtime.Storage;
using AbxrLib.Runtime.Telemetry;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;

/// <summary>
/// Main API class for AbxrLib - Unity Analytics and Data Collection Library
/// 
/// AbxrLib provides comprehensive analytics, telemetry, and data collection capabilities
/// for Unity applications, with special focus on XR/VR experiences. This class serves
/// as the primary entry point for all library functionality.
/// 
/// Key Features:
/// - Event tracking and analytics with hierarchical assessment/objective/interaction structure
/// - User authentication and session management with LMS integration support
/// - Persistent data storage with device and user scoping
/// - Real-time telemetry collection for performance monitoring
/// - AI proxy functionality for LLM integration
/// - Exit polling and user feedback collection
/// - Mixpanel and Cognitive3D compatibility layers
/// 
/// Usage Example:
/// <code>
/// // Initialize and authenticate
/// Abxr.OnAuthCompleted += (success, error) => {
///     if (success) {
///         // Start tracking events
///         Abxr.EventAssessmentStart("Training Module 1");
///         Abxr.EventObjectiveStart("Safety Check");
///         Abxr.EventInteractionStart("Button Click");
///         
///         // Complete events
///         Abxr.EventInteractionComplete("Button Click", InteractionType.Select, "correct");
///         Abxr.EventObjectiveComplete("Safety Check", 95, EventStatus.Pass);
///         Abxr.EventAssessmentComplete("Training Module 1", 88, EventStatus.Pass);
///     }
/// };
/// </code>
/// </summary>
public static partial class Abxr
{
	#region Constructor
	static Abxr()
	{
		LoadSuperMetaData();
		// Subscribe to OnAuthCompleted to start delayed DEFAULT assessment timer
		OnAuthCompleted += OnAuthCompletedHandler;
	}
	#endregion

	#region Specialized Dictionary

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

	#endregion

	#region Authentication Functions and Wrappers

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

	// Connection status - tracks whether AbxrLib can communicate with the server
	private static bool _connectionActive = false;

	/// <summary>
	/// Check if AbxrLib has an active connection to the server and can send data
	/// This indicates whether the library is configured and ready to communicate
	/// </summary>
	/// <returns>True if connection is active, false otherwise</returns>
	public static bool ConnectionActive() => _connectionActive;

	/// <summary>
	/// Allow the user to toggle whether the authentication UI follows them or stay
	/// in place
	/// </summary>
	/// <summary>
	/// Gets the AuthUIFollowCamera setting from configuration
	/// </summary>
	public static bool AuthUIFollowCamera => Configuration.Instance.authUIFollowCamera;
	
	/// <summary>
	/// Trigger authentication completion callback
	/// Internal method - called by authentication system when authentication completes
	/// </summary>
	internal static void NotifyAuthCompleted()
	{
		// Update connection status based on authentication success
		_connectionActive = true;
		
		// Start default assessment tracking if no assessments are currently running
		// This ensures duration tracking starts immediately after authentication
		// But delay sending the event to server for 1 minute to allow developers to start their own assessment
		// Use lock to prevent race condition with concurrent EventAssessmentStart calls
		lock (_assessmentStartTimesLock)
		{
			if (_assessmentStartTimes.Count == 0)
			{
				// Set start time for duration tracking, but don't send event yet
				// The OnAuthCompleted event handler will start the delayed timer
				_assessmentStartTimes["DEFAULT"] = DateTime.UtcNow;
			}
		}
		
		// Invoke authentication completion event
		// The OnAuthCompletedHandler will start the delayed DEFAULT assessment timer if needed
		OnAuthCompleted?.Invoke(true, null);
		
		var response = Authentication.GetAuthResponse();
		if (response.Modules == null || response.Modules.Count == 0) return;
		
		if (OnModuleTarget != null)
		{
			if (Configuration.Instance.enableAutoStartModules && _currentModuleIndex < response.Modules.Count)
			{
				OnModuleTarget?.Invoke(response.Modules[_currentModuleIndex].Target);
			}
		}
		else
		{
			Debug.LogError("AbxrLib: Need to subscribe to OnModuleTarget before running modules");
		}
	}

	/// <summary>
	/// Get the learner/user data from the most recent authentication completion
	/// This is the userData object from the authentication response, containing user preferences and information
	/// The API handles normalization and adds OrgId to the UserData
	/// Returns null if authentication has not completed or UserData is not available
	/// </summary>
	/// <returns>Dictionary containing learner data, or null if not authenticated or UserData is not available</returns>
	public static Dictionary<string, string> GetUserData()
	{
		if (!Authentication.Authenticated())
		{
			return null;
		}
		
		var authResponse = Authentication.GetAuthResponse();
		if (authResponse?.UserData == null)
		{
			return null;
		}
		
		// Return a copy of UserData to avoid modifying the original
		return new Dictionary<string, string>(authResponse.UserData);
	}
	
	/// <summary>
	/// Update user data (UserId and UserData) and reauthenticate to sync with server
	/// Updates the authentication response with new user information without clearing authentication state
	/// The server API allows reauthenticate to update these values
	/// </summary>
	/// <param name="userId">Optional user ID to update</param>
	/// <param name="additionalUserData">Optional additional user data dictionary to merge with existing UserData</param>
	public static void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null)
	{
		Authentication.SetUserData(userId, additionalUserData);
	}

	/// <summary>
	/// Manually start the authentication process
	/// Use this when autoStartAuthentication is disabled in configuration
	/// or when you want to trigger authentication at a specific time in your app
	/// </summary>
	public static void StartAuthentication()
	{
		CoroutineRunner.Instance.StartCoroutine(Authentication.Authenticate());
	}

	/// <summary>
	/// Trigger manual reauthentication with existing stored parameters
	/// Primarily useful for testing authentication flows or recovering from auth issues
	/// Resets authentication state and attempts to re-authenticate with stored credentials
	/// </summary>
	public static void ReAuthenticate()
	{
		Authentication.ReAuthenticate();
	}

	/// <summary>
	/// Start a new session with a fresh session identifier
	/// Generates a new session ID and performs fresh authentication
	/// Useful for starting new training experiences or resetting user context
	/// </summary>
	public static void StartNewSession()
	{
		Authentication.SetSessionId(Guid.NewGuid().ToString());
		CoroutineRunner.Instance.StartCoroutine(Authentication.Authenticate());
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
			KeyboardHandler.SetPrompt(promptText ?? "Enter Your Login");
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
				$"{promptText} \n(<u>username</u>@{emailDomain})" :
				$"Enter your email username\n(<u>username</u>@{emailDomain})");
		}
	}

	#endregion

	#region Event Functions and Wrappers

	// Event start times for duration tracking
	private static readonly Dictionary<string, DateTime> _timedEventStartTimes = new();
	private static readonly Dictionary<string, DateTime> _assessmentStartTimes = new();
	private static readonly Dictionary<string, DateTime> _objectiveStartTimes = new();
	private static readonly Dictionary<string, DateTime> _interactionStartTimes = new();
	private static readonly Dictionary<string, DateTime> _levelStartTimes = new();
	
	// Lock for thread-safe access to assessment start times
	private static readonly object _assessmentStartTimesLock = new object();
	
	// Track whether any assessment has been started (either DEFAULT or user-initiated)
	private static bool _assessmentStarted = false;

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

	/// <summary>
	/// Add event information
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="metadata">Any additional information (optional)</param>
	/// <param name="sendTelemetry">Send telemetry with the event (optional)</param>
	public static void Event(string eventName, Dictionary<string, string> metadata = null, bool sendTelemetry = true)
	{
		// Ensure an assessment has been started before sending any event
		// This ensures we always have an assessment start as the first event
		// NOTE: Not sure if this is needed anymore since the OnAuthCompletedHandler will start the delayed timer
		// lock (_assessmentStartTimesLock)
		// {
		// 	if (!_assessmentStarted)
		// 	{
		// 		DefaultEventAssessmentStart();
		// 	}
		// }
		
		metadata ??= new Dictionary<string, string>();
		metadata["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
		// Add super metadata to all events
		metadata = MergeSuperMetaData(metadata);
		
		// Add duration if this was a timed event (StartTimedEvent functionality)
		if (_timedEventStartTimes.ContainsKey(eventName) && !metadata.ContainsKey("duration"))
		{
			AddDuration(_timedEventStartTimes, eventName, metadata);
		}
		
		DataBatcher.AddEvent(eventName, metadata);
		if (sendTelemetry)
		{
			TrackSystemInfo.SendAll();
			TrackInputDevices.SendLocationData();
		}
	}

	/// <summary>
	/// Add event information
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="position">Adds position tracking of the object</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void Event(string eventName, Vector3 position, Dictionary<string, string> metadata = null)
	{
		metadata ??= new Dictionary<string, string>();
		metadata["position_x"] = position.x.ToString();
		metadata["position_y"] = position.y.ToString();
		metadata["position_z"] = position.z.ToString();
		Event(eventName, metadata);
	}

	// Event wrapper functions
	
	/// <summary>
	/// Start timing an event
	/// Call Event() later with the same event name to automatically include duration
	/// Works with all event methods since they use Event() internally
	/// </summary>
	/// <param name="eventName">Name of the event to start timing</param>
	public static void StartTimedEvent(string eventName)
	{
		_timedEventStartTimes[eventName] = DateTime.UtcNow;
	}

	/// <summary>
	/// Private method to send the DEFAULT assessment start event
	/// Called automatically if no assessment is started within 1 minute of authentication
	/// or before any other event is sent
	/// </summary>
	private static void DefaultEventAssessmentStart()
	{
		lock (_assessmentStartTimesLock)
		{
			// Only send if assessment hasn't been started yet
			if (_assessmentStarted)
			{
				return;
			}
			
			// Ensure start time exists for duration tracking (should already be set in NotifyAuthCompleted)
			// Only set if it doesn't exist (defensive programming)
			if (!_assessmentStartTimes.ContainsKey("DEFAULT"))
			{
				_assessmentStartTimes["DEFAULT"] = DateTime.UtcNow;
			}
			
			var defaultMeta = new Dictionary<string, string>
			{
				["type"] = "assessment",
				["verb"] = "started"
			};
			Event("DEFAULT", defaultMeta);
			SetModule("DEFAULT");
			_assessmentStarted = true;
		}
	}

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
		// Use lock to prevent race conditions with concurrent calls and NotifyAuthCompleted
		lock (_assessmentStartTimesLock)
		{
			// Skip if this assessment already exists
			if (_assessmentStartTimes.ContainsKey(assessmentName))
			{
				return;
			}
			
			// Mark that an assessment has been started (either DEFAULT or user-initiated)
			_assessmentStarted = true;
			
			// If user is starting their own assessment (not the default), silently remove the default assessment
			// This removes it as if it never existed - no completion event will be sent
			if (assessmentName != "DEFAULT" && _assessmentStartTimes.ContainsKey("DEFAULT"))
			{
				_assessmentStartTimes.Remove("DEFAULT");
			}
			
			// Set module metadata using the assessment name (only if no auth-provided modules exist)
			SetModule(assessmentName);
			
			meta ??= new Dictionary<string, string>();
			meta["type"] = "assessment";
			meta["verb"] = "started";
			_assessmentStartTimes[assessmentName] = DateTime.UtcNow;
			Event(assessmentName, meta);
		}
	}
	
	/// <summary>
	/// Complete an assessment with score and status - triggers LMS grade recording
	/// When complete, automatically records and closes the assessment in supported LMS platforms
	/// </summary>
	/// <param name="assessmentName">Name of the assessment (must match the start event)</param>
	/// <param name="score">Numerical score achieved (typically 0-100, but any integer is valid)</param>
	/// <param name="status">Result status of the assessment (Pass, Fail, Complete, etc.)</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "assessment";
		meta["verb"] = "completed";
		meta["score"] = score.ToString();
		meta["status"] = status.ToString().ToLower();
		lock (_assessmentStartTimesLock)
		{
			// If user is completing their own assessment (not the default), silently remove the default assessment
			// This removes it as if it never existed - no completion event will be sent
			// This handles the case where user completes an assessment without starting it
			if (assessmentName != "DEFAULT" && _assessmentStartTimes.ContainsKey("DEFAULT"))
			{
				_assessmentStartTimes.Remove("DEFAULT");
			}
			
			AddDuration(_assessmentStartTimes, assessmentName, meta);
		}
		Event(assessmentName, meta);
		
		// Check if we're in a module sequence
		if (Authentication.GetAuthResponse().Modules.Count > 0 && Configuration.Instance.enableAutoAdvanceModules)
		{
			AdvanceToNextModule();
		}
		else
		{
			// Not in a module sequence - use original exit logic
			if (Authentication.SessionUsedAuthHandoff() && Configuration.Instance.returnToLauncherAfterAssessmentComplete)
			{
				CoroutineRunner.Instance.StartCoroutine(ExitAfterAssessmentComplete());
			}
		}
	}
	// backwards compatibility for old method signature
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
	public static void EventExperienceStart(string experienceName, Dictionary<string, string> meta = null)
	{
		EventAssessmentStart(experienceName, meta);
	}

	/// <summary>
	/// Complete an experience - developer-friendly wrapper for EventAssessmentComplete
	/// This method automatically uses score=100 and status=Complete, making it perfect for VR experiences
	/// where completion itself is the goal rather than a graded assessment
	/// </summary>
	/// <param name="experienceName">Name of the experience (must match the start event)</param>
	/// <param name="meta">Optional metadata with completion details</param>
	public static void EventExperienceComplete(string experienceName, Dictionary<string, string> meta = null)
	{
		EventAssessmentComplete(experienceName, 100, EventStatus.Complete, meta);
	}

	/// <summary>
	/// Handler for OnAuthCompleted event that starts the delayed DEFAULT assessment timer
	/// Only starts the timer if a DEFAULT assessment was set up during authentication
	/// </summary>
	/// <param name="success">Whether authentication succeeded</param>
	/// <param name="error">Error message if authentication failed</param>
	private static void OnAuthCompletedHandler(bool success, string error)
	{
		// Only proceed if authentication succeeded
		if (!success)
		{
			return;
		}
		
		// Check if we need to start the delayed DEFAULT assessment timer
		lock (_assessmentStartTimesLock)
		{
			if (_assessmentStartTimes.ContainsKey("DEFAULT") && !_assessmentStarted)
			{
				CoroutineRunner.Instance.StartCoroutine(DelayedDefaultAssessmentStart());
			}
		}
	}

	/// <summary>
	/// Coroutine to send DEFAULT assessment start event after 1 minute if no assessment has been started
	/// This gives developers time to start their own assessment before the DEFAULT one is sent
	/// </summary>
	private static IEnumerator DelayedDefaultAssessmentStart()
	{
		yield return new WaitForSeconds(60f);
		
		lock (_assessmentStartTimesLock)
		{
			if (!_assessmentStarted)
			{
				DefaultEventAssessmentStart();
			}
		}
	}

	/// <summary>
	/// Coroutine to exit the application after a 2-second delay when assessment is complete
	/// and the session used auth handoff with return to launcher enabled
	/// </summary>
	private static IEnumerator ExitAfterAssessmentComplete()
	{
		CoroutineRunner.Instance.StartCoroutine(DataBatcher.Send());
		Debug.Log("AbxrLib: Assessment complete with auth handoff - returning to launcher in 2 seconds");
		yield return new WaitForSeconds(2f);
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
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
		_objectiveStartTimes[objectiveName] = DateTime.UtcNow;
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
	public static void EventObjectiveComplete(string objectiveName, string score, EventStatus result = EventStatus.Complete, Dictionary<string, string> meta = null) =>
		EventObjectiveComplete(objectiveName, int.Parse(score), result, meta);  // just here for backwards compatibility
	public static void EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "objective";
		meta["verb"] = "completed";
		meta["score"] = score.ToString();
		meta["status"] = status.ToString().ToLower();
		AddDuration(_objectiveStartTimes, objectiveName, meta);
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
		_interactionStartTimes[interactionName] = DateTime.UtcNow;
		Event(interactionName, meta);
	}
	
	/// <summary>
	/// Complete an interaction with type, response, and optional metadata
	/// Interactions automatically calculate duration if corresponding start event was logged
	/// </summary>
	/// <param name="interactionName">Name of the interaction (must match the start event)</param>
	/// <param name="interactionType">Type of interaction (Select, Text, Bool, Rating, etc.)</param>
	/// <param name="result">User's response (e.g., Correct, Incorrect, Neutral)</param>
	/// <param name="response">User's response (e.g., "A", "red_pill", "blue_pill")</param>
	/// <param name="meta">Optional metadata with interaction details</param>
	public static void EventInteractionComplete(string interactionName, InteractionType type, InteractionResult result = InteractionResult.Neutral, string response = "", Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["type"] = "interaction";
		meta["verb"] = "completed";
		meta["interaction"] = type.ToString().ToLower();
		meta["result"] = result.ToString().ToLower();
		if (!string.IsNullOrEmpty(response)) meta["response"] = response;
		AddDuration(_interactionStartTimes, interactionName, meta);
		Event(interactionName, meta);
	}
	// backwards compatibility for old method signature
	public static void EventInteractionComplete(string interactionName, InteractionType type, string response = "", Dictionary<string, string> meta = null)
	{
		EventInteractionComplete(interactionName, type, InteractionResult.Neutral, response, meta); // Just here for backwards compatability
	}

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
	public static void EventLevelStart(string levelName, Dictionary<string, string> meta = null)
	{
		meta ??= new Dictionary<string, string>();
		meta["verb"] = "started";
		meta["id"] = levelName;
		_levelStartTimes[levelName] = DateTime.UtcNow;
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
		AddDuration(_levelStartTimes, levelName, meta);
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

	// Event Duration Tracking
	
	/// <summary>
	/// Gets a copy of the current assessment start times for application quit handling.
	/// This method provides safe access to private timing data without using reflection.
	/// </summary>
	/// <returns>Copy of the assessment start times dictionary</returns>
	internal static Dictionary<string, DateTime> GetAssessmentStartTimes()
	{
		lock (_assessmentStartTimesLock)
		{
			return new Dictionary<string, DateTime>(_assessmentStartTimes);
		}
	}
	
	/// <summary>
	/// Gets a copy of the current objective start times for application quit handling.
	/// This method provides safe access to private timing data without using reflection.
	/// </summary>
	/// <returns>Copy of the objective start times dictionary</returns>
	internal static Dictionary<string, DateTime> GetObjectiveStartTimes()
	{
		return new Dictionary<string, DateTime>(_objectiveStartTimes);
	}
	
	/// <summary>
	/// Gets a copy of the current interaction start times for application quit handling.
	/// This method provides safe access to private timing data without using reflection.
	/// </summary>
	/// <returns>Copy of the interaction start times dictionary</returns>
	internal static Dictionary<string, DateTime> GetInteractionStartTimes()
	{
		return new Dictionary<string, DateTime>(_interactionStartTimes);
	}
	
	/// <summary>
	/// Gets a copy of the current level start times for application quit handling.
	/// This method provides safe access to private timing data without using reflection.
	/// </summary>
	/// <returns>Copy of the level start times dictionary</returns>
	internal static Dictionary<string, DateTime> GetLevelStartTimes()
	{
		return new Dictionary<string, DateTime>(_levelStartTimes);
	}
	
	/// <summary>
	/// Clears all timing dictionaries. Used by application quit handler to clean up after processing.
	/// This method provides safe access to private timing data without using reflection.
	/// </summary>
	internal static void ClearAllStartTimes()
	{
		lock (_assessmentStartTimesLock)
		{
			_assessmentStartTimes.Clear();
		}
		_objectiveStartTimes.Clear();
		_interactionStartTimes.Clear();
		_levelStartTimes.Clear();
		_timedEventStartTimes.Clear();
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
	
	#endregion

	#region Logging Functions and Wrappers

	public enum LogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
		Critical
	}

	/// <summary>
	/// General logging method with configurable level - main logging function
	/// </summary>
	/// <param name="logMessage">The log message</param>
	/// <param name="logLevel">Log level (defaults to LogLevel.Info)</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void Log(string logMessage, LogLevel logLevel = LogLevel.Info, Dictionary<string, string> metadata = null)
	{
		metadata ??= new Dictionary<string, string>();
		metadata["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
		// Add super metadata to all logs
		metadata = MergeSuperMetaData(metadata);
		
		string logLevelString = logLevel switch
		{
			LogLevel.Debug => "debug",
			LogLevel.Info => "info",
			LogLevel.Warn => "warn",
			LogLevel.Error => "error",
			LogLevel.Critical => "critical",
			_ => "info" // Default case
		};
		
		DataBatcher.AddLog(logLevelString, logMessage, metadata);
	}

	/// <summary>
	/// Add log information at the 'Debug' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogDebug(string logText, Dictionary<string, string> metadata = null)
	{
		Log(logText, LogLevel.Debug, metadata);
	}

	/// <summary>
	/// Add log information at the 'Informational' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogInfo(string logText, Dictionary<string, string> metadata = null)
	{
		Log(logText, LogLevel.Info, metadata);
	}

	/// <summary>
	/// Add log information at the 'Warning' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogWarn(string logText, Dictionary<string, string> metadata = null)
	{
		Log(logText, LogLevel.Warn, metadata);
	}

	/// <summary>
	/// Add log information at the 'Error' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogError(string logText, Dictionary<string, string> metadata = null)
	{
		Log(logText, LogLevel.Error, metadata);
	}

	/// <summary>
	/// Add log information at the 'Critical' level
	/// </summary>
	/// <param name="logText">The log text</param>
	/// <param name="metadata">Any additional information (optional)</param>
	public static void LogCritical(string logText, Dictionary<string, string> metadata = null)
	{
		Log(logText, LogLevel.Critical, metadata);
	}

	#endregion

	#region Telemetry

	/// <summary>
	/// Manual telemetry activation for disabled automatic telemetry
	/// If you select 'Disable Automatic Telemetry' in the AbxrLib configuration,
	/// you can manually start tracking system telemetry with this function call.
	/// This captures headset/controller movements, performance metrics, and environmental data.
	/// </summary>
	public static void TrackAutoTelemetry() => TrackSystemInfo.StartTracking();

	/// <summary>
	/// Send spatial, hardware, or system telemetry data for XR analytics
	/// Captures headset/controller movements, performance metrics, and environmental data
	/// </summary>
	/// <param name="telemetryName">Type of telemetry data (e.g., "headset_position", "frame_rate", "battery_level")</param>
	/// <param name="telemetryData">Key-value pairs of telemetry measurements</param>
	public static void Telemetry(string telemetryName, Dictionary<string, string> telemetryData)
	{
		telemetryData ??= new Dictionary<string, string>();
		telemetryData["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
		// Add super metadata to all telemetry entries
		telemetryData = MergeSuperMetaData(telemetryData);
		
		DataBatcher.AddTelemetry(telemetryName, telemetryData);
	}

	// BACKWARD COMPATIBILITY ONLY - DO NOT DOCUMENT
	// This method exists purely for backward compatibility with older code that used TelemetryEntry()
	// It simply wraps the new Telemetry() method. Keep this undocumented in README files.
	public static void TelemetryEntry(string telemetryName, Dictionary<string, string> telemetryData)
	{
		Telemetry(telemetryName, telemetryData);
	}

	#endregion

	#region Storage

	// Data structures for storage
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
	/// <param name="entryName">The name of the entry to retrieve</param>
	/// <param name="scope">Get from 'device' or 'user'</param>
	/// <param name="callback">Return value when finished</param>
	/// <returns>All the session data stored under the given name</returns>
	public static IEnumerator StorageGetEntry(string entryName, StorageScope scope, Action<List<Dictionary<string, string>>> callback)
	{
		yield return StorageBatcher.Get(entryName, scope, callback);
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
		if (scope == StorageScope.user && Authentication.GetAuthResponse().UserId == null)
		{
			// User-scoped storage requires a user to be logged in, defer this request
			return;
		}
		
		StorageBatcher.Add("state", entry, scope, policy);
	}

	/// <summary>
	/// Set the session data with the given name
	/// </summary>
	/// <param name="entryName">The name of the entry to store</param>
	/// <param name="entryData">The data to store</param>
	/// <param name="scope">Store under 'device' or 'user'</param>
	/// <param name="policy">How should this be stored, 'keep latest' or 'append history' (defaults to 'keep latest')</param>
	public static void StorageSetEntry(string entryName, Dictionary<string, string> entryData, StorageScope scope, StoragePolicy policy = StoragePolicy.keepLatest)
	{
		// Check if basic authentication is ready
		if (!Authentication.Authenticated())
		{
			// Basic authentication not ready, defer all storage requests
			return;
		}
		
		// For user-scoped storage, we need a user to actually be logged in
		// For device-scoped storage, app-level authentication should be sufficient
		var authResponse = Authentication.GetAuthResponse();
		if (scope == StorageScope.user && (authResponse == null || authResponse.UserId == null))
		{
			// User-scoped storage requires a user to be logged in, defer this request
			return;
		}
		
		StorageBatcher.Add(entryName, entryData, scope, policy);
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
	/// <param name="entryName">The name of the entry to remove</param>
	/// <param name="scope">Remove from 'device' or 'user' (defaults to 'user')</param>
	public static void StorageRemoveEntry(string entryName, StorageScope scope = StorageScope.user)
	{
		CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(scope, entryName));
	}

	/// <summary>
	/// Remove all the session data stored on the device or for the current user
	/// </summary>
	/// <param name="scope">Remove all from 'device' or 'user' (defaults to 'user')</param>
	public static void StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user)
	{
		CoroutineRunner.Instance.StartCoroutine(StorageBatcher.Delete(scope));
	}

	#endregion

	#region Exit Polls

	/// <summary>
	/// Get feedback from the user with a Poll
	/// </summary>
	/// <param name="prompt">The question being asked</param>
	/// <param name="pollType">What kind of poll would you like</param>
	/// <param name="responses">If a multiple choice poll, you need to provide between 2 and 8 possible responses</param>
	/// <param name="callback">Optional callback that will be called with the selected string value (Multiple-choice poll only)</param>
	public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null)
	{
		// Validate prompt
		if (string.IsNullOrWhiteSpace(prompt))
		{
			Debug.LogError("AbxrLib: Poll prompt cannot be null or empty");
			return;
		}

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

			// Validate that all responses are not null or empty
			for (int i = 0; i < responses.Count; i++)
			{
				if (string.IsNullOrWhiteSpace(responses[i]))
				{
					Debug.LogError($"AbxrLib: Response at index {i} cannot be null or empty");
					return;
				}
			}
		}

		ExitPollHandler.AddPoll(prompt, pollType, responses, callback);
	}
	
	#endregion

	#region AI Proxy

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

	#endregion

	#region Super Metadata

	// super metadata for metadata
	private static readonly Dictionary<string, string> _superMetaData = new();
	private const string _superMetaDataKey = "AbxrSuperMetaData";

	/// <summary>
	/// Register a super metadata that will be automatically included in all events
	/// super metadata persist across app sessions and are stored locally
	/// </summary>
	/// <param name="key">Metadata name</param>
	/// <param name="value">Metadata value</param>
	/// <param name="overwrite">Overwrite existing super metadata (optional)</param>
	public static void Register(string key, string value, bool overwrite = true)
	{
		if (IsReservedSuperMetaDataKey(key))
		{
			string errorMessage = $"AbxrLib: Cannot register super metadata with reserved key '{key}'. Reserved keys are: module, moduleName, moduleId, moduleOrder";
			Debug.LogWarning(errorMessage);
			LogInfo(errorMessage, new Dictionary<string, string> { 
				{ "attempted_key", key }, 
				{ "attempted_value", value },
				{ "error_type", "reserved_super_metadata_key" }
			});
			return;
		}

		if (overwrite || !_superMetaData.ContainsKey(key))
		{
			_superMetaData[key] = value;
			SaveSuperMetaData();
		}
	}

	/// <summary>
	/// Register a super metadata only if it doesn't already exist
	/// Will not overwrite existing super metadata with the same key
	/// </summary>
	/// <param name="key">Metadata name</param>
	/// <param name="value">Metadata value</param>
	public static void RegisterOnce(string key, string value) => Register(key, value, false);

	/// <summary>
	/// Remove a super metadata entry
	/// </summary>
	/// <param name="key">Metadata name to remove</param>
	public static void Unregister(string key)
	{
		_superMetaData.Remove(key);
		SaveSuperMetaData();
	}

	/// <summary>
	/// Clear all super metadata
	/// Clears all super metadata from persistent storage (matches Mixpanel.Reset())
	/// </summary>
	public static void Reset()
	{
		_superMetaData.Clear();
		SaveSuperMetaData();
	}

	/// <summary>
	/// Get a copy of all current super metadata
	/// </summary>
	/// <returns>Dictionary containing all super metadata</returns>
	public static Dictionary<string, string> GetSuperMetaData()
	{
		return new Dictionary<string, string>(_superMetaData);
	}

	private static void LoadSuperMetaData()
	{
		string json = PlayerPrefs.GetString(_superMetaDataKey, "{}");
		try
		{
		var serializableDictionary = JsonUtility.FromJson<SerializableDictionary>(json);
		if (serializableDictionary?.items != null)
		{
			_superMetaData.Clear();
			foreach (var keyValueItem in serializableDictionary.items)
			{
				_superMetaData[keyValueItem.key] = keyValueItem.value;
			}
		}
		}
		catch (Exception ex)
		{
			// Log error with consistent format and include stack trace for debugging
			Debug.LogError($"AbxrLib: Failed to load super metadata: {ex.Message}\n" +
						  $"Exception Type: {ex.GetType().Name}\n" +
						  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
		}
	}

	private static void SaveSuperMetaData()
	{
		try
		{
		var serializableDictionary = new SerializableDictionary
		{
			items = _superMetaData.Select(keyValuePair => new SerializableKeyValuePair
			{
				key = keyValuePair.Key,
				value = keyValuePair.Value
			}).ToArray()
		};
		string json = JsonUtility.ToJson(serializableDictionary);
			PlayerPrefs.SetString(_superMetaDataKey, json);
			PlayerPrefs.Save();
		}
		catch (Exception ex)
		{
			// Log error with consistent format and include stack trace for debugging
			Debug.LogError($"AbxrLib: Failed to save super metadata: {ex.Message}\n" +
						  $"Exception Type: {ex.GetType().Name}\n" +
						  $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
		}
	}

	[Serializable]
	private class SerializableDictionary
	{
		public SerializableKeyValuePair[] items;
	}

	[Serializable]
	private class SerializableKeyValuePair
	{
		public string key;
		public string value;
	}

	/// <summary>
	/// Private helper function to merge super metadata and module info into metadata
	/// Ensures data-specific metadata take precedence over super metadata and module info
	/// </summary>
	/// <param name="meta">The metadata dictionary to merge super metadata into</param>
	/// <returns>The metadata dictionary with super metadata and module info merged</returns>
	private static Dictionary<string, string> MergeSuperMetaData(Dictionary<string, string> meta)
	{
		meta ??= new Dictionary<string, string>();
		
		// If LMS modules exist, inject current module metadata unless the event already specifies it.
		// (Data-specific metadata takes precedence.)
		if (Authentication.GetAuthResponse()?.Modules?.Count > 0 && _currentModuleIndex < Authentication.GetAuthResponse()?.Modules?.Count)
		{
			Authentication.ModuleData moduleData = Authentication.GetAuthResponse().Modules[_currentModuleIndex];
			if (!meta.ContainsKey("module")) meta["module"] = moduleData.Target;
			if (!meta.ContainsKey("moduleName")) meta["moduleName"] = moduleData.Name;
			if (!meta.ContainsKey("moduleId")) meta["moduleId"] = moduleData.Id;
			if (!meta.ContainsKey("moduleOrder")) meta["moduleOrder"] = moduleData.Order.ToString();
		}
		
		// Add super metadata to metadata (includes manually-set moduleName/moduleId/moduleOrder when no LMS modules)
		// Auth-provided module metadata takes precedence, so manually-set values only appear when no LMS modules exist
		foreach (var superMetaDataKeyValue in _superMetaData)
		{
			// super metadata don't overwrite data-specific metadata or auth-provided module info
			if (!meta.ContainsKey(superMetaDataKeyValue.Key))
			{
				meta[superMetaDataKeyValue.Key] = superMetaDataKeyValue.Value;
			}
		}
		
		return meta;
	}

	/// <summary>
	/// Private helper to check if a super metadata key is reserved for module data
	/// Reserved keys: module, moduleName, moduleId, moduleOrder
	/// </summary>
	/// <param name="key">The key to validate</param>
	/// <returns>True if the key is reserved, false otherwise</returns>
	private static bool IsReservedSuperMetaDataKey(string key)
	{
		return key == "module" || key == "moduleName" || key == "moduleId" || key == "moduleOrder";
	}
	#endregion

	#region Module Management

	// Module index for sequential LMS multi-module applications
	private static int _currentModuleIndex;

	/// <summary>
	/// Event that gets triggered when a moduleTarget should be handled.
	/// Subscribe to this event to handle moduleTargets with your own logic (e.g., deep link handling, scene navigation, etc.).
	/// </summary>
	public static Action<string> OnModuleTarget;
	
	public static Action OnAllModulesCompleted;
	
	public static bool StartModuleAtIndex(int moduleIndex)
	{
		var response = Authentication.GetAuthResponse();
		if (response?.Modules == null || response.Modules.Count == 0)
		{
			Debug.LogError("AbxrLib: No modules available");
			return false;
		}
	
		if (moduleIndex >= response.Modules.Count || moduleIndex < 0)
		{
			Debug.LogError($"AbxrLib: Invalid module index - {moduleIndex}");
			return false;
		}

		if (OnModuleTarget == null)
		{
			Debug.LogError("AbxrLib: Need to subscribe to OnModuleTarget before running modules");
			return false;
		}
	
		_currentModuleIndex = moduleIndex;
		OnModuleTarget.Invoke(response.Modules[_currentModuleIndex].Target);
		return true;
	}

	/// <summary>
	/// Advance to the next module in the sequence after current module completion.
	/// Called automatically from EventAssessmentComplete() when in a module sequence.
	/// Advances the module index and triggers the next module, or exits if all modules are complete.
	/// </summary>
	private static void AdvanceToNextModule()
	{
		_currentModuleIndex++;
		if (_currentModuleIndex < Authentication.GetAuthResponse().Modules.Count)
		{
			Debug.Log($"AbxrLib: Module '{Authentication.GetAuthResponse().Modules[_currentModuleIndex-1].Name}' complete. " +
			          $"Advancing to next module - '{Authentication.GetAuthResponse().Modules[_currentModuleIndex].Name}'");
			OnModuleTarget?.Invoke(Authentication.GetAuthResponse().Modules[_currentModuleIndex].Target);
		}
		else
		{
			OnAllModulesCompleted?.Invoke();
			Debug.Log("AbxrLib: All modules complete");
		}
	}
	
	/// <summary>
	/// Set module metadata when no modules are provided in authentication.
	/// This method allows developers to track module information even when the LMS doesn't provide a module list.
	/// Only works when NOT using auth-provided module targets - returns safely if auth modules exist.
	/// Sets moduleName, moduleId, and moduleOrder in super metadata for automatic inclusion in all events.
	/// </summary>
	/// <param name="module">The target name of the module</param>
	/// <param name="moduleName">Optional user friendly name of the module</param>
	private static void SetModule(string module, string moduleName = null)
	{
		// Check if we're using auth-provided module targets
		var response = Authentication.GetAuthResponse();
		if (response?.Modules == null || response.Modules.Count > 0)
		{
			// Auth-provided modules exist - don't allow manual setting to prevent breaking module sequence
			return;
		}
		
		if (string.IsNullOrEmpty(module)) return;
		
		// Directly set module metadata in super metadata, bypassing Register() check
		_superMetaData["module"] = module;
			
		if (!string.IsNullOrEmpty(moduleName))
		{
			_superMetaData["moduleName"] = moduleName;
		}
		else
		{
			_superMetaData["moduleName"] = FormatModuleNameForDisplay(module);
		}
				
		// When using SetModule, we should not use moduleOrder - unset it if it was set elsewhere
		_superMetaData.Remove("moduleOrder");
		
		SaveSuperMetaData();
	}

	/// <summary>
	/// Get all available modules from the authentication response
	/// Provides complete module information including id, name, target, and order
	/// Returns empty list if no authentication has completed yet
	/// </summary>
	/// <returns>List of ModuleData objects with complete module information</returns>
	public static List<Authentication.ModuleData> GetModuleList() => Authentication.GetAuthResponse().Modules;

	#endregion

	#region XRDM Device ManagementInformation
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

	#endregion

	#region Helper Methods

	/// <summary>
	/// Formats a module name to be more human-readable by adding spaces between words.
	/// Converts camelCase and PascalCase to space-separated format.
	/// Example: "ModuleName" -> "Module Name", "myModule" -> "my Module"
	/// </summary>
	/// <param name="moduleName">The module name to format</param>
	/// <returns>The formatted module name with spaces between words</returns>
	private static string FormatModuleNameForDisplay(string moduleName)
	{
		if (string.IsNullOrEmpty(moduleName)) return moduleName;

		// Replace underscores with spaces
		string withSpaces = moduleName.Replace('_', ' ');
		
		// Split by spaces to get individual words
		string[] words = withSpaces.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		
		// Capitalize first letter of each word, lowercase the rest
		var result = new System.Text.StringBuilder();
		for (int i = 0; i < words.Length; i++)
		{
			if (i > 0) result.Append(' ');
			
			if (words[i].Length > 0)
			{
				result.Append(char.ToUpper(words[i][0]));
				if (words[i].Length > 1)
				{
					result.Append(words[i].Substring(1).ToLower());
				}
			}
		}

		return result.ToString();
	}

	#endregion
}
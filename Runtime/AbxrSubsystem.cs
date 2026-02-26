using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.AI;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.Services.Data;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Telemetry;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using Newtonsoft.Json;
using UnityEngine;

namespace AbxrLib.Runtime
{
    [DefaultExecutionOrder(-100)]
    internal class AbxrSubsystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────
        internal static AbxrSubsystem Instance { get; private set; }

        // ── Services ─────────────────────────────────────────────────
        private AbxrAuthService _authService;
        private AbxrDataService _dataService;
        private AbxrTelemetryService _telemetryService;
        private ArborServiceClient _arborClient;
#if UNITY_ANDROID && !UNITY_EDITOR
	    private ArborInsightServiceClient _arborInsightService;
#endif
        private AbxrStorageService _storageService;
        private AIProxyApi _aiProxyApi;
        private SceneChangeDetector _sceneChangeDetector;
        private HeadsetDetector _headsetDetector;

        // ── Module state ─────────────────────────────────────────────
        private int _currentModuleIndex;
        private Action<string, string, string, string> _appOnInputRequested;

        // Developer-supplied overrides (bypass ArborServiceClient); null = not set.
        private string _overrideOrgId;
        private string _overrideAuthSecret;
        private string _overrideDeviceId;

        // ── Super metadata ───────────────────────────────────────────
        private const string SuperMetaDataPrefsKey = "AbxrSuperMetaData";
        private readonly Dictionary<string, string> _superMetaData = new();
        private static readonly HashSet<string> ReservedKeys = new()
            { "module", "moduleName", "moduleId", "moduleOrder" };

        // ── Duration tracking ────────────────────────────────────────
        private readonly Dictionary<string, DateTime> _assessmentStartTimes = new();
        private readonly Dictionary<string, DateTime> _objectiveStartTimes = new();
        private readonly Dictionary<string, DateTime> _interactionStartTimes = new();
        private readonly Dictionary<string, DateTime> _levelStartTimes = new();
        
        // Event start times for duration tracking
        private readonly Dictionary<string, DateTime> _timedEventStartTimes = new();
	
        // Lock for thread-safe access to assessment start times
        private readonly object _assessmentStartTimesLock = new();
	
        // Track whether any assessment has been started (either DEFAULT or user-initiated)
        private static bool _assessmentStarted;
        
        public static readonly long StartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        private Coroutine _delayedStartCoroutine;
        private Coroutine _exitAfterAssessmentCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GetComponent<TrackTargetGaze>() == null)
                gameObject.AddComponent<TrackTargetGaze>();

            var jsonVersion = typeof(JsonConvert).Assembly.GetName().Version;
            Debug.Log($"[AbxrLib] Using Newtonsoft.Json version: {jsonVersion}");

            if (jsonVersion < new Version(13, 0, 0))
            {
	            Debug.LogError("[AbxrLib] Incompatible Newtonsoft.Json version loaded.");
            }

            // Create services
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Configuration.Instance.enableArborServiceClient)
            {
                _arborClient = new ArborServiceClient();
                // Start bind early so it can complete while the scene loads; auth will wait for ready in a coroutine.
                _arborClient.Initialize();
            }
            if (Configuration.Instance.enableArborInsightServiceClient)
                _arborInsightService = new ArborInsightServiceClient();
            if (_arborInsightService != null)
                _arborInsightService.Start();
#endif
            _authService = new AbxrAuthService(this, _arborClient);
            _dataService = new AbxrDataService(_authService, this);
            _telemetryService = new AbxrTelemetryService(this);
            _aiProxyApi = new AIProxyApi(_authService);
            _storageService = new AbxrStorageService(_authService, this);
            
            // Subscribe to OnAuthCompleted to start delayed DEFAULT assessment timer
            Abxr.OnAuthCompleted += OnAuthCompletedHandler;

            // Wire auth callbacks. Our internal keyboard handling is the initial OnInputRequested handler
            // so the native keyboard (including when the user replaces prefabs in Configuration) uses the exact same flow.
            _authService.OnInputRequested = PresentKeyboard;
            _authService.OnSucceeded = () => HandleAuthCompleted(true);
            _authService.OnFailed = error =>
            {
                Debug.LogWarning($"[AbxrLib] Auth failed: {error}");
                HandleAuthCompleted(false);
            };

            LoadSuperMetaData();
            
            _sceneChangeDetector = new SceneChangeDetector();
            _sceneChangeDetector.Start();
            
            _headsetDetector = new HeadsetDetector(_authService, this);
            _headsetDetector.Start();
            
            _dataService.Start();
            KeyboardManager.AuthService = _authService;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            QRCodeReader.AuthService = _authService;
#if PICO_ENTERPRISE_SDK_3
            QRCodeReaderPico.AuthService = _authService;
#endif
#endif

            // Auto-start auth
            var settings = Configuration.Instance;
            if (settings.enableAutoStartAuthentication)
            {
                if (settings.authenticationStartDelay > 0)
                {
	                Invoke(nameof(DoAuthenticate), settings.authenticationStartDelay);
                }
                else
                {
	                DoAuthenticate();
                }
            }
            else
            {
                Debug.Log("[AbxrLib] Auto-start auth is disabled. Call Abxr.StartAuthentication() manually when ready.");
            }

            // Telemetry collector
            if (settings.enableAutomaticTelemetry)
            {
                _telemetryService.Start();
            }
            
            Debug.Log($"[AbxrLib] Version {AbxrLibVersion.Version} Initialized.");
        }

        private void OnDestroy()
        {
            _authService?.Shutdown();
            _dataService?.Stop();
            _telemetryService?.Stop();
            _sceneChangeDetector?.Stop();
            _headsetDetector?.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            _arborClient?.Shutdown();
            _arborInsightService?.Stop();
#endif
            
            if (_delayedStartCoroutine != null)
            {
	            StopCoroutine(_delayedStartCoroutine);
	            _delayedStartCoroutine = null;
            }
            
            if (_exitAfterAssessmentCoroutine != null)
            {
	            StopCoroutine(_exitAfterAssessmentCoroutine);
	            _exitAfterAssessmentCoroutine = null;
            }

            if (Instance == this) Instance = null;
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
	        if (!hasFocus) SendAll();
        }
        
        private void OnApplicationQuit()
        {
	        Debug.Log("[AbxrLib] Application quitting, automatically closing running events");
	        CloseRunningEvents();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_authService.UsingArborInsightServiceForData())
            {
                ArborInsightServiceClient.ForceSendUnsent();
            }
            else
#endif
            {
                SendAll();
            }
        }
        
        internal void DoAuthenticate() => _authService.Authenticate();

        internal void SubmitInput(string input)
        {
            _authService.SubmitInput(input);
        }

        internal bool IsQRScanForAuthAvailable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.Instance != null) return true;
#endif
            return QRCodeReader.Instance != null && QRCodeReader.Instance.IsQRScanningAvailable();
#else
            return false;
#endif
        }

        internal void StartQRScanForAuthInput(Action<string> onResult)
        {
            if (onResult == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.Instance != null)
            {
                QRCodeReaderPico.Instance.SetScanResultCallback(onResult);
                QRCodeReaderPico.Instance.ScanQRCode();
                return;
            }
#endif
            if (QRCodeReader.Instance != null && QRCodeReader.Instance.IsQRScanningAvailable())
            {
                QRCodeReader.Instance.SetScanResultCallback(onResult);
                QRCodeReader.Instance.ScanQRCode();
                return;
            }
#endif
            onResult(null);
        }

        internal void CancelQRScanForAuthInput()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.Instance != null)
            {
                QRCodeReaderPico.Instance.CancelScanForAuthInput();
                return;
            }
#endif
            if (QRCodeReader.Instance != null && QRCodeReader.Instance.IsScanning())
                QRCodeReader.Instance.CancelScanning();
#endif
        }

        internal Texture GetQRScanCameraTexture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            // Pico uses platform scanner UI; there is no embeddable camera texture.
            if (QRCodeReaderPico.Instance != null) return null;
#endif
            return QRCodeReader.Instance?.GetCameraTexture();
#else
            return null;
#endif
        }

        private void HandleAuthCompleted(bool success)
        {
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
	        
	        Abxr.OnAuthCompleted?.Invoke(success, null);
	        if (!success) return;

	        var modules = _authService.ResponseData.Modules;
	        if (modules == null || modules.Count == 0) return;

	        if (Abxr.OnModuleTarget == null)
	        {
		        Debug.LogError("[AbxrLib] Subscribe to OnModuleTarget before running modules");
		        return;
	        }

	        if (Configuration.Instance.enableAutoStartModules && _currentModuleIndex < modules.Count)
	        {
		        Abxr.OnModuleTarget.Invoke(modules[_currentModuleIndex].Target);
	        }
        }
        
		internal Dictionary<string, string> GetUserData()
		{
			if (!_authService.Authenticated) return null;
			
			var authResponse = _authService.ResponseData;
			if (authResponse == null) return null;
			
			// Build a copy of UserData (or empty dict) and always include userId (from top-level or fallbacks)
			var userData = authResponse.UserData != null
				? new Dictionary<string, string>(authResponse.UserData)
				: new Dictionary<string, string>();
			var userIdStr = authResponse.UserId?.ToString();
			if (string.IsNullOrEmpty(userIdStr))
			{
				// Fallback: fill userId from other fields in userData, in priority order
				// 1) userId / id / userName variants
				userIdStr = GetFirstNonEmpty(userData, "userId", "id", "userName", "username", "user_name", "user")
					// 2) email variants
					?? GetFirstNonEmpty(userData, "email", "emailAddress", "email_address", "e-mail", "e_mail")
					// 3) full name variants or merge of first & last
					?? GetFirstNonEmpty(userData, "fullname", "fullName", "full_name", "name", "displayName", "display_name");
				if (string.IsNullOrEmpty(userIdStr))
				{
					var first = GetFirstNonEmpty(userData, "firstName", "first_name", "first", "givenName", "given_name");
					var last = GetFirstNonEmpty(userData, "lastName", "last_name", "last", "surname", "familyName", "family_name");
					if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last))
						userIdStr = (first + " " + last).Trim();
					else if (!string.IsNullOrEmpty(first))
						userIdStr = first;
					else if (!string.IsNullOrEmpty(last))
						userIdStr = last;
				}
			}
			// Always set userId in the dictionary so callers can rely on the key being present
			userData["userId"] = userIdStr ?? "";
			return userData;
		}

		/// <summary>Returns the full auth response from the last successful authentication (Token, UserData, AppId, Modules, PackageName, etc.). Null if not authenticated.</summary>
		internal AuthResponse GetAuthResponse() => _authService?.ResponseData;

		/// <summary>Returns the first non-empty value for any of the given keys (case-sensitive).</summary>
		private static string GetFirstNonEmpty(Dictionary<string, string> dict, params string[] keys)
		{
			if (dict == null) return null;
			foreach (var key in keys)
			{
				if (dict.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
					return v.Trim();
			}
			return null;
		}

		internal void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null) =>
			_authService.SetUserData(userId, additionalUserData);
		
		internal void StartAuthentication() => _authService.Authenticate();

		/// <summary>
		/// Get or set the single OnInputRequested handler (forwarded to auth service). Handler receives (type, prompt, domain, error).
		/// </summary>
		internal Action<string, string, string, string> OnInputRequested
		{
			get => _appOnInputRequested;
			set
			{
				_appOnInputRequested = value;
				if (_authService != null)
					_authService.OnInputRequested = value != null ? (type, prompt, domain, error) => value(type, prompt, domain, error) : null;
			}
		}
		
		internal void StartNewSession()
		{
			_authService.SetSessionId(Guid.NewGuid().ToString());
			_authService.Authenticate();
		}
		
		internal bool StartModuleAtIndex(int moduleIndex)
		{
			var response = _authService.ResponseData;
			if (response?.Modules == null || response.Modules.Count == 0)
			{
				Debug.LogError("[AbxrLib] No modules available");
				return false;
			}
		
			if (moduleIndex >= response.Modules.Count || moduleIndex < 0)
			{
				Debug.LogError($"[AbxrLib] Invalid module index - {moduleIndex}");
				return false;
			}

			if (Abxr.OnModuleTarget == null)
			{
				Debug.LogError("[AbxrLib] Need to subscribe to OnModuleTarget before running modules");
				return false;
			}
		
			_currentModuleIndex = moduleIndex;
			Abxr.OnModuleTarget.Invoke(response.Modules[_currentModuleIndex].Target);
			return true;
		}

		/// <summary>
		/// Advance to the next module in the sequence after current module completion.
		/// Called automatically from EventAssessmentComplete() when in a module sequence.
		/// Advances the module index and triggers the next module, or exits if all modules are complete.
		/// </summary>
		private void AdvanceToNextModule()
		{
			_currentModuleIndex++;
			if (_currentModuleIndex < _authService.ResponseData.Modules.Count)
			{
				Debug.Log($"[AbxrLib] Module '{_authService.ResponseData.Modules[_currentModuleIndex-1].Name}' complete. " +
				          $"Advancing to next module - '{_authService.ResponseData.Modules[_currentModuleIndex].Name}'");
				Abxr.OnModuleTarget?.Invoke(_authService.ResponseData.Modules[_currentModuleIndex].Target);
			}
			else
			{
				Abxr.OnAllModulesCompleted?.Invoke();
				Debug.Log("[AbxrLib] All modules complete");
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
		private void SetModule(string module, string moduleName = null)
		{
			// Check if we're using auth-provided module targets
			var response = _authService.ResponseData;
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
		
		internal List<ModuleData> GetModuleList() => _authService.ResponseData.Modules;
		
		internal void Log(string logMessage, Abxr.LogLevel logLevel, Dictionary<string, string> metadata)
		{
			metadata ??= new Dictionary<string, string>();
			metadata["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
			// Add super metadata to all logs
			metadata = MergeSuperMetaData(metadata);
		
			string logLevelString = logLevel switch
			{
				Abxr.LogLevel.Debug => "debug",
				Abxr.LogLevel.Info => "info",
				Abxr.LogLevel.Warn => "warn",
				Abxr.LogLevel.Error => "error",
				Abxr.LogLevel.Critical => "critical",
				_ => "info" // Default case
			};
		
			_dataService.AddLog(logLevelString, logMessage, metadata);
		}
        
        internal void TrackAutoTelemetry() => _telemetryService.Start();
        
        internal void Telemetry(string telemetryName, Dictionary<string, string> telemetryData)
        {
            telemetryData ??= new Dictionary<string, string>();
            telemetryData["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
            // Add super metadata to all telemetry entries
            telemetryData = MergeSuperMetaData(telemetryData);
		
            _dataService.AddTelemetry(telemetryName, telemetryData);
        }
        
        internal void Event(string eventName, Dictionary<string, string> metadata, bool sendTelemetry = true)
        {
            metadata ??= new Dictionary<string, string>();
            // Add gaze scores to event metadata when AbxrTargets exist (and send gaze telemetry)
            TrackTargetGaze.SendTargetGazeData(metadata);
            metadata["sceneName"] = SceneChangeDetector.CurrentSceneName;
		
            // Add super metadata to all events
            metadata = MergeSuperMetaData(metadata);
		
            // Add duration if this was a timed event (StartTimedEvent functionality)
            if (_timedEventStartTimes.ContainsKey(eventName) && !metadata.ContainsKey("duration"))
            {
                AddDuration(_timedEventStartTimes, eventName, metadata);
            }
		
            _dataService.AddEvent(eventName, metadata);
            if (sendTelemetry)
            {
	            _telemetryService.RecordLocationData();
	            _telemetryService.RecordSystemInfo();
            }
        }
        
        internal void Event(string eventName, Vector3 position, Dictionary<string, string> metadata)
        {
            metadata ??= new Dictionary<string, string>();
            metadata["position_x"] = position.x.ToString();
            metadata["position_y"] = position.y.ToString();
            metadata["position_z"] = position.z.ToString();
            Event(eventName, metadata);
        }
        
        internal void StartTimedEvent(string eventName) => _timedEventStartTimes[eventName] = DateTime.UtcNow;

        /// <summary>
		/// Private method to send the DEFAULT assessment start event
		/// Called automatically if no assessment is started within 1 minute of authentication
		/// or before any other event is sent
		/// </summary>
		private void DefaultEventAssessmentStart()
		{
			lock (_assessmentStartTimesLock)
			{
				// Only send if assessment hasn't been started yet
				if (_assessmentStarted) return;
				
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
        
		internal void EventAssessmentStart(string assessmentName, Dictionary<string, string> meta)
		{
			// Use lock to prevent race conditions with concurrent calls and NotifyAuthCompleted
			lock (_assessmentStartTimesLock)
			{
				// Skip if this assessment already exists
				if (_assessmentStartTimes.ContainsKey(assessmentName)) return;
				
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
		
		internal void EventAssessmentComplete(string assessmentName, int score, Abxr.EventStatus status, Dictionary<string, string> meta)
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
			if (_authService.ResponseData.Modules?.Count > 0 && Configuration.Instance.enableAutoAdvanceModules)
			{
				AdvanceToNextModule();
			}
			else
			{
				// Not in a module sequence - use original exit logic
				if (_authService.SessionUsedAuthHandoff() && Configuration.Instance.returnToLauncherAfterAssessmentComplete)
				{
					_exitAfterAssessmentCoroutine = StartCoroutine(ExitAfterAssessmentComplete());
				}
			}
		}
		
		internal void EventExperienceStart(string experienceName, Dictionary<string, string> meta) =>
			EventAssessmentStart(experienceName, meta);
		
		internal void EventExperienceComplete(string experienceName, Dictionary<string, string> meta) =>
			EventAssessmentComplete(experienceName, 100, Abxr.EventStatus.Complete, meta);

		/// <summary>
		/// Coroutine to exit the application after a 2-second delay when assessment is complete
		/// and the session used auth handoff with return to launcher enabled
		/// </summary>
		private IEnumerator ExitAfterAssessmentComplete()
		{
			SendAll();
			Debug.Log("[AbxrLib] Assessment complete with auth handoff - returning to launcher in 2 seconds");
			yield return new WaitForSeconds(2f);
	#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
	#else
			Application.Quit();
	#endif
		}
		
		internal void EventObjectiveStart(string objectiveName, Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "objective";
			meta["verb"] = "started";
			_objectiveStartTimes[objectiveName] = DateTime.UtcNow;
			Event(objectiveName, meta);
		}
		
		internal void EventObjectiveComplete(string objectiveName, int score, Abxr.EventStatus status, Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "objective";
			meta["verb"] = "completed";
			meta["score"] = score.ToString();
			meta["status"] = status.ToString().ToLower();
			AddDuration(_objectiveStartTimes, objectiveName, meta);
			Event(objectiveName, meta);
		}
		
		internal void EventInteractionStart(string interactionName, Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			meta["type"] = "interaction";
			meta["verb"] = "started";
			_interactionStartTimes[interactionName] = DateTime.UtcNow;
			Event(interactionName, meta);
		}
		
		internal void EventInteractionComplete(string interactionName, Abxr.InteractionType type, Abxr.InteractionResult result, string response, Dictionary<string, string> meta)
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
		
		internal void EventLevelStart(string levelName, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["verb"] = "started";
			meta["id"] = levelName;
			_levelStartTimes[levelName] = DateTime.UtcNow;
			Event("level_start", meta);
		}
		
		internal void EventLevelComplete(string levelName, int score, Dictionary<string, string> meta = null)
		{
			meta ??= new Dictionary<string, string>();
			meta["verb"] = "completed";
			meta["id"] = levelName;
			meta["score"] = score.ToString();
			AddDuration(_levelStartTimes, levelName, meta);
			Event("level_complete", meta);
		}
		
		internal void EventCritical(string label, Dictionary<string, string> meta)
		{
			string taggedName = $"CRITICAL_ABXR_{label}";
			Event(taggedName, meta);
		}
        
        internal string GetDeviceId() =>
			_overrideDeviceId != null ? _overrideDeviceId : (_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetDeviceId() : "");
        
		internal string GetDeviceSerial() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetDeviceSerial() : "";
		
		internal string GetDeviceTitle() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetDeviceTitle() : "";
		
		internal string[] GetDeviceTags() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetDeviceTags() : null;
		
		internal void SetOrgId(string orgId) => _overrideOrgId = orgId;
		internal void SetAuthSecret(string authSecret) => _overrideAuthSecret = authSecret;
		internal void SetDeviceId(string deviceId) => _overrideDeviceId = deviceId;

		internal string GetOrgId() =>
			_overrideOrgId != null ? _overrideOrgId : (_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetOrgId() : Configuration.Instance?.orgID ?? "");
		
		internal string GetOrgTitle() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetOrgTitle() : "";
		
		internal string GetOrgSlug() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetOrgSlug() : "";
		
		internal string GetMacAddressFixed() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetMacAddressFixed() : "";
		
		internal string GetMacAddressRandom() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetMacAddressRandom() : "";
		
		internal bool GetIsAuthenticated() =>
			_arborClient != null && _arborClient.IsConnected() && _arborClient.ServiceWrapper != null && _arborClient.ServiceWrapper.GetIsAuthenticated();
		
		internal string GetAccessToken() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetAccessToken() : "";
		
		internal string GetRefreshToken() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetRefreshToken() : "";
		
		internal DateTime? GetExpiresDateUtc() =>
			_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetExpiresDateUtc() : DateTime.MinValue;
		
		internal string GetFingerprint() =>
			_overrideAuthSecret != null ? _overrideAuthSecret : (_arborClient != null && _arborClient.IsConnected() ? _arborClient.ServiceWrapper?.GetFingerprint() : "");
		
		internal IEnumerator StorageGetDefaultEntry(Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			yield return _storageService.Get("state", scope, callback);
		}
		
		internal IEnumerator StorageGetEntry(string entryName, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			yield return _storageService.Get(entryName, scope, callback);
		}
		
		internal void StorageSetDefaultEntry(Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
		{
			// Check if basic authentication is ready
			if (!_authService.Authenticated) return;
			
			// For user-scoped storage, we need a user to actually be logged in
			// For device-scoped storage, app-level authentication should be sufficient
			if (scope == Abxr.StorageScope.User && _authService.ResponseData.UserId == null)
			{
				// User-scoped storage requires a user to be logged in, defer this request
				return;
			}
			
			_storageService.Add("state", entry, scope, policy);
		}
		
		internal void StorageSetEntry(string entryName, Dictionary<string, string> entryData, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
		{
			// Check if basic authentication is ready
			if (!_authService.Authenticated) return;
			
			// For user-scoped storage, we need a user to actually be logged in
			// For device-scoped storage, app-level authentication should be sufficient
			var authResponse = _authService.ResponseData;
			if (scope == Abxr.StorageScope.User && (authResponse == null || authResponse.UserId == null))
			{
				// User-scoped storage requires a user to be logged in, defer this request
				return;
			}
			
			_storageService.Add(entryName, entryData, scope, policy);
		}

		internal void StorageRemoveDefaultEntry(Abxr.StorageScope scope)
		{
			StartCoroutine(_storageService.Delete(scope, "state"));
		}

		internal void StorageRemoveEntry(string entryName, Abxr.StorageScope scope)
		{
			StartCoroutine(_storageService.Delete(scope, entryName ?? ""));
		}

		internal void StorageRemoveMultipleEntries(Abxr.StorageScope scope)
		{
			StartCoroutine(_storageService.Delete(scope, ""));
		}

		internal IEnumerator AIProxy(string prompt, string llmProvider, Action<string> callback)
		{
			yield return _aiProxyApi.SendPrompt(prompt, llmProvider, null, callback);
		}
		
		internal IEnumerator AIProxy(string prompt, List<string> pastMessages, string llmProvider, Action<string> callback)
		{
			yield return _aiProxyApi.SendPrompt(prompt, llmProvider, pastMessages, callback);
		}
		
		internal void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses, Action<string> callback)
		{
			// Validate prompt
			if (string.IsNullOrWhiteSpace(prompt))
			{
				Debug.LogError("[AbxrLib] Poll prompt cannot be null or empty");
				return;
			}

			if (pollType == ExitPollHandler.PollType.MultipleChoice)
			{
				if (responses == null)
				{
					Debug.LogError("[AbxrLib] List of responses required for multiple choice poll");
					return;
				}

				if (responses.Count is < 2 or > 8)
				{
					Debug.LogError("[AbxrLib] Multiple choice poll must have at least two and no more than 8 responses");
					return;
				}

				// Validate that all responses are not null or empty
				for (int i = 0; i < responses.Count; i++)
				{
					if (string.IsNullOrWhiteSpace(responses[i]))
					{
						Debug.LogError($"[AbxrLib] Response at index {i} cannot be null or empty");
						return;
					}
				}
			}

			ExitPollHandler.AddPoll(prompt, pollType, responses, callback);
		}
		
		/// <summary>
		/// Handler for OnAuthCompleted event that starts the delayed DEFAULT assessment timer
		/// Only starts the timer if a DEFAULT assessment was set up during authentication
		/// </summary>
		/// <param name="success">Whether authentication succeeded</param>
		/// <param name="error">Error message if authentication failed</param>
		private void OnAuthCompletedHandler(bool success, string error)
		{
			// Only proceed if authentication succeeded
			if (!success) return;
		
			// Check if we need to start the delayed DEFAULT assessment timer
			lock (_assessmentStartTimesLock)
			{
				if (_assessmentStartTimes.ContainsKey("DEFAULT") && !_assessmentStarted)
				{
					_delayedStartCoroutine = StartCoroutine(DelayedDefaultAssessmentStart());
				}
			}
		}
		
		/// <summary>
		/// Coroutine to send DEFAULT assessment start event after 1 minute if no assessment has been started
		/// This gives developers time to start their own assessment before the DEFAULT one is sent
		/// </summary>
		private IEnumerator DelayedDefaultAssessmentStart()
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
		
		internal void Register(string key, string value, bool overwrite = true)
		{
			if (IsReservedSuperMetaDataKey(key))
			{
				string errorMessage = $"[AbxrLib] Cannot register super metadata with reserved key '{key}'. Reserved keys are: module, moduleName, moduleId, moduleOrder";
				Debug.LogWarning(errorMessage);
				Abxr.LogInfo(errorMessage, new Dictionary<string, string> { 
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
		
		internal void RegisterOnce(string key, string value) => Register(key, value, false);
		
		internal void Unregister(string key)
		{
			_superMetaData.Remove(key);
			SaveSuperMetaData();
		}
		
		internal void Reset()
		{
			_superMetaData.Clear();
			SaveSuperMetaData();
		}
		
		internal Dictionary<string, string> GetSuperMetaData() => new(_superMetaData);

		/// <summary>
		/// Private helper function to merge super metadata and module info into metadata
		/// Ensures data-specific metadata take precedence over super metadata and module info
		/// </summary>
		/// <param name="meta">The metadata dictionary to merge super metadata into</param>
		/// <returns>The metadata dictionary with super metadata and module info merged</returns>
		private Dictionary<string, string> MergeSuperMetaData(Dictionary<string, string> meta)
		{
			meta ??= new Dictionary<string, string>();
			
			// If LMS modules exist, inject current module metadata unless the event already specifies it.
			// (Data-specific metadata takes precedence.)
			if (_authService.ResponseData.Modules?.Count > 0 && _currentModuleIndex < _authService.ResponseData.Modules?.Count)
			{
				ModuleData moduleData = _authService.ResponseData.Modules[_currentModuleIndex];
				// these are potentially sharing developer private information. Disabling for now
				//if (!meta.ContainsKey("module")) meta["module"] = moduleData.Target;
				//if (!meta.ContainsKey("moduleId")) meta["moduleId"] = moduleData.Id;
				//if (!meta.ContainsKey("moduleOrder")) meta["moduleOrder"] = moduleData.Order.ToString();
				if (!meta.ContainsKey("moduleName")) meta["moduleName"] = moduleData.Name;
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
		private static bool IsReservedSuperMetaDataKey(string key) => ReservedKeys.Contains(key);

		private void LoadSuperMetaData()
		{
			string json = PlayerPrefs.GetString(SuperMetaDataPrefsKey, "{}");
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
				Debug.LogError($"[AbxrLib] Failed to load super metadata: {ex.Message}\n" +
				               $"Exception Type: {ex.GetType().Name}\n" +
				               $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
			}
		}

		private void SaveSuperMetaData()
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
				PlayerPrefs.SetString(SuperMetaDataPrefsKey, json);
				PlayerPrefs.Save();
			}
			catch (Exception ex)
			{
				// Log error with consistent format and include stack trace for debugging
				Debug.LogError($"[AbxrLib] Failed to save super metadata: {ex.Message}\n" +
				               $"Exception Type: {ex.GetType().Name}\n" +
				               $"Stack Trace: {ex.StackTrace ?? "No stack trace available"}");
			}
		}
		
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

		private void PresentKeyboard(string type, string prompt, string domain, string error)
		{
			string displayPrompt = string.IsNullOrEmpty(error) ? (prompt ?? "") : $"{error}\n{prompt ?? ""}";
			if (type is "text" or null or "")
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
				KeyboardHandler.SetPrompt(!string.IsNullOrEmpty(displayPrompt) ? displayPrompt : "Enter Your Login");
			}
			else if (type == "pin")
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);
				KeyboardHandler.SetPrompt(!string.IsNullOrEmpty(displayPrompt) ? displayPrompt : "Enter your 6-digit PIN");
			}
			else if (type == "email")
			{
				KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
				string emailPrompt = !string.IsNullOrEmpty(prompt)
					? $"{displayPrompt} \n(<u>username</u>@{domain ?? ""})"
					: $"Enter your email username\n(<u>username</u>@{domain ?? ""})";
				KeyboardHandler.SetPrompt(emailPrompt);
			}
		}
		
		/// <summary>
		/// Gets a copy of the current assessment start times for application quit handling.
		/// This method provides safe access to private timing data without using reflection.
		/// </summary>
		/// <returns>Copy of the assessment start times dictionary</returns>
		private Dictionary<string, DateTime> GetAssessmentStartTimes()
		{
			lock (_assessmentStartTimesLock)
			{
				return new Dictionary<string, DateTime>(_assessmentStartTimes);
			}
		}
		
		/// <summary>
		/// Clears all timing dictionaries. Used by application quit handler to clean up after processing.
		/// This method provides safe access to private timing data without using reflection.
		/// </summary>
		private void ClearAllStartTimes()
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
		
		/// <summary>
        /// Automatically complete all running Assessments, Objectives, and Interactions
        /// Uses Incomplete status to indicate the events were terminated due to application quit
        /// Processing order: Interactions → Objectives → Assessments (hierarchical order)
        /// </summary>
        private void CloseRunningEvents()
        {
            // Get references to the static dictionaries using safe public methods
            var runningAssessmentTimes = GetAssessmentStartTimes();
            var runningObjectiveTimes = new Dictionary<string, DateTime>(_objectiveStartTimes);
            var runningInteractionTimes = new Dictionary<string, DateTime>(_interactionStartTimes);

            int totalClosed = 0;

            // Close running Interactions first (lowest level)
            if (runningInteractionTimes.Count > 0)
            {
                var interactionNames = new List<string>(runningInteractionTimes.Keys);
                foreach (string interactionName in interactionNames)
                {
                    Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Null, Abxr.InteractionResult.Neutral, "",
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            // Close running Objectives second (middle level)
            if (runningObjectiveTimes.Count > 0)
            {
                var objectiveNames = new List<string>(runningObjectiveTimes.Keys);
                foreach (string objectiveName in objectiveNames)
                {
                    Abxr.EventObjectiveComplete(objectiveName, 0, Abxr.EventStatus.Incomplete,
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            // Close running Assessments last (highest level)
            if (runningAssessmentTimes != null && runningAssessmentTimes.Count > 0)
            {
                var assessmentNames = new List<string>(runningAssessmentTimes.Keys);
                foreach (string assessmentName in assessmentNames)
                {
                    Abxr.EventAssessmentComplete(assessmentName, 0, Abxr.EventStatus.Fail, 
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            if (totalClosed > 0)
            {
                // Clear all start times since we've processed them
                ClearAllStartTimes();
                
                // Force immediate send of all events with maximum redundancy for VR reliability
                SendAll();
                
                // Log the cleanup activity
                Abxr.Log($"Application quit handler closed {totalClosed} running events", Abxr.LogLevel.Info, 
                    new Dictionary<string, string> 
                    { 
                        ["events_closed"] = totalClosed.ToString(),
                        ["quit_handler"] = "automatic"
                    });
            }
        }
		
        private void SendAll()
        {
	        _dataService.ForceSend();
	        _storageService.ForceSend();
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
	}
}
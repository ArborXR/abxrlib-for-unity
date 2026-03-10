using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.AI;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.Services.Data;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Telemetry;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Services.Transport;
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

        /// <summary>For testing only. Resets static state that survives MonoBehaviour destruction.</summary>
        internal static void ResetStaticStateForTesting()
        {
            _assessmentStarted = false;
            _nextRuntimeAuthConfigForTesting = null;
            _simulateQuitInExitAfterAssessmentComplete = false;
        }

        /// <summary>For testing only. When true, ExitAfterAssessmentComplete() does not call EditorApplication.isPlaying = false / Application.Quit(); it logs and ends the coroutine so PlayMode tests can complete.</summary>
        internal static bool SimulateQuitInExitAfterAssessmentComplete
        {
            get => _simulateQuitInExitAfterAssessmentComplete;
            set => _simulateQuitInExitAfterAssessmentComplete = value;
        }
        private static bool _simulateQuitInExitAfterAssessmentComplete;

        /// <summary>For testing only. When set before CreateSubsystem(), applied as overrides when the auth service is created (e.g. enableAutoStartAuthentication = false) so the Configuration asset is not modified.</summary>
        internal static RuntimeAuthConfig NextRuntimeAuthConfigForTesting
        {
            get => _nextRuntimeAuthConfigForTesting;
            set => _nextRuntimeAuthConfigForTesting = value;
        }
        private static RuntimeAuthConfig _nextRuntimeAuthConfigForTesting;

        /// <summary>For testing only. Exposes the auth service so tests can simulate auth success.</summary>
        internal AbxrAuthService AuthServiceForTesting => _authService;

        /// <summary>For testing only. True when ArborMdmClient is available and connected (e.g. on Android device with MDM). Used to decide expected auth outcome in environment-dependent tests.</summary>
        internal bool IsArborMdmClientAvailableAndConnected => _arborMdmClient != null && _arborMdmClient.IsConnected();

        /// <summary>For testing only. True when transport selection has finished (REST or ArborInsights). Use before reading DeviceCanSupplyOrgCredentialForAuth so device/ArborInsights state is stable.</summary>
        internal bool TransportSelectionComplete => _transportSelectionComplete;

        /// <summary>For testing only. True when the device can supply org credentials for auth: MDM connected or ArborInsightsClient transport active (service supplies org). Read after transport selection is complete.</summary>
        internal bool DeviceCanSupplyOrgCredentialForAuth => IsArborMdmClientAvailableAndConnected || IsUsingArborInsightsTransport();

        /// <summary>For testing only. Exposes the data service so tests can inspect pending events/logs/telemetry.</summary>
        internal AbxrDataService DataServiceForTesting => _dataService;

        /// <summary>For testing only. REST transport when active; null when using ArborInsightsClient. Use for GetPending*ForTesting in PlayMode.</summary>
        internal AbxrTransportRest RestTransportForTesting => _transport as AbxrTransportRest;

        /// <summary>For testing only. Current transport (REST or ArborInsights). Use to check IsServiceTransport and call GetPending*ForTesting on any transport.</summary>
        internal IAbxrTransport GetTransportForTesting() => _transport;

        /// <summary>For testing only. Pending events from current transport; empty list when transport is null or service (device).</summary>
        internal List<EventPayload> GetPendingEventsForTesting() => _transport?.GetPendingEventsForTesting() ?? new List<EventPayload>();
        /// <summary>For testing only. Pending logs from current transport; empty when null or service.</summary>
        internal List<LogPayload> GetPendingLogsForTesting() => _transport?.GetPendingLogsForTesting() ?? new List<LogPayload>();
        /// <summary>For testing only. Pending telemetry from current transport; empty when null or service.</summary>
        internal List<TelemetryPayload> GetPendingTelemetryForTesting() => _transport?.GetPendingTelemetryForTesting() ?? new List<TelemetryPayload>();

        // ── Services ─────────────────────────────────────────────────
        private AbxrAuthService _authService;
        private AbxrDataService _dataService;
        private AbxrTelemetryService _telemetryService;
        private ArborMdmClient _arborMdmClient;
#if UNITY_ANDROID && !UNITY_EDITOR
	    private ArborInsightsClient _arborInsightsClient;
#endif
        private AbxrStorageService _storageService;
        private volatile IAbxrTransport _transport;
        private bool _transportSelectionComplete;
        private Coroutine _transportSelectionCoroutine;
        private AIProxyApi _aiProxyApi;
        private SceneChangeDetector _sceneChangeDetector;
        private HeadsetDetector _headsetDetector;

        // ── Module state ─────────────────────────────────────────────
        private int _currentModuleIndex;
        private Action<string, string, string, string> _appOnInputRequested;

        // Developer-supplied overrides (bypass ArborMdmClient); null = not set.
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

        private static readonly WaitForSecondsRealtime TransportPollWait = new WaitForSecondsRealtime(0.25f);
        private static readonly WaitForSecondsRealtime AuthStartPollWait = new WaitForSecondsRealtime(0.1f);

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

            if (jsonVersion < new Version(13, 0, 0))
            {
	            Logcat.Error("Incompatible Newtonsoft.Json version loaded.");
            }

            // Create services
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Configuration.Instance.enableArborMdmClient)
            {
                _arborMdmClient = new ArborMdmClient();
                // Start bind early so it can complete while the scene loads; auth will wait for ready in a coroutine.
                _arborMdmClient.Initialize();
            }
            if (Configuration.Instance.enableArborInsightsClient)
                _arborInsightsClient = new ArborInsightsClient();
            if (_arborInsightsClient != null)
                _arborInsightsClient.Start();
#endif
            _authService = new AbxrAuthService(this, _arborMdmClient);
            if (_nextRuntimeAuthConfigForTesting != null)
            {
                _authService.ApplyRuntimeAuthOverridesForTesting(_nextRuntimeAuthConfigForTesting);
                _nextRuntimeAuthConfigForTesting = null;
            }
            _transport = new AbxrTransportRest(_authService, this);
            _authService.SetTransportGetter(() => _transport);
            _dataService = new AbxrDataService(this, () => _transport);
            _telemetryService = new AbxrTelemetryService(this);
            _aiProxyApi = new AIProxyApi(_authService);
            _storageService = new AbxrStorageService(_authService, this, () => _transport);

            _transportSelectionComplete = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Configuration.Instance.enableArborInsightsClient && _arborInsightsClient != null)
                _transportSelectionCoroutine = StartCoroutine(WaitForTransportSelectionCoroutine());
            else
#endif
                _transportSelectionComplete = true;
            
            // Subscribe to OnAuthCompleted to start delayed DEFAULT assessment timer
            Abxr.OnAuthCompleted += OnAuthCompletedHandler;

            // Wire auth callbacks. Auth always invokes our dispatcher; we never call PresentKeyboard when the app has set OnInputRequested.
            _authService.OnInputRequested = OnInputRequestedDispatch;
            _authService.OnSucceeded = () => HandleAuthCompleted(true);
            _authService.OnFailed = error =>
            {
                Logcat.Error($"Authentication failure: {error}");
                HandleAuthCompleted(false, error);
            };
            _authService.OnUserDataSyncCompleted = (success, errorMsg) => Abxr.OnUserDataSyncCompleted?.Invoke(success, errorMsg);

            // Super metadata is per-session; clear any persisted value so we start fresh each run.
            PlayerPrefs.DeleteKey(SuperMetaDataPrefsKey);
            PlayerPrefs.Save();
            LoadSuperMetaData();
            
            _sceneChangeDetector = new SceneChangeDetector();
            _sceneChangeDetector.Start();
            
            _headsetDetector = new HeadsetDetector(_authService, this);
            _headsetDetector.Start();
            
            KeyboardManager.AuthService = _authService;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            QRCodeReader.AuthService = _authService;
#if PICO_ENTERPRISE_SDK_3
            QRCodeReaderPico.AuthService = _authService;
#endif
#endif

            // Log version/init result first so it always appears before any auth failure from the coroutine.
            var settings = Configuration.Instance;
            var configInvalid = !string.IsNullOrEmpty(Configuration.LastValidationErrorMessage);
            if (configInvalid)
            {
                var reason = Configuration.LastValidationErrorMessage ?? "required configuration missing or invalid";
                if (reason.StartsWith("Authentication error: ", StringComparison.Ordinal))
                    reason = reason.Substring("Authentication error: ".Length);
                Logcat.Error($"Version {AbxrLibVersion.Version} Initialization Failed: {reason}");
            }
            else
                Logcat.Info($"Version {AbxrLibVersion.Version} Initialized.");

            // Auto-start auth (gated on transport selection so first auth uses correct backend). Do not start when config validation failed.
            bool enableAutoStart = _authService.GetEnableAutoStartAuthentication();
            if (enableAutoStart && !configInvalid)
                StartCoroutine(AuthStartAfterTransportSelectionCoroutine(settings.authenticationStartDelay));
            else if (enableAutoStart && configInvalid)
                HandleAuthCompleted(false);
            else
                Logcat.Info("Auto-start auth is disabled. Call Abxr.StartAuthentication() manually when ready.");

            // Telemetry collector
            if (settings.enableAutomaticTelemetry)
            {
                _telemetryService.Start();
            }
        }

        private void OnDestroy()
        {
            Abxr.OnAuthCompleted -= OnAuthCompletedHandler;
            if (_transportSelectionCoroutine != null)
            {
                StopCoroutine(_transportSelectionCoroutine);
                _transportSelectionCoroutine = null;
            }
            _authService?.Shutdown();
            _telemetryService?.Stop();
            _sceneChangeDetector?.Stop();
            _headsetDetector?.Stop();
            if (_transport is AbxrTransportRest rest)
                rest.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            _arborMdmClient?.Shutdown();
            _arborInsightsClient?.Stop();
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

            _superMetaData.Clear();
            PlayerPrefs.DeleteKey(SuperMetaDataPrefsKey);
            PlayerPrefs.Save();

            if (Instance == this) Instance = null;
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
	        if (!hasFocus) SendAll();
        }
        
        private void OnApplicationQuit()
        {
            OnApplicationQuitHandler();
        }

        /// <summary>
        /// Ends the current session: closes running events, flushes data, calls transport OnQuit (REST: ForceSend; service: Unbind),
        /// clears pending batches, storage, super metadata, and auth state. Used by OnApplicationQuit and by Abxr.EndSession().
        /// Does not start a new session; call Abxr.StartAuthentication() when ready.
        /// </summary>
        internal void OnApplicationQuitHandler()
        {
	        Logcat.Info("Ending session: closing running events and flushing");
	        if (_delayedStartCoroutine != null) { StopCoroutine(_delayedStartCoroutine); _delayedStartCoroutine = null; }
	        if (_exitAfterAssessmentCoroutine != null) { StopCoroutine(_exitAfterAssessmentCoroutine); _exitAfterAssessmentCoroutine = null; }
	        CloseRunningEvents();
	        // Service transport: ForceSend (ForceSendUnsent) before Unbind. REST: no-op here; actual flush is in OnQuit() (sync).
	        SendAll();
            _transport?.OnQuit();
	        _transport?.ClearAllPending();
	        _superMetaData.Clear();
	        PlayerPrefs.DeleteKey(SuperMetaDataPrefsKey);
	        PlayerPrefs.Save();
	        _assessmentStarted = false;
	        AIProxyApi.ClearPastMessages();
	        _authService.ClearSessionAndPrepareForNew();
        }
        
        internal void DoAuthenticate() => _authService.Authenticate();

        private IEnumerator WaitForTransportSelectionCoroutine()
        {
            const int waitAttempts = 40;
            if (!ArborInsightsClient.IsServicePackageInstalled())
            {
                _transportSelectionComplete = true;
                yield break;
            }
            for (int i = 0; i < waitAttempts && !ArborInsightsClient.ServiceIsFullyInitialized(); i++)
                yield return TransportPollWait;
            if (ArborInsightsClient.ServiceIsFullyInitialized())
                SwitchToArborInsightsTransport();
            _transportSelectionComplete = true;
        }

        private void SwitchToArborInsightsTransport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_transport is AbxrTransportRest rest)
                rest.Stop();
            _transport = new AbxrTransportArborInsights();
            Logcat.Info("Switched to ArborInsightsClient transport.");
#endif
        }

        /// <summary>For testing only. True when current transport is ArborInsightsClient (Android only).</summary>
        private bool IsUsingArborInsightsTransport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return _transport is AbxrTransportArborInsights;
#else
            return false;
#endif
        }

        private IEnumerator AuthStartAfterTransportSelectionCoroutine(float delaySeconds)
        {
            while (!_transportSelectionComplete)
                yield return AuthStartPollWait;
            if (delaySeconds > 0)
                yield return new WaitForSeconds(delaySeconds);
            DoAuthenticate();
        }

        internal void SubmitInput(string input)
        {
            _authService.SubmitInput(input);
        }

        /// <summary>True when QR scanning is available on this device and the SDK is currently waiting for auth input (OnInputRequested was invoked). Use this to show/hide the "Scan QR" option; when true, OnInputSubmitted will be accepted after a scan.</summary>
        internal bool IsQRScanForAuthAvailable()
        {
            if (_authService == null || !_authService.IsInputRequestPending) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.IsAvailable) return true;
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
            if (QRCodeReaderPico.IsAvailable)
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
            if (QRCodeReaderPico.IsAvailable)
            {
                QRCodeReaderPico.Instance.CancelScanForAuthInput();
                return;
            }
#endif
            if (QRCodeReader.Instance != null && QRCodeReader.Instance.IsScanning())
                QRCodeReader.Instance.CancelScanning();
#endif
        }

        /// <summary>True when the app can choose where to display the QR camera feed (non-Pico). When true, use GetQRScanCameraTexture() and assign to your own RawImage. When false (Pico), the platform shows its own scanner UI.</summary>
        internal bool IsQRScanCameraTexturePlaceable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.IsAvailable) return false;
#endif
            return QRCodeReader.Instance != null && QRCodeReader.Instance.IsQRScanningAvailable();
#else
            return false;
#endif
        }

        internal Texture GetQRScanCameraTexture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            // Pico uses platform scanner UI; there is no embeddable camera texture.
            if (QRCodeReaderPico.IsAvailable) return null;
#endif
            return QRCodeReader.Instance?.GetCameraTexture();
#else
            return null;
#endif
        }

        private void HandleAuthCompleted(bool success, string errorMessage = null)
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
	        
	        Abxr.OnAuthCompleted?.Invoke(success, errorMessage);
	        if (!success) return;

	        var modules = _authService.ResponseData.Modules;
	        if (modules == null || modules.Count == 0) return;

	        if (Abxr.OnModuleTarget == null)
	        {
		        Logcat.Error("Subscribe to OnModuleTarget before running modules");
		        return;
	        }

	        if (_authService.GetEffectiveEnableAutoStartModules() && _currentModuleIndex < modules.Count)
	        {
		        Abxr.OnModuleTarget.Invoke(modules[_currentModuleIndex].Target);
	        }
        }
        
		internal Dictionary<string, string> GetUserData()
		{
			if (!_authService.Authenticated) return null;
			var authResponse = _authService.ResponseData;
			if (authResponse == null) return null;
			return authResponse.UserData != null
				? new Dictionary<string, string>(authResponse.UserData)
				: new Dictionary<string, string>();
		}

		internal void SetUserData(string userId = null, Dictionary<string, string> additionalUserData = null) =>
			_authService.SetUserData(userId, additionalUserData);

		/// <summary>Returns the session userId (read-only, set by backend). Do not document for now.</summary>
		internal string GetAnonymizedUserId() => _authService?.ResponseData?.UserId?.ToString();

		/// <summary>Returns the full auth response from the last successful authentication (Token, UserData, AppId, Modules, PackageName, etc.). Null if not authenticated.</summary>
		internal AuthResponse GetAuthResponse() => _authService?.ResponseData;

		/// <summary>Launches another Android app and passes the current auth session via the auth_handoff intent extra.
		/// The target app must also use AbxrLib; it will adopt the session without re-authenticating.
		/// Call this in your OnAuthCompleted handler using PackageName from GetAuthResponse().
		/// When includeReturnToPackage is true, the handoff includes ReturnToPackage (this app's identifier) so the receiving app can return the session when assessment completes.</summary>
		internal bool LaunchAppWithAuthHandoff(string packageName, bool includeReturnToPackage = false)
		{
			if (string.IsNullOrEmpty(packageName))
			{
				Logcat.Warning("LaunchAppWithAuthHandoff: packageName is empty");
				return false;
			}
			if (!(_authService?.Authenticated ?? false))
			{
				Logcat.Warning("LaunchAppWithAuthHandoff: not authenticated");
				return false;
			}
#if UNITY_ANDROID && !UNITY_EDITOR
			try
			{
				string handoffJson = _authService.GetHandoffJson(includeReturnToPackage);
				if (handoffJson == null)
				{
					Logcat.Warning("LaunchAppWithAuthHandoff: failed to build handoff payload");
					return false;
				}
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var intent = new AndroidJavaObject("android.content.Intent");
				using var intentClass = new AndroidJavaClass("android.content.Intent");
				intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_MAIN"));
				intent.Call<AndroidJavaObject>("setPackage", packageName);
				intent.Call<AndroidJavaObject>("putExtra", "auth_handoff", handoffJson);
				intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK"));
				activity.Call("startActivity", intent);
				Logcat.Info($"Launched '{packageName}' with auth handoff");
				return true;
			}
			catch (Exception ex)
			{
				Logcat.Error($"LaunchAppWithAuthHandoff failed for '{packageName}': {ex.Message}");
				return false;
			}
#else
			Logcat.Warning("LaunchAppWithAuthHandoff is only supported on Android");
			return false;
#endif
		}

		/// <summary>
		/// For testing only. Same validation and handoff payload as LaunchAppWithAuthHandoff but does not start an app;
		/// injects the handoff via SetAuthHandoffForTesting so the test can emulate "App 2" receiving it.
		/// Use when testing flows that would call LaunchAppWithAuthHandoff in production.
		/// </summary>
		/// <param name="packageName">Target package name (validated like production; used only for consistency).</param>
		/// <param name="includeReturnToPackage">If true, handoff includes ReturnToPackage so the receiving app can return the session.</param>
		/// <param name="useBase64Encoding">If true, inject the handoff as base64-encoded JSON so NormalizeHandoffPayload is exercised.</param>
		/// <returns>True if authenticated and handoff was injected, false otherwise.</returns>
		internal bool LaunchAppWithAuthHandoffForTest(string packageName, bool includeReturnToPackage = false, bool useBase64Encoding = false)
		{
			if (string.IsNullOrEmpty(packageName))
			{
				Logcat.Warning("LaunchAppWithAuthHandoffForTest: packageName is empty");
				return false;
			}
			if (!(_authService?.Authenticated ?? false))
			{
				Logcat.Warning("LaunchAppWithAuthHandoffForTest: not authenticated");
				return false;
			}
			string handoffJson = _authService.GetHandoffJson(includeReturnToPackage);
			if (handoffJson == null)
			{
				Logcat.Warning("LaunchAppWithAuthHandoffForTest: failed to build handoff payload");
				return false;
			}
			string payload = useBase64Encoding
				? Convert.ToBase64String(Encoding.UTF8.GetBytes(handoffJson))
				: handoffJson;
			AbxrAuthService.SetAuthHandoffForTesting(payload);
			Logcat.Info($"LaunchAppWithAuthHandoffForTest: injected handoff for '{packageName}'" + (useBase64Encoding ? " (base64)" : ""));
			return true;
		}

		/// <summary>For testing only. Returns the handoff JSON that would be sent by LaunchAppWithAuthHandoff (e.g. so tests can simulate App 2 returning the session to App 1).</summary>
		internal string GetHandoffJsonForTesting(bool includeReturnToPackage = false) => _authService?.GetHandoffJson(includeReturnToPackage);

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
		
		/// <summary>Starts authentication. Waits for transport selection to complete first so the first auth uses the correct backend (service if available, else REST).</summary>
		internal void StartAuthentication() => StartCoroutine(AuthStartAfterTransportSelectionCoroutine(0f));

		/// <summary>True when the SDK is waiting for auth input (OnInputRequested was invoked). Use with device-specific QR availability to show "Scan QR" only when it will be accepted (e.g. Meta: IsAuthInputRequestPending() &amp;&amp; MetaQRCodeReader.Instance.IsQRScanningAvailable()).</summary>
		internal bool IsAuthInputRequestPending() => _authService != null && _authService.IsInputRequestPending;

		/// <summary>
		/// Get or set the single OnInputRequested handler. When set, auth input requests go to this handler; when null, PresentKeyboard is used.
		/// Auth service always invokes OnInputRequestedDispatch, which never calls PresentKeyboard if the app handler is set.
		/// </summary>
		internal Action<string, string, string, string> OnInputRequested
		{
			get => _appOnInputRequested;
			set => _appOnInputRequested = value;
		}

		private void OnInputRequestedDispatch(string type, string prompt, string domain, string error)
		{
			if (_appOnInputRequested != null)
				_appOnInputRequested(type, prompt, domain, error);
			else
				PresentKeyboard(type, prompt, domain, error);
		}
		
		/// <summary>
		/// Starts an entirely fresh session: clears all API tokens, auth state, and pending data; then re-authenticates.
		/// When using ArborInsightsClient (Android), unbinds and rebinds the service so the connection is fresh.
		/// Equivalent to the user closing the app and starting it again from a session perspective.
		/// </summary>
internal void StartNewSession()
        {
			// Clear in-memory batchers so no previous-session events/telemetry/logs/storage are sent with the new session.
			_transport?.ClearAllPending();

			// Super metadata is per-session; clear in-memory and persisted value.
			_superMetaData.Clear();
			PlayerPrefs.DeleteKey(SuperMetaDataPrefsKey);
			PlayerPrefs.Save();

			if (_delayedStartCoroutine != null) { StopCoroutine(_delayedStartCoroutine); _delayedStartCoroutine = null; }
			if (_exitAfterAssessmentCoroutine != null) { StopCoroutine(_exitAfterAssessmentCoroutine); _exitAfterAssessmentCoroutine = null; }
			_assessmentStarted = false;
			AIProxyApi.ClearPastMessages();

#if UNITY_ANDROID && !UNITY_EDITOR
			// When using ArborInsightsClient, unbind then bind to clear session-related connection state and get a fresh connection.
			if (_arborInsightsClient != null && ArborInsightsClient.IsServiceBound())
			{
				ArborInsightsClient.Unbind();
				ArborInsightsClient.Bind(null);
			}
#endif
			_authService.ClearSessionAndPrepareForNew();
			_authService.Authenticate(clearStateFirst: false);
		}

		/// <summary>
		/// Ends the current session (same as quit-time logic): closes running events, flushes, unbinds/flushes transport, clears pending data and auth state.
		/// Does not start a new session. Call Abxr.StartAuthentication() when ready to begin a fresh session.
		/// </summary>
		internal void EndSession()
		{
			OnApplicationQuitHandler();
		}
		
		internal bool StartModuleAtIndex(int moduleIndex)
		{
			var response = _authService.ResponseData;
			if (response?.Modules == null || response.Modules.Count == 0)
			{
				Logcat.Error("No modules available");
				return false;
			}
		
			if (moduleIndex >= response.Modules.Count || moduleIndex < 0)
			{
				Logcat.Error($"Invalid module index - {moduleIndex}");
				return false;
			}

			if (Abxr.OnModuleTarget == null)
			{
				Logcat.Error("Need to subscribe to OnModuleTarget before running modules");
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
            var modules = _authService.ResponseData?.Modules;
            if (modules == null || modules.Count == 0) return;
            _currentModuleIndex++;
            if (_currentModuleIndex < modules.Count)
            {
                Logcat.Info($"Module '{modules[_currentModuleIndex - 1].Name}' complete. " +
                             $"Advancing to next module - '{modules[_currentModuleIndex].Name}'");
                Abxr.OnModuleTarget?.Invoke(modules[_currentModuleIndex].Target);
            }
            else
            {
                Abxr.OnAllModulesCompleted?.Invoke();
                Logcat.Info("All modules complete");
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
			if (response?.Modules != null && response.Modules.Count > 0)
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
		
		internal List<ModuleData> GetModuleList() => _authService.ResponseData?.Modules;
		
		internal void Log(string logMessage, Abxr.LogLevel logLevel, Dictionary<string, string> metadata)
		{
			metadata ??= new Dictionary<string, string>();
			if (Configuration.Instance.enableSceneEvents)
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
            if (Configuration.Instance.enableSceneEvents)
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
            if (Configuration.Instance.enableSceneEvents)
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
			
			// Module sequence: advance to next, or treat as "all modules complete"
			var modules = _authService.ResponseData?.Modules;
			bool inModuleSequence = modules != null && modules.Count > 0 && _authService.GetEffectiveEnableAutoAdvanceModules();
			if (inModuleSequence)
			{
				AdvanceToNextModule();
			}

			// enableReturnTo: exit or return handoff only when "assessment is fully complete" —
			// i.e. no module sequence, or we just completed the last module (same rule as original exit behavior).
			bool shouldExitOrReturn = _authService.SessionUsedAuthHandoff() && _authService.GetEffectiveEnableReturnTo();
			bool assessmentFullyComplete = !inModuleSequence || _currentModuleIndex >= (modules?.Count ?? 0);
			if (shouldExitOrReturn && assessmentFullyComplete)
			{
				_exitAfterAssessmentCoroutine = StartCoroutine(ExitAfterAssessmentComplete());
			}
		}
		
		internal void EventExperienceStart(string experienceName, Dictionary<string, string> meta) =>
			EventAssessmentStart(experienceName, meta);
		
		internal void EventExperienceComplete(string experienceName, Dictionary<string, string> meta) =>
			EventAssessmentComplete(experienceName, 100, Abxr.EventStatus.Complete, meta);

		/// <summary>
		/// Coroutine to exit the application after a 2-second delay when assessment is complete
		/// and the session used auth handoff with return to launcher enabled.
		/// If the handoff included ReturnToPackage, launch that app with the current session first (then clear it so no loop).
		/// </summary>
		private IEnumerator ExitAfterAssessmentComplete()
		{
			string returnToPackage = _authService?.GetAndClearReturnToPackage();
			if (!string.IsNullOrEmpty(returnToPackage))
			{
				// In tests (Editor or Test Runner Player), "return to launcher" is simulated: inject handoff so the next subsystem can adopt the session. Do not start an Activity (e.g. com.UnityTestRunner.UnityTestRunner has no launchable Activity on device).
				bool useInject = _simulateQuitInExitAfterAssessmentComplete;
#if !(UNITY_ANDROID && !UNITY_EDITOR)
				useInject = true; // Editor: always inject
#endif
				if (useInject)
				{
					if (LaunchAppWithAuthHandoffForTest(returnToPackage, includeReturnToPackage: false))
						Logcat.Info($"Injected handoff for return-to launcher '{returnToPackage}' (Editor/test).");
					else
						Logcat.Warning($"Failed to inject handoff for return target '{returnToPackage}'.");
				}
#if UNITY_ANDROID && !UNITY_EDITOR
				else
				{
					if (LaunchAppWithAuthHandoff(returnToPackage, includeReturnToPackage: false))
						Logcat.Info($"Launched '{returnToPackage}' with auth handoff (return to launcher)");
					else
						Logcat.Warning($"Failed to launch return target '{returnToPackage}' with auth handoff");
				}
#endif
			}
			SendAll();
			Logcat.Info("Assessment complete with auth handoff - returning to launcher in 2 seconds");
			yield return new WaitForSeconds(2f);
			if (_simulateQuitInExitAfterAssessmentComplete)
			{
				Logcat.Info("(Test) Simulated app quit - ExitAfterAssessmentComplete would have exited.");
				yield break;
			}
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
			_overrideDeviceId != null ? _overrideDeviceId : (_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetDeviceId() : "");
        
		internal string GetDeviceSerial() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetDeviceSerial() : "";
		
		internal string GetDeviceTitle() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetDeviceTitle() : "";
		
		internal string[] GetDeviceTags() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetDeviceTags() : null;
		
		internal void SetOrgId(string orgId)
		{
			_overrideOrgId = orgId;
			_authService?.SetRuntimeAuthOrgId(orgId);
		}
		internal void SetAuthSecret(string authSecret)
		{
			_overrideAuthSecret = authSecret;
			_authService?.SetRuntimeAuthAuthSecret(authSecret);
		}
		internal void SetDeviceId(string deviceId)
		{
			_overrideDeviceId = deviceId;
			_authService?.SetRuntimeAuthDeviceId(deviceId);
		}

		internal string GetOrgId() =>
			_overrideOrgId != null ? _overrideOrgId : (_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetOrgId() : Configuration.Instance?.orgID ?? "");
		
		internal string GetOrgTitle() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetOrgTitle() : "";
		
		internal string GetOrgSlug() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetOrgSlug() : "";
		
		internal string GetMacAddressFixed() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetMacAddressFixed() : "";
		
		internal string GetMacAddressRandom() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetMacAddressRandom() : "";
		
		internal bool GetIsAuthenticated() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() && _arborMdmClient.ServiceWrapper != null && _arborMdmClient.ServiceWrapper.GetIsAuthenticated();
		
		internal string GetAccessToken() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetAccessToken() : "";
		
		internal string GetRefreshToken() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetRefreshToken() : "";
		
		internal DateTime? GetExpiresDateUtc() =>
			_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetExpiresDateUtc() : DateTime.MinValue;
		
		internal string GetFingerprint() =>
			_overrideAuthSecret != null ? _overrideAuthSecret : (_arborMdmClient != null && _arborMdmClient.IsConnected() ? _arborMdmClient.ServiceWrapper?.GetFingerprint() : "");
		
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
				Logcat.Error("Poll prompt cannot be null or empty");
				return;
			}

			if (pollType == ExitPollHandler.PollType.MultipleChoice)
			{
				if (responses == null)
				{
					Logcat.Error("List of responses required for multiple choice poll");
					return;
				}

				if (responses.Count is < 2 or > 8)
				{
					Logcat.Error("Multiple choice poll must have at least two and no more than 8 responses");
					return;
				}

				// Validate that all responses are not null or empty
				for (int i = 0; i < responses.Count; i++)
				{
					if (string.IsNullOrWhiteSpace(responses[i]))
					{
						Logcat.Error($"Response at index {i} cannot be null or empty");
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
				string errorMessage = $"Cannot register super metadata with reserved key '{key}'. Reserved keys are: module, moduleName, moduleId, moduleOrder";
				Logcat.Warning(errorMessage);
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
			var modules = _authService.ResponseData?.Modules;
			if (modules?.Count > 0 && _currentModuleIndex >= 0 && _currentModuleIndex < modules.Count)
			{
				ModuleData moduleData = modules[_currentModuleIndex];
				// these are potentially sharing developer private information. Disabling for now
				if (!meta.ContainsKey("module")) meta["module"] = moduleData.Target;
				if (!meta.ContainsKey("moduleId")) meta["moduleId"] = moduleData.Id;
				if (!meta.ContainsKey("moduleName")) meta["moduleName"] = moduleData.Name;
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
				Logcat.Error($"Failed to load super metadata: {ex.Message}\n" +
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
				Logcat.Error($"Failed to save super metadata: {ex.Message}\n" +
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
			// When showing an error (e.g. invalid PIN), stop the Processing animation so the message is visible.
			if (!string.IsNullOrEmpty(error))
				KeyboardHandler.StopProcessing();
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
				meta["duration"] = duration.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
	        _dataService?.ForceSend();
	        _storageService?.ForceSend();
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
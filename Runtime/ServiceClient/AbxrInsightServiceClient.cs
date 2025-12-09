//#nullable enable
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace AbxrLib.Runtime.ServiceClient.AbxrInsightService
{
#if true
	public static class AbxrInsightServiceBridge
	{
		private const string		PackageName = "aar.xrdi.abxrinsightservice.unity.UnityAbxrInsightServiceClient";
		//private const string		PackageName = "aar.xrdi.abxrinsightservice";
		static AndroidJavaObject	_client = null;

		static AndroidJavaObject Activity => new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");

		/// <summary>
		/// Init().
		/// </summary>
		public static void Init()
		{
			//using var clientClass = new AndroidJavaClass(PackageName);

			Debug.Log($"[AbxrInsightServiceClient] AbxrInsightServiceBridge.Init() gonna call on {PackageName}");
			try
			{
				_client = new AndroidJavaObject(PackageName, Activity);
				Debug.Log($"[AbxrInsightServiceClient] AbxrInsightServiceBridge.Init() succeeded using PackageName {PackageName}");
			}
			catch (Exception e)
			{
				Debug.Log($"[AbxrInsightServiceClient] AbxrInsightServiceBridge.Init() failed using PackageName {PackageName} exception message {e.Message}");
			}
		}
		/// <summary>
		/// Bind().
		/// </summary>
		/// <param name="explicitPackage"></param>
		/// <returns></returns>
		public static bool Bind(string explicitPackage = null)
		{
			return _client.Call<bool>("bind", null, explicitPackage); // listener null for brevity
		}
		/// <summary>
		/// IsInitialized().
		/// </summary>
		/// <returns></returns>
		public static bool IsInitialized()
		{
			return (_client != null);
		}

		public static void Unbind() => _client.Call("unbind");
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) => _client.Call<int>("basicTypes", anInt, aLong, aBoolean, aFloat, aDouble, aString);
		public static bool PlaySampleOnLoop() => _client.Call<bool>("playSampleOnLoop");
		public static bool StopPlayback() => _client.Call<bool>("stopPlayback");
		public static string WhatTimeIsIt() => _client.Call<string>("whatTimeIsIt");
		public static bool IsServiceBound() => _client.Call<bool>("isServiceBound");
		public static bool IsServiceAvailable() => _client.Call<bool>("isServiceAvailable");
		public static bool ServiceIsFullyInitialized() => _client.Call<bool>("serviceIsFullyInitialized");
		// --- API code.
		public static void AbxrLibInitStart() => _client.Call<int>("abxrLibInitStart");
		public static void AbxrLibInitEnd() => _client.Call<int>("abxrLibInitEnd");
		// ---
		public static int Authenticate(String szAppId, String szOrgId, String szDeviceId, String szAuthSecret, int ePartner) => _client.Call<int>("authenticate", szAppId, szOrgId, szDeviceId, szAuthSecret, ePartner);
		public static int FinalAuthenticate() => _client.Call<int>("finalAuthenticate");
		public static int ReAuthenticate(bool bObtainAuthSecret) => _client.Call<int>("reAuthenticate", bObtainAuthSecret);
		public static int ForceSendUnsent() => _client.Call<int>("forceSendUnsent");
		// ---
		public static void CaptureTimeStamp() => _client.Call<int>("captureTimeStamp");
		public static void UnCaptureTimeStamp() => _client.Call<int>("unCaptureTimeStamp");
		// ---
		public static int LogDebug(String szText, Dictionary<string, string> dictMeta) => _client.Call<int>("logDebug", szText, Utils.DictToString(dictMeta));
		public static int LogDebugDeferred(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logDebugDeferred", szText, Utils.DictToString(dictMeta));
		public static int LogInfo(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logInfo", szText, Utils.DictToString(dictMeta));
		public static int LogInfoDeferred(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logInfoDeferred", szText, Utils.DictToString(dictMeta));
		public static int LogWarn(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logWarn", szText, Utils.DictToString(dictMeta));
		public static int LogWarnDeferred(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logWarnDeferred", szText, Utils.DictToString(dictMeta));
		public static int LogError(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logError", szText, Utils.DictToString(dictMeta));
		public static int LogErrorDeferred(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logErrorDeferred", szText, Utils.DictToString(dictMeta));
		public static int LogCritical(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logCritical", szText, Utils.DictToString(dictMeta));
		public static int LogCriticalDeferred(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logCriticalDeferred", szText, Utils.DictToString(dictMeta));
		// ---
		public static int Event (String szMessage, Dictionary<String, String> dictMeta) => _client.Call<int>("event", szMessage, Utils.DictToString(dictMeta));
		public static int EventDeferred(String szMessage, Dictionary<String, String> dictMeta) => _client.Call<int>("eventDeferred", szMessage, Utils.DictToString(dictMeta));
		// --- Convenient wrappers for particular forms of events.
		public static int EventAssessmentStart(String szAssessmentName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventAssessmentStart", szAssessmentName, Utils.DictToString(dictMeta));
		public static int EventAssessmentComplete(String szAssessmentName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => _client.Call<int>("eventAssessmentComplete", szAssessmentName, Utils.DictToString(dictMeta));
		// ---
		public static int EventObjectiveStart(String szObjectiveName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventObjectiveStart", szObjectiveName, Utils.DictToString(dictMeta));
		public static int EventObjectiveComplete(String szObjectiveName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => _client.Call<int>("eventObjectiveComplete", szObjectiveName, Utils.DictToString(dictMeta));
		// ---
		public static int EventInteractionStart(String szInteractionName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventInteractionStart", szInteractionName, Utils.DictToString(dictMeta));
		public static int EventInteractionComplete(String szInteractionName, String szResult, String szResultDetails, int eInteractionType, Dictionary<String, String> dictMeta) => _client.Call<int>("eventInteractionComplete", szInteractionName, Utils.DictToString(dictMeta));
		// ---
		public static int EventLevelStart(String szLevelName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventLevelStart", szLevelName, Utils.DictToString(dictMeta));
		public static int EventLevelComplete(String szLevelName, String szScore, Dictionary<String, String> dictMeta) => _client.Call<int>("eventLevelComplete", szLevelName, Utils.DictToString(dictMeta));
		// ---
		public static int AddAIProxy(String szPrompt, String szPastMessages, String szLMMProvider) => _client.Call<int>("addAIProxy", szPrompt, szPastMessages, szLMMProvider);
		public static int AddAIProxyDeferred(String szPrompt, String szPastMessages, String szLMMProvider) => _client.Call<int>("addAIProxyDeferred", szPrompt, szPastMessages, szLMMProvider);
		// ---
		public static int AddTelemetryEntry(String szName, Dictionary<String, String> dictMeta) => _client.Call<int>("addTelemetryEntry", szName, Utils.DictToString(dictMeta));
		public static int AddTelemetryEntryDeferred(String szName, Dictionary<String, String> dictMeta) => _client.Call<int>("addTelemetryEntryDeferred", szName, Utils.DictToString(dictMeta));
		// ---
		//boolean platformIsWindows();
		// --- Authentication fields.
		public static String get_ApiToken() => _client.Call<String>("get_ApiToken");
		public static void set_ApiToken(String szApiToken) => _client.Call<int>("set_ApiToken", szApiToken);
		// ---
		public static String get_ApiSecret() => _client.Call<String>("get_ApiSecret");
		public static void set_ApiSecret(String szApiSecret) => _client.Call<int>("set_ApiSecret", szApiSecret);
		// ---
		public static String get_AppID() => _client.Call<String>("get_AppID");
		public static void set_AppID(String szAppID) => _client.Call<int>("set_AppID", szAppID);
		// ---
		public static String get_OrgID() => _client.Call<String>("get_OrgID");
		public static void set_OrgID(String szOrgID) => _client.Call<int>("set_OrgID", szOrgID);
		// ---
		public static long get_TokenExpiration() => _client.Call<long>("get_TokenExpiration");
		public static void set_TokenExpiration(long dtTokenExpiration) => _client.Call<int>("set_TokenExpiration", dtTokenExpiration);
		// ---
		public static bool TokenExpirationImminent() => _client.Call<bool>("tokenExpirationImminent");
		// ---
		public static int get_Partner() => _client.Call<int>("get_Partner");
		public static void set_Partner(int ePartner) => _client.Call<int>("set_Partner", ePartner);
		// --- Environment/session globals that get sent with the auth payload in Authenticate() functions.
		public static String get_OsVersion() => _client.Call<String>("get_OsVersion");
		public static void set_OsVersion(String szOsVersion) => _client.Call<int>("set_OsVersion", szOsVersion);
		// ---
		public static String get_IpAddress() => _client.Call<String>("get_IpAddress");
		public static void set_IpAddress(String szIpAddress) => _client.Call<int>("set_IpAddress", szIpAddress);
		// ---
		public static String get_XrdmVersion() => _client.Call<String>("get_XrdmVersion");
		public static void set_XrdmVersion(String szXrdmVersion) => _client.Call<int>("set_XrdmVersion", szXrdmVersion);
		// ---
		public static String get_AppVersion() => _client.Call<String>("get_AppVersion");
		public static void set_AppVersion(String szAppVersion) => _client.Call<int>("set_AppVersion", szAppVersion);
		// ---
		public static String get_UnityVersion() => _client.Call<String>("get_UnityVersion");
		public static void set_UnityVersion(String szUnityVersion) => _client.Call<int>("set_UnityVersion", szUnityVersion);
		// ---
		// Not sure about this one... seems to be an artifact of an earlier time.  It is in the C++ code but only as a data member that is not used anywhere.
		//String get_DataPath();
		//void set_DataPath(String szDataPath);
		// ---
		public static String get_DeviceModel() => _client.Call<String>("get_DeviceModel");
		public static void set_DeviceModel(String szDeviceModel) => _client.Call<int>("set_DeviceModel", szDeviceModel);
		// ---
		public static String get_UserId() => _client.Call<String>("get_UserId");
		public static void set_UserId(String szUserId) => _client.Call<int>("set_UserId", szUserId);
		// ---
		public static List<String> get_Tags() => Utils.StringToStringList(_client.Call<String>("get_UnityVersion"));
		public static void set_Tags(List<String> lszTags) => _client.Call<int>("set_Tags", Utils.StringListToString(lszTags));
		// ---
		public static Dictionary<String, String> get_GeoLocation() => Utils.StringToDict(_client.Call<String>("get_GeoLocation"));
		public static void set_GeoLocation(Dictionary<String, String> dictValue) => _client.Call<int>("set_GeoLocation", Utils.DictToString(dictValue));
		// ---
		public static Dictionary<String, String> get_SessionAuthMechanism() => Utils.StringToDict(_client.Call<String>("get_SessionAuthMechanism"));
		public static void set_SessionAuthMechanism(Dictionary<String, String> dictValue) => _client.Call<int>("set_SessionAuthMechanism", Utils.DictToString(dictValue));
		// --- Environment / Storage functions.
		public static String StorageGetDefaultEntryAsString() => _client.Call<String>("storageGetDefaultEntryAsString");
		public static String StorageGetEntryAsString(String szName) => _client.Call<String>("storageGetEntryAsString", szName);
		// ---
		public static int StorageSetDefaultEntryFromString(String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => _client.Call<int>("storageSetDefaultEntryAsString", szStorageEntry, bKeepLatest, szOrigin, bSessionData);
		public static int StorageSetEntryFromString(String szName, String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => _client.Call<int>("storageSetEntryAsString", szName, szStorageEntry, bKeepLatest, szOrigin, bSessionData);
		// ---
		public static int StorageRemoveDefaultEntry() => _client.Call<int>("storageRemoveDefaultEntry");
		public static int StorageRemoveEntry(String szName) => _client.Call<int>("storageRemoveEntry", szName);
		public static int StorageRemoveMultipleEntries(bool bSessionOnly) => _client.Call<int>("storageRemoveMultipleEntries", bSessionOnly);
		// --- Configuration fields.
		public static String get_RestUrl() => _client.Call<String>("get_RestUrl");
		public static void set_RestUrl(String szValue) => _client.Call<int>("set_RestUrl", szValue);
		// ---
		public static int get_SendRetriesOnFailure() => _client.Call<int>("get_SendRetriesOnFailure");
		public static void set_SendRetriesOnFailure(int nValue) => _client.Call<int>("set_SendRetriesOnFailure", nValue);
		// ---
		public static double get_SendRetryInterval() => _client.Call<double>("get_SendRetryInterval");
		public static void set_SendRetryInterval(double tsValue) => _client.Call<int>("set_SendRetryInterval", tsValue);
		// ---
		public static double get_SendNextBatchWait() => _client.Call<double>("get_SendNextBatchWait");
		public static void set_SendNextBatchWait(double tsValue) => _client.Call<int>("set_SendRetryInterval", tsValue);
		// ---
		public static double get_StragglerTimeout() => _client.Call<double>("get_StragglerTimeout");
		public static void set_StragglerTimeout(double tsValue) => _client.Call<int>("set_StragglerTimeout", tsValue);
		// ---
		public static double get_PositionCapturePeriodicity() => _client.Call<double>("get_PositionCapturePeriodicity");
		public static void set_PositionCapturePeriodicity(double dValue) => _client.Call<int>("set_PositionCapturePeriodicity", dValue);
		// ---
		public static double get_FrameRateCapturePeriodicity() => _client.Call<double>("get_FrameRateCapturePeriodicity");
		public static void set_FrameRateCapturePeriodicity(double dValue) => _client.Call<int>("set_FrameRateCapturePeriodicity", dValue);
		// ---
		public static double get_TelemetryCapturePeriodicity() => _client.Call<double>("get_TelemetryCapturePeriodicity");
		public static void set_TelemetryCapturePeriodicity(double dValue) => _client.Call<int>("set_TelemetryCapturePeriodicity", dValue);
		// ---
		public static int get_DataItemsPerSendAttempt() => _client.Call<int>("get_DataItemsPerSendAttempt");
		public static void set_DataItemsPerSendAttempt(int nValue) => _client.Call<int>("set_DataItemsPerSendAttempt", nValue);
		// ---
		public static int get_StorageEntriesPerSendAttempt() => _client.Call<int>("get_StorageEntriesPerSendAttempt");
		public static void set_StorageEntriesPerSendAttempt(int nValue) => _client.Call<int>("set_StorageEntriesPerSendAttempt", nValue);
		// ---
		public static double get_PruneSentItemsOlderThan() => _client.Call<double>("get_PruneSentItemsOlderThan");
		public static void set_PruneSentItemsOlderThan(double tsValue) => _client.Call<int>("set_PruneSentItemsOlderThan", tsValue);
		// ---
		public static int get_MaximumCachedItems() => _client.Call<int>("get_MaximumCachedItems");
		public static void set_MaximumCachedItems(int nValue) => _client.Call<int>("set_MaximumCachedItems", nValue);
		// ---
		public static bool get_RetainLocalAfterSent() => _client.Call<bool>("get_RetainLocalAfterSent");
		public static void set_RetainLocalAfterSent(bool bValue) => _client.Call<int>("set_RetainLocalAfterSent", bValue);
		// ---
		public static bool get_ReAuthenticateBeforeTokenExpires() => _client.Call<bool>("get_ReAuthenticateBeforeTokenExpires");
		public static void set_ReAuthenticateBeforeTokenExpires(bool bValue) => _client.Call<int>("set_ReAuthenticateBeforeTokenExpires", bValue);
		// ---
		public static bool get_UseDatabase() => _client.Call<bool>("get_UseDatabase");
		public static void set_UseDatabase(bool bValue) => _client.Call<int>("set_UseDatabase", bValue);
		// ---
		public static Dictionary<String, String> get_AppConfigAuthMechanism() => Utils.StringToDict(_client.Call<String>("get_AppConfigAuthMechanism"));
		public static void set_AppConfigAuthMechanism(Dictionary<String, String> dictValue) => _client.Call<int>("set_AppConfigAuthMechanism", Utils.DictToString(dictValue));
		// ---
		public static bool ReadConfig() => _client.Call<bool>("readConfig");
	}


	/// <summary>Allows interacting with the SDK service.</summary>
	/// <remarks>
	///   Only a single instance of this class should be used per app. The SDK is automatically initialized and shut
	///   down whenever the instance of this class is enabled/disabled (respectively).
	/// </remarks>
	public class AbxrInsightServiceClient : MonoBehaviour
	{
		//private const string				PackageName = "aar.xrdi.abxrinsightservice";
		//private AndroidJavaObject			_mjpsdk = null;
		//private MJPNativeConnectionCallback	_nativeCallback = null;

		// Constructor logging
		public AbxrInsightServiceClient()
		{
			Debug.Log("[AbxrInsightServiceClient] Constructor called - AbxrInsightServiceClient instance created");
		}
		public static string WhatTimeIsIt()
		{
			return AbxrInsightServiceBridge.WhatTimeIsIt();
		}
		public static bool IsServiceAvailable()
		{
			return AbxrInsightServiceBridge.IsServiceAvailable();
		}
		public static bool ServiceIsFullyInitialized()
		{
			return AbxrInsightServiceBridge.ServiceIsFullyInitialized();
		}
		private void Awake()
		{
			Debug.Log($"[AbxrInsightServiceClient] Awake() called on GameObject: {gameObject.name}");
		}
		private void Start()
		{
			bool	bOk;

			try
			{
				Debug.Log($"[AbxrInsightServiceClient] Start() called on GameObject: {gameObject.name}");
				AbxrInsightServiceBridge.Init();
				Debug.Log($"[AbxrInsightServiceClient] about to call AbxrInsightServiceBridge.Bind() on GameObject: {gameObject.name}");
				bOk = AbxrInsightServiceBridge.Bind();
				Debug.Log($"[AbxrInsightServiceClient] Bind() result: {bOk}");
				// ---
				//_nativeCallback = new MJPNativeConnectionCallback();
				// ---
			}
			catch (Exception e)
			{
				Debug.Log($"[AbxrInsightServiceClient] Bind() blew: {e.Message}");
			}
		}
		private void OnDestroy()
		{
			AbxrInsightServiceBridge.Unbind();
		}
	}



#else
	// This is the core mechanism for the ServiceWrapper below for calling bound methods in the service.
	public static class AndroidJavaObjectExt
	{
		public static T CallResult<T>(this AndroidJavaObject native, string methodName, params object[] args) =>
			native.Call<AndroidJavaObject>(methodName, args) is var result
			&& result.Call<bool>("isOk")
				? result.Call<T>("getValue")
				: throw new MjpSdkException(result.Call<string>("getError"));	// Not a specific bound method... part of the framework supplied by result type.
	}

	/// <summary>Allows interacting with the SDK service.</summary>
	/// <remarks>
	///   Only a single instance of this class should be used per app. The SDK is automatically initialized and shut
	///   down whenever the instance of this class is enabled/disabled (respectively).
	/// </remarks>
	public class AbxrInsightServiceClient : MonoBehaviour
	{
		private const string PackageName = "aar.xrdi.abxrinsightservice";
		private AndroidJavaObject?				_mjpsdk;
		private MJPNativeConnectionCallback?	_nativeCallback;
		public static MjpSdkServiceWrapper?		MjpServiceWrapper;

		// Constructor logging
		public AbxrInsightServiceClient()
		{
			Debug.Log("[AbxrInsightServiceClient] Constructor called - AbxrInsightServiceClient instance created");
		}

		private void Awake()
		{
			Debug.Log($"[AbxrInsightServiceClient] Awake() called on GameObject: {gameObject.name}");
		}

		private void Start()
		{
			Debug.Log($"[AbxrInsightServiceClient] Start() called on GameObject: {gameObject.name}");
		}

		// Whenever we delay via Task.Delay, there is no guarantee that our current thread would be already attached to Android JNI,
		// so we must reattached the current thread to AndroidJNI right after Task.Delay to ensure we don't run into threading issues.
		private static Task DelayAndReattachThreadToJNI(int delay) => Task.Delay(delay).ContinueWith(_ => AndroidJNI.AttachCurrentThread());

		private AndroidJavaObject Sdk
		{
			get
			{
				if (_mjpsdk is null)
				{
					throw new InvalidOperationException("This MonoBehaviour is not enabled.");
				}

				return _mjpsdk;
			}
		}

		public static bool IsConnected() 
		{
			bool isConnected = MjpServiceWrapper != null;
			Debug.Log($"[AbxrInsightServiceClient] IsConnected() = {isConnected}");
			return isConnected;
		}

		public static AbxrInsightServiceClient? FindInstance()
		{
			var instance = FindObjectOfType<AbxrInsightServiceClient>();
			Debug.Log($"[AbxrInsightServiceClient] FindInstance() - found instance: {(instance != null ? "YES" : "NO")}");
			if (instance != null)
			{
				Debug.Log($"[AbxrInsightServiceClient] Instance found on GameObject: {instance.gameObject.name}, enabled: {instance.enabled}");
			}
			return instance;
		}

		private void Connect()
		{
			Debug.Log("[AbxrInsightServiceClient] Attempting to connect to service");
			try
			{
				using var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
				_nativeCallback = new MJPNativeConnectionCallback(this);
				Debug.Log("[AbxrInsightServiceClient] Calling Sdk.connect() method");
				Sdk.Call("connect", currentActivity, _nativeCallback);
				Debug.Log("[AbxrInsightServiceClient] Sdk.connect() method called successfully");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AbxrInsightServiceClient] Error in Connect(): {ex.Message}");
				Debug.LogError($"[AbxrInsightServiceClient] Stack trace: {ex.StackTrace}");
			}
		}

		protected void OnDisable()
		{
			Debug.Log("[AbxrInsightServiceClient] OnDisable() called - cleaning up");
			_mjpsdk?.Dispose();
			_mjpsdk = null;
			MjpServiceWrapper = null;
		}

		protected void OnEnable()
		{
			Debug.Log($"[AbxrInsightServiceClient] OnEnable() called - attempting to create SDK for package: {PackageName}");
			try
			{
				// Instantiates our `Sdk.java`.
				//Debug.Log($"[AbxrInsightServiceClient] about to attempt to create {PackageName}.Sdk");
				//_mjpsdk = new AndroidJavaObject($"{PackageName}.Sdk");
				Debug.Log($"[AbxrInsightServiceClient] about to attempt to create {PackageName}.NewService");
				_mjpsdk = new AndroidJavaObject($"{PackageName}.NewService");
				Debug.Log("[AbxrInsightServiceClient] SDK object created successfully");
				Connect();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AbxrInsightServiceClient] Error in OnEnable(): {ex.Message}");
				Debug.LogError($"[AbxrInsightServiceClient] Stack trace: {ex.StackTrace}");
			}
		}

		public sealed class MjpSdkServiceWrapper
		{
			private readonly AndroidJavaObject _native;	// MJP:  Somehow this knows it is an AndroidJavaObjectExt despite AndroidJavaObjectExt not inheriting from AndroidObject.

			public MjpSdkServiceWrapper(AndroidJavaObject native) => _native = native;

			public void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, string aString) => _native.CallResult<string>("basicTypes");
			public void PlaySampleOnLoop() => _native.CallResult<string>("playSampleOnLoop");

			public void StopPlayback() => _native.CallResult<string>("stopPlayback");
			public string WhatTimeIsIt() 
			{
				Debug.Log("[AbxrInsightServiceClient] WhatTimeIsIt() called");
				try
				{
					var result = _native.CallResult<string>("whatTimeIsIt");
					Debug.Log($"[AbxrInsightServiceClient] WhatTimeIsIt() returned: {result}");
					return result;
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AbxrInsightServiceClient] Error in WhatTimeIsIt(): {ex.Message}");
					Debug.LogError($"[AbxrInsightServiceClient] Stack trace: {ex.StackTrace}");
					return "";
				}
			}
			public bool GetIsInitialized()
			{
				//var value = _native.CallResult<string>("getIsInitialized");
				//return !string.IsNullOrWhiteSpace(value) && Convert.ToBoolean(value);
				return true;
			}
		}

		private async Task NotifyWhenInitializedAsync(AndroidJavaObject? nativeObj)
		{
			Debug.Log("[AbxrInsightServiceClient] NotifyWhenInitializedAsync started");
			// If the application gets loaded before the XRDM client, the XRDM client may not have time to be initialized.
			// To avoid this timing issue, we should wait until XRDM client is initialized to fire the event of OnConnected.
			var delay = 500;
			var delayMultiplier = 1.5f;
			var maximumAttempts = 7;

#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CS8604 // Possible null reference argument.
			// nativeObj shouldn't be null, and if it is null, something really bad must have happened already.
			if (nativeObj == null)
			{
				Debug.LogError("[AbxrInsightServiceClient] nativeObj is null in NotifyWhenInitializedAsync!");
				return;
			}
			var serviceWrapper = new MjpSdkServiceWrapper(nativeObj);
			Debug.Log("[AbxrInsightServiceClient] Service wrapper created, checking initialization...");
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CA2000 // Dispose objects before losing scope
			try
			{
				for (var attempt = 0; attempt < maximumAttempts; attempt++)
				{
					Debug.Log($"[AbxrInsightServiceClient] Initialization attempt {attempt + 1}/{maximumAttempts}");
					if (serviceWrapper.GetIsInitialized())
					{
						Debug.Log("[AbxrInsightServiceClient] Service is initialized! Setting MjpServiceWrapper.");
						MjpServiceWrapper = serviceWrapper;
						return;
					}
					Debug.Log($"[AbxrInsightServiceClient] Service not yet initialized, waiting {delay}ms...");
					await DelayAndReattachThreadToJNI(delay);
					_ = AndroidJNI.AttachCurrentThread();
					delay = (int)Math.Floor(delay * delayMultiplier);
				}
				Debug.LogWarning("[AbxrInsightServiceClient] Maximum initialization attempts reached, service may not be ready");
#pragma warning disable CA1031
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AbxrInsightServiceClient] Exception in NotifyWhenInitializedAsync: {ex.Message}");
				Debug.LogError($"[AbxrInsightServiceClient] Stack trace: {ex.StackTrace}");
				await DelayAndReattachThreadToJNI(delay);
				_ = AndroidJNI.AttachCurrentThread();
				Debug.Log("[AbxrInsightServiceClient] Setting MjpServiceWrapper despite exception (fallback)");
				MjpServiceWrapper = serviceWrapper;
			}
#pragma warning restore CA1031
		}

		private sealed class MJPNativeConnectionCallback : AndroidJavaProxy
		{
			private readonly AbxrInsightServiceClient _sdkBehavior;

			public MJPNativeConnectionCallback(AbxrInsightServiceClient sdkBehavior) : base(PackageName + ".IConnectionCallback")
			{
				_sdkBehavior = sdkBehavior;
			}

			// Invoke the method ourselves, as the base does an expensive lookup via reflection:
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/AndroidJNI/AndroidJava.cs#L124-L139
			public override AndroidJavaObject? Invoke(string methodName, AndroidJavaObject[] javaArgs)
			{
				Debug.Log($"[AbxrInsightServiceClient] Connection callback invoked: {methodName}");
				if (methodName == "onConnected")
				{
					Debug.Log("[AbxrInsightServiceClient] onConnected callback triggered - starting initialization");
					_ = _sdkBehavior.NotifyWhenInitializedAsync(javaArgs[0]);
					// `onConnected` is a `void` method.
					return null;
				}

				return base.Invoke(methodName, javaArgs);
			}
		}
	}

	public class MjpSdkException : Exception
	{
		public MjpSdkException(string message) : base(message)
		{
		}
	}
#endif
}

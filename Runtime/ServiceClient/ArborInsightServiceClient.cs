//#nullable enable
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace AbxrLib.Runtime.ServiceClient.ArborInsightService
{
	internal static class ArborInsightServiceBridge
	{
		private const string		PackageName = "aar.xrdi.arborinsightservice.unity.UnityArborInsightServiceClient";
		/// <summary>Package name of the ArborInsightService APK (impl app). Used to check if the service is installed before waiting for bind.</summary>
		private const string		ServiceApkPackageName = "impl.xrdi.arborinsightservice";
		static AndroidJavaObject	_client = null;

		static AndroidJavaObject Activity => new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");

		/// <summary>Returns true if the ArborInsightService APK is installed on the device. Use to skip the readiness poll when not installed.</summary>
		public static bool IsServicePackageInstalled()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			try
			{
				using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
				{
					if (activity == null) return false;
					using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
					{
						if (pm == null) return false;
						pm.Call<AndroidJavaObject>("getPackageInfo", ServiceApkPackageName, 0);
						return true;
					}
				}
			}
			catch
			{
				return false;
			}
#else
			return false;
#endif
		}

		/// <summary>
		/// Init().
		/// </summary>
		public static void Init()
		{
			//using var clientClass = new AndroidJavaClass(PackageName);

			try
			{
				_client = new AndroidJavaObject(PackageName, Activity);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[ArborInsightServiceClient] Init failed ({PackageName}): {e.Message}");
			}
		}
		/// <summary>
		/// Bind().
		/// </summary>
		/// <param name="explicitPackage"></param>
		/// <returns></returns>
		public static bool Bind(string explicitPackage = null)
		{
			if (_client == null)
			{
				Debug.LogWarning("[ArborInsightServiceClient] Bind() skipped: bridge not initialized (Unity ArborInsightService AAR may be missing from Plugins/Android).");
				return false;
			}
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

		public static void Unbind() { if (_client != null) _client.Call("unbind"); }
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) => _client.Call<int>("basicTypes", anInt, aLong, aBoolean, aFloat, aDouble, aString);
		public static string WhatTimeIsIt() => _client.Call<string>("whatTimeIsIt");
		public static bool IsServiceBound() => _client.Call<bool>("isServiceBound");
		public static bool IsServiceAvailable() => _client.Call<bool>("isServiceAvailable");
		public static bool ServiceIsFullyInitialized() => _client.Call<bool>("serviceIsFullyInitialized");
		// --- API code.
		public static void AbxrLibInitStart() => _client.Call<int>("abxrLibInitStart");
		public static void AbxrLibInitEnd() => _client.Call<int>("abxrLibInitEnd");
		// ---
		public static String AuthRequest(String szUserId, String dictAdditionalUserData)
		{
			if (_client == null)
			{
				Debug.LogError($"[ArborInsightServiceClient] AuthRequest called but _client is null!");
				return "{\"result\":0}";
			}
			try
			{
				return _client.Call<String>("authRequest", szUserId ?? "", dictAdditionalUserData ?? "") ?? "{\"result\":0}";
			}
			catch (Exception e)
			{
				Debug.LogError($"[ArborInsightServiceClient] AuthRequest exception: {e.GetType().Name}: {e.Message}\nStackTrace: {e.StackTrace}");
				return "{\"result\":0}";
			}
		}
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
		public static int EventAssessmentComplete(String szAssessmentName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => _client.Call<int>("eventAssessmentComplete", szAssessmentName, szScore, eResultOptions, Utils.DictToString(dictMeta));
		// ---
		public static int EventObjectiveStart(String szObjectiveName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventObjectiveStart", szObjectiveName, Utils.DictToString(dictMeta));
		public static int EventObjectiveComplete(String szObjectiveName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => _client.Call<int>("eventObjectiveComplete", szObjectiveName, szScore, eResultOptions, Utils.DictToString(dictMeta));
		// ---
		public static int EventInteractionStart(String szInteractionName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventInteractionStart", szInteractionName, Utils.DictToString(dictMeta));
		public static int EventInteractionComplete(String szInteractionName, String szResult, String szResultDetails, int eInteractionType, Dictionary<String, String> dictMeta) => _client.Call<int>("eventInteractionComplete", szInteractionName, szResult, szResultDetails, eInteractionType, Utils.DictToString(dictMeta));
		// ---
		public static int EventLevelStart(String szLevelName, Dictionary<String, String> dictMeta) => _client.Call<int>("eventLevelStart", szLevelName, Utils.DictToString(dictMeta));
		public static int EventLevelComplete(String szLevelName, String szScore, Dictionary<String, String> dictMeta) => _client.Call<int>("eventLevelComplete", szLevelName, szScore, Utils.DictToString(dictMeta));
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
		public static String get_AppToken() => _client.Call<String>("get_AppToken");
		public static void set_AppToken(String szAppToken) => _client.Call("set_AppToken", szAppToken);
		// --- Organization Token (for InsightsToken/OrgToken auth). AAR must support set_OrgToken when using app tokens.
		public static void set_OrgToken(String szOrgToken) => _client.Call("set_OrgToken", szOrgToken);
		// --- SSO access token (optional; when set, sent in auth body to match REST path).
		public static void set_SSOAccessToken(String szSSOAccessToken) => _client.Call("set_SSOAccessToken", szSSOAccessToken ?? "");
		// ---
		public static String get_AppID() => _client.Call<String>("get_AppID");
		public static void set_AppID(String szAppID) => _client.Call<int>("set_AppID", szAppID);
		// ---
		public static String get_OrgID() => _client.Call<String>("get_OrgID");
		public static void set_OrgID(String szOrgID) => _client.Call<int>("set_OrgID", szOrgID);
		// ---
		public static String get_AuthSecret() => _client.Call<String>("get_AuthSecret");
		public static void set_AuthSecret(String szAuthSecret) => _client.Call<int>("set_AuthSecret", szAuthSecret);
		// ---
		public static String get_BuildType() => _client.Call<String>("get_BuildType");
		public static void set_BuildType(String szBuildType) => _client.Call("set_BuildType", szBuildType);
		// ---
		public static String get_DeviceID() => _client.Call<String>("get_DeviceID");
		public static void set_DeviceID(String szDeviceID) => _client.Call<int>("set_DeviceID", szDeviceID);
		// ---
		public static String get_UserID() => _client.Call<String>("get_UserID");
		public static void set_UserID(String szUserID) => _client.Call<int>("set_UserID", szUserID);
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
		public static String get_AbxrLibType() => _client.Call<String>("get_AbxrLibType");
		public static void set_AbxrLibType(String szAbxrLibType) => _client.Call<int>("set_AbxrLibType", szAbxrLibType);
		// ---
		public static String get_AbxrLibVersion() => _client.Call<String>("get_AbxrLibVersion");
		public static void set_AbxrLibVersion(String szAbxrLibVersion) => _client.Call<int>("set_AbxrLibVersion", szAbxrLibVersion);
		// ---
		// Not sure about this one... seems to be an artifact of an earlier time.  It is in the C++ code but only as a data member that is not used anywhere.
		//String get_DataPath();
		//void set_DataPath(String szDataPath);
		// ---
		public static String get_DeviceModel() => _client.Call<String>("get_DeviceModel");
		public static void set_DeviceModel(String szDeviceModel) => _client.Call<int>("set_DeviceModel", szDeviceModel);
		// --- Build fingerprint (optional on AAR; no-op if AAR does not expose yet). Session ID is owned by the service.
		public static void set_BuildFingerprint(String szBuildFingerprint)
		{
			if (_client == null) return;
			try { _client.Call("set_BuildFingerprint", szBuildFingerprint ?? ""); } catch (Exception) { /* AAR may not support yet */ }
		}
		// ---
		public static void set_UserId(String szUserId) => _client.Call<int>("set_UserId", szUserId);
		// ---
		public static List<String> get_Tags() => Utils.StringToStringList(_client.Call<String>("get_Tags"));
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
		public static int StorageSetDefaultEntryFromString(String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => _client.Call<int>("storageSetDefaultEntryFromString", szStorageEntry, bKeepLatest, szOrigin, bSessionData);
		public static int StorageSetEntryFromString(String szName, String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => _client.Call<int>("storageSetEntryFromString", szName, szStorageEntry, bKeepLatest, szOrigin, bSessionData);
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
		public static void set_SendNextBatchWait(double tsValue) => _client.Call<int>("set_SendNextBatchWait", tsValue);
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
		/** Full app config as JSON (same shape as GET /v1/storage/config). */
		public static string GetAppConfig() => _client.Call<String>("get_AppConfig") ?? "";
		// ---
		public static bool ReadConfig() => _client.Call<bool>("readConfig");
	}

	/// <summary>
	/// Due to marshalling, the ArborInsightServiceClient calls that use enums need to represent them with ints.
	/// This is for the ones that return AbxrResult.
	/// Co-maintain with Kotlin service DotNetishTypes.kt.
	/// </summary>
	public enum AbxrResult
	{
		// --- Unity compatible.
		OK,                             // Analytics API result: Success.
		NOT_INITIALIZED,                // Analytics API result: Analytics not initialized.
		ANALYTICS_DISABLED,             // Analytics API result: Analytics is disabled.
		TOO_MANY_ITEMS,                 // Analytics API result: Too many parameters.
		SIZE_LIMIT_REACHED,             // Analytics API result: Argument size limit.
		TOO_MANY_REQUESTS,              // Analytics API result: Too many requests.
		INVALID_DATA,                   // Analytics API result: Invalid argument value.
		UNSUPPORTED_PLATFORM,           // Analytics API result: This platform doesn't support Analytics.
										// --- end Unity compatible.
		ENABLE_EVENT_FAILED,            // Really bad... the dictionary insert failed, system out of memory.
		EVENT_NOT_ENABLED,              // User attempting to fire an event that has not been registered/enabled.
		EVENT_CACHED,                   // Attempt to fire event could not reach cloud, so it got stored into local db.
		SEND_EVENT_FAILED,              // General failure of AbxrLibSend.Event().
		POST_OBJECTS_FAILED,            // General failure of AbxrLibAnalytics.PostABXREvents().
		POST_OBJECTS_FAILED_NETWORK_ERROR,
		POST_OBJECTS_BAD_JSON_RESPONSE,
		DELETE_OBJECTS_FAILED,          // General failure of AbxrLibAnalytics.DeleteABXREvents().
		DELETE_OBJECTS_FAILED_NETWORK_ERROR,
		DELETE_OBJECTS_FAILED_DATABASE,
		DELETE_OBJECTS_BAD_JSON_RESPONSE,
		AUTHENTICATE_FAILED,
		AUTHENTICATE_FAILED_NETWORK_ERROR,
		COULD_NOT_OBTAIN_AUTH_SECRET,   // ReAuthenticate().
		CORRUPT_JSON,
		SET_ENVIRONMENT_DATA_FAILED,
		OBJECT_NOT_FOUND,
		TASK_BLEW_EXCEPTION,
		INVALID_PARAMETER
	}

	/// <summary>Allows interacting with the SDK service.</summary>
	/// <remarks>
	///   Only a single instance of this class should be used per app. The SDK is automatically initialized and shut
	///   down whenever the instance of this class is enabled/disabled (respectively).
	/// </remarks>
	public class ArborInsightServiceClient : MonoBehaviour
	{
		//private const string				PackageName = "aar.xrdi.abxrinsightservice";
		//private AndroidJavaObject			_mjpsdk = null;
		//private MJPNativeConnectionCallback	_nativeCallback = null;

		public ArborInsightServiceClient() { }
		private void Awake() { }
		private void Start()
		{
			try
			{
				ArborInsightServiceBridge.Init();
				if (!ArborInsightServiceBridge.IsInitialized())
				{
					Debug.LogWarning("[ArborInsightServiceClient] Init failed (ClassNotFoundException usually means the ArborInsightService unity-client AAR is not in Assets/Plugins/Android). Skipping Bind().");
					return;
				}
				ArborInsightServiceBridge.Bind();
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[ArborInsightServiceClient] Bind failed: {e.Message}");
			}
		}
		private void OnDestroy()
		{
			ArborInsightServiceBridge.Unbind();
		}
		// --- API CALLS.
		public static bool Bind(string explicitPackage = null) => ArborInsightServiceBridge.Bind(explicitPackage);
		/// <summary>
		/// IsInitialized().
		/// </summary>
		/// <returns></returns>
		public static bool IsInitialized() => ArborInsightServiceBridge.IsInitialized();
		public static void Unbind() => ArborInsightServiceBridge.Unbind();
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) => ArborInsightServiceBridge.BasicTypes(anInt, aLong, aBoolean, aFloat, aDouble, aString);
		public static string WhatTimeIsIt() => ArborInsightServiceBridge.WhatTimeIsIt();
		public static bool IsServiceBound() => ArborInsightServiceBridge.IsServiceBound();
		public static bool IsServiceAvailable() => ArborInsightServiceBridge.IsServiceAvailable();
		/// <summary>True if the ArborInsightService APK is installed. Use to fail fast and skip the readiness poll when running standalone.</summary>
		public static bool IsServicePackageInstalled() => ArborInsightServiceBridge.IsServicePackageInstalled();
		/// <summary>True when the service is fully initialized and ready for calls. Never throws; returns false if the bridge is unavailable or JNI fails.</summary>
		public static bool ServiceIsFullyInitialized()
		{
			try { return ArborInsightServiceBridge.ServiceIsFullyInitialized(); }
			catch { return false; }
		}
		/// <summary>Blocks until the service reports ready or maxAttempts is reached. Only runs on Android when the service APK is installed; no-op otherwise. Call from main thread at startup so auth can proceed.</summary>
		public static void WaitForServiceReady(int maxAttempts = 40, int delayMs = 250)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!IsServicePackageInstalled()) return;
			for (int i = 0; i < maxAttempts; i++)
			{
				if (ServiceIsFullyInitialized()) return;
				System.Threading.Thread.Sleep(delayMs);
			}
#endif
		}
		// --- API code.
		public static void AbxrLibInitStart() => ArborInsightServiceBridge.AbxrLibInitStart();
		public static void AbxrLibInitEnd() => ArborInsightServiceBridge.AbxrLibInitEnd();
		// ---
		public static String AuthRequest(String szUserId, String dictAdditionalUserData) => ArborInsightServiceBridge.AuthRequest(szUserId ?? "", dictAdditionalUserData ?? "");
		/// <summary>Calls the service auth endpoint and returns true with the response body when the response looks like a successful auth payload (same shape as REST). Returns false when the service returned a failure or empty/short result so the caller can retry or use one response-handling path.</summary>
		public static bool TryAuthRequest(string userId, string authMechanismDict, out string responseJson)
		{
			responseJson = ArborInsightServiceBridge.AuthRequest(userId ?? "", authMechanismDict ?? "");
			if (string.IsNullOrEmpty(responseJson)) return false;
			var trimmed = responseJson.TrimStart();
			if (trimmed.StartsWith("{\"result\":") && responseJson.Length < 25) return false;
			return true;
		}
		// ---
		public static int Authenticate(String szAppId, String szOrgId, String szDeviceId, String szAuthSecret, int ePartner) => ArborInsightServiceBridge.Authenticate(szAppId ?? "", szOrgId ?? "", szDeviceId ?? "", szAuthSecret ?? "", ePartner);
		public static int FinalAuthenticate() => ArborInsightServiceBridge.FinalAuthenticate();
		public static int ReAuthenticate(bool bObtainAuthSecret) => ArborInsightServiceBridge.ReAuthenticate(bObtainAuthSecret);
		public static int ForceSendUnsent() => ArborInsightServiceBridge.ForceSendUnsent();
		// ---
		public static void CaptureTimeStamp() => ArborInsightServiceBridge.CaptureTimeStamp();
		public static void UnCaptureTimeStamp() => ArborInsightServiceBridge.UnCaptureTimeStamp();
		// ---
		public static int LogDebug(String szText, Dictionary<string, string> dictMeta) => ArborInsightServiceBridge.LogDebug(szText ?? "", dictMeta);
		public static int LogDebugDeferred(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogDebugDeferred(szText ?? "", dictMeta);
		public static int LogInfo(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogInfo(szText ?? "", dictMeta);
		public static int LogInfoDeferred(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogInfoDeferred(szText, dictMeta);
		public static int LogWarn(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogWarn(szText ?? "", dictMeta);
		public static int LogWarnDeferred(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogWarnDeferred(szText ?? "", dictMeta);
		public static int LogError(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogError(szText ?? "", dictMeta);
		public static int LogErrorDeferred(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogErrorDeferred(szText ?? "", dictMeta);
		public static int LogCritical(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogCritical(szText ?? "", dictMeta);
		public static int LogCriticalDeferred(String szText, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.LogCriticalDeferred(szText ?? "", dictMeta);
		// ---
		public static int Event(String szMessage, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.Event(szMessage ?? "", dictMeta);
		public static int EventDeferred(String szMessage, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventDeferred(szMessage ?? "", dictMeta);
		// --- Convenient wrappers for particular forms of events.
		public static int EventAssessmentStart(String szAssessmentName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventAssessmentStart(szAssessmentName ?? "", dictMeta);
		public static int EventAssessmentComplete(String szAssessmentName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventAssessmentComplete(szAssessmentName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventObjectiveStart(String szObjectiveName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventObjectiveStart(szObjectiveName ?? "", dictMeta);
		public static int EventObjectiveComplete(String szObjectiveName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventObjectiveComplete(szObjectiveName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventInteractionStart(String szInteractionName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventInteractionStart(szInteractionName ?? "", dictMeta);
		public static int EventInteractionComplete(String szInteractionName, String szResult, String szResultDetails, int eInteractionType, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventInteractionComplete(szInteractionName ?? "", szResult ?? "", szResultDetails ?? "", eInteractionType, dictMeta);
		// ---
		public static int EventLevelStart(String szLevelName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventLevelStart(szLevelName ?? "", dictMeta);
		public static int EventLevelComplete(String szLevelName, String szScore, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.EventLevelComplete(szLevelName ?? "", szScore ?? "", dictMeta);
		// ---
		public static int AddAIProxy(String szPrompt, String szPastMessages, String szLMMProvider) => ArborInsightServiceBridge.AddAIProxy(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		public static int AddAIProxyDeferred(String szPrompt, String szPastMessages, String szLMMProvider) => ArborInsightServiceBridge.AddAIProxyDeferred(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		// ---
		public static int AddTelemetryEntry(String szName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.AddTelemetryEntry(szName ?? "", dictMeta);
		public static int AddTelemetryEntryDeferred(String szName, Dictionary<String, String> dictMeta) => ArborInsightServiceBridge.AddTelemetryEntryDeferred(szName ?? "", dictMeta);
		// ---
		//boolean platformIsWindows();
		// --- Authentication fields. get_ApiToken, get_ApiSecret, get_AppConfigAuthMechanism remain for non-auth use; userId/userData come from authRequest response only.
		internal static String get_ApiToken() => ArborInsightServiceBridge.get_ApiToken();
		public static void set_ApiToken(String szApiToken) => ArborInsightServiceBridge.set_ApiToken(szApiToken ?? "");
		// ---
		internal static String get_ApiSecret() => ArborInsightServiceBridge.get_ApiSecret();
		public static void set_ApiSecret(String szApiSecret) => ArborInsightServiceBridge.set_ApiSecret(szApiSecret ?? "");
		// ---
		public static void set_AppToken(String szAppToken) => ArborInsightServiceBridge.set_AppToken(szAppToken ?? "");
		// ---
		public static void set_OrgToken(String szOrgToken) => ArborInsightServiceBridge.set_OrgToken(szOrgToken ?? "");
		public static void set_SSOAccessToken(String szSSOAccessToken) => ArborInsightServiceBridge.set_SSOAccessToken(szSSOAccessToken ?? "");
		// ---
		public static void set_AppID(String szAppID) => ArborInsightServiceBridge.set_AppID(szAppID ?? "");
		// ---
		public static void set_OrgID(String szOrgID) => ArborInsightServiceBridge.set_OrgID(szOrgID ?? "");
		// ---
		public static void set_AuthSecret(String szAuthSecret) => ArborInsightServiceBridge.set_AuthSecret(szAuthSecret ?? "");
		// ---
		public static void set_BuildType(String szBuildType) => ArborInsightServiceBridge.set_BuildType(szBuildType ?? "");
		// ---
		public static void set_DeviceID(String szDeviceID) => ArborInsightServiceBridge.set_DeviceID(szDeviceID ?? "");
		// ---
		public static void set_UserID(String szUserID) => ArborInsightServiceBridge.set_UserID(szUserID ?? "");
		// ---
		public static void set_TokenExpiration(long dtTokenExpiration) => ArborInsightServiceBridge.set_TokenExpiration(dtTokenExpiration);
		// ---
		public static bool TokenExpirationImminent() => ArborInsightServiceBridge.TokenExpirationImminent();
		// ---
		public static void set_Partner(int ePartner) => ArborInsightServiceBridge.set_Partner(ePartner);
		// --- Environment/session globals that get sent with the auth payload in Authenticate() functions.
		public static void set_OsVersion(String szOsVersion) => ArborInsightServiceBridge.set_OsVersion(szOsVersion ?? "");
		// ---
		public static void set_IpAddress(String szIpAddress) => ArborInsightServiceBridge.set_IpAddress(szIpAddress ?? "");
		// ---
		public static void set_XrdmVersion(String szXrdmVersion) => ArborInsightServiceBridge.set_XrdmVersion(szXrdmVersion ?? "");
		// ---
		public static void set_AppVersion(String szAppVersion) => ArborInsightServiceBridge.set_AppVersion(szAppVersion ?? "");
		// ---
		public static void set_UnityVersion(String szUnityVersion) => ArborInsightServiceBridge.set_UnityVersion(szUnityVersion ?? "");
		// ---
		public static void set_AbxrLibType(String szAbxrLibType) => ArborInsightServiceBridge.set_AbxrLibType(szAbxrLibType ?? "");
		// ---
		public static void set_AbxrLibVersion(String szAbxrLibVersion) => ArborInsightServiceBridge.set_AbxrLibVersion(szAbxrLibVersion ?? "");
		// ---
		public static void set_DeviceModel(String szDeviceModel) => ArborInsightServiceBridge.set_DeviceModel(szDeviceModel ?? "");
		// --- Build fingerprint (optional on AAR)
		public static void set_BuildFingerprint(String szBuildFingerprint) => ArborInsightServiceBridge.set_BuildFingerprint(szBuildFingerprint ?? "");
		// ---
		public static void set_UserId(String szUserId) => ArborInsightServiceBridge.set_UserId(szUserId ?? "");
		// ---
		public static void set_Tags(List<String> lszTags) => ArborInsightServiceBridge.set_Tags(lszTags);
		// ---
		public static void set_GeoLocation(Dictionary<String, String> dictValue) => ArborInsightServiceBridge.set_GeoLocation(dictValue);
		// ---
		public static void set_SessionAuthMechanism(Dictionary<String, String> dictValue) => ArborInsightServiceBridge.set_SessionAuthMechanism(dictValue);
		// --- Environment / Storage functions.
		public static String StorageGetDefaultEntryAsString() => ArborInsightServiceBridge.StorageGetDefaultEntryAsString();
		public static String StorageGetEntryAsString(String szName) => ArborInsightServiceBridge.StorageGetEntryAsString(szName ?? "");
		// ---
		public static int StorageSetDefaultEntryFromString(String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => ArborInsightServiceBridge.StorageSetDefaultEntryFromString(szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		public static int StorageSetEntryFromString(String szName, String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => ArborInsightServiceBridge.StorageSetEntryFromString(szName ?? "", szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		// ---
		public static int StorageRemoveDefaultEntry() => ArborInsightServiceBridge.StorageRemoveDefaultEntry();
		public static int StorageRemoveEntry(String szName) => ArborInsightServiceBridge.StorageRemoveEntry(szName ?? "");
		public static int StorageRemoveMultipleEntries(bool bSessionOnly) => ArborInsightServiceBridge.StorageRemoveMultipleEntries(bSessionOnly);
		// --- Configuration fields.
		public static void set_RestUrl(String szValue) => ArborInsightServiceBridge.set_RestUrl(szValue ?? "");
		// ---
		/// <summary>Sets REST URL and all auth-related session fields on the service from the given auth payload. Call once per auth request before AuthRequest(). Keeps service-path auth setup in one place and limits divergence from the standalone auth path. Internal so AuthPayload can stay internal.</summary>
		internal static void SetAuthPayloadForRequest(string restUrl, AbxrLib.Runtime.Authentication.Authentication.AuthPayload data, int partner)
		{
			set_RestUrl(restUrl ?? "https://lib-backend.xrdm.app/");
			if (data.appId != null) set_AppID(data.appId);
			if (data.orgId != null) set_OrgID(data.orgId);
			if (data.authSecret != null) set_AuthSecret(data.authSecret);
			if (data.appToken != null) set_AppToken(data.appToken);
			if (data.orgToken != null) set_OrgToken(data.orgToken);
			if (data.SSOAccessToken != null) set_SSOAccessToken(data.SSOAccessToken);
			if (data.buildType != null) set_BuildType(data.buildType);
			if (data.deviceId != null) set_DeviceID(data.deviceId);
			if (data.userId != null) set_UserID(data.userId);
			set_Partner(partner);
			if (data.ipAddress != null) set_IpAddress(data.ipAddress);
			if (data.deviceModel != null) set_DeviceModel(data.deviceModel);
			set_GeoLocation(data.geolocation ?? new Dictionary<string, string>());
			if (data.osVersion != null) set_OsVersion(data.osVersion);
			if (data.xrdmVersion != null) set_XrdmVersion(data.xrdmVersion);
			if (data.appVersion != null) set_AppVersion(data.appVersion);
			if (data.unityVersion != null) set_UnityVersion(data.unityVersion);
			if (data.abxrLibType != null) set_AbxrLibType(data.abxrLibType);
			if (data.abxrLibVersion != null) set_AbxrLibVersion(data.abxrLibVersion);
			if (data.buildFingerprint != null) set_BuildFingerprint(data.buildFingerprint);
			if (data.tags != null) set_Tags(data.tags.ToList());
		}
		// ---
		public static void set_SendRetriesOnFailure(int nValue) => ArborInsightServiceBridge.set_SendRetriesOnFailure(nValue);
		// ---
		public static void set_SendRetryInterval(double tsValue) => ArborInsightServiceBridge.set_SendRetryInterval(tsValue);
		// ---
		public static void set_SendNextBatchWait(double tsValue) => ArborInsightServiceBridge.set_SendNextBatchWait(tsValue);
		// ---
		public static void set_StragglerTimeout(double tsValue) => ArborInsightServiceBridge.set_StragglerTimeout(tsValue);
		// ---
		public static void set_PositionCapturePeriodicity(double dValue) => ArborInsightServiceBridge.set_PositionCapturePeriodicity(dValue);
		// ---
		public static void set_FrameRateCapturePeriodicity(double dValue) => ArborInsightServiceBridge.set_FrameRateCapturePeriodicity(dValue);
		// ---
		public static void set_TelemetryCapturePeriodicity(double dValue) => ArborInsightServiceBridge.set_TelemetryCapturePeriodicity(dValue);
		// ---
		public static void set_DataItemsPerSendAttempt(int nValue) => ArborInsightServiceBridge.set_DataItemsPerSendAttempt(nValue);
		// ---
		public static void set_StorageEntriesPerSendAttempt(int nValue) => ArborInsightServiceBridge.set_StorageEntriesPerSendAttempt(nValue);
		// ---
		public static void set_PruneSentItemsOlderThan(double tsValue) => ArborInsightServiceBridge.set_PruneSentItemsOlderThan(tsValue);
		// ---
		public static void set_MaximumCachedItems(int nValue) => ArborInsightServiceBridge.set_MaximumCachedItems(nValue);
		// ---
		public static void set_RetainLocalAfterSent(bool bValue) => ArborInsightServiceBridge.set_RetainLocalAfterSent(bValue);
		// ---
		public static void set_ReAuthenticateBeforeTokenExpires(bool bValue) => ArborInsightServiceBridge.set_ReAuthenticateBeforeTokenExpires(bValue);
		// ---
		public static void set_UseDatabase(bool bValue) => ArborInsightServiceBridge.set_UseDatabase(bValue);
		// ---
		internal static Dictionary<String, String> get_AppConfigAuthMechanism() => ArborInsightServiceBridge.get_AppConfigAuthMechanism();
		public static void set_AppConfigAuthMechanism(Dictionary<String, String> dictValue) => ArborInsightServiceBridge.set_AppConfigAuthMechanism(dictValue);
		internal static string GetAppConfig() => ArborInsightServiceBridge.GetAppConfig();
		// ---
		public static bool ReadConfig() => ArborInsightServiceBridge.ReadConfig();
	}
}

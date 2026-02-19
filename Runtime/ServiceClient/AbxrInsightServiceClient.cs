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
	internal static class AbxrInsightServiceBridge
	{
		private const string		PackageName = "aar.xrdi.abxrinsightservice.unity.UnityAbxrInsightServiceClient";
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
		public static string WhatTimeIsIt() => _client.Call<string>("whatTimeIsIt");
		public static bool IsServiceBound() => _client.Call<bool>("isServiceBound");
		public static bool IsServiceAvailable() => _client.Call<bool>("isServiceAvailable");
		public static bool ServiceIsFullyInitialized() => _client.Call<bool>("serviceIsFullyInitialized");
		// --- API code.
		public static void AbxrLibInitStart() => _client.Call<int>("abxrLibInitStart");
		public static void AbxrLibInitEnd() => _client.Call<int>("abxrLibInitEnd");
		// ---
		public static int AuthRequest(String szUserId, String dictAdditionalUserData)
		{
			if (_client == null)
			{
				Debug.LogError($"[AbxrInsightServiceClient] AuthRequest called but _client is null!");
				return 0;
			}
			try
			{
				Debug.Log($"[AbxrInsightServiceClient] AbxrInsightServiceBridge.AuthRequest() calling _client.Call<int>(\"authRequest\", ...)");
				int result = _client.Call<int>("authRequest", szUserId ?? "", dictAdditionalUserData ?? "");
				Debug.Log($"[AbxrInsightServiceClient] AbxrInsightServiceBridge.AuthRequest() returned: {result}");
				return result;
			}
			catch (Exception e)
			{
				Debug.LogError($"[AbxrInsightServiceClient] AuthRequest exception: {e.GetType().Name}: {e.Message}\nStackTrace: {e.StackTrace}");
				return 0;
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
		// ---
		public static String get_UserId() => _client.Call<String>("get_UserId");
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

	/// <summary>
	/// Due to marshalling, the AbxrInsightServiceClient calls that use enums need to represent them with ints.
	/// This is for the ones that return AbxrResult.
	/// Co-maintain with Kotlin service DotNetishTypes.kt.
	/// </summary>
	enum AbxrResult
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
		AUTHENTICATE_FAILED_SERVICE_NULL,
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
		// --- API CALLS.
		public static bool Bind(string explicitPackage = null) => AbxrInsightServiceBridge.Bind(explicitPackage);
		/// <summary>
		/// IsInitialized().
		/// </summary>
		/// <returns></returns>
		public static bool IsInitialized() => AbxrInsightServiceBridge.IsInitialized();
		public static void Unbind() => AbxrInsightServiceBridge.Unbind();
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) => AbxrInsightServiceBridge.BasicTypes(anInt, aLong, aBoolean, aFloat, aDouble, aString);
		public static string WhatTimeIsIt() => AbxrInsightServiceBridge.WhatTimeIsIt();
		public static bool IsServiceBound() => AbxrInsightServiceBridge.IsServiceBound();
		public static bool IsServiceAvailable() => AbxrInsightServiceBridge.IsServiceAvailable();
		public static bool ServiceIsFullyInitialized() => AbxrInsightServiceBridge.ServiceIsFullyInitialized();
		// --- API code.
		public static void AbxrLibInitStart() => AbxrInsightServiceBridge.AbxrLibInitStart();
		public static void AbxrLibInitEnd() => AbxrInsightServiceBridge.AbxrLibInitEnd();
		// ---
		public static int AuthRequest(String szUserId, String dictAdditionalUserData) => AbxrInsightServiceBridge.AuthRequest(szUserId ?? "", dictAdditionalUserData ?? "");
		// ---
		public static int Authenticate(String szAppId, String szOrgId, String szDeviceId, String szAuthSecret, int ePartner) => AbxrInsightServiceBridge.Authenticate(szAppId ?? "", szOrgId ?? "", szDeviceId ?? "", szAuthSecret ?? "", ePartner);
		public static int FinalAuthenticate() => AbxrInsightServiceBridge.FinalAuthenticate();
		public static int ReAuthenticate(bool bObtainAuthSecret) => AbxrInsightServiceBridge.ReAuthenticate(bObtainAuthSecret);
		public static int ForceSendUnsent() => AbxrInsightServiceBridge.ForceSendUnsent();
		// ---
		public static void CaptureTimeStamp() => AbxrInsightServiceBridge.CaptureTimeStamp();
		public static void UnCaptureTimeStamp() => AbxrInsightServiceBridge.UnCaptureTimeStamp();
		// ---
		public static int LogDebug(String szText, Dictionary<string, string> dictMeta) => AbxrInsightServiceBridge.LogDebug(szText ?? "", dictMeta);
		public static int LogDebugDeferred(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogDebugDeferred(szText ?? "", dictMeta);
		public static int LogInfo(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogInfo(szText ?? "", dictMeta);
		public static int LogInfoDeferred(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogInfoDeferred(szText, dictMeta);
		public static int LogWarn(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogWarn(szText ?? "", dictMeta);
		public static int LogWarnDeferred(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogWarnDeferred(szText ?? "", dictMeta);
		public static int LogError(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogError(szText ?? "", dictMeta);
		public static int LogErrorDeferred(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogErrorDeferred(szText ?? "", dictMeta);
		public static int LogCritical(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogCritical(szText ?? "", dictMeta);
		public static int LogCriticalDeferred(String szText, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.LogCriticalDeferred(szText ?? "", dictMeta);
		// ---
		public static int Event(String szMessage, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.Event(szMessage ?? "", dictMeta);
		public static int EventDeferred(String szMessage, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventDeferred(szMessage ?? "", dictMeta);
		// --- Convenient wrappers for particular forms of events.
		public static int EventAssessmentStart(String szAssessmentName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventAssessmentStart(szAssessmentName ?? "", dictMeta);
		public static int EventAssessmentComplete(String szAssessmentName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventAssessmentComplete(szAssessmentName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventObjectiveStart(String szObjectiveName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventObjectiveStart(szObjectiveName ?? "", dictMeta);
		public static int EventObjectiveComplete(String szObjectiveName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventObjectiveComplete(szObjectiveName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventInteractionStart(String szInteractionName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventInteractionStart(szInteractionName ?? "", dictMeta);
		public static int EventInteractionComplete(String szInteractionName, String szResult, String szResultDetails, int eInteractionType, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventInteractionComplete(szInteractionName ?? "", szResult ?? "", szResultDetails ?? "", eInteractionType, dictMeta);
		// ---
		public static int EventLevelStart(String szLevelName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventLevelStart(szLevelName ?? "", dictMeta);
		public static int EventLevelComplete(String szLevelName, String szScore, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.EventLevelComplete(szLevelName ?? "", szScore ?? "", dictMeta);
		// ---
		public static int AddAIProxy(String szPrompt, String szPastMessages, String szLMMProvider) => AbxrInsightServiceBridge.AddAIProxy(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		public static int AddAIProxyDeferred(String szPrompt, String szPastMessages, String szLMMProvider) => AbxrInsightServiceBridge.AddAIProxyDeferred(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		// ---
		public static int AddTelemetryEntry(String szName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.AddTelemetryEntry(szName ?? "", dictMeta);
		public static int AddTelemetryEntryDeferred(String szName, Dictionary<String, String> dictMeta) => AbxrInsightServiceBridge.AddTelemetryEntryDeferred(szName ?? "", dictMeta);
		// ---
		//boolean platformIsWindows();
		// --- Authentication fields.
		public static String get_ApiToken() => AbxrInsightServiceBridge.get_ApiToken();
		public static void set_ApiToken(String szApiToken) => AbxrInsightServiceBridge.set_ApiToken(szApiToken ?? "");
		// ---
		public static String get_ApiSecret() => AbxrInsightServiceBridge.get_ApiSecret();
		public static void set_ApiSecret(String szApiSecret) => AbxrInsightServiceBridge.set_ApiSecret(szApiSecret ?? "");
		// ---
		public static String get_AppToken() => AbxrInsightServiceBridge.get_AppToken();
		public static void set_AppToken(String szAppToken) => AbxrInsightServiceBridge.set_AppToken(szAppToken ?? "");
		// ---
		public static String get_AppID() => AbxrInsightServiceBridge.get_AppID();
		public static void set_AppID(String szAppID) => AbxrInsightServiceBridge.set_AppID(szAppID ?? "");
		// ---
		public static String get_OrgID() => AbxrInsightServiceBridge.get_OrgID();
		public static void set_OrgID(String szOrgID) => AbxrInsightServiceBridge.set_OrgID(szOrgID ?? "");
		// ---
		public static String get_AuthSecret() => AbxrInsightServiceBridge.get_AuthSecret();
		public static void set_AuthSecret(String szAuthSecret) => AbxrInsightServiceBridge.set_AuthSecret(szAuthSecret ?? "");
		// ---
		public static String get_BuildType() => AbxrInsightServiceBridge.get_BuildType();
		public static void set_BuildType(String szBuildType) => AbxrInsightServiceBridge.set_BuildType(szBuildType ?? "");
		// ---
		public static String get_DeviceID() => AbxrInsightServiceBridge.get_DeviceID();
		public static void set_DeviceID(String szDeviceID) => AbxrInsightServiceBridge.set_DeviceID(szDeviceID ?? "");
		// ---
		public static String get_UserID() => AbxrInsightServiceBridge.get_UserID();
		public static void set_UserID(String szUserID) => AbxrInsightServiceBridge.set_UserID(szUserID ?? "");
		// ---
		public static long get_TokenExpiration() => AbxrInsightServiceBridge.get_TokenExpiration();
		public static void set_TokenExpiration(long dtTokenExpiration) => AbxrInsightServiceBridge.set_TokenExpiration(dtTokenExpiration);
		// ---
		public static bool TokenExpirationImminent() => AbxrInsightServiceBridge.TokenExpirationImminent();
		// ---
		public static int get_Partner() => AbxrInsightServiceBridge.get_Partner();
		public static void set_Partner(int ePartner) => AbxrInsightServiceBridge.set_Partner(ePartner);
		// --- Environment/session globals that get sent with the auth payload in Authenticate() functions.
		public static String get_OsVersion() => AbxrInsightServiceBridge.get_OsVersion();
		public static void set_OsVersion(String szOsVersion) => AbxrInsightServiceBridge.set_OsVersion(szOsVersion ?? "");
		// ---
		public static String get_IpAddress() => AbxrInsightServiceBridge.get_IpAddress();
		public static void set_IpAddress(String szIpAddress) => AbxrInsightServiceBridge.set_IpAddress(szIpAddress ?? "");
		// ---
		public static String get_XrdmVersion() => AbxrInsightServiceBridge.get_XrdmVersion();
		public static void set_XrdmVersion(String szXrdmVersion) => AbxrInsightServiceBridge.set_XrdmVersion(szXrdmVersion ?? "");
		// ---
		public static String get_AppVersion() => AbxrInsightServiceBridge.get_AppVersion();
		public static void set_AppVersion(String szAppVersion) => AbxrInsightServiceBridge.set_AppVersion(szAppVersion ?? "");
		// ---
		public static String get_UnityVersion() => AbxrInsightServiceBridge.get_UnityVersion();
		public static void set_UnityVersion(String szUnityVersion) => AbxrInsightServiceBridge.set_UnityVersion(szUnityVersion ?? "");
		// ---
		public static String get_AbxrLibType() => AbxrInsightServiceBridge.get_AbxrLibType();
		public static void set_AbxrLibType(String szAbxrLibType) => AbxrInsightServiceBridge.set_AbxrLibType(szAbxrLibType ?? "");
		// ---
		public static String get_AbxrLibVersion() => AbxrInsightServiceBridge.get_AbxrLibVersion();
		public static void set_AbxrLibVersion(String szAbxrLibVersion) => AbxrInsightServiceBridge.set_AbxrLibVersion(szAbxrLibVersion ?? "");
		// ---
		// Not sure about this one... seems to be an artifact of an earlier time.  It is in the C++ code but only as a data member that is not used anywhere.
		//String get_DataPath();
		//void set_DataPath(String szDataPath);
		// ---
		public static String get_DeviceModel() => AbxrInsightServiceBridge.get_DeviceModel();
		public static void set_DeviceModel(String szDeviceModel) => AbxrInsightServiceBridge.set_DeviceModel(szDeviceModel ?? "");
		// ---
		public static String get_UserId() => AbxrInsightServiceBridge.get_UserId();
		public static void set_UserId(String szUserId) => AbxrInsightServiceBridge.set_UserId(szUserId ?? "");
		// ---
		public static List<String> get_Tags() => AbxrInsightServiceBridge.get_Tags();
		public static void set_Tags(List<String> lszTags) => AbxrInsightServiceBridge.set_Tags(lszTags);
		// ---
		public static Dictionary<String, String> get_GeoLocation() => AbxrInsightServiceBridge.get_GeoLocation();
		public static void set_GeoLocation(Dictionary<String, String> dictValue) => AbxrInsightServiceBridge.set_GeoLocation(dictValue);
		// ---
		public static Dictionary<String, String> get_SessionAuthMechanism() => AbxrInsightServiceBridge.get_SessionAuthMechanism();
		public static void set_SessionAuthMechanism(Dictionary<String, String> dictValue) => AbxrInsightServiceBridge.set_SessionAuthMechanism(dictValue);
		// --- Environment / Storage functions.
		public static String StorageGetDefaultEntryAsString() => AbxrInsightServiceBridge.StorageGetDefaultEntryAsString();
		public static String StorageGetEntryAsString(String szName) => AbxrInsightServiceBridge.StorageGetEntryAsString(szName ?? "");
		// ---
		public static int StorageSetDefaultEntryFromString(String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => AbxrInsightServiceBridge.StorageSetDefaultEntryFromString(szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		public static int StorageSetEntryFromString(String szName, String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => AbxrInsightServiceBridge.StorageSetEntryFromString(szName ?? "", szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		// ---
		public static int StorageRemoveDefaultEntry() => AbxrInsightServiceBridge.StorageRemoveDefaultEntry();
		public static int StorageRemoveEntry(String szName) => AbxrInsightServiceBridge.StorageRemoveEntry(szName ?? "");
		public static int StorageRemoveMultipleEntries(bool bSessionOnly) => AbxrInsightServiceBridge.StorageRemoveMultipleEntries(bSessionOnly);
		// --- Configuration fields.
		public static String get_RestUrl() => AbxrInsightServiceBridge.get_RestUrl();
		public static void set_RestUrl(String szValue) => AbxrInsightServiceBridge.set_RestUrl(szValue ?? "");
		// ---
		public static int get_SendRetriesOnFailure() => AbxrInsightServiceBridge.get_SendRetriesOnFailure();
		public static void set_SendRetriesOnFailure(int nValue) => AbxrInsightServiceBridge.set_SendRetriesOnFailure(nValue);
		// ---
		public static double get_SendRetryInterval() => AbxrInsightServiceBridge.get_SendRetryInterval();
		public static void set_SendRetryInterval(double tsValue) => AbxrInsightServiceBridge.set_SendRetryInterval(tsValue);
		// ---
		public static double get_SendNextBatchWait() => AbxrInsightServiceBridge.get_SendNextBatchWait();
		public static void set_SendNextBatchWait(double tsValue) => AbxrInsightServiceBridge.set_SendRetryInterval(tsValue);
		// ---
		public static double get_StragglerTimeout() => AbxrInsightServiceBridge.get_StragglerTimeout();
		public static void set_StragglerTimeout(double tsValue) => AbxrInsightServiceBridge.set_StragglerTimeout(tsValue);
		// ---
		public static double get_PositionCapturePeriodicity() => AbxrInsightServiceBridge.get_PositionCapturePeriodicity();
		public static void set_PositionCapturePeriodicity(double dValue) => AbxrInsightServiceBridge.set_PositionCapturePeriodicity(dValue);
		// ---
		public static double get_FrameRateCapturePeriodicity() => AbxrInsightServiceBridge.get_FrameRateCapturePeriodicity();
		public static void set_FrameRateCapturePeriodicity(double dValue) => AbxrInsightServiceBridge.set_FrameRateCapturePeriodicity(dValue);
		// ---
		public static double get_TelemetryCapturePeriodicity() => AbxrInsightServiceBridge.get_TelemetryCapturePeriodicity();
		public static void set_TelemetryCapturePeriodicity(double dValue) => AbxrInsightServiceBridge.set_TelemetryCapturePeriodicity(dValue);
		// ---
		public static int get_DataItemsPerSendAttempt() => AbxrInsightServiceBridge.get_DataItemsPerSendAttempt();
		public static void set_DataItemsPerSendAttempt(int nValue) => AbxrInsightServiceBridge.set_DataItemsPerSendAttempt(nValue);
		// ---
		public static int get_StorageEntriesPerSendAttempt() => AbxrInsightServiceBridge.get_StorageEntriesPerSendAttempt();
		public static void set_StorageEntriesPerSendAttempt(int nValue) => AbxrInsightServiceBridge.set_StorageEntriesPerSendAttempt(nValue);
		// ---
		public static double get_PruneSentItemsOlderThan() => AbxrInsightServiceBridge.get_PruneSentItemsOlderThan();
		public static void set_PruneSentItemsOlderThan(double tsValue) => AbxrInsightServiceBridge.set_PruneSentItemsOlderThan(tsValue);
		// ---
		public static int get_MaximumCachedItems() => AbxrInsightServiceBridge.get_MaximumCachedItems();
		public static void set_MaximumCachedItems(int nValue) => AbxrInsightServiceBridge.set_MaximumCachedItems(nValue);
		// ---
		public static bool get_RetainLocalAfterSent() => AbxrInsightServiceBridge.get_RetainLocalAfterSent();
		public static void set_RetainLocalAfterSent(bool bValue) => AbxrInsightServiceBridge.set_RetainLocalAfterSent(bValue);
		// ---
		public static bool get_ReAuthenticateBeforeTokenExpires() => AbxrInsightServiceBridge.get_ReAuthenticateBeforeTokenExpires();
		public static void set_ReAuthenticateBeforeTokenExpires(bool bValue) => AbxrInsightServiceBridge.set_ReAuthenticateBeforeTokenExpires(bValue);
		// ---
		public static bool get_UseDatabase() => AbxrInsightServiceBridge.get_UseDatabase();
		public static void set_UseDatabase(bool bValue) => AbxrInsightServiceBridge.set_UseDatabase(bValue);
		// ---
		public static Dictionary<String, String> get_AppConfigAuthMechanism() => AbxrInsightServiceBridge.get_AppConfigAuthMechanism();
		public static void set_AppConfigAuthMechanism(Dictionary<String, String> dictValue) => AbxrInsightServiceBridge.set_AppConfigAuthMechanism(dictValue);
		// ---
		public static bool ReadConfig() => AbxrInsightServiceBridge.ReadConfig();
	}
}

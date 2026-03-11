//#nullable enable

using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Platform
{
	internal static class ArborInsightsClientBridge
	{
		private const string		PackageName = "app.xrdi.client.Service";
		/// <summary>Package name of the ArborInsightsClient APK (impl app). Used to check if the service is installed before waiting for bind.</summary>
		private const string		ServiceApkPackageName = "app.xrdi.client";
		static AndroidJavaObject	_client = null;

		static AndroidJavaObject Activity => new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");

		/// <summary>Returns true if the ArborInsightsClient APK is installed on the device. Use to skip the readiness poll when not installed.</summary>
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
				Logcat.Warning($"[ArborInsightsClient] Init failed ({PackageName}): {e.Message}");
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
				Logcat.Warning("[ArborInsightsClient] Bind() skipped: bridge not initialized (Unity ArborInsightsClient AAR may be missing from Plugins/Android).");
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
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) { if (_client != null) _client.Call<int>("basicTypes", anInt, aLong, aBoolean, aFloat, aDouble, aString); }
		public static string WhatTimeIsIt() => _client != null ? _client.Call<string>("whatTimeIsIt") : null;
		public static bool IsServiceBound() => _client != null && _client.Call<bool>("isServiceBound");
		public static bool IsServiceAvailable() => _client != null && _client.Call<bool>("isServiceAvailable");
		/// <summary>True if the service reports fully initialized. Never throws; returns false if the bridge is unavailable or JNI fails.</summary>
		public static bool ServiceIsFullyInitialized()
		{
			if (_client == null) return false;
			try { return _client.Call<bool>("serviceIsFullyInitialized"); }
			catch { return false; }
		}
		// --- API code.
		public static void AbxrLibInitStart() { if (_client != null) _client.Call<int>("abxrLibInitStart"); }
		public static void AbxrLibInitEnd() { if (_client != null) _client.Call<int>("abxrLibInitEnd"); }
		// ---
		public static String AuthRequest(String szUserId, String dictAdditionalUserData)
		{
			if (_client == null)
			{
				Logcat.Error($"[ArborInsightsClient] AuthRequest called but _client is null!");
				return "{\"result\":0}";
			}
			try
			{
				return _client.Call<String>("authRequest", szUserId ?? "", dictAdditionalUserData ?? "") ?? "{\"result\":0}";
			}
			catch (Exception e)
			{
				Logcat.Error($"[ArborInsightsClient] AuthRequest exception: {e.GetType().Name}: {e.Message}\nStackTrace: {e.StackTrace}");
				return "{\"result\":0}";
			}
		}
		// ---
		public static int Authenticate(String szAppId, String szOrgId, String szDeviceId, String szAuthSecret, int ePartner) => _client.Call<int>("authenticate", szAppId, szOrgId, szDeviceId, szAuthSecret, ePartner);
		public static int FinalAuthenticate() => _client.Call<int>("finalAuthenticate");
		public static int SetAuthFromHandoff(string szAuthResponseJson, string szRestUrl)
		{
			if (_client == null) return (int)AbxrResult.NOT_INITIALIZED;
			try { return _client.Call<int>("setAuthFromHandoff", szAuthResponseJson ?? "", szRestUrl ?? ""); }
			catch (Exception e) { Logcat.Warning($"[ArborInsightsClient] SetAuthFromHandoff failed: {e.Message}"); return (int)AbxrResult.NOT_INITIALIZED; }
		}
		public static int ReAuthenticate(bool bObtainAuthSecret) => _client.Call<int>("reAuthenticate", bObtainAuthSecret);
		public static int ForceSendUnsent() => _client.Call<int>("forceSendUnsent");
		// ---
		public static void CaptureTimeStamp() => _client.Call<int>("captureTimeStamp");
		public static void UnCaptureTimeStamp() => _client.Call<int>("unCaptureTimeStamp");
		// ---
		// Default = non-blocking; use *Blocking for sync/debugging.
		public static int LogDebug(String szText, Dictionary<string, string> dictMeta) => _client.Call<int>("logDebug", szText, Utils.DictToString(dictMeta));
		public static int LogDebugBlocking(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logDebugBlocking", szText, Utils.DictToString(dictMeta));
		public static int LogInfo(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logInfo", szText, Utils.DictToString(dictMeta));
		public static int LogInfoBlocking(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logInfoBlocking", szText, Utils.DictToString(dictMeta));
		public static int LogWarn(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logWarn", szText, Utils.DictToString(dictMeta));
		public static int LogWarnBlocking(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logWarnBlocking", szText, Utils.DictToString(dictMeta));
		public static int LogError(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logError", szText, Utils.DictToString(dictMeta));
		public static int LogErrorBlocking(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logErrorBlocking", szText, Utils.DictToString(dictMeta));
		public static int LogCritical(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logCritical", szText, Utils.DictToString(dictMeta));
		public static int LogCriticalBlocking(String szText, Dictionary<String, String> dictMeta) => _client.Call<int>("logCriticalBlocking", szText, Utils.DictToString(dictMeta));
		// ---
		public static int Event(String szMessage, Dictionary<String, String> dictMeta) => _client.Call<int>("event", szMessage, Utils.DictToString(dictMeta));
		public static int EventBlocking(String szMessage, Dictionary<String, String> dictMeta) => _client.Call<int>("eventBlocking", szMessage, Utils.DictToString(dictMeta));
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
		public static int AddAIProxyBlocking(String szPrompt, String szPastMessages, String szLMMProvider) => _client.Call<int>("addAIProxyBlocking", szPrompt, szPastMessages, szLMMProvider);
		// ---
		public static int AddTelemetryEntry(String szName, Dictionary<String, String> dictMeta) => _client.Call<int>("addTelemetryEntry", szName, Utils.DictToString(dictMeta));
		public static int AddTelemetryEntryBlocking(String szName, Dictionary<String, String> dictMeta) => _client.Call<int>("addTelemetryEntryBlocking", szName, Utils.DictToString(dictMeta));
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
		public static void set_SSOAccessToken(String szSSOAccessToken) { try { _client?.Call("set_SSOAccessToken", szSSOAccessToken ?? ""); } catch (Exception) { /* AAR may not support yet */ } }
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
		// --- Build fingerprint (optional on AAR; no-op if AAR does not expose yet).
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
		/// <param name="szScope">CamelCase scope, e.g. "device" or "user", matching REST GET /v1/storage?name=&amp;scope=</param>
		public static String StorageGetEntryAsString(String szName, String szScope) => _client.Call<String>("storageGetEntryAsString", szName ?? "", szScope ?? "");
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
		/// <summary>Full app config as JSON (same shape as GET /v1/storage/config). Returns empty string if AAR does not support.</summary>
		public static string GetAppConfig() { try { return _client?.Call<String>("get_AppConfig") ?? ""; } catch (Exception) { return ""; } }
		// ---
		public static bool ReadConfig() => _client.Call<bool>("readConfig");
	}

	/// <summary>
	/// Due to marshalling, the ArborInsightsClient calls that use enums need to represent them with ints.
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
	public class ArborInsightsClient
	{
		//private const string				PackageName = "aar.xrdi.abxrinsightservice";
		//private AndroidJavaObject			_mjpsdk = null;
		//private MJPNativeConnectionCallback	_nativeCallback = null;
		
		public void Start()
		{
			try
			{
				ArborInsightsClientBridge.Init();
				if (!ArborInsightsClientBridge.IsInitialized())
				{
					Logcat.Warning("[ArborInsightsClient] Init failed (ClassNotFoundException usually means the ArborInsightsClient unity-client AAR is not in Assets/Plugins/Android). Skipping Bind().");
					return;
				}
				ArborInsightsClientBridge.Bind();
			}
			catch (Exception e)
			{
				Logcat.Warning($"[ArborInsightsClient] Bind failed: {e.Message}");
			}
		}
		public void Stop()
		{
			ArborInsightsClientBridge.Unbind();
		}
		// --- API CALLS.
		public static bool Bind(string explicitPackage = null) => ArborInsightsClientBridge.Bind(explicitPackage);
		/// <summary>
		/// IsInitialized().
		/// </summary>
		/// <returns></returns>
		public static bool IsInitialized() => ArborInsightsClientBridge.IsInitialized();
		public static void Unbind() => ArborInsightsClientBridge.Unbind();
		public static void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, String aString) => ArborInsightsClientBridge.BasicTypes(anInt, aLong, aBoolean, aFloat, aDouble, aString);
		public static string WhatTimeIsIt() => ArborInsightsClientBridge.WhatTimeIsIt();
		public static bool IsServiceBound() => ArborInsightsClientBridge.IsServiceBound();
		public static bool IsServiceAvailable() => ArborInsightsClientBridge.IsServiceAvailable();
		/// <summary>True if the ArborInsightsClient APK is installed. Use to fail fast and skip the readiness poll when running standalone.</summary>
		public static bool IsServicePackageInstalled() => ArborInsightsClientBridge.IsServicePackageInstalled();
		/// <summary>True if the service reports fully initialized. Never throws; returns false if the bridge is unavailable or JNI fails.</summary>
		public static bool ServiceIsFullyInitialized() => ArborInsightsClientBridge.ServiceIsFullyInitialized();
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
		public static void AbxrLibInitStart() => ArborInsightsClientBridge.AbxrLibInitStart();
		public static void AbxrLibInitEnd() => ArborInsightsClientBridge.AbxrLibInitEnd();
		// ---
		public static string AuthRequest(String szUserId, String dictAdditionalUserData) => ArborInsightsClientBridge.AuthRequest(szUserId ?? "", dictAdditionalUserData ?? "");
		// ---
		public static int Authenticate(String szAppId, String szOrgId, String szDeviceId, String szAuthSecret, int ePartner) => ArborInsightsClientBridge.Authenticate(szAppId ?? "", szOrgId ?? "", szDeviceId ?? "", szAuthSecret ?? "", ePartner);
		public static int FinalAuthenticate() => ArborInsightsClientBridge.FinalAuthenticate();
		public static int SetAuthFromHandoff(string szAuthResponseJson, string szRestUrl) => ArborInsightsClientBridge.SetAuthFromHandoff(szAuthResponseJson ?? "", szRestUrl ?? "");
		public static int ReAuthenticate(bool bObtainAuthSecret) => ArborInsightsClientBridge.ReAuthenticate(bObtainAuthSecret);
		public static int ForceSendUnsent() => ArborInsightsClientBridge.ForceSendUnsent();
		// ---
		public static void CaptureTimeStamp() => ArborInsightsClientBridge.CaptureTimeStamp();
		public static void UnCaptureTimeStamp() => ArborInsightsClientBridge.UnCaptureTimeStamp();
		// ---
		public static int LogDebug(String szText, Dictionary<string, string> dictMeta) => ArborInsightsClientBridge.LogDebug(szText ?? "", dictMeta);
		public static int LogDebugBlocking(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogDebugBlocking(szText ?? "", dictMeta);
		public static int LogInfo(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogInfo(szText ?? "", dictMeta);
		public static int LogInfoBlocking(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogInfoBlocking(szText, dictMeta);
		public static int LogWarn(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogWarn(szText ?? "", dictMeta);
		public static int LogWarnBlocking(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogWarnBlocking(szText ?? "", dictMeta);
		public static int LogError(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogError(szText ?? "", dictMeta);
		public static int LogErrorBlocking(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogErrorBlocking(szText ?? "", dictMeta);
		public static int LogCritical(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogCritical(szText ?? "", dictMeta);
		public static int LogCriticalBlocking(String szText, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.LogCriticalBlocking(szText ?? "", dictMeta);
		// ---
		public static int Event(String szMessage, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.Event(szMessage ?? "", dictMeta);
		public static int EventBlocking(String szMessage, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventBlocking(szMessage ?? "", dictMeta);
		// --- Convenient wrappers for particular forms of events.
		public static int EventAssessmentStart(String szAssessmentName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventAssessmentStart(szAssessmentName ?? "", dictMeta);
		public static int EventAssessmentComplete(String szAssessmentName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventAssessmentComplete(szAssessmentName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventObjectiveStart(String szObjectiveName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventObjectiveStart(szObjectiveName ?? "", dictMeta);
		public static int EventObjectiveComplete(String szObjectiveName, String szScore, int eResultOptions, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventObjectiveComplete(szObjectiveName ?? "", szScore ?? "", eResultOptions, dictMeta);
		// ---
		public static int EventInteractionStart(String szInteractionName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventInteractionStart(szInteractionName ?? "", dictMeta);
		public static int EventInteractionComplete(String szInteractionName, String szResult, String szResultDetails, int eInteractionType, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventInteractionComplete(szInteractionName ?? "", szResult ?? "", szResultDetails ?? "", eInteractionType, dictMeta);
		// ---
		public static int EventLevelStart(String szLevelName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventLevelStart(szLevelName ?? "", dictMeta);
		public static int EventLevelComplete(String szLevelName, String szScore, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.EventLevelComplete(szLevelName ?? "", szScore ?? "", dictMeta);
		// ---
		public static int AddAIProxy(String szPrompt, String szPastMessages, String szLMMProvider) => ArborInsightsClientBridge.AddAIProxy(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		public static int AddAIProxyBlocking(String szPrompt, String szPastMessages, String szLMMProvider) => ArborInsightsClientBridge.AddAIProxyBlocking(szPrompt ?? "", szPastMessages ?? "", szLMMProvider ?? "");
		// ---
		public static int AddTelemetryEntry(String szName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.AddTelemetryEntry(szName ?? "", dictMeta);
		public static int AddTelemetryEntryBlocking(String szName, Dictionary<String, String> dictMeta) => ArborInsightsClientBridge.AddTelemetryEntryBlocking(szName ?? "", dictMeta);
		// ---
		//boolean platformIsWindows();
		// --- Authentication fields. get_ApiToken and get_ApiSecret are internal for post-auth use; setters stay public to push auth payload.
		internal static String get_ApiToken() => ArborInsightsClientBridge.get_ApiToken();
		public static void set_ApiToken(String szApiToken) => ArborInsightsClientBridge.set_ApiToken(szApiToken ?? "");
		// ---
		internal static String get_ApiSecret() => ArborInsightsClientBridge.get_ApiSecret();
		public static void set_ApiSecret(String szApiSecret) => ArborInsightsClientBridge.set_ApiSecret(szApiSecret ?? "");
		// ---
		public static void set_AppToken(String szAppToken) => ArborInsightsClientBridge.set_AppToken(szAppToken ?? "");
		public static void set_OrgToken(String szOrgToken) => ArborInsightsClientBridge.set_OrgToken(szOrgToken ?? "");
		public static void set_SSOAccessToken(String szSSOAccessToken) => ArborInsightsClientBridge.set_SSOAccessToken(szSSOAccessToken ?? "");
		// ---
		public static void set_AppID(String szAppID) => ArborInsightsClientBridge.set_AppID(szAppID ?? "");
		// ---
		public static String get_OrgID() => ArborInsightsClientBridge.get_OrgID();
		public static void set_OrgID(String szOrgID) => ArborInsightsClientBridge.set_OrgID(szOrgID ?? "");
		// ---
		public static String get_AuthSecret() => ArborInsightsClientBridge.get_AuthSecret();
		public static void set_AuthSecret(String szAuthSecret) => ArborInsightsClientBridge.set_AuthSecret(szAuthSecret ?? "");
		// ---
		public static String get_BuildType() => ArborInsightsClientBridge.get_BuildType();
		public static void set_BuildType(String szBuildType) => ArborInsightsClientBridge.set_BuildType(szBuildType ?? "");
		// ---
		public static String get_DeviceID() => ArborInsightsClientBridge.get_DeviceID();
		public static void set_DeviceID(String szDeviceID) => ArborInsightsClientBridge.set_DeviceID(szDeviceID ?? "");
		// ---
		public static String get_UserID() => ArborInsightsClientBridge.get_UserID();
		public static void set_UserID(String szUserID) => ArborInsightsClientBridge.set_UserID(szUserID ?? "");
		// ---
		public static long get_TokenExpiration() => ArborInsightsClientBridge.get_TokenExpiration();
		public static void set_TokenExpiration(long dtTokenExpiration) => ArborInsightsClientBridge.set_TokenExpiration(dtTokenExpiration);
		// ---
		public static bool TokenExpirationImminent() => ArborInsightsClientBridge.TokenExpirationImminent();
		// ---
		public static int get_Partner() => ArborInsightsClientBridge.get_Partner();
		public static void set_Partner(int ePartner) => ArborInsightsClientBridge.set_Partner(ePartner);
		// --- Environment/session globals that get sent with the auth payload in Authenticate() functions.
		public static String get_OsVersion() => ArborInsightsClientBridge.get_OsVersion();
		public static void set_OsVersion(String szOsVersion) => ArborInsightsClientBridge.set_OsVersion(szOsVersion ?? "");
		// ---
		public static String get_IpAddress() => ArborInsightsClientBridge.get_IpAddress();
		public static void set_IpAddress(String szIpAddress) => ArborInsightsClientBridge.set_IpAddress(szIpAddress ?? "");
		// ---
		public static String get_XrdmVersion() => ArborInsightsClientBridge.get_XrdmVersion();
		public static void set_XrdmVersion(String szXrdmVersion) => ArborInsightsClientBridge.set_XrdmVersion(szXrdmVersion ?? "");
		// ---
		public static String get_AppVersion() => ArborInsightsClientBridge.get_AppVersion();
		public static void set_AppVersion(String szAppVersion) => ArborInsightsClientBridge.set_AppVersion(szAppVersion ?? "");
		// ---
		public static String get_UnityVersion() => ArborInsightsClientBridge.get_UnityVersion();
		public static void set_UnityVersion(String szUnityVersion) => ArborInsightsClientBridge.set_UnityVersion(szUnityVersion ?? "");
		// ---
		public static String get_AbxrLibType() => ArborInsightsClientBridge.get_AbxrLibType();
		public static void set_AbxrLibType(String szAbxrLibType) => ArborInsightsClientBridge.set_AbxrLibType(szAbxrLibType ?? "");
		// ---
		public static String get_AbxrLibVersion() => ArborInsightsClientBridge.get_AbxrLibVersion();
		public static void set_AbxrLibVersion(String szAbxrLibVersion) => ArborInsightsClientBridge.set_AbxrLibVersion(szAbxrLibVersion ?? "");
		// ---
		// Not sure about this one... seems to be an artifact of an earlier time.  It is in the C++ code but only as a data member that is not used anywhere.
		//String get_DataPath();
		//void set_DataPath(String szDataPath);
		// ---
		public static String get_DeviceModel() => ArborInsightsClientBridge.get_DeviceModel();
		public static void set_DeviceModel(String szDeviceModel) => ArborInsightsClientBridge.set_DeviceModel(szDeviceModel ?? "");
		// --- Build fingerprint (optional on AAR)
		public static void set_BuildFingerprint(String szBuildFingerprint) => ArborInsightsClientBridge.set_BuildFingerprint(szBuildFingerprint ?? "");
		// ---
		public static void set_UserId(String szUserId) => ArborInsightsClientBridge.set_UserId(szUserId ?? "");
		// ---
		public static List<String> get_Tags() => ArborInsightsClientBridge.get_Tags();
		public static void set_Tags(List<String> lszTags) => ArborInsightsClientBridge.set_Tags(lszTags);
		// ---
		public static Dictionary<String, String> get_GeoLocation() => ArborInsightsClientBridge.get_GeoLocation();
		public static void set_GeoLocation(Dictionary<String, String> dictValue) => ArborInsightsClientBridge.set_GeoLocation(dictValue);
		// ---
		public static Dictionary<String, String> get_SessionAuthMechanism() => ArborInsightsClientBridge.get_SessionAuthMechanism();
		public static void set_SessionAuthMechanism(Dictionary<String, String> dictValue) => ArborInsightsClientBridge.set_SessionAuthMechanism(dictValue);
		// --- Environment / Storage functions.
		public static String StorageGetDefaultEntryAsString() => ArborInsightsClientBridge.StorageGetDefaultEntryAsString();
		/// <param name="szScope">CamelCase scope, e.g. "device" or "user", matching REST GET /v1/storage?name=&amp;scope=</param>
		public static String StorageGetEntryAsString(String szName, String szScope) => ArborInsightsClientBridge.StorageGetEntryAsString(szName ?? "", szScope ?? "");
		// ---
		public static int StorageSetDefaultEntryFromString(String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => ArborInsightsClientBridge.StorageSetDefaultEntryFromString(szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		public static int StorageSetEntryFromString(String szName, String szStorageEntry, bool bKeepLatest, String szOrigin, bool bSessionData) => ArborInsightsClientBridge.StorageSetEntryFromString(szName ?? "", szStorageEntry ?? "", bKeepLatest, szOrigin ?? "", bSessionData);
		// ---
		public static int StorageRemoveDefaultEntry() => ArborInsightsClientBridge.StorageRemoveDefaultEntry();
		public static int StorageRemoveEntry(String szName) => ArborInsightsClientBridge.StorageRemoveEntry(szName ?? "");
		public static int StorageRemoveMultipleEntries(bool bSessionOnly) => ArborInsightsClientBridge.StorageRemoveMultipleEntries(bSessionOnly);
		// --- Configuration fields.
		public static String get_RestUrl() => ArborInsightsClientBridge.get_RestUrl();
		public static void set_RestUrl(String szValue) => ArborInsightsClientBridge.set_RestUrl(szValue ?? "");
		// ---
		/// <summary>Sets REST URL and all auth-related session fields on the service from the given auth payload. Call once per auth request before AuthRequest(). Keeps service-path auth setup in one place and limits divergence from the standalone auth path.</summary>
		public static void SetAuthPayloadForRequest(string restUrl, AuthPayload data)
		{
			set_RestUrl(restUrl ?? "https://lib-backend.xrdm.app/");
			// Send one mode only: app tokens OR legacy (app_id/org_id/auth_secret), never both.
			bool useAppTokens = !string.IsNullOrEmpty(data.appToken);
			if (useAppTokens)
			{
				if (!string.IsNullOrEmpty(data.appToken)) set_AppToken(data.appToken);
				if (!string.IsNullOrEmpty(data.orgToken)) set_OrgToken(data.orgToken);
			}
			else
			{
				if (!string.IsNullOrEmpty(data.appId)) set_AppID(data.appId);
				if (!string.IsNullOrEmpty(data.orgId)) set_OrgID(data.orgId);
				if (!string.IsNullOrEmpty(data.authSecret)) set_AuthSecret(data.authSecret);
			}
			if (!string.IsNullOrEmpty(data.buildType)) set_BuildType(data.buildType);
			if (!string.IsNullOrEmpty(data.SSOAccessToken)) set_SSOAccessToken(data.SSOAccessToken);
			if (!string.IsNullOrEmpty(data.deviceId)) set_DeviceID(data.deviceId);
			if (!string.IsNullOrEmpty(data.userId)) set_UserID(data.userId);
			int partner = string.Equals(data.partner, "arborxr", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
			set_Partner(partner);
			if (!string.IsNullOrEmpty(data.ipAddress)) set_IpAddress(data.ipAddress);
			if (!string.IsNullOrEmpty(data.deviceModel)) set_DeviceModel(data.deviceModel);
			set_GeoLocation(data.geolocation ?? new Dictionary<string, string>());
			if (!string.IsNullOrEmpty(data.osVersion)) set_OsVersion(data.osVersion);
			if (!string.IsNullOrEmpty(data.xrdmVersion)) set_XrdmVersion(data.xrdmVersion);
			if (!string.IsNullOrEmpty(data.appVersion)) set_AppVersion(data.appVersion);
			if (!string.IsNullOrEmpty(data.unityVersion)) set_UnityVersion(data.unityVersion);
			if (!string.IsNullOrEmpty(data.abxrLibType)) set_AbxrLibType(data.abxrLibType);
			if (!string.IsNullOrEmpty(data.abxrLibVersion)) set_AbxrLibVersion(data.abxrLibVersion);
			if (!string.IsNullOrEmpty(data.buildFingerprint)) set_BuildFingerprint(data.buildFingerprint);
			if (data.tags != null) set_Tags(new List<string>(data.tags));
		}
		// ---
		public static int get_SendRetriesOnFailure() => ArborInsightsClientBridge.get_SendRetriesOnFailure();
		public static void set_SendRetriesOnFailure(int nValue) => ArborInsightsClientBridge.set_SendRetriesOnFailure(nValue);
		// ---
		public static double get_SendRetryInterval() => ArborInsightsClientBridge.get_SendRetryInterval();
		public static void set_SendRetryInterval(double tsValue) => ArborInsightsClientBridge.set_SendRetryInterval(tsValue);
		// ---
		public static double get_SendNextBatchWait() => ArborInsightsClientBridge.get_SendNextBatchWait();
		public static void set_SendNextBatchWait(double tsValue) => ArborInsightsClientBridge.set_SendNextBatchWait(tsValue);
		// ---
		public static double get_StragglerTimeout() => ArborInsightsClientBridge.get_StragglerTimeout();
		public static void set_StragglerTimeout(double tsValue) => ArborInsightsClientBridge.set_StragglerTimeout(tsValue);
		// ---
		public static double get_PositionCapturePeriodicity() => ArborInsightsClientBridge.get_PositionCapturePeriodicity();
		public static void set_PositionCapturePeriodicity(double dValue) => ArborInsightsClientBridge.set_PositionCapturePeriodicity(dValue);
		// ---
		public static double get_FrameRateCapturePeriodicity() => ArborInsightsClientBridge.get_FrameRateCapturePeriodicity();
		public static void set_FrameRateCapturePeriodicity(double dValue) => ArborInsightsClientBridge.set_FrameRateCapturePeriodicity(dValue);
		// ---
		public static double get_TelemetryCapturePeriodicity() => ArborInsightsClientBridge.get_TelemetryCapturePeriodicity();
		public static void set_TelemetryCapturePeriodicity(double dValue) => ArborInsightsClientBridge.set_TelemetryCapturePeriodicity(dValue);
		// ---
		public static int get_DataItemsPerSendAttempt() => ArborInsightsClientBridge.get_DataItemsPerSendAttempt();
		public static void set_DataItemsPerSendAttempt(int nValue) => ArborInsightsClientBridge.set_DataItemsPerSendAttempt(nValue);
		// ---
		public static int get_StorageEntriesPerSendAttempt() => ArborInsightsClientBridge.get_StorageEntriesPerSendAttempt();
		public static void set_StorageEntriesPerSendAttempt(int nValue) => ArborInsightsClientBridge.set_StorageEntriesPerSendAttempt(nValue);
		// ---
		public static double get_PruneSentItemsOlderThan() => ArborInsightsClientBridge.get_PruneSentItemsOlderThan();
		public static void set_PruneSentItemsOlderThan(double tsValue) => ArborInsightsClientBridge.set_PruneSentItemsOlderThan(tsValue);
		// ---
		public static int get_MaximumCachedItems() => ArborInsightsClientBridge.get_MaximumCachedItems();
		public static void set_MaximumCachedItems(int nValue) => ArborInsightsClientBridge.set_MaximumCachedItems(nValue);
		// ---
		public static bool get_RetainLocalAfterSent() => ArborInsightsClientBridge.get_RetainLocalAfterSent();
		public static void set_RetainLocalAfterSent(bool bValue) => ArborInsightsClientBridge.set_RetainLocalAfterSent(bValue);
		// ---
		public static bool get_ReAuthenticateBeforeTokenExpires() => ArborInsightsClientBridge.get_ReAuthenticateBeforeTokenExpires();
		public static void set_ReAuthenticateBeforeTokenExpires(bool bValue) => ArborInsightsClientBridge.set_ReAuthenticateBeforeTokenExpires(bValue);
		// ---
		public static bool get_UseDatabase() => ArborInsightsClientBridge.get_UseDatabase();
		public static void set_UseDatabase(bool bValue) => ArborInsightsClientBridge.set_UseDatabase(bValue);
		// ---
		public static Dictionary<String, String> get_AppConfigAuthMechanism() => ArborInsightsClientBridge.get_AppConfigAuthMechanism();
		public static void set_AppConfigAuthMechanism(Dictionary<String, String> dictValue) => ArborInsightsClientBridge.set_AppConfigAuthMechanism(dictValue);
		/// <summary>Full app config as JSON (same shape as GET /v1/storage/config).</summary>
		public static string GetAppConfig() => ArborInsightsClientBridge.GetAppConfig();
		// ---
		public static bool ReadConfig() => ArborInsightsClientBridge.ReadConfig();
	}
}

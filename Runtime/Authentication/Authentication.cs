using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

[DefaultExecutionOrder(1)]
public class Authentication : MonoBehaviour
{
    private static string _orgId;
    private static string _deviceId;
    private static string _authSecret;
    private static string _userId;
    private static string _appId;
    private static Partner _partner = Partner.None;
    private static string _deviceModel;
    private static string _osVersion;
    private static string _xrdmVersion;
    private static string _appVersion;
    private static string _unityVersion;
    private static string _ipAddress;
    private static string _sessionId;
    private static int _failedAuthAttempts;

    private static string _authToken;
    private static string _apiSecret;
    private static AuthMechanism _authMechanism;
    private static DateTime _tokenExpiry = DateTime.MinValue;
    
    private const string DeviceIdKey = "abxrlib_device_id";

    private static bool _keyboardAuthSuccess;

    public static bool Authenticated() => DateTime.UtcNow <= _tokenExpiry;
    
    private void Start()
    {
        GetConfigData();
        _deviceId = SystemInfo.deviceUniqueIdentifier;
#if UNITY_ANDROID && !UNITY_EDITOR
        GetArborData();
#elif UNITY_WEBGL && !UNITY_EDITOR
        GetQueryData();
        _deviceId = GetOrCreateDeviceId();
#endif
        if (!ValidateConfigValues()) return;

        SetSessionData();
        StartCoroutine(Authenticate());
        StartCoroutine(CheckForReAuth());
    }

    public static void SetSessionId(string sessionId) => _sessionId = sessionId;

    public static IEnumerator Authenticate()
    {
        yield return AuthRequest();
        if (!string.IsNullOrEmpty(_authToken))
        {
            yield return GetConfiguration();
            if (!string.IsNullOrEmpty(_authMechanism?.prompt))
            {
                yield return KeyboardAuthenticate();
            }
        }
    }

    private static IEnumerator CheckForReAuth()
    {
        while (true)
        {
            yield return new WaitForSeconds(60);
            if (_tokenExpiry - DateTime.UtcNow <= TimeSpan.FromMinutes(2))
            {
                yield return AuthRequest();
            }
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            EventBatcher.SendNow();
            TelemetryBatcher.SendNow();
            LogBatcher.SendNow();
            StorageBatcher.SendNow();
        }
    }

    private static void GetConfigData()
    {
        _appId = Configuration.Instance.appID;
        _orgId = Configuration.Instance.orgID;
        _authSecret = Configuration.Instance.authSecret;
    }
    
    private static void GetArborData()
    {
        if (!ArborServiceClient.IsConnected()) return;
        
        _partner = Partner.ArborXR;
        _orgId = Abxr.GetOrgId();
        _deviceId = Abxr.GetDeviceId();
        _authSecret = Abxr.GetFingerprint();
        _userId = Abxr.GetAccessToken();
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    private static void GetQueryData()
    {
        string orgIdQuery = Utils.GetQueryParam("abxr_orgid", Application.absoluteURL);
        if (!string.IsNullOrEmpty(orgIdQuery))
        {
            _orgId = orgIdQuery;
        }
        
        string authSecretQuery = Utils.GetQueryParam("abxr_auth_secret", Application.absoluteURL);
        if (!string.IsNullOrEmpty(authSecretQuery))
        {
            _authSecret = authSecretQuery;
        }
    }
    
    private static string GetOrCreateDeviceId()
    {
        if (PlayerPrefs.HasKey(DeviceIdKey))
        {
            return PlayerPrefs.GetString(DeviceIdKey);
        }

        string newGuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(DeviceIdKey, newGuid);
        PlayerPrefs.Save();
        return newGuid;
    }
#endif
    private static bool ValidateConfigValues()
    {
        const string appIdPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
        if (string.IsNullOrEmpty(_appId) || !Regex.IsMatch(_appId, appIdPattern))
        {
            Debug.LogError("AbxrLib - Invalid Application ID. Cannot authenticate.");
            return false;
        }
        
        if (string.IsNullOrEmpty(_orgId))
        {
            Debug.LogError("AbxrLib - Missing Organization ID. Cannot authenticate.");
            return false;
        }
        
        const string orgIdPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
        if (!Regex.IsMatch(_orgId, orgIdPattern))
        {
            Debug.LogError("AbxrLib - Invalid Organization ID. Cannot authenticate.");
            return false;
        }
        
        if (string.IsNullOrEmpty(_authSecret))
        {
            Debug.LogError("AbxrLib - Missing Auth Secret. Cannot authenticate.");
            return false;
        }

        return true;
    }

    public static IEnumerator KeyboardAuthenticate(string keyboardInput = null)
    {
        if (keyboardInput != null)
        {
			string originalPrompt = _authMechanism.prompt;
            _authMechanism.prompt = keyboardInput;
            yield return AuthRequest();
			if (_keyboardAuthSuccess)
            {
                KeyboardHandler.Destroy();
                _failedAuthAttempts = 0;
                yield break;
            }

            _authMechanism.prompt = originalPrompt;
        }
        
        string prompt = _failedAuthAttempts > 0 ? $"Authentication Failed ({_failedAuthAttempts})\n" : "";
        prompt += _authMechanism.prompt;
        Abxr.PresentKeyboard(prompt, _authMechanism.type, _authMechanism.domain);
        _failedAuthAttempts++;
    }

    private static void SetSessionData()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _ipAddress = Utils.GetIPAddress();
        if (!string.IsNullOrEmpty(DeviceModel.deviceModel)) _deviceModel = DeviceModel.deviceModel;
        
        var currentAssembly = Assembly.GetExecutingAssembly();
        AssemblyName[] referencedAssemblies = currentAssembly.GetReferencedAssemblies();
        foreach (AssemblyName assemblyName in referencedAssemblies)
        {
            if (assemblyName.Name == "XRDM.SDK.External.Unity")
            {
                _xrdmVersion = assemblyName.Version.ToString();
                break;
            }
        }
#endif
        _osVersion = SystemInfo.operatingSystem;
        _appVersion = Application.version;
        _unityVersion = Application.unityVersion;
        //TODO Geolocation
    }

    private static IEnumerator AuthRequest()
    {
        _keyboardAuthSuccess = false;
        if (string.IsNullOrEmpty(_sessionId)) _sessionId = Guid.NewGuid().ToString();
        
        var data = new AuthPayload
        {
            appId = _appId,
            orgId = _orgId,
            authSecret = _authSecret,
            deviceId = _deviceId,
            userId = _userId,
            tags = new string[] { },
            sessionId = _sessionId,
            partner = _partner.ToString().ToLower(),
            ipAddress = _ipAddress,
            deviceModel = _deviceModel,
            geolocation = new Dictionary<string, string>(),
            osVersion = _osVersion,
            xrdmVersion = _xrdmVersion,
            appVersion = _appVersion,
            unityVersion = _unityVersion,
            authMechanism = CreateAuthMechanismDict()
        };
        
        string json = JsonConvert.SerializeObject(data);

        var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/auth/token");
        using var request = new UnityWebRequest(fullUri.ToString(), "POST");
        Utils.BuildRequest(request, json);
        
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("AbxrLib - Authenticated successfully");
            AuthResponse postResponse = JsonConvert.DeserializeObject<AuthResponse>(request.downloadHandler.text);
            _authToken = postResponse.Token;
            _apiSecret = postResponse.Secret;
            Dictionary<string, object> decodedJwt = Utils.DecodeJwt(_authToken);
            _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds((long)decodedJwt["exp"]).UtcDateTime;
            _keyboardAuthSuccess = true;
        }
        else
        {
            Debug.LogError($"AbxrLib - Authentication failed : {request.error} - {request.downloadHandler.text}");
            _sessionId = null;
        }
    }

    private static IEnumerator GetConfiguration()
    {
        var fullUri = new Uri(new Uri(Configuration.Instance.restUrl), "/v1/storage/config");
        using UnityWebRequest request = UnityWebRequest.Get(fullUri.ToString());
        request.SetRequestHeader("Accept", "application/json");
        SetAuthHeaders(request);

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            string response = request.downloadHandler.text;
            var config = JsonConvert.DeserializeObject<ConfigPayload>(response);
            SetConfigFromPayload(config);
            _authMechanism = config.authMechanism;
        }
        else
        {
            Debug.LogWarning($"AbxrLib - GetConfiguration failed: {request.error} - {request.downloadHandler.text}");
        }
    }
    
    public static void SetAuthHeaders(UnityWebRequest request, string json = "")
    {
        request.SetRequestHeader("Authorization", "Bearer " + _authToken);
        
        string unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        request.SetRequestHeader("x-abxrlib-timestamp", unixTimeSeconds);
        
        string hashString = _authToken + _apiSecret + unixTimeSeconds;
        if (!string.IsNullOrEmpty(json))
        {
            uint crc = Utils.ComputeCRC(json);
            hashString += crc;
        }
        
        request.SetRequestHeader("x-abxrlib-hash", Utils.ComputeSha256Hash(hashString));
    }

    private static Dictionary<string, string> CreateAuthMechanismDict()
    {
        var dict = new Dictionary<string, string>();
        if (_authMechanism == null) return dict;
        
        if (!string.IsNullOrEmpty(_authMechanism.type)) dict["type"] = _authMechanism.type;
        if (!string.IsNullOrEmpty(_authMechanism.prompt)) dict["prompt"] = _authMechanism.prompt;
        if (!string.IsNullOrEmpty(_authMechanism.domain)) dict["domain"] = _authMechanism.domain;
        return dict;
    }
    
    private static void SetConfigFromPayload(ConfigPayload payload)
    {
        if (!string.IsNullOrEmpty(payload.restUrl)) Configuration.Instance.restUrl = payload.restUrl;
        if (!string.IsNullOrEmpty(payload.sendRetriesOnFailure)) Configuration.Instance.sendRetriesOnFailure = Convert.ToInt32(payload.sendRetriesOnFailure);
        if (!string.IsNullOrEmpty(payload.sendRetryInterval)) Configuration.Instance.sendRetryIntervalSeconds = Convert.ToInt32(payload.sendRetryInterval);
        if (!string.IsNullOrEmpty(payload.sendNextBatchWait)) Configuration.Instance.sendNextBatchWaitSeconds = Convert.ToInt32(payload.sendNextBatchWait);
        if (!string.IsNullOrEmpty(payload.stragglerTimeout)) Configuration.Instance.stragglerTimeoutSeconds = Convert.ToInt32(payload.stragglerTimeout);
        if (!string.IsNullOrEmpty(payload.eventsPerSendAttempt)) Configuration.Instance.eventsPerSendAttempt = Convert.ToInt32(payload.eventsPerSendAttempt);
        if (!string.IsNullOrEmpty(payload.logsPerSendAttempt)) Configuration.Instance.logsPerSendAttempt = Convert.ToInt32(payload.logsPerSendAttempt);
        if (!string.IsNullOrEmpty(payload.telemetryEntriesPerSendAttempt)) Configuration.Instance.telemetryEntriesPerSendAttempt = Convert.ToInt32(payload.telemetryEntriesPerSendAttempt);
        if (!string.IsNullOrEmpty(payload.storageEntriesPerSendAttempt)) Configuration.Instance.storageEntriesPerSendAttempt = Convert.ToInt32(payload.storageEntriesPerSendAttempt);
        if (!string.IsNullOrEmpty(payload.pruneSentItemsOlderThan)) Configuration.Instance.pruneSentItemsOlderThanHours = Convert.ToInt32(payload.pruneSentItemsOlderThan);
        if (!string.IsNullOrEmpty(payload.maximumCachedItems)) Configuration.Instance.maximumCachedItems = Convert.ToInt32(payload.maximumCachedItems);
        if (!string.IsNullOrEmpty(payload.retainLocalAfterSent)) Configuration.Instance.retainLocalAfterSent = Convert.ToBoolean(payload.retainLocalAfterSent);
    }
    
    [Preserve]
    private class AuthMechanism
    {
        public string type;
        public string prompt;
        public string domain;
        
        [Preserve]
        public AuthMechanism() {}
    }

    [Preserve]
    private class ConfigPayload
    {
        public AuthMechanism authMechanism;
        public string frameRateCapturePeriod;
        public string telemetryCapturePeriod;
        public string restUrl;
        public string sendRetriesOnFailure;
        public string sendRetryInterval;
        public string sendNextBatchWait;
        public string stragglerTimeout;
        public string eventsPerSendAttempt;
        public string logsPerSendAttempt;
        public string telemetryEntriesPerSendAttempt;
        public string storageEntriesPerSendAttempt;
        public string pruneSentItemsOlderThan;
        public string maximumCachedItems;
        public string retainLocalAfterSent;
        public string positionCapturePeriod;
        
        [Preserve]
        public ConfigPayload() {}
    }

    private class AuthPayload
    {
        public string appId;
        public string orgId;
        public string authSecret;
        public string deviceId;
        public string userId;
        public string[] tags;
        public string sessionId;
        public string partner;
        public string ipAddress;
        public string deviceModel;
        public Dictionary<string, string> geolocation;
        public string osVersion;
        public string xrdmVersion;
        public string appVersion;
        public string unityVersion;
        public Dictionary<string, string> authMechanism;
    }

    [Preserve]
    private class AuthResponse
    {
        public string Token;
        public string Secret;

        [Preserve]
        public AuthResponse() {}
    }

    private enum Partner
    {
        None,
        ArborXR
    }
}
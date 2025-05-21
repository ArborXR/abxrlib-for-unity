using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class StorageBatcher : MonoBehaviour
{
	private static float _sendIntervalSeconds;
	private const string UrlPath = "/v1/storage";
	private static Uri _uri;
	private static readonly List<Payload> Payloads = new();
	private static readonly object Lock = new();
	
	private void Start()
	{
		_uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
		_sendIntervalSeconds = Configuration.Instance.sendNextBatchWaitSeconds;
		StartCoroutine(SendLoop());
	}

	public static void SendNow()
	{
		CoroutineRunner.Instance.StartCoroutine(Send());
	}

	private static IEnumerator SendLoop()
	{
		while (true)
		{
			yield return new WaitForSeconds(_sendIntervalSeconds);
			yield return Send();
		}
	}

	private static IEnumerator Send()
	{
		if (!Authentication.Authenticated()) yield break;
		_sendIntervalSeconds = Configuration.Instance.sendNextBatchWaitSeconds;
		lock (Lock)
		{
			if (Payloads.Count > 0) yield return SendStorages();
		}
	}
	
	public static void Add(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
	{
		long storageTime = Utils.GetUnityTime();
		string isoTime = DateTimeOffset.FromUnixTimeMilliseconds(storageTime).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
		var payload = new Payload
		{
			timestamp = isoTime,
			keepPolicy = policy.ToString(),
			name = name,
			data = new List<Dictionary<string, string>>
			{
				entry
			},
			scope = scope.ToString()
		};
		
		lock (Lock)
		{
			Payloads.Add(payload);
			if (Payloads.Count >= Configuration.Instance.storageEntriesPerSendAttempt)
			{
				_sendIntervalSeconds = 1; // Send what we have soon
			}
		}
	}

	private static IEnumerator SendStorages()
	{
		List<Payload> storagesToSend;
		lock (Lock)
		{
			// Copy current list and leave original untouched
			storagesToSend = new List<Payload>(Payloads);
			foreach (var storage in storagesToSend) Payloads.Remove(storage);
		}
		
		var wrapper = new PayloadWrapper { data = storagesToSend };
		string json = JsonConvert.SerializeObject(wrapper);
		
		using var request = new UnityWebRequest(_uri, "POST");
		Utils.BuildRequest(request, json);
		Authentication.SetAuthHeaders(request, json);
		
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			Debug.Log("AbxrLib - Storage POST Request successful");
		}
		else
		{
			Debug.LogError($"AbxrLib - Storage POST Request failed : {request.error} - {request.downloadHandler.text}");
			_sendIntervalSeconds = Configuration.Instance.sendRetryIntervalSeconds;
			lock (Lock)
			{
				Payloads.InsertRange(0, storagesToSend);
			}
		}
	}

	public static IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
	{
		if (!Authentication.Authenticated()) yield break;
		
		var queryParams = new Dictionary<string, string>
		{
			{ "name", name },
			{ "scope", scope.ToString() }
		};
		
		string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
		using UnityWebRequest request = UnityWebRequest.Get(urlWithParams);
		request.SetRequestHeader("Accept", "application/json");
		Authentication.SetAuthHeaders(request);

		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			Debug.LogWarning("AbxrLib - Storage GET succeeded");
			PayloadWrapper payload = JsonConvert.DeserializeObject<PayloadWrapper>(request.downloadHandler.text);
			callback?.Invoke(payload.data.Count > 0 ? payload.data[0].data : null);
		}
		else
		{
			Debug.LogWarning($"AbxrLib - Storage GET failed: {request.error} - {request.downloadHandler.text}");
			callback?.Invoke(null);
		}
	}

	public static IEnumerator Delete(Abxr.StorageScope scope, string name = "")
	{
		if (!Authentication.Authenticated()) yield break;
		
		var queryParams = new Dictionary<string, string>
		{
			{ "scope", scope.ToString() }
		};
		if (string.IsNullOrEmpty(name)) queryParams.Add("name", name);
		
		string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
		using UnityWebRequest request = UnityWebRequest.Delete(urlWithParams);
		request.SetRequestHeader("Accept", "application/json");
		Authentication.SetAuthHeaders(request);

		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			Debug.Log("AbxrLib - Storage DELETE succeeded");
		}
		else
		{
			Debug.LogWarning($"AbxrLib - Storage DELETE failed: {request.error} - {request.downloadHandler.text}");
		}
	}
	
	private class Payload
	{
		public string timestamp;  // 'yyyy-MM-ddTHH:mm:ss.fffZ'
		public string keepPolicy; // 'keepLatest' or 'appendHistory'
		public string name;       // defaults to 'state'
		public List<Dictionary<string, string>> data;
		public string scope;      // 'device' or 'user'
	}
	private class PayloadWrapper
	{
		public List<Payload> data;
	}
}
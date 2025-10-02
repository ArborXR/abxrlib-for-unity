using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Storage
{
	public class StorageBatcher : MonoBehaviour
	{
		private const string UrlPath = "/v1/storage";
		private static Uri _uri;
		private static readonly List<Payload> _payloads = new();
		private static readonly object _lock = new();
		private static float _timer;
		private static float _lastCallTime;
		private const float MaxCallFrequencySeconds = 1f;
	
		private void Start()
		{
			_uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
			_timer = Configuration.Instance.sendNextBatchWaitSeconds;
		}
	
		private void Update()
		{
			_timer -= Time.deltaTime;
			if (_timer <= 0) CoroutineRunner.Instance.StartCoroutine(Send());
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
		
			lock (_lock)
			{
				if (IsQueueAtLimit(_payloads, "Storage"))
				{
					return; // Reject new storage if queue is at limit
				}
				_payloads.Add(payload);
				if (_payloads.Count >= Configuration.Instance.storageEntriesPerSendAttempt)
				{
					_timer = 0; // Send on the next update
				}
			}
		}

		public static IEnumerator Send()
		{
			if (Time.time - _lastCallTime < MaxCallFrequencySeconds) yield break;
		
			_lastCallTime = Time.time;
			_timer = Configuration.Instance.sendNextBatchWaitSeconds; // reset timer
			if (!Authentication.Authentication.Authenticated()) yield break;
			lock (_lock)
			{
				if (_payloads.Count == 0) yield break;
			}
		
			List<Payload> storagesToSend;
			lock (_lock)
			{
				// Copy current list and leave original untouched
				storagesToSend = new List<Payload>(_payloads);
				foreach (var storage in storagesToSend) _payloads.Remove(storage);
			}
		
			var wrapper = new PayloadWrapper { data = storagesToSend };
			string json = JsonConvert.SerializeObject(wrapper);
		
			using var request = new UnityWebRequest(_uri, "POST");
			Utils.BuildRequest(request, json);
			Authentication.Authentication.SetAuthHeaders(request, json);
		
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Debug.Log("AbxrLib: Storage POST Request successful");
			}
			else
			{
				Debug.LogError($"AbxrLib: Storage POST Request failed : {request.error} - {request.downloadHandler.text}");
				_timer = Configuration.Instance.sendRetryIntervalSeconds;
				lock (_lock)
				{
					// Re-insert storage entries with queue limit enforcement
					foreach (var storagePayload in storagesToSend)
					{
						if (IsQueueAtLimit(_payloads, "Storage"))
						{
							Debug.LogWarning("AbxrLib: Cannot re-insert failed storage - queue at limit, dropping storage");
							break; // Stop re-inserting if queue is at limit
						}
						_payloads.Insert(0, storagePayload);
					}
				}
			}
		}

		public static IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			if (!Authentication.Authentication.Authenticated()) yield break;
		
			var queryParams = new Dictionary<string, string>
			{
				{ "name", name },
				{ "scope", scope.ToString() }
			};
		
			string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
			using UnityWebRequest request = UnityWebRequest.Get(urlWithParams);
			request.SetRequestHeader("Accept", "application/json");
			Authentication.Authentication.SetAuthHeaders(request);

			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Debug.Log("AbxrLib: Storage GET succeeded");
				PayloadWrapper payload = JsonConvert.DeserializeObject<PayloadWrapper>(request.downloadHandler.text);
				callback?.Invoke(payload.data.Count > 0 ? payload.data[0].data : null);
			}
			else
			{
				Debug.LogWarning($"AbxrLib: Storage GET failed: {request.error} - {request.downloadHandler.text}");
				callback?.Invoke(null);
			}
		}

		public static IEnumerator Delete(Abxr.StorageScope scope, string name = "")
		{
			if (!Authentication.Authentication.Authenticated()) yield break;
		
			var queryParams = new Dictionary<string, string>
			{
				{ "scope", scope.ToString() }
			};
			if (!string.IsNullOrEmpty(name)) queryParams.Add("name", name);
		
			string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
			using UnityWebRequest request = UnityWebRequest.Delete(urlWithParams);
			request.SetRequestHeader("Accept", "application/json");
			Authentication.Authentication.SetAuthHeaders(request);

			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Debug.Log("AbxrLib: Storage DELETE succeeded");
			}
			else
			{
				Debug.LogWarning($"AbxrLib: Storage DELETE failed: {request.error} - {request.downloadHandler.text}");
			}
		}

		/// <summary>
		/// Checks if the queue has reached its maximum size limit
		/// </summary>
		private static bool IsQueueAtLimit<T>(List<T> queue, string queueType)
		{
			int maxSize = Configuration.Instance.maximumCachedItems;
			if (maxSize > 0 && queue.Count >= maxSize)
			{
				Debug.LogWarning($"AbxrLib: {queueType} queue limit reached ({maxSize}), rejecting new items");
				return true;
			}
			return false;
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
}
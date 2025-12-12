using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.ServiceClient.AbxrInsightService;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

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
			if (Abxr.IsServiceAvailable())
			{
				String	szOrigin = "";
				bool	bSessionData = false;	// TODO:  code actual values when known... this code is different than the API.

				AbxrInsightServiceClient.StorageSetEntryFromString(name, Utils.DictToString(entry), policy == Abxr.StoragePolicy.keepLatest, szOrigin, bSessionData);
			}
			else
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
		}

	public static IEnumerator Send()
	{
		if (Time.time - _lastCallTime < Configuration.Instance.maxCallFrequencySeconds) yield break;

		_lastCallTime = Time.time;
		_timer = Configuration.Instance.sendNextBatchWaitSeconds; // reset timer
		if (!Authentication.Authentication.Authenticated()) yield break;
		
		lock (_lock)
		{
			if (_payloads.Count == 0) yield break;
		}

		// Copy current list and clear originals
		List<Payload> storagesToSend;
		lock (_lock)
		{
			storagesToSend = new List<Payload>(_payloads);
			_payloads.Clear();
		}

		// Retry logic for storage sending - using separate coroutine to avoid yield in try-catch
		yield return SendWithRetry(storagesToSend);
	}

	/// <summary>
	/// Sends storage data with retry logic, avoiding yield statements in try-catch blocks
	/// </summary>
	private static IEnumerator SendWithRetry(List<Payload> storagesToSend)
	{
		int retryCount = 0;
		int maxRetries = Configuration.Instance.sendRetriesOnFailure;
		bool success = false;
		string lastError = "";

		while (retryCount <= maxRetries && !success)
		{
			// Create request and handle creation errors
			UnityWebRequest request = null;
			bool requestCreated = false;
			bool shouldRetry = false;

			// Request creation with error handling (no yield statements)
			try
			{
				var wrapper = new PayloadWrapper { data = storagesToSend };
				string json = JsonConvert.SerializeObject(wrapper);

				request = new UnityWebRequest(_uri, "POST");
				Utils.BuildRequest(request, json);
				Authentication.Authentication.SetAuthHeaders(request, json);

				// Set timeout to prevent hanging requests
				request.timeout = Configuration.Instance.requestTimeoutSeconds;
				requestCreated = true;
			}
			catch (System.Exception ex)
			{
				lastError = $"Storage request creation failed: {ex.Message}";
				Debug.LogError($"AbxrLib: {lastError}");

				if (IsStorageRetryableException(ex) && retryCount < maxRetries)
				{
					shouldRetry = true;
				}
			}

			// Handle retry logic for request creation failure (yield outside try-catch)
			if (shouldRetry)
			{
				retryCount++;
				Debug.LogWarning($"AbxrLib: Storage request creation failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
				yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds);
				continue;
			}
			else if (!requestCreated)
			{
				break; // Non-retryable error or max retries reached
			}

			// Send request (yield outside try-catch)
			yield return request.SendWebRequest();

			// Handle response (no yield statements in try-catch)
			bool responseSuccess = false;
			bool responseShouldRetry = false;

			try
			{
				if (request.result == UnityWebRequest.Result.Success)
				{
					responseSuccess = true;
					success = true;
				}
				else
				{
					// Handle different types of network errors
					lastError = HandleStorageNetworkError(request, retryCount, maxRetries);

					if (IsStorageRetryableError(request))
					{
						responseShouldRetry = true;
					}
				}
			}
			catch (System.Exception ex)
			{
				lastError = $"Storage response handling failed: {ex.Message}";
				Debug.LogError($"AbxrLib: {lastError}");

				if (IsStorageRetryableException(ex) && retryCount < maxRetries)
				{
					responseShouldRetry = true;
				}
			}
			finally
			{
				// Always dispose of request
				request?.Dispose();
			}

			// Handle retry logic for response failure (yield outside try-catch)
			if (responseShouldRetry)
			{
				retryCount++;
				if (retryCount <= maxRetries)
				{
					Debug.LogWarning($"AbxrLib: Storage POST Request failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
					yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds);
				}
			}
			else if (!responseSuccess)
			{
				// Non-retryable error, break out of retry loop
				break;
			}
		}

		// If all retries failed, put data back in queue (with size limits enforced)
		if (!success)
		{
			Debug.LogError($"AbxrLib: Storage POST Request failed after {retryCount} attempts: {lastError}");
			_timer = Configuration.Instance.sendRetryIntervalSeconds;
			lock (_lock)
			{
				// Re-insert storage entries to the front of the queue with queue limit enforcement
				foreach (var storagePayload in storagesToSend)
				{
					if (IsQueueAtLimit(_payloads, "Storage"))
					{
						Debug.LogWarning("AbxrLib: Cannot re-insert failed storage - queue at limit, dropping storage");
						break; // Stop re-inserting if queue is at limit
					}
					_payloads.Insert(0, storagePayload); // Insert at front for priority
				}
			}
		}
	}

	/// <summary>
	/// Handles network errors for storage requests and determines appropriate error messages
	/// </summary>
	private static string HandleStorageNetworkError(UnityWebRequest request, int retryCount, int maxRetries)
	{
		string errorMessage = "";

		switch (request.result)
		{
			case UnityWebRequest.Result.ConnectionError:
				errorMessage = $"Connection error: {request.error}";
				break;
			case UnityWebRequest.Result.DataProcessingError:
				errorMessage = $"Data processing error: {request.error}";
				break;
			case UnityWebRequest.Result.ProtocolError:
				errorMessage = $"Protocol error ({request.responseCode}): {request.error}";
				break;
			default:
				errorMessage = $"Unknown error: {request.error}";
				break;
		}

		if (!string.IsNullOrEmpty(request.downloadHandler.text))
		{
			errorMessage += $" - Response: {request.downloadHandler.text}";
		}

		return errorMessage;
	}

	/// <summary>
	/// Determines if a storage network error is retryable
	/// </summary>
	private static bool IsStorageRetryableError(UnityWebRequest request)
	{
		// Retry on connection errors and 5xx server errors
		if (request.result == UnityWebRequest.Result.ConnectionError)
			return true;

		if (request.result == UnityWebRequest.Result.ProtocolError)
		{
			// Retry on 5xx server errors, but not on 4xx client errors
			return request.responseCode >= 500 && request.responseCode < 600;
		}

		return false;
	}

	/// <summary>
	/// Determines if a storage exception is retryable
	/// </summary>
	private static bool IsStorageRetryableException(System.Exception ex)
	{
		// Retry on network-related exceptions
		return ex is System.Net.WebException ||
			   ex is System.Net.Sockets.SocketException ||
			   ex.Message.Contains("timeout") ||
			   ex.Message.Contains("connection");
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
				// Storage DELETE succeeded - no logging needed for routine operations
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
using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using AbxrLib.Runtime.Services.Platform;

namespace AbxrLib.Runtime.Services.Data
{
	public class AbxrStorageService
	{
		private readonly AbxrAuthService _authService;
		private readonly MonoBehaviour _runner;
		
		private float _nextSendAt;
		private Coroutine _tickCoroutine;
		
		private const string UrlPath = "/v1/storage";
		private readonly Uri _uri;
		private readonly List<StoragePayload> _payloads = new();
		private readonly object _lock = new();
		private float _lastCallTime;

		public AbxrStorageService(AbxrAuthService authService, MonoBehaviour runner)
		{
			_authService = authService;
			_runner = runner;
			_uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
		}
		
		public void Start()
		{
			_nextSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
			_tickCoroutine = _runner.StartCoroutine(TickCoroutine());
		}

		public void Stop()
		{
			if (_tickCoroutine != null)
			{
				_runner.StopCoroutine(_tickCoroutine);
				_tickCoroutine = null;
			}
		}
		
		public void ForceSend() => _nextSendAt = 0; // Send on the next update
		
		private IEnumerator TickCoroutine()
		{
			while (true)
			{
				yield return new WaitForSeconds(0.25f);
				if (Time.time >= _nextSendAt)
				{
					yield return Send();
				}
			}
		}
	
		/// <summary>
		/// Add a storage entry. When this session uses ArborInsightService we send directly to the service; otherwise we queue for Send() (standalone).
		/// </summary>
		public void Add(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (_authService.UsingArborInsightServiceForData())
			{
				long t = Utils.GetUnityTime();
				string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
				var p = new StoragePayload
				{
					timestamp = iso,
					keepPolicy = policy.ToString(),
					name = name,
					data = new List<Dictionary<string, string>> { entry ?? new Dictionary<string, string>() },
					scope = scope.ToString()
				};
				string json = JsonConvert.SerializeObject(p);
				bool keepLatest = policy == Abxr.StoragePolicy.KeepLatest;
				bool sessionData = scope == Abxr.StorageScope.User;
				if (name == "state")
					ArborInsightServiceClient.StorageSetDefaultEntryFromString(json, keepLatest, "unity", sessionData);
				else
					ArborInsightServiceClient.StorageSetEntryFromString(name, json, keepLatest, "unity", sessionData);
				return;
			}
#endif
			long storageTime = Utils.GetUnityTime();
			string isoTime = DateTimeOffset.FromUnixTimeMilliseconds(storageTime).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
			var payload = new StoragePayload
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
					_nextSendAt = 0; // Send on the next update
				}
			}
		}

		private IEnumerator Send()
		{
			if (Time.time - _lastCallTime < Configuration.Instance.maxCallFrequencySeconds) yield break;

			_lastCallTime = Time.time;
			_nextSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
			if (!_authService.Authenticated) yield break;
			
			lock (_lock)
			{
				if (_payloads.Count == 0) yield break;
			}

			// Copy current list and clear originals
			List<StoragePayload> storagesToSend;
			lock (_lock)
			{
				storagesToSend = new List<StoragePayload>(_payloads);
				_payloads.Clear();
			}

			// Retry logic for storage sending - using separate coroutine to avoid yield in try-catch
			yield return SendWithRetry(storagesToSend);
		}

		/// <summary>
		/// Sends storage data with retry logic, avoiding yield statements in try-catch blocks
		/// </summary>
		private IEnumerator SendWithRetry(List<StoragePayload> storagesToSend)
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
					var wrapper = new StoragePayloadWrapper { data = storagesToSend };
					string json = JsonConvert.SerializeObject(wrapper);

					request = new UnityWebRequest(_uri, "POST");
					Utils.BuildRequest(request, json);
					_authService.SetAuthHeaders(request, json);

					// Set timeout to prevent hanging requests
					request.timeout = Configuration.Instance.requestTimeoutSeconds;
					requestCreated = true;
				}
				catch (Exception ex)
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
						lastError = HandleStorageNetworkError(request);

						if (IsStorageRetryableError(request))
						{
							responseShouldRetry = true;
						}
					}
				}
				catch (Exception ex)
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
				_nextSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
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
		private static string HandleStorageNetworkError(UnityWebRequest request)
		{
			string errorMessage;

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
		private static bool IsStorageRetryableException(Exception ex)
		{
			// Retry on network-related exceptions
			return ex is System.Net.WebException ||
				   ex is System.Net.Sockets.SocketException ||
				   ex.Message.Contains("timeout") ||
				   ex.Message.Contains("connection");
		}

		public IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
		{
			if (!_authService.Authenticated) yield break;

#if UNITY_ANDROID && !UNITY_EDITOR
			if (_authService.UsingArborInsightServiceForData())
			{
				string json = name == "state"
					? ArborInsightServiceClient.StorageGetDefaultEntryAsString()
					: ArborInsightServiceClient.StorageGetEntryAsString(name);
				List<Dictionary<string, string>> result = null;
				if (!string.IsNullOrEmpty(json))
				{
					try
					{
						var payload = JsonConvert.DeserializeObject<StoragePayload>(json);
						if (payload?.data != null && payload.data.Count > 0)
							result = payload.data;
						else
						{
							var list = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
							if (list != null) result = list;
						}
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"AbxrLib: Storage GET parse failed: {ex.Message}");
					}
				}
				callback?.Invoke(result);
				yield break;
			}
#endif
			var queryParams = new Dictionary<string, string>
			{
				{ "name", name },
				{ "scope", scope.ToString() }
			};
		
			string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
			using UnityWebRequest request = UnityWebRequest.Get(urlWithParams);
			request.SetRequestHeader("Accept", "application/json");
			_authService.SetAuthHeaders(request);

			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				StoragePayloadWrapper payload = JsonConvert.DeserializeObject<StoragePayloadWrapper>(request.downloadHandler.text);
				callback?.Invoke(payload.data.Count > 0 ? payload.data[0].data : null);
			}
			else
			{
				Debug.LogWarning($"AbxrLib: Storage GET failed: {request.error} - {request.downloadHandler.text}");
				callback?.Invoke(null);
			}
		}

		public IEnumerator Delete(Abxr.StorageScope scope, string name = "")
		{
			if (!_authService.Authenticated) yield break;

#if UNITY_ANDROID && !UNITY_EDITOR
			if (_authService.UsingArborInsightServiceForData())
			{
				if (string.IsNullOrEmpty(name))
				{
					ArborInsightServiceClient.StorageRemoveMultipleEntries(scope == Abxr.StorageScope.User);
				}
				else if (name == "state")
				{
					ArborInsightServiceClient.StorageRemoveDefaultEntry();
				}
				else
				{
					ArborInsightServiceClient.StorageRemoveEntry(name);
				}
				yield break;
			}
#endif
			var queryParams = new Dictionary<string, string>
			{
				{ "scope", scope.ToString() }
			};
			if (!string.IsNullOrEmpty(name)) queryParams.Add("name", name);
		
			string urlWithParams = Utils.BuildUrlWithParams(_uri.ToString(), queryParams);
			using UnityWebRequest request = UnityWebRequest.Delete(urlWithParams);
			request.SetRequestHeader("Accept", "application/json");
			_authService.SetAuthHeaders(request);

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
	}
}
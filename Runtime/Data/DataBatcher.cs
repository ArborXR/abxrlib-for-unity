using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID && !UNITY_EDITOR
using AbxrLib.Runtime.ServiceClient.ArborInsightService;
#endif

namespace AbxrLib.Runtime.Data
{
	public class DataBatcher : MonoBehaviour
	{
		private const string UrlPath = "/v1/collect/data";
		private static Uri _uri;
		private static readonly List<EventPayload> _eventPayloads = new();
		private static readonly List<TelemetryPayload> _telemetryPayloads = new();
		private static readonly List<LogPayload> _logPayloads = new();
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

		/// <summary>
		/// Clears all queued events, telemetry, and logs. Used by Abxr.StartNewSession() so previous-session data is not sent with the new session.
		/// </summary>
		internal static void ClearQueues()
		{
			lock (_lock)
			{
				_eventPayloads.Clear();
				_telemetryPayloads.Clear();
				_logPayloads.Clear();
			}
		}

		/// <summary>
		/// Add an event to the batch.
		/// When this session authenticated via ArborInsightService we send directly to the service (no queue). Otherwise we queue and Send() uses HTTP; we never switch to the service later.
		/// </summary>
		public static void AddEvent(string name, Dictionary<string, string> meta)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (Authentication.Authentication.UsingArborInsightServiceForData())
			{
				ArborInsightServiceClient.EventDeferred(name, meta ?? new Dictionary<string, string>());
				return;
			}
#endif
			long eventTime = Utils.GetUnityTime();
			string isoTime = DateTimeOffset.FromUnixTimeMilliseconds(eventTime).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
			var payload = new EventPayload
			{
				timestamp = isoTime,
				preciseTimestamp = eventTime,
				name = name,
				meta = meta ?? new Dictionary<string, string>()
			};

			lock (_lock)
			{
				if (IsQueueAtLimit(_eventPayloads, "Event"))
				{
					return; // Reject new event if queue is at limit
				}
				_eventPayloads.Add(payload);
				if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt)
				{
					_timer = 0; // Send on the next update
				}
			}
		}

		/// <summary>
		/// Add telemetry data to the batch.
		/// When this session uses ArborInsightService we send directly to the service; otherwise we queue for Send() (standalone).
		/// </summary>
		public static void AddTelemetry(string name, Dictionary<string, string> meta)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (Authentication.Authentication.UsingArborInsightServiceForData())
			{
				ArborInsightServiceClient.AddTelemetryEntryDeferred(name, meta ?? new Dictionary<string, string>());
				return;
			}
#endif
			long telemetryTime = Utils.GetUnityTime();
			string isoTime = DateTimeOffset.FromUnixTimeMilliseconds(telemetryTime).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
			var payload = new TelemetryPayload
			{
				timestamp = isoTime,
				preciseTimestamp = telemetryTime,
				name = name,
				meta = meta ?? new Dictionary<string, string>()
			};

			lock (_lock)
			{
				if (IsQueueAtLimit(_telemetryPayloads, "Telemetry"))
				{
					return; // Reject new telemetry if queue is at limit
				}
				_telemetryPayloads.Add(payload);
				if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt)
				{
					_timer = 0; // Send on the next update
				}
			}
		}

		/// <summary>
		/// Add a log entry to the batch.
		/// When this session uses ArborInsightService we send directly to the service; otherwise we queue for Send() (standalone).
		/// </summary>
		public static void AddLog(string logLevel, string text, Dictionary<string, string> meta)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (Authentication.Authentication.UsingArborInsightServiceForData())
			{
				var dict = meta ?? new Dictionary<string, string>();
				string level = logLevel?.ToUpperInvariant() ?? "";
				if (level == "DEBUG")
					ArborInsightServiceClient.LogDebugDeferred(text ?? "", dict);
				else if (level == "INFO")
					ArborInsightServiceClient.LogInfoDeferred(text ?? "", dict);
				else if (level == "WARN")
					ArborInsightServiceClient.LogWarnDeferred(text ?? "", dict);
				else if (level == "ERROR")
					ArborInsightServiceClient.LogErrorDeferred(text ?? "", dict);
				else if (level == "CRITICAL")
					ArborInsightServiceClient.LogCriticalDeferred(text ?? "", dict);
				else
					ArborInsightServiceClient.LogInfoDeferred(text ?? "", dict);
				return;
			}
#endif
			long logTime = Utils.GetUnityTime();
			string isoTime = DateTimeOffset.FromUnixTimeMilliseconds(logTime).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
			var payload = new LogPayload
			{
				timestamp = isoTime,
				preciseTimestamp = logTime,
				logLevel = logLevel,
				text = text,
				meta = meta ?? new Dictionary<string, string>()
			};

			lock (_lock)
			{
				if (IsQueueAtLimit(_logPayloads, "Log"))
				{
					return; // Reject new log if queue is at limit
				}
				_logPayloads.Add(payload);
				if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt)
				{
					_timer = 0; // Send on the next update
				}
			}
		}

		/// <summary>
		/// Send all pending data
		/// </summary>
		public static IEnumerator Send()
		{
			if (Time.time - _lastCallTime < Configuration.Instance.maxCallFrequencySeconds) yield break;

			_lastCallTime = Time.time;
			_timer = Configuration.Instance.sendNextBatchWaitSeconds; // reset timer
			if (!Authentication.Authentication.Authenticated()) yield break;
			
			lock (_lock)
			{
				if (_eventPayloads.Count == 0 && _telemetryPayloads.Count == 0 && _logPayloads.Count == 0) yield break;
			}

			// Copy current lists and clear originals
			List<EventPayload> eventsToSend;
			List<TelemetryPayload> telemetriesToSend;
			List<LogPayload> logsToSend;

			lock (_lock)
			{
				eventsToSend = new List<EventPayload>(_eventPayloads);
				telemetriesToSend = new List<TelemetryPayload>(_telemetryPayloads);
				logsToSend = new List<LogPayload>(_logPayloads);

				_eventPayloads.Clear();
				_telemetryPayloads.Clear();
				_logPayloads.Clear();
			}

#if UNITY_ANDROID && !UNITY_EDITOR
			// When this session uses ArborInsightService, never do our own HTTP; push any queued items to the service.
			if (Authentication.Authentication.UsingArborInsightServiceForData())
			{
				int e = eventsToSend.Count, t = telemetriesToSend.Count, l = logsToSend.Count;
				PushQueuedToService(eventsToSend, telemetriesToSend, logsToSend);
				yield break;
			}
#endif
			// Retry logic for data sending - using separate coroutine to avoid yield in try-catch
			yield return SendWithRetry(eventsToSend, telemetriesToSend, logsToSend);
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		/// <summary>
		/// Pushes queued events, telemetry, and logs to ArborInsightService via Deferred APIs (no HTTP from Unity).
		/// </summary>
		private static void PushQueuedToService(List<EventPayload> events, List<TelemetryPayload> telemetries, List<LogPayload> logs)
		{
			foreach (var p in events)
				ArborInsightServiceClient.EventDeferred(p.name, p.meta ?? new Dictionary<string, string>());
			foreach (var p in telemetries)
				ArborInsightServiceClient.AddTelemetryEntryDeferred(p.name, p.meta ?? new Dictionary<string, string>());
			foreach (var p in logs)
			{
				string level = (p.logLevel ?? "").ToUpperInvariant();
				var dict = p.meta ?? new Dictionary<string, string>();
				if (level == "DEBUG") ArborInsightServiceClient.LogDebugDeferred(p.text ?? "", dict);
				else if (level == "INFO") ArborInsightServiceClient.LogInfoDeferred(p.text ?? "", dict);
				else if (level == "WARN") ArborInsightServiceClient.LogWarnDeferred(p.text ?? "", dict);
				else if (level == "ERROR") ArborInsightServiceClient.LogErrorDeferred(p.text ?? "", dict);
				else if (level == "CRITICAL") ArborInsightServiceClient.LogCriticalDeferred(p.text ?? "", dict);
				else ArborInsightServiceClient.LogInfoDeferred(p.text ?? "", dict);
			}
		}
#endif

		/// <summary>
		/// Sends data with retry logic, avoiding yield statements in try-catch blocks
		/// </summary>
		private static IEnumerator SendWithRetry(List<EventPayload> eventsToSend, List<TelemetryPayload> telemetriesToSend, List<LogPayload> logsToSend)
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
					var wrapper = new DataPayloadWrapper
					{
						@event = eventsToSend,
						telemetry = telemetriesToSend,
						basicLog = logsToSend
					};
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
					lastError = $"Data request creation failed: {ex.Message}";
					Debug.LogError($"AbxrLib: {lastError}");

					if (IsDataRetryableException(ex) && retryCount < maxRetries)
					{
						shouldRetry = true;
					}
				}

				// Handle retry logic for request creation failure (yield outside try-catch)
				if (shouldRetry)
				{
					retryCount++;
					Debug.LogWarning($"AbxrLib: Data request creation failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
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
						lastError = HandleDataNetworkError(request, retryCount, maxRetries);

						if (IsDataRetryableError(request))
						{
							responseShouldRetry = true;
						}
					}
				}
				catch (System.Exception ex)
				{
					lastError = $"Data response handling failed: {ex.Message}";
					Debug.LogError($"AbxrLib: {lastError}");

					if (IsDataRetryableException(ex) && retryCount < maxRetries)
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
						Debug.LogWarning($"AbxrLib: Data POST Request failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
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
				Debug.LogError($"AbxrLib: Data POST Request failed after {retryCount} attempts: {lastError}");
				_timer = Configuration.Instance.sendRetryIntervalSeconds;
				lock (_lock)
				{
					// Re-insert events with queue limit enforcement
					foreach (var eventPayload in eventsToSend)
					{
						if (IsQueueAtLimit(_eventPayloads, "Event"))
						{
							Debug.LogWarning("AbxrLib: Cannot re-insert failed events - queue at limit, dropping event");
							break; // Stop re-inserting if queue is at limit
						}
						_eventPayloads.Insert(0, eventPayload);
					}
					
					// Re-insert telemetry with queue limit enforcement
					foreach (var telemetryPayload in telemetriesToSend)
					{
						if (IsQueueAtLimit(_telemetryPayloads, "Telemetry"))
						{
							Debug.LogWarning("AbxrLib: Cannot re-insert failed telemetry - queue at limit, dropping telemetry");
							break; // Stop re-inserting if queue is at limit
						}
						_telemetryPayloads.Insert(0, telemetryPayload);
					}
					
					// Re-insert logs with queue limit enforcement
					foreach (var logPayload in logsToSend)
					{
						if (IsQueueAtLimit(_logPayloads, "Log"))
						{
							Debug.LogWarning("AbxrLib: Cannot re-insert failed logs - queue at limit, dropping log");
							break; // Stop re-inserting if queue is at limit
						}
						_logPayloads.Insert(0, logPayload);
					}
				}
			}
		}

		/// <summary>
		/// Handles network errors for data requests and determines appropriate error messages
		/// </summary>
		private static string HandleDataNetworkError(UnityWebRequest request, int retryCount, int maxRetries)
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
		/// Determines if a data network error is retryable
		/// </summary>
		private static bool IsDataRetryableError(UnityWebRequest request)
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
		/// Determines if a data exception is retryable
		/// </summary>
		private static bool IsDataRetryableException(System.Exception ex)
		{
			// Retry on network-related exceptions
			return ex is System.Net.WebException ||
				   ex is System.Net.Sockets.SocketException ||
				   ex.Message.Contains("timeout") ||
				   ex.Message.Contains("connection");
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

		/// <summary>
		/// Gets the total count of all data entries across all queues
		/// </summary>
		private static int GetTotalDataCount()
		{
			return _eventPayloads.Count + _telemetryPayloads.Count + _logPayloads.Count;
		}

		// Payload classes
		private class EventPayload
		{
			public string timestamp;
			public long preciseTimestamp;
			public string name;
			public Dictionary<string, string> meta;
		}

		private class TelemetryPayload
		{
			public string timestamp;
			public long preciseTimestamp;
			public string name;
			public Dictionary<string, string> meta;
		}

		private class LogPayload
		{
			public string timestamp;
			public long preciseTimestamp;
			public string logLevel;
			public string text;
			public Dictionary<string, string> meta;
		}

		private class DataPayloadWrapper
		{
			public List<EventPayload> @event;
			public List<TelemetryPayload> telemetry;
			public List<LogPayload> basicLog;
		}
	}
}

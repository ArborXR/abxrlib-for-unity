using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Events
{
	public class EventBatcher : MonoBehaviour
	{
		private const string UrlPath = "/v1/collect/event";
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
	
	public static void Add(string name, Dictionary<string, string> meta)
	{
		long eventTime = Utils.GetUnityTime();
		var payload = new Payload
		{
			preciseTimestamp = eventTime.ToString(),
			name = name,
			meta = meta
		};
    
		lock (_lock)
		{
			_payloads.Add(payload);
			if (_payloads.Count >= Configuration.Instance.eventsPerSendAttempt)
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
		if (!Authentication.Authentication.FullyAuthenticated()) yield break;
		lock (_lock)
		{
			if (_payloads.Count == 0) yield break;
		}
	
		List<Payload> eventsToSend;
		lock (_lock)
		{
			// Copy current list and leave original untouched
			eventsToSend = new List<Payload>(_payloads);
			foreach (var evt in eventsToSend) _payloads.Remove(evt);
		}
		
		// Retry logic for event sending - using separate coroutine to avoid yield in try-catch
		yield return SendWithRetry(eventsToSend);
	}
	
	/// <summary>
	/// Sends events with retry logic, avoiding yield statements in try-catch blocks
	/// </summary>
	private static IEnumerator SendWithRetry(List<Payload> eventsToSend)
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
				var wrapper = new PayloadWrapper { data = eventsToSend };
				string json = JsonConvert.SerializeObject(wrapper);
			
				request = new UnityWebRequest(_uri, "POST");
				Utils.BuildRequest(request, json);
				Authentication.Authentication.SetAuthHeaders(request, json);
				
				// Set timeout to prevent hanging requests
				request.timeout = 30; // 30 second timeout
				requestCreated = true;
			}
			catch (System.Exception ex)
			{
				lastError = $"Event request creation failed: {ex.Message}";
				Debug.LogError($"AbxrLib: {lastError}");
				
				if (IsEventRetryableException(ex) && retryCount < maxRetries)
				{
					shouldRetry = true;
				}
			}
			
			// Handle retry logic for request creation failure (yield outside try-catch)
			if (shouldRetry)
			{
				retryCount++;
				Debug.LogWarning($"AbxrLib: Event request creation failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
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
					Debug.Log($"AbxrLib: Event POST Request successful (sent {eventsToSend.Count} events)");
					responseSuccess = true;
					success = true;
				}
				else
				{
					// Handle different types of network errors
					lastError = HandleEventNetworkError(request, retryCount, maxRetries);
					
					if (IsEventRetryableError(request))
					{
						responseShouldRetry = true;
					}
				}
			}
			catch (System.Exception ex)
			{
				lastError = $"Event response handling failed: {ex.Message}";
				Debug.LogError($"AbxrLib: {lastError}");
				
				if (IsEventRetryableException(ex) && retryCount < maxRetries)
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
					Debug.LogWarning($"AbxrLib: Event POST Request failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
					yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds);
				}
			}
			else if (!responseSuccess)
			{
				// Non-retryable error, break out of retry loop
				break;
			}
		}
		
		// If all retries failed, put events back in queue
		if (!success)
		{
			Debug.LogError($"AbxrLib: Event POST Request failed after {retryCount} attempts: {lastError}");
			_timer = Configuration.Instance.sendRetryIntervalSeconds;
			lock (_lock)
			{
				_payloads.InsertRange(0, eventsToSend);
			}
		}
	}
	
	/// <summary>
	/// Handles network errors for event requests and determines appropriate error messages
	/// </summary>
	private static string HandleEventNetworkError(UnityWebRequest request, int retryCount, int maxRetries)
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
	/// Determines if an event network error is retryable
	/// </summary>
	private static bool IsEventRetryableError(UnityWebRequest request)
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
	/// Determines if an event exception is retryable
	/// </summary>
	private static bool IsEventRetryableException(System.Exception ex)
	{
		// Retry on network-related exceptions
		return ex is System.Net.WebException || 
			   ex is System.Net.Sockets.SocketException ||
			   ex.Message.Contains("timeout") ||
			   ex.Message.Contains("connection");
	}

	private class Payload
	{
		public string preciseTimestamp;
		public string name;
		public Dictionary<string, string> meta;
	}
	private class PayloadWrapper
	{
		public List<Payload> data;
	}
	}
}
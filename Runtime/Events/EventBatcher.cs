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
		private static readonly List<Payload> Payloads = new();
		private static readonly object Lock = new();
		private static float _timer;
		private static float _lastCallTime;
		private const float MaxCallFrequencySeconds = 1f;
		private static float _healthCheckTimer;
		
		private void Start()
		{
			_uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
			_timer = Configuration.Instance.sendNextBatchWaitSeconds;
			_healthCheckTimer = 30f; // Initialize health check timer
		}

		private void Update()
		{
			_timer -= Time.deltaTime;
			_healthCheckTimer -= Time.deltaTime;
			
			// Periodic health check of CoroutineRunner system
			if (_healthCheckTimer <= 0)
			{
				_healthCheckTimer = 30f; // Check every 30 seconds
				CoroutineRunner.HealthCheck();
			}
			
			if (_timer <= 0) 
			{
				// Use safe coroutine start with redundancy
				CoroutineRunner.SafeStartCoroutine(Send());
				
				// Also schedule backup action in case Update() cycle gets interrupted
				CoroutineRunner.ScheduleBackupAction(() => {
					CoroutineRunner.SafeStartCoroutine(Send());
				});
			}
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
	    
			lock (Lock)
			{
				Payloads.Add(payload);
				if (Payloads.Count >= Configuration.Instance.eventsPerSendAttempt)
				{
					_timer = 0; // Send on the next update
					
					// Also trigger immediate send with backup redundancy
					CoroutineRunner.SafeStartCoroutine(Send());
					CoroutineRunner.ScheduleBackupAction(() => {
						CoroutineRunner.SafeStartCoroutine(Send());
					});
				}
			}
		}
	
		public static IEnumerator Send()
		{
			if (Time.time - _lastCallTime < MaxCallFrequencySeconds) yield break;
		
			_lastCallTime = Time.time;
			_timer = Configuration.Instance.sendNextBatchWaitSeconds; // reset timer
			if (!Authentication.Authentication.Authenticated()) yield break;
			lock (Lock)
			{
				if (Payloads.Count == 0) yield break;
			}
		
			List<Payload> eventsToSend;
			lock (Lock)
			{
				// Copy current list and leave original untouched
				eventsToSend = new List<Payload>(Payloads);
				foreach (var evt in eventsToSend) Payloads.Remove(evt);
			}
				
			var wrapper = new PayloadWrapper { data = eventsToSend };
			string json = JsonConvert.SerializeObject(wrapper);
		
			using var request = new UnityWebRequest(_uri, "POST");
			Utils.BuildRequest(request, json);
			Authentication.Authentication.SetAuthHeaders(request, json);
		
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Debug.Log("AbxrLib - Event POST Request successful");
			}
			else
			{
				Debug.LogError($"AbxrLib - Event POST Request failed : {request.error} - {request.downloadHandler.text}");
				_timer = Configuration.Instance.sendRetryIntervalSeconds;
				lock (Lock)
				{
					Payloads.InsertRange(0, eventsToSend);
				}
			}
		}

		/// <summary>
		/// Force immediate send of all pending events with maximum redundancy
		/// Useful for critical events like assessments/objectives completion on VR headsets
		/// </summary>
		public static void ForceImmediateSend()
		{
			// Multiple attempts to ensure delivery
			CoroutineRunner.SafeStartCoroutine(Send());
			CoroutineRunner.ScheduleBackupAction(() => {
				CoroutineRunner.SafeStartCoroutine(Send());
			});
			
			// Additional backup after short delay
			CoroutineRunner.ScheduleBackupAction(() => {
				System.Threading.Thread.Sleep(1000); // Wait 1 second
				CoroutineRunner.SafeStartCoroutine(Send());
			});
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
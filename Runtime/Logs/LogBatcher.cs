using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Logs
{
	public class LogBatcher : MonoBehaviour
	{
		private const string UrlPath = "/v1/collect/log";
		private static Uri _uri;
		private static readonly List<Payload> Payloads = new();
		private static readonly object Lock = new();
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
	
		public static void Add(string logLevel, string text, Dictionary<string, string> meta)
		{
			long logTime = Utils.GetUnityTime();
			var payload = new Payload
			{
				preciseTimestamp = logTime.ToString(),
				logLevel = logLevel,
				text = text,
				meta = meta
			};
		
			lock (Lock)
			{
				Payloads.Add(payload);
				if (Payloads.Count >= Configuration.Instance.logsPerSendAttempt)
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
			lock (Lock)
			{
				if (Payloads.Count == 0) yield break;
			}
		
			List<Payload> logsToSend;
			lock (Lock)
			{
				// Copy current list and leave original untouched
				logsToSend = new List<Payload>(Payloads);
				foreach (var log in logsToSend) Payloads.Remove(log);
			}
		
			var wrapper = new PayloadWrapper { data = logsToSend };
			string json = JsonConvert.SerializeObject(wrapper);
		
			using var request = new UnityWebRequest(_uri, "POST");
			Utils.BuildRequest(request, json);
			Authentication.Authentication.SetAuthHeaders(request, json);
		
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Debug.Log("AbxrLib: Log POST Request successful");
			}
			else
			{
				Debug.LogError($"AbxrLib: Log POST Request failed : {request.error} - {request.downloadHandler.text}");
				_timer = Configuration.Instance.sendRetryIntervalSeconds;
				lock (Lock)
				{
					Payloads.InsertRange(0, logsToSend);
				}
			}
		}
	
		private class Payload
		{
			public string preciseTimestamp;
			public string logLevel;
			public string text;
			public Dictionary<string, string> meta;
		}
		private class PayloadWrapper
		{
			public List<Payload> data;
		}
	}
}
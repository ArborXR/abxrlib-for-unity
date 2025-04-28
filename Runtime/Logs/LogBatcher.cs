using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class LogBatcher : MonoBehaviour
{
	private static float _sendInterval;
	private const string UrlPath = "/v1/collect/log";
	private static Uri _uri;
	private static readonly List<Payload> Payloads = new();
	private static readonly object Lock = new();
	
	private void Start()
	{
		_uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
		_sendInterval = Configuration.Instance.sendNextBatchWaitSeconds;
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
			yield return new WaitForSeconds(_sendInterval);
			yield return Send();
		}
	}

	private static IEnumerator Send()
	{
		if (!Authentication.Authenticated()) yield break;
		_sendInterval = Configuration.Instance.sendNextBatchWaitSeconds;
		lock (Lock)
		{
			if (Payloads.Count > 0) yield return SendLogs();
		}
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
		
		lock (Lock) Payloads.Add(payload);
	}

	private static IEnumerator SendLogs()
	{
		List<Payload> logsToSend;
		lock (Lock)
		{
			// Copy current list and leave original untouched
			logsToSend = new List<Payload>(Payloads);
			foreach (var log in logsToSend) Payloads.Remove(log);
		}
		
		var wrapper = new PayloadWrapper { data = logsToSend };
		string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
		
		using var request = new UnityWebRequest(_uri, "POST");
		Utils.BuildRequest(request, json);
		Authentication.SetAuthHeaders(request, json);
		
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			Debug.Log("AbxrLib - Log POST Request successful");
		}
		else
		{
			Debug.LogError($"AbxrLib - Log POST Request failed : {request.error} - {request.downloadHandler.text}");
			_sendInterval = Configuration.Instance.sendRetryIntervalSeconds;
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
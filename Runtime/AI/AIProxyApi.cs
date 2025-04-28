using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class AIProxyApi : MonoBehaviour
{
    private const string UrlPath = "/v1/services/llm";
    private static readonly List<string> PastMessages = new();
    private static Uri _uri;

    private void Start()
    {
        _uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
    }
    
    public static IEnumerator SendPrompt(string prompt, string llmProvider, List<string> pastMessages, Action<string> callback)
    {
        if (!Authentication.Authenticated()) yield break;
        
        pastMessages = pastMessages == null ? PastMessages : pastMessages.Union(PastMessages).ToList();
        
        var payload = new AIPromptPayload
        {
            prompt = prompt,
            llmProvider = llmProvider,
            pastMessages = pastMessages
        };
        
        string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        
        using var request = new UnityWebRequest(_uri, "POST");
        Utils.BuildRequest(request, json);
        Authentication.SetAuthHeaders(request, json);
		
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            PastMessages.Add(prompt);
            callback?.Invoke(request.downloadHandler.text);
            Debug.Log("AbxrLib - AI POST Request successful");
        }
        else
        {
            Debug.LogError($"AbxrLib - AI POST Request failed : {request.error}");
            callback?.Invoke(null);
            //TODO retry logic
        }
    }
    
    private class AIPromptPayload
    {
        public string prompt;
        public string llmProvider;
        public List<string> pastMessages;
    }
}
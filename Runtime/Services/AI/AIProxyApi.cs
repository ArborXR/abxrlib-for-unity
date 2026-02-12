using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.AI
{
    public class AIProxyApi
    {
        private const string UrlPath = "/v1/services/llm";
        private static readonly List<string> PastMessages = new();
        private static Uri _uri;
        private readonly AbxrAuthService _authService;

        public AIProxyApi(AbxrAuthService authService)
        {
            _uri = new Uri(new Uri(Configuration.Instance.restUrl), UrlPath);
            _authService = authService;
        }
    
        public IEnumerator SendPrompt(string prompt, string llmProvider, List<string> pastMessages, Action<string> callback)
        {
            if (!_authService.Authenticated) 
            {
                callback?.Invoke(null);
                yield break;
            }
        
            pastMessages = pastMessages == null ? PastMessages : pastMessages.Union(PastMessages).ToList();
        
            var payload = new AIPromptPayload
            {
                prompt = prompt,
                llmProvider = llmProvider,
                pastMessages = pastMessages
            };
        
            string json = JsonConvert.SerializeObject(payload);
            
            // Use retry logic for AI proxy requests
            yield return SendPromptWithRetry(json, prompt, callback);
        }
        
        /// <summary>
        /// Sends AI prompt with retry logic, avoiding yield statements in try-catch blocks
        /// </summary>
        private IEnumerator SendPromptWithRetry(string json, string prompt, Action<string> callback)
        {
            int retryCount = 0;
            int maxRetries = Configuration.Instance.sendRetriesOnFailure;
            bool success = false;
            string lastError = "";
            string response = null;

            while (retryCount <= maxRetries && !success)
            {
                // Create request and handle creation errors
                UnityWebRequest request = null;
                bool requestCreated = false;
                bool shouldRetry = false;

                // Request creation with error handling (no yield statements)
                try
                {
                    request = new UnityWebRequest(_uri, "POST");
                    Utils.BuildRequest(request, json);
                    _authService.SetAuthHeaders(request, json);

                    // Set timeout to prevent hanging requests
                    request.timeout = Configuration.Instance.requestTimeoutSeconds;
                    requestCreated = true;
                }
                catch (Exception ex)
                {
                    lastError = $"AI request creation failed: {ex.Message}";
                    Debug.LogError($"AbxrLib: {lastError}");

                    if (IsAIRetryableException(ex) && retryCount < maxRetries)
                    {
                        shouldRetry = true;
                    }
                }

                // Handle retry logic for request creation failure (yield outside try-catch)
                if (shouldRetry)
                {
                    retryCount++;
                    Debug.LogWarning($"AbxrLib: AI request creation failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
                    yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds);
                    continue;
                }
                
                if (!requestCreated) break; // Non-retryable error or max retries reached

                // Send request (yield outside try-catch)
                yield return request.SendWebRequest();

                // Handle response (no yield statements in try-catch)
                bool responseSuccess = false;
                bool responseShouldRetry = false;

                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        response = request.downloadHandler.text;
                        PastMessages.Add(prompt);
                        responseSuccess = true;
                        success = true;
                    }
                    else
                    {
                        // Handle different types of network errors
                        lastError = HandleAINetworkError(request);

                        if (IsAIRetryableError(request))
                        {
                            responseShouldRetry = true;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    lastError = $"AI response handling failed: {ex.Message}";
                    Debug.LogError($"AbxrLib: {lastError}");

                    if (IsAIRetryableException(ex) && retryCount < maxRetries)
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
                        Debug.LogWarning($"AbxrLib: AI POST Request failed (attempt {retryCount}), retrying in {Configuration.Instance.sendRetryIntervalSeconds} seconds...");
                        yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds);
                    }
                }
                else if (!responseSuccess)
                {
                    // Non-retryable error, break out of retry loop
                    break;
                }
            }

            // Call callback with result (success or failure)
            if (success)
            {
                callback?.Invoke(response);
            }
            else
            {
                Debug.LogError($"AbxrLib: AI POST Request failed after {retryCount} attempts: {lastError}");
                callback?.Invoke(null);
            }
        }

        /// <summary>
        /// Handles network errors for AI requests and determines appropriate error messages
        /// </summary>
        private static string HandleAINetworkError(UnityWebRequest request)
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
        /// Determines if an AI network error is retryable
        /// </summary>
        private static bool IsAIRetryableError(UnityWebRequest request)
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
        /// Determines if an AI exception is retryable
        /// </summary>
        private static bool IsAIRetryableException(Exception ex)
        {
            // Retry on network-related exceptions
            return ex is System.Net.WebException ||
                   ex is System.Net.Sockets.SocketException ||
                   ex.Message.Contains("timeout") ||
                   ex.Message.Contains("connection");
        }
    }
}
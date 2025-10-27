/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Mock Network Provider for ABXRLib Tests
 * 
 * Simulates network requests and responses for testing without requiring
 * real network connectivity or backend services.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Tests.Runtime.TestDoubles
{
    /// <summary>
    /// Mock network provider that simulates network requests/responses
    /// for testing without requiring real network connectivity.
    /// </summary>
    public class MockNetworkProvider
    {
        public enum NetworkScenario
        {
            Success,
            ConnectionError,
            Timeout,
            ServerError,
            InvalidResponse,
            RateLimited
        }

        public NetworkScenario CurrentScenario { get; set; } = NetworkScenario.Success;
        public int ResponseDelayMs { get; set; } = 100;
        public string MockResponseData { get; set; } = "{\"success\": true}";
        public int RequestCount { get; private set; }
        public List<string> RequestUrls { get; private set; } = new List<string>();
        public List<string> RequestBodies { get; private set; } = new List<string>();
        
        /// <summary>
        /// Simulates a network request and returns appropriate response
        /// </summary>
        public IEnumerator SimulateRequest(string url, string method, string body = null)
        {
            RequestCount++;
            RequestUrls.Add(url);
            RequestBodies.Add(body ?? "");
            
            // Simulate network delay
            yield return new WaitForSeconds(ResponseDelayMs / 1000f);
            
            switch (CurrentScenario)
            {
                case NetworkScenario.Success:
                    yield return SimulateSuccessfulResponse();
                    break;
                    
                case NetworkScenario.ConnectionError:
                    yield return SimulateConnectionError();
                    break;
                    
                case NetworkScenario.Timeout:
                    yield return SimulateTimeout();
                    break;
                    
                case NetworkScenario.ServerError:
                    yield return SimulateServerError();
                    break;
                    
                case NetworkScenario.InvalidResponse:
                    yield return SimulateInvalidResponse();
                    break;
                    
                case NetworkScenario.RateLimited:
                    yield return SimulateRateLimited();
                    break;
            }
        }
        
        private IEnumerator SimulateSuccessfulResponse()
        {
            Debug.Log($"MockNetworkProvider: Simulating successful response - {MockResponseData}");
            yield return null;
        }
        
        private IEnumerator SimulateConnectionError()
        {
            Debug.Log("MockNetworkProvider: Simulating connection error");
            yield return null;
        }
        
        private IEnumerator SimulateTimeout()
        {
            Debug.Log("MockNetworkProvider: Simulating timeout");
            yield return new WaitForSeconds(30f); // Simulate long timeout
        }
        
        private IEnumerator SimulateServerError()
        {
            Debug.Log("MockNetworkProvider: Simulating server error (500)");
            yield return null;
        }
        
        private IEnumerator SimulateInvalidResponse()
        {
            Debug.Log("MockNetworkProvider: Simulating invalid response");
            yield return null;
        }
        
        private IEnumerator SimulateRateLimited()
        {
            Debug.Log("MockNetworkProvider: Simulating rate limited (429)");
            yield return null;
        }
        
        /// <summary>
        /// Sets up mock response for authentication requests
        /// </summary>
        public void SetAuthResponse(string token, string secret, DateTime expiry, string userId = null, string userEmail = null)
        {
            var authResponse = new
            {
                Token = token,
                Secret = secret,
                ExpiresAt = expiry.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                UserId = userId,
                UserEmail = userEmail
            };
            
            MockResponseData = JsonUtility.ToJson(authResponse);
        }
        
        /// <summary>
        /// Sets up mock response for data collection requests
        /// </summary>
        public void SetDataResponse(bool success, string message = null)
        {
            var dataResponse = new
            {
                success = success,
                message = message ?? (success ? "Data received successfully" : "Data processing failed")
            };
            
            MockResponseData = JsonUtility.ToJson(dataResponse);
        }
        
        /// <summary>
        /// Resets the mock provider to initial state
        /// </summary>
        public void Reset()
        {
            CurrentScenario = NetworkScenario.Success;
            ResponseDelayMs = 100;
            MockResponseData = "{\"success\": true}";
            RequestCount = 0;
            RequestUrls.Clear();
            RequestBodies.Clear();
        }
        
        /// <summary>
        /// Verifies that a request was made to the expected URL
        /// </summary>
        public bool WasRequestMadeTo(string url)
        {
            return RequestUrls.Contains(url);
        }
        
        /// <summary>
        /// Verifies that a request contained the expected body content
        /// </summary>
        public bool WasRequestBodyContaining(string content)
        {
            foreach (var body in RequestBodies)
            {
                if (body.Contains(content))
                    return true;
            }
            return false;
        }
    }
}

/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Mock Authentication Provider for ABXRLib Tests
 * 
 * Provides configurable authentication responses for testing different
 * AuthMechanism scenarios without requiring real backend authentication.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AbxrLib.Tests.Runtime.TestDoubles
{
    /// <summary>
    /// Mock authentication provider that simulates different authentication scenarios
    /// for testing without requiring real backend authentication.
    /// </summary>
    public class MockAuthenticationProvider
    {
        public enum AuthScenario
        {
            Success,
            Failure,
            KeyboardAuth,
            SSOAuth,
            CustomAuth,
            Timeout,
            NetworkError
        }

        public AuthScenario CurrentScenario { get; set; } = AuthScenario.Success;
        
        public string MockAuthToken { get; set; } = "mock_auth_token_12345";
        public string MockApiSecret { get; set; } = "mock_api_secret_67890";
        public DateTime MockTokenExpiry { get; set; } = DateTime.UtcNow.AddHours(1);
        
        public string MockUserId { get; set; } = "test_user_123";
        public string MockUserEmail { get; set; } = "test@example.com";
        public List<Abxr.ModuleData> MockModuleData { get; set; } = new List<Abxr.ModuleData>();
        
        public string LastError { get; set; }
        public int AuthCallCount { get; private set; }
        
        // AuthMechanism simulation
        public AuthMechanismResponse MockAuthMechanism { get; set; } = new AuthMechanismResponse();
        public bool RequiresAuthMechanismResponse { get; set; } = false;
        public string MockAuthMechanismResponse { get; set; } = "";
        
        // Default responses for different AuthMechanism types
        private static readonly Dictionary<string, string> DefaultAuthResponses = new Dictionary<string, string>
        {
            ["assessmentPin"] = "999999",
            ["email"] = "testuser",  // Will be combined with domain
            ["text"] = "EmpID1234"
        };
        
        /// <summary>
        /// Simulates an authentication request and returns appropriate response
        /// </summary>
        public IEnumerator SimulateAuthentication()
        {
            AuthCallCount++;
            
            // Simulate network delay
            yield return new WaitForSeconds(0.1f);
            
            switch (CurrentScenario)
            {
                case AuthScenario.Success:
                    yield return SimulateSuccessfulAuth();
                    break;
                    
                case AuthScenario.Failure:
                    yield return SimulateFailedAuth("Invalid credentials");
                    break;
                    
                case AuthScenario.KeyboardAuth:
                    yield return SimulateKeyboardAuth();
                    break;
                    
                case AuthScenario.SSOAuth:
                    yield return SimulateSSOAuth();
                    break;
                    
                case AuthScenario.CustomAuth:
                    yield return SimulateCustomAuth();
                    break;
                    
                case AuthScenario.Timeout:
                    yield return SimulateTimeout();
                    break;
                    
                case AuthScenario.NetworkError:
                    yield return SimulateNetworkError();
                    break;
            }
        }
        
        private IEnumerator SimulateSuccessfulAuth()
        {
            // Simulate successful authentication response
            Debug.Log("MockAuthenticationProvider: Simulating successful authentication");
            yield return null;
        }
        
        private IEnumerator SimulateFailedAuth(string error)
        {
            LastError = error;
            Debug.Log($"MockAuthenticationProvider: Simulating failed authentication - {error}");
            yield return null;
        }
        
        private IEnumerator SimulateKeyboardAuth()
        {
            // Simulate keyboard authentication flow
            Debug.Log("MockAuthenticationProvider: Simulating keyboard authentication");
            yield return new WaitForSeconds(0.2f); // Simulate user input delay
            yield return SimulateSuccessfulAuth();
        }
        
        private IEnumerator SimulateSSOAuth()
        {
            // Simulate SSO authentication flow
            Debug.Log("MockAuthenticationProvider: Simulating SSO authentication");
            yield return new WaitForSeconds(0.3f); // Simulate SSO redirect delay
            yield return SimulateSuccessfulAuth();
        }
        
        private IEnumerator SimulateCustomAuth()
        {
            // Simulate custom authentication mechanism
            Debug.Log("MockAuthenticationProvider: Simulating custom authentication");
            yield return new WaitForSeconds(0.1f);
            yield return SimulateSuccessfulAuth();
        }
        
        private IEnumerator SimulateTimeout()
        {
            Debug.Log("MockAuthenticationProvider: Simulating authentication timeout");
            yield return new WaitForSeconds(30f); // Simulate long timeout
            LastError = "Authentication timeout";
        }
        
        private IEnumerator SimulateNetworkError()
        {
            Debug.Log("MockAuthenticationProvider: Simulating network error");
            yield return new WaitForSeconds(0.1f);
            LastError = "Network connection failed";
        }
        
        /// <summary>
        /// Sets up mock auth mechanism response for testing
        /// </summary>
        public void SetAuthMechanism(string type, string prompt = null, string domain = null, string customResponse = null)
        {
            MockAuthMechanism = new AuthMechanismResponse
            {
                type = type,
                prompt = prompt,
                domain = domain
            };
            RequiresAuthMechanismResponse = !string.IsNullOrEmpty(type);
            
            // Set default response if none provided and type has a default
            if (RequiresAuthMechanismResponse && string.IsNullOrEmpty(customResponse))
            {
                if (DefaultAuthResponses.ContainsKey(type))
                {
                    if (type == "email" && !string.IsNullOrEmpty(domain))
                    {
                        // For email type, combine testuser with domain
                        MockAuthMechanismResponse = $"testuser@{domain}";
                    }
                    else
                    {
                        MockAuthMechanismResponse = DefaultAuthResponses[type];
                    }
                }
                else
                {
                    MockAuthMechanismResponse = $"default_response_for_{type}";
                }
            }
            else if (!string.IsNullOrEmpty(customResponse))
            {
                MockAuthMechanismResponse = customResponse;
            }
        }
        
        /// <summary>
        /// Sets up no auth mechanism required
        /// </summary>
        public void SetNoAuthMechanism()
        {
            MockAuthMechanism = new AuthMechanismResponse();
            RequiresAuthMechanismResponse = false;
        }
        
        /// <summary>
        /// Simulates client response to auth mechanism
        /// </summary>
        public void SetAuthMechanismResponse(string response)
        {
            MockAuthMechanismResponse = response;
        }
        
        /// <summary>
        /// Sets up assessment PIN auth mechanism with default PIN (999999)
        /// </summary>
        public void SetAssessmentPinAuth(string prompt = "Please enter your assessment PIN", string customPin = null)
        {
            SetAuthMechanism("assessmentPin", prompt, null, customPin);
        }
        
        /// <summary>
        /// Sets up email auth mechanism with default email (testuser@domain)
        /// </summary>
        public void SetEmailAuth(string prompt = "Please enter your email", string domain = "example.com", string customEmail = null)
        {
            SetAuthMechanism("email", prompt, domain, customEmail);
        }
        
        /// <summary>
        /// Sets up text auth mechanism with default text (EmpID1234)
        /// </summary>
        public void SetTextAuth(string prompt = "Please enter your employee ID", string customText = null)
        {
            SetAuthMechanism("text", prompt, null, customText);
        }
        
        /// <summary>
        /// Resets the mock provider to initial state
        /// </summary>
        public void Reset()
        {
            CurrentScenario = AuthScenario.Success;
            MockAuthToken = "mock_auth_token_12345";
            MockApiSecret = "mock_api_secret_67890";
            MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            MockUserId = "test_user_123";
            MockUserEmail = "test@example.com";
            MockModuleData.Clear();
            LastError = null;
            AuthCallCount = 0;
            MockAuthMechanism = new AuthMechanismResponse();
            RequiresAuthMechanismResponse = false;
            MockAuthMechanismResponse = "";
        }
        
        /// <summary>
        /// Creates mock module data for testing module target functionality
        /// </summary>
        public void SetMockModuleData(params string[] moduleTargets)
        {
            MockModuleData.Clear();
            foreach (var target in moduleTargets)
            {
                MockModuleData.Add(new Abxr.ModuleData
                {
                    moduleTarget = target,
                    userId = MockUserId,
                    userEmail = MockUserEmail,
                    userData = new Dictionary<string, object> { { "test_data", "value" } }
                });
            }
        }
    }
    
    /// <summary>
    /// Represents an authentication mechanism response from the server
    /// </summary>
    public class AuthMechanismResponse
    {
        public string type { get; set; }
        public string prompt { get; set; }
        public string domain { get; set; }
        
        public AuthMechanismResponse()
        {
            type = null;
            prompt = null;
            domain = null;
        }
        
        public AuthMechanismResponse(string type, string prompt = null, string domain = null)
        {
            this.type = type;
            this.prompt = prompt;
            this.domain = domain;
        }
        
        public bool IsEmpty => string.IsNullOrEmpty(type);
    }
}

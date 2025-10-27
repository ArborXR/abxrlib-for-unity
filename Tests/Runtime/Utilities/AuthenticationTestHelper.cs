/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Test Helper for ABXRLib Tests
 * 
 * Helpers for setting up different authentication scenarios
 * and testing authentication-related functionality.
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Tests.Runtime.TestDoubles;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Helper utilities for testing authentication functionality
    /// </summary>
    public static class AuthenticationTestHelper
    {
        /// <summary>
        /// Sets up authentication test environment
        /// </summary>
        public static MockAuthenticationProvider SetupAuthTestEnvironment()
        {
            var mockAuth = new MockAuthenticationProvider();
            Debug.Log("AuthenticationTestHelper: Authentication test environment setup complete");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up successful authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupSuccessfulAuth()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            mockAuth.MockAuthToken = "test_auth_token_success";
            mockAuth.MockApiSecret = "test_api_secret_success";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_success";
            mockAuth.MockUserEmail = "test@example.com";
            
            Debug.Log("AuthenticationTestHelper: Set up successful authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up failed authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupFailedAuth(string errorMessage = "Authentication failed")
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Failure;
            mockAuth.LastError = errorMessage;
            
            Debug.Log($"AuthenticationTestHelper: Set up failed authentication scenario - {errorMessage}");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up keyboard authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupKeyboardAuth()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.KeyboardAuth;
            mockAuth.MockAuthToken = "test_auth_token_keyboard";
            mockAuth.MockApiSecret = "test_api_secret_keyboard";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_keyboard";
            mockAuth.MockUserEmail = "keyboard@example.com";
            
            // Set up keyboard auth mechanism
            mockAuth.SetAuthMechanism("keyboard", "Please enter your credentials", null);
            mockAuth.SetAuthMechanismResponse("keyboard_response");
            
            Debug.Log("AuthenticationTestHelper: Set up keyboard authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up SSO authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupSSOAuth()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.SSOAuth;
            mockAuth.MockAuthToken = "test_auth_token_sso";
            mockAuth.MockApiSecret = "test_api_secret_sso";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_sso";
            mockAuth.MockUserEmail = "sso@example.com";
            
            // Set up SSO auth mechanism
            mockAuth.SetAuthMechanism("sso", "Redirecting to SSO provider", "https://sso.example.com");
            mockAuth.SetAuthMechanismResponse("sso_response");
            
            Debug.Log("AuthenticationTestHelper: Set up SSO authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up custom authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupCustomAuth()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.CustomAuth;
            mockAuth.MockAuthToken = "test_auth_token_custom";
            mockAuth.MockApiSecret = "test_api_secret_custom";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_custom";
            mockAuth.MockUserEmail = "custom@example.com";
            
            // Set up custom auth mechanism
            mockAuth.SetAuthMechanism("custom", "Custom authentication required", "https://custom.example.com");
            mockAuth.SetAuthMechanismResponse("custom_response");
            
            Debug.Log("AuthenticationTestHelper: Set up custom authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up assessment PIN authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupAssessmentPinAuth(string customPin = null)
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            mockAuth.MockAuthToken = "test_auth_token_assessment_pin";
            mockAuth.MockApiSecret = "test_api_secret_assessment_pin";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_assessment_pin";
            mockAuth.MockUserEmail = "assessment@example.com";
            
            // Set up assessment PIN auth mechanism with default PIN (999999)
            mockAuth.SetAssessmentPinAuth("Please enter your assessment PIN", customPin);
            
            Debug.Log("AuthenticationTestHelper: Set up assessment PIN authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up email authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupEmailAuth(string domain = "example.com", string customEmail = null)
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            mockAuth.MockAuthToken = "test_auth_token_email";
            mockAuth.MockApiSecret = "test_api_secret_email";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_email";
            mockAuth.MockUserEmail = customEmail ?? $"testuser@{domain}";
            
            // Set up email auth mechanism with default email (testuser@domain)
            mockAuth.SetEmailAuth("Please enter your email", domain, customEmail);
            
            Debug.Log($"AuthenticationTestHelper: Set up email authentication scenario with domain {domain}");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up text authentication scenario
        /// </summary>
        public static MockAuthenticationProvider SetupTextAuth(string customText = null)
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            mockAuth.MockAuthToken = "test_auth_token_text";
            mockAuth.MockApiSecret = "test_api_secret_text";
            mockAuth.MockTokenExpiry = DateTime.UtcNow.AddHours(1);
            mockAuth.MockUserId = "test_user_text";
            mockAuth.MockUserEmail = "text@example.com";
            
            // Set up text auth mechanism with default text (EmpID1234)
            mockAuth.SetTextAuth("Please enter your employee ID", customText);
            
            Debug.Log("AuthenticationTestHelper: Set up text authentication scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up authentication timeout scenario
        /// </summary>
        public static MockAuthenticationProvider SetupAuthTimeout()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Timeout;
            mockAuth.LastError = "Authentication timeout";
            
            Debug.Log("AuthenticationTestHelper: Set up authentication timeout scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up network error scenario
        /// </summary>
        public static MockAuthenticationProvider SetupNetworkError()
        {
            var mockAuth = new MockAuthenticationProvider();
            mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.NetworkError;
            mockAuth.LastError = "Network connection failed";
            
            Debug.Log("AuthenticationTestHelper: Set up network error scenario");
            return mockAuth;
        }
        
        /// <summary>
        /// Sets up authentication with module data for testing module targets
        /// </summary>
        public static MockAuthenticationProvider SetupAuthWithModules(params string[] moduleTargets)
        {
            var mockAuth = SetupSuccessfulAuth();
            mockAuth.SetMockModuleData(moduleTargets);
            
            Debug.Log($"AuthenticationTestHelper: Set up authentication with {moduleTargets.Length} modules");
            return mockAuth;
        }
        
        /// <summary>
        /// Simulates authentication process with the given provider
        /// </summary>
        public static IEnumerator SimulateAuthentication(MockAuthenticationProvider mockAuth)
        {
            Debug.Log("AuthenticationTestHelper: Starting authentication simulation");
            yield return mockAuth.SimulateAuthentication();
            Debug.Log("AuthenticationTestHelper: Authentication simulation complete");
        }
        
        /// <summary>
        /// Asserts that authentication was successful
        /// </summary>
        public static void AssertAuthenticationSuccessful(MockAuthenticationProvider mockAuth)
        {
            Assert.AreEqual(MockAuthenticationProvider.AuthScenario.Success, mockAuth.CurrentScenario,
                "Authentication should be successful");
            Assert.IsNull(mockAuth.LastError, "Authentication should not have errors");
            Assert.IsNotNull(mockAuth.MockAuthToken, "Authentication should have a token");
            Assert.IsNotNull(mockAuth.MockApiSecret, "Authentication should have an API secret");
        }
        
        /// <summary>
        /// Asserts that authentication failed
        /// </summary>
        public static void AssertAuthenticationFailed(MockAuthenticationProvider mockAuth, string expectedError = null)
        {
            Assert.AreEqual(MockAuthenticationProvider.AuthScenario.Failure, mockAuth.CurrentScenario,
                "Authentication should have failed");
            Assert.IsNotNull(mockAuth.LastError, "Authentication should have an error message");
            
            if (!string.IsNullOrEmpty(expectedError))
            {
                Assert.AreEqual(expectedError, mockAuth.LastError, "Authentication error should match expected");
            }
        }
        
        /// <summary>
        /// Asserts that authentication timed out
        /// </summary>
        public static void AssertAuthenticationTimeout(MockAuthenticationProvider mockAuth)
        {
            Assert.AreEqual(MockAuthenticationProvider.AuthScenario.Timeout, mockAuth.CurrentScenario,
                "Authentication should have timed out");
            Assert.IsNotNull(mockAuth.LastError, "Authentication should have a timeout error");
            Assert.IsTrue(mockAuth.LastError.Contains("timeout"), "Error should indicate timeout");
        }
        
        /// <summary>
        /// Asserts that authentication had a network error
        /// </summary>
        public static void AssertAuthenticationNetworkError(MockAuthenticationProvider mockAuth)
        {
            Assert.AreEqual(MockAuthenticationProvider.AuthScenario.NetworkError, mockAuth.CurrentScenario,
                "Authentication should have had a network error");
            Assert.IsNotNull(mockAuth.LastError, "Authentication should have a network error message");
            Assert.IsTrue(mockAuth.LastError.Contains("network") || mockAuth.LastError.Contains("connection"),
                "Error should indicate network issue");
        }
        
        /// <summary>
        /// Asserts that module data was provided
        /// </summary>
        public static void AssertModuleDataProvided(MockAuthenticationProvider mockAuth, int expectedCount)
        {
            Assert.IsNotNull(mockAuth.MockModuleData, "Module data should not be null");
            Assert.AreEqual(expectedCount, mockAuth.MockModuleData.Count, 
                $"Should have {expectedCount} modules");
        }
        
        /// <summary>
        /// Asserts that a specific module target was provided
        /// </summary>
        public static void AssertModuleTargetProvided(MockAuthenticationProvider mockAuth, string expectedTarget)
        {
            Assert.IsNotNull(mockAuth.MockModuleData, "Module data should not be null");
            Assert.IsTrue(mockAuth.MockModuleData.Exists(m => m.target == expectedTarget),
                $"Module target '{expectedTarget}' should be provided");
        }
        
        /// <summary>
        /// Asserts that auth mechanism was provided correctly
        /// </summary>
        public static void AssertAuthMechanismProvided(MockAuthenticationProvider mockAuth, string expectedType, string expectedPrompt = null)
        {
            Assert.IsTrue(mockAuth.RequiresAuthMechanismResponse, "Auth mechanism should be required");
            Assert.IsNotNull(mockAuth.MockAuthMechanism, "Auth mechanism should not be null");
            Assert.AreEqual(expectedType, mockAuth.MockAuthMechanism.type, "Auth mechanism type should match");
            
            if (!string.IsNullOrEmpty(expectedPrompt))
            {
                Assert.AreEqual(expectedPrompt, mockAuth.MockAuthMechanism.prompt, "Auth mechanism prompt should match");
            }
        }
        
        /// <summary>
        /// Asserts that no auth mechanism was required
        /// </summary>
        public static void AssertNoAuthMechanismRequired(MockAuthenticationProvider mockAuth)
        {
            Assert.IsFalse(mockAuth.RequiresAuthMechanismResponse, "No auth mechanism should be required");
            Assert.IsTrue(mockAuth.MockAuthMechanism.IsEmpty, "Auth mechanism should be empty");
        }
        
        /// <summary>
        /// Asserts that auth mechanism response was provided
        /// </summary>
        public static void AssertAuthMechanismResponseProvided(MockAuthenticationProvider mockAuth, string expectedResponse = null)
        {
            Assert.IsNotNull(mockAuth.MockAuthMechanismResponse, "Auth mechanism response should not be null");
            
            if (!string.IsNullOrEmpty(expectedResponse))
            {
                Assert.AreEqual(expectedResponse, mockAuth.MockAuthMechanismResponse, "Auth mechanism response should match");
            }
        }
        
        /// <summary>
        /// Cleans up authentication test environment
        /// </summary>
        public static void CleanupAuthTestEnvironment(MockAuthenticationProvider mockAuth)
        {
            if (mockAuth != null)
            {
                mockAuth.Reset();
            }
            Debug.Log("AuthenticationTestHelper: Authentication test environment cleanup complete");
        }
        
        /// <summary>
        /// Cleans up authentication test environment (overload without parameter)
        /// </summary>
        public static void CleanupAuthTestEnvironment()
        {
            Debug.Log("AuthenticationTestHelper: Authentication test environment cleanup complete");
        }
    }
}

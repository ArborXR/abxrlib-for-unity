/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Test Helper for ABXRLib Tests
 * 
 * Consolidated authentication utilities for testing against real servers.
 * Includes shared authentication, test mode handling, and handoff functionality.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Consolidated authentication test helper for ABXRLib tests
    /// Combines shared authentication, test mode handling, and handoff functionality
    /// </summary>
    public static class AuthenticationTestHelper
    {
        // Shared authentication state
        private static bool _isAuthenticated = false;
        private static bool _authenticationInProgress = false;
        
        // Test mode state
        private static bool _isTestMode = false;
        private static string _defaultPin = "999999";
        private static string _defaultEmail = "testuser";
        private static string _defaultText = "EmpID1234";
        
        #region Shared Authentication
        
        /// <summary>
        /// Ensures authentication is completed before running tests
        /// This method is safe to call multiple times - it will only authenticate once
        /// </summary>
        public static IEnumerator EnsureAuthenticated()
        {
            // If already authenticated, return immediately
            if (_isAuthenticated)
            {
                Debug.Log("AuthenticationTestHelper: Already authenticated, skipping authentication");
                yield return null;
                yield break;
            }
            
            // If authentication is in progress, wait for it to complete
            if (_authenticationInProgress)
            {
                Debug.Log("AuthenticationTestHelper: Authentication in progress, waiting...");
                yield return WaitForAuthenticationToComplete();
                yield break;
            }
            
            // Start authentication
            _authenticationInProgress = true;
            Debug.Log("AuthenticationTestHelper: Starting shared authentication...");
            
            try
            {
                // Set up test environment with auto-start DISABLED
                TestHelpers.SetupTestEnvironmentWithExistingConfig();
                
                // Enable test mode to hijack authentication UI
                EnableTestMode();
                
                // Register callback to know when authentication completes
                var authState = new AuthCallbackState();
                
                Abxr.OnAuthCompleted += (success, error) => {
                    authState.Completed = true;
                    authState.Success = success;
                    authState.Error = error;
                    Debug.Log($"AuthenticationTestHelper: Auth callback received - Success: {success}, Error: {error}");
                };
                
                // Manually trigger authentication
                Debug.Log("AuthenticationTestHelper: Manually triggering authentication...");
                yield return Authentication.Authenticate();
                
                // Wait for authentication to complete via callback
                yield return WaitForAuthCallback(authState);
                
                if (authState.Success)
                {
                    _isAuthenticated = true;
                    Debug.Log("AuthenticationTestHelper: Shared authentication completed successfully");
                    Debug.Log($"AuthenticationTestHelper: ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}");
                }
                else
                {
                    Debug.LogError($"AuthenticationTestHelper: Shared authentication failed - {authState.Error}");
                    Assert.Fail($"Shared authentication failed - {authState.Error}");
                }
            }
            finally
            {
                _authenticationInProgress = false;
            }
        }
        
        /// <summary>
        /// Checks if the shared authentication session is active
        /// </summary>
        public static bool IsAuthenticated()
        {
            return _isAuthenticated && (Abxr.ConnectionActive() || Authentication.Authenticated());
        }
        
        /// <summary>
        /// Resets the shared authentication state (for cleanup between test runs)
        /// </summary>
        public static void ResetAuthenticationState()
        {
            Debug.Log("AuthenticationTestHelper: Resetting shared authentication state");
            _isAuthenticated = false;
            _authenticationInProgress = false;
        }
        
        /// <summary>
        /// Gets authentication status information for debugging
        /// </summary>
        public static string GetAuthenticationStatus()
        {
            return $"SharedAuth: {_isAuthenticated}, ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}";
        }
        
        #endregion
        
        #region Test Mode Authentication
        
        /// <summary>
        /// Enable test mode for authentication
        /// </summary>
        public static void EnableTestMode()
        {
            _isTestMode = true;
            Debug.Log("AuthenticationTestHelper: Test mode ENABLED - authentication will use programmatic responses");
            
            // Register this provider with the main ABXRLib
            TestAuthenticationRegistry.RegisterProvider(new TestAuthProviderImpl());
        }
        
        /// <summary>
        /// Disable test mode for authentication
        /// </summary>
        public static void DisableTestMode()
        {
            _isTestMode = false;
            Debug.Log("AuthenticationTestHelper: Test mode disabled - authentication will use normal UI flow");
            
            // Unregister the provider from the main ABXRLib
            TestAuthenticationRegistry.UnregisterProvider();
        }
        
        /// <summary>
        /// Check if we're in test mode
        /// </summary>
        public static bool IsTestMode => _isTestMode;
        
        /// <summary>
        /// Set default responses for different authentication mechanisms
        /// </summary>
        public static void SetDefaultResponses(string pin = null, string email = null, string text = null)
        {
            if (pin != null) _defaultPin = pin;
            if (email != null) _defaultEmail = email;
            if (text != null) _defaultText = text;
            
            Debug.Log($"AuthenticationTestHelper: Default responses set - PIN: {_defaultPin}, Email: {_defaultEmail}, Text: {_defaultText}");
        }
        
        /// <summary>
        /// Get the appropriate test response for the given authentication mechanism type
        /// </summary>
        public static string GetTestResponse(string authType, string domain = null)
        {
            if (!_isTestMode)
            {
                Debug.LogWarning("AuthenticationTestHelper: GetTestResponse called but not in test mode");
                return null;
            }
            
            return authType switch
            {
                "assessmentPin" => _defaultPin,
                "email" => domain != null ? $"{_defaultEmail}@{domain}" : _defaultEmail,
                "text" => _defaultText,
                "keyboard" => _defaultText,
                _ => _defaultText // Default fallback
            };
        }
        
        /// <summary>
        /// Hijack the PresentKeyboard call and provide a test response instead
        /// </summary>
        public static IEnumerator HandleTestAuthentication(string promptText, string keyboardType, string emailDomain)
        {
            if (!_isTestMode)
            {
                Debug.LogWarning("AuthenticationTestHelper: HandleTestAuthentication called but not in test mode");
                yield break;
            }
            
            Debug.Log("AuthenticationTestHelper: HIJACKING authentication!");
            Debug.Log($"AuthenticationTestHelper: Server requested AuthMechanism type: '{keyboardType}'");
            Debug.Log($"AuthenticationTestHelper: Server prompt: '{promptText}'");
            Debug.Log($"AuthenticationTestHelper: Email domain: '{emailDomain}'");
            
            // Get the test response based on the authentication mechanism type
            string testResponse = GetTestResponse(keyboardType, emailDomain);
            
            if (string.IsNullOrEmpty(testResponse))
            {
                Debug.LogError($"AuthenticationTestHelper: No test response available for auth type: {keyboardType}");
                yield break;
            }
            
            Debug.Log($"AuthenticationTestHelper: Providing test response: '{testResponse}' for auth type: '{keyboardType}'");
            
            // Simulate a brief delay to mimic user input time
            yield return new WaitForSeconds(0.1f);
            
            // Call KeyboardAuthenticate with the test response - this will make the actual server request
            yield return Authentication.KeyboardAuthenticate(testResponse);
            
            Debug.Log($"AuthenticationTestHelper: Authentication attempt completed with response: '{testResponse}'");
        }
        
        #endregion
        
        #region Authentication Handoff
        
        /// <summary>
        /// Simulates a launcher app that authenticates against the real server and creates handoff data
        /// </summary>
        public static IEnumerator SimulateLauncherAppHandoff()
        {
            Debug.Log("AuthenticationTestHelper: Starting real launcher app authentication...");
            
            // Clear any existing authentication state
            Authentication.ClearTestHandoffData();
            
            // Authenticate against the real server
            yield return Authentication.Authenticate();
            
            // Verify authentication was successful
            if (!Authentication.Authenticated())
            {
                Debug.LogError("AuthenticationTestHelper: Launcher app authentication failed");
                yield return null;
                yield break;
            }
            
            // Get the real auth response from the authenticated session
            var authResponse = Authentication.GetAuthResponse();
            if (authResponse == null)
            {
                Debug.LogError("AuthenticationTestHelper: No auth response available after authentication");
                yield return null;
                yield break;
            }
            
            // Verify we have the required fields for handoff
            if (string.IsNullOrEmpty(authResponse.PackageName))
            {
                Debug.LogError("AuthenticationTestHelper: No PackageName in auth response - cannot perform handoff");
                yield return null;
                yield break;
            }
            
            // Serialize the real auth response for handoff
            string handoffJson = JsonConvert.SerializeObject(authResponse);
            
            Debug.Log($"AuthenticationTestHelper: Real launcher app authentication successful");
            Debug.Log($"AuthenticationTestHelper: PackageName: {authResponse.PackageName}");
            Debug.Log($"AuthenticationTestHelper: AppId: {authResponse.AppId}");
            Debug.Log($"AuthenticationTestHelper: Handoff JSON: {handoffJson}");
            
            yield return handoffJson;
        }
        
        /// <summary>
        /// Validates that handoff data was processed correctly
        /// </summary>
        public static bool ValidateHandoffProcessing(Authentication.AuthResponse receivedAuthData, string expectedPackageName)
        {
            if (receivedAuthData == null)
            {
                Debug.LogError("AuthenticationTestHelper: Received auth data is null");
                return false;
            }

            if (string.IsNullOrEmpty(receivedAuthData.Token))
            {
                Debug.LogError("AuthenticationTestHelper: Received auth data missing token");
                return false;
            }

            if (string.IsNullOrEmpty(receivedAuthData.Secret))
            {
                Debug.LogError("AuthenticationTestHelper: Received auth data missing secret");
                return false;
            }

            if (receivedAuthData.PackageName != expectedPackageName)
            {
                Debug.LogError($"AuthenticationTestHelper: Package name mismatch. Expected: {expectedPackageName}, Got: {receivedAuthData.PackageName}");
                return false;
            }

            Debug.Log("AuthenticationTestHelper: Handoff data validation successful");
            return true;
        }
        
        #endregion
        
        #region Authentication Assertions and Utilities
        
        /// <summary>
        /// Waits for authentication to complete with timeout
        /// </summary>
        public static IEnumerator WaitForAuthenticationToComplete(float timeoutSeconds = 30f)
        {
            float elapsed = 0f;
            
            while (elapsed < timeoutSeconds)
            {
                // Check both connection status and authentication status
                bool authCompleted = Abxr.ConnectionActive() || Authentication.Authenticated();
                
                if (authCompleted)
                {
                    Debug.Log("AuthenticationTestHelper: Authentication completed successfully");
                    yield break;
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            Debug.LogError($"AuthenticationTestHelper: Authentication timed out after {timeoutSeconds} seconds");
            Assert.Fail($"Authentication timed out after {timeoutSeconds} seconds");
        }
        
        /// <summary>
        /// Asserts that authentication was successful
        /// </summary>
        public static void AssertAuthenticationSuccessful()
        {
            bool isConnected = Abxr.ConnectionActive() || Authentication.Authenticated();
            Assert.IsTrue(isConnected, "Authentication should be successful");
            Debug.Log("AuthenticationTestHelper: Authentication successful - verified");
        }
        
        /// <summary>
        /// Asserts that authentication failed
        /// </summary>
        public static void AssertAuthenticationFailed()
        {
            bool isConnected = Abxr.ConnectionActive() || Authentication.Authenticated();
            Assert.IsFalse(isConnected, "Authentication should have failed");
            Debug.Log("AuthenticationTestHelper: Authentication failed - verified");
        }
        
        /// <summary>
        /// Verifies that the configuration is valid for real server testing
        /// </summary>
        public static void AssertConfigurationValid()
        {
            Assert.IsNotNull(Configuration.Instance, "Configuration.Instance should not be null");
            Assert.IsTrue(Configuration.Instance.IsValid(), "Configuration should be valid");
            Assert.IsFalse(string.IsNullOrEmpty(Configuration.Instance.appID), "appID should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(Configuration.Instance.orgID), "orgID should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(Configuration.Instance.authSecret), "authSecret should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(Configuration.Instance.restUrl), "restUrl should not be empty");
            
            Debug.Log("AuthenticationTestHelper: Configuration is valid for real server testing");
        }
        
        /// <summary>
        /// Logs current authentication and configuration status for debugging
        /// </summary>
        public static void LogAuthenticationStatus()
        {
            Debug.Log($"AuthenticationTestHelper: Status - {GetAuthenticationStatus()}");
            
            if (Configuration.Instance != null)
            {
                Debug.Log($"AuthenticationTestHelper: Config - appID: {Configuration.Instance.appID}, " +
                         $"orgID: {Configuration.Instance.orgID}, restUrl: {Configuration.Instance.restUrl}");
            }
            else
            {
                Debug.LogError("AuthenticationTestHelper: Configuration.Instance is null");
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        /// <summary>
        /// Waits for authentication callback to complete with timeout
        /// </summary>
        private static IEnumerator WaitForAuthCallback(AuthCallbackState authState)
        {
            float timeout = 30f; // 30 second timeout
            float elapsed = 0f;
            
            while (!authState.Completed && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (!authState.Completed)
            {
                Debug.LogError($"AuthenticationTestHelper: Authentication callback timed out after {timeout} seconds");
                Assert.Fail($"Authentication callback timed out after {timeout} seconds");
            }
            else if (!authState.Success)
            {
                Debug.LogError($"AuthenticationTestHelper: Authentication failed - {authState.Error}");
                Assert.Fail($"Authentication failed - {authState.Error}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Helper class to hold authentication callback state by reference
    /// This fixes the closure variable issue where local variables are captured by value
    /// </summary>
    public class AuthCallbackState
    {
        public bool Completed { get; set; } = false;
        public bool Success { get; set; } = false;
        public string Error { get; set; } = null;
    }
    
    /// <summary>
    /// Implementation of ITestAuthenticationProvider for the static AuthenticationTestHelper
    /// </summary>
    internal class TestAuthProviderImpl : ITestAuthenticationProvider
    {
        public bool IsTestMode => AuthenticationTestHelper.IsTestMode;
        
        public IEnumerator HandleTestAuthentication(string promptText, string keyboardType, string emailDomain)
        {
            return AuthenticationTestHelper.HandleTestAuthentication(promptText, keyboardType, emailDomain);
        }
    }
}

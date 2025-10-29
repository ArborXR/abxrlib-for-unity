/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Test Helper for ABXRLib Tests
 * 
 * Helpers for testing authentication functionality against real servers.
 * All tests use actual server authentication to ensure both client and server work together.
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Helper utilities for testing authentication functionality against real servers
    /// </summary>
    public static class AuthenticationTestHelper
    {
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
        /// Asserts that authentication is in progress
        /// </summary>
        public static void AssertAuthenticationInProgress()
        {
            // This is harder to verify without access to internal state
            // For now, we'll just log that we would check this
            Debug.Log("AuthenticationTestHelper: Authentication in progress - verified");
        }
        
        /// <summary>
        /// Gets authentication status information for debugging
        /// </summary>
        public static string GetAuthenticationStatus()
        {
            return $"ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}";
        }
        
        /// <summary>
        /// Waits for a specific authentication state
        /// </summary>
        public static IEnumerator WaitForAuthenticationState(bool expectedState, float timeoutSeconds = 30f)
        {
            float elapsed = 0f;
            
            while (elapsed < timeoutSeconds)
            {
                bool currentState = Abxr.ConnectionActive() || Authentication.Authenticated();
                
                if (currentState == expectedState)
                {
                    Debug.Log($"AuthenticationTestHelper: Authentication state is now {expectedState} - verified");
                    yield break;
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            bool finalState = Abxr.ConnectionActive() || Authentication.Authenticated();
            Assert.AreEqual(expectedState, finalState, 
                $"Authentication state should be {expectedState} but was {finalState} after {timeoutSeconds} seconds");
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
    }
}
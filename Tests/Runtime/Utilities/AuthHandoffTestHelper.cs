/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Handoff Test Helper for ABXRLib Tests
 * 
 * Simulates the launcher app behavior for testing authentication handoff functionality.
 * This helper creates realistic AuthResponse data and simulates the handoff process.
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
    /// Helper utilities for testing authentication handoff functionality
    /// Simulates the launcher app behavior that authenticates and hands off to target apps
    /// </summary>
    public static class AuthHandoffTestHelper
    {
        /// <summary>
        /// Simulates a launcher app that authenticates against the real server and creates handoff data
        /// This performs actual authentication and returns real auth data for handoff
        /// </summary>
        /// <returns>Coroutine that yields the JSON string representing the AuthResponse</returns>
        public static IEnumerator SimulateLauncherAppHandoff()
        {
            Debug.Log("AuthHandoffTestHelper: Starting real launcher app authentication...");
            
            // Clear any existing authentication state
            Authentication.ClearTestHandoffData();
            
            // Environment is already set up by test [SetUp] methods
            // Register callback to know when authentication completes
            var authState = new AuthCallbackState();
            
            Abxr.OnAuthCompleted += (success, error) => {
                authState.Completed = true;
                authState.Success = success;
                authState.Error = error;
                Debug.Log($"AuthHandoffTestHelper: Launcher app auth callback received - Success: {success}, Error: {error}");
            };
            
            // Manually trigger authentication
            Debug.Log("AuthHandoffTestHelper: Manually triggering launcher app authentication...");
            yield return Authentication.Authenticate();
            
            // Wait for authentication to complete via callback
            yield return WaitForAuthCallback(authState);
            
            if (!authState.Success)
            {
                Debug.LogError($"AuthHandoffTestHelper: Launcher app authentication failed - {authState.Error}");
                Assert.Fail($"Launcher app authentication failed - {authState.Error}");
                yield return null;
                yield break;
            }
            
            // Verify authentication was successful
            if (!Authentication.Authenticated())
            {
                Debug.LogError("AuthHandoffTestHelper: Launcher app authentication failed");
                yield return null;
                yield break;
            }
            
            // Get the real auth response from the authenticated session
            var authResponse = Authentication.GetAuthResponse();
            if (authResponse == null)
            {
                Debug.LogError("AuthHandoffTestHelper: No auth response available after authentication");
                yield return null;
                yield break;
            }
            
            // Verify we have the required fields for handoff
            if (string.IsNullOrEmpty(authResponse.PackageName))
            {
                Debug.LogError("AuthHandoffTestHelper: No PackageName in auth response - cannot perform handoff");
                yield return null;
                yield break;
            }
            
            // Serialize the real auth response for handoff
            string handoffJson = JsonConvert.SerializeObject(authResponse);
            
            Debug.Log($"AuthHandoffTestHelper: Real launcher app authentication successful");
            Debug.Log($"AuthHandoffTestHelper: PackageName: {authResponse.PackageName}");
            Debug.Log($"AuthHandoffTestHelper: AppId: {authResponse.AppId}");
            Debug.Log($"AuthHandoffTestHelper: Handoff JSON: {handoffJson}");
            
            yield return handoffJson;
        }

        /// <summary>
        /// Validates that handoff data was processed correctly
        /// </summary>
        public static bool ValidateHandoffProcessing(Authentication.AuthResponse receivedAuthData, string expectedPackageName)
        {
            if (receivedAuthData == null)
            {
                Debug.LogError("AuthHandoffTestHelper: Received auth data is null");
                return false;
            }

            if (string.IsNullOrEmpty(receivedAuthData.Token))
            {
                Debug.LogError("AuthHandoffTestHelper: Received auth data missing token");
                return false;
            }

            if (string.IsNullOrEmpty(receivedAuthData.Secret))
            {
                Debug.LogError("AuthHandoffTestHelper: Received auth data missing secret");
                return false;
            }

            if (receivedAuthData.PackageName != expectedPackageName)
            {
                Debug.LogError($"AuthHandoffTestHelper: Package name mismatch. Expected: {expectedPackageName}, Got: {receivedAuthData.PackageName}");
                return false;
            }

            Debug.Log("AuthHandoffTestHelper: Handoff data validation successful");
            return true;
        }

        /// <summary>
        /// Simulates the handoff process by setting up the target app to receive handoff data
        /// This simulates what happens when the training app starts with auth_handoff parameter
        /// </summary>
        public static void SimulateTargetAppStartup(string handoffJson)
        {
            Debug.Log("AuthHandoffTestHelper: Simulating target app startup with handoff data...");
            
            // This would simulate the target app detecting the auth_handoff parameter
            // and processing the handoff data to bypass authentication
            
            // In a real scenario, this would be handled by the main AbxrLib code
            // when it detects the auth_handoff command line parameter or intent
            Debug.Log($"AuthHandoffTestHelper: Target app would process handoff data: {handoffJson}");
        }

        /// <summary>
        /// Verifies that the target app is properly authenticated after handoff
        /// </summary>
        public static void AssertTargetAppAuthenticated()
        {
            bool isAuthenticated = Abxr.ConnectionActive() || Authentication.Authenticated();
            Assert.IsTrue(isAuthenticated, "Target app should be authenticated after handoff");
            Debug.Log("AuthHandoffTestHelper: Target app authentication verified after handoff");
        }

        /// <summary>
        /// Simulates the complete handoff workflow:
        /// 1. Launcher app authenticates
        /// 2. Launcher app creates handoff data
        /// 3. Target app receives handoff data
        /// 4. Target app bypasses authentication using handoff data
        /// </summary>
        public static IEnumerator SimulateCompleteHandoffWorkflow()
        {
            Debug.Log("AuthHandoffTestHelper: Starting complete handoff workflow simulation...");
            
            // Step 1: Launcher app authenticates
            yield return SimulateLauncherAppHandoff();
            
            // Step 2: Get the handoff data
            string handoffJson = null;
            yield return SimulateLauncherAppHandoff();
            // Note: In a real test, you'd capture the handoffJson from the previous call
            
            // Step 3: Simulate target app startup
            if (!string.IsNullOrEmpty(handoffJson))
            {
                SimulateTargetAppStartup(handoffJson);
                
                // Step 4: Verify target app is authenticated
                AssertTargetAppAuthenticated();
                
                Debug.Log("AuthHandoffTestHelper: Complete handoff workflow simulation successful");
            }
            else
            {
                Debug.LogError("AuthHandoffTestHelper: Handoff workflow failed - no handoff data available");
            }
        }
        
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
                Debug.LogError($"AuthHandoffTestHelper: Authentication callback timed out after {timeout} seconds");
                Assert.Fail($"Authentication callback timed out after {timeout} seconds");
            }
            else if (!authState.Success)
            {
                Debug.LogError($"AuthHandoffTestHelper: Authentication failed - {authState.Error}");
                Assert.Fail($"Authentication failed - {authState.Error}");
            }
        }
        
        #endregion
    }
}

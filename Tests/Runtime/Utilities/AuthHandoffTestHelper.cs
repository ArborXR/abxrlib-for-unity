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
            
            // Authenticate against the real server
            yield return Authentication.Authenticate();
            
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

    }
}

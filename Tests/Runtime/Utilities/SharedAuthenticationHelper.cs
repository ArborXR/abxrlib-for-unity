/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Shared Authentication Helper for ABXRLib Tests
 * 
 * Provides a shared authentication session that can be used across all test classes.
 * This ensures authentication happens once and all tests can share the authenticated session.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Shared authentication helper for the entire test suite
    /// Provides a single authenticated session that all tests can use
    /// </summary>
    public static class SharedAuthenticationHelper
    {
        private static bool _isAuthenticated = false;
        private static bool _authenticationInProgress = false;
        
        /// <summary>
        /// Ensures authentication is completed before running tests
        /// This method is safe to call multiple times - it will only authenticate once
        /// </summary>
        public static IEnumerator EnsureAuthenticated()
        {
            // If already authenticated, return immediately
            if (_isAuthenticated)
            {
                Debug.Log("SharedAuthenticationHelper: Already authenticated, skipping authentication");
                yield return null;
                yield break;
            }
            
            // If authentication is in progress, wait for it to complete
            if (_authenticationInProgress)
            {
                Debug.Log("SharedAuthenticationHelper: Authentication in progress, waiting...");
                yield return WaitForAuthenticationToComplete();
                yield break;
            }
            
            // Start authentication
            _authenticationInProgress = true;
            Debug.Log("SharedAuthenticationHelper: Starting shared authentication...");
            
            try
            {
                // Set up test environment with auto-start DISABLED
                TestHelpers.SetupTestEnvironmentWithExistingConfig();
                
                // Enable test mode to hijack authentication UI
                TestAuthenticationProvider.EnableTestMode();
                // Use default test responses (no need to override - defaults are already set)
                
                // Register callback to know when authentication completes
                bool authCompleted = false;
                bool authSuccess = false;
                string authError = null;
                
                Abxr.OnAuthCompleted += (success, error) => {
                    authCompleted = true;
                    authSuccess = success;
                    authError = error;
                    Debug.Log($"SharedAuthenticationHelper: Auth callback received - Success: {success}, Error: {error}");
                };
                
                // Manually trigger authentication
                Debug.Log("SharedAuthenticationHelper: Manually triggering authentication...");
                yield return Authentication.Authenticate();
                
                // Wait for authentication to complete via callback
                yield return WaitForAuthCallback(authCompleted, authSuccess, authError);
                
                if (authSuccess)
                {
                    _isAuthenticated = true;
                    Debug.Log("SharedAuthenticationHelper: Shared authentication completed successfully");
                    Debug.Log($"SharedAuthenticationHelper: ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}");
                }
                else
                {
                    Debug.LogError($"SharedAuthenticationHelper: Shared authentication failed - {authError}");
                    Assert.Fail($"Shared authentication failed - {authError}");
                }
            }
            finally
            {
                _authenticationInProgress = false;
            }
        }
        
        /// <summary>
        /// Waits for authentication to complete with timeout
        /// </summary>
        private static IEnumerator WaitForAuthenticationToComplete()
        {
            float timeout = 30f; // 30 second timeout
            float elapsed = 0f;
            
            while (!_isAuthenticated && elapsed < timeout)
            {
                // Check both connection status and authentication status
                bool authCompleted = Abxr.ConnectionActive() || Authentication.Authenticated();
                
                if (authCompleted)
                {
                    _isAuthenticated = true;
                    break;
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (!_isAuthenticated)
            {
                Debug.LogError($"SharedAuthenticationHelper: Authentication timed out after {timeout} seconds");
                Debug.LogError($"SharedAuthenticationHelper: Final status - ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}");
                Assert.Fail($"Shared authentication timed out after {timeout} seconds");
            }
        }
        
        /// <summary>
        /// Waits for authentication callback to complete with timeout
        /// </summary>
        private static IEnumerator WaitForAuthCallback(bool authCompleted, bool authSuccess, string authError)
        {
            float timeout = 30f; // 30 second timeout
            float elapsed = 0f;
            
            while (!authCompleted && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (!authCompleted)
            {
                Debug.LogError($"SharedAuthenticationHelper: Authentication callback timed out after {timeout} seconds");
                Assert.Fail($"Authentication callback timed out after {timeout} seconds");
            }
            else if (!authSuccess)
            {
                Debug.LogError($"SharedAuthenticationHelper: Authentication failed - {authError}");
                Assert.Fail($"Authentication failed - {authError}");
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
            Debug.Log("SharedAuthenticationHelper: Resetting shared authentication state");
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
    }
}

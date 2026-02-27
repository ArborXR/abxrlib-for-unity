/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Authentication Test Helper for ABXRLib Tests (TakeTwo design)
 *
 * Waits for subsystem authentication to complete (auth is started by Initialize before scene load).
 * No test-mode or programmatic PIN injection; tests require valid Configuration and successful auth.
 */

using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Helper to ensure authentication is complete before running post-auth tests.
    /// TakeTwo: auth runs automatically; we only wait for GetIsAuthenticated() or OnAuthCompleted.
    /// </summary>
    public static class AuthenticationTestHelper
    {
        private static bool _isAuthenticated;
        private static bool _authWaitInProgress;

        /// <summary>
        /// Ensures authentication is completed before running tests.
        /// Safe to call multiple times; returns immediately if already authenticated.
        /// </summary>
        public static IEnumerator EnsureAuthenticated()
        {
            if (_isAuthenticated && Abxr.GetIsAuthenticated())
            {
                Debug.Log("AuthenticationTestHelper: Already authenticated");
                yield return null;
                yield break;
            }
            if (_authWaitInProgress)
            {
                yield return WaitForAuthenticationToComplete();
                yield break;
            }

            _authWaitInProgress = true;
            Debug.Log("AuthenticationTestHelper: Waiting for authentication...");

            var authState = new AuthCallbackState();
            Abxr.OnAuthCompleted += OnAuthCompleted;

            void OnAuthCompleted(bool success, string error)
            {
                authState.Completed = true;
                authState.Success = success;
                authState.Error = error;
                Abxr.OnAuthCompleted -= OnAuthCompleted;
                Debug.Log($"AuthenticationTestHelper: OnAuthCompleted Success={success}, Error={error}");
            }

            yield return WaitForAuthCallback(authState);

            if (authState.Success)
            {
                _isAuthenticated = true;
                Debug.Log("AuthenticationTestHelper: Authentication completed successfully");
            }
            else
            {
                Debug.LogError($"AuthenticationTestHelper: Authentication failed - {authState.Error}");
                Assert.Fail($"Authentication failed - {authState.Error}");
            }

            _authWaitInProgress = false;
        }

        /// <summary>
        /// Whether the shared authentication session is active
        /// </summary>
        public static bool IsAuthenticated() =>
            _isAuthenticated && Abxr.GetIsAuthenticated();

        /// <summary>
        /// Resets shared auth state for cleanup between test runs
        /// </summary>
        public static void ResetAuthenticationState()
        {
            Debug.Log("AuthenticationTestHelper: Resetting authentication state");
            _isAuthenticated = false;
            _authWaitInProgress = false;
        }

        /// <summary>
        /// Status string for debugging
        /// </summary>
        public static string GetAuthenticationStatus() =>
            $"SharedAuth: {_isAuthenticated}, GetIsAuthenticated: {Abxr.GetIsAuthenticated()}";

        /// <summary>
        /// Waits for authentication to complete with timeout
        /// </summary>
        public static IEnumerator WaitForAuthenticationToComplete(float timeoutSeconds = 30f)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (Abxr.GetIsAuthenticated())
                {
                    _isAuthenticated = true;
                    Debug.Log("AuthenticationTestHelper: Authentication completed");
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            Assert.Fail($"Authentication timed out after {timeoutSeconds} seconds");
        }

        private static IEnumerator WaitForAuthCallback(AuthCallbackState authState)
        {
            const float timeout = 30f;
            float elapsed = 0f;
            while (!authState.Completed && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            if (!authState.Completed)
                Assert.Fail("Authentication callback timed out");
            if (!authState.Success)
                Assert.Fail($"Authentication failed - {authState.Error}");
        }
    }

    /// <summary>
    /// Holds auth callback state by reference for use in coroutines
    /// </summary>
    public class AuthCallbackState
    {
        public bool Completed { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}

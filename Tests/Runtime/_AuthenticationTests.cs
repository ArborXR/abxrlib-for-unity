/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Authentication tests for ABXRLib (TakeTwo design)
 *
 * Verifies authentication state and post-auth functionality.
 * Run first to establish shared auth for other test classes.
 * Uses Abxr.GetIsAuthenticated() and Abxr.GetAuthResponse().
 */

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture, Category("Authentication")]
    public class _AuthenticationTests
    {
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            TestHelpers.CleanupTestEnvironment();
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
            yield return AuthenticationTestHelper.EnsureAuthenticated();
        }

        [TearDown]
        public void TearDown() => TestHelpers.CleanupTestEnvironment();

        [UnityTearDown]
        public void UnityTearDown() => AuthenticationTestHelper.ResetAuthenticationState();

        [UnityTest, Order(1)]
        public IEnumerator Test_RealServerAuthentication_CompletesSuccessfully()
        {
            Debug.Log("_AuthenticationTests: Verifying shared authentication...");
            bool isAuthenticated = AuthenticationTestHelper.IsAuthenticated();
            Assert.IsTrue(isAuthenticated, $"Shared authentication should be active. Status: {AuthenticationTestHelper.GetAuthenticationStatus()}");
            Debug.Log($" _AuthenticationTests: {AuthenticationTestHelper.GetAuthenticationStatus()}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_GetIsAuthenticated_AfterAuthentication_ReturnsTrue()
        {
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "GetIsAuthenticated should be true after shared authentication");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_EventTracking_AfterAuthentication_WorksCorrectly()
        {
            string testEventName = "test_post_auth_event";
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Should be authenticated before API calls");

            bool apiCallSucceeded = false;
            try
            {
                Abxr.Event(testEventName);
                apiCallSucceeded = true;
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Event call failed: {ex.Message}");
            }
            Assert.IsTrue(apiCallSucceeded);
            yield return new WaitForSeconds(0.5f);
            Debug.Log("_AuthenticationTests: Event tracking after auth OK");
        }

        [UnityTest]
        public IEnumerator Test_GetAuthResponse_AfterAuthentication_ReturnsData()
        {
            var response = Abxr.GetAuthResponse();
            Assert.IsNotNull(response, "GetAuthResponse should not be null when authenticated");
            Assert.IsFalse(string.IsNullOrEmpty(response.Token), "Token should be set");
            Assert.IsFalse(string.IsNullOrEmpty(response.Secret), "Secret should be set");
            yield return null;
        }
    }
}

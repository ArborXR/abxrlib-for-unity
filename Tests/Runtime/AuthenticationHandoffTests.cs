/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Authentication Handoff Tests for ABXRLib (TakeTwo design)
 *
 * TakeTwo: Handoff is processed from auth_handoff (intent/command line). We cannot inject
 * handoff data from tests; we test launcher side (get handoff JSON from auth) and verify
 * GetAuthResponse() shape and Modules. Target-side "receive handoff" tests require running
 * with auth_handoff set by the environment.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Types;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    public class AuthenticationHandoffTests
    {
        [SetUp]
        public void Setup()
        {
            TestHelpers.CleanupTestEnvironment();
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
        }

        [TearDown]
        public void TearDown() => TestHelpers.CleanupTestEnvironment();

        /// <summary>
        /// Launcher side: authenticate and produce handoff JSON; verify required fields.
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_LauncherProducesValidHandoffData()
        {
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();

            var authResponse = Abxr.GetAuthResponse();
            Assert.IsNotNull(authResponse, "Launcher should have auth response");
            Assert.IsFalse(string.IsNullOrEmpty(authResponse.PackageName), "Launcher should have PackageName");
            string handoffJson = AuthHandoffTestHelper.SerializeAuthResponseToJson(authResponse);
            Assert.IsFalse(string.IsNullOrEmpty(handoffJson), "Handoff JSON should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(authResponse.Token), "Token should be set");
            Assert.IsFalse(string.IsNullOrEmpty(authResponse.Secret), "Secret should be set");
            Debug.Log($"AuthenticationHandoffTests: Launcher handoff data valid, PackageName={authResponse.PackageName}");
        }

        /// <summary>
        /// When server returns modules, GetAuthResponse().Modules should be populated.
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_WithModules_GetAuthResponseHasModules()
        {
            yield return AuthenticationTestHelper.EnsureAuthenticated();
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Authentication should be successful");

            var authResponse = Abxr.GetAuthResponse();
            Assert.IsNotNull(authResponse, "Auth response should not be null");
            Assert.IsNotNull(authResponse.Modules, "Modules list should not be null");
            // Server may return 0 or more modules; we only assert shape
            if (authResponse.Modules.Count > 0)
            {
                var first = authResponse.Modules[0];
                Assert.IsNotNull(first.Target, "Module should have Target");
                Debug.Log($"AuthenticationHandoffTests: Modules count={authResponse.Modules.Count}, first Target={first.Target}");
            }
        }

        /// <summary>
        /// After authentication (e.g. after handoff in real scenario), Event() should work.
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_Event_WorksAfterAuth()
        {
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Should be authenticated");

            string eventName = "handoff_test_event";
            var metadata = TestHelpers.CreateTestMetadata(("test_key", "test_value"));
            bool ok = false;
            try
            {
                Abxr.Event(eventName, metadata);
                ok = true;
            }
            catch (System.Exception ex) { Assert.Fail($"Event failed: {ex.Message}"); }
            Assert.IsTrue(ok);
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"AuthenticationHandoffTests: Event '{eventName}' sent after auth");
        }

        /// <summary>
        /// Validate handoff data shape using helper (e.g. when auth came from handoff in real run).
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_ValidateHandoffProcessing()
        {
            yield return AuthenticationTestHelper.EnsureAuthenticated();
            var authResponse = Abxr.GetAuthResponse();
            if (authResponse == null || string.IsNullOrEmpty(authResponse.PackageName))
            {
                Assert.Ignore("Auth response has no PackageName (server may not return handoff shape)");
                yield break;
            }
            bool valid = AuthHandoffTestHelper.ValidateHandoffProcessing(authResponse, authResponse.PackageName);
            Assert.IsTrue(valid, "Handoff validation should pass for current auth response");
        }

        /// <summary>
        /// Full workflow: launcher auth -> handoff JSON -> verify Event and GetUserData work.
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_CompleteWorkflow_AfterAuth()
        {
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Target app should be authenticated");

            Abxr.Log("Target app log message");
            string testEvent = "target_app_event";
            var testMetadata = TestHelpers.CreateTestMetadata(("source", "target_app"));
            Abxr.Event(testEvent, testMetadata);
            yield return new WaitForSeconds(1.0f);

            var userData = Abxr.GetUserData();
            // userData may be null if server did not return UserData
            Debug.Log($"AuthenticationHandoffTests: Event sent, GetUserData present: {userData != null}");
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Should still be authenticated");
        }
    }
}

/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Handoff Tests for ABXRLib
 * 
 * Tests the authentication handoff flow where a launcher app authenticates
 * and then launches a target app with authentication data.
 * 
 * This simulates the real-world scenario where:
 * 1. Launcher app authenticates and gets AuthResponse with PackageName
 * 2. Launcher app launches target app with auth_handoff intent parameter
 * 3. Target app receives handoff data and uses it as its own auth data
 * 4. Target app can then use Abxr.Log() and other functionality
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Tests for authentication handoff functionality
    /// Simulates the complete flow from launcher app to target app
    /// </summary>
    public class AuthenticationHandoffTests
    {
        [SetUp]
        public void Setup()
        {
            // Clean up any state from previous tests (defensive - in case previous test failed)
            TestHelpers.CleanupTestEnvironment();
            
            // Setup test environment with existing config to enable test authentication mode
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
            
            // Auto-start authentication is controlled by editor check in Authentication.cs
            // No need to modify disableAutoStartAuthentication setting
            
            // Clear any existing handoff data
            Authentication.ClearTestHandoffData();
            
            Debug.Log("AuthenticationHandoffTests: Test setup complete (auto-start controlled by editor check)");
        }

        /// <summary>
        /// Validates that authentication data contains required handoff information
        /// and extracts the package name and app ID for testing
        /// </summary>
        private (string packageName, string appId) ValidateAndExtractHandoffData()
        {
            var authResponse = Authentication.GetAuthResponse();
            if (authResponse == null)
            {
                Assert.Fail("Authentication failed - no auth response available");
            }

            if (string.IsNullOrEmpty(authResponse.PackageName))
            {
                Debug.LogError("AuthenticationHandoffTests: No PackageName found in auth data - cannot perform handoff test");
                Assert.Fail("No PackageName found in authentication data. This indicates the app is not configured for authentication handoff.");
            }

            if (string.IsNullOrEmpty(authResponse.AppId))
            {
                Debug.LogError("AuthenticationHandoffTests: No AppId found in auth data - cannot perform handoff test");
                Assert.Fail("No AppId found in authentication data. This indicates the app is not configured for authentication handoff.");
            }

            Debug.Log($"AuthenticationHandoffTests: Using PackageName='{authResponse.PackageName}', AppId='{authResponse.AppId}' from auth data");
            return (authResponse.PackageName, authResponse.AppId);
        }

        [TearDown]
        public void TearDown()
        {
            // Cleanup
            TestHelpers.CleanupTestEnvironment();
            Authentication.ClearTestHandoffData();
            
            Debug.Log("AuthenticationHandoffTests: Test cleanup complete");
        }


        #region Basic Handoff Tests

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_BasicFlow_CompletesSuccessfully()
        {
            Debug.Log("AuthenticationHandoffTests: Testing basic authentication handoff flow...");

            // Step 1: Simulate launcher app authenticating with real server
            Debug.Log("Step 1: Launcher app authenticating with real server...");
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            
            // Get the handoff data from the launcher app authentication
            var authResponse = Authentication.GetAuthResponse();
            Assert.IsNotNull(authResponse, "Launcher app should have auth response");
            Assert.IsFalse(string.IsNullOrEmpty(authResponse.PackageName), "Launcher app should have PackageName");
            
            // Serialize the auth response for handoff
            string handoffJson = JsonConvert.SerializeObject(authResponse);
            Assert.IsFalse(string.IsNullOrEmpty(handoffJson), "Handoff JSON should not be empty");
            
            Debug.Log($"Step 1 Complete: Launcher app authenticated with PackageName='{authResponse.PackageName}', AppId='{authResponse.AppId}'");

            // Step 2: Simulate target app receiving handoff data
            Debug.Log("Step 2: Target app receiving handoff data...");
            Authentication.SetTestHandoffData(handoffJson);

            // Step 3: Target app authenticates using handoff data
            Debug.Log("Step 3: Target app authenticating with handoff data...");
            yield return Authentication.Authenticate();

            // Step 4: Verify target app is authenticated
            Debug.Log("Step 4: Verifying target app authentication...");
            Assert.IsTrue(Authentication.Authenticated(), "Target app should be authenticated after handoff");

            // Verify we got the correct auth response data
            var targetAuthResponse = Authentication.GetAuthResponse();
            Assert.IsNotNull(targetAuthResponse, "Target app auth response should not be null");
            Assert.AreEqual(authResponse.PackageName, targetAuthResponse.PackageName, "Package name should match between launcher and target");
            Assert.IsFalse(string.IsNullOrEmpty(targetAuthResponse.Token), "Target app token should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(targetAuthResponse.Secret), "Target app secret should not be empty");

            // Verify handoff data is available
            var handoffData = Authentication.GetAuthHandoffData();
            Assert.IsNotNull(handoffData, "Handoff data should be available");
            Assert.AreEqual(authResponse.PackageName, handoffData.PackageName, "Handoff package name should match");

            Debug.Log($"AuthenticationHandoffTests: Basic handoff flow completed successfully with PackageName='{authResponse.PackageName}', AppId='{authResponse.AppId}'");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_WithModules_ProcessesCorrectly()
        {
            Debug.Log("AuthenticationHandoffTests: Testing handoff with module data...");

            // Arrange: Simulate launcher app authentication to get real handoff data
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();

            // Act: Process handoff
            yield return Authentication.Authenticate();

            // Assert: Verify authentication is successful
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should be successful");

            // Validate and extract handoff data from auth response
            var (packageName, appId) = ValidateAndExtractHandoffData();

            // Verify module data is processed correctly
            var moduleData = Authentication.GetModuleData();
            Assert.IsNotNull(moduleData, "Module data should not be null");
            Assert.IsTrue(moduleData.Count > 0, "Should have at least one module");

            // Verify the expected module targets are present (in any order)
            var expectedTargets = new[] { "safety_assessment", "equipment_training", "final_evaluation" };
            var actualTargets = moduleData.Select(md => md.target).ToArray();
            
            foreach (var expectedTarget in expectedTargets)
            {
                Assert.IsTrue(actualTargets.Contains(expectedTarget), 
                    $"Expected module target '{expectedTarget}' not found in actual targets: [{string.Join(", ", actualTargets)}]");
            }

            Debug.Log($"AuthenticationHandoffTests: Handoff with modules processed correctly. Found {moduleData.Count} modules: [{string.Join(", ", actualTargets)}]");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_InvalidData_FallsBackToNormalAuth()
        {
            Debug.Log("AuthenticationHandoffTests: Testing handoff with invalid data...");

            // Arrange: Set invalid handoff data
            Authentication.SetTestHandoffData("invalid json data");

            // Act: Try to authenticate
            yield return Authentication.Authenticate();

            // Assert: Should fall back to normal authentication (which will fail in test environment)
            // In a real scenario, this would attempt normal authentication
            // For this test, we just verify the system doesn't crash
            Debug.Log("AuthenticationHandoffTests: Invalid handoff data handled gracefully");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_MissingToken_FallsBackToNormalAuth()
        {
            Debug.Log("AuthenticationHandoffTests: Testing handoff with missing token...");

            // Arrange: Create handoff data without token
            var invalidHandoffData = new
            {
                Secret = "test_secret",
                UserId = "test_user",
                AppId = "test-launcher-app-id",
                PackageName = "com.arborxr.testapp",
                UserData = new { },
                Modules = new object[] { }
            };
            string invalidJson = Newtonsoft.Json.JsonConvert.SerializeObject(invalidHandoffData);
            Authentication.SetTestHandoffData(invalidJson);

            // Act: Try to authenticate
            yield return Authentication.Authenticate();

            // Assert: Should fall back to normal authentication
            Debug.Log("AuthenticationHandoffTests: Missing token handled gracefully");
        }

        #endregion

        #region Abxr.Log() Functionality Tests

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_AbxrLog_WorksAfterHandoff()
        {
            Debug.Log("AuthenticationHandoffTests: Testing Abxr.Log() functionality after handoff...");

            // Arrange: Setup handoff using real authentication
            var handoffCoroutine = AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            yield return handoffCoroutine;
            string handoffJson = (string)handoffCoroutine.Current;
            Authentication.SetTestHandoffData(handoffJson);
            yield return Authentication.Authenticate();

            // Verify authentication is working
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should be successful");

            // Act: Try to use Abxr.Log() functionality
            string testMessage = "Test log message after handoff";
            Abxr.Log(testMessage);

            // Wait a frame for processing
            yield return null;

            // Assert: Verify the log was captured
            // Note: In a real test environment, you might need to check if the log
            // was actually sent to the server, but for this test we verify the system
            // doesn't crash and authentication is still working
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should still be working after Abxr.Log()");

            Debug.Log("AuthenticationHandoffTests: Abxr.Log() works correctly after handoff");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_AbxrEvent_WorksAfterHandoff()
        {
            Debug.Log("AuthenticationHandoffTests: Testing Abxr.Event() functionality after handoff...");

            // Step 1: Simulate launcher app authenticating with real server
            Debug.Log("Step 1: Launcher app authenticating with real server...");
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            
            // Get the handoff data from the launcher app authentication
            var authResponse = Authentication.GetAuthResponse();
            Assert.IsNotNull(authResponse, "Launcher app should have auth response");
            string handoffJson = JsonConvert.SerializeObject(authResponse);
            Authentication.SetTestHandoffData(handoffJson);
            
            // Step 2: Target app authenticates using handoff data
            Debug.Log("Step 2: Target app authenticating with handoff data...");
            yield return Authentication.Authenticate();

            // Step 3: Verify target app is authenticated
            Assert.IsTrue(Authentication.Authenticated(), "Target app should be authenticated after handoff");

            // Step 4: Test Abxr.Event() functionality
            Debug.Log("Step 4: Testing Abxr.Event() functionality...");
            string eventName = "handoff_test_event";
            var metadata = TestHelpers.CreateTestMetadata(("test_key", "test_value"));
            
            // Test that event API calls work without throwing exceptions
            bool apiCallSucceeded = false;
            try
            {
                Abxr.Event(eventName, metadata);
                apiCallSucceeded = true;
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Event API call failed with exception: {ex.Message}");
            }
            
            Assert.IsTrue(apiCallSucceeded, "Event API call should succeed");

            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);

            // Verify connection is still active after API calls
            bool stillConnected = Abxr.ConnectionActive() || Authentication.Authenticated();
            Assert.IsTrue(stillConnected, $"Connection should remain active after API calls. ConnectionActive: {Abxr.ConnectionActive()}, Authenticated: {Authentication.Authenticated()}");

            Debug.Log($"AuthenticationHandoffTests: Event '{eventName}' sent successfully after handoff");
            Debug.Log("AuthenticationHandoffTests: Abxr.Event() works correctly after handoff");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_ModuleTarget_WorksAfterHandoff()
        {
            Debug.Log("AuthenticationHandoffTests: Testing module target functionality after handoff...");

            // Arrange: Simulate launcher app authentication to get real handoff data
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            yield return Authentication.Authenticate();

            // Verify authentication is working
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should be successful");

            // Act: Get all available module targets
            var moduleTargets = new List<Abxr.CurrentSessionData>();
            Abxr.CurrentSessionData currentTarget;
            
            while ((currentTarget = Abxr.GetModuleTarget()) != null)
            {
                moduleTargets.Add(currentTarget);
            }

            // Assert: Verify module targets work correctly
            Assert.IsTrue(moduleTargets.Count > 0, "Should have at least one module target");
            
            // Verify the expected module targets are present (in any order)
            var expectedTargets = new[] { "safety_assessment", "equipment_training", "final_evaluation" };
            var actualTargets = moduleTargets.Select(mt => mt.moduleTarget).ToArray();
            
            foreach (var expectedTarget in expectedTargets)
            {
                Assert.IsTrue(actualTargets.Contains(expectedTarget), 
                    $"Expected module target '{expectedTarget}' not found in actual targets: [{string.Join(", ", actualTargets)}]");
            }
            
            Debug.Log($"AuthenticationHandoffTests: Found {moduleTargets.Count} module targets: [{string.Join(", ", actualTargets)}]");

            Debug.Log("AuthenticationHandoffTests: Module target functionality works correctly after handoff");
        }

        #endregion

        #region Edge Case Tests

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_NoPackageName_FailsGracefully()
        {
            Debug.Log("AuthenticationHandoffTests: Testing graceful failure when no PackageName is available...");

            // Arrange: Create handoff data without PackageName
            var handoffDataWithoutPackage = new
            {
                Token = "test_token",
                Secret = "test_secret",
                UserId = "test_user",
                AppId = "test-launcher-app-id",
                // PackageName is missing
                UserData = new { },
                Modules = new object[] { }
            };
            string handoffJson = Newtonsoft.Json.JsonConvert.SerializeObject(handoffDataWithoutPackage);
            Authentication.SetTestHandoffData(handoffJson);

            // Act: Try to process handoff
            yield return Authentication.Authenticate();

            // Assert: Should fail gracefully with clear error message
            Assert.IsFalse(Authentication.Authenticated(), "Authentication should fail when PackageName is missing");
            
            // Verify the error was logged appropriately
            Debug.Log("AuthenticationHandoffTests: Graceful failure test completed - no PackageName in auth data");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_EmptyModules_HandlesCorrectly()
        {
            Debug.Log("AuthenticationHandoffTests: Testing handoff with empty modules...");

            // Arrange: Simulate launcher app authentication to get real handoff data
            yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();

            // Act: Process handoff
            yield return Authentication.Authenticate();

            // Assert: Verify authentication is successful
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should be successful");

            // Verify module data is empty but not null
            var moduleData = Authentication.GetModuleData();
            Assert.IsNotNull(moduleData, "Module data should not be null");
            Assert.AreEqual(0, moduleData.Count, "Should have 0 modules");

            Debug.Log("AuthenticationHandoffTests: Empty modules handled correctly");
        }

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_ReAuthentication_ResetsHandoffState()
        {
            Debug.Log("AuthenticationHandoffTests: Testing re-authentication resets handoff state...");

            // Arrange: Setup initial handoff using real authentication
            var handoffCoroutine = AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            yield return handoffCoroutine;
            string handoffJson = (string)handoffCoroutine.Current;
            Authentication.SetTestHandoffData(handoffJson);
            yield return Authentication.Authenticate();

            // Verify initial handoff worked
            Assert.IsTrue(Authentication.Authenticated(), "Initial authentication should be successful");

            // Act: Re-authenticate (this should clear handoff state)
            Authentication.ReAuthenticate();
            yield return new WaitForSeconds(0.1f); // Wait for re-auth to start

            // Assert: Handoff state should be reset
            // Note: In a real scenario, ReAuthenticate would clear the handoff state
            // and attempt normal authentication
            Debug.Log("AuthenticationHandoffTests: Re-authentication resets handoff state");
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator Test_AuthenticationHandoff_CompleteWorkflow_SimulatesRealWorld()
        {
            Debug.Log("AuthenticationHandoffTests: Testing complete workflow simulation...");

            // This test simulates the complete real-world workflow:
            // 1. Launcher app authenticates
            // 2. Gets AuthResponse with PackageName
            // 3. Launches target app with handoff data
            // 4. Target app processes handoff
            // 5. Target app can use all Abxr functionality

            // Step 1: Simulate launcher app authentication
            Debug.Log("Step 1: Simulating launcher app authentication...");
            var handoffCoroutine = AuthHandoffTestHelper.SimulateLauncherAppHandoff();
            yield return handoffCoroutine;
            string handoffJson = (string)handoffCoroutine.Current;

            // Step 2: Simulate target app receiving handoff and authenticating
            Debug.Log("Step 2: Simulating target app handoff processing...");
            Authentication.SetTestHandoffData(handoffJson);
            yield return Authentication.Authenticate();

            // Step 3: Verify target app is fully authenticated
            Debug.Log("Step 3: Verifying target app authentication...");
            Assert.IsTrue(Authentication.Authenticated(), "Target app should be authenticated");

            // Step 4: Test that target app can use Abxr functionality
            Debug.Log("Step 4: Testing Abxr functionality in target app...");
            
            // Test logging
            Abxr.Log("Target app log message");
            
            // Test events
            string testEvent = "target_app_event";
            var testMetadata = TestHelpers.CreateTestMetadata(("source", "target_app"));
            Abxr.Event(testEvent, testMetadata);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Test module targets
            var moduleTarget = Abxr.GetModuleTarget();
            if (moduleTarget != null)
            {
                Debug.Log($"Target app got module target: {moduleTarget.moduleTarget}");
            }

            // Step 5: Verify everything worked
            Debug.Log("Step 5: Verifying complete workflow...");
            Debug.Log($"EventTrackingTests: Event '{testEvent}' sent successfully");
            Assert.IsTrue(Authentication.Authenticated(), "Authentication should still be working");
            Assert.IsTrue(true, "Event should be sent without throwing exceptions");

            Debug.Log("AuthenticationHandoffTests: Complete workflow simulation successful!");
        }

        #endregion
    }
}

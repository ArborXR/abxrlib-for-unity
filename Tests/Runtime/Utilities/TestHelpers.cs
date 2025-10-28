/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Test Helpers for ABXRLib Tests
 * 
 * Common setup/teardown, assertion helpers, and test data builders
 * for ABXRLib test suite.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Runtime.Core;
using AbxrLib.Tests.Runtime.TestDoubles;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Common test helpers and utilities for ABXRLib tests
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Sets up a clean test environment before each test
        /// </summary>
        public static void SetupTestEnvironment()
        {
            // Clear any existing super metadata
            Abxr.Reset();
            
            // Clear any existing timers
            ClearTimedEvents();
            
            // Enable auto-start authentication for real server testing
            if (Configuration.Instance != null)
            {
                Configuration.Instance.disableAutoStartAuthentication = false;
                Configuration.Instance.authenticationStartDelay = 0.1f; // Small delay for test stability
            }
            
            Debug.Log("TestHelpers: Test environment setup complete (real server authentication enabled)");
        }
        
        /// <summary>
        /// Sets up test environment with real server credentials for integration testing
        /// </summary>
        public static void SetupRealServerTestEnvironment(string appID, string orgID, string authSecret, string restUrl)
        {
            // Clear any existing state
            CleanupTestEnvironment();
            
            // Configure the real Configuration instance with provided credentials
            if (Configuration.Instance != null)
            {
                Configuration.Instance.appID = appID;
                Configuration.Instance.orgID = orgID;
                Configuration.Instance.authSecret = authSecret;
                Configuration.Instance.restUrl = restUrl;
                Configuration.Instance.disableAutoStartAuthentication = false;
                Configuration.Instance.authenticationStartDelay = 0.1f;
                
                Debug.Log($"TestHelpers: Real server test environment setup complete with appID: {appID}, orgID: {orgID}, restUrl: {restUrl}");
            }
            else
            {
                Debug.LogError("TestHelpers: Configuration.Instance is null - cannot set up real server credentials");
            }
        }
        
        /// <summary>
        /// Sets up test environment using existing configuration from the demo app
        /// </summary>
        public static void SetupTestEnvironmentWithExistingConfig()
        {
            // Clear any existing state
            CleanupTestEnvironment();
            
            // Enable test authentication mode
            Debug.Log("TestHelpers: Enabling test authentication mode...");
            TestAuthenticationProvider.EnableTestMode();
            
            // Set default test responses
            Debug.Log("TestHelpers: Setting default test responses...");
            TestAuthenticationProvider.SetDefaultResponses(
                pin: "999999",
                email: "testuser", 
                text: "EmpID1234"
            );
            
            // Use the existing configuration from the demo app
            if (Configuration.Instance != null)
            {
                // Enable auto-start authentication for tests with test mode
                Configuration.Instance.disableAutoStartAuthentication = false;
                Configuration.Instance.authenticationStartDelay = 0.1f; // Small delay for test stability
                
                Debug.Log($"TestHelpers: Using existing configuration - appID: {Configuration.Instance.appID}, orgID: {Configuration.Instance.orgID}, restUrl: {Configuration.Instance.restUrl}");
                Debug.Log("TestHelpers: Test authentication mode enabled - will use programmatic responses");
                
                // Validate that the configuration has the required fields
                if (!Configuration.Instance.IsValid())
                {
                    Debug.LogError("TestHelpers: Existing configuration is invalid - check your AbxrLib.asset file");
                }
            }
            else
            {
                Debug.LogError("TestHelpers: Configuration.Instance is null - make sure AbxrLib.asset exists in Resources folder");
            }
        }
        
        /// <summary>
        /// Cleans up test environment after each test
        /// </summary>
        public static void CleanupTestEnvironment()
        {
            // Clear super metadata
            Abxr.Reset();
            
            // Clear timers
            ClearTimedEvents();
            
            // Disable test authentication mode
            TestAuthenticationProvider.DisableTestMode();
            
            Debug.Log("TestHelpers: Test environment cleanup complete");
        }
        
        /// <summary>
        /// Clears all timed events (helper for testing)
        /// </summary>
        private static void ClearTimedEvents()
        {
            // This would need to be implemented in the actual Abxr class
            // For now, we'll just log that we would clear timers
            Debug.Log("TestHelpers: Would clear timed events (not implemented in Abxr class)");
        }
        
        /// <summary>
        /// Creates test metadata for testing
        /// </summary>
        public static Dictionary<string, string> CreateTestMetadata(params (string key, string value)[] pairs)
        {
            var metadata = new Dictionary<string, string>();
            foreach (var (key, value) in pairs)
            {
                metadata[key] = value;
            }
            return metadata;
        }
        
        /// <summary>
        /// Creates test metadata using Abxr.Dict
        /// </summary>
        public static Abxr.Dict CreateTestDict(params (string key, string value)[] pairs)
        {
            var dict = new Abxr.Dict();
            foreach (var (key, value) in pairs)
            {
                dict[key] = value;
            }
            return dict;
        }
        
        /// <summary>
        /// Creates test metadata with common test values
        /// </summary>
        public static Dictionary<string, string> CreateCommonTestMetadata()
        {
            return CreateTestMetadata(
                ("test_id", "test_123"),
                ("test_type", "unit_test"),
                ("test_scenario", "basic"),
                ("timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
            );
        }
        
        /// <summary>
        /// Asserts that metadata contains expected key-value pairs
        /// </summary>
        public static void AssertMetadataContains(Dictionary<string, string> actual, Dictionary<string, string> expected)
        {
            Assert.IsNotNull(actual, "Actual metadata should not be null");
            Assert.IsNotNull(expected, "Expected metadata should not be null");
            
            foreach (var kvp in expected)
            {
                Assert.IsTrue(actual.ContainsKey(kvp.Key), 
                    $"Metadata should contain key '{kvp.Key}'");
                Assert.AreEqual(kvp.Value, actual[kvp.Key], 
                    $"Metadata value for key '{kvp.Key}' should match expected value");
            }
        }
        
        /// <summary>
        /// Asserts that metadata contains expected key-value pairs (allowing additional keys)
        /// </summary>
        public static void AssertMetadataContainsSubset(Dictionary<string, string> actual, Dictionary<string, string> expected)
        {
            Assert.IsNotNull(actual, "Actual metadata should not be null");
            Assert.IsNotNull(expected, "Expected metadata should not be null");
            
            foreach (var kvp in expected)
            {
                Assert.IsTrue(actual.ContainsKey(kvp.Key), 
                    $"Metadata should contain key '{kvp.Key}'");
                Assert.AreEqual(kvp.Value, actual[kvp.Key], 
                    $"Metadata value for key '{kvp.Key}' should match expected value");
            }
        }
        
        /// <summary>
        /// Asserts that a Vector3 position is approximately equal to expected
        /// </summary>
        public static void AssertVector3Approximately(Vector3 actual, Vector3 expected, float tolerance = 0.001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "X component should match");
            Assert.AreEqual(expected.y, actual.y, tolerance, "Y component should match");
            Assert.AreEqual(expected.z, actual.z, tolerance, "Z component should match");
        }
        
        /// <summary>
        /// Asserts that a duration is approximately equal to expected
        /// </summary>
        public static void AssertDurationApproximately(float? actual, float expected, float tolerance = 0.1f)
        {
            Assert.IsNotNull(actual, "Duration should not be null");
            Assert.AreEqual(expected, actual.Value, tolerance, "Duration should match expected value");
        }
        
        /// <summary>
        /// Waits for a condition to be true with timeout
        /// </summary>
        public static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds = 5.0f)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (elapsed >= timeoutSeconds)
            {
                Assert.Fail($"Condition not met within {timeoutSeconds} seconds");
            }
        }
        
        /// <summary>
        /// Waits for a specific number of events to be captured
        /// </summary>
        public static IEnumerator WaitForEventCount(TestDataCapture capture, int expectedCount, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => capture.EventCount >= expectedCount, timeoutSeconds);
            Assert.AreEqual(expectedCount, capture.EventCount, "Expected number of events should be captured");
        }
        
        /// <summary>
        /// Waits for a specific event to be captured
        /// </summary>
        public static IEnumerator WaitForEvent(TestDataCapture capture, string eventName, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => capture.WasEventCaptured(eventName), timeoutSeconds);
            Assert.IsTrue(capture.WasEventCaptured(eventName), $"Event '{eventName}' should be captured");
        }
        
        /// <summary>
        /// Waits for a specific log to be captured
        /// </summary>
        public static IEnumerator WaitForLog(TestDataCapture capture, string logLevel, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => capture.WasLogCaptured(logLevel), timeoutSeconds);
            Assert.IsTrue(capture.WasLogCaptured(logLevel), $"Log with level '{logLevel}' should be captured");
        }
        
        /// <summary>
        /// Waits for a specific telemetry to be captured
        /// </summary>
        public static IEnumerator WaitForTelemetry(TestDataCapture capture, string telemetryName, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => capture.WasTelemetryCaptured(telemetryName), timeoutSeconds);
            Assert.IsTrue(capture.WasTelemetryCaptured(telemetryName), $"Telemetry '{telemetryName}' should be captured");
        }
        
        /// <summary>
        /// Creates a test GameObject for testing
        /// </summary>
        public static GameObject CreateTestGameObject(string name = "TestObject")
        {
            var go = new GameObject(name);
            return go;
        }
        
        /// <summary>
        /// Destroys a test GameObject
        /// </summary>
        public static void DestroyTestGameObject(GameObject go)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        
        /// <summary>
        /// Creates test scene name for testing scene name auto-addition
        /// </summary>
        public static string GetTestSceneName()
        {
            return "TestScene_" + DateTime.Now.Ticks;
        }
        
        /// <summary>
        /// Asserts that an event was captured with correct properties
        /// </summary>
        public static void AssertEventCaptured(TestDataCapture capture, string eventName, 
            Dictionary<string, string> expectedMeta = null, Vector3? expectedPosition = null)
        {
            Assert.IsTrue(capture.WasEventCaptured(eventName), $"Event '{eventName}' should be captured");
            
            var capturedEvent = capture.GetLastEvent(eventName);
            Assert.IsNotNull(capturedEvent, $"Captured event '{eventName}' should not be null");
            
            if (expectedMeta != null)
            {
                AssertMetadataContainsSubset(capturedEvent.meta, expectedMeta);
            }
            
            if (expectedPosition.HasValue)
            {
                Assert.IsTrue(capturedEvent.position.HasValue, "Event should have position data");
                AssertVector3Approximately(capturedEvent.position.Value, expectedPosition.Value);
            }
        }
        
        /// <summary>
        /// Asserts that a log was captured with correct properties
        /// </summary>
        public static void AssertLogCaptured(TestDataCapture capture, string level, string expectedMessage = null)
        {
            Assert.IsTrue(capture.WasLogCaptured(level), $"Log with level '{level}' should be captured");
            
            var capturedLog = capture.GetLastLog(level);
            Assert.IsNotNull(capturedLog, $"Captured log with level '{level}' should not be null");
            
            if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.AreEqual(expectedMessage, capturedLog.message, "Log message should match expected");
            }
        }
        
        /// <summary>
        /// Asserts that telemetry was captured with correct properties
        /// </summary>
        public static void AssertTelemetryCaptured(TestDataCapture capture, string telemetryName, 
            Dictionary<string, string> expectedMeta = null)
        {
            Assert.IsTrue(capture.WasTelemetryCaptured(telemetryName), $"Telemetry '{telemetryName}' should be captured");
            
            var capturedTelemetry = capture.GetLastTelemetry(telemetryName);
            Assert.IsNotNull(capturedTelemetry, $"Captured telemetry '{telemetryName}' should not be null");
            
            if (expectedMeta != null)
            {
                AssertMetadataContainsSubset(capturedTelemetry.meta, expectedMeta);
            }
        }
    }
}

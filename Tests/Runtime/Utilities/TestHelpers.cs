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
using AbxrLib.Runtime.Authentication;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Common test helpers and utilities for ABXRLib tests
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Generates a randomized name with the given prefix for test isolation
        /// </summary>
        /// <param name="prefix">The prefix for the name (e.g., "assessment", "objective", "interaction")</param>
        /// <returns>A unique name with format: {prefix}_{8-char-guid}</returns>
        public static string GenerateRandomName(string prefix)
        {
            return $"{prefix}_{System.Guid.NewGuid().ToString("N")[..8]}";
        }
        
        /// <summary>
        /// Sets up test environment using existing configuration from the demo app
        /// Note: Cleanup should be done separately via CleanupTestEnvironment() in [UnitySetUp] or [TearDown]
        /// </summary>
        public static void SetupTestEnvironmentWithExistingConfig()
        {
            // Use the existing configuration from the demo app
            if (Configuration.Instance != null)
            {
                // Auto-start authentication is controlled by editor check in Authentication.cs
                // No need to modify disableAutoStartAuthentication setting
                Configuration.Instance.authenticationStartDelay = 0.0f; // No delay needed for manual auth
                
                Debug.Log($"TestHelpers: Using existing configuration - appID: {Configuration.Instance.appID}, orgID: {Configuration.Instance.orgID}, restUrl: {Configuration.Instance.restUrl}");
                
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
            
            // Clear authentication state (only works in testing mode)
            Authentication.TestingClearAuthenticationState();
            
            Debug.Log("TestHelpers: Test environment cleanup complete");
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
        
        
        
    }
}

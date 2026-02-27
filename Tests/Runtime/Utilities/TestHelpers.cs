/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Test Helpers for ABXRLib Tests (TakeTwo design)
 *
 * Common setup/teardown, assertion helpers, and test data builders
 * for AbxrLib test suite. Uses Abxr static API and Configuration only.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using AbxrLib.Runtime.Core;

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
        public static string GenerateRandomName(string prefix)
        {
            return $"{prefix}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        /// <summary>
        /// Sets up test environment using existing configuration (e.g. from demo app).
        /// Cleanup should be done via CleanupTestEnvironment() in TearDown.
        /// </summary>
        public static void SetupTestEnvironmentWithExistingConfig()
        {
            if (Configuration.Instance != null)
            {
                Configuration.Instance.authenticationStartDelay = 0f;
                Debug.Log($"TestHelpers: Using existing configuration - restUrl: {Configuration.Instance.restUrl}");
                if (!Configuration.Instance.IsValid())
                    Debug.LogError("TestHelpers: Existing configuration is invalid - check AbxrLib.asset");
            }
            else
                Debug.LogError("TestHelpers: Configuration.Instance is null - ensure AbxrLib.asset exists in Resources");
        }

        /// <summary>
        /// Cleans up test environment after each test (clears super metadata only).
        /// Auth state is managed by the subsystem; use StartNewSession() in tests if you need a full reset.
        /// </summary>
        public static void CleanupTestEnvironment()
        {
            Abxr.Reset();
            Debug.Log("TestHelpers: Test environment cleanup complete");
        }

        /// <summary>
        /// Creates test metadata for testing
        /// </summary>
        public static Dictionary<string, string> CreateTestMetadata(params (string key, string value)[] pairs)
        {
            var metadata = new Dictionary<string, string>();
            foreach (var (key, value) in pairs)
                metadata[key] = value;
            return metadata;
        }

        /// <summary>
        /// Creates test metadata using Abxr.Dict
        /// </summary>
        public static Abxr.Dict CreateTestDict(params (string key, string value)[] pairs)
        {
            var dict = new Abxr.Dict();
            foreach (var (key, value) in pairs)
                dict[key] = value;
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
                Assert.IsTrue(actual.ContainsKey(kvp.Key), $"Metadata should contain key '{kvp.Key}'");
                Assert.AreEqual(kvp.Value, actual[kvp.Key], $"Metadata value for key '{kvp.Key}' should match");
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
                Assert.Fail($"Condition not met within {timeoutSeconds} seconds");
        }
    }
}

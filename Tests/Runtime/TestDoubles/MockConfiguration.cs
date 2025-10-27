/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Mock Configuration for ABXRLib Tests
 * 
 * Provides test configuration that doesn't require ScriptableObject assets
 * and allows for easy configuration of test scenarios.
 */

using UnityEngine;

namespace AbxrLib.Tests.Runtime.TestDoubles
{
    /// <summary>
    /// Mock configuration for testing that doesn't require ScriptableObject assets
    /// </summary>
    public class MockConfiguration
    {
        // Application Identity
        public string appID { get; set; } = "test_app_id";
        public string orgID { get; set; } = "test_org_id";
        public string authSecret { get; set; } = "test_auth_secret";
        
        // Network Configuration
        public string restUrl { get; set; } = "https://test-api.example.com";
        public int sendRetriesOnFailure { get; set; } = 3;
        public float sendRetryIntervalSeconds { get; set; } = 1.0f;
        public float sendNextBatchWaitSeconds { get; set; } = 5.0f;
        public float requestTimeoutSeconds { get; set; } = 30.0f;
        
        // Data Configuration
        public int dataEntriesPerSendAttempt { get; set; } = 50;
        public int maximumCachedItems { get; set; } = 1000;
        public bool retainLocalAfterSent { get; set; } = false;
        
        // Telemetry Configuration
        public float frameRateCapturePeriod { get; set; } = 1.0f;
        public float telemetryCapturePeriod { get; set; } = 1.0f;
        public float positionCapturePeriod { get; set; } = 0.1f;
        
        // Authentication Configuration
        public bool disableAutoStartAuthentication { get; set; } = true; // Disable for tests
        public float authenticationStartDelay { get; set; } = 0.0f;
        
        // Storage Configuration
        public int storageEntriesPerSendAttempt { get; set; } = 25;
        public float pruneSentItemsOlderThan { get; set; } = 3600.0f; // 1 hour
        
        /// <summary>
        /// Creates a mock configuration with default test values
        /// </summary>
        public static MockConfiguration CreateDefault()
        {
            return new MockConfiguration();
        }
        
        /// <summary>
        /// Creates a mock configuration for authentication testing
        /// </summary>
        public static MockConfiguration CreateForAuthTesting()
        {
            return new MockConfiguration
            {
                appID = "auth_test_app",
                orgID = "auth_test_org",
                authSecret = "auth_test_secret",
                restUrl = "https://auth-test-api.example.com",
                disableAutoStartAuthentication = false, // Enable for auth tests
                authenticationStartDelay = 0.1f
            };
        }
        
        /// <summary>
        /// Creates a mock configuration for network error testing
        /// </summary>
        public static MockConfiguration CreateForNetworkTesting()
        {
            return new MockConfiguration
            {
                appID = "network_test_app",
                restUrl = "https://network-test-api.example.com",
                sendRetriesOnFailure = 2,
                sendRetryIntervalSeconds = 0.5f,
                requestTimeoutSeconds = 5.0f
            };
        }
        
        /// <summary>
        /// Creates a mock configuration for storage testing
        /// </summary>
        public static MockConfiguration CreateForStorageTesting()
        {
            return new MockConfiguration
            {
                appID = "storage_test_app",
                storageEntriesPerSendAttempt = 10,
                maximumCachedItems = 100,
                pruneSentItemsOlderThan = 60.0f // 1 minute for faster testing
            };
        }
        
        /// <summary>
        /// Validates the configuration (similar to Configuration.IsValid())
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(appID))
            {
                Debug.LogError("MockConfiguration: appID is required");
                return false;
            }
            
            if (string.IsNullOrEmpty(restUrl))
            {
                Debug.LogError("MockConfiguration: restUrl is required");
                return false;
            }
            
            if (sendRetriesOnFailure < 0 || sendRetriesOnFailure > 10)
            {
                Debug.LogError($"MockConfiguration: sendRetriesOnFailure must be between 0 and 10, got {sendRetriesOnFailure}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Resets configuration to default test values
        /// </summary>
        public void Reset()
        {
            appID = "test_app_id";
            orgID = "test_org_id";
            authSecret = "test_auth_secret";
            restUrl = "https://test-api.example.com";
            sendRetriesOnFailure = 3;
            sendRetryIntervalSeconds = 1.0f;
            sendNextBatchWaitSeconds = 5.0f;
            requestTimeoutSeconds = 30.0f;
            dataEntriesPerSendAttempt = 50;
            maximumCachedItems = 1000;
            retainLocalAfterSent = false;
            frameRateCapturePeriod = 1.0f;
            telemetryCapturePeriod = 1.0f;
            positionCapturePeriod = 0.1f;
            disableAutoStartAuthentication = true;
            authenticationStartDelay = 0.0f;
            storageEntriesPerSendAttempt = 25;
            pruneSentItemsOlderThan = 3600.0f;
        }
    }
}

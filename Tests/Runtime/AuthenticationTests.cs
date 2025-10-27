using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AbxrLib.Runtime.Core;
using AbxrLib.Tests.Runtime.TestDoubles;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Tests for ABXRLib authentication functionality
    /// 
    /// These tests verify that authentication works correctly with real servers,
    /// including connection status, authentication completion, and post-authentication functionality.
    /// </summary>
    public class AuthenticationTests
    {
        private TestDataCapture _dataCapture;
        
        [SetUp]
        public void Setup()
        {
            // Use existing configuration from the demo app
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
            
            _dataCapture = new TestDataCapture();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
            _dataCapture?.Clear();
        }
        
        #region Real Server Integration Tests
        
        [UnityTest]
        public IEnumerator Test_RealServerAuthentication_CompletesSuccessfully()
        {
            // This test verifies that authentication with the real server works
            // using test authentication provider to provide programmatic responses
            
            Debug.Log("AuthenticationTests: Waiting for real server authentication to complete...");
            
            // Wait for authentication to complete (with timeout)
            float timeout = 30f; // 30 second timeout
            float elapsed = 0f;
            
            while (!Abxr.ConnectionActive() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (Abxr.ConnectionActive())
            {
                Debug.Log("AuthenticationTests: Real server authentication completed successfully!");
                Assert.IsTrue(true, "Authentication should complete successfully");
            }
            else
            {
                Debug.LogError("AuthenticationTests: Real server authentication timed out or failed");
                Assert.Fail($"Authentication did not complete within {timeout} seconds. Check your credentials and server connectivity.");
            }
        }
        
        [UnityTest]
        public IEnumerator Test_Configuration_IsValidAndComplete()
        {
            // This test verifies that the configuration is properly set up
            // without requiring actual authentication
            
            Debug.Log("AuthenticationTests: Verifying configuration setup...");
            
            // Verify configuration is loaded and valid
            Assert.IsNotNull(Configuration.Instance, "Configuration.Instance should not be null");
            Assert.IsTrue(Configuration.Instance.IsValid(), "Configuration should be valid");
            
            // Verify required fields are set
            Assert.IsNotEmpty(Configuration.Instance.appID, "appID should be set");
            Assert.IsNotEmpty(Configuration.Instance.orgID, "orgID should be set");
            Assert.IsNotEmpty(Configuration.Instance.authSecret, "authSecret should be set");
            Assert.IsNotEmpty(Configuration.Instance.restUrl, "restUrl should be set");
            
            Debug.Log($"AuthenticationTests: Configuration verified - appID: {Configuration.Instance.appID}, orgID: {Configuration.Instance.orgID}, restUrl: {Configuration.Instance.restUrl}");
            
            yield return null;
        }
        
        #endregion
        
        #region Connection Status Tests
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_BeforeAuthentication_ReturnsFalse()
        {
            // Arrange - Start with no authentication
            TestHelpers.CleanupTestEnvironment();
            
            // Act & Assert
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active before authentication");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_AfterAuthentication_ReturnsTrue()
        {
            // Wait for authentication to complete first
            yield return Test_RealServerAuthentication_CompletesSuccessfully();
            
            // Verify connection is active after authentication
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after successful authentication");
        }
        
        #endregion
        
        #region Post-Authentication Functionality Tests
        
        [UnityTest]
        public IEnumerator Test_EventTracking_AfterAuthentication_WorksCorrectly()
        {
            // Wait for authentication to complete first
            yield return Test_RealServerAuthentication_CompletesSuccessfully();
            
            // Test that we can track events after authentication
            string testEventName = "test_post_auth_event";
            Abxr.Event(testEventName);
            
            // Wait a moment for the event to be processed
            yield return new WaitForSeconds(0.5f);
            
            // Verify the event was captured
            Assert.IsTrue(_dataCapture.WasEventCaptured(testEventName), "Event should be captured after authentication");
        }
        
        [UnityTest]
        public IEnumerator Test_AssessmentEvents_AfterAuthentication_WorksCorrectly()
        {
            // Wait for authentication to complete first
            yield return Test_RealServerAuthentication_CompletesSuccessfully();
            
            // Test assessment events after authentication
            string assessmentName = "test_post_auth_assessment";
            
            Abxr.EventAssessmentStart(assessmentName);
            yield return new WaitForSeconds(0.1f);
            Abxr.EventAssessmentComplete(assessmentName, 85, Abxr.EventStatus.Pass);
            
            // Wait for events to be processed
            yield return new WaitForSeconds(0.5f);
            
            // Verify assessment events were captured
            Assert.IsTrue(_dataCapture.WasEventCaptured(assessmentName), "Assessment events should be captured after authentication");
        }
        
        #endregion
        
        #region Session Management Tests
        
        [UnityTest]
        public IEnumerator Test_SessionManagement_AfterAuthentication_WorksCorrectly()
        {
            // Wait for authentication to complete first
            yield return Test_RealServerAuthentication_CompletesSuccessfully();
            
            // Test session-related functionality
            string deviceId = Abxr.GetDeviceId();
            Assert.IsNotEmpty(deviceId, "Device ID should be available after authentication");
            
            string orgId = Abxr.GetOrgId();
            Assert.IsNotEmpty(orgId, "Organization ID should be available after authentication");
            
            Debug.Log($"AuthenticationTests: Session management verified - DeviceId: {deviceId}, OrgId: {orgId}");
            
            yield return null;
        }
        
        #endregion
        
        #region Error Handling Tests
        
        [UnityTest]
        public IEnumerator Test_Authentication_WithInvalidConfiguration_FailsGracefully()
        {
            // This test verifies that the system handles configuration errors gracefully
            // We'll temporarily modify the configuration to test error handling
            
            // Store original configuration
            string originalAppId = Configuration.Instance.appID;
            string originalOrgId = Configuration.Instance.orgID;
            string originalAuthSecret = Configuration.Instance.authSecret;
            string originalRestUrl = Configuration.Instance.restUrl;
            
            try
            {
                // Temporarily set invalid configuration
                Configuration.Instance.appID = "";
                Configuration.Instance.orgID = "";
                Configuration.Instance.authSecret = "";
                Configuration.Instance.restUrl = "";
                
                // Verify configuration is now invalid
                Assert.IsFalse(Configuration.Instance.IsValid(), "Configuration should be invalid with empty fields");
                
                Debug.Log("AuthenticationTests: Invalid configuration test completed");
            }
            finally
            {
                // Restore original configuration
                Configuration.Instance.appID = originalAppId;
                Configuration.Instance.orgID = originalOrgId;
                Configuration.Instance.authSecret = originalAuthSecret;
                Configuration.Instance.restUrl = originalRestUrl;
                
                // Verify configuration is valid again
                Assert.IsTrue(Configuration.Instance.IsValid(), "Configuration should be valid after restoration");
            }
            
            yield return null;
        }
        
        #endregion
    }
}
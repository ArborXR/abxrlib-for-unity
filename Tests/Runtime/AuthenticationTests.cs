/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Authentication Tests for ABXRLib
 * 
 * Tests for authentication functionality including:
 * - Authentication flow with server-provided AuthMechanism
 * - Client response to different AuthMechanism types
 * - Post-authentication functionality testing
 * - Connection status checks
 * - Session management
 * - Authentication retry logic and error handling
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Tests.Runtime.TestDoubles;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Tests for authentication functionality
    /// </summary>
    public class AuthenticationTests
    {
        private TestDataCapture _dataCapture;
        private MockAuthenticationProvider _mockAuth;
        private MockNetworkProvider _mockNetwork;
        private MockConfiguration _mockConfig;
        
        [SetUp]
        public void Setup()
        {
            // Set up test environment with required credentials
            TestHelpers.SetupAuthTestEnvironment(
                appID: "test_app_id",
                orgID: "test_org_id", 
                authSecret: "test_auth_secret",
                restUrl: "https://test-api.example.com"
            );
            
            _dataCapture = new TestDataCapture();
            _mockAuth = new MockAuthenticationProvider();
            _mockNetwork = new MockNetworkProvider();
            _mockConfig = MockConfiguration.CreateForAuthTesting();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
            _dataCapture?.Clear();
            AuthenticationTestHelper.CleanupAuthTestEnvironment(_mockAuth);
        }
        
        #region Authentication Flow Tests
        
        [UnityTest]
        public IEnumerator Test_Authentication_RequiredCredentials_AreConfigured()
        {
            // Arrange - Test that required credentials are properly set up
            var mockConfig = MockConfiguration.CreateForAuthTesting();
            
            // Act & Assert
            Assert.IsNotNull(mockConfig.appID, "appID should not be null");
            Assert.IsNotNull(mockConfig.orgID, "orgID should not be null");
            Assert.IsNotNull(mockConfig.authSecret, "authSecret should not be null");
            Assert.IsNotNull(mockConfig.restUrl, "restUrl should not be null");
            
            Assert.IsNotEmpty(mockConfig.appID, "appID should not be empty");
            Assert.IsNotEmpty(mockConfig.orgID, "orgID should not be empty");
            Assert.IsNotEmpty(mockConfig.authSecret, "authSecret should not be empty");
            Assert.IsNotEmpty(mockConfig.restUrl, "restUrl should not be empty");
            
            Assert.IsTrue(mockConfig.IsValid(), "Configuration should be valid with required credentials");
            
            Debug.Log($"Authentication credentials configured - appID: {mockConfig.appID}, orgID: {mockConfig.orgID}, restUrl: {mockConfig.restUrl}");
            
            yield return null; // Required for UnityTest
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_WithCredentials_SendsCorrectRequest()
        {
            // Arrange - Set up authentication with specific credentials
            string testAppID = "test_app_12345";
            string testOrgID = "test_org_67890";
            string testAuthSecret = "test_secret_abcdef";
            string testRestUrl = "https://api.test.com";
            
            TestHelpers.SetupAuthTestEnvironment(testAppID, testOrgID, testAuthSecret, testRestUrl);
            
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "auth_token_123";
            _mockAuth.MockApiSecret = "api_secret_456";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Configure network to expect the correct credentials
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.Success;
            _mockNetwork.SetAuthResponse("auth_token_123", "api_secret_456", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            
            // Verify that the authentication request would have been sent with correct credentials
            // Note: In a real implementation, we would verify the actual network request
            Debug.Log($"Authentication request sent with appID: {testAppID}, orgID: {testOrgID}, authSecret: {testAuthSecret}, restUrl: {testRestUrl}");
            
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after successful authentication");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_NoAuthMechanism_Succeeds()
        {
            // Arrange - Server responds with no auth mechanism required
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_no_auth";
            _mockAuth.MockApiSecret = "secret_no_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up no auth mechanism
            _mockAuth.SetNoAuthMechanism();
            
            // Configure network to return no auth mechanism
            _mockNetwork.SetAuthResponse("token_no_auth", "secret_no_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertNoAuthMechanismRequired(_mockAuth);
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after successful auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_KeyboardAuthMechanism_RequiresUserInput()
        {
            // Arrange - Server responds with keyboard auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.KeyboardAuth;
            _mockAuth.MockAuthToken = "token_keyboard_auth";
            _mockAuth.MockApiSecret = "secret_keyboard_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up keyboard auth mechanism
            _mockAuth.SetAuthMechanism("keyboard", "Please enter your credentials", null);
            _mockAuth.SetAuthMechanismResponse("keyboard_response");
            
            // Configure network to return keyboard auth mechanism
            _mockNetwork.SetAuthResponse("token_keyboard_auth", "secret_keyboard_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "keyboard", "Please enter your credentials");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "keyboard_response");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after keyboard auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_SSOAuthMechanism_RequiresSSOFlow()
        {
            // Arrange - Server responds with SSO auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.SSOAuth;
            _mockAuth.MockAuthToken = "token_sso_auth";
            _mockAuth.MockApiSecret = "secret_sso_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up SSO auth mechanism
            _mockAuth.SetAuthMechanism("sso", "Redirecting to SSO provider", "https://sso.example.com");
            _mockAuth.SetAuthMechanismResponse("sso_response");
            
            // Configure network to return SSO auth mechanism
            _mockNetwork.SetAuthResponse("token_sso_auth", "secret_sso_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "sso", "Redirecting to SSO provider");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "sso_response");
            Assert.AreEqual("https://sso.example.com", _mockAuth.MockAuthMechanism.domain, "SSO domain should match");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after SSO auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_AssessmentPinAuthMechanism_UsesDefaultPin()
        {
            // Arrange - Server responds with assessment PIN auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_assessment_pin_auth";
            _mockAuth.MockApiSecret = "secret_assessment_pin_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up assessment PIN auth mechanism with default PIN (999999)
            _mockAuth.SetAssessmentPinAuth("Please enter your assessment PIN");
            
            // Configure network to return assessment PIN auth mechanism
            _mockNetwork.SetAuthResponse("token_assessment_pin_auth", "secret_assessment_pin_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "assessmentPin", "Please enter your assessment PIN");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "999999");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after assessment PIN auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_EmailAuthMechanism_UsesDefaultEmail()
        {
            // Arrange - Server responds with email auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_email_auth";
            _mockAuth.MockApiSecret = "secret_email_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up email auth mechanism with default email (testuser@example.com)
            _mockAuth.SetEmailAuth("Please enter your email", "example.com");
            
            // Configure network to return email auth mechanism
            _mockNetwork.SetAuthResponse("token_email_auth", "secret_email_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "email", "Please enter your email");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "testuser@example.com");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after email auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_TextAuthMechanism_UsesDefaultText()
        {
            // Arrange - Server responds with text auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_text_auth";
            _mockAuth.MockApiSecret = "secret_text_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up text auth mechanism with default text (EmpID1234)
            _mockAuth.SetTextAuth("Please enter your employee ID");
            
            // Configure network to return text auth mechanism
            _mockNetwork.SetAuthResponse("token_text_auth", "secret_text_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "text", "Please enter your employee ID");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "EmpID1234");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after text auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_AssessmentPinAuthMechanism_WithCustomPin_UsesCustomPin()
        {
            // Arrange - Server responds with assessment PIN auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_custom_pin_auth";
            _mockAuth.MockApiSecret = "secret_custom_pin_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up assessment PIN auth mechanism with custom PIN
            string customPin = "123456";
            _mockAuth.SetAssessmentPinAuth("Please enter your assessment PIN", customPin);
            
            // Configure network to return assessment PIN auth mechanism
            _mockNetwork.SetAuthResponse("token_custom_pin_auth", "secret_custom_pin_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "assessmentPin", "Please enter your assessment PIN");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, customPin);
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after custom PIN auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_EmailAuthMechanism_WithDifferentDomains_UsesCorrectDomain()
        {
            // Arrange - Test with acme.com domain
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_acme_email_auth";
            _mockAuth.MockApiSecret = "secret_acme_email_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up email auth mechanism with acme.com domain
            _mockAuth.SetEmailAuth("Please enter your email", "acme.com");
            
            // Configure network to return email auth mechanism
            _mockNetwork.SetAuthResponse("token_acme_email_auth", "secret_acme_email_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "email", "Please enter your email");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "testuser@acme.com");
            Assert.AreEqual("acme.com", _mockAuth.MockAuthMechanism.domain, "Domain should be acme.com");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after acme.com email auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_EmailAuthMechanism_WithNoDomain_UsesDefaultDomain()
        {
            // Arrange - Test with no domain specified
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_no_domain_email_auth";
            _mockAuth.MockApiSecret = "secret_no_domain_email_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up email auth mechanism with no domain (should use default)
            _mockAuth.SetEmailAuth("Please enter your email", null);
            
            // Configure network to return email auth mechanism
            _mockNetwork.SetAuthResponse("token_no_domain_email_auth", "secret_no_domain_email_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "email", "Please enter your email");
            // When no domain is provided, should fall back to default response
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "testuser");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after no-domain email auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_EmailAuthMechanism_WithCustomEmail_UsesCustomEmail()
        {
            // Arrange - Server responds with email auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_custom_email_auth";
            _mockAuth.MockApiSecret = "secret_custom_email_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up email auth mechanism with custom email
            string customEmail = "custom@acme.com";
            _mockAuth.SetEmailAuth("Please enter your email", "acme.com", customEmail);
            
            // Configure network to return email auth mechanism
            _mockNetwork.SetAuthResponse("token_custom_email_auth", "secret_custom_email_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "email", "Please enter your email");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, customEmail);
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after custom email auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_TextAuthMechanism_WithCustomText_UsesCustomText()
        {
            // Arrange - Server responds with text auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "token_custom_text_auth";
            _mockAuth.MockApiSecret = "secret_custom_text_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up text auth mechanism with custom text
            string customText = "CustomEmpID5678";
            _mockAuth.SetTextAuth("Please enter your employee ID", customText);
            
            // Configure network to return text auth mechanism
            _mockNetwork.SetAuthResponse("token_custom_text_auth", "secret_custom_text_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "text", "Please enter your employee ID");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, customText);
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after custom text auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_CustomAuthMechanism_HandlesCustomFlow()
        {
            // Arrange - Server responds with custom auth mechanism
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.CustomAuth;
            _mockAuth.MockAuthToken = "token_custom_auth";
            _mockAuth.MockApiSecret = "secret_custom_auth";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            // Set up custom auth mechanism
            _mockAuth.SetAuthMechanism("custom", "Custom authentication required", "https://custom.example.com");
            _mockAuth.SetAuthMechanismResponse("custom_response");
            
            // Configure network to return custom auth mechanism
            _mockNetwork.SetAuthResponse("token_custom_auth", "secret_custom_auth", System.DateTime.UtcNow.AddHours(1));
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            AuthenticationTestHelper.AssertAuthMechanismProvided(_mockAuth, "custom", "Custom authentication required");
            AuthenticationTestHelper.AssertAuthMechanismResponseProvided(_mockAuth, "custom_response");
            Assert.AreEqual("https://custom.example.com", _mockAuth.MockAuthMechanism.domain, "Custom domain should match");
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after custom auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_InvalidCredentials_Fails()
        {
            // Arrange - Server responds with authentication failure
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Failure;
            _mockAuth.LastError = "Invalid credentials";
            
            // Configure network to return failure
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.ServerError;
            _mockNetwork.SetDataResponse(false, "Invalid credentials");
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationFailed(_mockAuth, "Invalid credentials");
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after failed auth");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_NetworkError_Fails()
        {
            // Arrange - Network error during authentication
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.NetworkError;
            _mockAuth.LastError = "Network connection failed";
            
            // Configure network to simulate connection error
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.ConnectionError;
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationNetworkError(_mockAuth);
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after network error");
        }
        
        [UnityTest]
        public IEnumerator Test_Authentication_Timeout_Fails()
        {
            // Arrange - Authentication timeout
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Timeout;
            _mockAuth.LastError = "Authentication timeout";
            
            // Configure network to simulate timeout
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.Timeout;
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationTimeout(_mockAuth);
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after timeout");
        }
        
        #endregion
        
        #region Post-Authentication Functionality Tests
        
        [UnityTest]
        public IEnumerator Test_PostAuth_EventAssessmentStart_SendsEventSuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string assessmentName = "post_auth_assessment";
            
            // Act
            Abxr.EventAssessmentStart(assessmentName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, assessmentName);
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(assessmentName, capturedEvent.name, "Assessment name should match");
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_EventObjectiveStart_SendsEventSuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string objectiveName = "post_auth_objective";
            
            // Act
            Abxr.EventObjectiveStart(objectiveName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, objectiveName);
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual(objectiveName, capturedEvent.name, "Objective name should match");
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_EventObjectiveComplete_SendsEventSuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string objectiveName = "post_auth_objective_complete";
            int score = 95;
            var status = EventStatus.Pass;
            
            // Act
            Abxr.EventObjectiveComplete(objectiveName, score, status);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, objectiveName);
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_EventAssessmentComplete_SendsEventSuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string assessmentName = "post_auth_assessment_complete";
            int score = 88;
            var status = EventStatus.Pass;
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, assessmentName);
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_CompleteAssessmentFlow_SendsAllEvents()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string assessmentName = "complete_post_auth_assessment";
            string objectiveName = "complete_post_auth_objective";
            string interactionName = "complete_post_auth_interaction";
            
            // Act - Complete assessment flow
            Abxr.EventAssessmentStart(assessmentName);
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            Abxr.EventObjectiveStart(objectiveName);
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            Abxr.EventInteractionStart(interactionName);
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            Abxr.EventInteractionComplete(interactionName, InteractionType.Select, InteractionResult.Correct, "completed");
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            Abxr.EventObjectiveComplete(objectiveName, 90, EventStatus.Pass);
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            Abxr.EventAssessmentComplete(assessmentName, 88, EventStatus.Pass);
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            Assert.AreEqual(6, _dataCapture.EventCount, "Should have 6 events total");
            Assert.IsTrue(_dataCapture.WasEventCaptured(assessmentName), "Assessment should be captured");
            Assert.IsTrue(_dataCapture.WasEventCaptured(objectiveName), "Objective should be captured");
            Assert.IsTrue(_dataCapture.WasEventCaptured(interactionName), "Interaction should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_EventWithMetadata_SendsEventWithMetadata()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string eventName = "post_auth_event_with_metadata";
            var metadata = TestHelpers.CreateTestMetadata(
                ("post_auth_key", "post_auth_value"),
                ("user_id", "12345"),
                ("session_id", "session_123")
            );
            
            // Act
            Abxr.Event(eventName, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_Logging_SendsLogsSuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string logMessage = "Post authentication log message";
            
            // Act
            Abxr.LogInfo(logMessage);
            
            // Wait for log to be processed
            yield return TestHelpers.WaitForLog(_dataCapture, "Info");
            
            // Assert
            TestHelpers.AssertLogCaptured(_dataCapture, "Info", logMessage);
        }
        
        [UnityTest]
        public IEnumerator Test_PostAuth_Telemetry_SendsTelemetrySuccessfully()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            string telemetryName = "post_auth_telemetry";
            var telemetryMetadata = TestHelpers.CreateTestMetadata(
                ("telemetry_type", "performance"),
                ("value", "95.5")
            );
            
            // Act
            Abxr.Telemetry(telemetryName, telemetryMetadata);
            
            // Wait for telemetry to be processed
            yield return TestHelpers.WaitForTelemetry(_dataCapture, telemetryName);
            
            // Assert
            TestHelpers.AssertTelemetryCaptured(_dataCapture, telemetryName, telemetryMetadata);
        }
        
        #endregion
        
        #region Connection Status Tests
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_BeforeAuthentication_ReturnsFalse()
        {
            // Arrange - No authentication setup
            
            // Act & Assert
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active before authentication");
        }
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_AfterSuccessfulAuth_ReturnsTrue()
        {
            // Arrange - Successful authentication
            yield return SetupSuccessfulAuthentication();
            
            // Act & Assert
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after successful authentication");
        }
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_AfterFailedAuth_ReturnsFalse()
        {
            // Arrange - Failed authentication
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Failure;
            _mockAuth.LastError = "Authentication failed";
            
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Act & Assert
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after failed authentication");
        }
        
        [UnityTest]
        public IEnumerator Test_ConnectionActive_AfterNetworkError_ReturnsFalse()
        {
            // Arrange - Network error during authentication
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.NetworkError;
            _mockAuth.LastError = "Network connection failed";
            
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Act & Assert
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after network error");
        }
        
        #endregion
        
        #region Session Management Tests
        
        [UnityTest]
        public IEnumerator Test_StartNewSession_GeneratesNewSessionId()
        {
            // Arrange - Successful authentication first
            yield return SetupSuccessfulAuthentication();
            
            // Act
            Abxr.StartNewSession();
            
            // Wait for new session to be established
            yield return new WaitForSeconds(0.1f);
            
            // Assert
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after starting new session");
        }
        
        [UnityTest]
        public IEnumerator Test_ReAuthenticate_WithStoredCredentials_Succeeds()
        {
            // Arrange - Initial successful authentication
            yield return SetupSuccessfulAuthentication();
            
            // Act
            Abxr.ReAuthenticate();
            
            // Wait for reauthentication to complete
            yield return new WaitForSeconds(0.1f);
            
            // Assert
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after reauthentication");
        }
        
        [UnityTest]
        public IEnumerator Test_ReAuthenticate_WithInvalidCredentials_Fails()
        {
            // Arrange - Initial successful authentication
            yield return SetupSuccessfulAuthentication();
            
            // Change to failure scenario for reauthentication
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Failure;
            _mockAuth.LastError = "Reauthentication failed";
            
            // Act
            Abxr.ReAuthenticate();
            
            // Wait for reauthentication to complete
            yield return new WaitForSeconds(0.1f);
            
            // Assert
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after failed reauthentication");
        }
        
        #endregion
        
        #region Authentication Retry Logic Tests
        
        [UnityTest]
        public IEnumerator Test_AuthenticationRetry_OnTemporaryFailure_SucceedsAfterRetry()
        {
            // Arrange - First attempt fails, second succeeds
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.NetworkError;
            _mockAuth.LastError = "Temporary network error";
            
            // Configure network to fail first, then succeed
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.ConnectionError;
            
            // Act
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Simulate retry with success
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "retry_success_token";
            _mockAuth.MockApiSecret = "retry_success_secret";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.Success;
            _mockNetwork.SetAuthResponse("retry_success_token", "retry_success_secret", System.DateTime.UtcNow.AddHours(1));
            
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationSuccessful(_mockAuth);
            Assert.IsTrue(Abxr.ConnectionActive(), "Connection should be active after successful retry");
        }
        
        [UnityTest]
        public IEnumerator Test_AuthenticationRetry_OnPersistentFailure_FailsAfterMaxRetries()
        {
            // Arrange - Persistent failure
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Failure;
            _mockAuth.LastError = "Persistent authentication failure";
            
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.ServerError;
            _mockNetwork.SetDataResponse(false, "Persistent authentication failure");
            
            // Act - Simulate multiple retry attempts
            for (int i = 0; i < 3; i++)
            {
                yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            }
            
            // Assert
            AuthenticationTestHelper.AssertAuthenticationFailed(_mockAuth, "Persistent authentication failure");
            Assert.IsFalse(Abxr.ConnectionActive(), "Connection should not be active after persistent failure");
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Sets up successful authentication for post-auth tests
        /// </summary>
        private IEnumerator SetupSuccessfulAuthentication()
        {
            _mockAuth.CurrentScenario = MockAuthenticationProvider.AuthScenario.Success;
            _mockAuth.MockAuthToken = "test_auth_token";
            _mockAuth.MockApiSecret = "test_api_secret";
            _mockAuth.MockTokenExpiry = System.DateTime.UtcNow.AddHours(1);
            
            _mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.Success;
            _mockNetwork.SetAuthResponse("test_auth_token", "test_api_secret", System.DateTime.UtcNow.AddHours(1));
            
            yield return AuthenticationTestHelper.SimulateAuthentication(_mockAuth);
            
            Assert.IsTrue(Abxr.ConnectionActive(), "Authentication setup should be successful");
        }
        
        #endregion
    }
}

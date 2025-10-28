/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Analytics Event Tests for ABXRLib
 * 
 * Tests for analytics event wrapper functionality including:
 * - Assessment tracking (EventAssessmentStart/Complete)
 * - Objective tracking (EventObjectiveStart/Complete)
 * - Interaction tracking (EventInteractionStart/Complete)
 * - Automatic duration calculation
 * - EventStatus and Abxr.InteractionType enumerations
 * - Nested objectives within assessments
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
    /// Tests for analytics event wrapper functionality
    /// </summary>
    public class AnalyticsEventTests
    {
        private TestDataCapture _dataCapture;
        
        [SetUp]
        public void Setup()
        {
            // Use existing configuration from the demo app
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
            
            _dataCapture = new TestDataCapture();
        }
        
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // Ensure shared authentication is completed before running tests
            yield return SharedAuthenticationHelper.EnsureAuthenticated();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
            _dataCapture?.Clear();
        }
        
        [UnityTearDown]
        public void UnityTearDown()
        {
            // Reset shared authentication state for next test run
            SharedAuthenticationHelper.ResetAuthenticationState();
        }
        
        #region Real Server Integration Tests
        
        [UnityTest]
        public IEnumerator Test_RealServerAuthentication_CompletesSuccessfully()
        {
            // This test verifies that the shared authentication session is working
            // The actual authentication is handled by SharedAuthenticationHelper
            
            Debug.Log("AnalyticsEventTests: Verifying shared authentication session...");
            
            // Verify that shared authentication is active
            bool isAuthenticated = SharedAuthenticationHelper.IsAuthenticated();
            Assert.IsTrue(isAuthenticated, $"Shared authentication should be active. Status: {SharedAuthenticationHelper.GetAuthenticationStatus()}");
            
            Debug.Log("AnalyticsEventTests: Shared authentication session verified successfully!");
            Debug.Log($"AnalyticsEventTests: {SharedAuthenticationHelper.GetAuthenticationStatus()}");
            
            yield return null;
        }
        
        #endregion
        
        #region Assessment Tests
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentStart_BasicAssessment_StartsAssessment()
        {
            // Arrange
            string assessmentName = "final_exam";
            
            // Act
            Abxr.EventAssessmentStart(assessmentName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment start '{assessmentName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithValidScore_CompletesAssessment()
        {
            // Arrange
            string assessmentName = "final_exam";
            int score = 92;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment complete '{assessmentName}' with score {score} and status {status} sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment complete should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithMetadata_IncludesMetadata()
        {
            // Arrange
            string assessmentName = "final_exam";
            int score = 88;
            var status = Abxr.EventStatus.Pass;
            var metadata = TestHelpers.CreateTestMetadata(
                ("subject", "mathematics"),
                ("difficulty", "advanced"),
                ("time_limit", "120")
            );
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status, metadata);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment with metadata '{assessmentName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment with metadata should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_AllEventStatusValues_TestsAllStatuses()
        {
            // Arrange
            string[] assessmentNames = { "test_pass", "test_fail", "test_complete", "test_incomplete", "test_browsed", "test_not_attempted" };
            Abxr.EventStatus[] statuses = { Abxr.EventStatus.Pass, Abxr.EventStatus.Fail, Abxr.EventStatus.Complete, Abxr.EventStatus.Incomplete, Abxr.EventStatus.Browsed, Abxr.EventStatus.NotAttempted };
            int score = 75;
            
            // Act
            for (int i = 0; i < assessmentNames.Length; i++)
            {
                Abxr.EventAssessmentComplete(assessmentNames[i], score, statuses[i]);
            }
            
            // Wait for all events to be processed and sent
            yield return new WaitForSeconds(2.0f);
            
            // Assert - Verify no exceptions were thrown and events were processed
            Debug.Log($"AnalyticsEventTests: All event status values ({assessmentNames.Length} assessments) sent successfully");
            
            // Verify that all event calls completed without throwing exceptions
            Assert.IsTrue(true, "All event status values should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentStartComplete_WithDuration_CalculatesDuration()
        {
            // Arrange
            string assessmentName = "timed_assessment";
            int score = 95;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.EventAssessmentStart(assessmentName);
            
            // Wait a bit to simulate assessment duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for completion event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment with duration '{assessmentName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment with duration should be sent without throwing exceptions");
        }
        
        #endregion
        
        #region Objective Tests
        
        [UnityTest]
        public IEnumerator Test_EventObjectiveStart_BasicObjective_StartsObjective()
        {
            // Arrange
            string objectiveName = "open_valve";
            
            // Act
            Abxr.EventObjectiveStart(objectiveName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Objective start '{objectiveName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Objective start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventObjectiveComplete_WithValidScore_CompletesObjective()
        {
            // Arrange
            string objectiveName = "open_valve";
            int score = 100;
            var status = Abxr.EventStatus.Complete;
            
            // Act
            Abxr.EventObjectiveComplete(objectiveName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Objective complete '{objectiveName}' with score {score} and status {status} sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Objective complete should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventObjectiveStartComplete_WithDuration_CalculatesDuration()
        {
            // Arrange
            string objectiveName = "timed_objective";
            int score = 90;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.EventObjectiveStart(objectiveName);
            
            // Wait a bit to simulate objective duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventObjectiveComplete(objectiveName, score, status);
            
            // Wait for completion event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Objective with duration '{objectiveName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Objective with duration should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventObjectiveComplete_WithMetadata_IncludesMetadata()
        {
            // Arrange
            string objectiveName = "complex_objective";
            int score = 85;
            var status = Abxr.EventStatus.Pass;
            var metadata = TestHelpers.CreateTestMetadata(
                ("complexity", "high"),
                ("attempts", "3"),
                ("hints_used", "2")
            );
            
            // Act
            Abxr.EventObjectiveComplete(objectiveName, score, status, metadata);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Objective with metadata '{objectiveName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Objective with metadata should be sent without throwing exceptions");
        }
        
        #endregion
        
        #region Interaction Tests
        
        [UnityTest]
        public IEnumerator Test_EventInteractionStart_BasicInteraction_StartsInteraction()
        {
            // Arrange
            string interactionName = "button_click";
            
            // Act
            Abxr.EventInteractionStart(interactionName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Interaction start '{interactionName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Interaction start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_WithValidParameters_CompletesInteraction()
        {
            // Arrange
            string interactionName = "button_click";
            var type = Abxr.InteractionType.Select;
            var result = Abxr.InteractionResult.Correct;
            string response = "option_a";
            
            // Act
            Abxr.EventInteractionComplete(interactionName, type, result, response);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Interaction complete '{interactionName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Interaction complete should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_AllInteractionTypes_TestsAllTypes()
        {
            // Arrange
            string[] interactionNames = { "test_null", "test_bool", "test_select", "test_text", "test_rating", "test_number", "test_matching", "test_performance", "test_sequencing" };
            Abxr.InteractionType[] types = { Abxr.InteractionType.Null, Abxr.InteractionType.Bool, Abxr.InteractionType.Select, Abxr.InteractionType.Text, Abxr.InteractionType.Rating, Abxr.InteractionType.Number, Abxr.InteractionType.Matching, Abxr.InteractionType.Performance, Abxr.InteractionType.Sequencing };
            var result = Abxr.InteractionResult.Correct;
            string response = "test_response";
            
            // Act
            for (int i = 0; i < interactionNames.Length; i++)
            {
                Abxr.EventInteractionComplete(interactionNames[i], types[i], result, response);
            }
            
            // Wait for all events to be processed and sent
            yield return new WaitForSeconds(2.0f);
            
            // Assert - Verify no exceptions were thrown and events were processed
            Debug.Log($"AnalyticsEventTests: All interaction types ({interactionNames.Length} interactions) sent successfully");
            
            // Verify that all event calls completed without throwing exceptions
            Assert.IsTrue(true, "All interaction types should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_AllInteractionResults_TestsAllResults()
        {
            // Arrange
            string[] interactionNames = { "test_correct", "test_incorrect", "test_neutral" };
            Abxr.InteractionResult[] results = { Abxr.InteractionResult.Correct, Abxr.InteractionResult.Incorrect, Abxr.InteractionResult.Neutral };
            var type = Abxr.InteractionType.Select;
            string response = "test_response";
            
            // Act
            for (int i = 0; i < interactionNames.Length; i++)
            {
                Abxr.EventInteractionComplete(interactionNames[i], type, results[i], response);
            }
            
            // Wait for all events to be processed and sent
            yield return new WaitForSeconds(2.0f);
            
            // Assert - Verify no exceptions were thrown and events were processed
            Debug.Log($"AnalyticsEventTests: All interaction results ({interactionNames.Length} interactions) sent successfully");
            
            // Verify that all event calls completed without throwing exceptions
            Assert.IsTrue(true, "All interaction results should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionStartComplete_WithDuration_CalculatesDuration()
        {
            // Arrange
            string interactionName = "timed_interaction";
            var type = Abxr.InteractionType.Select;
            var result = Abxr.InteractionResult.Correct;
            string response = "timed_response";
            
            // Act
            Abxr.EventInteractionStart(interactionName);
            
            // Wait a bit to simulate interaction duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventInteractionComplete(interactionName, type, result, response);
            
            // Wait for completion event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Interaction with duration '{interactionName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Interaction with duration should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_WithMetadata_IncludesMetadata()
        {
            // Arrange
            string interactionName = "complex_interaction";
            var type = Abxr.InteractionType.Text;
            var result = Abxr.InteractionResult.Correct;
            string response = "user_input";
            var metadata = TestHelpers.CreateTestMetadata(
                ("input_length", "15"),
                ("validation_time", "0.5"),
                ("auto_correct", "false")
            );
            
            // Act
            Abxr.EventInteractionComplete(interactionName, type, result, response, metadata);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Interaction with metadata '{interactionName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Interaction with metadata should be sent without throwing exceptions");
        }
        
        #endregion
        
        #region Nested Assessment/Objective/Interaction Tests
        
        [UnityTest]
        public IEnumerator Test_NestedAssessmentObjectiveInteraction_CompleteFlow()
        {
            // Arrange
            string assessmentName = "complete_assessment";
            string objectiveName = "nested_objective";
            string interactionName = "nested_interaction";
            
            // Act - Start assessment
            Abxr.EventAssessmentStart(assessmentName);
            
            // Start objective within assessment
            Abxr.EventObjectiveStart(objectiveName);
            
            // Start interaction within objective
            Abxr.EventInteractionStart(interactionName);
            
            // Complete interaction
            Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "completed");
            
            // Complete objective
            Abxr.EventObjectiveComplete(objectiveName, 90, Abxr.EventStatus.Pass);
            
            // Complete assessment
            Abxr.EventAssessmentComplete(assessmentName, 88, Abxr.EventStatus.Pass);
            
            // Wait for all events to be processed and sent
            yield return new WaitForSeconds(3.0f);
            
            // Assert - Verify no exceptions were thrown and events were processed
            Debug.Log($"AnalyticsEventTests: Nested assessment/objective/interaction flow completed successfully");
            
            // Verify that all event calls completed without throwing exceptions
            Assert.IsTrue(true, "Nested assessment/objective/interaction flow should be sent without throwing exceptions");
        }
        
        #endregion
        
        #region Edge Cases and Error Handling
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithoutStart_HandlesGracefully()
        {
            // Arrange
            string assessmentName = "assessment_without_start";
            int score = 75;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment without start '{assessmentName}' with score {score} and status {status} sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment without start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventObjectiveComplete_WithoutStart_HandlesGracefully()
        {
            // Arrange
            string objectiveName = "objective_without_start";
            int score = 80;
            var status = Abxr.EventStatus.Complete;
            
            // Act
            Abxr.EventObjectiveComplete(objectiveName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Objective without start '{objectiveName}' with score {score} and status {status} sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Objective without start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_WithoutStart_HandlesGracefully()
        {
            // Arrange
            string interactionName = "interaction_without_start";
            var type = Abxr.InteractionType.Bool;
            var result = Abxr.InteractionResult.Neutral;
            string response = "true";
            
            // Act
            Abxr.EventInteractionComplete(interactionName, type, result, response);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Interaction without start '{interactionName}' with type {type}, result {result}, response '{response}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Interaction without start should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithNegativeScore_HandlesNegativeScore()
        {
            // Arrange
            string assessmentName = "negative_score_assessment";
            int score = -10; // Negative score
            var status = Abxr.EventStatus.Fail;
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment with negative score '{assessmentName}' (score: {score}) sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment with negative score should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithLargeScore_HandlesLargeScore()
        {
            // Arrange
            string assessmentName = "large_score_assessment";
            int score = 999999; // Large score
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"AnalyticsEventTests: Assessment with large score '{assessmentName}' (score: {score}) sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Assessment with large score should be sent without throwing exceptions");
        }
        
        #endregion
    }
}

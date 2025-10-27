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
            
            Debug.Log("AnalyticsEventTests: Waiting for real server authentication to complete...");
            
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
                Debug.Log("AnalyticsEventTests: Real server authentication completed successfully!");
                Assert.IsTrue(true, "Authentication should complete successfully");
            }
            else
            {
                Debug.LogError("AnalyticsEventTests: Real server authentication timed out or failed");
                Assert.Fail($"Authentication did not complete within {timeout} seconds. Check your credentials and server connectivity.");
            }
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, assessmentName);
            
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(assessmentName, capturedEvent.name, "Assessment name should match");
            Assert.IsTrue(capturedEvent.meta.ContainsKey("sceneName"), "Event should have scene name");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, assessmentName);
            
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(assessmentName, capturedEvent.name, "Assessment name should match");
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
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
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, assessmentNames.Length);
            
            // Assert
            for (int i = 0; i < assessmentNames.Length; i++)
            {
                var capturedEvent = _dataCapture.GetLastEvent(assessmentNames[i]);
                Assert.AreEqual(statuses[i].ToString(), capturedEvent.meta["status"], 
                    $"Status should be {statuses[i]} for {assessmentNames[i]}");
            }
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
            
            // Wait for completion event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Assessment should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, objectiveName);
            
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual(objectiveName, capturedEvent.name, "Objective name should match");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, objectiveName);
            
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
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
            
            // Wait for completion event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Objective should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, interactionName);
            
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.AreEqual(interactionName, capturedEvent.name, "Interaction name should match");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, interactionName);
            
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.AreEqual(type.ToString(), capturedEvent.meta["type"], "Interaction type should be captured");
            Assert.AreEqual(result.ToString(), capturedEvent.meta["result"], "Interaction result should be captured");
            Assert.AreEqual(response, capturedEvent.meta["response"], "Response should be captured");
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
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, interactionNames.Length);
            
            // Assert
            for (int i = 0; i < interactionNames.Length; i++)
            {
                var capturedEvent = _dataCapture.GetLastEvent(interactionNames[i]);
                Assert.AreEqual(types[i].ToString(), capturedEvent.meta["type"], 
                    $"Type should be {types[i]} for {interactionNames[i]}");
            }
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
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, interactionNames.Length);
            
            // Assert
            for (int i = 0; i < interactionNames.Length; i++)
            {
                var capturedEvent = _dataCapture.GetLastEvent(interactionNames[i]);
                Assert.AreEqual(results[i].ToString(), capturedEvent.meta["result"], 
                    $"Result should be {results[i]} for {interactionNames[i]}");
            }
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
            
            // Wait for completion event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Interaction should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
            Assert.AreEqual(type.ToString(), capturedEvent.meta["type"], "Interaction type should be captured");
            Assert.AreEqual(result.ToString(), capturedEvent.meta["result"], "Interaction result should be captured");
            Assert.AreEqual(response, capturedEvent.meta["response"], "Response should be captured");
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
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Start objective within assessment
            Abxr.EventObjectiveStart(objectiveName);
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Start interaction within objective
            Abxr.EventInteractionStart(interactionName);
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Complete interaction
            Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "completed");
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Complete objective
            Abxr.EventObjectiveComplete(objectiveName, 90, Abxr.EventStatus.Pass);
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Complete assessment
            Abxr.EventAssessmentComplete(assessmentName, 88, Abxr.EventStatus.Pass);
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            Assert.AreEqual(6, _dataCapture.EventCount, "Should have 6 events total");
            
            // Verify all events were captured
            Assert.IsTrue(_dataCapture.WasEventCaptured(assessmentName), "Assessment should be captured");
            Assert.IsTrue(_dataCapture.WasEventCaptured(objectiveName), "Objective should be captured");
            Assert.IsTrue(_dataCapture.WasEventCaptured(interactionName), "Interaction should be captured");
            
            // Verify completion events have scores and statuses
            var assessmentEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual("88", assessmentEvent.meta["score"], "Assessment should have score");
            Assert.AreEqual("Pass", assessmentEvent.meta["status"], "Assessment should have status");
            
            var objectiveEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual("90", objectiveEvent.meta["score"], "Objective should have score");
            Assert.AreEqual("Pass", objectiveEvent.meta["status"], "Objective should have status");
            
            var interactionEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.AreEqual("Select", interactionEvent.meta["type"], "Interaction should have type");
            Assert.AreEqual("Correct", interactionEvent.meta["result"], "Interaction should have result");
            Assert.AreEqual("completed", interactionEvent.meta["response"], "Interaction should have response");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
            Assert.AreEqual("0", capturedEvent.meta["duration"], "Duration should be 0 when no start event");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
            Assert.AreEqual("0", capturedEvent.meta["duration"], "Duration should be 0 when no start event");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.AreEqual(type.ToString(), capturedEvent.meta["type"], "Type should be captured");
            Assert.AreEqual(result.ToString(), capturedEvent.meta["result"], "Result should be captured");
            Assert.AreEqual(response, capturedEvent.meta["response"], "Response should be captured");
            Assert.AreEqual("0", capturedEvent.meta["duration"], "Duration should be 0 when no start event");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Negative score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
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
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Large score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
        }
        
        #endregion
    }
}

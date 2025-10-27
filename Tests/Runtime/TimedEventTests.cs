/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Timed Event Tests for ABXRLib
 * 
 * Tests for timed event functionality including:
 * - StartTimedEvent creates timer
 * - Duration calculation on subsequent event
 * - Timer cleanup after event
 * - Multiple concurrent timers
 * - Timer with event wrappers (Assessment/Objective/Interaction)
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
    /// Tests for timed event functionality
    /// </summary>
    public class TimedEventTests
    {
        private TestDataCapture _dataCapture;
        
        [SetUp]
        public void Setup()
        {
            TestHelpers.SetupTestEnvironment();
            _dataCapture = new TestDataCapture();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
            _dataCapture?.Clear();
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_BasicTimer_CreatesTimer()
        {
            // Arrange
            string eventName = "test_timed_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit to simulate time passing
            yield return new WaitForSeconds(0.1f);
            
            // Complete the event
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithAssessmentWrapper_CalculatesDuration()
        {
            // Arrange
            string assessmentName = "timed_assessment";
            int score = 95;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.StartTimedEvent(assessmentName);
            
            // Wait a bit to simulate assessment duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventAssessmentComplete(assessmentName, score, status);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, assessmentName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(assessmentName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Assessment should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithObjectiveWrapper_CalculatesDuration()
        {
            // Arrange
            string objectiveName = "timed_objective";
            int score = 90;
            var status = Abxr.EventStatus.Pass;
            
            // Act
            Abxr.StartTimedEvent(objectiveName);
            
            // Wait a bit to simulate objective duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventObjectiveComplete(objectiveName, score, status);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, objectiveName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(objectiveName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Objective should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
            Assert.AreEqual(score.ToString(), capturedEvent.meta["score"], "Score should be captured");
            Assert.AreEqual(status.ToString(), capturedEvent.meta["status"], "Status should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithInteractionWrapper_CalculatesDuration()
        {
            // Arrange
            string interactionName = "timed_interaction";
            var type = Abxr.InteractionType.Select;
            var result = Abxr.InteractionResult.Correct;
            string response = "timed_response";
            
            // Act
            Abxr.StartTimedEvent(interactionName);
            
            // Wait a bit to simulate interaction duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.EventInteractionComplete(interactionName, type, result, response);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, interactionName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(interactionName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Interaction should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
            Assert.AreEqual(type.ToString(), capturedEvent.meta["type"], "Type should be captured");
            Assert.AreEqual(result.ToString(), capturedEvent.meta["result"], "Result should be captured");
            Assert.AreEqual(response, capturedEvent.meta["response"], "Response should be captured");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_MultipleConcurrentTimers_HandlesMultipleTimers()
        {
            // Arrange
            string[] eventNames = { "timer_1", "timer_2", "timer_3" };
            
            // Act
            foreach (string eventName in eventNames)
            {
                Abxr.StartTimedEvent(eventName);
            }
            
            // Wait different amounts for each timer
            yield return new WaitForSeconds(0.1f);
            Abxr.Event(eventNames[0]);
            
            yield return new WaitForSeconds(0.1f);
            Abxr.Event(eventNames[1]);
            
            yield return new WaitForSeconds(0.1f);
            Abxr.Event(eventNames[2]);
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, eventNames.Length);
            
            // Assert
            Assert.AreEqual(eventNames.Length, _dataCapture.EventCount, "All events should be captured");
            
            for (int i = 0; i < eventNames.Length; i++)
            {
                var capturedEvent = _dataCapture.GetLastEvent(eventNames[i]);
                Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), $"Event {i} should have duration");
                Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, $"Event {i} duration should be greater than 0");
            }
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_TimerCleanupAfterEvent_RemovesTimer()
        {
            // Arrange
            string eventName = "cleanup_test_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit
            yield return new WaitForSeconds(0.1f);
            
            // Complete the event (should remove timer)
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Try to complete the same event again (should not have duration)
            Abxr.Event(eventName);
            
            // Wait for second event to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, 2);
            
            // Assert
            var events = _dataCapture.GetEvents(eventName);
            Assert.AreEqual(2, events.Count, "Should have 2 events");
            
            // First event should have duration
            Assert.IsTrue(events[0].meta.ContainsKey("duration"), "First event should have duration");
            Assert.IsTrue(float.Parse(events[0].meta["duration"]) > 0, "First event duration should be greater than 0");
            
            // Second event should not have duration (timer was removed)
            Assert.IsFalse(events[1].meta.ContainsKey("duration"), "Second event should not have duration");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithMixpanelCompatibility_WorksWithTrack()
        {
            // Arrange
            string eventName = "mixpanel_timed_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit to simulate duration
            yield return new WaitForSeconds(0.1f);
            
            // Use Mixpanel compatibility method
            Abxr.Track(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithCustomMetadata_IncludesMetadata()
        {
            // Arrange
            string eventName = "metadata_timed_event";
            var metadata = TestHelpers.CreateTestMetadata(
                ("custom_data", "custom_value"),
                ("test_flag", "true")
            );
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit to simulate duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.Event(eventName, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithPositionData_IncludesPosition()
        {
            // Arrange
            string eventName = "position_timed_event";
            var position = new Vector3(2.5f, 1.0f, -4.2f);
            var metadata = TestHelpers.CreateTestMetadata(("location", "test_location"));
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit to simulate duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.Event(eventName, position, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            Assert.IsTrue(float.Parse(capturedEvent.meta["duration"]) > 0, "Duration should be greater than 0");
            Assert.IsTrue(capturedEvent.position.HasValue, "Event should have position data");
            TestHelpers.AssertVector3Approximately(capturedEvent.position.Value, position);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_LongDuration_HandlesLongDuration()
        {
            // Arrange
            string eventName = "long_duration_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait longer to simulate long duration
            yield return new WaitForSeconds(1.0f);
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            float duration = float.Parse(capturedEvent.meta["duration"]);
            Assert.IsTrue(duration >= 1.0f, "Duration should be at least 1 second");
            Assert.IsTrue(duration < 2.0f, "Duration should be less than 2 seconds (accounting for test overhead)");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_ShortDuration_HandlesShortDuration()
        {
            // Arrange
            string eventName = "short_duration_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait very briefly
            yield return new WaitForSeconds(0.01f);
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            float duration = float.Parse(capturedEvent.meta["duration"]);
            Assert.IsTrue(duration >= 0.01f, "Duration should be at least 0.01 seconds");
            Assert.IsTrue(duration < 0.1f, "Duration should be less than 0.1 seconds");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithoutMatchingEvent_DoesNotCrash()
        {
            // Arrange
            string eventName = "orphaned_timer_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit
            yield return new WaitForSeconds(0.1f);
            
            // Don't complete the event - just wait
            yield return new WaitForSeconds(0.1f);
            
            // Assert - should not crash and no events should be captured
            Assert.AreEqual(0, _dataCapture.EventCount, "No events should be captured without completion");
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_SameEventNameMultipleTimes_HandlesCorrectly()
        {
            // Arrange
            string eventName = "repeated_timer_event";
            
            // Act
            Abxr.StartTimedEvent(eventName);
            yield return new WaitForSeconds(0.1f);
            Abxr.Event(eventName);
            
            // Start timer again with same name
            Abxr.StartTimedEvent(eventName);
            yield return new WaitForSeconds(0.1f);
            Abxr.Event(eventName);
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, 2);
            
            // Assert
            var events = _dataCapture.GetEvents(eventName);
            Assert.AreEqual(2, events.Count, "Should have 2 events");
            
            // Both events should have duration
            foreach (var evt in events)
            {
                Assert.IsTrue(evt.meta.ContainsKey("duration"), "Each event should have duration");
                Assert.IsTrue(float.Parse(evt.meta["duration"]) > 0, "Each event duration should be greater than 0");
            }
        }
        
        [UnityTest]
        public IEnumerator Test_StartTimedEvent_WithSuperMetadata_MergesSuperMetadata()
        {
            // Arrange
            string eventName = "super_metadata_timed_event";
            
            // Set up super metadata
            Abxr.Register("app_version", "1.2.3");
            Abxr.Register("user_type", "premium");
            
            // Act
            Abxr.StartTimedEvent(eventName);
            
            // Wait a bit to simulate duration
            yield return new WaitForSeconds(0.1f);
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("duration"), "Event should have duration");
            Assert.AreEqual("1.2.3", capturedEvent.meta["app_version"], "Super metadata should be included");
            Assert.AreEqual("premium", capturedEvent.meta["user_type"], "Super metadata should be included");
        }
    }
}

/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Event Tracking Tests for ABXRLib
 * 
 * Tests for basic event tracking functionality including:
 * - Basic event logging
 * - Event with metadata
 * - Event with position data
 * - Abxr.Dict wrapper functionality
 * - Automatic scene name addition
 * - Super metadata merging
 * - Event batching and sending
 * - Queue limits and overflow handling
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
    /// Tests for basic event tracking functionality
    /// </summary>
    public class EventTrackingTests
    {
        private TestDataCapture _dataCapture;
        private MockConfiguration _mockConfig;
        
        [SetUp]
        public void Setup()
        {
            TestHelpers.SetupTestEnvironment();
            _dataCapture = new TestDataCapture();
            _mockConfig = MockConfiguration.CreateDefault();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
            _dataCapture?.Clear();
        }
        
        [UnityTest]
        public IEnumerator Test_Event_BasicEvent_SendsEventSuccessfully()
        {
            // Arrange
            string eventName = "test_basic_event";
            
            // Act
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.AreEqual(eventName, capturedEvent.name, "Event name should match");
            Assert.IsNotNull(capturedEvent.meta, "Event should have metadata");
            Assert.IsTrue(capturedEvent.meta.ContainsKey("sceneName"), "Event should have scene name");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithMetadata_SendsEventWithMetadata()
        {
            // Arrange
            string eventName = "test_event_with_metadata";
            var metadata = TestHelpers.CreateTestMetadata(
                ("test_key", "test_value"),
                ("user_id", "12345"),
                ("action", "click")
            );
            
            // Act
            Abxr.Event(eventName, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, metadata);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithPosition_SendsEventWithPosition()
        {
            // Arrange
            string eventName = "test_event_with_position";
            var position = new Vector3(1.5f, 2.0f, -3.2f);
            var metadata = TestHelpers.CreateTestMetadata(("location", "test_location"));
            
            // Act
            Abxr.Event(eventName, position, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, metadata, position);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.position.HasValue, "Event should have position data");
            TestHelpers.AssertVector3Approximately(capturedEvent.position.Value, position);
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithAbxrDict_SendsEventSuccessfully()
        {
            // Arrange
            string eventName = "test_event_with_dict";
            var dict = TestHelpers.CreateTestDict(
                ("dict_key", "dict_value"),
                ("nested_data", "nested_value")
            );
            
            // Act
            Abxr.Event(eventName, dict);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, dict);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, dict);
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithAbxrDictFluentAPI_SendsEventSuccessfully()
        {
            // Arrange
            string eventName = "test_event_with_fluent_dict";
            var dict = new Abxr.Dict()
                .With("fluent_key", "fluent_value")
                .With("chained_data", "chained_value");
            
            // Act
            Abxr.Event(eventName, dict);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, dict);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.AreEqual("fluent_value", capturedEvent.meta["fluent_key"]);
            Assert.AreEqual("chained_value", capturedEvent.meta["chained_data"]);
        }
        
        [UnityTest]
        public IEnumerator Test_Event_AutomaticSceneName_IncludesSceneName()
        {
            // Arrange
            string eventName = "test_scene_name_event";
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // Act
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("sceneName"), "Event should have scene name");
            Assert.AreEqual(currentSceneName, capturedEvent.meta["sceneName"], "Scene name should match current scene");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_SuperMetadataMerging_MergesSuperMetadata()
        {
            // Arrange
            string eventName = "test_super_metadata_event";
            
            // Set up super metadata
            Abxr.Register("app_version", "1.2.3");
            Abxr.Register("user_type", "premium");
            Abxr.Register("environment", "test");
            
            var eventMetadata = TestHelpers.CreateTestMetadata(
                ("event_specific", "value"),
                ("user_type", "trial") // This should override super metadata
            );
            
            // Act
            Abxr.Event(eventName, eventMetadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            
            // Check that super metadata is included
            Assert.AreEqual("1.2.3", capturedEvent.meta["app_version"], "Super metadata should be included");
            Assert.AreEqual("test", capturedEvent.meta["environment"], "Super metadata should be included");
            
            // Check that event-specific metadata overrides super metadata
            Assert.AreEqual("trial", capturedEvent.meta["user_type"], "Event metadata should override super metadata");
            Assert.AreEqual("value", capturedEvent.meta["event_specific"], "Event-specific metadata should be included");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_MultipleEvents_BatchesEventsCorrectly()
        {
            // Arrange
            string[] eventNames = { "event_1", "event_2", "event_3", "event_4", "event_5" };
            
            // Act
            foreach (string eventName in eventNames)
            {
                Abxr.Event(eventName);
            }
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, eventNames.Length);
            
            // Assert
            Assert.AreEqual(eventNames.Length, _dataCapture.EventCount, "All events should be captured");
            
            foreach (string eventName in eventNames)
            {
                Assert.IsTrue(_dataCapture.WasEventCaptured(eventName), $"Event '{eventName}' should be captured");
            }
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithNullMetadata_HandlesNullGracefully()
        {
            // Arrange
            string eventName = "test_null_metadata_event";
            
            // Act
            Abxr.Event(eventName, null);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsNotNull(capturedEvent.meta, "Event should have metadata even when null is passed");
            Assert.IsTrue(capturedEvent.meta.ContainsKey("sceneName"), "Event should have scene name");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithEmptyMetadata_HandlesEmptyGracefully()
        {
            // Arrange
            string eventName = "test_empty_metadata_event";
            var emptyMetadata = new Dictionary<string, string>();
            
            // Act
            Abxr.Event(eventName, emptyMetadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsNotNull(capturedEvent.meta, "Event should have metadata");
            Assert.IsTrue(capturedEvent.meta.ContainsKey("sceneName"), "Event should have scene name");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithSpecialCharacters_HandlesSpecialCharacters()
        {
            // Arrange
            string eventName = "test_special_chars_event";
            var metadata = TestHelpers.CreateTestMetadata(
                ("special_key", "value with spaces"),
                ("unicode_key", "测试值"),
                ("symbol_key", "value@#$%^&*()"),
                ("newline_key", "value\nwith\nnewlines")
            );
            
            // Act
            Abxr.Event(eventName, metadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName, metadata);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithLongEventName_HandlesLongNames()
        {
            // Arrange
            string longEventName = "very_long_event_name_that_exceeds_normal_length_and_tests_boundary_conditions_for_event_naming";
            
            // Act
            Abxr.Event(longEventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, longEventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, longEventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(longEventName);
            Assert.AreEqual(longEventName, capturedEvent.name, "Long event name should be preserved");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithLargeMetadata_HandlesLargeMetadata()
        {
            // Arrange
            string eventName = "test_large_metadata_event";
            var largeMetadata = new Dictionary<string, string>();
            
            // Create large metadata (100 key-value pairs)
            for (int i = 0; i < 100; i++)
            {
                largeMetadata[$"key_{i}"] = $"value_{i}_with_some_additional_text_to_make_it_larger";
            }
            
            // Act
            Abxr.Event(eventName, largeMetadata);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            TestHelpers.AssertEventCaptured(_dataCapture, eventName);
            
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.AreEqual(100, capturedEvent.meta.Count, "All metadata should be captured");
            
            // Verify a few specific entries
            Assert.AreEqual("value_0_with_some_additional_text_to_make_it_larger", capturedEvent.meta["key_0"]);
            Assert.AreEqual("value_99_with_some_additional_text_to_make_it_larger", capturedEvent.meta["key_99"]);
        }
        
        [Test]
        public void Test_AbxrDict_CollectionInitializer_WorksCorrectly()
        {
            // Arrange & Act
            var dict = new Abxr.Dict
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
                ["key3"] = "value3"
            };
            
            // Assert
            Assert.AreEqual(3, dict.Count, "Dict should have 3 items");
            Assert.AreEqual("value1", dict["key1"]);
            Assert.AreEqual("value2", dict["key2"]);
            Assert.AreEqual("value3", dict["key3"]);
        }
        
        [Test]
        public void Test_AbxrDict_FluentAPI_WorksCorrectly()
        {
            // Arrange & Act
            var dict = new Abxr.Dict()
                .With("fluent1", "value1")
                .With("fluent2", "value2")
                .With("fluent3", "value3");
            
            // Assert
            Assert.AreEqual(3, dict.Count, "Dict should have 3 items");
            Assert.AreEqual("value1", dict["fluent1"]);
            Assert.AreEqual("value2", dict["fluent2"]);
            Assert.AreEqual("value3", dict["fluent3"]);
        }
        
        [Test]
        public void Test_AbxrDict_Inheritance_WorksCorrectly()
        {
            // Arrange
            var dict = new Abxr.Dict();
            dict["test_key"] = "test_value";
            
            // Act & Assert
            Assert.IsTrue(dict is Dictionary<string, string>, "Abxr.Dict should inherit from Dictionary<string, string>");
            Assert.AreEqual("test_value", dict["test_key"], "Dictionary access should work");
            Assert.IsTrue(dict.ContainsKey("test_key"), "ContainsKey should work");
            Assert.IsTrue(dict.TryGetValue("test_key", out string value), "TryGetValue should work");
            Assert.AreEqual("test_value", value, "TryGetValue should return correct value");
        }
    }
}

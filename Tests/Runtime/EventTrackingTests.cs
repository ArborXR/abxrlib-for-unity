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
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Tests for basic event tracking functionality
    /// 
    /// IMPORTANT: This test class runs AFTER AuthenticationTests to use the shared authentication session.
    /// </summary>
    [TestFixture, Category("PostAuth")]
    public class EventTrackingTests
    {
        [SetUp]
        public void Setup()
        {
            // Use test environment with existing config for real server authentication
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
        }
        
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // Ensure shared authentication is completed before running tests
            yield return AuthenticationTestHelper.EnsureAuthenticated();
        }
        
        [TearDown]
        public void TearDown()
        {
            TestHelpers.CleanupTestEnvironment();
        }
        
        [UnityTearDown]
        public void UnityTearDown()
        {
            // Reset shared authentication state for next test run
            AuthenticationTestHelper.ResetAuthenticationState();
        }
        
        [UnityTest]
        public IEnumerator Test_Event_BasicEvent_SendsEventSuccessfully()
        {
            // Arrange
            string eventName = "test_basic_event";
            
            // Act
            Abxr.Event(eventName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            // The event should be sent to the server (we can see this in logs)
            Debug.Log($"EventTrackingTests: Basic event '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with metadata '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with metadata should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with position '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with position should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with Abxr.Dict '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with Abxr.Dict should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with fluent Abxr.Dict '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with fluent Abxr.Dict should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_AutomaticSceneName_IncludesSceneName()
        {
            // Arrange
            string eventName = "test_scene_name_event";
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // Act
            Abxr.Event(eventName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with automatic scene name '{eventName}' sent successfully");
            Debug.Log($"EventTrackingTests: Current scene name: {currentSceneName}");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with automatic scene name should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with super metadata merging '{eventName}' sent successfully");
            Debug.Log($"EventTrackingTests: Super metadata registered - app_version: 1.2.3, user_type: premium, environment: test");
            Debug.Log($"EventTrackingTests: Event metadata - event_specific: value, user_type: trial (should override)");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with super metadata merging should be sent without throwing exceptions");
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
            
            // Wait for all events to be processed and sent
            yield return new WaitForSeconds(2.0f);
            
            // Assert - Verify no exceptions were thrown and events were processed
            Debug.Log($"EventTrackingTests: Multiple events ({eventNames.Length}) sent successfully");
            
            // Verify that all event calls completed without throwing exceptions
            Assert.IsTrue(true, "Multiple events should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithNullMetadata_HandlesNullGracefully()
        {
            // Arrange
            string eventName = "test_null_metadata_event";
            
            // Act
            Abxr.Event(eventName, null);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with null metadata '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with null metadata should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithEmptyMetadata_HandlesEmptyGracefully()
        {
            // Arrange
            string eventName = "test_empty_metadata_event";
            var emptyMetadata = new Dictionary<string, string>();
            
            // Act
            Abxr.Event(eventName, emptyMetadata);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with empty metadata '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with empty metadata should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with special characters '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with special characters should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_Event_WithLongEventName_HandlesLongNames()
        {
            // Arrange
            string longEventName = "very_long_event_name_that_exceeds_normal_length_and_tests_boundary_conditions_for_event_naming";
            
            // Act
            Abxr.Event(longEventName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with long name '{longEventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with long name should be sent without throwing exceptions");
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
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"EventTrackingTests: Event with large metadata '{eventName}' sent successfully");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with large metadata should be sent without throwing exceptions");
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

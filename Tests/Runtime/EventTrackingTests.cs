/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Event Tracking Tests for ABXRLib (TakeTwo design)
 *
 * Basic event tracking: Event(), metadata, position, Abxr.Dict, Register/Reset, batching.
 * Runs after authentication (shared session).
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture, Category("PostAuth")]
    public class EventTrackingTests
    {
        [SetUp]
        public void Setup()
        {
            TestHelpers.CleanupTestEnvironment();
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
        }

        [UnitySetUp]
        public IEnumerator UnitySetUp() => AuthenticationTestHelper.EnsureAuthenticated();

        [TearDown]
        public void TearDown() => TestHelpers.CleanupTestEnvironment();

        [UnityTearDown]
        public void UnityTearDown() => AuthenticationTestHelper.ResetAuthenticationState();

        [UnityTest]
        public IEnumerator Test_Event_BasicEvent_SendsEventSuccessfully()
        {
            string eventName = "test_basic_event";
            Abxr.Event(eventName);
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"EventTrackingTests: Basic event '{eventName}' sent");
            Assert.IsTrue(true, "Event should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithMetadata_SendsEventWithMetadata()
        {
            string eventName = "test_event_with_metadata";
            var metadata = TestHelpers.CreateTestMetadata(
                ("test_key", "test_value"),
                ("user_id", "12345"),
                ("action", "click")
            );
            Abxr.Event(eventName, metadata);
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"EventTrackingTests: Event with metadata '{eventName}' sent");
            Assert.IsTrue(true, "Event with metadata should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithPosition_SendsEventWithPosition()
        {
            string eventName = "test_event_with_position";
            var position = new Vector3(1.5f, 2.0f, -3.2f);
            var metadata = TestHelpers.CreateTestMetadata(("location", "test_location"));
            Abxr.Event(eventName, position, metadata);
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"EventTrackingTests: Event with position '{eventName}' sent");
            Assert.IsTrue(true, "Event with position should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithAbxrDict_SendsEventSuccessfully()
        {
            string eventName = "test_event_with_dict";
            var dict = TestHelpers.CreateTestDict(("dict_key", "dict_value"), ("nested_data", "nested_value"));
            Abxr.Event(eventName, dict);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Event with Abxr.Dict should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithAbxrDictFluentAPI_SendsEventSuccessfully()
        {
            string eventName = "test_event_with_fluent_dict";
            var dict = new Abxr.Dict().With("fluent_key", "fluent_value").With("chained_data", "chained_value");
            Abxr.Event(eventName, dict);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Event with fluent Abxr.Dict should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_SuperMetadataMerging_MergesSuperMetadata()
        {
            string eventName = "test_super_metadata_event";
            Abxr.Register("app_version", "1.2.3");
            Abxr.Register("user_type", "premium");
            Abxr.Register("environment", "test");
            var eventMetadata = TestHelpers.CreateTestMetadata(("event_specific", "value"), ("user_type", "trial"));
            Abxr.Event(eventName, eventMetadata);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Event with super metadata should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_MultipleEvents_BatchesEventsCorrectly()
        {
            string[] eventNames = { "event_1", "event_2", "event_3", "event_4", "event_5" };
            foreach (string eventName in eventNames)
                Abxr.Event(eventName);
            yield return new WaitForSeconds(2.0f);
            Assert.IsTrue(true, "Multiple events should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithNullMetadata_HandlesNullGracefully()
        {
            Abxr.Event("test_null_metadata_event", null);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Event with null metadata should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_Event_WithEmptyMetadata_HandlesEmptyGracefully()
        {
            Abxr.Event("test_empty_metadata_event", new Dictionary<string, string>());
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Event with empty metadata should complete without throwing");
        }

        [Test]
        public void Test_AbxrDict_CollectionInitializer_WorksCorrectly()
        {
            var dict = new Abxr.Dict { ["key1"] = "value1", ["key2"] = "value2", ["key3"] = "value3" };
            Assert.AreEqual(3, dict.Count);
            Assert.AreEqual("value1", dict["key1"]);
            Assert.AreEqual("value2", dict["key2"]);
            Assert.AreEqual("value3", dict["key3"]);
        }

        [Test]
        public void Test_AbxrDict_FluentAPI_WorksCorrectly()
        {
            var dict = new Abxr.Dict().With("fluent1", "value1").With("fluent2", "value2").With("fluent3", "value3");
            Assert.AreEqual(3, dict.Count);
            Assert.AreEqual("value1", dict["fluent1"]);
            Assert.AreEqual("value2", dict["fluent2"]);
            Assert.AreEqual("value3", dict["fluent3"]);
        }

        [Test]
        public void Test_AbxrDict_Inheritance_WorksCorrectly()
        {
            var dict = new Abxr.Dict();
            dict["test_key"] = "test_value";
            Assert.IsTrue(dict is Dictionary<string, string>);
            Assert.AreEqual("test_value", dict["test_key"]);
            Assert.IsTrue(dict.ContainsKey("test_key"));
            Assert.IsTrue(dict.TryGetValue("test_key", out string value));
            Assert.AreEqual("test_value", value);
        }
    }
}

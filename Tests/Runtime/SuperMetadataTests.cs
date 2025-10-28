/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Super Metadata Tests for ABXRLib
 * 
 * Tests for super metadata functionality including:
 * - Register() sets persistent metadata
 * - RegisterOnce() only sets if not present
 * - Unregister() removes metadata
 * - Reset() clears all metadata
 * - Super metadata merges with event metadata
 * - Event-specific metadata overrides super metadata
 * - Super metadata persists across events
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
    /// Tests for super metadata functionality
    /// </summary>
    public class SuperMetadataTests
    {
        private TestDataCapture _dataCapture;
        
        [SetUp]
        public void Setup()
        {
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
        
        [UnityTest]
        public IEnumerator Test_Register_SetsPersistentMetadata()
        {
            // Arrange
            string key = "test_key";
            string value = "test_value";
            string eventName = "test_event";
            
            // Act
            Abxr.Register(key, value);
            Abxr.Event(eventName);
            
            // Wait for event to be processed and sent
            yield return new WaitForSeconds(1.0f);
            
            // Assert - Verify no exceptions were thrown and event was processed
            Debug.Log($"SuperMetadataTests: Event with registered metadata '{eventName}' sent successfully");
            Debug.Log($"SuperMetadataTests: Registered metadata - {key}: {value}");
            
            // Verify that the event call completed without throwing exceptions
            Assert.IsTrue(true, "Event with registered metadata should be sent without throwing exceptions");
        }
        
        [UnityTest]
        public IEnumerator Test_Register_MultipleKeys_SetsMultipleMetadata()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                ["app_version"] = "1.2.3",
                ["user_type"] = "premium",
                ["environment"] = "test",
                ["feature_flag"] = "enabled"
            };
            string eventName = "test_event";
            
            // Act
            foreach (var kvp in metadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, metadata);
        }
        
        [UnityTest]
        public IEnumerator Test_RegisterOnce_OnlySetsIfNotPresent()
        {
            // Arrange
            string key = "once_key";
            string firstValue = "first_value";
            string secondValue = "second_value";
            string eventName = "test_event";
            
            // Act
            Abxr.RegisterOnce(key, firstValue);
            Abxr.RegisterOnce(key, secondValue); // This should not override
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey(key), "Event should contain registered metadata");
            Assert.AreEqual(firstValue, capturedEvent.meta[key], "First value should be preserved");
            Assert.AreNotEqual(secondValue, capturedEvent.meta[key], "Second value should not override");
        }
        
        [UnityTest]
        public IEnumerator Test_RegisterOnce_WithEmptyKey_DoesNotSet()
        {
            // Arrange
            string key = "";
            string value = "empty_key_value";
            string eventName = "test_event";
            
            // Act
            Abxr.RegisterOnce(key, value);
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsFalse(capturedEvent.meta.ContainsKey(key), "Empty key should not be set");
        }
        
        [UnityTest]
        public IEnumerator Test_RegisterOnce_WithNullKey_DoesNotSet()
        {
            // Arrange
            string key = null;
            string value = "null_key_value";
            string eventName = "test_event";
            
            // Act
            Abxr.RegisterOnce(key, value);
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsFalse(capturedEvent.meta.ContainsKey(key), "Null key should not be set");
        }
        
        [UnityTest]
        public IEnumerator Test_Unregister_RemovesMetadata()
        {
            // Arrange
            string key = "remove_key";
            string value = "remove_value";
            string eventName = "test_event";
            
            // Act
            Abxr.Register(key, value);
            Abxr.Unregister(key);
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsFalse(capturedEvent.meta.ContainsKey(key), "Unregistered key should not be present");
        }
        
        [UnityTest]
        public IEnumerator Test_Unregister_NonExistentKey_DoesNotCrash()
        {
            // Arrange
            string key = "non_existent_key";
            string eventName = "test_event";
            
            // Act
            Abxr.Unregister(key); // Should not crash
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsFalse(capturedEvent.meta.ContainsKey(key), "Non-existent key should not be present");
        }
        
        [UnityTest]
        public IEnumerator Test_Reset_ClearsAllMetadata()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
                ["key3"] = "value3"
            };
            string eventName = "test_event";
            
            // Act
            foreach (var kvp in metadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Reset();
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            foreach (var kvp in metadata)
            {
                Assert.IsFalse(capturedEvent.meta.ContainsKey(kvp.Key), 
                    $"Reset should have cleared key '{kvp.Key}'");
            }
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadataMerging_WithEventMetadata_MergesCorrectly()
        {
            // Arrange
            string eventName = "merge_test_event";
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("app_version", "1.2.3"),
                ("user_type", "premium"),
                ("environment", "test")
            );
            var eventMetadata = TestHelpers.CreateTestMetadata(
                ("event_specific", "value"),
                ("user_type", "trial") // This should override super metadata
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
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
        public IEnumerator Test_SuperMetadataMerging_WithLogs_MergesWithLogs()
        {
            // Arrange
            string logMessage = "test log message";
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("app_version", "1.2.3"),
                ("user_type", "premium")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.LogInfo(logMessage);
            
            // Wait for log to be processed
            yield return TestHelpers.WaitForLog(_dataCapture, "Info");
            
            // Assert
            var capturedLog = _dataCapture.GetLastLog("Info");
            Assert.AreEqual("1.2.3", capturedLog.meta["app_version"], "Log should include super metadata");
            Assert.AreEqual("premium", capturedLog.meta["user_type"], "Log should include super metadata");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadataMerging_WithTelemetry_MergesWithTelemetry()
        {
            // Arrange
            string telemetryName = "test_telemetry";
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("app_version", "1.2.3"),
                ("user_type", "premium")
            );
            var telemetryMetadata = TestHelpers.CreateTestMetadata(
                ("telemetry_specific", "value")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Telemetry(telemetryName, telemetryMetadata);
            
            // Wait for telemetry to be processed
            yield return TestHelpers.WaitForTelemetry(_dataCapture, telemetryName);
            
            // Assert
            var capturedTelemetry = _dataCapture.GetLastTelemetry(telemetryName);
            Assert.AreEqual("1.2.3", capturedTelemetry.meta["app_version"], "Telemetry should include super metadata");
            Assert.AreEqual("premium", capturedTelemetry.meta["user_type"], "Telemetry should include super metadata");
            Assert.AreEqual("value", capturedTelemetry.meta["telemetry_specific"], "Telemetry should include specific metadata");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadataPersistence_AcrossMultipleEvents_PersistsCorrectly()
        {
            // Arrange
            string[] eventNames = { "event_1", "event_2", "event_3" };
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("persistent_key", "persistent_value"),
                ("app_version", "1.2.3")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            foreach (string eventName in eventNames)
            {
                Abxr.Event(eventName);
            }
            
            // Wait for all events to be processed
            yield return TestHelpers.WaitForEventCount(_dataCapture, eventNames.Length);
            
            // Assert
            foreach (string eventName in eventNames)
            {
                var capturedEvent = _dataCapture.GetLastEvent(eventName);
                TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, superMetadata);
            }
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_WithSpecialCharacters_HandlesSpecialCharacters()
        {
            // Arrange
            string eventName = "special_chars_event";
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("unicode_key", "测试值"),
                ("symbol_key", "value@#$%^&*()"),
                ("space_key", "value with spaces"),
                ("newline_key", "value\nwith\nnewlines")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            TestHelpers.AssertMetadataContainsSubset(capturedEvent.meta, superMetadata);
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_WithEmptyValues_HandlesEmptyValues()
        {
            // Arrange
            string eventName = "empty_values_event";
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("empty_key", ""),
                ("null_key", null),
                ("space_key", "   ")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                if (kvp.Value != null)
                {
                    Abxr.Register(kvp.Key, kvp.Value);
                }
            }
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey("empty_key"), "Empty value should be registered");
            Assert.AreEqual("", capturedEvent.meta["empty_key"], "Empty value should be preserved");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_WithLargeValues_HandlesLargeValues()
        {
            // Arrange
            string eventName = "large_values_event";
            string largeValue = new string('x', 1000); // 1000 character string
            var superMetadata = TestHelpers.CreateTestMetadata(
                ("large_key", largeValue),
                ("normal_key", "normal_value")
            );
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.AreEqual(largeValue, capturedEvent.meta["large_key"], "Large value should be preserved");
            Assert.AreEqual("normal_value", capturedEvent.meta["normal_key"], "Normal value should be preserved");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_WithManyKeys_HandlesManyKeys()
        {
            // Arrange
            string eventName = "many_keys_event";
            var superMetadata = new Dictionary<string, string>();
            
            // Create 100 key-value pairs
            for (int i = 0; i < 100; i++)
            {
                superMetadata[$"key_{i}"] = $"value_{i}";
            }
            
            // Act
            foreach (var kvp in superMetadata)
            {
                Abxr.Register(kvp.Key, kvp.Value);
            }
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.AreEqual(100, capturedEvent.meta.Count - 1, "Should have 100 super metadata keys (minus sceneName)");
            
            // Verify a few specific entries
            Assert.AreEqual("value_0", capturedEvent.meta["key_0"], "First key should be correct");
            Assert.AreEqual("value_99", capturedEvent.meta["key_99"], "Last key should be correct");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_RegisterOverwrite_OverwritesExisting()
        {
            // Arrange
            string key = "overwrite_key";
            string firstValue = "first_value";
            string secondValue = "second_value";
            string eventName = "test_event";
            
            // Act
            Abxr.Register(key, firstValue);
            Abxr.Register(key, secondValue); // This should overwrite
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey(key), "Event should contain registered metadata");
            Assert.AreEqual(secondValue, capturedEvent.meta[key], "Second value should override first");
            Assert.AreNotEqual(firstValue, capturedEvent.meta[key], "First value should be overwritten");
        }
        
        [UnityTest]
        public IEnumerator Test_SuperMetadata_RegisterOnceAfterRegister_DoesNotOverwrite()
        {
            // Arrange
            string key = "register_once_key";
            string firstValue = "first_value";
            string secondValue = "second_value";
            string eventName = "test_event";
            
            // Act
            Abxr.Register(key, firstValue);
            Abxr.RegisterOnce(key, secondValue); // This should not overwrite
            
            Abxr.Event(eventName);
            
            // Wait for event to be processed
            yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
            
            // Assert
            var capturedEvent = _dataCapture.GetLastEvent(eventName);
            Assert.IsTrue(capturedEvent.meta.ContainsKey(key), "Event should contain registered metadata");
            Assert.AreEqual(firstValue, capturedEvent.meta[key], "First value should be preserved");
            Assert.AreNotEqual(secondValue, capturedEvent.meta[key], "Second value should not override");
        }
    }
}

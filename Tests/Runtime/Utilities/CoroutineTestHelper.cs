/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Coroutine Test Helper for ABXRLib Tests
 * 
 * Utilities for testing coroutines in PlayMode tests,
 * including waiting for conditions and handling async operations.
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Helper utilities for testing coroutines and async operations
    /// </summary>
    public static class CoroutineTestHelper
    {
        /// <summary>
        /// Waits for a coroutine to complete with timeout
        /// </summary>
        public static IEnumerator WaitForCoroutine(IEnumerator coroutine, float timeoutSeconds = 10.0f)
        {
            float elapsed = 0f;
            bool completed = false;
            
            // Start the coroutine
            var coroutineRunner = new GameObject("CoroutineTestRunner");
            var monoBehaviour = coroutineRunner.AddComponent<MonoBehaviour>();
            
            // Wrap the coroutine to track completion
            IEnumerator wrappedCoroutine = WrapCoroutine(coroutine, () => completed = true);
            monoBehaviour.StartCoroutine(wrappedCoroutine);
            
            // Wait for completion or timeout
            while (!completed && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            // Cleanup
            UnityEngine.Object.DestroyImmediate(coroutineRunner);
            
            if (elapsed >= timeoutSeconds)
            {
                Assert.Fail($"Coroutine did not complete within {timeoutSeconds} seconds");
            }
        }
        
        /// <summary>
        /// Wraps a coroutine to track completion
        /// </summary>
        private static IEnumerator WrapCoroutine(IEnumerator coroutine, Action onComplete)
        {
            yield return coroutine;
            onComplete();
        }
        
        /// <summary>
        /// Waits for a condition to be true with timeout
        /// </summary>
        public static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds = 5.0f)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (elapsed >= timeoutSeconds)
            {
                Assert.Fail($"Condition not met within {timeoutSeconds} seconds");
            }
        }
        
        /// <summary>
        /// Waits for a specific number of frames
        /// </summary>
        public static IEnumerator WaitForFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
            }
        }
        
        /// <summary>
        /// Waits for a specific amount of time
        /// </summary>
        public static IEnumerator WaitForSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
        
        /// <summary>
        /// Waits for the end of the current frame
        /// </summary>
        public static IEnumerator WaitForEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
        }
        
        /// <summary>
        /// Waits for the next frame
        /// </summary>
        public static IEnumerator WaitForNextFrame()
        {
            yield return null;
        }
        
        /// <summary>
        /// Waits for a specific number of events to occur
        /// </summary>
        public static IEnumerator WaitForEventCount<T>(System.Collections.Generic.List<T> eventList, int expectedCount, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => eventList.Count >= expectedCount, timeoutSeconds);
            Assert.AreEqual(expectedCount, eventList.Count, $"Expected {expectedCount} events, got {eventList.Count}");
        }
        
        /// <summary>
        /// Waits for a specific event to occur
        /// </summary>
        public static IEnumerator WaitForEvent<T>(System.Collections.Generic.List<T> eventList, Predicate<T> predicate, float timeoutSeconds = 5.0f)
        {
            yield return WaitForCondition(() => eventList.Exists(predicate), timeoutSeconds);
            Assert.IsTrue(eventList.Exists(predicate), "Expected event should have occurred");
        }
        
        /// <summary>
        /// Waits for authentication to complete
        /// </summary>
        public static IEnumerator WaitForAuthentication(bool expectedSuccess = true, float timeoutSeconds = 10.0f)
        {
            bool authCompleted = false;
            bool authSuccess = false;
            string authError = null;
            
            // Subscribe to auth completion event
            System.Action<bool, string> authHandler = (success, error) =>
            {
                authCompleted = true;
                authSuccess = success;
                authError = error;
            };
            
            // Note: This would need to be implemented in the actual Abxr class
            // Abxr.OnAuthCompleted += authHandler;
            
            // Wait for authentication to complete
            yield return WaitForCondition(() => authCompleted, timeoutSeconds);
            
            // Unsubscribe from event
            // Abxr.OnAuthCompleted -= authHandler;
            
            // Verify result
            if (expectedSuccess)
            {
                Assert.IsTrue(authSuccess, $"Authentication should succeed, but got error: {authError}");
            }
            else
            {
                Assert.IsFalse(authSuccess, "Authentication should fail");
                Assert.IsNotNull(authError, "Authentication error should not be null");
            }
        }
        
        /// <summary>
        /// Waits for module target to be received
        /// </summary>
        public static IEnumerator WaitForModuleTarget(string expectedTarget = null, float timeoutSeconds = 5.0f)
        {
            bool moduleReceived = false;
            string receivedTarget = null;
            
            // Subscribe to module target event
            System.Action<string> moduleHandler = (target) =>
            {
                moduleReceived = true;
                receivedTarget = target;
            };
            
            // Note: This would need to be implemented in the actual Abxr class
            // Abxr.OnModuleTarget += moduleHandler;
            
            // Wait for module target
            yield return WaitForCondition(() => moduleReceived, timeoutSeconds);
            
            // Unsubscribe from event
            // Abxr.OnModuleTarget -= moduleHandler;
            
            // Verify result
            Assert.IsTrue(moduleReceived, "Module target should be received");
            
            if (!string.IsNullOrEmpty(expectedTarget))
            {
                Assert.AreEqual(expectedTarget, receivedTarget, "Module target should match expected");
            }
        }
        
        /// <summary>
        /// Waits for data to be sent (simulated)
        /// </summary>
        public static IEnumerator WaitForDataSent(int expectedCount = 1, float timeoutSeconds = 5.0f)
        {
            // This would need to be implemented with actual data capture
            // For now, just wait a short time to simulate data sending
            yield return WaitForSeconds(0.1f);
        }
        
        /// <summary>
        /// Waits for storage operation to complete
        /// </summary>
        public static IEnumerator WaitForStorageOperation(System.Action callback, float timeoutSeconds = 5.0f)
        {
            bool operationCompleted = false;
            
            // Wrap the callback to track completion
            System.Action wrappedCallback = () =>
            {
                callback();
                operationCompleted = true;
            };
            
            // Execute the operation
            wrappedCallback();
            
            // Wait for completion
            yield return WaitForCondition(() => operationCompleted, timeoutSeconds);
        }
        
        /// <summary>
        /// Waits for AI proxy response
        /// </summary>
        public static IEnumerator WaitForAIResponse(System.Action<string> callback, float timeoutSeconds = 10.0f)
        {
            bool responseReceived = false;
            string response = null;
            
            // Wrap the callback to track completion
            System.Action<string> wrappedCallback = (result) =>
            {
                response = result;
                responseReceived = true;
                callback(result);
            };
            
            // Note: This would need to be implemented with actual AI proxy
            // For now, simulate a response
            yield return WaitForSeconds(0.5f);
            wrappedCallback("Mock AI response");
            
            // Wait for completion
            yield return WaitForCondition(() => responseReceived, timeoutSeconds);
        }
        
        /// <summary>
        /// Waits for exit poll response
        /// </summary>
        public static IEnumerator WaitForExitPollResponse(System.Action<string> callback, float timeoutSeconds = 5.0f)
        {
            bool responseReceived = false;
            string response = null;
            
            // Wrap the callback to track completion
            System.Action<string> wrappedCallback = (result) =>
            {
                response = result;
                responseReceived = true;
                callback(result);
            };
            
            // Note: This would need to be implemented with actual exit poll
            // For now, simulate a response
            yield return WaitForSeconds(0.2f);
            wrappedCallback("Mock poll response");
            
            // Wait for completion
            yield return WaitForCondition(() => responseReceived, timeoutSeconds);
        }
        
        /// <summary>
        /// Runs a test with timeout protection
        /// </summary>
        public static IEnumerator RunTestWithTimeout(IEnumerator testCoroutine, float timeoutSeconds = 30.0f)
        {
            float elapsed = 0f;
            bool testCompleted = false;
            
            // Start the test coroutine
            var testRunner = new GameObject("TestRunner");
            var monoBehaviour = testRunner.AddComponent<MonoBehaviour>();
            
            // Wrap the test to track completion
            IEnumerator wrappedTest = WrapCoroutine(testCoroutine, () => testCompleted = true);
            monoBehaviour.StartCoroutine(wrappedTest);
            
            // Wait for completion or timeout
            while (!testCompleted && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            // Cleanup
            UnityEngine.Object.DestroyImmediate(testRunner);
            
            if (elapsed >= timeoutSeconds)
            {
                Assert.Fail($"Test did not complete within {timeoutSeconds} seconds");
            }
        }
    }
}

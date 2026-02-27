/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Test initializer (TakeTwo design). No test-mode or PIN injection; auth is handled by the subsystem.
 * Kept for compatibility; can be used to log or set test-only config if needed.
 */

using UnityEngine;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Runs before scene load when tests are present. TakeTwo: no test authentication provider;
    /// tests rely on valid Configuration and subsystem auth.
    /// </summary>
    public static class TestInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Debug.Log("TestInitializer: Test assembly loaded (TakeTwo - no test mode)");
        }
    }
}

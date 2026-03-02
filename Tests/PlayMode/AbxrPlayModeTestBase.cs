// Copyright (c) 2026 ArborXR. All rights reserved.
// Base class providing per-test setup/teardown for all PlayMode test fixtures.
// Creates a fresh AbxrSubsystem with a controlled Configuration before each test
// and tears it all down cleanly afterward.
using System.Collections.Generic;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;

public class AbxrPlayModeTestBase
{
    protected GameObject SubsystemGO;

    [SetUp]
    public void BaseSetUp()
    {
        // Clear any static event subscriptions left by previous tests.
        Abxr.OnAuthCompleted = null;
        Abxr.OnModuleTarget = null;
        Abxr.OnAllModulesCompleted = null;
        Abxr.OnHeadsetPutOnNewSession = null;

        // Reset static fields on AbxrSubsystem that survive MonoBehaviour destruction.
        AbxrSubsystem.ResetStaticStateForTesting();

        // Create a fresh Configuration singleton for this test.
        Configuration.ResetForTesting();

        // Configuration.Instance creates a default instance and runs MigrateIfNeeded().
        // Migration flips enableAutoStartAuthentication/Telemetry/SceneEvents from true → false,
        // which is exactly what we want for tests.
        var config = Configuration.Instance;
        config.appID = "12345678-1234-1234-1234-123456789012";

        // Disable platform-specific services that aren't available in the editor.
        config.enableArborServiceClient = false;
        config.enableArborInsightServiceClient = false;

        // Disable auto-module behaviours so tests control module flow explicitly.
        config.enableAutoStartModules = false;
        config.enableAutoAdvanceModules = false;

        // Never quit the test runner process.
        config.returnToLauncherAfterAssessmentComplete = false;

        // Create the subsystem – Awake() runs synchronously, setting AbxrSubsystem.Instance.
        SubsystemGO = new GameObject("[Test] AbxrSubsystem");
        SubsystemGO.AddComponent<AbxrSubsystem>();

        // If Unit Test Credentials are configured, auto-respond to auth input requests so tests
        // that exercise the real StartAuthentication() flow don't stall waiting for user input.
#if UNITY_EDITOR
        if (config.unitTestConfigEnabled)
        {
            Abxr.OnInputRequested = (type, prompt, domain, error) =>
            {
                string value = type switch
                {
                    "email" => config.unitTestAuthEmail,
                    "pin"   => config.unitTestAuthPin,
                    _       => config.unitTestAuthText
                };
                Abxr.OnInputSubmitted(value);
            };
        }
#endif
    }

    [TearDown]
    public void BaseTearDown()
    {
        // DestroyImmediate calls OnDestroy synchronously, which sets AbxrSubsystem.Instance = null.
        if (SubsystemGO != null)
            Object.DestroyImmediate(SubsystemGO);

        // Clean up statics so the next test starts fresh.
        Configuration.ResetForTesting();
        AbxrSubsystem.ResetStaticStateForTesting();
        Abxr.OnAuthCompleted = null;
        Abxr.OnModuleTarget = null;
        Abxr.OnAllModulesCompleted = null;
        Abxr.OnHeadsetPutOnNewSession = null;
    }

    // ── Auth simulation helpers ───────────────────────────────────────────

    /// <summary>
    /// Simulates a successful authentication using the provided (or a default) response.
    /// Fires OnSucceeded on the auth service, which triggers HandleAuthCompleted in the subsystem
    /// and fires Abxr.OnAuthCompleted — identical to what a real HTTP auth would do.
    /// </summary>
    protected void SimulateAuth(AuthResponse response = null)
    {
        AbxrSubsystem.Instance.AuthServiceForTesting
            .SimulateAuthSuccess(response ?? BuildTestAuthResponse());
    }

    protected static AuthResponse BuildTestAuthResponse(
        string userId = "test-user-id",
        Dictionary<string, string> userData = null,
        List<ModuleData> modules = null)
    {
        return new AuthResponse
        {
            Token = "test-token-abc123",
            Secret = "test-secret",
            UserId = userId,
            UserData = userData ?? new Dictionary<string, string>
            {
                ["email"] = "test@example.com",
                ["displayName"] = "Test User"
            },
            AppId = "12345678-1234-1234-1234-123456789012",
            Modules = modules ?? new List<ModuleData>()
        };
    }
}

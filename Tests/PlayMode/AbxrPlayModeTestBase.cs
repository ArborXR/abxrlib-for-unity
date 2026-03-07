// Copyright (c) 2026 ArborXR. All rights reserved.
// Base class providing per-test setup/teardown for all PlayMode test fixtures.
// Creates a fresh AbxrSubsystem with a controlled Configuration before each test
// and tears it all down cleanly afterward.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;

public class AbxrPlayModeTestBase
{
    protected GameObject SubsystemGO;

    /// <summary>Override true to allow ArborInsightsClient transport in tests (e.g. when running on device). Default false so Editor tests use REST and can inspect pending events.</summary>
    protected virtual bool AllowArborInsightsClientInTests => false;

    /// <summary>Set to true in a test to have TearDown call OnApplicationQuitHandler() (close running events, send or unbind) before EndSession(). Default false.</summary>
    protected bool RunQuitHandlerInTearDown { get; set; }

    /// <summary>Set to false in a test to skip Abxr.EndSession() in TearDown (e.g. to test quit-without-EndSession behavior). Default true.</summary>
    protected bool RunEndSessionInTearDown { get; set; } = true;

    // Config field name -> saved value; restore in TearDown so test overrides are not persisted.
    private Dictionary<string, object> _savedConfig = new Dictionary<string, object>();

    [SetUp]
    public void BaseSetUp()
    {
        // Remove any subsystem created by Initialize.OnBeforeSceneLoad so only our test subsystem runs.
        // That one starts auth with project config and would request input before we set OnInputRequested;
        // its OnAuthCompleted(false) can fire later and overwrite our success. Find by type so we never miss it.
        foreach (var existing in UnityEngine.Object.FindObjectsOfType<AbxrSubsystem>())
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        // Clear any static event subscriptions left by previous tests.
        Abxr.OnAuthCompleted = null;
        Abxr.OnModuleTarget = null;
        Abxr.OnAllModulesCompleted = null;
        Abxr.OnHeadsetPutOnNewSession = null;

        // Reset static fields on AbxrSubsystem that survive MonoBehaviour destruction.
        AbxrSubsystem.ResetStaticStateForTesting();

        // Create a fresh Configuration singleton for this test.
        Configuration.ResetForTesting();

        // Configuration.Instance creates a default instance and runs MigrateIfNeeded() (needed for other settings).
        _ = Configuration.Instance;

        // Apply enableAutoStartAuthentication = false via RuntimeAuthConfig when the subsystem is created, so we never modify the Configuration asset (avoids leaving the user's config file changed if TearDown doesn't run).
        AbxrSubsystem.NextRuntimeAuthConfigForTesting = new RuntimeAuthConfig { enableAutoStartAuthentication = false };

        // Subsystem creation can be deferred by derived fixtures (e.g. first-stage auth tests set config then create).
        CreateSubsystemIfNeeded();

        // In PlayMode tests, when Unit Test Credentials are enabled we auto-respond to auth input using configured values.
        // Set handler here so the subsystem created in SetUp gets it. For fixtures that create the subsystem in the test
        // (e.g. AuthenticationFirstStageTests), we set it again in PerformAuth so the current subsystem receives it.
        Abxr.OnInputRequested = GetUnitTestInputRequestedHandler();
    }

    /// <summary>Handler that submits Unit Test Credentials when auth requests input. Used so PerformAuth can re-assign it to the current subsystem (which may have been created in the test).</summary>
    private static Action<string, string, string, string> GetUnitTestInputRequestedHandler()
    {
        return (type, prompt, domain, error) =>
        {
            var c = Configuration.Instance;
            if (c == null || !c.unitTestConfigEnabled)
            {
                Assert.Fail("Auth requested input but Unit Test Credentials are disabled. Enable \"Unit Test Credentials (Editor only)\" in the AbxrLib config and set the PIN/email/text values you need, then save the project.");
                return;
            }
            string value = type switch
            {
                "email" => c.unitTestAuthEmail,
                "pin" or "assessmentPin" => c.unitTestAuthPin,
                _ => c.unitTestAuthText
            };
            if (string.IsNullOrEmpty(value))
            {
                Assert.Fail($"Auth requested input type=\"{type}\" but the corresponding Unit Test Credentials value is not set. In the AbxrLib config asset, set unitTestAuthPin (for pin/assessmentPin), unitTestAuthEmail (for email), or unitTestAuthText (for text), then save the project.");
                return;
            }
            Debug.Log($"[AbxrPlayModeTestBase] OnInputRequested: type=" + type + ", prompt=" + prompt + ", submitting configured value " + value + ")");
            Abxr.OnInputSubmitted(value);
        };
    }

    /// <summary>Saves the current config field value and sets the new one; TearDown restores from _savedConfig.</summary>
    private void ModifyConfig(Configuration config, string fieldName, object newValue)
    {
        var field = typeof(Configuration).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return;
        if (!_savedConfig.ContainsKey(fieldName))
            _savedConfig[fieldName] = field.GetValue(config);
        field.SetValue(config, newValue);
    }

    /// <summary>Derived tests can alter config per test (e.g. enableArborMdmClient). For auth-related values prefer SetRuntimeAuth so the Configuration asset is not modified. Restored in TearDown.</summary>
    protected void ModifyConfig(string fieldName, object newValue)
    {
        if (Configuration.Instance == null) return;
        ModifyConfig(Configuration.Instance, fieldName, newValue);
    }

    /// <summary>Sets the runtime auth config used by the next Authenticate() call. Call after CreateSubsystem(). Avoids mutating Configuration; use for auth first-stage and similar tests.</summary>
    protected void SetRuntimeAuth(RuntimeAuthConfig config)
    {
        if (config == null || AbxrSubsystem.Instance == null) return;
        AbxrSubsystem.Instance.AuthServiceForTesting.SetRuntimeAuthForTesting(config);
    }

    /// <summary>Creates the test subsystem GameObject and AbxrSubsystem. Called from SetUp by default; first-stage tests call after setting config.</summary>
    protected void CreateSubsystem()
    {
        SubsystemGO = new GameObject("[Test] AbxrSubsystem");
        SubsystemGO.AddComponent<AbxrSubsystem>();
    }

    /// <summary>Override to skip subsystem creation in SetUp (e.g. first-stage tests create after altering config).</summary>
    protected virtual void CreateSubsystemIfNeeded()
    {
        CreateSubsystem();
    }

    /// <summary>True when running on Android device (not Editor). Use to skip tests that require ArborMdmClient/ArborInsightsClient.</summary>
    protected static bool IsRunningOnAndroidDevice()
    {
        return Application.platform == RuntimePlatform.Android && !Application.isEditor;
    }

    /// <summary>TearDown runs after each test whether it passed, failed, or threw—so config restore is reliable. Must be void (Unity Test Runner does not support IEnumerator TearDown).</summary>
    [TearDown]
    public void BaseTearDown()
    {
        // Restore all config values we changed so the asset is not left with test settings (avoids affecting normal builds).
        if (Configuration.Instance != null && _savedConfig.Count > 0)
        {
            var configType = typeof(Configuration);
            foreach (var kv in _savedConfig)
            {
                var field = configType.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    field.SetValue(Configuration.Instance, kv.Value);
            }
            _savedConfig.Clear();
        }

        // Clear any injected runtime auth so the next test does not inherit it.
        if (AbxrSubsystem.Instance != null)
            AbxrSubsystem.Instance.AuthServiceForTesting.ClearRuntimeAuthInjectionForTesting();

        // Optionally run quit handler first (close running events, send); then optionally end session (close, send, clear).
        if (AbxrSubsystem.Instance != null)
        {
            if (RunQuitHandlerInTearDown)
            {
                RunQuitHandlerInTearDown = false;
                Debug.Log("BaseTearDown: OnApplicationQuitHandler");
                AbxrSubsystem.Instance.OnApplicationQuitHandler();
            }
            if (RunEndSessionInTearDown)
            {
                RunEndSessionInTearDown = true;
                Debug.Log("BaseTearDown: Abxr.EndSession");
                Abxr.EndSession();
            }
        }
        RunQuitHandlerInTearDown = false;
        RunEndSessionInTearDown = true;

        // DestroyImmediate calls OnDestroy synchronously, which sets AbxrSubsystem.Instance = null.
        if (SubsystemGO != null)
            UnityEngine.Object.DestroyImmediate(SubsystemGO);

        // Clean up statics so the next test starts fresh.
        Configuration.ResetForTesting();
        AbxrSubsystem.ResetStaticStateForTesting();
        Abxr.OnAuthCompleted = null;
        Abxr.OnModuleTarget = null;
        Abxr.OnAllModulesCompleted = null;
        Abxr.OnHeadsetPutOnNewSession = null;
    }

    // ── Auth and session helpers ────────────────────────────────────────────

    /// <summary>
    /// Runs the real auth flow: waits for scene to settle, then StartAuthentication(), wait for OnAuthCompleted, invoke onComplete(success).
    /// Assigns the unit-test OnInputRequested handler before starting auth so the current subsystem (e.g. one created in the test) receives it.
    /// </summary>
    /// <param name="onComplete">Called with auth success/failure.</param>
    /// <param name="timeoutSeconds">Max time to wait for OnAuthCompleted.</param>
    /// <param name="sceneLoadWaitSeconds">Seconds to wait at the start so the scene can fully load (default 5).</param>
    protected IEnumerator PerformAuth(Action<bool> onComplete, float timeoutSeconds = 30f, float sceneLoadWaitSeconds = 5f)
    {
        var runner = SubsystemGO != null ? SubsystemGO.GetComponent<AbxrSubsystem>() : null;
        if (runner == null || onComplete == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        yield return new WaitForSeconds(sceneLoadWaitSeconds);

        bool authCompletedReceived = false;
        bool authSuccess = false;
        var prevHandler = Abxr.OnAuthCompleted;

        Abxr.OnAuthCompleted += (success, _) =>
        {
            // Only accept the result from our subsystem; another (e.g. from Initialize) may still fire OnAuthCompleted(false).
            if (AbxrSubsystem.Instance != runner)
                return;
            authSuccess = success;
            authCompletedReceived = true;
            prevHandler?.Invoke(success, null);
        };

        // Ensure current subsystem has the unit-test input handler (critical when subsystem was created in the test, e.g. AuthenticationFirstStageTests).
        Abxr.OnInputRequested = GetUnitTestInputRequestedHandler();
        Abxr.StartAuthentication();

        float elapsed = 0f;
        while (!authCompletedReceived && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Abxr.OnAuthCompleted = prevHandler;

        if (!authCompletedReceived)
        {
            Assert.Fail($"PerformAuth: OnAuthCompleted was not invoked within {timeoutSeconds}s.");
            onComplete(false);
            yield break;
        }

        onComplete(authSuccess);
    }

    /// <summary>
    /// Ends the current session (send all data, close running events, clear session state). Does not start a new session.
    /// Call Abxr.StartAuthentication() when ready for a fresh session.
    /// </summary>
    protected void RunEndSession()
    {
        Abxr.EndSession();
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

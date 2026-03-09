// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for authentication first-stage: config + validation outcome (succeed/fail).
// Uses RuntimeAuthConfig via SetRuntimeAuth so Configuration is not modified.
// All tests set authMechanism type to "none" so no second-stage PIN/input is requested.
// When a value is "set" (not empty/invalid), use the Configuration asset so tests run with project credentials.
//
// Environment: A = Unity TestRunner play mode (no ArborMdmClient). B = Android headset without MDM. C = Headset with ArborMdmClient.
// Outcome: _Fails = fails in all environments (e.g. missing appId/appToken or invalid format).  = fails in A/B (org credentials unavailable), succeeds in C (MDM supplies org). _Succeeds = has full credentials from config/overrides.
using System.Collections;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthenticationFirstStageTests : AbxrPlayModeTestBase
{
    /// <summary>Auth values from Configuration; use when the test scenario expects the field to be set (not empty/invalid).</summary>
    internal static string ConfigAppId => Configuration.Instance?.appID ?? "";
    internal static string ConfigOrgId => Configuration.Instance?.orgID ?? "";
    internal static string ConfigAuthSecret => Configuration.Instance?.authSecret ?? "";
    internal static string ConfigAppToken => Configuration.Instance?.appToken ?? "";
    internal static string ConfigOrgToken => Configuration.Instance?.orgToken ?? "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static string ConfigDeviceId => Configuration.Instance?.unitTestDeviceId ?? "";
    internal static string ConfigFingerprint => Configuration.Instance?.unitTestFingerprint ?? "";
#else
    internal static string ConfigDeviceId => "";
    internal static string ConfigFingerprint => "";
#endif

    // Expected auth failure messages (for LogAssert.Expect and assertions). Outcome may depend on IsArborMdmClientAvailableAndConnected.
    internal const string AuthFailureAppIdNotSet = "[AbxrLib] Authentication failure: App identification not set.";
    internal const string AuthFailureOrgUnavailable = "[AbxrLib] Authentication failure: Organization identification unavailable.";
    internal const string AuthFailureInitialRequestFailed = "[AbxrLib] Authentication failure: [AbxrLib] Initial authentication request failed";

    /// <summary>Auth mechanism "none" so first-stage tests skip second-stage PIN/input.</summary>
    private static AuthMechanism AuthMechanismNone => new AuthMechanism { type = "none", prompt = "", domain = "" };

    /// <summary>Auth mechanism assessmentPin for handoff tests (launcher/receiver flow may request PIN).</summary>
    private static AuthMechanism AuthMechanismAssessmentPin => new AuthMechanism { type = "assessmentPin", prompt = "Enter your 6-digit PIN", domain = "" };

    protected override void CreateSubsystemIfNeeded()
    {
        // First-stage tests set runtime auth then call CreateSubsystem() in each test.
    }

    /// <summary>Package name for handoff tests: from GetAuthResponse().PackageName when set, otherwise "com.example.app2". Logs when using response value.</summary>
    private static string GetHandoffTargetPackageName()
    {
        var response = Abxr.GetAuthResponse();
        string name = !string.IsNullOrEmpty(response?.PackageName) ? response.PackageName : "com.example.app2";
        if (response != null && !string.IsNullOrEmpty(response.PackageName))
            Debug.Log("[AbxrLib] (Test) Using PackageName from auth response: " + response.PackageName);
        return name;
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_NoIds_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "development", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_AppIdOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = "", authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (orgId/authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_AppIdOrgId()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_FullConfig_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success, "With full legacy credentials (appId, orgId, authSecret) in development, auth should succeed.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_NoIds_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = "", authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (orgId/authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOrgId()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_FullConfig()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (production accepts org from device).");
        else
            Assert.IsFalse(success, "Without MDM, production rejects org from config so auth fails.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_AppIdOnly_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = "", authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (orgId/authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_AppIdOrgId_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
        bool mdmCanSupplyCredentials = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyCredentials)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyCredentials)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (authSecret from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_FullConfig_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Dev_NoAppToken_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "development", appToken = "", orgToken = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Dev_AppTokenOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "development", appToken = ConfigAppToken, orgToken = "" });
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyOrgToken)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (orgToken from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Dev_BothTokens_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "development", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_NoAppToken_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = "", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_OrgTokenOnly_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = "", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_AppTokenOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = "" });
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyOrgToken)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (dynamic orgToken from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_BothTokens()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyOrgToken)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (production uses orgToken from device).");
        else
            Assert.IsFalse(success, "Without MDM, production ignores orgToken from config so auth fails.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_ProdCustom_AppTokenOnly_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = "" });
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyOrgToken)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (org token/credentials from device).");
        else
            Assert.IsFalse(success, "Without MDM, auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_ProdCustom_BothTokens_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOnly_WithOverrides_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = "", authSecret = "" });
        Abxr.SetOrgId(ConfigOrgId);
        Abxr.SetAuthSecret(ConfigAuthSecret);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_AppTokenOnly_WithOverrides()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = "" });
        Abxr.SetOrgId(ConfigOrgId);
        bool hasUnitTestDeviceCredentials = !string.IsNullOrEmpty(ConfigDeviceId) && !string.IsNullOrEmpty(ConfigFingerprint);
        if (hasUnitTestDeviceCredentials)
        {
            Abxr.SetDeviceId(ConfigDeviceId);
            Abxr.SetAuthSecret(ConfigFingerprint);
        }
        else
        {
            Abxr.SetAuthSecret(ConfigAuthSecret);
        }
        
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        // With unitTestDeviceId & unitTestFingerprint set: we build a valid dynamic org token from overrides → auth can pass. Without them we use configured authSecret + random device ID → backend rejects the token → auth fails.
        if (!mdmCanSupplyOrgToken && !hasUnitTestDeviceCredentials)
        {
            if (Application.isEditor)
                LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
            else
                LogAssert.Expect(LogType.Error, AuthFailureInitialRequestFailed);
        }
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (dynamic orgToken).");
        else if (hasUnitTestDeviceCredentials)
            Assert.IsTrue(success, "With unit test device ID and fingerprint set, dynamic org token from overrides should allow auth to succeed.");
        else
            Assert.IsFalse(success, "Without MDM or unit test device credentials, auth should fail.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_InvalidAppTokenFormat_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = true, buildType = "production", appToken = "not-a-jwt", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_InvalidAppIdFormat_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismNone, useAppTokens = false, buildType = "development", appId = "not-a-valid-uuid", orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    // ── Auth handoff (App 1 → App 2 → return to App 1) ─────────────────────
    // The only bridge between the two emulated apps/APKs is the auth_handoff intent payload (the JSON).
    // We use full teardown + full base setup so each "app" is otherwise isolated.
    // Handoff tests use authMechanism = AuthMechanismAssessmentPin so the PIN flow runs where needed (unit test handler submits configured PIN).

    /// <summary>
    /// Simulates auth_handoff flow: App 1 authenticates, gets PackageName from response, stores handoff JSON,
    /// full teardown (equivalent to app/APK exit), then "launches App 2" with full base setup and injected handoff;
    /// App 2 adopts the session via StartAuthentication() and CheckAuthHandoff. Finally we simulate return to
    /// App 1 (full teardown + full base setup, no handoff) and assert we are in starting state.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App2AdoptsSession_ThenReturnToApp1_IsStartingState()
    {
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // App 2 has enableReturnTo; EventAssessmentComplete would trigger exit path
        // ── App 1: normal auth flow (same as other tests) until success ───────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);

        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        Assert.IsTrue(Abxr.GetAuthResponse() != null, "App 1 should have auth response after success.");

        string packageName = GetHandoffTargetPackageName();
        // Same flow as LaunchAppWithAuthHandoff: build handoff and "deliver" it. For test we inject instead of starting an app.
        bool launched = AbxrSubsystem.Instance.LaunchAppWithAuthHandoffForTest(packageName);
        Assert.IsTrue(launched, "LaunchAppWithAuthHandoffForTest should succeed when authenticated (same validation as LaunchAppWithAuthHandoff).");
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2.");
        BaseSetUpForAppSwitch();

        // ── App 2: fresh subsystem, receive handoff and adopt session; enableReturnTo so exit/return rule applies ───────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app2AuthCompleted = false;
        bool app2Success = false;
        Abxr.OnAuthCompleted += (success, _) => { app2AuthCompleted = true; app2Success = success; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!app2AuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app2AuthCompleted, "App 2 should receive OnAuthCompleted.");
        Assert.IsTrue(app2Success, "App 2 should adopt session via handoff.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "Session should be marked as handoff.");

        // Simulate App 2 doing work (e.g. assessment complete); then "user leaves" and returns to App 1.
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // set here so not cleared by FullTeardown/BaseSetUp; exit coroutine runs after 2s
        Debug.Log("[AbxrLib] (Test) EventAssessmentComplete: handoff-test-assessment score: 85 result: Abxr.EventStatus.Pass");
        Abxr.EventAssessmentComplete("handoff-test-assessment", 85, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(2.5f); // let ExitAfterAssessmentComplete run (no returnToPackage → SendAll, 2s delay, simulated quit)

        // ── Return to App 1: full teardown (clear handoff) + full base setup ──────
        FullTeardownForAppSwitch(clearAuthHandoff: true);
        Debug.Log("[AbxrLib] (Test) Leaving App 2, returning to App 1.");
        BaseSetUpForAppSwitch();
        CreateSubsystem();
        AssignUnitTestInputRequestedHandler(); // so PIN request is auto-submitted and OnAuthCompleted fires
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app1ReturnAuthCompleted = false;
        bool app1ReturnSuccess = false;
        Abxr.OnAuthCompleted += (success, _) => { app1ReturnAuthCompleted = true; app1ReturnSuccess = success; };
        Abxr.StartAuthentication();
        deadline = Time.realtimeSinceStartup + 35f;
        while (!app1ReturnAuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app1ReturnAuthCompleted, "Return to App 1 should eventually complete auth flow (success or input/failure).");
        // We expect either full success (if unit test credentials auto-submit) or that we are not authenticated via handoff.
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "Return to App 1 should not have used handoff; we are in starting state.");
    }

    private const string EndingSessionLog = "[AbxrLib] Ending session: closing running events and flushing";

    /// <summary>
    /// Simulates auth_handoff flow: App 1 authenticates, gets PackageName from response, stores handoff JSON,
    /// full teardown (equivalent to app/APK exit), then "launches App 2" with full base setup and injected handoff;
    /// App 2 adopts session via auth handoff with no return-to (handoff without includeReturnToPackage).
    /// With enableReturnTo true, EventAssessmentComplete (last-module / no-module path) triggers the exit check;
    /// returnToPackage is not set, so the app quits (SendAll, delay, then quit). We assert the on-quit handler message is logged.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App2AdoptsSession_NoReturnTo_QuitApp()
    {
        RunEndSessionInTearDown = false; // quit is triggered by EventAssessmentComplete exit path; avoid second log in TearDown
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // don't stop play mode; simulate quit so test completes
        // ── App 1: auth and hand off to App 2 without ReturnToPackage ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);
        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        string packageName = GetHandoffTargetPackageName();
        bool launched = AbxrSubsystem.Instance.LaunchAppWithAuthHandoffForTest(packageName); // no includeReturnToPackage
        Assert.IsTrue(launched, "LaunchAppWithAuthHandoffForTest should succeed.");
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2 (no return-to).");
        BaseSetUpForAppSwitch();

        // ── App 2: receive handoff, adopt session; enableReturnTo true so EventAssessmentComplete triggers exit path ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app2AuthCompleted = false;
        bool app2Success = false;
        Abxr.OnAuthCompleted += (success, _) => { app2AuthCompleted = true; app2Success = success; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!app2AuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app2AuthCompleted, "App 2 should receive OnAuthCompleted.");
        Assert.IsTrue(app2Success, "App 2 should adopt session via handoff.");

        // Last "module" complete: session used handoff + enableReturnTo true → exit path runs; no returnToPackage → app quits; on-quit handler logs.
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // set here so not cleared by FullTeardown/BaseSetUp; exit coroutine runs after 2s
        LogAssert.Expect(LogType.Log, EndingSessionLog);
        Debug.Log("[AbxrLib] (Test) EventAssessmentComplete: handoff-test-re-adopt score: 85 result: Abxr.EventStatus.Pass");
        Abxr.EventAssessmentComplete("handoff-test-re-adopt", 85, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(2.5f); // allow ExitAfterAssessmentComplete coroutine to run (SendAll, 2s delay, then simulated quit → OnApplicationQuit → handler log)
    }

    /// <summary>
    /// App 1 hands off to App 2 with includeReturnToPackage so the handoff contains ReturnToPackage. App 2 has enableReturnTo = false,
    /// so EventAssessmentComplete() does not trigger the exit/return path (shouldExitOrReturn is false). We assert App 2 stays authenticated
    /// and no exit/quit runs (no need to simulate quit or wait).
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App2AdoptsSession_IgnoresReturnTo_QuitApp()
    {
        // ── App 1: auth and hand off to App 2 with ReturnToPackage ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);
        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        string packageName = GetHandoffTargetPackageName();
        bool launched = AbxrSubsystem.Instance.LaunchAppWithAuthHandoffForTest(packageName, includeReturnToPackage: true);
        Assert.IsTrue(launched, "LaunchAppWithAuthHandoffForTest(includeReturnToPackage) should succeed.");
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2 (auth_handoff with ReturnToPackage).");
        BaseSetUpForAppSwitch();

        // ── App 2: receive handoff (with ReturnToPackage), adopt session; enableReturnTo false so exit/return path is NOT triggered ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = false });
        bool app2AuthCompleted = false;
        bool app2Success = false;
        Abxr.OnAuthCompleted += (success, _) => { app2AuthCompleted = true; app2Success = success; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!app2AuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app2AuthCompleted, "App 2 should receive OnAuthCompleted.");
        Assert.IsTrue(app2Success, "App 2 should adopt session via handoff.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "App 2 session should be marked as handoff.");

        // With enableReturnTo false, EventAssessmentComplete does not start ExitAfterAssessmentComplete; app does not quit or return handoff.
        Abxr.EventAssessmentComplete("handoff-test-ignores-return-to", 85, Abxr.EventStatus.Pass);
        yield return null;

        Assert.IsTrue(Abxr.GetAuthResponse() != null, "App 2 should still be authenticated after EventAssessmentComplete.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "App 2 should still have session from handoff.");
    }

    /// <summary>
    /// App 1 hands off to App 2 with includeReturnToPackage so App 2 has ReturnToPackage. App 2 does EventAssessmentComplete(),
    /// which triggers ExitAfterAssessmentComplete(). That sees returnToPackage and (in Editor) calls LaunchAppWithAuthHandoffForTest()
    /// to inject the handoff for the return-to launcher; then App 2 would quit (we simulate quit so the test runner does not exit).
    /// We teardown/setup without clearing handoff so the injected handoff is still present; the new "App 1" subsystem adopts the session via handoff (no PIN).
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App2AdoptsSession_ThenReturnToApp1_App1ReAdoptsSession()
    {
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // App 2 would quit after handoff; we simulate so test continues
        // ── App 1: auth and hand off to App 2 with ReturnToPackage (JSON payload) ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);
        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        Assert.IsTrue(Abxr.GetAuthResponse() != null, "App 1 should have auth response after success.");

        string packageName = GetHandoffTargetPackageName();
        bool launched = AbxrSubsystem.Instance.LaunchAppWithAuthHandoffForTest(packageName, includeReturnToPackage: true);
        Assert.IsTrue(launched, "LaunchAppWithAuthHandoffForTest(includeReturnToPackage) should succeed.");
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2 (auth_handoff with ReturnToPackage).");
        BaseSetUpForAppSwitch();

        // ── App 2: receive handoff (with ReturnToPackage), adopt session; enableReturnTo so exit/return path runs on EventAssessmentComplete ─
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app2AuthCompleted = false;
        bool app2Success = false;
        Abxr.OnAuthCompleted += (success, _) => { app2AuthCompleted = true; app2Success = success; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!app2AuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app2AuthCompleted, "App 2 should receive OnAuthCompleted.");
        Assert.IsTrue(app2Success, "App 2 should adopt session via handoff.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "App 2 session should be marked as handoff.");

        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // set here so not cleared by FullTeardown/BaseSetUp; exit coroutine runs after 2s
        // ReturnToPackage in the handoff is the launcher's Application.identifier (set when App 1 built the handoff); on Test Runner Player that is com.UnityTestRunner.UnityTestRunner.
        LogAssert.Expect(LogType.Log, $"[AbxrLib] Injected handoff for return-to launcher '{Application.identifier}' (Editor/test).");
        Debug.Log("[AbxrLib] (Test) EventAssessmentComplete: handoff-test-re-adopt score: 85 result: Abxr.EventStatus.Pass");
        Abxr.EventAssessmentComplete("handoff-test-re-adopt", 85, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(2.5f); // let ExitAfterAssessmentComplete run: returnToPackage set → LaunchAppWithAuthHandoffForTest injects handoff (Editor), SendAll, 2s delay, simulated quit

        // ── Return to App 1: teardown/setup; do not clear handoff so the handoff injected by ExitAfterAssessmentComplete is still there for the new "App 1" ─
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 2, returning to App 1 (with handoff).");
        BaseSetUpForAppSwitch();

        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app1ReAdoptCompleted = false;
        bool app1ReAdoptSuccess = false;
        Abxr.OnAuthCompleted += (success, _) => { app1ReAdoptCompleted = true; app1ReAdoptSuccess = success; };
        Abxr.StartAuthentication();
        deadline = Time.realtimeSinceStartup + 5f;
        while (!app1ReAdoptCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app1ReAdoptCompleted, "Return to App 1 should receive OnAuthCompleted.");
        Assert.IsTrue(app1ReAdoptSuccess, "App 1 should re-adopt session via handoff (no PIN).");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "App 1 should have adopted session via handoff when returning.");
    }

    /// <summary>
    /// Same flow as AuthHandoff_App2AdoptsSession_ThenReturnToApp1_IsStartingState but injects the auth_handoff
    /// payload as base64-encoded JSON so that NormalizeHandoffPayload decodes it and handoff still succeeds.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App2AdoptsSession_ThenReturnToApp1_IsStartingState_base64()
    {
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // App 2 has enableReturnTo; EventAssessmentComplete would trigger exit path
        // ── App 1: normal auth flow until success ─────────────────────────────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);

        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        Assert.IsTrue(Abxr.GetAuthResponse() != null, "App 1 should have auth response after success.");

        string packageName = GetHandoffTargetPackageName();
        bool launched = AbxrSubsystem.Instance.LaunchAppWithAuthHandoffForTest(packageName, useBase64Encoding: true);
        Assert.IsTrue(launched, "LaunchAppWithAuthHandoffForTest(base64) should succeed when authenticated.");
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2 (auth_handoff as base64).");
        BaseSetUpForAppSwitch();

        // ── App 2: receive base64 handoff, normalize to JSON, adopt session; enableReturnTo so exit/return rule applies ────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app2AuthCompleted = false;
        bool app2Success = false;
        Abxr.OnAuthCompleted += (success, _) => { app2AuthCompleted = true; app2Success = success; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!app2AuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app2AuthCompleted, "App 2 should receive OnAuthCompleted.");
        Assert.IsTrue(app2Success, "App 2 should adopt session via base64-decoded handoff.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "Session should be marked as handoff.");

        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // set here so not cleared by FullTeardown/BaseSetUp; exit coroutine runs after 2s
        Debug.Log("[AbxrLib] (Test) EventAssessmentComplete: handoff-test-assessment-base64 score: 85 result: Abxr.EventStatus.Pass");
        Abxr.EventAssessmentComplete("handoff-test-assessment-base64", 85, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(2.5f); // let ExitAfterAssessmentComplete run (no returnToPackage → SendAll, 2s delay, simulated quit)

        // ── Return to App 1 ───────────────────────────────────────────────────
        FullTeardownForAppSwitch(clearAuthHandoff: true);
        Debug.Log("[AbxrLib] (Test) Leaving App 2, returning to App 1.");
        BaseSetUpForAppSwitch();
        CreateSubsystem();
        AssignUnitTestInputRequestedHandler();
        SetRuntimeAuth(new RuntimeAuthConfig { authMechanism = AuthMechanismAssessmentPin, useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken, enableReturnTo = true });

        bool app1ReturnAuthCompleted = false;
        bool app1ReturnSuccess = false;
        Abxr.OnAuthCompleted += (success, _) => { app1ReturnAuthCompleted = true; app1ReturnSuccess = success; };
        Abxr.StartAuthentication();
        deadline = Time.realtimeSinceStartup + 35f;
        while (!app1ReturnAuthCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(app1ReturnAuthCompleted, "Return to App 1 should eventually complete auth flow.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "Return to App 1 should not have used handoff; we are in starting state.");
    }
}

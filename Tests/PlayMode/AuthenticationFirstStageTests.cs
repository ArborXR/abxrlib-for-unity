// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for authentication first-stage: config + validation outcome (succeed/fail).
// Uses RuntimeAuthConfig via SetRuntimeAuth so Configuration is not modified.
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

    // Expected auth failure messages (for LogAssert.Expect and assertions). Outcome may depend on IsArborMdmClientAvailableAndConnected.
    internal const string AuthFailureAppIdNotSet = "[AbxrLib] Authentication failure: App identification not set.";
    internal const string AuthFailureOrgUnavailable = "[AbxrLib] Authentication failure: Organization identification unavailable.";
    internal const string AuthFailureInitialRequestFailed = "[AbxrLib] Authentication failure: [AbxrLib] Initial authentication request failed";

    protected override void CreateSubsystemIfNeeded()
    {
        // First-stage tests set runtime auth then call CreateSubsystem() in each test.
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_NoIds_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_AppIdOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = "", authSecret = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success, "With full legacy credentials (appId, orgId, authSecret) in development, auth should succeed.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_NoIds_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = "", authSecret = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_AppIdOrgId_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_FullConfig_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production_custom", appId = ConfigAppId, orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Dev_NoAppToken_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "development", appToken = "", orgToken = "" });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Dev_AppTokenOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "development", appToken = ConfigAppToken, orgToken = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "development", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_NoAppToken_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = "", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_OrgTokenOnly_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = "", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_AppTokenOnly()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = "" });
        LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_ProdCustom_BothTokens_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOnly_WithOverrides_Succeeds()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = ConfigAppId, orgId = "", authSecret = "" });
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
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = ConfigAppToken, orgToken = "" });
        Abxr.SetOrgId(ConfigOrgId);
        Abxr.SetAuthSecret(ConfigAuthSecret);
        bool mdmCanSupplyOrgToken = AbxrSubsystem.Instance.IsArborMdmClientAvailableAndConnected;
        if (!mdmCanSupplyOrgToken)
            LogAssert.Expect(LogType.Error, AuthFailureOrgUnavailable);
        bool success = false;
        yield return PerformAuth(r => success = r);
        if (mdmCanSupplyOrgToken)
            Assert.IsTrue(success, "With MDM connected, auth should succeed (dynamic orgToken).");
        else
            Assert.IsFalse(success, "Without MDM (Editor or headset without MDM), auth should fail with Organization identification unavailable.");
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_InvalidAppTokenFormat_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = "not-a-jwt", orgToken = ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_InvalidAppIdFormat_Fails()
    {
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = "not-a-valid-uuid", orgId = ConfigOrgId, authSecret = ConfigAuthSecret });
        LogAssert.Expect(LogType.Error, AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    // ── Auth handoff (App 1 → App 2 → return to App 1) ─────────────────────
    // The only bridge between the two emulated apps/APKs is the auth_handoff intent payload (the JSON).
    // We use full teardown + full base setup so each "app" is otherwise isolated.

    /// <summary>
    /// Simulates auth_handoff flow: App 1 authenticates, gets PackageName from response, stores handoff JSON,
    /// full teardown (equivalent to app/APK exit), then "launches App 2" with full base setup and injected handoff;
    /// App 2 adopts the session via StartAuthentication() and CheckAuthHandoff. Finally we simulate return to
    /// App 1 (full teardown + full base setup, no handoff) and assert we are in starting state.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthHandoff_App1HandsOffToApp2_ThenReturnToApp1_IsStartingState()
    {
        // ── App 1: normal auth flow (same as other tests) until success ───────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken });
        bool app1Success = false;
        yield return PerformAuth(r => app1Success = r);

        Assert.IsTrue(app1Success, "App 1 should authenticate successfully.");
        Assert.IsTrue(Abxr.GetAuthResponse() != null, "App 1 should have auth response after success.");

        string handoffJson = AbxrSubsystem.Instance.AuthServiceForTesting.GetHandoffJson();
        Assert.IsNotNull(handoffJson, "Handoff JSON should be built when authenticated.");
        Assert.IsFalse(string.IsNullOrEmpty(handoffJson), "Handoff JSON should not be empty.");

        // Full teardown (don't clear handoff yet; App 2 will consume it). Then full base setup so App 2 is a fresh "APK".
        FullTeardownForAppSwitch(clearAuthHandoff: false);
        Debug.Log("[AbxrLib] (Test) Leaving App 1, launching App 2.");
        AbxrAuthService.SetAuthHandoffForTesting(handoffJson);
        BaseSetUpForAppSwitch();

        // ── App 2: fresh subsystem, receive handoff and adopt session ───────
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken });

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
        Debug.Log("EventAssessmentComplete: handoff-test-assessment score: 85 result: Abxr.EventStatus.Pass");
        Abxr.EventAssessmentComplete("handoff-test-assessment", 85, Abxr.EventStatus.Pass);
        yield return null;

        // ── Return to App 1: full teardown (clear handoff) + full base setup ──────
        FullTeardownForAppSwitch(clearAuthHandoff: true);
        BaseSetUpForAppSwitch();
        CreateSubsystem();
        AssignUnitTestInputRequestedHandler(); // so PIN request is auto-submitted and OnAuthCompleted fires
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = ConfigAppToken, orgToken = ConfigOrgToken });

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
}

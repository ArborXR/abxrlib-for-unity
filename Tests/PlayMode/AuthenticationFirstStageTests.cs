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
}

[TestFixture]
public class AuthenticationFirstStageTestsDevice : AbxrPlayModeTestBase
{
    protected override void CreateSubsystemIfNeeded()
    {
        // Device-only tests: each test calls CreateSubsystem() and sets runtime auth; run on VR headset to verify with ArborMdmClient when available.
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_NoIds_OnDevice_Fails()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthenticationFirstStageTests.AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Dev_AppIdOnly_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "development", appId = AuthenticationFirstStageTests.ConfigAppId, orgId = "", authSecret = "" });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_NoIds_OnDevice_Fails()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = "", orgId = "", authSecret = "" });
        LogAssert.Expect(LogType.Error, AuthenticationFirstStageTests.AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_Prod_AppIdOnly_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production", appId = AuthenticationFirstStageTests.ConfigAppId, orgId = "", authSecret = "" });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_Legacy_ProdCustom_FullConfig_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = false, buildType = "production_custom", appId = AuthenticationFirstStageTests.ConfigAppId, orgId = AuthenticationFirstStageTests.ConfigOrgId, authSecret = AuthenticationFirstStageTests.ConfigAuthSecret });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_AppTokenOnly_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = AuthenticationFirstStageTests.ConfigAppToken, orgToken = "" });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_BothTokens_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = AuthenticationFirstStageTests.ConfigAppToken, orgToken = AuthenticationFirstStageTests.ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_Prod_OrgTokenOnly_OnDevice_Fails()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production", appToken = "", orgToken = AuthenticationFirstStageTests.ConfigOrgToken });
        LogAssert.Expect(LogType.Error, AuthenticationFirstStageTests.AuthFailureAppIdNotSet);
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsFalse(success);
    }

    [UnityTest]
    public IEnumerator AuthFirstStage_AppTokens_ProdCustom_BothTokens_OnDevice_Succeeds()
    {
        if (!IsRunningOnAndroidDevice())
            Assert.Ignore("Requires Android device (ArborMdmClient/ArborInsightsClient).");
        CreateSubsystem();
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "production_custom", appToken = AuthenticationFirstStageTests.ConfigAppToken, orgToken = AuthenticationFirstStageTests.ConfigOrgToken });
        bool success = false;
        yield return PerformAuth(r => success = r);
        Assert.IsTrue(success);
    }
}

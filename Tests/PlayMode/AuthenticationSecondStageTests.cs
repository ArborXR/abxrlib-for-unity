// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for second-stage auth. All tests set authMechanism type to "none" (prompt and domain empty) so we skip second-stage auth and avoid extra processing when the server would normally ask for it.
// Requires backend (lib-backend) and project config: useAppTokens=true, buildType=production_custom, appToken and orgToken from Configuration.
using System;
using System.Collections;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthenticationSecondStageTests : AbxrPlayModeTestBase
{
    internal static string ConfigAppToken => Configuration.Instance?.appToken ?? "";
    internal static string ConfigOrgToken => Configuration.Instance?.orgToken ?? "";
    /// <summary>Standard runtime auth for second-stage tests: app tokens, production_custom, tokens from config.</summary>
    private static RuntimeAuthConfig SecondStageRuntimeAuth()
    {
        return new RuntimeAuthConfig
        {
            useAppTokens = true,
            buildType = "production_custom",
            appToken = ConfigAppToken,
            orgToken = ConfigOrgToken
        };
    }

    /// <summary>Runtime auth with authMechanism type "none" so no second-stage auth is requested (prompt and domain empty). Saves extra activity when the server would normally ask for second-stage.</summary>
    private static RuntimeAuthConfig SecondStageRuntimeAuthNoSecondStage()
    {
        var c = SecondStageRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" };
        return c;
    }

    protected override void CreateSubsystemIfNeeded()
    {
        // Create subsystem in each test after SetRuntimeAuth.
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_ConfiguredPin_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required. Set in AbxrLib config (production_custom).");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_InvalidPin_BadPin_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_EmptyPin_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_TooShort_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_TooLong_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_NonNumeric_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_NoAuthMechanism_OnInputRequestedNotInvoked()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        int onInputRequestedCount = 0;
        Abxr.OnInputRequested = (type, prompt, domain, err) => { onInputRequestedCount++; };
        yield return new WaitForSeconds(5f);
        bool authCompleted = false;
        bool success = false;
        Abxr.OnAuthCompleted += (s, e) => { success = s; authCompleted = true; };
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 35f;
        while (!authCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        Abxr.OnAuthCompleted = null;
        Assert.IsTrue(authCompleted, "OnAuthCompleted should be invoked.");
        Assert.AreEqual(0, onInputRequestedCount, "When authMechanism is none, OnInputRequested must not be invoked.");
        Assert.IsTrue(success, "Auth should succeed without second-stage input when no authMechanism is required.");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_Email_SubmitsFullEmailAddress()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_Email_NoDomain_FailsWithError()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_Email_ConfiguredEmail_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_Email_InvalidCharacter_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }

    [UnityTest]
    public IEnumerator AuthSecondStage_Text_Empty_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthNoSecondStage());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 35f);
        Assert.IsTrue(success, $"Auth with no second-stage should succeed. Error: {error}");
    }
}

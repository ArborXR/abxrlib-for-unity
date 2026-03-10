// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for second-stage auth. Tests force authMechanism by type: assessmentPin (PIN), email (with domain from unitTestAuthEmailDomain), or text. OnInputRequested is auto-answered from Unit Test Credentials (unitTestAuthPin, unitTestAuthEmail, unitTestAuthText). Only AuthSecondStage_NoAuthMechanism_* uses type=none.
// Failed-PIN tests (e.g. EmptyPin_Fails): the keyboard auth path uses a single attempt (withRetry: false), so one wrong/empty PIN yields OnFailed and "Authentication failure". In production, the app would typically show the error and re-prompt; the test imposes the single-attempt limit.
// Requires backend (lib-backend) and project config: useAppTokens=true, buildType=production_custom, appToken and orgToken from Configuration; Unit Test Credentials enabled and PIN/email/text/domain set as needed.
using System;
using System.Collections;
using System.Collections.Generic;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static string ConfigUnitTestAuthPin => Configuration.Instance?.unitTestAuthPin ?? "";
    internal static string ConfigUnitTestAuthBadPin => Configuration.Instance?.unitTestAuthBadPin ?? "";
    internal static string ConfigUnitTestAuthEmail => Configuration.Instance?.unitTestAuthEmail ?? "";
    internal static string ConfigUnitTestAuthEmailDomain => Configuration.Instance?.unitTestAuthEmailDomain ?? "";
    internal static string ConfigUnitTestAuthText => Configuration.Instance?.unitTestAuthText ?? "";
#else
    internal static string ConfigUnitTestAuthPin => "";
    internal static string ConfigUnitTestAuthBadPin => "";
    internal static string ConfigUnitTestAuthEmail => "";
    internal static string ConfigUnitTestAuthEmailDomain => "";
    internal static string ConfigUnitTestAuthText => "";
#endif
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

    /// <summary>Runtime auth with authMechanism type assessmentPin so the SDK will prompt for a PIN (OnInputRequested). Auto-answer uses unitTestAuthPin from config.</summary>
    private static RuntimeAuthConfig SecondStageRuntimeAuthWithAssessmentPin()
    {
        var c = SecondStageRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "assessmentPin", prompt = "Enter your 6-digit PIN", domain = "" };
        return c;
    }

    /// <summary>Runtime auth with authMechanism type email; domain from configured unitTestAuthEmailDomain. Auto-answer uses unitTestAuthEmail.</summary>
    private static RuntimeAuthConfig SecondStageRuntimeAuthWithEmail(string domain = null)
    {
        var c = SecondStageRuntimeAuth();
        c.authMechanism = new AuthMechanism
        {
            type = "email",
            prompt = "Enter your email address",
            domain = domain ?? ConfigUnitTestAuthEmailDomain ?? ""
        };
        Logcat.Info($"(Test) SecondStageRuntimeAuthWithEmail: domain={domain ?? ConfigUnitTestAuthEmailDomain ?? ""}");
        return c;
    }

    /// <summary>Runtime auth with authMechanism type text. Auto-answer uses unitTestAuthText from config.</summary>
    private static RuntimeAuthConfig SecondStageRuntimeAuthWithText()
    {
        var c = SecondStageRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "text", prompt = "Enter your Employee ID", domain = "" };
        return c;
    }

    protected override void CreateSubsystemIfNeeded()
    {
        // Create subsystem in each test after SetRuntimeAuth.
    }

    /// <summary>Forces authMechanism type=assessmentPin; OnInputRequested is auto-answered with unitTestAuthPin. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_ConfiguredPin_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required. Set in AbxrLib config (production_custom).");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with assessmentPin and configured PIN should succeed. Error: {error}");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits unitTestAuthBadPin (configured bad PIN). Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_InvalidPin_BadPin_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", ConfigUnitTestAuthBadPin ?? "000000");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with bad PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits empty PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_EmptyPin_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with empty PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits too-short PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_TooShort_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "123");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with too-short PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits too-long PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_TooLong_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "1234567");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with too-long PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits non-numeric PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_AssessmentPin_NonNumeric_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "12ab34");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with non-numeric PIN should fail.");
    }

    /// <summary>Uses authMechanism type=none so no second-stage; OnInputRequested must not be invoked.</summary>
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

    /// <summary>Forces authMechanism type=email with domain from config; auto-answer uses unitTestAuthEmail. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_Email_ConfiguredEmail_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithEmail());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with email (domain from unitTestAuthEmailDomain) and unitTestAuthEmail should succeed. Error: {error}");
    }

    /// <summary>Forces authMechanism type=email; submits invalid email (unitTestAuthEmail overridden). Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_Email_InvalidCharacter_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithEmail());
        ModifyConfig("unitTestAuthEmail", "bad@@email");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with invalid email should fail.");
    }

    /// <summary>Forces authMechanism type=text; auto-answer uses unitTestAuthText. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_Text_ConfiguredText_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithText());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with text and configured unitTestAuthText should succeed. Error: {error}");
    }

    /// <summary>Forces authMechanism type=text; submits empty (unitTestAuthText overridden). Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_Text_Empty_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithText());
        ModifyConfig("unitTestAuthText", "");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with empty text should fail.");
    }

    // ── userData.id and GetAnonymizedUserId (backend contract; see linear-ticket-lib-backend-userid-userdata.md) ──

    /// <summary>
    /// Performs first auth (no second-stage), then SetUserData(id, additionalUserData), then waits for re-auth to complete.
    /// onComplete(success, error) is called after the re-auth. Use to assert Abxr.GetAnonymizedUserId() and Abxr.GetUserData().
    /// </summary>
    private IEnumerator PerformFirstAuthThenSetUserDataAndWaitForReAuth(
        string id,
        Dictionary<string, string> additionalUserData,
        Action<bool, string> onComplete,
        float firstAuthTimeoutSeconds = 35f,
        float reAuthTimeoutSeconds = 25f)
    {
        var runner = SubsystemGO != null ? SubsystemGO.GetComponent<AbxrSubsystem>() : null;
        if (runner == null || onComplete == null)
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        yield return new WaitForSeconds(5f);

        // First auth (no second-stage).
        bool firstDone = false;
        bool firstSuccess = false;
        yield return PerformAuthWithError((s, e) => { firstSuccess = s; firstDone = true; }, timeoutSeconds: firstAuthTimeoutSeconds);
        if (!firstDone || !firstSuccess)
        {
            onComplete(false, "First auth did not succeed.");
            yield break;
        }

        // Trigger re-auth with userData. SetUserData does not fire OnAuthCompleted; it fires OnUserDataSyncCompleted only.
        bool reAuthDone = false;
        bool reAuthSuccess = false;
        string reAuthError = null;
        Action<bool, string> handler = (success, errorMsg) =>
        {
            reAuthSuccess = success;
            reAuthError = errorMsg;
            reAuthDone = true;
        };
        Abxr.OnUserDataSyncCompleted += handler;

        Abxr.SetUserData(id, additionalUserData);

        float elapsed = 0f;
        while (!reAuthDone && elapsed < reAuthTimeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Abxr.OnUserDataSyncCompleted -= handler;

        if (!reAuthDone)
        {
            onComplete(false, "Re-auth (after SetUserData) did not complete within timeout.");
            yield break;
        }

        onComplete(reAuthSuccess, reAuthError);
    }

    /// <summary>Format userData for test logging: "key=value, key2=value2". inputSource appears because the SDK adds it to the custom auth payload in CreateAuthMechanismDict and the backend may echo it back in userData.</summary>
    private static string FormatUserDataForLog(Dictionary<string, string> userData)
    {
        if (userData == null) return "(null)";
        var parts = new List<string>();
        foreach (var kvp in userData)
            parts.Add(kvp.Key + "=" + kvp.Value);
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Sends userData with id via SetUserData(); waits for OnUserDataSyncCompleted (re-auth does not fire OnAuthCompleted).
    /// After sync completes, reads Abxr.GetAnonymizedUserId() and Abxr.GetUserData() to verify what the backend returned.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_UserData_WithId_BackendReturnsUserId_GetAnonymizedUserIdAndUserDataReflectResponse()
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
        // Helper: first auth (no second-stage), then SetUserData(id, additional), then waits for OnUserDataSyncCompleted.
        yield return PerformFirstAuthThenSetUserDataAndWaitForReAuth(
            "user-with-id@example.com",
            new Dictionary<string, string> { ["department"] = "QA" },
            (s, e) => { success = s; error = e; });

        Assert.IsTrue(success, $"SetUserData(id, additional) then re-auth should succeed (OnUserDataSyncCompleted). Error: {error}");

        // After OnUserDataSyncCompleted(success=true), backend response is applied; read what we got.
        string anonymizedUserId = Abxr.GetAnonymizedUserId();
        var userData = Abxr.GetUserData();
        Logcat.Info("(Test) After OnUserDataSyncCompleted: GetAnonymizedUserId()=" + (anonymizedUserId ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userData) + "}");

        Assert.IsNotNull(anonymizedUserId, "Backend should return session userId when userData.id is sent (lib-backend Linear ticket).");
        Assert.IsNotEmpty(anonymizedUserId, "Session userId from backend should be non-empty.");

        Assert.IsNotNull(userData, "GetUserData() should not be null after successful auth.");
        Assert.IsTrue(userData.ContainsKey("id"), "Backend should return userData.id (same or normalized from what we sent).");
        Assert.AreEqual("user-with-id@example.com", userData["id"], "userData.id should match what we sent.");
        Assert.IsTrue(userData.ContainsKey("department"), "Additional userData (department) should be in response.");
        Assert.AreEqual("QA", userData["department"]);
        Assert.IsFalse(userData.ContainsKey("userId"), "GetUserData() must not include session userId; use GetAnonymizedUserId().");
    }

    /// <summary>
    /// Performs first and second stage auth forced to email type (auto-answered from unit test credentials), logs GetAnonymizedUserId() and GetUserData(),
    /// then SetUserData(null, first_name + last_name), waits for OnUserDataSyncCompleted, logs again, and asserts.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_UserData_WithoutId_WithEmail_BackendDerivesId_GetAnonymizedUserIdAndUserDataReflectResponse()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(SecondStageRuntimeAuthWithEmail());
        bool authSuccess = false;
        string authError = null;
        yield return new WaitForSeconds(5f);
        yield return PerformAuthWithError((s, e) => { authSuccess = s; authError = e; }, timeoutSeconds: 40f);

        Assert.IsTrue(authSuccess, $"First + second stage (email) auth should succeed. Error: {authError}");

        // Log after email auth (before SetUserData).
        string anonymizedUserIdAfterAuth = Abxr.GetAnonymizedUserId();
        var userDataAfterAuth = Abxr.GetUserData();
        Logcat.Info("(Test) After email auth: GetAnonymizedUserId()=" + (anonymizedUserIdAfterAuth ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userDataAfterAuth) + "}");

        // SetUserData to add first_name and last_name; wait for OnUserDataSyncCompleted.
        bool reAuthDone = false;
        bool reAuthSuccess = false;
        string reAuthError = null;
        Action<bool, string> handler = (s, e) => { reAuthSuccess = s; reAuthError = e; reAuthDone = true; };
        Abxr.OnUserDataSyncCompleted += handler;
        Abxr.SetUserData(null, new Dictionary<string, string> { ["first_name"] = "Chad", ["last_name"] = "Tester" });
        float elapsed = 0f;
        while (!reAuthDone && elapsed < 25f) { elapsed += Time.deltaTime; yield return null; }
        Abxr.OnUserDataSyncCompleted -= handler;

        Assert.IsTrue(reAuthDone, "SetUserData re-auth should complete within timeout.");
        Assert.IsTrue(reAuthSuccess, $"SetUserData(null, first_name + last_name) re-auth should succeed. Error: {reAuthError}");

        // Log after SetUserData sync.
        string anonymizedUserIdAfterSetUserData = Abxr.GetAnonymizedUserId();
        var userDataAfterSetUserData = Abxr.GetUserData();
        Logcat.Info("(Test) After SetUserData: GetAnonymizedUserId()=" + (anonymizedUserIdAfterSetUserData ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userDataAfterSetUserData) + "}");

        Assert.IsNotNull(anonymizedUserIdAfterSetUserData, "Backend should return session userId.");
        Assert.IsNotEmpty(anonymizedUserIdAfterSetUserData, "Session userId from backend should be non-empty.");
        Assert.IsNotNull(userDataAfterSetUserData, "GetUserData() should not be null after successful auth.");
        Assert.IsTrue(userDataAfterSetUserData.ContainsKey("first_name"), "userData should include first_name we sent via SetUserData.");
        Assert.AreEqual("Chad", userDataAfterSetUserData["first_name"]);
        Assert.IsTrue(userDataAfterSetUserData.ContainsKey("last_name"), "userData should include last_name we sent via SetUserData.");
        Assert.AreEqual("Tester", userDataAfterSetUserData["last_name"]);
        Assert.IsFalse(userDataAfterSetUserData.ContainsKey("userId"), "GetUserData() must not include session userId; use GetAnonymizedUserId().");
    }

    /// <summary>
    /// Sends only userData.id via SetUserData(); waits for OnUserDataSyncCompleted. Then reads GetAnonymizedUserId() and GetUserData().
    /// </summary>
    [UnityTest]
    public IEnumerator AuthSecondStage_UserData_IdOnly_GetAnonymizedUserIdReturnsBackendValue()
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
        yield return PerformFirstAuthThenSetUserDataAndWaitForReAuth(
            "minimal-id@test.com",
            null,
            (s, e) => { success = s; error = e; });

        Assert.IsTrue(success, $"SetUserData(id only) then re-auth should succeed (OnUserDataSyncCompleted). Error: {error}");

        // After OnUserDataSyncCompleted(success=true), read what the backend returned.
        string anonymizedUserId = Abxr.GetAnonymizedUserId();
        var userData = Abxr.GetUserData();
        Logcat.Info("(Test) After OnUserDataSyncCompleted: GetAnonymizedUserId()=" + (anonymizedUserId ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userData) + "}");

        Assert.IsNotNull(anonymizedUserId, "Backend should return session userId when userData.id is sent.");
        Assert.IsNotEmpty(anonymizedUserId);

        Assert.IsNotNull(userData);
        Assert.IsTrue(userData.ContainsKey("id"));
        Assert.AreEqual("minimal-id@test.com", userData["id"]);
    }
}

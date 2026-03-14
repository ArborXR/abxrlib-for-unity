// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for user authentication (API may request user identification after device authentication). Tests force authMechanism by type: assessmentPin (PIN), email (with domain from unitTestAuthEmailDomain), or text. OnInputRequested is auto-answered from Unit Test Credentials (unitTestAuthPin, unitTestAuthEmail, unitTestAuthText). Only AuthUser_NoAuthMechanism_* uses type=none.
// Failed-PIN tests (e.g. InvalidPin): the keyboard auth path uses a single attempt (withRetry: false), so one wrong PIN yields OnFailed and "Authentication failure". Empty input is rejected locally (no transport call); OnInputRequested is re-invoked with an error so the user can try again.
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
public class AuthenticationUserTests : AbxrPlayModeTestBase
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
    /// <summary>Standard runtime auth for user authentication tests: app tokens, production_custom, tokens from config.</summary>
    private static RuntimeAuthConfig UserRuntimeAuth()
    {
        return new RuntimeAuthConfig
        {
            useAppTokens = true,
            buildType = "production_custom",
            appToken = ConfigAppToken,
            orgToken = ConfigOrgToken
        };
    }

    /// <summary>Runtime auth with authMechanism type "none" so no user authentication is requested (prompt and domain empty). Saves extra activity when the server would normally ask for user identification.</summary>
    private static RuntimeAuthConfig UserRuntimeAuthNoUserAuth()
    {
        var c = UserRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" };
        return c;
    }

    /// <summary>Runtime auth with authMechanism type assessmentPin so the SDK will prompt for a PIN (OnInputRequested). Auto-answer uses unitTestAuthPin from config.</summary>
    private static RuntimeAuthConfig UserRuntimeAuthWithAssessmentPin()
    {
        var c = UserRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "assessmentPin", prompt = "Enter your 6-digit PIN", domain = "" };
        return c;
    }

    /// <summary>Runtime auth with authMechanism type email; domain from configured unitTestAuthEmailDomain. Auto-answer uses unitTestAuthEmail.</summary>
    private static RuntimeAuthConfig UserRuntimeAuthWithEmail(string domain = null)
    {
        var c = UserRuntimeAuth();
        c.authMechanism = new AuthMechanism
        {
            type = "email",
            prompt = "Enter your email address",
            domain = domain ?? ConfigUnitTestAuthEmailDomain ?? ""
        };
        Logcat.Info($"(Test) UserRuntimeAuthWithEmail: domain={domain ?? ConfigUnitTestAuthEmailDomain ?? ""}");
        return c;
    }

    /// <summary>Runtime auth with authMechanism type text. Auto-answer uses unitTestAuthText from config.</summary>
    private static RuntimeAuthConfig UserRuntimeAuthWithText()
    {
        var c = UserRuntimeAuth();
        c.authMechanism = new AuthMechanism { type = "text", prompt = "Enter your Employee ID", domain = "" };
        return c;
    }

    protected override void CreateSubsystemIfNeeded()
    {
        // Create subsystem in each test after SetRuntimeAuth.
    }

    /// <summary>Forces authMechanism type=assessmentPin; OnInputRequested is auto-answered with unitTestAuthPin. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_ConfiguredPin_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required. Set in AbxrLib config (production_custom).");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with assessmentPin and configured PIN should succeed. Error: {error}");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits unitTestAuthBadPin (configured bad PIN). Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_InvalidPin_BadPin_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", ConfigUnitTestAuthBadPin ?? "000000");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with bad PIN should fail.");
    }

    /// <summary>Submits empty PIN first; SDK rejects locally and re-invokes OnInputRequested. Second submit uses valid PIN; auth succeeds (no transport call for empty).</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_EmptyPin_RejectedLocally_ThenValidSucceeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        string validPin = ConfigUnitTestAuthPin;
        if (string.IsNullOrEmpty(validPin))
        {
            Assert.Ignore("Unit test PIN required. Set unitTestAuthPin in AbxrLib config.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        int onInputCount = 0;
        Action<string, string, string, string> customHandler = (type, prompt, domain, err) =>
        {
            onInputCount++;
            if (onInputCount == 1)
            {
                Logcat.Info("(Test) Submitting empty PIN to trigger local rejection (no transport call).");
                Abxr.OnInputSubmitted("");
            }
            else
                Abxr.OnInputSubmitted(validPin);
        };
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f, customOnInputRequested: customHandler);
        Assert.IsTrue(success, $"Auth should succeed after re-prompt. Error: {error}");
        Assert.AreEqual(2, onInputCount, "OnInputRequested should be called twice: initial prompt, then re-prompt after empty (no transport call for empty).");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits too-short PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_TooShort_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "123");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with too-short PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits too-long PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_TooLong_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "1234567");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with too-long PIN should fail.");
    }

    /// <summary>Forces authMechanism type=assessmentPin; submits non-numeric PIN. Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthUser_AssessmentPin_NonNumeric_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithAssessmentPin());
        ModifyConfig("unitTestAuthPin", "12ab34");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with non-numeric PIN should fail.");
    }

    /// <summary>Uses authMechanism type=none so no user authentication; OnInputRequested must not be invoked.</summary>
    [UnityTest]
    public IEnumerator AuthUser_NoAuthMechanism_OnInputRequestedNotInvoked()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthNoUserAuth());
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
        Assert.IsTrue(success, "Auth should succeed when no authMechanism is required (same for REST and ArborInsightsClient transport).");
    }

    /// <summary>Forces authMechanism type=email with domain from config; auto-answer uses unitTestAuthEmail. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthUser_Email_ConfiguredEmail_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithEmail());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with email (domain from unitTestAuthEmailDomain) and unitTestAuthEmail should succeed. Error: {error}");
    }

    /// <summary>Forces authMechanism type=email; submits invalid email (unitTestAuthEmail overridden). Expects auth to fail.</summary>
    [UnityTest]
    public IEnumerator AuthUser_Email_InvalidCharacter_Fails()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Authentication failure:.*"));
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithEmail());
        ModifyConfig("unitTestAuthEmail", "bad@@email");
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsFalse(success, "Auth with invalid email should fail.");
    }

    /// <summary>Forces authMechanism type=text; auto-answer uses unitTestAuthText. Expects auth to succeed.</summary>
    [UnityTest]
    public IEnumerator AuthUser_Text_ConfiguredText_Succeeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithText());
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f);
        Assert.IsTrue(success, $"Auth with text and configured unitTestAuthText should succeed. Error: {error}");
    }

    /// <summary>Submits empty text first; SDK rejects locally and re-invokes OnInputRequested. Second submit uses valid text; auth succeeds (no transport call for empty).</summary>
    [UnityTest]
    public IEnumerator AuthUser_Text_Empty_RejectedLocally_ThenValidSucceeds()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        string validText = ConfigUnitTestAuthText;
        if (string.IsNullOrEmpty(validText))
        {
            Assert.Ignore("Unit test text required. Set unitTestAuthText in AbxrLib config.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithText());
        int onInputCount = 0;
        Action<string, string, string, string> customHandler = (type, prompt, domain, err) =>
        {
            onInputCount++;
            if (onInputCount == 1)
            {
                Logcat.Info("(Test) Submitting empty text to trigger local rejection (no transport call).");
                Abxr.OnInputSubmitted("");
            }
            else
                Abxr.OnInputSubmitted(validText);
        };
        bool success = false;
        string error = null;
        yield return PerformAuthWithError((s, e) => { success = s; error = e; }, timeoutSeconds: 40f, customOnInputRequested: customHandler);
        Assert.IsTrue(success, $"Auth should succeed after re-prompt. Error: {error}");
        Assert.AreEqual(2, onInputCount, "OnInputRequested should be called twice: initial prompt, then re-prompt after empty (no transport call for empty).");
    }

    // ── userData.id and GetAnonymizedUserId (backend contract; see linear-ticket-lib-backend-userid-userdata.md) ──

    /// <summary>
    /// Performs device auth (no user authentication), then SetUserData(id, additionalUserData), then waits for re-auth to complete.
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

        // Device auth (no user authentication).
        bool firstDone = false;
        bool firstSuccess = false;
        yield return PerformAuthWithError((s, e) => { firstSuccess = s; firstDone = true; }, timeoutSeconds: firstAuthTimeoutSeconds);
        if (!firstDone || !firstSuccess)
        {
            onComplete(false, "Device auth did not succeed.");
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
    public IEnumerator AuthUser_UserData_WithId_BackendReturnsUserId_GetAnonymizedUserIdAndUserDataReflectResponse()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthNoUserAuth());
        bool success = false;
        string error = null;
        // Helper: device auth (no user authentication), then SetUserData(id, additional), then waits for OnUserDataSyncCompleted.
        yield return PerformFirstAuthThenSetUserDataAndWaitForReAuth(
            "user-with-id@example.com",
            new Dictionary<string, string> { ["department"] = "QA" },
            (s, e) => { success = s; error = e; });

        Assert.IsTrue(success, $"SetUserData(id, additional) then re-auth should succeed (OnUserDataSyncCompleted). Error: {error}");

        // After OnUserDataSyncCompleted(success=true), backend response is applied; read what we got.
        string userId = Abxr.GetUserId();
        string anonymizedUserId = Abxr.GetAnonymizedUserId();
        var userData = Abxr.GetUserData();
        Logcat.Info("(Test) After OnUserDataSyncCompleted: GetUserId()=" + (userId ?? "(null)") + ", GetAnonymizedUserId()=" + (anonymizedUserId ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userData) + "}");

        Assert.IsNotNull(userId, "GetUserId() should return userData.id or anonymized userId after successful sync (not null).");
        Assert.IsNotEmpty(userId, "GetUserId() should return userData.id or anonymized userId after successful sync (not empty).");
        Assert.IsTrue(userId == "user-with-id@example.com" || userId == anonymizedUserId, "GetUserId() should match what we sent or the anonymized userId.");

        Assert.IsNotNull(anonymizedUserId, "Backend should return session userId when userData.id is sent (lib-backend Linear ticket).");
        Assert.IsNotEmpty(anonymizedUserId, "Session userId from backend should be non-empty.");

        Assert.IsNotNull(userData, "GetUserData() should not be null after successful auth.");
        // When GetUserId() == GetAnonymizedUserId(): either guest mode (both null; authMechanism type=none or user declined) or PII disabled (both non-null, backend does not echo userData).
        // When they differ, the backend echoed userData.id so we have user data and should assert the values we set.
        bool guestOrNoPii = userId == anonymizedUserId;
        if (guestOrNoPii)
        {
            Logcat.Info("(Test) Guest or PII disabled (GetUserId() == GetAnonymizedUserId()); GetUserData() may be empty.");
            Assert.AreEqual(0, userData.Count, "When guest or PII disabled (GetUserId() == GetAnonymizedUserId()), GetUserData() should be empty.");
        }
        else
        {
            Logcat.Info("(Test) PII enabled (GetUserId() != GetAnonymizedUserId()); GetUserData() should contain userData.id and additional fields.");
            Assert.IsTrue(userData.ContainsKey("id"), "Backend echoed userData.id so id should be present when PII is not disabled.");
            Assert.AreEqual("user-with-id@example.com", userData["id"], "userData.id should match what we sent.");
            Assert.IsTrue(userData.ContainsKey("department"), "Backend echoed userData so department should be present when PII is not disabled.");
            Assert.AreEqual("QA", userData["department"], "userData.department should match what we sent.");
        }
        Assert.IsFalse(userData.ContainsKey("userId"), "GetUserData() must not include session userId; use GetAnonymizedUserId().");
    }

    /// <summary>
    /// Performs device and user authentication forced to email type (auto-answered from unit test credentials), logs GetAnonymizedUserId() and GetUserData(),
    /// then SetUserData(null, first_name + last_name), waits for OnUserDataSyncCompleted, logs again, and asserts.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthUser_UserData_WithoutId_WithEmail_BackendDerivesId_GetAnonymizedUserIdAndUserDataReflectResponse()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthWithEmail());
        bool authSuccess = false;
        string authError = null;
        yield return new WaitForSeconds(5f);
        yield return PerformAuthWithError((s, e) => { authSuccess = s; authError = e; }, timeoutSeconds: 40f);

        Assert.IsTrue(authSuccess, $"Device + user (email) auth should succeed. Error: {authError}");

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
    /// Core contract: backend returns session userId; we assert GetAnonymizedUserId() is set. When the backend echoes userData (e.g. when PII is enabled), we assert userData.id matches.
    /// </summary>
    [UnityTest]
    public IEnumerator AuthUser_UserData_IdOnly_GetAnonymizedUserIdReturnsBackendValue()
    {
        if (string.IsNullOrEmpty(ConfigAppToken) || string.IsNullOrEmpty(ConfigOrgToken))
        {
            Assert.Ignore("App token and org token required.");
            yield break;
        }
        CreateSubsystem();
        SetRuntimeAuth(UserRuntimeAuthNoUserAuth());
        bool success = false;
        string error = null;
        yield return PerformFirstAuthThenSetUserDataAndWaitForReAuth(
            "minimal-id@test.com",
            null,
            (s, e) => { success = s; error = e; });

        Assert.IsTrue(success, $"SetUserData(id only) then re-auth should succeed (OnUserDataSyncCompleted). Error: {error}");

        // After OnUserDataSyncCompleted(success=true), read what the backend returned. Use GetUserId() for a single id (userData.id or anonymized).
        string userId = Abxr.GetUserId();
        string anonymizedUserId = Abxr.GetAnonymizedUserId();
        var userData = Abxr.GetUserData();
        Logcat.Info("(Test) After OnUserDataSyncCompleted: GetUserId()=" + (userId ?? "(null)") + ", GetAnonymizedUserId()=" + (anonymizedUserId ?? "(null)") + ", GetUserData()={" + FormatUserDataForLog(userData) + "}");

        Assert.IsNotNull(anonymizedUserId, "Backend should return session userId when userData.id is sent.");
        Assert.IsNotEmpty(anonymizedUserId);
        Assert.IsNotNull(userId, "GetUserId() should return userData.id or anonymized userId after successful sync (not null).");
        Assert.IsNotEmpty(userId, "GetUserId() should return userData.id or anonymized userId after successful sync (not empty).");
        Assert.IsTrue(userId == "minimal-id@test.com" || userId == anonymizedUserId, "GetUserId() should match what we sent or the anonymized userId.");

        Assert.IsNotNull(userData);
        if (userData.Count > 0 && userData.ContainsKey("id"))
            Assert.AreEqual("minimal-id@test.com", userData["id"], "When backend echoes userData.id it should match what we sent.");
    }
}

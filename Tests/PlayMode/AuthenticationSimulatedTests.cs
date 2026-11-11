// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests using simulated auth only (no API calls): auth state, override setters, module list, session management.
// Device auth (config, validation, handoff) is in AuthenticationDeviceTests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthenticationSimulatedTests : AbxrPlayModeTestBase
{
    // ── Pre-authentication defaults ───────────────────────────────────────

    [Test]
    public void GetUserData_BeforeAuth_ReturnsNull()
        => Assert.IsNull(Abxr.GetUserData());

    [Test]
    public void IsAuthInputRequestPending_BeforeInputRequest_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsAuthInputRequestPending());

    [Test]
    public void IsQRScanForAuthAvailable_InEditor_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsQRScanForAuthAvailable());

    [Test]
    public void IsQRScanCameraTexturePlaceable_InEditor_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsQRScanCameraTexturePlaceable());

    [Test]
    public void GetQRScanCameraTexture_InEditor_ReturnsNull()
        => Assert.IsNull(Abxr.GetQRScanCameraTexture());

    // ── MDM / Insights client disabled in tests ───────────────────────────

    [Test]
    public void GetIsAuthenticated_WithoutArborInsightsClient_ReturnsFalse()
        => Assert.IsFalse(Abxr.GetIsAuthenticated());

    [Test]
    public void GetAccessToken_WithoutArborInsightsClient_ReturnsEmpty()
    {
        var token = Abxr.GetAccessToken();
        Assert.IsTrue(token == null || token == "");
    }

    [Test]
    public void GetRefreshToken_WithoutArborInsightsClient_ReturnsEmpty()
    {
        var token = Abxr.GetRefreshToken();
        Assert.IsTrue(token == null || token == "");
    }

    // ── Override setters ──────────────────────────────────────────────────

    [Test]
    public void SetDeviceId_ReflectsInGetDeviceId()
    {
        Abxr.SetDeviceId("test-device-001");
        Assert.AreEqual("test-device-001", Abxr.GetDeviceId());
    }

    [Test]
    public void SetDeviceId_Null_ClearsOverride()
    {
        Abxr.SetDeviceId("test-device-001");
        Abxr.SetDeviceId(null);
        var id = Abxr.GetDeviceId();
        Assert.AreNotEqual("test-device-001", id, "Clearing override should stop returning the overridden value; GetDeviceId now falls back to platform (e.g. MDM on device) or empty.");
    }

    [Test]
    public void SetOrgId_ReflectsInGetOrgId()
    {
        Abxr.SetOrgId("my-org-uuid");
        Assert.AreEqual("my-org-uuid", Abxr.GetOrgId());
    }

    [Test]
    public void SetOrgId_Null_ClearsOverride()
    {
        Abxr.SetOrgId("my-org-uuid");
        Abxr.SetOrgId(null);
        var id = Abxr.GetOrgId();
        Assert.AreNotEqual("my-org-uuid", id, "Clearing override should stop returning the overridden value; GetOrgId now falls back to config or empty.");
    }

    [Test]
    public void SetAuthSecret_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.SetAuthSecret("my-auth-secret"));

    // ── Simulated authentication ──────────────────────────────────────────

    [Test]
    public void SimulateAuth_SetsAuthServiceAuthenticated()
    {
        SimulateAuth();
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated);
    }

    [Test]
    public void SimulateAuth_GetUserData_ContainsUserId()
    {
        SimulateAuth(BuildTestAuthResponse(userId: "learner-42"));
        Assert.AreEqual("learner-42", Abxr.GetAnonymizedUserId());
        var userData = Abxr.GetUserData();
        Assert.IsNotNull(userData);
        // GetUserData returns userData only; session userId is read via GetAnonymizedUserId()
        Assert.IsFalse(userData.ContainsKey("userId"));
    }

    [Test]
    public void SimulateAuth_GetUserData_ContainsCustomFields()
    {
        SimulateAuth(BuildTestAuthResponse(userData: new Dictionary<string, string>
        {
            ["email"] = "jane@example.com",
            ["department"] = "Engineering"
        }));
        var userData = Abxr.GetUserData();
        Assert.AreEqual("jane@example.com", userData["email"]);
        Assert.AreEqual("Engineering", userData["department"]);
    }

    [Test]
    public void SimulateAuth_GetUserData_UserIdFallsBackToEmail()
    {
        SimulateAuth(new AuthResponse
        {
            Token = "tok",
            UserId = null,
            UserData = new Dictionary<string, string> { ["email"] = "fallback@example.com" },
            Modules = new List<ModuleData>()
        });
        var userData = Abxr.GetUserData();
        Assert.AreEqual("fallback@example.com", userData["email"]);
        Assert.IsFalse(userData.ContainsKey("userId"));
        Assert.IsNull(Abxr.GetAnonymizedUserId());
        // Simulated auth does not set transport auth headers; flush logs this. We EndSession here and expect the log so TearDown does not run EndSession (avoids unhandled log).
        RunEndSessionInTearDown = false;
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Cannot set auth headers - authentication tokens are missing")));
        Abxr.EndSession();
    }

    [Test]
    public void SimulateAuth_FiresOnAuthCompleted_WithTrue()
    {
        bool authSucceeded = false;
        Abxr.OnAuthCompleted += (success, _) => authSucceeded = success;
        SimulateAuth();
        Assert.IsTrue(authSucceeded);
    }

    [Test]
    public void SimulateAuth_GetAuthResponse_IsNotNull()
    {
        SimulateAuth();
        Assert.IsNotNull(Abxr.GetAuthResponse());
    }

    [Test]
    public void SimulateAuth_GetAuthResponse_TokenMatches()
    {
        SimulateAuth(BuildTestAuthResponse());
        Assert.AreEqual("test-token-abc123", Abxr.GetAuthResponse().Token);
    }

    // ── Module list ───────────────────────────────────────────────────────

    [Test]
    public void GetModuleList_AfterAuthWithNoModules_ReturnsEmptyList()
    {
        SimulateAuth(BuildTestAuthResponse(modules: new List<ModuleData>()));
        var modules = Abxr.GetModuleList();
        Assert.IsNotNull(modules);
        Assert.AreEqual(0, modules.Count);
    }

    [Test]
    public void GetModuleList_AfterAuthWithModules_ReturnsAllModules()
    {
        var modules = new List<ModuleData>
        {
            new ModuleData { Id = "m1", Name = "Introduction", Target = "scene://intro", Order = 1 },
            new ModuleData { Id = "m2", Name = "Assessment",   Target = "scene://assess", Order = 2 }
        };
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Subscribe to OnModuleTarget before running modules")));
        SimulateAuth(BuildTestAuthResponse(modules: modules));
        var result = Abxr.GetModuleList();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("m1", result[0].Id);
        Assert.AreEqual("Introduction", result[0].Name);
        Assert.AreEqual("scene://intro", result[0].Target);
    }

    [Test]
    public void StartModuleAtIndex_NegativeIndex_ReturnsFalse()
    {
        SimulateAuth();
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("No modules available")));
        Assert.IsFalse(Abxr.StartModuleAtIndex(-1));
    }

    [Test]
    public void StartModuleAtIndex_IndexOutOfRange_ReturnsFalse()
    {
        SimulateAuth();
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("No modules available")));
        Assert.IsFalse(Abxr.StartModuleAtIndex(99));
    }

    [Test]
    public void StartModuleAtIndex_WithModulesButNoHandler_ReturnsFalse()
    {
        var modules = new List<ModuleData>
        {
            new ModuleData { Id = "m1", Name = "Module 1", Target = "target1", Order = 1 }
        };
        Abxr.OnModuleTarget = null;
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Subscribe to OnModuleTarget before running modules")));
        SimulateAuth(BuildTestAuthResponse(modules: modules));
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Need to subscribe to OnModuleTarget before running modules")));
        Assert.IsFalse(Abxr.StartModuleAtIndex(0));
    }

    [Test]
    public void StartModuleAtIndex_WithModulesAndHandler_InvokesHandlerAndReturnsTrue()
    {
        string receivedTarget = null;
        Abxr.OnModuleTarget = target => receivedTarget = target;
        var modules = new List<ModuleData>
        {
            new ModuleData { Id = "m1", Name = "Module 1", Target = "scene://module1", Order = 1 }
        };
        SimulateAuth(BuildTestAuthResponse(modules: modules));

        bool result = Abxr.StartModuleAtIndex(0);
        Assert.IsTrue(result);
        Assert.AreEqual("scene://module1", receivedTarget);
    }

    // ── OnInputRequested handler ──────────────────────────────────────────

    [Test]
    public void OnInputRequested_SetToHandler_IsRetrievable()
    {
        Abxr.OnInputRequested = (type, prompt, domain, error) => { };
        Assert.IsNotNull(Abxr.OnInputRequested);
    }

    [Test]
    public void OnInputRequested_SetToNull_ClearsHandler()
    {
        Abxr.OnInputRequested = (type, prompt, domain, error) => { };
        Abxr.OnInputRequested = null;
        Assert.IsNull(Abxr.OnInputRequested);
    }

    // ── StartNewSession ───────────────────────────────────────────────────

    [Test]
    public void StartNewSession_DoesNotThrow()
    {
        SimulateAuth();
        Assert.DoesNotThrow(() => Abxr.StartNewSession());
    }

    [Test]
    public void StartNewSession_ClearsSuperMetaData()
    {
        SimulateAuth();
        Abxr.Register("context", "training");
        Abxr.StartNewSession();
        Assert.That(Abxr.GetSuperMetaData(), Is.Null.Or.Empty);
    }

    // ── DEFAULT assessment ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator DefaultAssessment_StartsOverWhenTrueFollowsFalse()
    {
        // A device auth that retried offline, or a wrong PIN, reports false first. The DEFAULT duration counts from the true.
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: offline")));
        AbxrSubsystem.Instance.AuthServiceForTesting.OnFailed("offline");
        var startTimes = (Dictionary<string, System.DateTime>)typeof(AbxrSubsystem)
            .GetField("_assessmentStartTimes", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(AbxrSubsystem.Instance);
        System.DateTime afterFalse = startTimes["DEFAULT"];

        yield return new WaitForSecondsRealtime(0.05f);
        SimulateAuth();

        Assert.Greater(startTimes["DEFAULT"], afterFalse);
    }

    // ── EndSession ────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator EndSession_WithAnOpenAssessmentInAHandoffSession_DoesNotStartTheReturnFlow()
    {
        AbxrSubsystem.SimulateQuitInExitAfterAssessmentComplete = true; // if the flow does start, it must not stop play mode
        ModifyConfig("restUrl", "http://127.0.0.1:1/"); // EndSession flushes the closed assessment; keep that off the network
        SimulateAuth();
        // A handoff session with enableReturnTo on (the default) exits or returns once an assessment completes.
        typeof(AbxrAuthService).GetField("_sessionUsedAuthHandoff", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(AbxrSubsystem.Instance.AuthServiceForTesting, true);
        Abxr.EventAssessmentStart("open_assessment");

        Abxr.EndSession();

        Assert.IsFalse(AbxrSubsystem.Instance.IsExitAfterAssessmentScheduledForTesting,
            "EndSession auto-closes the assessment, but the app keeps running, so it must not launch the return app or quit.");
        yield return new WaitForSeconds(2.5f); // lets a wrongly started flow finish before TearDown resets the quit simulation
    }

    // ── Auth handoff, applied once ────────────────────────────────────────
    // Device auth goes to a scripted transport that answers offline, so a test sees which path an attempt took without the
    // network: applying a handoff sends no auth request, and device auth sends one and reports that it is retrying.

    private const string OfflineRetryingReport =
        "Authentication failure: Device authentication failed (no connection); the SDK is retrying in the background.";

    [UnityTest]
    public IEnumerator AuthHandoff_StartNewSession_DoesNotAdoptItAgain()
    {
        var transport = UseOfflineDeviceAuth();
        var results = new List<bool>();
        yield return AdoptFixedHandoff(results);

        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(OfflineRetryingReport)));
        Abxr.StartNewSession();
        yield return WaitFor(() => results.Count >= 2, 5f);

        CollectionAssert.AreEqual(new[] { true, false }, results);
        Assert.AreEqual(1, transport.AuthCalls, "The next learner's session must run device auth, not inherit the previous learner's handoff.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff());
    }

    [UnityTest]
    public IEnumerator AuthHandoff_ReAuth_DoesNotReapplyIt()
    {
        var transport = UseOfflineDeviceAuth();
        var results = new List<bool>();
        yield return AdoptFixedHandoff(results);

        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(OfflineRetryingReport)));
        // What ReAuthPollCoroutine calls when the token is about to expire.
        AbxrSubsystem.Instance.AuthServiceForTesting.Authenticate();
        yield return WaitFor(() => results.Count >= 2, 5f);

        CollectionAssert.AreEqual(new[] { true, false }, results);
        Assert.AreEqual(1, transport.AuthCalls, "Re-auth must go through device auth, not re-apply the handoff and push its expiry out again.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff());
    }

    [UnityTest]
    public IEnumerator AuthHandoff_DeliveredWhileRunning_WithTheCurrentSession_IsIgnored()
    {
        var transport = UseOfflineDeviceAuth();
        SimulateAuth();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        // Return-to-launcher: App 2 hands the launcher's own session back while the launcher still runs.
        AbxrAuthService.SetAuthHandoffForTesting(AbxrSubsystem.Instance.GetHandoffJsonForTesting());

        SubsystemGO.SendMessage("OnApplicationFocus", true);

        Assert.IsTrue(auth.Authenticated, "The launcher keeps its session.");
        Assert.IsFalse(auth.SessionUsedAuthHandoff());
        CollectionAssert.IsEmpty(results);

        // It was consumed, so the launcher's next learner doesn't inherit it either.
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(OfflineRetryingReport)));
        Abxr.StartNewSession();
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { false }, results);
        Assert.AreEqual(1, transport.AuthCalls);
        Assert.IsFalse(auth.SessionUsedAuthHandoff());
    }

    [UnityTest]
    public IEnumerator AuthHandoff_DeliveredWhileRunning_WithAnotherSession_ReplacesTheSession()
    {
        var transport = UseOfflineDeviceAuth();
        SimulateAuth();
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("next-learner-token"));
        // The session it replaces ends the way EndSession does, so what it recorded is sent rather than dropped.
        LogAssert.Expect(LogType.Log, new Regex(Regex.Escape("Ending session: closing running events and flushing")));

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { true }, results);
        Assert.AreEqual("next-learner-token", Abxr.GetAuthResponse().Token);
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff());
        Assert.AreEqual(0, transport.AuthCalls, "The handoff replaces device auth.");
    }

    [UnityTest]
    public IEnumerator AuthHandoff_DeliveredWhileRunning_AlreadyExpired_IsIgnored()
    {
        UseOfflineDeviceAuth();
        SimulateAuth();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        string token = Abxr.GetAuthResponse().Token;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        // exp = 1: a token that expired in 1970.
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("eyJhbGciOiJub25lIn0.eyJleHAiOjF9.sig"));

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return null;

        Assert.IsTrue(auth.Authenticated, "A handoff that can't be applied must not cost the running session.");
        Assert.AreEqual(token, Abxr.GetAuthResponse().Token);
        Assert.IsFalse(auth.SessionUsedAuthHandoff());
        CollectionAssert.IsEmpty(results);
    }

    // ── Auth handoff on the ArborInsightsClient transport ─────────────────
    // A handoff skips device auth, which is where the service binds again after EndSession, so the session is given to the
    // transport before the app signs in. The scripted transport stands in for the service: HoldHandoff is its bind and wait.

    [UnityTest]
    public IEnumerator AuthHandoff_DeliveredWhileRunning_OnTheServiceTransport_SignsInOnceTheServiceHasIt()
    {
        var transport = UseOfflineDeviceAuth();
        transport.IsServiceTransport = true;
        transport.HoldHandoff = true;
        SimulateAuth();
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        string handoff = HandoffJson("next-learner-token");
        AbxrAuthService.SetAuthHandoffForTesting(handoff);

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return WaitFor(() => transport.HandoffCalls >= 1, 5f);
        yield return null;

        Assert.AreEqual(1, transport.HandoffCalls);
        CollectionAssert.IsEmpty(results, "The app must not report signed in while the service has no session for it.");
        Assert.AreEqual(0, transport.ConfigCalls, "GET config goes to the service too, so it waits as well.");

        transport.HoldHandoff = false;
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { true }, results);
        Assert.AreEqual(handoff, transport.LastHandoffJson);
        Assert.AreEqual(0, transport.AuthCalls, "The handoff replaces device auth.");
    }

    [UnityTest]
    public IEnumerator AuthHandoff_DeliveredAfterEndSession_OnTheServiceTransport_IsGivenToTheService()
    {
        var transport = UseOfflineDeviceAuth();
        transport.IsServiceTransport = true;
        var results = new List<bool>();
        yield return AdoptFixedHandoff(results);
        Abxr.EndSession();
        transport.HoldHandoff = true;
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("next-learner-token"));

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return WaitFor(() => transport.HandoffCalls >= 2, 5f);
        yield return null;

        Assert.AreEqual(2, transport.HandoffCalls, "EndSession unbound the service, so the next session must be given to it too.");
        Assert.AreEqual(1, results.Count, "No second sign-in before the service has the session.");

        transport.HoldHandoff = false;
        yield return WaitFor(() => results.Count >= 2, 5f);

        CollectionAssert.AreEqual(new[] { true, true }, results);
        Assert.AreEqual("next-learner-token", Abxr.GetAuthResponse().Token);
    }

    [UnityTest]
    public IEnumerator AuthHandoff_TheServiceCantTakeIt_RunsDeviceAuth()
    {
        var transport = UseOfflineDeviceAuth();
        transport.IsServiceTransport = true;
        transport.HandoffFailure = "ArborInsightsClient service not ready after bind";
        SimulateAuth();
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("next-learner-token"));
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(OfflineRetryingReport)));

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { false }, results, "A session the service doesn't have must not be reported as signed in.");
        Assert.AreEqual(1, transport.AuthCalls, "It continues with device authentication, as for any handoff that can't be applied.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff());
        Assert.AreNotEqual("next-learner-token", Abxr.GetAuthResponse()?.Token);
    }

    [UnityTest]
    public IEnumerator AuthHandoff_SessionEndedWhileTheServiceBinds_IsNotGivenToIt()
    {
        var transport = UseOfflineDeviceAuth();
        transport.IsServiceTransport = true;
        transport.HoldHandoff = true;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("next-learner-token"));
        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.HandoffCalls >= 1, 5f);

        Abxr.EndSession();
        transport.HoldHandoff = false;
        yield return null;
        yield return null;

        Assert.AreEqual(0, transport.HandoffsApplied, "The service must not end up holding a session EndSession cleared.");
        CollectionAssert.IsEmpty(results);
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated);
    }

    // ── Auth handoff returned to a launcher still running ─────────────────
    // App 2 sends the launcher's SessionId back, which the launcher matches whatever became of the token.

    [UnityTest]
    public IEnumerator AuthHandoff_ReturnedWithoutAToken_OnTheServiceTransport_IsIgnored()
    {
        var transport = UseOfflineDeviceAuth();
        transport.IsServiceTransport = true;
        // Without a token the teardown flush would log an error on REST, so data goes to the scripted transport too.
        AbxrSubsystem.Instance.SetTransportForTesting(transport);
        // The service strips the token from its auth response, so the launcher has none to hand over and none comes back.
        var response = BuildTestAuthResponse();
        response.Token = null;
        SimulateAuth(response);
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        auth.SetSessionId("launcher-session");
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        string returned = AbxrSubsystem.Instance.GetHandoffJsonForTesting();
        StringAssert.Contains("\"Token\":\"\"", returned);
        AbxrAuthService.SetAuthHandoffForTesting(returned);

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return null;

        Assert.IsTrue(auth.Authenticated, "The launcher keeps its session.");
        Assert.AreEqual(0, transport.QuitCalls, "Its session isn't ended, so open events aren't closed as application_quit.");
        CollectionAssert.IsEmpty(results, "OnAuthCompleted must not fire again: a handler that launches App 2 would launch it again.");
        Assert.AreEqual(0, transport.HandoffCalls);
    }

    [UnityTest]
    public IEnumerator AuthHandoff_ReturnedAfterAppTwoReauthenticated_IsIgnored()
    {
        UseOfflineDeviceAuth();
        SimulateAuth();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        auth.SetSessionId("launcher-session");
        string token = Abxr.GetAuthResponse().Token;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        // App 2 re-authenticated without changing the user data, so the session comes back with another token.
        AbxrAuthService.SetAuthHandoffForTesting(AbxrSubsystem.Instance.GetHandoffJsonForTesting().Replace(token, "app-two-token"));

        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return null;

        Assert.IsTrue(auth.Authenticated, "The launcher keeps its session.");
        Assert.AreEqual(token, Abxr.GetAuthResponse().Token);
        CollectionAssert.IsEmpty(results);
    }

    [UnityTest]
    public IEnumerator AuthHandoff_Adopted_SendsTheLaunchersSessionIdOn()
    {
        UseOfflineDeviceAuth();
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("launcher-token", sessionId: "launcher-session"));

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { true }, results);
        StringAssert.Contains("\"SessionId\":\"launcher-session\"", AbxrSubsystem.Instance.GetHandoffJsonForTesting(),
            "The return handoff must carry the launcher's session, which is what the launcher matches.");
    }

    [UnityTest]
    public IEnumerator AuthHandoff_FromTheSameLauncherSession_WithNewUserData_ReplacesTheSession()
    {
        UseOfflineDeviceAuth();
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("launcher-token", sessionId: "launcher-session"));
        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1, 5f);

        // App 3 returning this app's session, unchanged.
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("launcher-token", sessionId: "launcher-session"));
        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return null;
        CollectionAssert.AreEqual(new[] { true }, results, "The same session with the same user data is kept.");

        // The launcher set the next learner's user data, which keeps its SessionId, and launched this app again.
        AbxrAuthService.SetAuthHandoffForTesting(HandoffJson("launcher-token", sessionId: "launcher-session", userDataId: "next-learner"));
        SubsystemGO.SendMessage("OnApplicationFocus", true);
        yield return WaitFor(() => results.Count >= 2, 5f);

        CollectionAssert.AreEqual(new[] { true, true }, results, "Ignoring it would record the next learner under the previous one.");
        Assert.AreEqual("next-learner", Abxr.GetAuthResponse().UserData["id"]);
    }

    /// <summary>Routes device auth to a scripted transport that answers offline, with valid-looking legacy credentials.</summary>
    private AuthRetryTests.ScriptedAuthTransport UseOfflineDeviceAuth()
    {
        var transport = new AuthRetryTests.ScriptedAuthTransport(new AuthTransportResult(false, null, false, 0, networkError: true));
        AbxrSubsystem.Instance.AuthServiceForTesting.SetTransportGetter(() => transport);
        SetRuntimeAuth(new RuntimeAuthConfig
        {
            authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" },
            useAppTokens = false,
            buildType = "development",
            appId = "12345678-1234-1234-1234-123456789012",
            orgId = "87654321-4321-4321-4321-210987654321",
            authSecret = "test-secret"
        });
        ModifyConfig("restUrl", "http://127.0.0.1:1/"); // what the sessions record goes nowhere
        // Unit-test SSO on the host's asset would add a SetUserData re-auth after each success.
        AbxrSubsystem.UnitTestSsoSimulationFromConfigAllowed = false;
        return transport;
    }

    /// <summary>Authenticates with a handoff that stays for the life of the process, as the command line and a WebGL URL do.</summary>
    private static IEnumerator AdoptFixedHandoff(List<bool> results)
    {
        AbxrAuthService.SetFixedAuthHandoffForTesting(HandoffJson("previous-learner-token"));
        Abxr.OnAuthCompleted += (success, _) => results.Add(success);
        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1, 5f);
        CollectionAssert.AreEqual(new[] { true }, results);
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.SessionUsedAuthHandoff(), "The first session adopts the handoff.");
    }

    /// <summary>
    /// A launcher's handoff payload (the fields GetHandoffJson sends that a receiver reads) for a session with this token.
    /// Without a sessionId it is what an SDK before 3.0 sends.
    /// </summary>
    private static string HandoffJson(string token, string sessionId = null, string userDataId = null) =>
        "{\"Token\":\"" + token + "\",\"Secret\":\"handoff-secret\",\"AppId\":\"12345678-1234-1234-1234-123456789012\",\"UserId\":\"learner\""
        + ",\"UserData\":" + (userDataId != null ? "{\"id\":\"" + userDataId + "\"}" : "{}")
        + (sessionId != null ? ",\"SessionId\":\"" + sessionId + "\"" : "") + "}";

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    // ── SetUserData ───────────────────────────────────────────────────────

    [Test]
    public void SetUserData_DoesNotThrow()
    {
        SimulateAuth();
        // id = primary user identifier (userData.id); additionalUserData merged into userData
        Assert.DoesNotThrow(() => Abxr.SetUserData("new-user-id", new Dictionary<string, string> { ["role"] = "admin" }));
    }
}

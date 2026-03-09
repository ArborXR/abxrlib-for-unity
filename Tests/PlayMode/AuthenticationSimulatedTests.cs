// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests using simulated auth only (no API calls): auth state, override setters, module list, session management.
// First-stage auth (config, validation, handoff) is in AuthenticationFirstStageTests.
using System.Collections.Generic;
using AbxrLib.Runtime;
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
        var userData = Abxr.GetUserData();
        Assert.IsNotNull(userData);
        Assert.IsTrue(userData.ContainsKey("userId"));
        Assert.AreEqual("learner-42", userData["userId"]);
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
        Assert.AreEqual("fallback@example.com", userData["userId"]);
        // Simulated auth does not set transport auth headers; flush logs this. We EndSession here and expect the log so TearDown does not run EndSession (avoids unhandled log).
        RunEndSessionInTearDown = false;
        LogAssert.Expect(LogType.Error, "Cannot set auth headers - authentication tokens are missing");
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
        LogAssert.Expect(LogType.Error, "Subscribe to OnModuleTarget before running modules");
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
        LogAssert.Expect(LogType.Error, "No modules available");
        Assert.IsFalse(Abxr.StartModuleAtIndex(-1));
    }

    [Test]
    public void StartModuleAtIndex_IndexOutOfRange_ReturnsFalse()
    {
        SimulateAuth();
        LogAssert.Expect(LogType.Error, "No modules available");
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
        LogAssert.Expect(LogType.Error, "Subscribe to OnModuleTarget before running modules");
        SimulateAuth(BuildTestAuthResponse(modules: modules));
        LogAssert.Expect(LogType.Error, "Need to subscribe to OnModuleTarget before running modules");
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
        Assert.AreEqual(0, Abxr.GetSuperMetaData().Count);
    }

    // ── SetUserData ───────────────────────────────────────────────────────

    [Test]
    public void SetUserData_DoesNotThrow()
    {
        SimulateAuth();
        Assert.DoesNotThrow(() => Abxr.SetUserData("new-user-id", new Dictionary<string, string> { ["role"] = "admin" }));
    }
}

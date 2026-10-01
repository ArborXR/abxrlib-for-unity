// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode: which identity device auth uses (SDK-60 step 3, SDK-59 RFC §03 and D2). An org credential wins, then a
// stored pairing, then none, which is quiet. The choice holds for the launch. A paired bootstrap refusal revokes on 401
// and suspends on 403, without latching the session. Also the subsystem's wiring around pairing: where the prompt and
// its input go, recording while unpaired, and auth after a new pairing.
// Drives the real AbxrAuthService through AuthRetryTests' scripted transport and an in-memory pairing store, so no
// network or PlayerPrefs is involved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Services.Pairing;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class PairingIdentityTests : AbxrPlayModeTestBase
{
    private const string AppToken = "eyJhbGciOiJub25lIn0.eyJhcHAiOiJ0ZXN0In0.sig";
    private const string OrgToken = "eyJhbGciOiJub25lIn0.eyJvcmciOiJ0ZXN0In0.sig";
    private const string InstanceToken = "0123456789abcdef0123456789abcdef01234567";
    private const string InstanceId = "9b2c6a58-2f61-4c1e-9d0a-3f6f1d1e2a11";
    private const string OtherInstanceToken = "fedcba9876543210fedcba9876543210fedcba98";
    private const string OtherInstanceId = "0d5e3c1b-7a24-4f9e-8b6d-2c1a0f9e8d77";
    private const string RetryingLog =
        "Authentication failure: Device authentication failed (no connection); the SDK is retrying in the background.";

    private const string AuthorizedBody =
        "{\"token\":\"eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.sig\",\"secret\":\"test-secret\",\"appId\":\"12345678-1234-1234-1234-123456789012\"";
    private static readonly AuthTransportResult Authorized = new AuthTransportResult(true, AuthorizedBody + "}", false, 200);
    private static readonly AuthTransportResult Unauthorized =
        new AuthTransportResult(false, "{\"detail\":\"Invalid AppInstanceToken\"}", true, 401);
    private static readonly AuthTransportResult Forbidden =
        new AuthTransportResult(false, "{\"detail\":\"Access suspended\"}", true, 403);
    private static readonly AuthTransportResult Offline =
        new AuthTransportResult(false, "No response body.", false, 0, networkError: true);

    private MemoryPairingStore _store;
    private readonly List<(bool success, string reason)> _reports = new List<(bool, string)>();

    protected override void CreateSubsystemIfNeeded()
    {
        // Each test chooses its stored pairing first, then creates the subsystem.
    }

    private FakeAuthUi _ui;
    private readonly List<(Abxr.PairingState state, Abxr.PairingChangeReason reason)> _stateEvents =
        new List<(Abxr.PairingState, Abxr.PairingChangeReason)>();

    private void RecordState(Abxr.PairingState state, Abxr.PairingChangeReason reason) => _stateEvents.Add((state, reason));

    [TearDown]
    public void UnsubscribeReports()
    {
        Abxr.OnAuthCompleted -= Record;
        Abxr.OnPairingStateChanged -= RecordState;
        _stateEvents.Clear();
        if (_ui != null) AbxrUi.UnregisterAuthUi(_ui);
        _ui = null;
    }

    private void Record(bool success, string reason) => _reports.Add((success, reason));

    // ── No identity ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator NoIdentity_IsQuiet()
    {
        var transport = Start(pairedAs: null, Authorized);
        LogAssert.Expect(LogType.Log, new Regex(Regex.Escape("Not authenticating: No organization identity.")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        CollectionAssert.AreEqual(new[] { (false, "No organization identity") }, _reports, "One report, and no error log.");
        Assert.AreEqual(0, transport.AuthCalls);
        Assert.AreEqual(Abxr.PairingState.Unpaired, Pairing.State);
    }

    [UnityTest]
    public IEnumerator FirstAttempt_ReportsOnALaterFrame()
    {
        Start(pairedAs: null, Authorized);

        Abxr.StartAuthentication();
        Assert.IsEmpty(_reports, "Nothing fires inside the call, so subscribers added this frame still hear it.");
        Assert.AreEqual(Abxr.PairingState.Resolving, Pairing.State);
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.AreEqual(1, _reports.Count);
    }

    [UnityTest]
    public IEnumerator IdentityHoldsForTheLaunch_ALaterOrgTokenIsIgnored()
    {
        var transport = Start(pairedAs: null, Authorized);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        // A development build honors a baked org token, so without the hold this one would authenticate.
        SetRuntimeAuth(Auth(OrgToken, "development"));
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 1, 5f);

        Assert.AreEqual((false, "No organization identity"), _reports[1]);
        Assert.AreEqual(0, transport.AuthCalls, "Unpaired at startup stays unpaired until the next launch.");
    }

    [UnityTest]
    public IEnumerator WherePairingCantRun_ALaterOrgToken_Authenticates()
    {
        var transport = Start(pairedAs: null, Authorized, pairingPlatform: false);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        SetRuntimeAuth(Auth(OrgToken, "development"));
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 1, 5f);

        Assert.AreEqual((true, (string)null), _reports[1], "A standalone or Editor build that sets its org late still signs in.");
        Assert.AreEqual(1, transport.AuthCalls);
        Assert.AreEqual(Abxr.PairingState.Managed, Pairing.State);
    }

    [UnityTest]
    public IEnumerator StartNewSession_WhileResolving_SettlesOnALaterFrame()
    {
        Start(pairedAs: null, Authorized);

        Abxr.StartNewSession();
        Assert.AreEqual(Abxr.PairingState.Resolving, Pairing.State, "The first attempt waits, like auto-start, before deciding identity.");
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.AreEqual(Abxr.PairingState.Unpaired, Pairing.State);
        CollectionAssert.AreEqual(new[] { (false, "No organization identity") }, _reports);
    }

    // ── Paired bootstrap ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator StoredPairing_BootstrapsAsTheInstance()
    {
        var transport = Start(pairedAs: InstanceId, Authorized);

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        CollectionAssert.AreEqual(new[] { (true, (string)null) }, _reports);
        Assert.AreEqual(Abxr.PairingState.Paired, Pairing.State);
        Assert.AreEqual(AppToken, transport.LastPayload.appToken);
        Assert.AreEqual(InstanceToken, transport.LastPayload.appInstanceToken);
        Assert.AreEqual(InstanceId, transport.LastPayload.deviceId, "The instance id is the device id.");
        Assert.IsNull(transport.LastPayload.orgToken);
        Assert.IsNull(transport.LastPayload.priorAppInstanceId);
    }

    [UnityTest]
    public IEnumerator PairedBootstrap_StoresTheDeviceNameItReturns()
    {
        Start(pairedAs: InstanceId, new AuthTransportResult(true, AuthorizedBody + ",\"deviceName\":\"Headset 12\"}", false, 200));

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.AreEqual("Headset 12", Pairing.DeviceName);
        Assert.AreEqual("Headset 12", _store.DeviceName);
    }

    [UnityTest]
    public IEnumerator PairedBootstrap_WithoutADeviceNameKey_KeepsTheStoredName()
    {
        Start(pairedAs: InstanceId, Authorized, deviceName: "Headset 12");

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.IsTrue(_reports[0].success);
        Assert.AreEqual("Headset 12", _store.DeviceName, "A backend that doesn't send the key mustn't clear the name.");
    }

    [UnityTest]
    public IEnumerator PairedBootstrap_Unauthorized_RevokesWithoutLatching()
    {
        var transport = Start(pairedAs: InstanceId, Unauthorized);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Pairing revoked")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.AreEqual((false, "Pairing revoked"), _reports[0]);
        Assert.AreEqual(Abxr.PairingState.Unpaired, Pairing.State);
        Assert.IsNull(_store.Token);

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 1, 5f);
        Assert.AreEqual((false, "No organization identity"), _reports[1], "Not the session-wide rejection latch.");
        Assert.AreEqual(1, transport.AuthCalls);
    }

    [UnityTest]
    public IEnumerator PairedBootstrap_Forbidden_KeepsThePairingAndStopsForTheLaunch()
    {
        var transport = Start(pairedAs: InstanceId, Forbidden);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Access suspended")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.AreEqual((false, "Access suspended"), _reports[0]);
        Assert.AreEqual(Abxr.PairingState.Paired, Pairing.State);
        Assert.AreEqual(InstanceToken, _store.Token);
        Assert.IsTrue(Pairing.IsSuspendedForSession);

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 1, 5f);
        Assert.AreEqual((false, "Access suspended"), _reports[1]);
        Assert.AreEqual(1, transport.AuthCalls, "Nothing more is sent with a suspended pairing this launch.");
    }

    [UnityTest]
    public IEnumerator PairedBootstrap_Offline_RetriesAndKeepsThePairing()
    {
        var transport = Start(pairedAs: InstanceId, Offline);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(
            "Authentication failure: Device authentication failed (no connection); the SDK is retrying in the background.")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 2, 5f);

        Assert.GreaterOrEqual(transport.AuthCalls, 2);
        Assert.AreEqual(Abxr.PairingState.Paired, Pairing.State);
        Assert.AreEqual(InstanceToken, _store.Token);
        Abxr.EndSession();
    }

    // ── Managed ───────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator OrgToken_WinsOverAStoredPairing_AndSendsItAsThePriorInstance()
    {
        // A development build, since production ignores a baked org token and only takes one from the device or a launch.
        var transport = Start(pairedAs: InstanceId, Authorized, orgToken: OrgToken, buildType: "development");

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.IsTrue(_reports[0].success);
        Assert.AreEqual(Abxr.PairingState.Managed, Pairing.State);
        Assert.AreEqual(OrgToken, transport.LastPayload.orgToken);
        Assert.IsNull(transport.LastPayload.appInstanceToken);
        Assert.AreEqual(InstanceId, transport.LastPayload.priorAppInstanceId);
        Assert.AreEqual(InstanceToken, _store.Token, "The stored pairing is kept for a later migration.");
    }

    // ── Subsystem wiring ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator Unpaired_RecordsNothingUntilThePromptOpens()
    {
        Start(pairedAs: null, Authorized);
        Abxr.OnInputRequested = (_, _, _, _) => { };
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Abxr.Event("before_pairing");
        Assert.IsFalse(PendingEventNames().Contains("before_pairing"), "A never-paired install records nothing.");

        Assert.IsTrue(Pairing.StartPairing());
        Abxr.Event("while_prompting");
        Assert.IsTrue(PendingEventNames().Contains("while_prompting"), "Opening the prompt is the opt-in.");
    }

    [UnityTest]
    public IEnumerator SettlingUnpaired_DropsWhatWasRecordedWhileResolving()
    {
        Start(pairedAs: null, Authorized);
        Abxr.Event("while_resolving");
        Assert.IsTrue(PendingEventNames().Contains("while_resolving"), "Resolving buffers, as before.");

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.IsFalse(PendingEventNames().Contains("while_resolving"), "A later pairing mustn't send data recorded before anyone opted in.");
    }

    [UnityTest]
    public IEnumerator ThePrompt_GoesToTheAppHandler_AndItsInputComesBack()
    {
        Start(pairedAs: null, Authorized);
        var requests = new List<string>();
        Abxr.OnInputRequested = (type, _, _, _) => requests.Add(type);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.IsTrue(Pairing.StartPairing());
        CollectionAssert.AreEqual(new[] { AbxrPairingService.PasscodeInputType }, requests);

        Abxr.OnInputSubmitted("**skip**");
        Assert.AreEqual(Abxr.PairingState.Unpaired, Pairing.State, "The input went to pairing, whose skip closes the prompt.");
    }

    [UnityTest]
    public IEnumerator ThePrompt_WithoutAHandler_ShowsThePinPad()
    {
        Start(pairedAs: null, Authorized);
        _ui = new FakeAuthUi();
        AbxrUi.RegisterAuthUi(_ui);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Assert.IsTrue(Pairing.StartPairing());

        Assert.AreEqual(AuthUiKind.PairingPasscode, _ui.Shown);
        Assert.AreEqual("Enter Pairing Passcode", _ui.Prompt);
    }

    [UnityTest]
    public IEnumerator ANewPairing_StartsAuthAsTheInstance()
    {
        var transport = Start(pairedAs: null, Authorized);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        Assert.AreEqual(0, transport.AuthCalls);

        Pairing.SetAppInstanceToken(InstanceToken, InstanceId);
        yield return WaitFor(() => _reports.Count > 1, 5f);

        Assert.AreEqual((true, (string)null), _reports[1], "False at startup, then true once paired.");
        Assert.AreEqual(1, transport.AuthCalls);
        Assert.AreEqual(InstanceToken, transport.LastPayload.appInstanceToken);
        Assert.AreEqual(InstanceId, transport.LastPayload.deviceId);
    }

    // ── Public API ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator OnPairingStateChanged_ReportsStartup_AndGetPairingStateAgrees()
    {
        Start(pairedAs: null, Authorized);
        Abxr.OnPairingStateChanged += RecordState;
        Assert.AreEqual(Abxr.PairingState.Resolving, Abxr.GetPairingState());

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Startup) }, _stateEvents);
        Assert.AreEqual(Abxr.PairingState.Unpaired, Abxr.GetPairingState());
    }

    [UnityTest]
    public IEnumerator StartPairing_ThenCancelPairing_Dismisses()
    {
        Start(pairedAs: null, Authorized);
        Abxr.OnInputRequested = (_, _, _, _) => { };
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        Abxr.OnPairingStateChanged += RecordState;

        Assert.IsTrue(Abxr.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Prompting, Abxr.GetPairingState());
        Abxr.CancelPairing();

        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _stateEvents,
            "Prompting itself never fires the event.");
    }

    [UnityTest]
    public IEnumerator RedeemPairingPasscode_WhenPaired_CallsBackWithInvalidState()
    {
        Start(pairedAs: InstanceId, Authorized, deviceName: "Headset 12");
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        var results = new List<Abxr.PairingRedeemResult>();

        Abxr.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, Abxr.GetLastPairingRedeemResult().Error);
        Assert.AreEqual("Headset 12", Abxr.GetPairedDeviceName());
    }

    [UnityTest]
    public IEnumerator ClearPairing_FromPaired_FiresCleared()
    {
        Start(pairedAs: InstanceId, Authorized);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        Abxr.OnPairingStateChanged += RecordState;

        Abxr.ClearPairing();

        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Cleared) }, _stateEvents);
        Assert.IsNull(_store.Token);
        Assert.IsNull(Abxr.GetPairedDeviceName());
    }

    [UnityTest]
    public IEnumerator ClearPairing_WhileAuthenticated_EndsTheSession()
    {
        Start(pairedAs: InstanceId, Authorized);
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated);

        Abxr.ClearPairing();

        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated, "Storage and the AI proxy mustn't keep using the cleared instance.");
    }

    [UnityTest]
    public IEnumerator ClearPairing_WhileRetrying_StopsTheOldAttempt()
    {
        var transport = Start(pairedAs: InstanceId, Offline);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(RetryingLog)));
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Abxr.ClearPairing();
        int callsAtClear = transport.AuthCalls;
        transport.AnswerNextWith(Authorized);
        yield return new WaitForSecondsRealtime(3f);

        Assert.AreEqual(callsAtClear, transport.AuthCalls, "The cleared instance's attempt stopped retrying.");
        Assert.IsFalse(_reports.Exists(r => r.success), "Nothing authenticates as the cleared instance.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated);
    }

    [UnityTest]
    public IEnumerator RePairing_WhileTheOldAttemptRetries_AuthenticatesAsTheNewInstance()
    {
        var transport = Start(pairedAs: InstanceId, Offline);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(RetryingLog)));
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        Abxr.ClearPairing();
        transport.AnswerNextWith(Authorized);
        Pairing.SetAppInstanceToken(OtherInstanceToken, OtherInstanceId);
        yield return WaitFor(() => _reports.Exists(r => r.success), 5f);

        Assert.IsTrue(_reports.Exists(r => r.success));
        Assert.AreEqual(OtherInstanceToken, transport.LastPayload.appInstanceToken);
        Assert.AreEqual(OtherInstanceId, transport.LastPayload.deviceId);
    }

    [UnityTest]
    public IEnumerator ReplacingAPairing_WhileItsAttemptRetries_AuthenticatesAsTheNewInstance()
    {
        var transport = Start(pairedAs: InstanceId, Offline);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(RetryingLog)));
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);

        transport.AnswerNextWith(Authorized);
        Pairing.SetAppInstanceToken(OtherInstanceToken, OtherInstanceId);
        yield return WaitFor(() => _reports.Exists(r => r.success), 5f);

        Assert.IsTrue(_reports.Exists(r => r.success));
        Assert.AreEqual(OtherInstanceToken, transport.LastPayload.appInstanceToken);
        Assert.AreEqual(OtherInstanceId, transport.LastPayload.deviceId);
    }

    [UnityTest]
    public IEnumerator AnInvalidPairingUrl_RefusesStartPairing()
    {
        Start(pairedAs: null, Authorized);
        ModifyConfig("pairingUrl", "api.xrdm.app");
        Abxr.OnInputRequested = (_, _, _, _) => { };
        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 5f);
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("isn't an HTTP or HTTPS URL")));

        Assert.IsFalse(Abxr.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Unpaired, Abxr.GetPairingState());
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static List<string> PendingEventNames() =>
        AbxrSubsystem.Instance.GetTransportForTesting().GetPendingEventsForTesting().ConvertAll(e => e.name);

    private static AbxrPairingService Pairing => AbxrSubsystem.Instance.PairingServiceForTesting;

    /// <summary>A production build with an App Token, optionally a stored pairing, routed through a scripted transport.</summary>
    private AuthRetryTests.ScriptedAuthTransport Start(string pairedAs, AuthTransportResult response, string orgToken = null,
        string deviceName = null, string buildType = "production", bool pairingPlatform = true)
    {
        _reports.Clear();
        _store = new MemoryPairingStore();
        if (pairedAs != null) _store.Save(InstanceToken, pairedAs, deviceName);
        AbxrSubsystem.NextPairingStoreForTesting = _store;
        AbxrSubsystem.PairingPlatformSupportedForTesting = pairingPlatform;
        CreateSubsystem();

        var transport = new AuthRetryTests.ScriptedAuthTransport(response);
        AbxrSubsystem.Instance.AuthServiceForTesting.SetTransportGetter(() => transport);
        SetRuntimeAuth(Auth(orgToken, buildType));
        ModifyConfig("sendRetryIntervalSeconds", 1);
        // A host whose AbxrLib asset sets unit-test SSO would otherwise start a SetUserData re-auth after each success.
        AbxrSubsystem.UnitTestSsoSimulationFromConfigAllowed = false;
        Abxr.OnAuthCompleted += Record;
        return transport;
    }

    private static RuntimeAuthConfig Auth(string orgToken, string buildType) => new RuntimeAuthConfig
    {
        authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" },
        useAppTokens = true,
        buildType = buildType,
        appToken = AppToken,
        orgToken = orgToken
    };

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private sealed class FakeAuthUi : IAbxrAuthUi
    {
        public AuthUiKind? Shown;
        public string Prompt;
        public void Show(AuthUiKind kind) => Shown = kind;
        public void SetPrompt(string prompt) => Prompt = prompt;
        public void Hide() { }
        public void StopProcessing() { }
    }

    private sealed class MemoryPairingStore : IPairingStore
    {
        public string Token;
        public string InstanceId;
        public string DeviceName;

        public bool TryLoad(out string token, out string instanceId)
        {
            token = Token;
            instanceId = InstanceId;
            return !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(InstanceId);
        }

        public string LoadDeviceName() => DeviceName;

        public void Save(string token, string instanceId, string deviceName)
        {
            Token = token;
            InstanceId = instanceId;
            DeviceName = deviceName;
        }

        public void SaveDeviceName(string deviceName) => DeviceName = deviceName;

        public void Clear()
        {
            Token = null;
            InstanceId = null;
            DeviceName = null;
        }
    }
}

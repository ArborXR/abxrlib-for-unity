// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the pairing state machine (SDK-60), with the store, the network, and the SDK around it faked.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Services.Pairing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class PairingServiceTests
{
    private const string Token = "0123456789abcdef0123456789abcdef01234567";
    private const string InstanceId = "9b2c6a58-2f61-4c1e-9d0a-3f6f1d1e2a11";
    private const string OtherToken = "fedcba9876543210fedcba9876543210fedcba98";
    private const string OtherInstanceId = "0d5e3c1b-7a24-4f9e-8b6d-2c1a0f9e8d77";
    private static readonly string SuccessBody = $"{{\"app_instance_token\":\"{Token}\",\"app_instance_id\":\"{InstanceId}\"}}";

    private FakeHost _host;
    private MemoryStore _store;
    private FakeClient _client;
    private FakeAuthUi _ui;
    private List<(string type, string prompt, string domain, string error)> _requests;
    private List<(Abxr.PairingState state, Abxr.PairingChangeReason reason)> _events;

    [SetUp]
    public void SetUp()
    {
        _host = new FakeHost();
        _store = new MemoryStore();
        _client = new FakeClient();
        _ui = new FakeAuthUi();
        AbxrUi.RegisterAuthUi(_ui);
        _requests = new List<(string, string, string, string)>();
        _events = new List<(Abxr.PairingState, Abxr.PairingChangeReason)>();
    }

    [TearDown]
    public void TearDown() => AbxrUi.UnregisterAuthUi(_ui);

    private AbxrPairingService Create()
    {
        var service = new AbxrPairingService(_store, _client, _host);
        service.OnInputRequested = (type, prompt, domain, error) => _requests.Add((type, prompt, domain, error));
        service.OnStateChanged = (state, reason) => _events.Add((state, reason));
        return service;
    }

    private AbxrPairingService CreateUnpaired()
    {
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);
        _events.Clear();
        return service;
    }

    private AbxrPairingService CreatePaired()
    {
        _store.Save(Token, InstanceId, null);
        _store.Saves = 0;
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);
        _events.Clear();
        return service;
    }

    private AbxrPairingService CreatePrompting()
    {
        var service = CreateUnpaired();
        Assert.IsTrue(service.StartPairing());
        _requests.Clear();
        return service;
    }

    private static PairingHttpResponse Ok(string deviceName = null) => new PairingHttpResponse(200, deviceName == null
        ? SuccessBody
        : $"{{\"app_instance_token\":\"{Token}\",\"app_instance_id\":\"{InstanceId}\",\"device_name\":\"{deviceName}\"}}");
    private static PairingHttpResponse Status(long status, string retryAfter = null) => new PairingHttpResponse(status, "{\"error\":\"nope\"}", retryAfter);
    private static PairingHttpResponse Coded(long status, string code) => new PairingHttpResponse(status, $"{{\"error\":\"nope\",\"code\":\"{code}\"}}");
    private static PairingHttpResponse NameTaken() =>
        new PairingHttpResponse(409, "{\"error\":\"That name is taken\",\"code\":\"device_name_exists\",\"device_name\":\"Headset 12\"}");

    private static PairingHttpResponse NameRequested() => Coded(422, "device_name_requested");

    /// <summary>
    /// Answers the passcode step. With a name, the Portal first asks for one (the passcode allows naming), and the
    /// name step answers it. Forgets those requests.
    /// </summary>
    private void SubmitPrompt(AbxrPairingService service, string passcode = "483921", string deviceName = null)
    {
        service.SubmitInput(passcode);
        if (deviceName != null)
        {
            _client.Respond(NameRequested());
            service.SubmitInput(deviceName);
        }
        _requests.Clear();
    }

    // ── Resolving and startup ─────────────────────────────────────

    [Test]
    public void StartsResolving_AndWontPromptYet()
    {
        var service = Create();

        Assert.AreEqual(Abxr.PairingState.Resolving, service.State);
        Assert.IsFalse(service.StartPairing());
        Assert.IsEmpty(_requests);
        Assert.IsEmpty(_events);
    }

    [Test]
    public void Settle_OtherIdentityWins_IsManaged()
    {
        var service = Create();

        service.SettleIdentity(otherIdentityWins: true);

        Assert.AreEqual(Abxr.PairingState.Managed, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Managed, Abxr.PairingChangeReason.Startup) }, _events);
    }

    [Test]
    public void Settle_StoredPairing_IsPairedWithoutAnyCall()
    {
        _store.Save(Token, InstanceId, null);
        var service = Create();

        service.SettleIdentity(otherIdentityWins: false);

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Startup) }, _events);
        Assert.IsTrue(service.TryGetStored(out string token, out string instanceId));
        Assert.AreEqual(Token, token);
        Assert.AreEqual(InstanceId, instanceId);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void Settle_NoIdentity_IsUnpairedAndQuiet()
    {
        var service = Create();

        service.SettleIdentity(otherIdentityWins: false);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Startup) }, _events);
        Assert.IsEmpty(_requests, "The SDK never prompts on its own.");
        Assert.IsEmpty(_client.Sent);
    }

    [Test]
    public void Settle_OtherIdentityBeatsAStoredPairing_AndKeepsIt()
    {
        _store.Save(Token, InstanceId, null);
        var service = Create();

        service.SettleIdentity(otherIdentityWins: true);

        Assert.AreEqual(Abxr.PairingState.Managed, service.State);
        Assert.IsTrue(service.TryGetStored(out _, out string instanceId), "Auth sends it as priorAppInstanceId.");
        Assert.AreEqual(InstanceId, instanceId);
        Assert.AreEqual(0, _store.Clears);
    }

    [Test]
    public void Settle_HappensOnce()
    {
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);

        service.SettleIdentity(otherIdentityWins: true);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.AreEqual(1, _events.Count);
    }

    [Test]
    public void Settle_WherePairingRuns_ALaterOrgCredentialWarnsOnceAndHolds()
    {
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("turn off Enable Auto Start Authentication")));

        service.SettleIdentity(otherIdentityWins: true);
        service.SettleIdentity(otherIdentityWins: true);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.AreEqual(1, _events.Count);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Settle_WherePairingCantRun_ALaterOrgCredentialWins()
    {
        _host.IsPlatformSupported = false;
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);

        service.SettleIdentity(otherIdentityWins: true);

        Assert.AreEqual(Abxr.PairingState.Managed, service.State, "Holding \"no identity\" protects nothing where pairing can't run.");
        CollectionAssert.AreEqual(new[]
        {
            (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Startup),
            (Abxr.PairingState.Managed, Abxr.PairingChangeReason.Startup)
        }, _events);
    }

    [Test]
    public void UnsupportedPlatform_IgnoresAStoredPairing()
    {
        _store.Save(Token, InstanceId, null);
        _host.IsPlatformSupported = false;
        var service = Create();

        service.SettleIdentity(otherIdentityWins: false);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsFalse(service.TryGetStored(out _, out _));
    }

    // ── StartPairing ──────────────────────────────────────────────

    [Test]
    public void StartPairing_FromUnpaired_RequestsAPairingPasscode()
    {
        var service = CreateUnpaired();

        Assert.IsTrue(service.StartPairing());

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.IsTrue(service.IsInputRequestPending);
        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingPasscode", _requests[0].type);
        Assert.AreEqual("", _requests[0].error);
        Assert.IsEmpty(_events, "Prompting is transient, so it doesn't fire the event.");
    }

    [Test]
    public void StartPairing_WhenManaged_IsRefused()
    {
        var service = Create();
        service.SettleIdentity(otherIdentityWins: true);

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Managed, service.State);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void StartPairing_WhenPaired_IsRefused()
    {
        var service = CreatePaired();

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void StartPairing_WhilePrompting_IsRefused()
    {
        var service = CreatePrompting();

        Assert.IsFalse(service.StartPairing());
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void StartPairing_WhileRedeeming_IsRefused()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
    }

    [Test]
    public void StartPairing_WithNothingToShowThePrompt_IsRefused()
    {
        var service = CreateUnpaired();
        _host.CanPresentPrompt = false;

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void StartPairing_OnAnUnsupportedPlatform_IsRefused()
    {
        _host.IsPlatformSupported = false;
        var service = CreateUnpaired();

        Assert.IsFalse(service.StartPairing());
        Assert.IsEmpty(_requests);
    }

    [TestCase("", "https://api.xrdm.app/")]
    [TestCase(null, "https://api.xrdm.app/")]
    [TestCase("app.token.jwt", "")]
    [TestCase("app.token.jwt", "api.xrdm.app")]
    [TestCase("app.token.jwt", "ftp://api.xrdm.app/")]
    public void StartPairing_WithoutAnAppTokenOrValidPairingUrl_IsRefused(string appToken, string pairingUrl)
    {
        _host.AppToken = appToken;
        _host.PairingUrl = pairingUrl;
        var service = CreateUnpaired();

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
    }

    // ── Dismissing ────────────────────────────────────────────────

    [Test]
    public void Skip_ClosesThePromptAndStoresNothing()
    {
        var service = CreatePrompting();

        service.SubmitInput("**skip**");

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.AreEqual(0, _store.Saves);
        Assert.IsEmpty(_client.Sent);
        Assert.AreEqual(1, _ui.Hides);
        Assert.IsFalse(service.IsInputRequestPending);
    }

    [Test]
    public void CancelPairing_ClosesThePromptAndStoresNothing()
    {
        var service = CreatePrompting();

        service.CancelPairing();

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void CancelPairing_WithNoPromptOpen_DoesNothing()
    {
        var service = CreateUnpaired();

        service.CancelPairing();

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsEmpty(_events);
    }

    [Test]
    public void AfterADismiss_TheAppCanAskAgain()
    {
        var service = CreatePrompting();
        service.CancelPairing();

        Assert.IsTrue(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
    }

    // ── Redeeming from the prompt ─────────────────────────────────

    [Test]
    public void Submit_SendsOneRedeem()
    {
        var service = CreatePrompting();

        service.SubmitInput(" 483 921 ");

        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
        Assert.IsFalse(service.IsInputRequestPending);
        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual("https://api.xrdm.dev/api/insights-pairing/redeem", _client.Sent[0].url);
        StringAssert.Contains("\"passcode\":\"483921\"", _client.Sent[0].json);
        StringAssert.Contains("\"app_token\":\"app.token.jwt\"", _client.Sent[0].json);
        StringAssert.Contains("\"manufacturer\":\"Pico\"", _client.Sent[0].json);
        StringAssert.DoesNotContain("device_name", _client.Sent[0].json, "The passcode goes alone, so the Portal decides whether to ask for a name.");
    }

    [Test]
    public void Submit_Success_StoresBothValuesAndPairs()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired) }, _events);
        Assert.AreEqual(1, _store.Saves);
        Assert.AreEqual(Token, _store.Token);
        Assert.AreEqual(InstanceId, _store.InstanceId);
        Assert.IsTrue(service.LastRedeemResult.Success);
        Assert.AreEqual(1, _ui.Hides);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void Submit_WrongPasscode_RepromptsWithoutAnEvent()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.IsEmpty(_events, "A failed attempt doesn't fire the event.");
        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingPasscode", _requests[0].type);
        Assert.AreEqual(PairingOutcomes.InvalidPasscodeMessage, _requests[0].error);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidPasscode, service.LastRedeemResult.Error);
        Assert.AreEqual(0, _store.Saves);
    }

    [TestCase("12345")]
    [TestCase("1234567")]
    [TestCase("12a456")]
    [TestCase("")]
    public void Submit_MalformedPasscode_RepromptsWithoutSending(string input)
    {
        var service = CreatePrompting();

        service.SubmitInput(input);

        Assert.IsEmpty(_client.Sent, "A typo never spends the app's failure budget.");
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(PairingOutcomes.InvalidPasscodeMessage, _requests[0].error);
    }

    [Test]
    public void Submit_ServiceDown_RepromptsAndNeverRetriesOnItsOwn()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Status(503));

        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(PairingOutcomes.UnavailableMessage, _requests[0].error);
    }

    [Test]
    public void Submit_Offline_IsUnavailable()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(new PairingHttpResponse(0, null, networkError: true, errorDetail: "Request timeout"));

        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, service.LastRedeemResult.Error);
        Assert.AreEqual(1, _client.Sent.Count);
    }

    [Test]
    public void Submit_BuildRejected_Reprompts()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Status(401));

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(PairingOutcomes.BuildRejectedMessage, _requests[0].error);
    }

    [Test]
    public void RateLimited_HoldsAttemptsUntilRetryAfterPasses()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _client.Respond(Status(429, retryAfter: "30"));

        Assert.AreEqual(Abxr.PairingRedeemError.RateLimited, service.LastRedeemResult.Error);
        Assert.AreEqual(30, service.LastRedeemResult.RetryAfterSeconds);
        Assert.AreEqual("Too many attempts. Try again in 30 seconds.", _requests[0].error);

        _host.Now += 10;
        service.SubmitInput("483921");
        Assert.AreEqual(1, _client.Sent.Count, "Held locally while the Portal's budget is saturated.");
        Assert.AreEqual(20, service.LastRedeemResult.RetryAfterSeconds);
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);

        _host.Now += 21;
        service.SubmitInput("483921");
        Assert.AreEqual(2, _client.Sent.Count);
    }

    [Test]
    public void Cancel_WhileRedeeming_ASuccessStillPairs()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        service.CancelPairing();
        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Paired, service.State, "The instance exists; dropping it would orphan it in the Portal.");
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired) }, _events);
    }

    [Test]
    public void Cancel_WhileRedeeming_AFailureDismissesWithoutReprompting()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        service.CancelPairing();
        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void NotNow_WhileThePasscodeIsRedeeming_Cancels()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        Assert.IsTrue(service.IsPromptRedeemInFlight);

        service.SubmitInput("**skip**");
        _client.Respond(Status(400));

        Assert.AreEqual(1, _ui.Hides, "Not now closes the prompt right away, not when the answer comes.");
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.IsEmpty(_requests, "A cancelled prompt doesn't reopen with the failure.");
    }

    [Test]
    public void OtherInput_WhileThePasscodeIsRedeeming_IsIgnored()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        service.SubmitInput("111111");
        _client.Respond(Status(400));

        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual("pairingPasscode", _requests[0].type, "The failure still reopens the prompt.");
    }

    [Test]
    public void Skip_WhileTheNameIsRedeeming_IsIgnored()
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "Headset 12");

        service.SubmitInput("**skip**");
        _client.Respond(Ok(deviceName: "Headset 12"));

        Assert.AreEqual(1, _ui.Hides, "Only the success closes the prompt.");
        Assert.AreEqual(2, _client.Sent.Count, "The name step's skip has no question to answer until the redeem comes back.");
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
    }

    [Test]
    public void AHeadlessRedeem_DoesNotTakePromptInput()
    {
        var service = CreateUnpaired();
        service.RedeemPairingPasscode("483921", _ => { });

        Assert.IsFalse(service.IsPromptRedeemInFlight, "With no prompt, OnInputSubmitted still goes to auth.");
    }

    // ── A redeem whose answer never comes ─────────────────────────

    private const double PastTheDeadline = 30 + AbxrPairingService.StalledRedeemSlackSeconds + 1;

    [Test]
    public void AnUnansweredRedeem_BeforeItsDeadline_StillRefusesStartPairing()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();
        service.RedeemPairingPasscode("483921", results.Add);

        _host.Now += PastTheDeadline - 2;

        Assert.IsFalse(service.StartPairing());
        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
        Assert.IsEmpty(results);
    }

    [Test]
    public void AnUnansweredRedeem_PastItsDeadline_SettlesAsUnavailable()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();
        service.RedeemPairingPasscode("483921", results.Add);

        _host.Now += PastTheDeadline;

        Assert.IsTrue(service.StartPairing(), "A lost answer doesn't hold Redeeming for the rest of the launch.");
        Assert.AreEqual(1, results.Count, "The headless onComplete still fires.");
        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, results[0].Error);
    }

    [Test]
    public void AnUnansweredRedeem_PastItsDeadline_NoLongerBlocksClearPairingOrSetAppInstanceToken()
    {
        var service = CreateUnpaired();
        service.RedeemPairingPasscode("483921", _ => { });
        _host.Now += PastTheDeadline;

        service.ClearPairing();
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);

        service.RedeemPairingPasscode("483921", _ => { });
        _host.Now += PastTheDeadline;
        service.SetAppInstanceToken(Token, InstanceId);
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
    }

    [Test]
    public void AnUnansweredPromptRedeem_PastItsDeadline_ReopensThePromptWithTheError()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _host.Now += PastTheDeadline;

        service.SubmitInput("111111");

        Assert.AreEqual("pairingPasscode", _requests[0].type);
        Assert.AreEqual(PairingOutcomes.UnavailableMessage, _requests[0].error);
        Assert.AreEqual(2, _client.Sent.Count, "The new passcode goes out once the lost one has settled.");
        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
    }

    [Test]
    public void NotNow_OnAnUnansweredPromptRedeem_PastItsDeadline_DismissesAtOnce()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _host.Now += PastTheDeadline;

        service.SubmitInput("**skip**");

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.IsEmpty(_requests, "The cancel isn't undone by re-prompting with the lost answer's error.");
    }

    [Test]
    public void AnAnswerArrivingAfterTheDeadlineSettled_IsIgnored()
    {
        var service = CreateUnpaired();
        service.RedeemPairingPasscode("483921", _ => { });
        _host.Now += PastTheDeadline;
        service.ClearPairing();

        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void ASendThatThrows_SettlesAsUnavailable()
    {
        var service = CreateUnpaired();
        _client.Throw = new InvalidOperationException("boom");
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, results[0].Error);
    }

    [Test]
    public void ASendThatAnswersThenThrows_SettlesOnce()
    {
        var service = CreateUnpaired();
        _client.AnswerBeforeThrowing = Ok();
        _client.Throw = new InvalidOperationException("boom");
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Success);
    }

    [Test]
    public void AResponseArrivingTwice_IsHandledOnce()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _client.Respond(Ok());

        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual(1, _events.Count);
    }

    // ── Naming the headset (INS-511) ──────────────────────────────

    [Test]
    public void APasscodeWithoutNaming_PairsWithoutANameStep()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.IsEmpty(_requests, "The passcode doesn't allow naming, so the headset never asks.");
        Assert.AreEqual(1, _client.Sent.Count);
        Assert.IsNull(service.DeviceName);
    }

    [Test]
    public void ANameRequest_AsksForANameWithASkip()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

        _client.Respond(NameRequested());

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingDeviceName", _requests[0].type);
        Assert.AreEqual("Give this connection a name.", _requests[0].prompt);
        Assert.AreEqual("", _requests[0].error, "Being asked for a name isn't an error.");
        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameRequested, service.LastRedeemResult.Error);
        Assert.IsEmpty(_events);
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void Name_IsSentWithThePasscode()
    {
        var service = CreatePrompting();

        SubmitPrompt(service, deviceName: "  Headset 12 ");

        Assert.AreEqual(2, _client.Sent.Count);
        StringAssert.Contains("\"passcode\":\"483921\",\"device_name\":\"Headset 12\",", _client.Sent[1].json);
        StringAssert.DoesNotContain("skip_device_name", _client.Sent[1].json);
        StringAssert.DoesNotContain("join_existing", _client.Sent[1].json);
    }

    [TestCase("**skip**")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void SkippingTheName_SendsTheSkipFlag(string input)
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");
        _client.Respond(NameRequested());

        service.SubmitInput(input);

        Assert.AreEqual(2, _client.Sent.Count);
        StringAssert.Contains("\"passcode\":\"483921\",\"skip_device_name\":true", _client.Sent[1].json);
        StringAssert.DoesNotContain("\"device_name\"", _client.Sent[1].json);
    }

    [Test]
    public void Success_StoresTheNameThePortalEchoes()
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "headset 12");

        _client.Respond(Ok(deviceName: "Headset 12"));

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual("Headset 12", _store.DeviceName);
        Assert.AreEqual("Headset 12", service.DeviceName);
        Assert.AreEqual("Headset 12", service.LastRedeemResult.DeviceName);
    }

    [Test]
    public void ANameTooLong_IsAskedForAgainWithoutSending()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");
        _client.Respond(NameRequested());
        _requests.Clear();

        service.SubmitInput(new string('x', 65));

        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual("pairingDeviceName", _requests[0].type);
        Assert.AreEqual(PairingOutcomes.DeviceNameInvalidMessage, _requests[0].error);
        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameInvalid, service.LastRedeemResult.Error);
    }

    [Test]
    public void ARequiredName_IsAskedForWithoutASkip()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Coded(422, "device_name_required"));

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual("pairingDeviceNameRequired", _requests[0].type);
        Assert.AreEqual(PairingOutcomes.DeviceNameRequiredMessage, _requests[0].error);
        Assert.IsEmpty(_events);

        service.SubmitInput("**skip**");
        Assert.AreEqual(1, _client.Sent.Count, "Skipping a required name isn't sent.");
        Assert.AreEqual("pairingDeviceNameRequired", _requests[1].type);

        service.SubmitInput("Headset 12");
        Assert.AreEqual(2, _client.Sent.Count);
        StringAssert.Contains("\"passcode\":\"483921\",\"device_name\":\"Headset 12\"", _client.Sent[1].json);
    }

    [Test]
    public void ANameThePortalRejects_IsAskedForAgain_StillRequired()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _client.Respond(Coded(422, "device_name_required"));
        service.SubmitInput("Headset 12");
        _requests.Clear();

        _client.Respond(Coded(422, "device_name_invalid"));

        Assert.AreEqual("pairingDeviceNameRequired", _requests[0].type);
        Assert.AreEqual(PairingOutcomes.DeviceNameInvalidMessage, _requests[0].error);
    }

    [Test]
    public void ANameInUse_AsksToJoinThatDevice()
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "headset 12");

        _client.Respond(NameTaken());

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingDeviceJoin", _requests[0].type);
        Assert.AreEqual("Headset 12 is already in your organization. Add this app to it?", _requests[0].prompt);
        Assert.AreEqual("Headset 12", _requests[0].domain);
        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameExists, service.LastRedeemResult.Error);
        Assert.AreEqual("Headset 12", service.LastRedeemResult.DeviceName);
        Assert.AreEqual(0, _store.Saves, "Nothing is created until the person confirms.");
        Assert.IsEmpty(_events);
    }

    [TestCase("Headset 12")]
    [TestCase("HEADSET 12")]
    public void ConfirmingTheJoin_ResendsWithJoinExisting(string input)
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "headset 12");
        _client.Respond(NameTaken());

        service.SubmitInput(input);

        Assert.AreEqual(3, _client.Sent.Count);
        StringAssert.Contains("\"passcode\":\"483921\",\"device_name\":\"Headset 12\",\"join_existing\":true", _client.Sent[2].json);

        _client.Respond(Ok(deviceName: "Headset 12"));
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual("Headset 12", _store.DeviceName);
    }

    [Test]
    public void DecliningTheJoin_AsksForAnotherName()
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "Headset 12");
        _client.Respond(NameTaken());
        _requests.Clear();

        service.SubmitInput("**skip**");

        Assert.AreEqual(2, _client.Sent.Count);
        Assert.AreEqual("pairingDeviceName", _requests[0].type);

        service.SubmitInput("Headset 13");
        Assert.AreEqual(3, _client.Sent.Count);
        StringAssert.Contains("\"device_name\":\"Headset 13\"", _client.Sent[2].json);
        StringAssert.DoesNotContain("join_existing", _client.Sent[2].json);
    }

    [Test]
    public void ADifferentNameAtTheJoin_IsSentWithoutJoining()
    {
        var service = CreatePrompting();
        SubmitPrompt(service, deviceName: "Headset 12");
        _client.Respond(NameTaken());

        service.SubmitInput("Headset 13");

        StringAssert.Contains("\"device_name\":\"Headset 13\"", _client.Sent[2].json);
        StringAssert.DoesNotContain("join_existing", _client.Sent[2].json);
    }

    [Test]
    public void AWrongPasscode_IsCaughtBeforeAnyNameStep()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        _client.Respond(Status(400));

        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingPasscode", _requests[0].type, "Nobody names the headset for a passcode that doesn't work.");
    }

    [Test]
    public void CancellingAtTheNameStep_Dismisses()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");
        _client.Respond(NameRequested());

        service.CancelPairing();

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void ANewPrompt_StartsFromThePasscodeWithNothingRemembered()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);
        _client.Respond(Coded(422, "device_name_required"));
        service.CancelPairing();
        _requests.Clear();

        Assert.IsTrue(service.StartPairing());
        service.SubmitInput("483921");
        _client.Respond(NameRequested());

        Assert.AreEqual("pairingPasscode", _requests[0].type);
        Assert.AreEqual("pairingDeviceName", _requests[1].type, "The requirement belonged to the old prompt's passcode.");
        Assert.AreEqual(2, _client.Sent.Count);
    }

    [Test]
    public void ARateLimit_IsReportedAtThePasscodeStep()
    {
        var service = CreateUnpaired();
        service.RedeemPairingPasscode("483921", _ => { });
        _client.Respond(Status(429, retryAfter: "30"));
        Assert.IsTrue(service.StartPairing());
        _requests.Clear();

        service.SubmitInput("483921");

        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual(1, _requests.Count);
        Assert.AreEqual("pairingPasscode", _requests[0].type);
        Assert.AreEqual("Too many attempts. Try again in 30 seconds.", _requests[0].error);
    }

    // ── Headless redeem ───────────────────────────────────────────

    [Test]
    public void Headless_FromUnpaired_PairsWithoutAPrompt()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);
        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
        _client.Respond(Ok());

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired) }, _events);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void Headless_Failure_StaysUnpairedWithoutAnEventOrPrompt()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);
        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingRedeemError.InvalidPasscode, results[0].Error);
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsEmpty(_events);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void Headless_WhenPaired_CallsBackAtOnceWithInvalidState()
    {
        var service = CreatePaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(1, results.Count, "The callback always fires, refused calls included.");
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.IsEmpty(_client.Sent);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, service.LastRedeemResult.Error);
    }

    [Test]
    public void Headless_WhileResolvingOrManaged_IsInvalidState()
    {
        var resolving = Create();
        var results = new List<Abxr.PairingRedeemResult>();
        resolving.RedeemPairingPasscode("483921", results.Add);

        var managed = Create();
        managed.SettleIdentity(otherIdentityWins: true);
        managed.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[1].Error);
        Assert.IsEmpty(_client.Sent);
    }

    [Test]
    public void Headless_WhileRedeeming_IsInvalidState()
    {
        var service = CreateUnpaired();
        service.RedeemPairingPasscode("483921", _ => { });
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("111111", results.Add);

        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.AreEqual(1, _client.Sent.Count);
    }

    [Test]
    public void Headless_OnAnUnsupportedPlatform_IsInvalidState()
    {
        _host.IsPlatformSupported = false;
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.IsEmpty(_client.Sent);
    }

    [Test]
    public void Headless_WithoutAnAppToken_IsBuildRejectedWithoutSending()
    {
        _host.AppToken = "";
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);

        Assert.AreEqual(Abxr.PairingRedeemError.BuildRejected, results[0].Error);
        Assert.IsEmpty(_client.Sent);
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
    }

    [Test]
    public void Headless_WhilePrompting_AFailureReprompts()
    {
        var service = CreatePrompting();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);
        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingRedeemError.InvalidPasscode, results[0].Error);
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(1, _requests.Count);
    }

    [Test]
    public void Headless_APasscodeThatAllowsNaming_ReportsTheRequestWithoutPrompting()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", results.Add);
        StringAssert.DoesNotContain("skip_device_name", _client.Sent[0].json);
        _client.Respond(NameRequested());

        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameRequested, results[0].Error);
        Assert.AreEqual(PairingOutcomes.DeviceNameRequestedMessage, results[0].Message);
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsEmpty(_requests);
        Assert.IsEmpty(_events);
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void Headless_WithoutAName_SendsTheSkipFlag(string deviceName)
    {
        var service = CreateUnpaired();

        service.RedeemPairingPasscode("483921", deviceName, false, _ => { });

        StringAssert.Contains("\"skip_device_name\":true", _client.Sent[0].json);
        StringAssert.DoesNotContain("\"device_name\"", _client.Sent[0].json);
    }

    [Test]
    public void Headless_WithAName_SendsItAndStoresTheEcho()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", " Headset 12 ", false, results.Add);
        StringAssert.Contains("\"device_name\":\"Headset 12\"", _client.Sent[0].json);
        StringAssert.DoesNotContain("skip_device_name", _client.Sent[0].json);
        _client.Respond(Ok(deviceName: "Headset 12"));

        Assert.IsTrue(results[0].Success);
        Assert.AreEqual("Headset 12", results[0].DeviceName);
        Assert.AreEqual("Headset 12", service.DeviceName);
    }

    [Test]
    public void Headless_ANameInUse_ReportsTheDeviceWithoutPrompting()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", "headset 12", false, results.Add);
        _client.Respond(NameTaken());

        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameExists, results[0].Error);
        Assert.AreEqual("Headset 12", results[0].DeviceName);
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void Headless_JoinExisting_SendsTheFlag()
    {
        var service = CreateUnpaired();

        service.RedeemPairingPasscode("483921", "Headset 12", true, _ => { });

        StringAssert.Contains("\"device_name\":\"Headset 12\",\"join_existing\":true", _client.Sent[0].json);
    }

    [Test]
    public void Headless_JoinWithoutAName_IsInvalidState()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", " ", true, results.Add);

        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, results[0].Error);
        Assert.IsEmpty(_client.Sent);
    }

    [Test]
    public void Headless_AMalformedName_IsNotSent()
    {
        var service = CreateUnpaired();
        var results = new List<Abxr.PairingRedeemResult>();

        service.RedeemPairingPasscode("483921", new string('x', 65), false, results.Add);
        service.RedeemPairingPasscode("483921", "Head\nset", false, results.Add);

        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameInvalid, results[0].Error);
        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameInvalid, results[1].Error);
        Assert.IsEmpty(_client.Sent);
        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
    }

    // ── SetAppInstanceToken ───────────────────────────────────────

    [Test]
    public void SetAppInstanceToken_FromUnpaired_StoresAndPairs()
    {
        var service = CreateUnpaired();

        service.SetAppInstanceToken(Token, InstanceId);

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired) }, _events);
        Assert.AreEqual(Token, _store.Token);
        Assert.AreEqual(InstanceId, _store.InstanceId);
    }

    [Test]
    public void SetAppInstanceToken_WhilePrompting_ClosesThePromptAndPairs()
    {
        var service = CreatePrompting();

        service.SetAppInstanceToken(Token, InstanceId);

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.IsFalse(service.IsInputRequestPending);
        Assert.AreEqual(1, _ui.Hides);
    }

    [Test]
    public void SetAppInstanceToken_WhileResolving_WaitsForIdentityToSettle()
    {
        var service = Create();

        service.SetAppInstanceToken(Token, InstanceId);
        Assert.AreEqual(Abxr.PairingState.Resolving, service.State);
        Assert.IsEmpty(_events);

        service.SettleIdentity(otherIdentityWins: false);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Startup) }, _events);
    }

    [Test]
    public void SetAppInstanceToken_WhenManaged_StoresButStaysManaged()
    {
        var service = Create();
        service.SettleIdentity(otherIdentityWins: true);
        _events.Clear();

        service.SetAppInstanceToken(Token, InstanceId);

        Assert.AreEqual(Abxr.PairingState.Managed, service.State);
        Assert.IsEmpty(_events);
        Assert.IsTrue(service.TryGetStored(out _, out _));
    }

    [Test]
    public void SetAppInstanceToken_WhileRedeeming_IsIgnored()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        service.SetAppInstanceToken(OtherToken, OtherInstanceId);

        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
        Assert.AreEqual(0, _store.Saves);
    }

    [TestCase(null, InstanceId)]
    [TestCase("", InstanceId)]
    [TestCase(Token, null)]
    [TestCase(Token, " ")]
    public void SetAppInstanceToken_NeedsBothValues(string token, string instanceId)
    {
        var service = CreateUnpaired();

        service.SetAppInstanceToken(token, instanceId);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void SetAppInstanceToken_TrimsWhatItStores()
    {
        var service = CreateUnpaired();

        service.SetAppInstanceToken($" {Token}\n", $"{InstanceId} ");

        Assert.AreEqual(Token, _store.Token);
        Assert.AreEqual(InstanceId, _store.InstanceId);
    }

    // ── ClearPairing ──────────────────────────────────────────────

    [Test]
    public void ClearPairing_FromPaired_UnpairsWithoutPrompting()
    {
        var service = CreatePaired();

        service.ClearPairing();

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Cleared) }, _events);
        Assert.IsNull(_store.Token);
        Assert.IsNull(_store.InstanceId);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void ClearPairing_WhenManaged_OnlyForgetsTheStoredInstance()
    {
        _store.Save(Token, InstanceId, null);
        var service = Create();
        service.SettleIdentity(otherIdentityWins: true);
        _events.Clear();

        service.ClearPairing();

        Assert.AreEqual(Abxr.PairingState.Managed, service.State);
        Assert.IsEmpty(_events);
        Assert.IsFalse(service.TryGetStored(out _, out _));
    }

    [Test]
    public void ClearPairing_WhileRedeeming_IsIgnored()
    {
        var service = CreatePrompting();
        SubmitPrompt(service);

        service.ClearPairing();

        Assert.AreEqual(Abxr.PairingState.Redeeming, service.State);
        Assert.AreEqual(0, _store.Clears);
    }

    // ── Bootstrap refusals, through the auth seam ─────────────────

    [Test]
    public void Revoke_RemovesBothValuesAndFiresRevoked()
    {
        var service = CreatePaired();

        service.Revoke(InstanceId);

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Revoked) }, _events);
        Assert.IsNull(_store.Token);
        Assert.IsNull(_store.InstanceId);
        Assert.IsFalse(service.TryGetStored(out _, out _));
        Assert.IsEmpty(_requests, "No prompt from the SDK; whether to ask again is the app's rule.");
    }

    [Test]
    public void Revoke_ForAnInstanceAlreadyReplaced_IsIgnored()
    {
        var service = CreatePaired();

        service.Revoke(OtherInstanceId);

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual(0, _store.Clears);
        Assert.IsEmpty(_events);
    }

    [Test]
    public void Suspend_KeepsThePairingWithoutAnEvent()
    {
        var service = CreatePaired();

        service.SuspendForSession(InstanceId);

        Assert.IsTrue(service.IsSuspendedForSession);
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.IsEmpty(_events);
        Assert.AreEqual(0, _store.Clears);
        Assert.IsTrue(service.TryGetStored(out _, out _));
    }

    [Test]
    public void Suspend_DoesNotOutliveThePairing()
    {
        var service = CreatePaired();
        service.SuspendForSession(InstanceId);

        service.ClearPairing();
        service.SetAppInstanceToken(OtherToken, OtherInstanceId);

        Assert.IsFalse(service.IsSuspendedForSession, "A re-pair in the same session must still authenticate.");
        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
    }

    [Test]
    public void Suspend_ForAnInstanceAlreadyReplaced_IsIgnored()
    {
        var service = CreatePaired();

        service.SuspendForSession(OtherInstanceId);

        Assert.IsFalse(service.IsSuspendedForSession);
    }

    // ── The stored name ───────────────────────────────────────────

    [Test]
    public void TheStoredName_SurvivesARelaunch()
    {
        _store.Save(Token, InstanceId, "Headset 12");
        var service = Create();

        service.SettleIdentity(otherIdentityWins: false);

        Assert.AreEqual("Headset 12", service.DeviceName);
    }

    [Test]
    public void DeviceName_WithoutAPairing_IsNull()
    {
        _store.DeviceName = "Headset 12";

        var service = CreateUnpaired();

        Assert.IsNull(service.DeviceName);
    }

    [Test]
    public void UpdateDeviceName_StoresARenameFromThePortal()
    {
        var service = CreatePaired();

        service.UpdateDeviceName(InstanceId, " Headset 7 ");

        Assert.AreEqual("Headset 7", service.DeviceName);
        Assert.AreEqual("Headset 7", _store.DeviceName);
        Assert.AreEqual(Token, _store.Token, "Only the name changes.");
        Assert.AreEqual(0, _store.Saves);
    }

    [Test]
    public void UpdateDeviceName_Null_RemovesTheName()
    {
        _store.Save(Token, InstanceId, "Headset 12");
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);

        service.UpdateDeviceName(InstanceId, null);

        Assert.IsNull(service.DeviceName);
        Assert.IsNull(_store.DeviceName);
    }

    [Test]
    public void UpdateDeviceName_ForAnInstanceAlreadyReplaced_IsIgnored()
    {
        var service = CreatePaired();

        service.UpdateDeviceName(OtherInstanceId, "Headset 7");

        Assert.IsNull(service.DeviceName);
        Assert.AreEqual(0, _store.NameSaves);
    }

    [Test]
    public void ClearPairing_ForgetsTheName()
    {
        _store.Save(Token, InstanceId, "Headset 12");
        var service = Create();
        service.SettleIdentity(otherIdentityWins: false);

        service.ClearPairing();

        Assert.IsNull(service.DeviceName);
        Assert.IsNull(_store.DeviceName);
    }

    [Test]
    public void SetAppInstanceToken_StoresNoName()
    {
        _store.DeviceName = "Headset 12";
        var service = CreateUnpaired();

        service.SetAppInstanceToken(Token, InstanceId);

        Assert.IsNull(service.DeviceName);
        Assert.IsNull(_store.DeviceName);
    }

    // ── Robustness ────────────────────────────────────────────────

    [Test]
    public void AThrowingStateHandler_StillCompletesTheRedeem()
    {
        var service = CreateUnpaired();
        service.OnStateChanged = (_, _) => throw new InvalidOperationException("app bug");
        var results = new List<Abxr.PairingRedeemResult>();
        LogAssert.Expect(LogType.Error, new Regex("OnPairingStateChanged handler threw"));

        service.RedeemPairingPasscode("483921", results.Add);
        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Success);
    }

    [Test]
    public void AHandlerResubmittingFromItsCallback_DoesNotRecurseForever()
    {
        var service = CreateUnpaired();
        int calls = 0;
        service.OnInputRequested = (_, _, _, _) =>
        {
            calls++;
            service.SubmitInput("bad");
        };

        Assert.IsTrue(service.StartPairing());

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.LessOrEqual(calls, 8);
        Assert.IsEmpty(_client.Sent);
    }

    [Test]
    public void AHandlerAnsweringEachStepFromItsCallback_Pairs()
    {
        var service = CreateUnpaired();
        service.OnInputRequested = (type, _, _, _) =>
            service.SubmitInput(type == AbxrPairingService.PasscodeInputType ? "483921" : "Headset 12");

        Assert.IsTrue(service.StartPairing());
        _client.Respond(NameRequested());
        _client.Respond(Ok(deviceName: "Headset 12"));

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        StringAssert.Contains("\"device_name\":\"Headset 12\"", _client.Sent[1].json);
    }

    // ── Fakes ─────────────────────────────────────────────────────

    private sealed class FakeHost : IPairingHost
    {
        public bool IsPlatformSupported { get; set; } = true;
        public bool CanPresentPrompt { get; set; } = true;
        public string AppToken { get; set; } = "app.token.jwt";
        public string PairingUrl { get; set; } = "https://api.xrdm.dev/";
        public double Now { get; set; } = 1000;
        public double RequestTimeoutSeconds { get; set; } = 30;
        public PairingDeviceMetadata DeviceMetadata { get; } = new PairingDeviceMetadata
        {
            Model = "PICO 4", Manufacturer = "Pico", OsVersion = "Android OS 12", AppVersion = "1.0.0", SdkVersion = "3.0.0"
        };
    }

    private sealed class MemoryStore : IPairingStore
    {
        public string Token;
        public string InstanceId;
        public string DeviceName;
        public int Saves;
        public int NameSaves;
        public int Clears;

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
            Saves++;
        }

        public void SaveDeviceName(string deviceName)
        {
            DeviceName = deviceName;
            NameSaves++;
        }

        public void Clear()
        {
            Token = null;
            InstanceId = null;
            DeviceName = null;
            Clears++;
        }
    }

    private sealed class FakeClient : IPairingRedeemClient
    {
        public readonly List<(string url, string json, Action<PairingHttpResponse> onComplete)> Sent = new List<(string, string, Action<PairingHttpResponse>)>();

        /// <summary>Thrown from Send, after the request is recorded and, with AnswerBeforeThrowing, answered.</summary>
        public Exception Throw;
        public PairingHttpResponse? AnswerBeforeThrowing;

        public void Send(string url, string json, Action<PairingHttpResponse> onComplete)
        {
            Sent.Add((url, json, onComplete));
            if (AnswerBeforeThrowing.HasValue) onComplete(AnswerBeforeThrowing.Value);
            if (Throw != null) throw Throw;
        }

        /// <summary>Answers the latest request.</summary>
        public void Respond(PairingHttpResponse response) => Sent[Sent.Count - 1].onComplete(response);
    }

    private sealed class FakeAuthUi : IAbxrAuthUi
    {
        public int Hides;
        public void Show(AuthUiKind kind) { }
        public void SetPrompt(string prompt) { }
        public void Hide() => Hides++;
        public void StopProcessing() { }
    }
}

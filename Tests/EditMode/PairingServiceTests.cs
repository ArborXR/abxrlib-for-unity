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
        _store.Save(Token, InstanceId);
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

    private static PairingHttpResponse Ok() => new PairingHttpResponse(200, SuccessBody);
    private static PairingHttpResponse Status(long status, string retryAfter = null) => new PairingHttpResponse(status, "{\"error\":\"nope\"}", retryAfter);

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
        _store.Save(Token, InstanceId);
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
        _store.Save(Token, InstanceId);
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
    public void UnsupportedPlatform_IgnoresAStoredPairing()
    {
        _store.Save(Token, InstanceId);
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
        service.SubmitInput("483921");

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
    }

    [Test]
    public void Submit_Success_StoresBothValuesAndPairs()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

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
        service.SubmitInput("483921");

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
        service.SubmitInput("483921");

        _client.Respond(Status(503));

        Assert.AreEqual(1, _client.Sent.Count);
        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(PairingOutcomes.UnavailableMessage, _requests[0].error);
    }

    [Test]
    public void Submit_Offline_IsUnavailable()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

        _client.Respond(new PairingHttpResponse(0, null, networkError: true, errorDetail: "Request timeout"));

        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, service.LastRedeemResult.Error);
        Assert.AreEqual(1, _client.Sent.Count);
    }

    [Test]
    public void Submit_BuildRejected_Reprompts()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

        _client.Respond(Status(401));

        Assert.AreEqual(Abxr.PairingState.Prompting, service.State);
        Assert.AreEqual(PairingOutcomes.BuildRejectedMessage, _requests[0].error);
    }

    [Test]
    public void RateLimited_HoldsAttemptsUntilRetryAfterPasses()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");
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
        service.SubmitInput("483921");

        service.CancelPairing();
        _client.Respond(Ok());

        Assert.AreEqual(Abxr.PairingState.Paired, service.State, "The instance exists; dropping it would orphan it in the Portal.");
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Paired, Abxr.PairingChangeReason.Paired) }, _events);
    }

    [Test]
    public void Cancel_WhileRedeeming_AFailureDismissesWithoutReprompting()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");

        service.CancelPairing();
        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingState.Unpaired, service.State);
        CollectionAssert.AreEqual(new[] { (Abxr.PairingState.Unpaired, Abxr.PairingChangeReason.Dismissed) }, _events);
        Assert.IsEmpty(_requests);
    }

    [Test]
    public void AResponseArrivingTwice_IsHandledOnce()
    {
        var service = CreatePrompting();
        service.SubmitInput("483921");
        _client.Respond(Ok());

        _client.Respond(Status(400));

        Assert.AreEqual(Abxr.PairingState.Paired, service.State);
        Assert.AreEqual(1, _events.Count);
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
        service.SubmitInput("483921");

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
        _store.Save(Token, InstanceId);
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
        service.SubmitInput("483921");

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

    // ── Fakes ─────────────────────────────────────────────────────

    private sealed class FakeHost : IPairingHost
    {
        public bool IsPlatformSupported { get; set; } = true;
        public bool CanPresentPrompt { get; set; } = true;
        public string AppToken { get; set; } = "app.token.jwt";
        public string PairingUrl { get; set; } = "https://api.xrdm.dev/";
        public double Now { get; set; } = 1000;
        public PairingDeviceMetadata DeviceMetadata { get; } = new PairingDeviceMetadata
        {
            Model = "PICO 4", Manufacturer = "Pico", OsVersion = "Android OS 12", AppVersion = "1.0.0", SdkVersion = "3.0.0"
        };
    }

    private sealed class MemoryStore : IPairingStore
    {
        public string Token;
        public string InstanceId;
        public int Saves;
        public int Clears;

        public bool TryLoad(out string token, out string instanceId)
        {
            token = Token;
            instanceId = InstanceId;
            return !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(InstanceId);
        }

        public void Save(string token, string instanceId)
        {
            Token = token;
            InstanceId = instanceId;
            Saves++;
        }

        public void Clear()
        {
            Token = null;
            InstanceId = null;
            Clears++;
        }
    }

    private sealed class FakeClient : IPairingRedeemClient
    {
        public readonly List<(string url, string json, Action<PairingHttpResponse> onComplete)> Sent = new List<(string, string, Action<PairingHttpResponse>)>();

        public void Send(string url, string json, Action<PairingHttpResponse> onComplete) => Sent.Add((url, json, onComplete));

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

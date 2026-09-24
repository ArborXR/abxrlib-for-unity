// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the pairing redeem contract (SDK-60): passcode and device name shape, response classification,
// Retry-After, and the request.
using System;
using System.Linq;
using AbxrLib.Runtime.Services.Pairing;
using NUnit.Framework;

[TestFixture]
public class PairingOutcomesTests
{
    private static readonly DateTime Now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private const string Token = "0123456789abcdef0123456789abcdef01234567";
    private const string InstanceId = "9b2c6a58-2f61-4c1e-9d0a-3f6f1d1e2a11";
    private static readonly string SuccessBody = $"{{\"app_instance_token\":\"{Token}\",\"app_instance_id\":\"{InstanceId}\"}}";

    private static Abxr.PairingRedeemResult Classify(PairingHttpResponse response) =>
        PairingOutcomes.Classify(response, Now, out _, out _);

    // ── Passcode shape ────────────────────────────────────────────

    [TestCase("483921", ExpectedResult = true)]
    [TestCase("000042", ExpectedResult = true)]
    [TestCase("48392", ExpectedResult = false)]
    [TestCase("4839210", ExpectedResult = false)]
    [TestCase("48392a", ExpectedResult = false)]
    [TestCase("４８３９２１", ExpectedResult = false)] // full-width digits, which char.IsDigit accepts
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool IsWellFormedPasscode_AcceptsOnlySixAsciiDigits(string passcode) =>
        PairingOutcomes.IsWellFormedPasscode(passcode);

    [TestCase(" 483921 ", "483921")]
    [TestCase("483 921", "483921")]
    [TestCase("483-921", "483921")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void NormalizePasscode_DropsWhitespaceAndHyphens(string input, string expected) =>
        Assert.AreEqual(expected, PairingOutcomes.NormalizePasscode(input));

    // ── Device name shape (INS-511) ───────────────────────────────

    [TestCase(" Headset 12 ", "Headset 12")]
    [TestCase("Headset 12", "Headset 12")]
    [TestCase("   ", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void NormalizeDeviceName_TrimsAndTreatsBlankAsSkipped(string input, string expected) =>
        Assert.AreEqual(expected, PairingOutcomes.NormalizeDeviceName(input));

    [TestCase("H", ExpectedResult = true)]
    [TestCase("Headset 12", ExpectedResult = true)]
    [TestCase("Head\nset", ExpectedResult = false)]
    [TestCase("Head\tset", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool IsWellFormedDeviceName_RejectsControlCharacters(string name) =>
        PairingOutcomes.IsWellFormedDeviceName(name);

    [Test]
    public void IsWellFormedDeviceName_CountsCodePointsUpTo64()
    {
        const string headphones = "\U0001F3A7"; // two UTF-16 units, one character to the Portal
        Assert.IsTrue(PairingOutcomes.IsWellFormedDeviceName(new string('x', 64)));
        Assert.IsFalse(PairingOutcomes.IsWellFormedDeviceName(new string('x', 65)));
        Assert.IsTrue(PairingOutcomes.IsWellFormedDeviceName(string.Concat(Enumerable.Repeat(headphones, 64))));
        Assert.IsFalse(PairingOutcomes.IsWellFormedDeviceName(string.Concat(Enumerable.Repeat(headphones, 65))));
    }

    // ── Classification ────────────────────────────────────────────

    [Test]
    public void Classify_200WithBothFields_IsSuccessWithTheCredential()
    {
        var result = PairingOutcomes.Classify(new PairingHttpResponse(200, SuccessBody), Now, out string token, out string instanceId);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(Abxr.PairingRedeemError.None, result.Error);
        Assert.AreEqual(Token, token);
        Assert.AreEqual(InstanceId, instanceId);
    }

    [TestCase("{\"app_instance_token\":\"" + Token + "\"}")]
    [TestCase("{\"app_instance_id\":\"" + InstanceId + "\"}")]
    [TestCase("{\"app_instance_token\":\"  \",\"app_instance_id\":\"" + InstanceId + "\"}")]
    [TestCase("<html><body>Welcome</body></html>")]
    [TestCase("[]")]
    [TestCase("")]
    [TestCase(null)]
    public void Classify_200WithoutAUsableCredential_IsUnavailableAndStoresNothing(string body)
    {
        var result = PairingOutcomes.Classify(new PairingHttpResponse(200, body), Now, out string token, out string instanceId);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, result.Error);
        Assert.IsNull(token);
        Assert.IsNull(instanceId);
    }

    [TestCase(400, Abxr.PairingRedeemError.InvalidPasscode)]
    [TestCase(401, Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(422, Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(403, Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(404, Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(408, Abxr.PairingRedeemError.Unavailable)]
    [TestCase(500, Abxr.PairingRedeemError.Unavailable)]
    [TestCase(502, Abxr.PairingRedeemError.Unavailable)]
    [TestCase(503, Abxr.PairingRedeemError.Unavailable)]
    public void Classify_MapsStatusToOneOfTheFourOutcomes(long status, Abxr.PairingRedeemError expected)
    {
        var result = Classify(new PairingHttpResponse(status, "{\"error\":\"whatever\"}"));

        Assert.IsFalse(result.Success);
        Assert.AreEqual(expected, result.Error);
    }

    [Test]
    public void Classify_200_CarriesTheStoredName()
    {
        string body = $"{{\"app_instance_token\":\"{Token}\",\"app_instance_id\":\"{InstanceId}\",\"device_name\":\" Headset 12 \"}}";

        Assert.AreEqual("Headset 12", Classify(new PairingHttpResponse(200, body)).DeviceName);
        Assert.IsNull(Classify(new PairingHttpResponse(200, SuccessBody)).DeviceName, "No name means the name was skipped.");
    }

    [TestCase(422, "device_name_required", Abxr.PairingRedeemError.DeviceNameRequired)]
    [TestCase(422, "device_name_invalid", Abxr.PairingRedeemError.DeviceNameInvalid)]
    [TestCase(409, "device_name_exists", Abxr.PairingRedeemError.DeviceNameExists)]
    [TestCase(409, "something_else", Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(422, "something_else", Abxr.PairingRedeemError.BuildRejected)]
    [TestCase(400, "device_name_invalid", Abxr.PairingRedeemError.InvalidPasscode)]
    [TestCase(401, "device_name_required", Abxr.PairingRedeemError.BuildRejected)]
    public void Classify_ReadsTheNameCodesOnlyOn409And422(long status, string code, Abxr.PairingRedeemError expected) =>
        Assert.AreEqual(expected, Classify(new PairingHttpResponse(status, $"{{\"error\":\"nope\",\"code\":\"{code}\"}}")).Error);

    [Test]
    public void Classify_409_NamesTheExistingDeviceForTheConfirm()
    {
        var result = PairingOutcomes.Classify(
            new PairingHttpResponse(409, "{\"code\":\"device_name_exists\",\"device_name\":\"Headset 12\"}"), Now, out _, out _, "headset 12");

        Assert.AreEqual(Abxr.PairingRedeemError.DeviceNameExists, result.Error);
        Assert.AreEqual("Headset 12", result.DeviceName);
        Assert.AreEqual("Headset 12 is already in your organization. Add this app to it?", result.Message);
    }

    [Test]
    public void Classify_409_WithoutAName_FallsBackToTheOneSent()
    {
        var result = PairingOutcomes.Classify(new PairingHttpResponse(409, "{\"code\":\"device_name_exists\"}"), Now, out _, out _, "Headset 12");

        Assert.AreEqual("Headset 12", result.DeviceName);
    }

    [Test]
    public void Classify_NoResponse_IsUnavailable()
    {
        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, Classify(new PairingHttpResponse(0, null, networkError: true, errorDetail: "Cannot resolve destination host")).Error);
        Assert.AreEqual(Abxr.PairingRedeemError.Unavailable, Classify(new PairingHttpResponse(0, null)).Error);
    }

    [Test]
    public void Classify_429_CarriesRetryAfter()
    {
        var result = Classify(new PairingHttpResponse(429, "{\"error\":\"Too many failed pairing attempts\"}", retryAfter: "42"));

        Assert.AreEqual(Abxr.PairingRedeemError.RateLimited, result.Error);
        Assert.AreEqual(42, result.RetryAfterSeconds);
        Assert.AreEqual("Too many attempts. Try again in 42 seconds.", result.Message);
    }

    [Test]
    public void Failures_CarryTheDefaultCopy()
    {
        Assert.AreEqual("That passcode wasn't recognized. Check it and try again.", Classify(new PairingHttpResponse(400, null)).Message);
        Assert.AreEqual("This app can't pair right now. Contact the app's developer.", Classify(new PairingHttpResponse(401, null)).Message);
        Assert.AreEqual("Can't reach the pairing service. Check the connection and try again.", Classify(new PairingHttpResponse(503, null)).Message);
        Assert.AreEqual("Your organization requires a name for this headset.", PairingOutcomes.Failure(Abxr.PairingRedeemError.DeviceNameRequired).Message);
        Assert.AreEqual("That name can't be used. Use 1 to 64 characters.", PairingOutcomes.Failure(Abxr.PairingRedeemError.DeviceNameInvalid).Message);
    }

    [Test]
    public void RateLimited_SaysSecondForOneSecond()
    {
        Assert.AreEqual("Too many attempts. Try again in 1 second.", PairingOutcomes.RateLimited(1).Message);
    }

    [Test]
    public void Refused_IsInvalidStateWithTheReason()
    {
        var result = PairingOutcomes.Refused("this app is already paired.");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Abxr.PairingRedeemError.InvalidState, result.Error);
        Assert.AreEqual("this app is already paired.", result.Message);
    }

    [Test]
    public void DefaultResult_MeansNoAttempt()
    {
        var result = default(Abxr.PairingRedeemResult);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Abxr.PairingRedeemError.None, result.Error);
    }

    // ── Retry-After ───────────────────────────────────────────────

    [TestCase("30", 30)]
    [TestCase(" 45 ", 45)]
    [TestCase("0", 1)]
    [TestCase("86400", 3600)]
    [TestCase("-5", 60)]
    [TestCase("soon", 60)]
    [TestCase("", 60)]
    [TestCase(null, 60)]
    public void ParseRetryAfter_ReadsDeltaSeconds(string header, int expected) =>
        Assert.AreEqual(expected, PairingOutcomes.ParseRetryAfter(header, Now));

    [Test]
    public void ParseRetryAfter_ReadsAnHttpDate()
    {
        Assert.AreEqual(90, PairingOutcomes.ParseRetryAfter(Now.AddSeconds(90).ToString("r"), Now));
        Assert.AreEqual(1, PairingOutcomes.ParseRetryAfter(Now.AddSeconds(-30).ToString("r"), Now));
    }

    // ── Request ───────────────────────────────────────────────────

    [TestCase("https://api.xrdm.app/", "https://api.xrdm.app/api/insights-pairing/redeem")]
    [TestCase("https://api.xrdm.app", "https://api.xrdm.app/api/insights-pairing/redeem")]
    [TestCase("https://proxy.example.com/portal/", "https://proxy.example.com/portal/api/insights-pairing/redeem")]
    public void RedeemUrl_AppendsTheRoute(string pairingUrl, string expected) =>
        Assert.AreEqual(expected, PairingOutcomes.RedeemUrl(pairingUrl));

    [Test]
    public void RedeemBody_MatchesTheWireContract()
    {
        // Pinned exactly: the Portal reads these names, and the metadata keys ship at ISV cadence.
        var metadata = new PairingDeviceMetadata { Model = "Meta Quest 3", Manufacturer = "Oculus", OsVersion = "Android OS 14", AppVersion = "1.2.3", SdkVersion = "3.0.0" };

        Assert.AreEqual(
            "{\"app_token\":\"app.token.jwt\",\"passcode\":\"483921\"," +
            "\"device_metadata\":{\"model\":\"Meta Quest 3\",\"manufacturer\":\"Oculus\",\"osVersion\":\"Android OS 14\",\"appVersion\":\"1.2.3\",\"sdkVersion\":\"3.0.0\"}}",
            PairingOutcomes.RedeemBody("app.token.jwt", "483921", metadata));
    }

    [Test]
    public void RedeemBody_LeavesOutMissingMetadata()
    {
        Assert.AreEqual("{\"app_token\":\"app.token.jwt\",\"passcode\":\"483921\"}", PairingOutcomes.RedeemBody("app.token.jwt", "483921", null));
    }

    [Test]
    public void RedeemBody_WithANameAndAJoin_MatchesTheWireContract()
    {
        Assert.AreEqual(
            "{\"app_token\":\"app.token.jwt\",\"passcode\":\"483921\",\"device_name\":\"Headset 12\",\"join_existing\":true}",
            PairingOutcomes.RedeemBody("app.token.jwt", "483921", null, "Headset 12", joinExisting: true));
        Assert.AreEqual(
            "{\"app_token\":\"app.token.jwt\",\"passcode\":\"483921\",\"device_name\":\"Headset 12\"}",
            PairingOutcomes.RedeemBody("app.token.jwt", "483921", null, "Headset 12"));
    }

    [Test]
    public void DeviceMetadata_Current_FillsEveryKey()
    {
        var metadata = PairingDeviceMetadata.Current();

        Assert.IsNotNull(metadata.Model);
        Assert.IsNotNull(metadata.Manufacturer);
        Assert.IsNotNull(metadata.OsVersion);
        Assert.IsNotNull(metadata.AppVersion);
        Assert.AreEqual(AbxrLib.Runtime.Core.AbxrLibVersion.Version, metadata.SdkVersion);
    }

    [Test]
    public void ServerError_ReadsTheErrorFieldForLogs()
    {
        Assert.AreEqual("Invalid app token", PairingOutcomes.ServerError("{\"error\":\"Invalid app token\"}"));
        Assert.IsNull(PairingOutcomes.ServerError("<html></html>"));
        Assert.IsNull(PairingOutcomes.ServerError(null));
    }
}

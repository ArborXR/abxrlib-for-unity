// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode: an app that adopts a handed-over session takes the token's real expiry (SDK-82): the token's exp claim, then
// the launcher's TokenExpirationMs, then 24 hours. A handoff whose token already expired isn't applied, and device
// authentication runs instead.
// Drives the real AbxrAuthService through AuthRetryTests' scripted transport, so no network is involved.
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthHandoffExpiryTests : AbxrPlayModeTestBase
{
    private const string AppId = "12345678-1234-1234-1234-123456789012";
    /// <summary>Device auth's answer: a token that expires in 2100 and a secret the handoffs don't use.</summary>
    private static readonly AuthTransportResult Authorized = new AuthTransportResult(true,
        "{\"token\":\"eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.sig\",\"secret\":\"device-secret\",\"appId\":\"" + AppId + "\"}",
        false, 200);

    [UnityTest]
    public IEnumerator Handoff_TakesTheExpiryFromTheToken()
    {
        // A launcher writes now + 24 hours as TokenExpirationMs when it doesn't know its expiry; the token knows.
        var transport = UseScriptedTransport();
        long exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        AbxrAuthService.SetAuthHandoffForTesting(Handoff(TokenExpiringAt(exp), DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds()));

        yield return AuthenticateAndWait();

        Assert.AreEqual(0, transport.AuthCalls, "The handoff must be applied without device authentication.");
        Assert.IsTrue(Auth.SessionUsedAuthHandoff());
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime, TokenExpiry());
    }

    [UnityTest]
    public IEnumerator Handoff_WithoutAToken_TakesTokenExpirationMs()
    {
        // A launcher on the ArborInsightsClient transport hands over no token.
        var transport = UseScriptedTransport();
        long expiryMs = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
        AbxrAuthService.SetAuthHandoffForTesting(Handoff(token: null, expiryMs));

        yield return AuthenticateAndWait();

        Assert.AreEqual(0, transport.AuthCalls);
        Assert.IsTrue(Auth.SessionUsedAuthHandoff());
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(expiryMs).UtcDateTime, TokenExpiry());
    }

    [UnityTest]
    public IEnumerator Handoff_WithoutAnyExpiry_Assumes24Hours()
    {
        UseScriptedTransport();
        AbxrAuthService.SetAuthHandoffForTesting(Handoff(token: null, tokenExpirationMs: null));
        DateTime before = DateTime.UtcNow;

        yield return AuthenticateAndWait();

        Assert.IsTrue(Auth.SessionUsedAuthHandoff());
        Assert.That(TokenExpiry(), Is.InRange(before.AddHours(24), DateTime.UtcNow.AddHours(24)));
    }

    [UnityTest]
    public IEnumerator Handoff_WithAnExpiredToken_RunsDeviceAuthentication()
    {
        // What a launcher whose token already expired sends: the dead token, and now + 24 hours as TokenExpirationMs.
        var transport = UseScriptedTransport();
        long exp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        AbxrAuthService.SetAuthHandoffForTesting(Handoff(TokenExpiringAt(exp), DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds()));

        yield return AuthenticateAndWait();

        Assert.AreEqual(1, transport.AuthCalls, "An expired handoff must fall back to device authentication.");
        Assert.IsFalse(Auth.SessionUsedAuthHandoff());
        Assert.IsTrue(Auth.Authenticated);
        Assert.AreEqual("device-secret", Auth.ResponseData.Secret, "The session must be device auth's, not the expired handoff's.");
    }

    private static AbxrAuthService Auth => AbxrSubsystem.Instance.AuthServiceForTesting;

    private static DateTime TokenExpiry() => (DateTime)typeof(AbxrAuthService)
        .GetField("_tokenExpiry", BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(Auth);

    /// <summary>
    /// Routes auth and data through a scripted transport that answers device auth with <see cref="Authorized"/>, with
    /// valid-looking legacy credentials. Data goes there so the teardown flush of a session without a token skips REST.
    /// </summary>
    private AuthRetryTests.ScriptedAuthTransport UseScriptedTransport()
    {
        var transport = new AuthRetryTests.ScriptedAuthTransport(Authorized);
        AbxrSubsystem.Instance.SetTransportForTesting(transport);
        SetRuntimeAuth(new RuntimeAuthConfig
        {
            authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" },
            useAppTokens = false,
            buildType = "development",
            appId = AppId,
            orgId = "87654321-4321-4321-4321-210987654321",
            authSecret = "test-secret"
        });
        AbxrSubsystem.UnitTestSsoSimulationFromConfigAllowed = false;
        return transport;
    }

    private static IEnumerator AuthenticateAndWait()
    {
        bool? result = null;
        Abxr.OnAuthCompleted += (success, error) => result = success;
        Abxr.StartAuthentication();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (result == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual(true, result, "Authentication must complete.");
    }

    /// <summary>The handoff fields GetHandoffJson writes that matter here.</summary>
    private static string Handoff(string token, long? tokenExpirationMs)
    {
        string json = "{\"Token\":\"" + (token ?? "") + "\",\"Secret\":\"handoff-secret\",\"AppId\":\"" + AppId + "\"," +
                      "\"UserId\":\"learner-1\",\"UserData\":{\"id\":\"learner-1\"}";
        if (tokenExpirationMs.HasValue)
            json += ",\"TokenExpirationMs\":" + tokenExpirationMs.Value;
        return json + "}";
    }

    private static string TokenExpiringAt(long exp)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":" + exp + "}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "eyJhbGciOiJub25lIn0." + payload + ".sig";
    }
}

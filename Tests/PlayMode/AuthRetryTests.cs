// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode: the device-auth loop retries transient failures with backoff and latches refusals for the session.
// Drives the real AbxrAuthService with a scripted transport, so no network is involved.
// AuthFailureClassificationTests (EditMode) pins the classification table itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthRetryTests : AbxrPlayModeTestBase
{
    private static readonly AuthTransportResult Offline =
        new AuthTransportResult(false, "No response body.", false, 0, networkError: true);
    private static readonly AuthTransportResult Unauthorized =
        new AuthTransportResult(false, "{\"detail\":\"Invalid app token\"}", true, 401);

    [UnityTest]
    public IEnumerator DeviceAuth_Offline_RetriesWithBackoff()
    {
        var transport = UseScriptedTransport(Offline);
        bool completed = false;
        Abxr.OnAuthCompleted += (success, error) => completed = true;
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Retrying in 1 seconds")));
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Retrying in 2 seconds")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 3, 15f);

        Assert.GreaterOrEqual(transport.AuthCalls, 3, "An offline launch must keep retrying, not latch as rejected.");
        Assert.IsFalse(completed, "OnAuthCompleted must not fire while retrying.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_Unauthorized_LatchesForTheSession()
    {
        var transport = UseScriptedTransport(Unauthorized);
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Invalid app token")));
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Authentication was rejected by the API")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count == 1, 5f);
        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count == 2, 5f);

        CollectionAssert.AreEqual(new[] { false, false }, results);
        Assert.AreEqual(1, transport.AuthCalls, "A refusal must not reach the API again this session, even when the app asks.");
    }

    /// <summary>Routes device auth through a transport that always answers with the given result, with valid-looking legacy credentials and a 1s base retry interval.</summary>
    private ScriptedAuthTransport UseScriptedTransport(AuthTransportResult response)
    {
        var transport = new ScriptedAuthTransport(response);
        AbxrSubsystem.Instance.AuthServiceForTesting.SetTransportGetter(() => transport);
        SetRuntimeAuth(new RuntimeAuthConfig
        {
            authMechanism = new AuthMechanism { type = "none", prompt = "", domain = "" },
            useAppTokens = false,
            buildType = "development",
            appId = "12345678-1234-1234-1234-123456789012",
            // Must be a UUID: validation rejects any other orgId before the request reaches the transport.
            orgId = "87654321-4321-4321-4321-210987654321",
            authSecret = "test-secret"
        });
        ModifyConfig("sendRetryIntervalSeconds", 1);
        return transport;
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private sealed class ScriptedAuthTransport : IAbxrTransport
    {
        private readonly AuthTransportResult _response;
        public int AuthCalls { get; private set; }

        public ScriptedAuthTransport(AuthTransportResult response) => _response = response;

        public bool IsServiceTransport => false;

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete)
        {
            AuthCalls++;
            onComplete?.Invoke(_response);
            yield break;
        }

        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete)
        {
            onComplete?.Invoke(false, "not scripted");
            yield break;
        }

        public void AddEvent(string name, Dictionary<string, string> meta) { }
        public void AddTelemetry(string name, Dictionary<string, string> meta) { }
        public void AddLog(string logLevel, string text, Dictionary<string, string> meta) { }
        public void ForceSend() { }
        public void StorageAdd(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy) { }

        public IEnumerator StorageGetCoroutine(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        public IEnumerator StorageDeleteCoroutine(Abxr.StorageScope scope, string name, Action<bool> onComplete)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        public void OnQuit() { }
        public void ClearAllPending() { }
        public List<EventPayload> GetPendingEventsForTesting() => new List<EventPayload>();
        public List<LogPayload> GetPendingLogsForTesting() => new List<LogPayload>();
        public List<TelemetryPayload> GetPendingTelemetryForTesting() => new List<TelemetryPayload>();
    }
}

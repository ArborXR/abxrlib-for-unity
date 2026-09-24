// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode: the device-auth loop retries transient failures with backoff, latches refusals for the session, and stops
// when EndSession or StartNewSession clears the session it belonged to.
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
    public IEnumerator DeviceAuth_Offline_SaysOncePerAttemptThatItIsRetrying()
    {
        // Release builds drop warnings, so the retry warning alone would leave a stuck headset's log silent.
        var transport = UseScriptedTransport(Offline);
        var infoLines = new List<string>();
        Application.LogCallback onLog = (message, stackTrace, type) =>
        {
            if (type == LogType.Log && message.Contains("Device authentication failed")) infoLines.Add(message);
        };
        Application.logMessageReceived += onLog;
        try
        {
            Abxr.StartAuthentication();
            yield return WaitFor(() => transport.AuthCalls >= 3, 15f);

            Assert.GreaterOrEqual(transport.AuthCalls, 3);
            Assert.AreEqual(1, infoLines.Count, "The retry is announced once per attempt, not on every retry.");
            StringAssert.Contains("(no connection)", infoLines[0]);
        }
        finally
        {
            Application.logMessageReceived -= onLog;
        }
    }

    [UnityTest]
    public IEnumerator DeviceAuth_RetriesWhileTimeScaleIsZero()
    {
        var transport = UseScriptedTransport(Offline);
        float savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        try
        {
            Abxr.StartAuthentication();
            yield return WaitFor(() => transport.AuthCalls >= 2, 5f);

            Assert.GreaterOrEqual(transport.AuthCalls, 2, "An app paused with timeScale 0 while it waits for auth must still get retries.");
        }
        finally
        {
            Time.timeScale = savedTimeScale;
        }
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

    [UnityTest]
    public IEnumerator DeviceAuth_EndSessionWhileRetrying_StopsRetrying()
    {
        var transport = UseScriptedTransport(Offline);
        bool completed = false;
        Abxr.OnAuthCompleted += (success, error) => completed = true;

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 1, 5f);
        Abxr.EndSession();
        // The first retry was due one second after the first failure.
        yield return new WaitForSeconds(2.5f);

        Assert.AreEqual(1, transport.AuthCalls, "EndSession must stop the retries of the session it ended.");
        Assert.IsFalse(completed, "An ended session must not report an auth outcome.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 2, 5f);
        Assert.AreEqual(2, transport.AuthCalls, "StartAuthentication after EndSession must send a request, not be ignored as already in progress.");
    }

    [UnityTest]
    public IEnumerator DeviceAuth_StartNewSessionDuringRequest_DropsTheOldAttempt()
    {
        var transport = UseScriptedTransport(Offline);
        transport.Hold = true;
        bool completed = false;
        Abxr.OnAuthCompleted += (success, error) => completed = true;
        var retryWarnings = new List<string>();
        Application.LogCallback onLog = (message, stackTrace, type) =>
        {
            if (type == LogType.Warning && message.Contains("Retrying in")) retryWarnings.Add(message);
        };
        Application.logMessageReceived += onLog;
        try
        {
            Abxr.StartAuthentication();
            yield return WaitFor(() => transport.AuthCalls >= 1, 5f);
            Abxr.StartNewSession();
            yield return WaitFor(() => transport.AuthCalls >= 2, 1f);
            Assert.AreEqual(2, transport.AuthCalls, "StartNewSession must start its own device auth, not wait on the old attempt.");

            // Both requests now fail as offline. The new session's attempt waits at least a second before its next retry.
            transport.Hold = false;
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(1, retryWarnings.Count, "Only the new session's attempt may retry; the old attempt's late response must be dropped.");
            Assert.IsFalse(completed, "Neither attempt has an outcome to report yet.");
            Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);
        }
        finally
        {
            Application.logMessageReceived -= onLog;
        }
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
        /// <summary>While true, requests stay in flight: the transport was called but has not answered yet.</summary>
        public bool Hold { get; set; }

        public ScriptedAuthTransport(AuthTransportResult response) => _response = response;

        public bool IsServiceTransport => false;

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete)
        {
            AuthCalls++;
            while (Hold)
                yield return null;
            onComplete?.Invoke(_response);
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

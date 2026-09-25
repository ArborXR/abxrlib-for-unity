// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode: the device-auth loop retries transient failures with backoff, latches refusals for the session, and stops
// when EndSession or StartNewSession clears the session it belonged to. It reports its first transient failure as
// OnAuthCompleted(false), then only a later success.
// Drives the real AbxrAuthService with a scripted transport, so no network is involved.
// AuthFailureClassificationTests (EditMode) pins the classification table itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

[TestFixture]
public class AuthRetryTests : AbxrPlayModeTestBase
{
    private static readonly AuthTransportResult Offline =
        new AuthTransportResult(false, "No response body.", false, 0, networkError: true);
    private static readonly AuthTransportResult Unauthorized =
        new AuthTransportResult(false, "{\"detail\":\"Invalid app token\"}", true, 401);
    /// <summary>A valid success without a token: the backend wants user authentication next (AuthResponse.IsValidSuccess).</summary>
    private static readonly AuthTransportResult UserAuthRequired =
        new AuthTransportResult(true, "{\"appId\":\"12345678-1234-1234-1234-123456789012\"}", false, 200);
    /// <summary>A full success: a token (a JWT that expires in 2100) and a secret, so the REST data path can sign its requests.</summary>
    private static readonly AuthTransportResult Authorized = new AuthTransportResult(true,
        "{\"token\":\"eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.sig\",\"secret\":\"test-secret\",\"appId\":\"12345678-1234-1234-1234-123456789012\"}",
        false, 200);
    private static readonly AuthTransportResult ServiceUnavailable =
        new AuthTransportResult(false, "<html>Service Unavailable</html>", false, 503);

    /// <summary>What OnAuthCompleted(false) carries when device auth fails offline and keeps retrying.</summary>
    private const string RetryingReport = "Device authentication failed (no connection); the SDK is retrying in the background.";

    [UnityTest]
    public IEnumerator DeviceAuth_Offline_RetriesWithBackoff()
    {
        var transport = UseScriptedTransport(Offline);
        // LogAssert matches expected logs in order: the first retry warning comes just before the report.
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Retrying in 1 seconds")));
        ExpectRetryingReport();
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Retrying in 2 seconds")));

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 3, 15f);

        Assert.GreaterOrEqual(transport.AuthCalls, 3, "An offline launch must keep retrying, not latch as rejected.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_FirstTransientFailure_ReportsFalseOnce()
    {
        // Apps start without auth on OnAuthCompleted(false), as they did when every failure latched, while the SDK retries.
        var transport = UseScriptedTransport(Offline);
        var results = new List<bool>();
        var errors = new List<string>();
        Abxr.OnAuthCompleted += (success, error) => { results.Add(success); errors.Add(error); };
        ExpectRetryingReport();

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1, 5f);

        CollectionAssert.AreEqual(new[] { false }, results);
        Assert.AreEqual(RetryingReport, errors[0], "The message must say that the SDK keeps retrying.");
        Assert.AreEqual(1, transport.AuthCalls, "The report comes with the first failure, not after a retry.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive, "Reporting false must not end the attempt.");
    }

    [UnityTest]
    public IEnumerator DeviceAuth_FurtherTransientFailures_DoNotReportAgain()
    {
        var transport = UseScriptedTransport(Offline);
        transport.AnswerNextWith(Offline);
        transport.AnswerNextWith(ServiceUnavailable);
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        ExpectRetryingReport();

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 3, 15f);

        Assert.GreaterOrEqual(transport.AuthCalls, 3);
        CollectionAssert.AreEqual(new[] { false }, results, "Later transient failures of the same attempt, of any kind, must not report again.");
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_RetrySucceeds_ReportsTrueAfterTheFalse()
    {
        var transport = UseScriptedTransport(Authorized);
        transport.AnswerNextWith(Offline);
        transport.AnswerNextWith(Offline);
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        ExpectRetryingReport();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 2, 10f);

        CollectionAssert.AreEqual(new[] { false, true }, results, "A retry that succeeds must report true after the false.");
        Assert.AreEqual(3, transport.AuthCalls);
        Assert.IsTrue(auth.Authenticated);
        Assert.IsFalse(auth.IsAuthenticationAttemptActive);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_RefusalAfterRetrying_LatchesWithoutReportingAgain()
    {
        var transport = UseScriptedTransport(Unauthorized);
        transport.AnswerNextWith(Offline);
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        ExpectRetryingReport();
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Invalid app token (device authentication stopped retrying")));
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 2 && !auth.IsAuthenticationAttemptActive, 5f);

        Assert.AreEqual(2, transport.AuthCalls);
        Assert.IsFalse(auth.IsAuthenticationAttemptActive, "A refusal ends the attempt.");
        CollectionAssert.AreEqual(new[] { false }, results, "The app already has this attempt's failure; the refusal must not report another.");

        // The refusal latched: a new call reports the latch without reaching the API.
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Authentication was rejected by the API")));
        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 2, 5f);

        CollectionAssert.AreEqual(new[] { false, false }, results);
        Assert.AreEqual(2, transport.AuthCalls, "A refusal must not reach the API again this session.");
    }

    [UnityTest]
    public IEnumerator DeviceAuth_Offline_SaysOncePerAttemptThatItIsRetrying()
    {
        // Release builds drop warnings, so the retry warning alone would leave a stuck headset's log silent. The report's
        // failure log is an error, which release builds keep.
        var transport = UseScriptedTransport(Offline);
        var errorLines = new List<string>();
        Application.LogCallback onLog = (message, stackTrace, type) =>
        {
            if (type == LogType.Error && message.Contains("retrying in the background")) errorLines.Add(message);
        };
        Application.logMessageReceived += onLog;
        ExpectRetryingReport();
        try
        {
            Abxr.StartAuthentication();
            yield return WaitFor(() => transport.AuthCalls >= 3, 15f);

            Assert.GreaterOrEqual(transport.AuthCalls, 3);
            Assert.AreEqual(1, errorLines.Count, "The retry is announced once per attempt, not on every retry.");
            StringAssert.Contains("(no connection)", errorLines[0]);
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
        ExpectRetryingReport();
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
    public IEnumerator DeviceAuth_UnauthorizedWithoutErrorBody_ReportsARefusal()
    {
        // What the REST transport produces for a bare 401: the body is normalized to "No response body."
        var transport = UseScriptedTransport(AbxrTransportRest.ToAuthResult(UnityWebRequest.Result.ProtocolError, 401, ""));
        string reported = null;
        Abxr.OnAuthCompleted += (success, error) => reported = error;
        const string refusal = "Authentication was rejected by the API (credentials invalid or denied).";
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: " + refusal)));

        Abxr.StartAuthentication();
        yield return WaitFor(() => reported != null, 5f);

        Assert.AreEqual(refusal, reported);
        Assert.AreEqual(1, transport.AuthCalls);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_EndSessionWhileRetrying_StopsRetrying()
    {
        var transport = UseScriptedTransport(Offline);
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        ExpectRetryingReport();

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1, 5f);
        Abxr.EndSession();
        // The first retry was due one second after the first failure.
        yield return new WaitForSeconds(2.5f);

        Assert.AreEqual(1, transport.AuthCalls, "EndSession must stop the retries of the session it ended.");
        CollectionAssert.AreEqual(new[] { false }, results, "An ended session must report nothing after its first failure.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);

        // A new attempt reports its own first failure.
        ExpectRetryingReport();
        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 2, 5f);
        Assert.AreEqual(2, transport.AuthCalls, "StartAuthentication after EndSession must send a request, not be ignored as already in progress.");
        CollectionAssert.AreEqual(new[] { false, false }, results);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_EndSessionWhileRetrying_ReportsNothingLate()
    {
        // The retry in flight when the session ends would succeed. The ended attempt must neither report it nor apply it.
        var transport = UseScriptedTransport(Offline);
        transport.AnswerNextWith(Offline);
        transport.AnswerNextWith(UserAuthRequired);
        bool releaseRetry = false;
        transport.HoldCall = call => call == 2 && !releaseRetry;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        ExpectRetryingReport();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;

        Abxr.StartAuthentication();
        yield return WaitFor(() => results.Count >= 1 && transport.AuthCalls >= 2, 5f);
        Assert.AreEqual(2, transport.AuthCalls, "The retry must be in flight before the session ends.");
        Abxr.EndSession();
        releaseRetry = true;
        // Long enough for the late success to be applied, and for a further retry, had the attempt carried on.
        yield return new WaitForSecondsRealtime(2.5f);

        CollectionAssert.AreEqual(new[] { false }, results, "After EndSession the ended attempt must report nothing, not even its late success.");
        Assert.IsFalse(auth.Authenticated);
        Assert.IsNull(auth.ResponseData.AppId, "The late success must not be applied after EndSession.");
        Assert.AreEqual(2, transport.AuthCalls, "The ended attempt must not retry.");
        Assert.AreEqual(0, transport.ConfigCalls, "The ended attempt must not go on to fetch config.");
    }

    [UnityTest]
    public IEnumerator DeviceAuth_StartNewSessionDuringRequest_DropsTheOldAttempt()
    {
        var transport = UseScriptedTransport(Offline);
        transport.Hold = true;
        var results = new List<bool>();
        Abxr.OnAuthCompleted += (success, error) => results.Add(success);
        // Only the new session's attempt reports its failure.
        ExpectRetryingReport();
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
            CollectionAssert.AreEqual(new[] { false }, results, "Only the new session's attempt may report its failure.");
            Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.IsAuthenticationAttemptActive);
        }
        finally
        {
            Application.logMessageReceived -= onLog;
        }
    }

    [UnityTest]
    public IEnumerator DeviceAuth_StartNewSessionDuringRequest_DropsALateSuccess()
    {
        var transport = UseScriptedTransport(Offline);
        transport.AnswerNextWith(UserAuthRequired);
        bool releaseOldRequest = false;
        // The old session's request succeeds only after StartNewSession; the new session's request stays in flight.
        transport.HoldCall = call => call == 1 ? !releaseOldRequest : true;
        bool completed = false;
        Abxr.OnAuthCompleted += (success, error) => completed = true;
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.AuthCalls >= 1, 5f);
        Abxr.StartNewSession();
        yield return WaitFor(() => transport.AuthCalls >= 2, 1f);
        releaseOldRequest = true;
        yield return new WaitForSecondsRealtime(0.25f);

        Assert.IsNull(auth.ResponseData.AppId, "The old session's late success must not be applied to the new session.");
        Assert.IsFalse(auth.Authenticated);
        Assert.AreEqual(0, transport.ConfigCalls, "The old attempt must not go on to fetch config.");
        Assert.IsFalse(completed);
    }

    [UnityTest]
    public IEnumerator DeviceAuth_StartNewSessionDuringConfigFetch_DropsTheOldConfig()
    {
        var transport = UseScriptedTransport(Offline);
        transport.AnswerNextWith(UserAuthRequired);
        transport.HoldCall = call => call >= 2;
        transport.HoldConfig = true;
        transport.ConfigJson = "{\"launcherAppID\":\"old-session-launcher\"}";
        bool completed = false;
        Abxr.OnAuthCompleted += (success, error) => completed = true;

        Abxr.StartAuthentication();
        yield return WaitFor(() => transport.ConfigCalls >= 1, 5f);
        Abxr.StartNewSession();
        yield return WaitFor(() => transport.AuthCalls >= 2, 1f);
        transport.HoldConfig = false;
        yield return new WaitForSecondsRealtime(0.25f);

        Assert.AreEqual(1, transport.ConfigCalls);
        Assert.AreNotEqual("old-session-launcher", Configuration.Instance.launcherAppID, "The old session's config must not be applied after StartNewSession.");
        Assert.IsFalse(completed);
    }

    [UnityTest]
    public IEnumerator SetUserData_EndSessionDuringSync_ReportsTheSyncAsFailed()
    {
        var transport = UseScriptedTransport(Offline);
        transport.Hold = true;
        SimulateAuth();
        bool? synced = null;
        string syncError = null;
        Abxr.OnUserDataSyncCompleted = (success, error) => { synced = success; syncError = error; };

        Abxr.SetUserId("user-1");
        yield return WaitFor(() => transport.AuthCalls >= 1, 5f);
        Abxr.EndSession();
        transport.Hold = false;
        yield return WaitFor(() => synced.HasValue, 2f);

        Assert.AreEqual(false, synced, "A sync cut off by EndSession must still be reported.");
        Assert.AreEqual("The session ended before user data was synced.", syncError);
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
        // A host whose AbxrLib asset sets unit-test SSO would otherwise make every success start a SetUserData re-auth
        // (an extra transport call), and make SimulateAuth start one too. BaseSetUp turns this back on for the next test.
        AbxrSubsystem.UnitTestSsoSimulationFromConfigAllowed = false;
        return transport;
    }

    /// <summary>The subsystem logs every reported failure as an error, and LogAssert fails a test on an error it did not expect.</summary>
    private static void ExpectRetryingReport()
        => LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: " + RetryingReport)));

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private sealed class ScriptedAuthTransport : IAbxrTransport
    {
        private readonly AuthTransportResult _response;
        private readonly Queue<AuthTransportResult> _nextResponses = new Queue<AuthTransportResult>();
        public int AuthCalls { get; private set; }
        public int ConfigCalls { get; private set; }
        /// <summary>While true, requests stay in flight: the transport was called but has not answered yet.</summary>
        public bool Hold { get; set; }
        /// <summary>Holds individual requests by their 1-based call number, on top of <see cref="Hold"/>.</summary>
        public Func<int, bool> HoldCall { get; set; } = call => false;
        /// <summary>While true, GET config stays in flight.</summary>
        public bool HoldConfig { get; set; }
        /// <summary>The GET config answer; null answers with a failure.</summary>
        public string ConfigJson { get; set; }

        public ScriptedAuthTransport(AuthTransportResult response) => _response = response;

        /// <summary>Answers the next request with this result instead of the default one.</summary>
        public void AnswerNextWith(AuthTransportResult response) => _nextResponses.Enqueue(response);

        public bool IsServiceTransport => false;

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete)
        {
            int call = ++AuthCalls;
            var response = _nextResponses.Count > 0 ? _nextResponses.Dequeue() : _response;
            while (Hold || HoldCall(call))
                yield return null;
            onComplete?.Invoke(response);
        }

        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete)
        {
            ConfigCalls++;
            while (HoldConfig)
                yield return null;
            if (ConfigJson != null)
                onComplete?.Invoke(true, ConfigJson);
            else
                onComplete?.Invoke(false, "not scripted");
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

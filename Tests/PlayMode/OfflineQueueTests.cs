// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for offline capture (SDK-72, offline RFC) through the real REST transport and a local HTTP listener:
// sessions kept across a relaunch and replayed, chunked sends when the connection returns, sealing on EndSession and
// StartNewSession, the offline sign-in from a cached policy, the cached telemetry setting, and upload status.
// The queue's file format and the replayer's decisions are covered in EditMode (DurableRecordQueueTests,
// SessionReplayerTests); revoke, suspend and clear in PairingIdentityTests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Services.Transport.Offline;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class OfflineQueueTests : AbxrPlayModeTestBase
{
    private const string AuthPath = "/v1/auth/token";
    private const string ConfigPath = "/v1/storage/config";
    private const string DataPath = "/v1/collect/data";

    private LocalRestServer _server;
    /// <summary>While true the backend answers data and sign-in with 503, as an outage would.</summary>
    private volatile bool _offline;
    private readonly List<(bool success, string reason)> _reports = new List<(bool, string)>();
    /// <summary>What GET config answers. Null for an empty config.</summary>
    private volatile string _configBody;

    [SetUp]
    public void SetUpOffline()
    {
        if (AbxrSubsystem.Instance?.GetTransportForTesting()?.IsServiceTransport == true)
            Assert.Ignore("These tests drive the REST transport. Run them in the Editor.");
        _offline = false;
        _configBody = null;
        _reports.Clear();
        _server = new LocalRestServer { Respond = Answer };
        PointAtServer();
    }

    [TearDown]
    public void TearDownOffline()
    {
        Abxr.OnAuthCompleted -= Report;
        _server?.Dispose();
    }

    private void PointAtServer()
    {
        ModifyConfig("restUrl", _server.Url);
        ModifyConfig("sendRetriesOnFailure", 0);
        ModifyConfig("sendRetryIntervalSeconds", 1);
        // Keeps automatic telemetry out of the queue, so each test sees only its own records.
        AbxrSubsystem.Instance.AuthServiceForTesting.OnConfigReceived(new ConfigPayload { enableAutomaticTelemetry = false });
        Abxr.OnAuthCompleted += Report;
    }

    private void Report(bool success, string reason) => _reports.Add((success, reason));

    private LocalRestServer.Answer? Answer(string path, string body)
    {
        if (_offline && path != ConfigPath) return new LocalRestServer.Answer(503);
        if (path == AuthPath) return new LocalRestServer.Answer(200, LocalRestServer.AuthSuccessBody());
        if (path == ConfigPath) return new LocalRestServer.Answer(200, _configBody ?? "{}");
        return null;
    }


    private static DurableRecordQueue Queue => (DurableRecordQueue)AbxrSubsystem.Instance.RestTransportForTesting.QueueForTesting;

    private void Relaunch()
    {
        FullTeardownForAppSwitch();
        BaseSetUpForAppSwitch();
        CreateSubsystem();
        PointAtServer();
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    // ── Close and keep, replay ───────────────────────────────

    [UnityTest]
    public IEnumerator EndedSession_UploadsAsItselfAfterARelaunch()
    {
        _offline = true;
        SimulateAuth();
        string endedSession = AbxrSubsystem.Instance.AuthServiceForTesting.SessionId;
        Abxr.Event("recorded offline", null, sendTelemetry: false);

        Abxr.EndSession();

        Assert.AreEqual(1, Queue.SealedSessions().Count, "EndSession keeps what it couldn't send (offline RFC D2).");

        Relaunch();
        Assert.AreEqual(endedSession, Queue.SealedSessions().Single(s => s.SignIn != null).SessionId, "It survived the relaunch.");
        _offline = false;
        SimulateAuth(); // the live session's sign-in proves the connection

        yield return WaitFor(() => Queue.SealedSessions().Count == 0, 10f);

        Assert.AreEqual(0, Queue.SealedSessions().Count, "Replayed and deleted.");
        Assert.IsTrue(_server.Bodies(AuthPath).Any(b => b.Contains("\"sessionId\":\"" + endedSession + "\"")),
            "The ended session signs in again as itself.");
        Assert.IsTrue(_server.Bodies(DataPath).Any(b => b.Contains("recorded offline")));
    }

    [UnityTest]
    public IEnumerator StartNewSession_SealsTheOldSession_AndTheNewOneStartsEmpty()
    {
        _offline = true;
        SimulateAuth();
        Abxr.Event("before", null, sendTelemetry: false);

        Abxr.StartNewSession();
        yield return null;

        Assert.IsEmpty(AbxrSubsystem.Instance.GetPendingEventsForTesting(), "None of the old session's records go with the new one.");
        var sealedSession = Queue.SealedSessions().Single();
        var chunk = Queue.Peek(sealedSession, RecordStream.Data, 100);
        CollectionAssert.Contains(chunk.Records.Select(r => (r.Payload as EventPayload)?.name).ToList(), "before");
    }

    [UnityTest]
    public IEnumerator ConnectionReturningMidSession_SendsTheBacklogInChunks()
    {
        ModifyConfig("dataEntriesPerSendAttempt", 2);
        ModifyConfig("maxCallFrequencySeconds", 0.1f);
        _offline = true;
        SimulateAuth();
        var names = new[] { "e1", "e2", "e3", "e4", "e5" };
        foreach (var n in names) Abxr.Event(n, null, sendTelemetry: false);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Count > 0, 5f);
        Assert.AreEqual(5, AbxrSubsystem.Instance.GetPendingEventsForTesting().Count, "Nothing is lost while offline.");

        _offline = false;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => AbxrSubsystem.Instance.GetPendingEventsForTesting().Count == 0, 10f);

        var sent = _server.Bodies(DataPath).Skip(1).ToList();
        foreach (var n in names) Assert.IsTrue(sent.Any(b => b.Contains("\"" + n + "\"")), n);
        Assert.IsTrue(sent.All(b => Regex.Matches(b, "\"recordId\"").Count <= 2), "Each request carries one chunk.");
    }

    // ── Offline sign-in from the cached policy (D7) ──────────

    private static readonly string OrgToken = "eyJhbGciOiJIUzI1NiJ9." +
        Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"orgId\":\"org-offline-test\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".sig";

    /// <summary>A managed headset's credentials, and the org's last sign-in policy cached for them.</summary>
    private void CachePolicy(string type)
    {
        string appToken = Configuration.Instance.appToken;
        SetRuntimeAuth(new RuntimeAuthConfig { useAppTokens = true, buildType = "development", appToken = appToken, orgToken = OrgToken });
        string key = OfflineSettingsCache.KeyOf(new AuthPayload { appToken = appToken, orgToken = OrgToken });
        AbxrSubsystem.Instance.OfflineSettingsForTesting.Save(key, new CachedOrgSettings
        {
            authMechanism = new AuthMechanism { type = type, prompt = "Sign in", domain = "" },
            automaticTelemetry = false
        });
        AbxrSubsystem.UnitTestSsoSimulationFromConfigAllowed = false;
    }

    [UnityTest]
    public IEnumerator CachedEmailPolicy_Offline_SignsInWithoutAConnection_AndSendsTheAnswerOnceOnline()
    {
        CachePolicy("email");
        _configBody = "{\"authMechanism\":{\"type\":\"email\",\"prompt\":\"Sign in\",\"domain\":\"\"}}";
        int prompts = 0;
        Abxr.OnInputRequested = (type, prompt, domain, error) =>
        {
            prompts++;
            Assert.AreEqual("email", type);
            Abxr.OnInputSubmitted("sam@example.com");
        };
        _offline = true;

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 10f);

        Assert.AreEqual((true, (string)null), _reports.Single(), "Offline RFC: a cached email sign-in reports success.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated, "No token yet.");
        Abxr.Event("signed in offline", null, sendTelemetry: false);

        _offline = false;
        yield return WaitFor(() => AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated, 15f);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Any(b => b.Contains("signed in offline")), 5f);

        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated);
        Assert.AreEqual(1, _reports.Count, "The online sign-in finishes without a second OnAuthCompleted.");
        Assert.AreEqual(1, prompts, "The person isn't asked again.");
        Assert.IsTrue(_server.Bodies(AuthPath).Any(b => b.Contains("sam@example.com") && b.Contains("\"type\":\"email\"")));
        Assert.IsTrue(_server.Bodies(DataPath).Any(b => b.Contains("signed in offline")));
    }

    [UnityTest]
    public IEnumerator OfflinePromptStillOpen_WhenTheSessionSignsInOnline_ReportsOnceAndClosesIt()
    {
        CachePolicy("email");
        _configBody = "{\"authMechanism\":{\"type\":\"none\"}}"; // the org dropped the sign-in since
        int prompts = 0;
        Abxr.OnInputRequested = (type, prompt, domain, error) => prompts++; // nobody answers
        _offline = true;

        Abxr.StartAuthentication();
        yield return WaitFor(() => prompts > 0, 10f);
        Assert.AreEqual(0, _reports.Count, "Nothing is reported until the person answers.");
        _offline = false;
        yield return WaitFor(() => AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated, 15f);

        Assert.AreEqual((true, (string)null), _reports.Single());
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsInputRequestPending, "The offline prompt closed.");
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("OnInputSubmitted was ignored")));
        Abxr.OnInputSubmitted("late@example.com");
        Assert.AreEqual(1, _reports.Count, "A late answer can't report success a second time.");
    }

    [UnityTest]
    public IEnumerator OfflinePromptOpen_WhenTheBackendRefusesTheDevice_ReportsFalse()
    {
        CachePolicy("email");
        Abxr.OnInputRequested = (type, prompt, domain, error) => { };
        _server.Respond = (path, body) => path == AuthPath
            ? (_offline ? new LocalRestServer.Answer(503) : new LocalRestServer.Answer(401, "{\"detail\":\"Invalid credentials\"}"))
            : (LocalRestServer.Answer?)null;
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: Invalid credentials")));
        _offline = true;

        Abxr.StartAuthentication();
        yield return new WaitForSecondsRealtime(0.5f);
        _offline = false;
        yield return WaitFor(() => _reports.Count > 0, 10f);

        Assert.AreEqual((false, "Invalid credentials"), _reports.Single(), "The app hears the refusal even though nothing reported false before.");
        Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsInputRequestPending);
    }

    [UnityTest]
    public IEnumerator CachedPinPolicy_Offline_SaysAConnectionIsNeeded()
    {
        CachePolicy("assessmentPin");
        bool prompted = false;
        Abxr.OnInputRequested = (type, prompt, domain, error) => prompted = true;
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Authentication failure: A connection is needed to sign in with a PIN.")));
        _offline = true;

        Abxr.StartAuthentication();
        yield return WaitFor(() => _reports.Count > 0, 10f);

        Assert.AreEqual((false, "A connection is needed to sign in with a PIN."), _reports.Single());
        Assert.IsFalse(prompted, "A PIN is checked by the backend, so there's nothing to ask offline.");
    }

    // ── Cached telemetry setting (D4) ────────────────────────

    [UnityTest]
    public IEnumerator OrgTelemetryOff_FromTheLastConfig_StopsAutomaticTelemetry_AndDropsTelemetry()
    {
        var created = new List<AbxrObserver.Record>();
        FullTeardownForAppSwitch();
        File.WriteAllText(Path.Combine(QueueRoot, "settings.json"),
            "{\"last\":\"k\",\"entries\":{\"k\":{\"automaticTelemetry\":false}}}");
        BaseSetUpForAppSwitch();
        CreateSubsystem();
        AbxrObserver.ResetForTesting();
        AbxrObserver.OnRecordCreated += created.Add;
        try
        {
            Assert.IsFalse(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting, "Off at Start(), before any config arrives.");

            Abxr.Telemetry("Heart Rate", new Dictionary<string, string> { ["bpm"] = "80" });
            Abxr.Event("with snapshot?");
            yield return null;

            var telemetry = created.Where(r => r.Kind == AbxrObserver.RecordKind.Telemetry).ToList();
            Assert.AreEqual("Heart Rate", telemetry.Single().Name, "The app's telemetry call is reported, and nothing automatic.");
            Assert.AreEqual(AbxrObserver.DropReason.TelemetryOff, telemetry.Single().DropReason, "Not kept while the org has telemetry off, and the observer says why.");
            Assert.IsTrue(created.Any(r => r.Name == "with snapshot?"), "Events still record.");

            AbxrSubsystem.Instance.AuthServiceForTesting.OnConfigReceived(new ConfigPayload { enableAutomaticTelemetry = true });
            Assert.IsTrue(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting, "This launch's config turns it back on.");
        }
        finally { AbxrObserver.ResetForTesting(); }
    }

    [Test]
    public void TrackAutoTelemetry_SurvivesTheConfig()
    {
        FullTeardownForAppSwitch();
        BaseSetUpForAppSwitch();
        ModifyConfig("enableAutomaticTelemetry", false);
        CreateSubsystem();
        Abxr.TrackAutoTelemetry();
        Assert.IsTrue(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting);

        AbxrSubsystem.Instance.AuthServiceForTesting.OnConfigReceived(new ConfigPayload { enableAutomaticTelemetry = true });

        Assert.IsTrue(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting, "The app asked for it; only the org turning it off stops it.");
        AbxrSubsystem.Instance.AuthServiceForTesting.OnConfigReceived(new ConfigPayload { enableAutomaticTelemetry = false });
        Assert.IsFalse(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting);
    }

    [Test]
    public void NothingCached_AutomaticTelemetryWaitsForTheConfig()
    {
        FullTeardownForAppSwitch();
        File.Delete(Path.Combine(QueueRoot, "settings.json"));
        BaseSetUpForAppSwitch();
        CreateSubsystem();

        Assert.IsFalse(AbxrSubsystem.Instance.AutomaticTelemetryRunningForTesting);
    }

    // ── Clear and upload status (D9, D11) ────────────────────

    [UnityTest]
    public IEnumerator ClearQueuedData_DeletesEverySession_WithoutEndingTheSession()
    {
        _offline = true;
        SimulateAuth();
        Abxr.Event("old", null, sendTelemetry: false);
        Abxr.StartNewSession();
        yield return null;
        SimulateAuth();
        Abxr.Event("new", null, sendTelemetry: false);
        Assert.AreEqual(2, Abxr.GetUploadStatus().PendingSessions);
        Assert.GreaterOrEqual(Abxr.GetPendingUploadCount(), 2);

        Abxr.ClearQueuedData();

        Assert.AreEqual(0, Abxr.GetPendingUploadCount());
        Assert.IsTrue(Abxr.GetUploadStatus().AllUploaded);
        Assert.IsTrue(AbxrSubsystem.Instance.AuthServiceForTesting.Authenticated, "The session goes on.");
    }

    [UnityTest]
    public IEnumerator UploadStatus_ReportsPending_ThenAllUploaded()
    {
        var statuses = new List<Abxr.UploadStatus>();
        Action<Abxr.UploadStatus> onStatus = statuses.Add;
        Abxr.OnUploadStatusChanged += onStatus;
        try
        {
            SimulateAuth();
            Abxr.Event("tracked", null, sendTelemetry: false);
            yield return WaitFor(() => statuses.Any(s => s.PendingRecords > 0), 3f);
            AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
            yield return WaitFor(() => statuses.Count > 0 && statuses.Last().AllUploaded && statuses.Last().UploadedRecords > 0, 5f);

            // The live session can hold a few records from startup (scene events) besides this one.
            Assert.IsTrue(statuses.Any(s => s.PendingRecords >= 1 && s.PendingSessions == 1 && !s.AllUploaded));
            Assert.IsTrue(statuses.Last().AllUploaded);
            Assert.GreaterOrEqual(statuses.Last().UploadedRecords, 1);
        }
        finally { Abxr.OnUploadStatusChanged -= onStatus; }
    }
}

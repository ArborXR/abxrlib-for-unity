// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for AbxrObserver (SDK-64) through the public Abxr methods and the real REST transport, which sends to
// a local HTTP listener so each test picks the backend's answer. AbxrObserverTests (EditMode) covers the scope rules.
// From 3.1 an ended session is kept and uploaded later (offline RFC D2), and a refused record is dropped as Rejected
// instead of being queued again; OfflineQueueTests covers the rest of the queue.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AbxrLib.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AbxrObserverRestTests : AbxrPlayModeTestBase
{
    private const string DataPath = "/v1/collect/data";
    private const string StoragePath = "/v1/storage";

    private LocalRestServer _server;
    private List<AbxrObserver.Record> _created;
    private List<AbxrObserver.SendResult> _sent;

    [SetUp]
    public void SetUpObserver()
    {
        if (AbxrSubsystem.Instance?.GetTransportForTesting()?.IsServiceTransport == true)
            Assert.Ignore("These tests drive the REST transport. Run them in the Editor.");
        AbxrObserver.ResetForTesting();
        _created = new List<AbxrObserver.Record>();
        _sent = new List<AbxrObserver.SendResult>();
        AbxrObserver.OnRecordCreated += _created.Add;
        AbxrObserver.OnRecordsSent += _sent.Add;
        _server = new LocalRestServer();
        ModifyConfig("restUrl", _server.Url);
    }

    [TearDown]
    public void TearDownObserver()
    {
        AbxrObserver.ResetForTesting();
        _server?.Dispose();
    }

    // ── Records from the public methods ───────────────────────────────────

    [Test]
    public void PublicMethod_ReportsTheMethod_TheCallersData_AndWhatTheSdkAdded()
    {
        var meta = new Dictionary<string, string> { ["attempt"] = "2" };
        Abxr.EventAssessmentComplete("Quiz", 87, Abxr.EventStatus.Pass, meta);

        var record = Single("Quiz");
        Assert.AreEqual("EventAssessmentComplete", record.Method);
        Assert.IsFalse(record.Automatic);
        CollectionAssert.AreEquivalent(new Dictionary<string, string> { ["attempt"] = "2" }, record.CallerData);
        Assert.AreEqual("87", record.Data["score"]);
        Assert.AreEqual("0", record.Data["score_min"]);
        Assert.AreEqual("100", record.Data["score_max"]);
        Assert.AreEqual("pass", record.Data["status"]);
        Assert.AreEqual("2", record.Data["attempt"]);
    }

    [Test]
    public void OverloadThatDelegates_ReportsTheMethodTheAppCalled()
    {
        Abxr.LogDebug("Opened the menu");
        Abxr.EventAssessmentComplete("Quiz", "75");

        Assert.AreEqual("LogDebug", Single("Opened the menu").Method);
        Assert.AreEqual("debug", Single("Opened the menu").Level);
        Assert.AreEqual("EventAssessmentComplete", Single("Quiz").Method);
    }

    [Test]
    public void ClosingEventsOnEndSession_AreAutomatic()
    {
        Abxr.EventAssessmentStart("Quiz");
        Abxr.EndSession();

        var records = _created.Where(r => r.Name == "Quiz").ToList();
        Assert.AreEqual(2, records.Count);
        Assert.IsFalse(records[0].Automatic, "The app started the assessment.");
        Assert.AreEqual("EventAssessmentStart", records[0].Method);
        Assert.IsTrue(records[1].Automatic, "The SDK closed it.");
        Assert.IsNull(records[1].Method);
        Assert.AreEqual("true", records[1].Data["auto_closed"]);
    }

    // ── REST batches ──────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Batch_Accepted_ReportsSentWithTheBatchsIds()
    {
        SimulateAuth();
        Abxr.Event("first", null, sendTelemetry: false);
        Abxr.LogInfo("second");
        long first = Single("first").Id, second = Single("second").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => ResultFor(first).HasValue, 5f);

        var result = ResultFor(first).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Sent, result.Status);
        Assert.AreEqual(200, result.HttpStatus);
        Assert.IsNull(result.Error);
        CollectionAssert.Contains(result.RecordIds, second, "One POST carries both records, so one result names both.");
        Assert.IsFalse(_server.Bodies(DataPath).Any(b => b.Contains("ObserverId")), "The observer id must never reach the backend.");
        Assert.IsTrue(_server.Bodies(DataPath).All(b => b.Contains("\"recordId\"")), "Each record carries its UUID for the backend's duplicate check.");
    }

    [UnityTest]
    public IEnumerator Batch_Refused_DropsTheRecordAsRejected_WithTheStatus()
    {
        _server.StatusCode = 400;
        ModifyConfig("sendNextBatchWaitSeconds", 60); // one attempt in this test
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Data POST failed")));
        SimulateAuth();
        Abxr.Event("refused", null, sendTelemetry: false);
        long id = Single("refused").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => ResultFor(id).HasValue, 5f);

        var result = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, result.Status);
        Assert.AreEqual(AbxrObserver.DropReason.Rejected, result.DropReason);
        Assert.AreEqual(400, result.HttpStatus);
        Assert.IsFalse(string.IsNullOrEmpty(result.Error));
        yield return WaitFor(() => AbxrSubsystem.Instance.GetPendingEventsForTesting().All(e => e.name != "refused"), 2f);
        Assert.IsFalse(AbxrSubsystem.Instance.GetPendingEventsForTesting().Any(e => e.name == "refused"), "A refused record never comes back.");
    }

    [UnityTest]
    public IEnumerator Batch_Refused_IsolatesTheRefusedRecord_AndSendsTheRest()
    {
        // The backend refuses any body that carries the bad record, so the transport splits the chunk until it's alone.
        _server.Respond = (path, body) => path == DataPath && body.Contains("\"bad\"") ? new LocalRestServer.Answer(422) : (LocalRestServer.Answer?)null;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Data POST failed")));
        SimulateAuth();
        foreach (var name in new[] { "a", "b", "bad", "c", "d" }) Abxr.Event(name, null, sendTelemetry: false);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => new[] { "a", "b", "bad", "c", "d" }.All(n => ResultFor(Single(n).Id).HasValue), 10f);

        foreach (var name in new[] { "a", "b", "c", "d" })
            Assert.AreEqual(AbxrObserver.SendStatus.Sent, ResultFor(Single(name).Id).Value.Status, name);
        var bad = ResultFor(Single("bad").Id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, bad.Status);
        Assert.AreEqual(AbxrObserver.DropReason.Rejected, bad.DropReason);
        Assert.AreEqual(422, bad.HttpStatus);
        Assert.IsEmpty(AbxrSubsystem.Instance.GetPendingEventsForTesting(), "Every record got a final answer.");
    }

    [UnityTest]
    public IEnumerator Batch_RateLimited_WaitsForRetryAfter_EvenAsTheQueueFills()
    {
        _server.Respond = (path, body) => path == DataPath ? new LocalRestServer.Answer(429, "{}", "30") : (LocalRestServer.Answer?)null;
        ModifyConfig("dataEntriesPerSendAttempt", 2);
        ModifyConfig("maxCallFrequencySeconds", 0.1f);
        SimulateAuth();
        Abxr.Event("first", null, sendTelemetry: false);
        Abxr.Event("second", null, sendTelemetry: false);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Count > 0, 5f);

        // A full queue and ForceSend both ask to send now; a 429's Retry-After still wins.
        float until = Time.realtimeSinceStartup + 2f;
        for (int i = 0; Time.realtimeSinceStartup < until; i++)
        {
            Abxr.Event("more" + i, null, sendTelemetry: false);
            AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
            yield return null;
        }

        Assert.AreEqual(1, _server.Bodies(DataPath).Count, "Nothing more goes out during Retry-After.");
    }

    [UnityTest]
    public IEnumerator Batch_RequestRefused403_KeepsTheRecordsQueued_AndReportsFailed() => RequestRefusedKeepsRecords(403);

    [UnityTest]
    public IEnumerator Batch_RequestRefused404_KeepsTheRecordsQueued_AndReportsFailed() => RequestRefusedKeepsRecords(404);

    /// <summary>A refusal of the request itself (suspended, wrong URL) must not split the chunk and drop every record.</summary>
    private IEnumerator RequestRefusedKeepsRecords(int status)
    {
        _server.StatusCode = status;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        SimulateAuth();
        foreach (var name in new[] { "a", "b", "c" }) Abxr.Event(name, null, sendTelemetry: false);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => ResultFor(Single("a").Id).HasValue, 5f);

        foreach (var name in new[] { "a", "b", "c" })
        {
            var result = ResultFor(Single(name).Id).Value;
            Assert.AreEqual(AbxrObserver.SendStatus.Failed, result.Status, name);
            Assert.AreEqual(status, result.HttpStatus, name);
        }
        Assert.AreEqual(1, _server.Bodies(DataPath).Count(), "One request: a refused request isn't split.");
        CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, AbxrSubsystem.Instance.GetPendingEventsForTesting().Select(e => e.name));
    }

    [UnityTest]
    public IEnumerator Batch_Failed_HandlerThatClearsTheQueue_DoesNotCarryTheBatchIntoTheNextSession()
    {
        _server.StatusCode = 503;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        ModifyConfig("sendRetriesOnFailure", 0);
        bool cleared = false;
        AbxrObserver.OnRecordsSent += result =>
        {
            if (result.Status != AbxrObserver.SendStatus.Failed) return;
            AbxrSubsystem.Instance.RestTransportForTesting.ClearAllPending(AbxrObserver.DropReason.Cleared);
            cleared = true;
        };
        SimulateAuth();
        Abxr.Event("old_session", null, sendTelemetry: false);
        long id = Single("old_session").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => cleared, 5f);

        Assert.IsTrue(cleared);
        Assert.IsFalse(AbxrSubsystem.Instance.GetPendingEventsForTesting().Any(e => e.name == "old_session"),
            "The failed batch stays queued until the handler clears it.");
        var last = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, last.Status, "Clearing it from inside the handler must still report it, after the handler returns.");
        Assert.AreEqual(AbxrObserver.DropReason.Cleared, last.DropReason);
    }

    [UnityTest]
    public IEnumerator Batch_Failed_StaysQueued_AndReportsFailed()
    {
        _server.StatusCode = 503;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        ModifyConfig("sendRetriesOnFailure", 0);
        SimulateAuth();
        Abxr.Event("offline", null, sendTelemetry: false);
        long id = Single("offline").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => ResultFor(id).HasValue, 5f);

        var result = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Failed, result.Status);
        Assert.AreEqual(503, result.HttpStatus);
        Assert.IsTrue(AbxrSubsystem.Instance.GetPendingEventsForTesting().Any(e => e.name == "offline"), "A failed batch stays queued.");
    }

    [UnityTest]
    public IEnumerator QueueClearedDuringARetry_ReportsTheBatchDroppedOnce()
    {
        _server.StatusCode = 503; // retried
        ModifyConfig("sendRetryIntervalSeconds", 1);
        ModifyConfig("sendRetriesOnFailure", 3);
        SimulateAuth();
        Abxr.Event("stale", null, sendTelemetry: false);
        long id = Single("stale").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Count > 0, 5f);

        Abxr.ClearQueuedData();
        yield return WaitFor(() => ResultFor(id).HasValue, 5f);
        yield return new WaitForSecondsRealtime(1.5f); // the retry wakes up and must stop quietly

        var result = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, result.Status);
        Assert.AreEqual(AbxrObserver.DropReason.Cleared, result.DropReason);
        Assert.AreEqual(1, _sent.Count(s => s.RecordIds.Contains(id)), "A dropped batch reports once, never as failed too.");
    }

    [Test]
    public void QueuedRecords_KeptWhenTheSessionEnds()
    {
        // Not authenticated, so nothing is sent.
        Abxr.Event("queued", null, sendTelemetry: false);

        Abxr.EndSession();

        Assert.IsFalse(_sent.Any(s => s.RecordIds.Contains(Single("queued").Id)), "An ended session is kept for upload, not dropped.");
    }

    [Test]
    public void QueuedRecords_ClearedByClearQueuedData_ReportDropped()
    {
        // Not authenticated, so nothing is sent and both stay queued.
        Abxr.Event("queued", null, sendTelemetry: false);
        Abxr.Telemetry("Heart Rate", new Dictionary<string, string> { ["bpm"] = "80" });

        Abxr.ClearQueuedData();

        var result = _sent.Single();
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, result.Status);
        Assert.AreEqual(AbxrObserver.DropReason.Cleared, result.DropReason);
        CollectionAssert.AreEquivalent(new[] { Single("queued").Id, Single("Heart Rate").Id }, result.RecordIds);
    }

    // ── Storage ───────────────────────────────────────────────────────────

    [Test]
    public void Storage_BeforeAuth_ReportsNotAuthenticated()
    {
        Abxr.StorageSetEntry("progress", new Dictionary<string, string> { ["level"] = "3" }, Abxr.StorageScope.Device);

        var record = Single("progress");
        Assert.AreEqual(AbxrObserver.RecordKind.Storage, record.Kind);
        Assert.AreEqual("StorageSetEntry", record.Method);
        Assert.AreEqual(AbxrObserver.DropReason.NotAuthenticated, record.DropReason);
    }

    [Test]
    public void UserStorage_WithNoSignedInUser_ReportsNoUser()
    {
        SimulateAuth(BuildTestAuthResponse(userId: null));
        Abxr.StorageSetDefaultEntry(new Dictionary<string, string> { ["level"] = "3" }, Abxr.StorageScope.User);

        var record = Single("state");
        Assert.AreEqual("StorageSetDefaultEntry", record.Method);
        Assert.AreEqual(AbxrObserver.DropReason.NoUser, record.DropReason);
    }

    [UnityTest]
    public IEnumerator Storage_Accepted_ReportsSent()
    {
        SimulateAuth();
        Abxr.StorageSetEntry("progress", new Dictionary<string, string> { ["level"] = "3" }, Abxr.StorageScope.Device);
        long id = Single("progress").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => ResultFor(id).HasValue, 5f);

        Assert.AreEqual(AbxrObserver.SendStatus.Sent, ResultFor(id).Value.Status);
        Assert.AreEqual(1, _server.Bodies(StoragePath).Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private AbxrObserver.Record Single(string name) => _created.Single(r => r.Name == name);

    /// <summary>The latest result that names this record.</summary>
    private AbxrObserver.SendResult? ResultFor(long id)
    {
        for (int i = _sent.Count - 1; i >= 0; i--)
            if (_sent[i].RecordIds.Contains(id)) return _sent[i];
        return null;
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }
}

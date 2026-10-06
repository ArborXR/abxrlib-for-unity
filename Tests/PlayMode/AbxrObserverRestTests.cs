// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for AbxrObserver (SDK-64) through the public Abxr methods and the real REST transport, which sends to
// a local HTTP listener so each test picks the backend's answer. AbxrObserverTests (EditMode) covers the scope rules.
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
using AbxrLib.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AbxrObserverRestTests : AbxrPlayModeTestBase
{
    private const string DataPath = "/v1/collect/data";
    private const string StoragePath = "/v1/storage";

    private LocalServer _server;
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
        _server = new LocalServer();
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
        Assert.IsFalse(_server.Bodies(DataPath).Any(b => b.Contains("RecordId")), "The observer id must never reach the backend.");
    }

    [UnityTest]
    public IEnumerator Batch_Refused_ReportsFailedWithTheStatusAndError_AndQueuesItAgain()
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
        Assert.AreEqual(AbxrObserver.SendStatus.Failed, result.Status);
        Assert.AreEqual(400, result.HttpStatus);
        Assert.IsFalse(string.IsNullOrEmpty(result.Error));
        Assert.IsTrue(AbxrSubsystem.Instance.GetPendingEventsForTesting().Any(e => e.name == "refused"), "A failed batch goes back in the queue.");
    }

    [UnityTest]
    public IEnumerator Batch_Refused_HandlerThatEndsTheSession_DoesNotCarryTheBatchIntoTheNextOne()
    {
        _server.StatusCode = 400;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Data POST failed")));
        bool cleared = false;
        AbxrObserver.OnRecordsSent += result =>
        {
            if (result.Status != AbxrObserver.SendStatus.Failed) return;
            AbxrSubsystem.Instance.RestTransportForTesting.ClearAllPending();
            cleared = true;
        };
        SimulateAuth();
        Abxr.Event("old_session", null, sendTelemetry: false);
        long id = Single("old_session").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();

        yield return WaitFor(() => cleared, 5f);

        Assert.IsTrue(cleared);
        Assert.IsFalse(AbxrSubsystem.Instance.GetPendingEventsForTesting().Any(e => e.name == "old_session"),
            "The failed batch must be queued before the handler runs, so ending the session clears it.");
        var last = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, last.Status, "Clearing it from inside the handler must still report it, after the handler returns.");
        Assert.AreEqual(AbxrObserver.DropReason.SessionEnded, last.DropReason);
    }

    [UnityTest]
    public IEnumerator Batch_Refused_WithNoRoomToQueueItAgain_ReportsTheOverflowDropped()
    {
        _server.StatusCode = 400;
        ModifyConfig("sendNextBatchWaitSeconds", 60);
        ModifyConfig("maximumCachedItems", 1);
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Data POST failed")));
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Event queue limit reached (1).")));
        SimulateAuth();
        Abxr.Event("kept", null, sendTelemetry: false);
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Count > 0, 5f);
        // Fills the queue again while the batch is on its way back.
        Abxr.Event("newer", null, sendTelemetry: false);
        long kept = Single("kept").Id;

        yield return WaitFor(() => _sent.Any(s => s.Status == AbxrObserver.SendStatus.Dropped), 5f);

        var dropped = _sent.Single(s => s.Status == AbxrObserver.SendStatus.Dropped);
        Assert.AreEqual(AbxrObserver.DropReason.QueueFull, dropped.DropReason);
        CollectionAssert.AreEqual(new[] { kept }, dropped.RecordIds);
    }

    [UnityTest]
    public IEnumerator SessionEndingDuringARetry_ReportsTheBatchDropped()
    {
        _server.StatusCode = 503; // retried
        ModifyConfig("sendRetryIntervalSeconds", 1);
        ModifyConfig("sendRetriesOnFailure", 3);
        SimulateAuth();
        Abxr.Event("stale", null, sendTelemetry: false);
        long id = Single("stale").Id;
        AbxrSubsystem.Instance.RestTransportForTesting.ForceSend();
        yield return WaitFor(() => _server.Bodies(DataPath).Count > 0, 5f);

        AbxrSubsystem.Instance.RestTransportForTesting.ClearAllPending();
        yield return WaitFor(() => ResultFor(id).HasValue, 5f);

        var result = ResultFor(id).Value;
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, result.Status);
        Assert.AreEqual(AbxrObserver.DropReason.SessionEnded, result.DropReason);
        Assert.AreEqual(1, _sent.Count(s => s.RecordIds.Contains(id)), "A dropped batch reports once, never as failed too.");
    }

    [Test]
    public void QueuedRecords_ClearedWhenTheSessionEnds_ReportDropped()
    {
        // Not authenticated, so nothing is sent and both stay queued.
        Abxr.Event("queued", null, sendTelemetry: false);
        Abxr.Telemetry("Heart Rate", new Dictionary<string, string> { ["bpm"] = "80" });

        AbxrSubsystem.Instance.RestTransportForTesting.ClearAllPending();

        var result = _sent.Single();
        Assert.AreEqual(AbxrObserver.SendStatus.Dropped, result.Status);
        Assert.AreEqual(AbxrObserver.DropReason.SessionEnded, result.DropReason);
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

    /// <summary>Answers every request with <see cref="StatusCode"/> and keeps each request's path and body.</summary>
    private sealed class LocalServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly ConcurrentQueue<(string path, string body)> _requests = new ConcurrentQueue<(string, string)>();
        public string Url { get; }
        public volatile int StatusCode = 200;

        public LocalServer()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _listener.BeginGetContext(OnRequest, null);
        }

        public List<string> Bodies(string path) => _requests.Where(r => r.path == path).Select(r => r.body).ToList();

        private void OnRequest(IAsyncResult ar)
        {
            HttpListenerContext context;
            try { context = _listener.EndGetContext(ar); }
            catch { return; } // stopped
            try { _listener.BeginGetContext(OnRequest, null); } catch { /* stopped */ }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();
            _requests.Enqueue((context.Request.Url.AbsolutePath, body));
            context.Response.StatusCode = StatusCode;
            byte[] bytes = Encoding.UTF8.GetBytes("{}");
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { /* already closed */ }
        }
    }
}

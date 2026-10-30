// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the on-disk queue (SDK-72, offline RFC): the file format, start-up, acknowledgements, sessions,
// retention and the disk budget. Pure C# against a temp folder and an injected clock.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Services.Transport.Offline;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

[TestFixture]
public class DurableRecordQueueTests
{
    private string _root;
    private DateTime _now;
    private int _retentionDays;
    private long _budget;
    private List<(string session, int count, AbxrObserver.DropReason reason)> _dropped;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "abxrlib-queue-tests", Guid.NewGuid().ToString("N"));
        _now = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        _retentionDays = 30;
        _budget = 50L * 1024 * 1024;
        _dropped = new List<(string, int, AbxrObserver.DropReason)>();
        AbxrObserver.ResetForTesting();
    }

    [TearDown]
    public void TearDown()
    {
        AbxrObserver.ResetForTesting();
        try { Directory.Delete(_root, true); } catch (DirectoryNotFoundException) { }
    }

    private DurableRecordQueue NewQueue()
    {
        var q = new DurableRecordQueue(_root, () => _now, () => _retentionDays, () => _budget);
        q.OnDropped = (s, n, r, _) => _dropped.Add((s, n, r));
        q.Load();
        return q;
    }

    private static QueuedRecord Event(string name) =>
        new QueuedRecord(QueuedKind.Event, new EventPayload { name = name, timestamp = "2026-10-29T12:00:00.000Z", meta = new Dictionary<string, string>(), recordId = Guid.NewGuid().ToString() });

    private static QueuedRecord Telemetry(string name) =>
        new QueuedRecord(QueuedKind.Telemetry, new TelemetryPayload { name = name, timestamp = "2026-10-29T12:00:00.000Z", meta = new Dictionary<string, string>(), recordId = Guid.NewGuid().ToString() });

    private static QueuedRecord Storage(string name) =>
        new QueuedRecord(QueuedKind.Storage, new StoragePayload { name = name, timestamp = "2026-10-29T12:00:00.000Z", data = new List<Dictionary<string, string>>(), recordId = Guid.NewGuid().ToString() });

    private static SavedSignIn SignIn(string sessionId, string app = "app-1", string org = "org-1") => new SavedSignIn
    {
        payload = new AuthPayload { sessionId = sessionId, deviceId = "device-1" },
        appKey = app,
        orgKey = org,
        instanceKey = ""
    };

    private static string NameOf(QueuedRecord r) => r.Payload switch
    {
        EventPayload e => e.name,
        TelemetryPayload t => t.name,
        StoragePayload s => s.name,
        LogPayload l => l.text,
        _ => null
    };

    // ── Line format ──────────────────────────────────────────

    [Test]
    public void Line_RoundTrips_EachKind_WithItsRecordId()
    {
        foreach (var record in new[] { Event("e"), Telemetry("t"), Storage("s"),
                     new QueuedRecord(QueuedKind.Log, new LogPayload { text = "l", logLevel = "info", recordId = "id-l" }) })
        {
            string line = DurableRecordQueue.ToLine(record);
            Assert.IsTrue(DurableRecordQueue.TryParseLine(line, out var back), line);
            Assert.AreEqual(record.Kind, back.Kind);
            Assert.AreEqual(record.RecordId, back.RecordId);
            Assert.AreEqual(NameOf(record), NameOf(back));
            StringAssert.DoesNotContain("ObserverId", line, "The observer id is per run and never written.");
        }
    }

    [Test]
    public void ReadLines_SkipsATornLastLine()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "torn.jsonl");
        string good = DurableRecordQueue.ToLine(Event("kept"));
        File.WriteAllText(path, good + "\n" + good.Substring(0, good.Length / 2), new UTF8Encoding(false));

        var lines = DurableRecordQueue.ReadLines(path, 0, 10);

        Assert.AreEqual(1, lines.Count, "The half-written line has no newline, so it isn't read.");
        Assert.AreEqual(Encoding.UTF8.GetByteCount(good) + 1, lines[0].end);
    }

    // ── Sessions ─────────────────────────────────────────────

    [Test]
    public void RecordsBeforeTheFirstSignIn_BelongToTheSessionItStarts()
    {
        var q = NewQueue();
        q.Add("provisional", Event("early"));
        q.SaveSignIn(SignIn("signed-in"));
        q.Add("signed-in", Event("later"));

        var chunk = q.PeekLive(RecordStream.Data, 10);

        Assert.AreEqual("signed-in", chunk.SessionId);
        CollectionAssert.AreEqual(new[] { "early", "later" }, chunk.Records.Select(NameOf));
        Assert.AreEqual(1, q.PendingSessions);
    }

    [Test]
    public void ANewSessionId_AfterASignIn_SealsTheLiveSession()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("first"));
        q.Add("first", Event("a"));
        q.Add("second", Event("b"));
        q.Flush();

        Assert.AreEqual(new[] { "first" }, q.SealedSessions().Select(s => s.SessionId).ToArray());
        Assert.AreEqual("second", q.LiveSession.SessionId);
    }

    [Test]
    public void Seal_KeepsAPendingSession_AndDeletesAnEmptyOne()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("empty"));
        q.SealLive();
        Assert.AreEqual(0, q.SealedSessions().Count, "Nothing to replay.");

        q.SaveSignIn(SignIn("full"));
        q.Add("full", Event("a"));
        q.SealLive();
        Assert.AreEqual(1, q.SealedSessions().Count);
        Assert.AreEqual(1, q.PendingRecords);
    }

    [Test]
    public void Load_SealsASessionTheAppWasKilledIn_AndKeepsItsRecords()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("killed"));
        q.Add("killed", Event("a"));
        q.Add("killed", Telemetry("t"));
        q.Flush();

        var reloaded = NewQueue();

        var session = reloaded.SealedSessions().Single();
        Assert.AreEqual("killed", session.SessionId);
        Assert.IsTrue(session.IsSealed);
        Assert.AreEqual(2, reloaded.PendingRecords);
        Assert.AreEqual("device-1", session.SignIn.payload.deviceId);
        CollectionAssert.AreEqual(new[] { "a", "t" }, reloaded.Peek(session, RecordStream.Data, 10).Records.Select(NameOf));
    }

    [Test]
    public void Load_LeavesANewerFormatAlone()
    {
        Directory.CreateDirectory(_root);
        string summary = Path.Combine(_root, "s-future.json");
        string records = Path.Combine(_root, "s-future.records.jsonl");
        File.WriteAllText(summary, "{\"formatVersion\":99,\"sessionId\":\"x\"}");
        File.WriteAllText(records, "{}\n");

        var q = NewQueue();

        Assert.AreEqual(0, q.PendingSessions, "A newer SDK's session isn't counted or sent.");
        Assert.IsTrue(File.Exists(summary) && File.Exists(records), "Nor deleted: a downgrade must not lose it.");
    }

    [Test]
    public void Load_MigratesAnOlderFormat()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "s-old.json"),
            "{\"sessionId\":\"old\",\"created\":\"2026-10-28T12:00:00Z\",\"records\":{\"acked\":0,\"count\":1,\"ackedCount\":0}}");
        File.WriteAllText(Path.Combine(_root, "s-old.records.jsonl"), DurableRecordQueue.ToLine(Event("a")) + "\n");

        var q = NewQueue();

        Assert.AreEqual("old", q.SealedSessions().Single().SessionId);
        StringAssert.Contains("\"formatVersion\":1", File.ReadAllText(Path.Combine(_root, "s-old.json")));
    }

    [Test]
    public void Load_DeletesRecordFilesWithNoSummary()
    {
        Directory.CreateDirectory(_root);
        string orphan = Path.Combine(_root, "s-orphan.records.jsonl");
        File.WriteAllText(orphan, DurableRecordQueue.ToLine(Event("a")) + "\n");

        NewQueue();

        Assert.IsFalse(File.Exists(orphan), "Without a summary nothing could sign these records in.");
    }

    // ── Acknowledgements ─────────────────────────────────────

    [Test]
    public void Ack_AdvancesPastTheChunk_AndTheNextPeekStartsAfterIt()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        foreach (var n in new[] { "a", "b", "c" }) q.Add("s", Event(n));

        var first = q.PeekLive(RecordStream.Data, 2);
        q.Ack(first);
        var second = q.PeekLive(RecordStream.Data, 2);

        CollectionAssert.AreEqual(new[] { "a", "b" }, first.Records.Select(NameOf));
        CollectionAssert.AreEqual(new[] { "c" }, second.Records.Select(NameOf));
        Assert.AreEqual(1, q.PendingLive(RecordStream.Data));
    }

    [Test]
    public void Ack_PersistsAcrossALaunch()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        foreach (var n in new[] { "a", "b", "c" }) q.Add("s", Event(n));
        q.Ack(q.PeekLive(RecordStream.Data, 2));

        var reloaded = NewQueue();

        var session = reloaded.SealedSessions().Single();
        CollectionAssert.AreEqual(new[] { "c" }, reloaded.Peek(session, RecordStream.Data, 10).Records.Select(NameOf));
        Assert.AreEqual(1, reloaded.PendingRecords);
    }

    [Test]
    public void Ack_OfOverlappingChunks_CountsEachRecordOnce()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        foreach (var n in new[] { "a", "b", "c" }) q.Add("s", Event(n));
        var outFirst = q.PeekLive(RecordStream.Data, 2); // a send still out...
        var quitFlush = q.PeekLive(RecordStream.Data, 3); // ...when quit sends from the same place

        q.Ack(quitFlush);
        q.Ack(outFirst);

        Assert.AreEqual(0, q.PendingRecords);
        Assert.IsNull(q.PeekLive(RecordStream.Data, 10));
    }

    [Test]
    public void Ack_OfAChunkReadBeforeTheFileWasEmptied_DoesNotSkipNewRecords()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        for (int i = 0; i < 20; i++) q.Add("s", Telemetry("old" + i));
        var outBeforeTheBudget = q.PeekLive(RecordStream.Data, 20);

        // The budget empties the live telemetry file while that chunk is out, and new telemetry follows.
        _budget = 1;
        q.Flush();
        _budget = 50L * 1024 * 1024;
        q.Flush(); // back under the budget, so records are taken again
        q.Add("s", Telemetry("new"));
        q.Flush();
        q.Ack(outBeforeTheBudget);

        CollectionAssert.AreEqual(new[] { "new" }, q.PeekLive(RecordStream.Data, 10).Records.Select(NameOf));
    }

    [Test]
    public void ASummaryAheadOfItsData_IsDiscarded()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("torn"));
        q.Add("torn", Event("a"));
        q.SealLive();
        var session = q.SealedSessions().Single();
        File.WriteAllText(Directory.GetFiles(_root, "s-*.records.jsonl").Single(), ""); // power lost before the data hit disk

        Assert.IsTrue(q.DiscardIfNothingToSend(session));
        Assert.AreEqual(0, q.PendingSessions);
    }

    [Test]
    public void Ack_OfTheLastRecords_DeletesASealedSession()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        q.Add("s", Event("a"));
        q.Add("s", Storage("st"));
        q.SealLive();
        var session = q.SealedSessions().Single();

        q.Ack(q.Peek(session, RecordStream.Data, 10));
        Assert.AreEqual(1, q.SealedSessions().Count, "Storage is still waiting.");
        q.Ack(q.Peek(session, RecordStream.Storage, 10));

        Assert.AreEqual(0, q.SealedSessions().Count);
        Assert.IsEmpty(Directory.GetFiles(_root, "s-*"));
    }

    [Test]
    public void Peek_ReadsRecordsBeforeTelemetry_AndStorageSeparately()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        q.Add("s", Telemetry("t1"));
        q.Add("s", Event("e1"));
        q.Add("s", Storage("s1"));

        CollectionAssert.AreEqual(new[] { "e1", "t1" }, q.PeekLive(RecordStream.Data, 10).Records.Select(NameOf));
        CollectionAssert.AreEqual(new[] { "s1" }, q.PeekLive(RecordStream.Storage, 10).Records.Select(NameOf));
    }

    [Test]
    public void ObserverIds_FollowTheRecords_ThroughTheDisk()
    {
        AbxrObserver.OnRecordsSent += _ => { };
        var q = NewQueue();
        var record = Event("observed");
        record.ObserverId = 42;
        q.Add(null, record);

        var chunk = q.PeekLive(RecordStream.Data, 10);

        Assert.AreEqual(42, chunk.Records.Single().ObserverId);
        CollectionAssert.AreEqual(new[] { 42L }, q.ClearAll());
    }

    [Test]
    public void ClearAll_DeletesEverySession_ButTheLiveOneKeepsItsSignIn()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("ended"));
        q.Add("ended", Event("a"));
        q.SealLive();
        q.SaveSignIn(SignIn("live"));
        q.Add("live", Event("b"));

        q.ClearAll();
        q.Add("live", Event("after"));
        q.SealLive();

        var kept = q.SealedSessions().Single();
        Assert.AreEqual("live", kept.SessionId, "ClearQueuedData doesn't end the session; what it records next still uploads.");
        Assert.IsNotNull(kept.SignIn);
        Assert.AreEqual(1, q.PendingRecords);
    }

    // ── Retention and budget ─────────────────────────────────

    [Test]
    public void Retention_DropsSessionsOlderThanTheLimit()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("old"));
        q.Add("old", Event("a"));
        q.SealLive();

        _now = _now.AddDays(31);
        NewQueue();

        Assert.AreEqual(1, _dropped.Count);
        Assert.AreEqual(("old", 1, AbxrObserver.DropReason.Retention), _dropped[0]);
    }

    [Test]
    public void Budget_DropsTelemetryFirst_ThenTheOldestSession()
    {
        var q = NewQueue();
        foreach (var id in new[] { "oldest", "newer" })
        {
            q.SaveSignIn(SignIn(id));
            for (int i = 0; i < 50; i++) { q.Add(id, Event("e" + i)); q.Add(id, Telemetry("t" + i)); }
            _now = _now.AddMinutes(1);
            q.SealLive();
        }
        long total = Directory.GetFiles(_root).Sum(f => new FileInfo(f).Length);
        long oneTelemetryFile = new FileInfo(Directory.GetFiles(_root, "s-*.telemetry.jsonl").First()).Length;

        // Room for everything except one telemetry file.
        _budget = total - oneTelemetryFile / 2;
        var reloaded = NewQueue();

        Assert.AreEqual(1, _dropped.Count);
        Assert.AreEqual("oldest", _dropped[0].session, "The oldest session's telemetry goes first.");
        Assert.AreEqual(50, _dropped[0].count);
        Assert.AreEqual(2, reloaded.PendingSessions, "Both sessions keep their events.");

        // Room for barely one session: the oldest goes whole.
        _dropped.Clear();
        _budget = total / 3;
        reloaded = NewQueue();
        Assert.IsTrue(_dropped.Any(d => d.session == "oldest" && d.reason == AbxrObserver.DropReason.Retention));
        Assert.AreEqual("newer", reloaded.SealedSessions().Single().SessionId);
    }

    [Test]
    public void Budget_WithOnlyTheLiveSessionLeft_RefusesNewRecords()
    {
        _budget = 2048;
        var q = NewQueue();
        q.SaveSignIn(SignIn("live"));
        int accepted = 0;
        for (int i = 0; i < 100; i++)
        {
            if (q.Add("live", Event("e" + i))) accepted++;
            q.Flush();
        }

        Assert.Less(accepted, 100, "Over the budget with nothing older to drop, new records are refused.");
        Assert.Greater(accepted, 0);
    }

    // ── Saved sign-in ────────────────────────────────────────

    [Test]
    public void SavedSignIn_HoldsNoCredentials()
    {
        var payload = new AuthPayload
        {
            appToken = "APP-TOKEN", orgToken = "ORG-TOKEN", appInstanceToken = "INSTANCE-TOKEN", authSecret = "SECRET",
            appId = "app", orgId = "org", SSOAccessToken = "SSO-TOKEN", sessionId = "s", deviceId = "d", deviceTitle = "Headset 7",
            authMechanism = new Dictionary<string, string> { ["type"] = "assessmentPin", ["prompt"] = "123456" }
        };

        var saved = SavedSignIn.WithoutCredentials(payload);
        // Every string the saved payload holds, as it's written to disk. (This assembly has no Newtonsoft.)
        var values = typeof(AuthPayload).GetFields().Select(f => f.GetValue(saved)).OfType<string>().ToList();

        foreach (var secret in new[] { "APP-TOKEN", "ORG-TOKEN", "INSTANCE-TOKEN", "SECRET", "SSO-TOKEN" })
            CollectionAssert.DoesNotContain(values, secret);
        Assert.IsNull(saved.authMechanism, "The PIN in the auth mechanism isn't saved.");
        Assert.IsNull(saved.appId);
        Assert.IsNull(saved.orgId);
        Assert.AreEqual("s", saved.sessionId);
        Assert.AreEqual("Headset 7", saved.deviceTitle);
        Assert.AreEqual("APP-TOKEN", payload.appToken, "The live payload is untouched.");
    }

    [Test]
    public void IdentityKeys_ComeFromTheTokensClaims_NotTheTokens()
    {
        string Jwt(string claims) => "eyJhbGciOiJIUzI1NiJ9." + Convert.ToBase64String(Encoding.UTF8.GetBytes(claims)).TrimEnd('=') + ".sig";
        var managed = new AuthPayload { appToken = Jwt("{\"appId\":\"app-9\"}"), orgToken = Jwt("{\"orgId\":\"org-3\"}"), deviceId = "d" };
        var paired = new AuthPayload { appToken = managed.appToken, appInstanceToken = "secret", deviceId = "instance-5" };

        Assert.AreEqual("app-9", SavedSignIn.AppKeyOf(managed));
        Assert.AreEqual("org-3", SavedSignIn.OrgKeyOf(managed));
        Assert.AreEqual("", SavedSignIn.InstanceKeyOf(managed));
        Assert.AreEqual("instance-5", SavedSignIn.InstanceKeyOf(paired));
        Assert.AreEqual("", SavedSignIn.OrgKeyOf(paired));
    }
}

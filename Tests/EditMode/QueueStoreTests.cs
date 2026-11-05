// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the durable queue's stores (SDK-72, offline RFC D13): the same queue behaviour over the file store
// (desktop and the Editor) and over RowQueueStore, an in-memory stand-in that works the way the Android SQLite store does
// (row ids as positions, acknowledged rows deleted, each tick's records and summary committed together or not at all).
// The SQLite store itself runs only on a headset; AbxrQueueDatabase's Java is exercised by the Android smoke build.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Services.Transport.Offline;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

public abstract class QueueStoreTests
{
    private protected DateTime Now;
    private readonly List<(string session, int count, AbxrObserver.DropReason reason)> _dropped = new List<(string, int, AbxrObserver.DropReason)>();

    /// <summary>The store a fresh queue opens: the same data across calls, as a relaunch would find it.</summary>
    private protected abstract IQueueStore OpenStore();

    [SetUp]
    public void SetUpQueueStore()
    {
        Now = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        _dropped.Clear();
        AbxrObserver.ResetForTesting();
    }

    private protected DurableRecordQueue NewQueue()
    {
        var q = new DurableRecordQueue(OpenStore(), () => Now, () => 30, () => 500L * 1024 * 1024);
        q.OnDropped = (s, n, r, _) => _dropped.Add((s, n, r));
        q.Load();
        return q;
    }

    private protected static QueuedRecord Event(string name) =>
        new QueuedRecord(QueuedKind.Event, new EventPayload { name = name, timestamp = "2026-11-05T12:00:00.000Z", meta = new Dictionary<string, string>(), recordId = Guid.NewGuid().ToString() });

    private protected static QueuedRecord Telemetry(string name) =>
        new QueuedRecord(QueuedKind.Telemetry, new TelemetryPayload { name = name, timestamp = "2026-11-05T12:00:00.000Z", meta = new Dictionary<string, string>(), recordId = Guid.NewGuid().ToString() });

    private protected static SavedSignIn SignIn(string sessionId) => new SavedSignIn
    {
        payload = new AuthPayload { sessionId = sessionId, deviceId = "device-1" },
        appKey = "app-1",
        orgKey = "org-1",
        instanceKey = ""
    };

    private protected static string NameOf(QueuedRecord r) => r.Payload switch
    {
        EventPayload e => e.name,
        TelemetryPayload t => t.name,
        _ => null
    };

    /// <summary>Sends a session's data stream chunk by chunk, as replay does, and returns the names in the order sent.</summary>
    private protected static List<string> DrainData(DurableRecordQueue q, StoredSession session, int chunkSize, int maxChunks = int.MaxValue)
    {
        var sent = new List<string>();
        for (int i = 0; i < maxChunks; i++)
        {
            var chunk = q.Peek(session, RecordStream.Data, chunkSize);
            if (chunk == null) break;
            sent.AddRange(chunk.Records.Select(NameOf));
            q.Ack(chunk);
        }
        return sent;
    }

    [Test]
    public void ASessionSurvivesARelaunch_WithItsRecordsInOrder()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        foreach (var n in new[] { "a", "b", "c" }) q.Add("s", Event(n));
        q.Flush();

        var reloaded = NewQueue();
        var session = reloaded.SealedSessions().Single();

        Assert.AreEqual("s", session.SessionId);
        Assert.AreEqual(3, reloaded.PendingRecords);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, DrainData(reloaded, session, 10));
        Assert.AreEqual(0, reloaded.PendingSessions, "Every record acknowledged: the session is deleted.");
        Assert.AreEqual(0, NewQueue().PendingSessions, "And it stays deleted.");
    }

    [Test]
    public void PagingALargeBacklog_SendsEveryRecordOnce_InOrder_AcrossARelaunch()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("big"));
        var events = Enumerable.Range(0, 3000).Select(i => "e" + i).ToList();
        var telemetry = Enumerable.Range(0, 500).Select(i => "t" + i).ToList();
        // Written over many ticks, as a long offline session would be.
        for (int i = 0; i < events.Count; i++)
        {
            q.Add("big", Event(events[i]));
            if (i < telemetry.Count) q.Add("big", Telemetry(telemetry[i]));
            if (i % 50 == 0) q.Flush();
        }
        q.SealLive();

        // Half of it uploads, then the app is closed and opened again.
        var first = DrainData(q, q.SealedSessions().Single(), 32, maxChunks: 50);
        var reloaded = NewQueue();
        var rest = DrainData(reloaded, reloaded.SealedSessions().Single(), 32);

        var sent = first.Concat(rest).ToList();
        Assert.AreEqual(events.Count + telemetry.Count, sent.Count, "Nothing lost, nothing sent twice.");
        CollectionAssert.AreEqual(events, sent.Where(n => n.StartsWith("e")).ToList());
        CollectionAssert.AreEqual(telemetry, sent.Where(n => n.StartsWith("t")).ToList());
        Assert.AreEqual(0, reloaded.PendingRecords);
        Assert.AreEqual(0, reloaded.PendingSessions);
    }

    [Test]
    public void AKill_KeepsWhatWasWritten_AndSealsTheSession()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("killed"));
        q.Add("killed", Event("written"));
        q.Flush();
        q.Add("killed", Event("still in memory"));
        // The app is killed here: nothing flushes or seals.

        var reloaded = NewQueue();
        var session = reloaded.SealedSessions().Single();

        Assert.IsTrue(session.IsSealed);
        CollectionAssert.AreEqual(new[] { "written" }, DrainData(reloaded, session, 10));
    }

    [Test]
    public void ARecordOverTheSizeCap_IsRefused()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        var big = new QueuedRecord(QueuedKind.Event, new EventPayload
        {
            name = "big",
            meta = new Dictionary<string, string> { ["blob"] = new string('x', DurableRecordQueue.MaxRecordChars) },
            recordId = Guid.NewGuid().ToString()
        });

        Assert.IsFalse(q.Add("s", big));
        Assert.IsTrue(q.Add("s", Event("small")));
        Assert.AreEqual(1, q.PendingRecords);
    }
}

[TestFixture]
public class FileQueueStoreTests : QueueStoreTests
{
    private string _root;

    [SetUp]
    public void SetUpFiles() => _root = Path.Combine(Path.GetTempPath(), "abxrlib-store-tests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDownFiles()
    {
        try { Directory.Delete(_root, true); } catch (DirectoryNotFoundException) { }
    }

    private protected override IQueueStore OpenStore() => new FileQueueStore(_root);

    [Test]
    public void AKillMidLine_LosesOnlyThatLine()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("torn"));
        q.Add("torn", Event("a"));
        q.Add("torn", Event("b"));
        q.Flush();
        // Killed while appending: half a line, no newline.
        string line = DurableRecordQueue.ToLine(Event("c"));
        File.AppendAllText(Directory.GetFiles(_root, "*.records.jsonl").Single(), line.Substring(0, line.Length / 2));

        var reloaded = NewQueue();

        Assert.AreEqual(2, reloaded.PendingRecords);
        CollectionAssert.AreEqual(new[] { "a", "b" }, DrainData(reloaded, reloaded.SealedSessions().Single(), 10));
    }
}

[TestFixture]
public class RowQueueStoreTests : QueueStoreTests
{
    private RowQueueStore _store;

    [SetUp]
    public void SetUpRows() => _store = new RowQueueStore();

    private protected override IQueueStore OpenStore() => _store;

    [Test]
    public void AFailedWrite_StoresNothing_AndTheRecordsGoInOnceOnTheNextTick()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        q.Add("s", Event("a"));
        q.Add("s", Telemetry("t"));
        _store.FailNextAppend = true;

        q.Flush();
        Assert.AreEqual(0, _store.RowCount, "The transaction rolled back: no record is half there.");

        q.Flush();
        Assert.AreEqual(2, _store.RowCount);
        var reloaded = NewQueue();
        CollectionAssert.AreEquivalent(new[] { "a", "t" }, DrainData(reloaded, reloaded.SealedSessions().Single(), 10));
    }

    [Test]
    public void AcknowledgedRecords_AreDeletedAsTheyAreAnswered()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        foreach (var n in new[] { "a", "b", "c", "d" }) q.Add("s", Event(n));
        q.SealLive();
        var session = q.SealedSessions().Single();

        q.Ack(q.Peek(session, RecordStream.Data, 3));

        Assert.AreEqual(1, _store.RowCount);
        CollectionAssert.AreEqual(new[] { "d" }, DrainData(q, session, 10));
    }

    [Test]
    public void TheSummary_IsWrittenWithTheRecords()
    {
        var q = NewQueue();
        q.SaveSignIn(SignIn("s"));
        int summaryWrites = _store.SummaryWrites;
        q.Add("s", Event("a"));

        q.Flush();

        Assert.AreEqual(summaryWrites, _store.SummaryWrites, "No separate summary write: it went in the append's transaction.");
        StringAssert.Contains("\"count\":1", _store.SummaryOf(q.LiveSession.Summary.fileKey));
    }
}

/// <summary>
/// An in-memory store that behaves like SqliteQueueStore: positions are row ids that only grow, acknowledged rows are
/// deleted, an append and its summary commit together or not at all, and a read stops early once a page is full.
/// </summary>
internal sealed class RowQueueStore : IQueueStore
{
    private const int PageChars = 2000;
    private readonly Dictionary<string, string> _summaries = new Dictionary<string, string>();
    private readonly List<(long id, string key, int stream, string line)> _rows = new List<(long, string, int, string)>();
    private long _nextId = 1;

    public bool FailNextAppend;
    public int SummaryWrites;
    public int RowCount => _rows.Count;
    public string SummaryOf(string key) => _summaries[key];

    public List<(string key, string json)> ReadSummaries() => _summaries.Select(kv => (kv.Key, kv.Value)).ToList();

    public void DeleteOrphans() => _rows.RemoveAll(r => !_summaries.ContainsKey(r.key));

    public int CountAfter(string key, QueueFile file, long after) => _rows.Count(r => r.key == key && r.stream == (int)file && r.id > after);

    public bool Append(string key, IReadOnlyList<string>[] lines, string summaryJson, Action<QueueFile, long> committed)
    {
        if (FailNextAppend)
        {
            FailNextAppend = false;
            throw new IOException("disk I/O error (simulated)");
        }
        var added = new long[3];
        foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            foreach (var line in lines[(int)f] ?? new List<string>())
            {
                _rows.Add((_nextId++, key, (int)f, line));
                added[(int)f] += line.Length;
            }
        if (!string.IsNullOrEmpty(summaryJson)) _summaries[key] = summaryJson;
        foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            if ((lines[(int)f]?.Count ?? 0) > 0) committed(f, added[(int)f]);
        return !string.IsNullOrEmpty(summaryJson);
    }

    public List<(string line, long position)> Read(string key, QueueFile file, long after, int max)
    {
        var result = new List<(string, long)>();
        long chars = 0;
        foreach (var r in _rows.Where(r => r.key == key && r.stream == (int)file && r.id > after).OrderBy(r => r.id).Take(max))
        {
            if (result.Count > 0 && chars + r.line.Length > PageChars) break;
            result.Add((r.line, r.id));
            chars += r.line.Length;
        }
        return result;
    }

    public long WriteSummary(string key, string json, bool durable)
    {
        SummaryWrites++;
        long before = _summaries.TryGetValue(key, out var old) ? old.Length : 0;
        _summaries[key] = json;
        return json.Length - before;
    }

    public long Acknowledged(string key, QueueFile file, long through)
    {
        long freed = _rows.Where(r => r.key == key && r.stream == (int)file && r.id <= through).Sum(r => (long)r.line.Length);
        _rows.RemoveAll(r => r.key == key && r.stream == (int)file && r.id <= through);
        return freed;
    }

    public long TryCompact(string key, QueueFile file, long acked) => -1;

    public long DeleteStream(string key, QueueFile file)
    {
        long freed = StreamSize(key, file);
        _rows.RemoveAll(r => r.key == key && r.stream == (int)file);
        return freed;
    }

    public long DeleteSession(string key)
    {
        long freed = SummarySize(key) + _rows.Where(r => r.key == key).Sum(r => (long)r.line.Length);
        _rows.RemoveAll(r => r.key == key);
        _summaries.Remove(key);
        return freed;
    }

    public long StreamSize(string key, QueueFile file) => _rows.Where(r => r.key == key && r.stream == (int)file).Sum(r => (long)r.line.Length);

    public long SummarySize(string key) => _summaries.TryGetValue(key, out var s) ? s.Length : 0;
}

// The on-disk queue is for headsets and desktops. WebGL stays online-only and keeps the in-memory queue (MemoryRecordQueue).
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>One of a session's three record streams.</summary>
    internal enum QueueFile
    {
        /// <summary>Events and logs.</summary>
        Records,
        Telemetry,
        Storage
    }

    /// <summary>How much of one record stream the backend has answered for.</summary>
    [Serializable]
    internal sealed class QueueFileState
    {
        /// <summary>Position (a byte offset in a file, a row id in SQLite) up to which every record has been accepted or permanently refused.</summary>
        public long acked;
        /// <summary>Records written and records acknowledged, for upload status.</summary>
        public int count;
        public int ackedCount;

        [JsonIgnore] public int Pending => Math.Max(0, count - ackedCount);
        /// <summary>Bumped when the stream is emptied (compaction, the disk budget), so a chunk read before can't acknowledge positions of the new contents.</summary>
        [JsonIgnore] public int epoch;

        internal void Reset()
        {
            acked = 0;
            count = 0;
            ackedCount = 0;
            epoch++;
        }
    }

    /// <summary>A session's summary: everything needed to sign it in again and to find where its upload got to.</summary>
    [Serializable]
    internal sealed class SessionSummary
    {
        internal const int CurrentFormat = 1;

        public int formatVersion = CurrentFormat;
        public string sessionId;
        /// <summary>Names the session in the store. Random, so a session ID the app set can't make a bad file name.</summary>
        public string fileKey;
        public DateTime created;
        public DateTime? sealedAt;
        public SavedSignIn signIn;
        public QueueFileState records = new QueueFileState();
        public QueueFileState telemetry = new QueueFileState();
        public QueueFileState storage = new QueueFileState();
        /// <summary>Launches whose replay sign-in the backend refused. The session is dropped at three.</summary>
        public int failedReplays;

        internal QueueFileState State(QueueFile file) => file switch
        {
            QueueFile.Records => records,
            QueueFile.Telemetry => telemetry,
            _ => storage
        };
    }

    /// <summary>A session the queue knows about, with the records the live one hasn't written yet.</summary>
    internal sealed class StoredSession
    {
        public SessionSummary Summary;
        internal readonly List<string>[] Buffers = { new List<string>(), new List<string>(), new List<string>() };
        internal bool SummaryDirty;

        public string SessionId => Summary.sessionId;
        public bool IsSealed => Summary.sealedAt.HasValue;
        public SavedSignIn SignIn => Summary.signIn;
        public int Pending => Summary.records.Pending + Summary.telemetry.Pending + Summary.storage.Pending + Buffered(QueueFile.Records)
                              + Buffered(QueueFile.Telemetry) + Buffered(QueueFile.Storage);
        internal int Buffered(QueueFile file) => Buffers[(int)file].Count;
        internal bool HasBuffered => Buffers[0].Count + Buffers[1].Count + Buffers[2].Count > 0;
    }

    /// <summary>
    /// The durable queue (offline RFC, SDK section). Each session has a summary and three record streams (events and logs,
    /// telemetry, storage) in an <see cref="IQueueStore"/>: SQLite on Android headsets, append-only files on desktop and in
    /// the Editor (D13). Records are buffered in memory and written once per transport tick; the backend's answers move a
    /// position per stream, and a sealed session is deleted once every stream is acknowledged. Start-up reads only the
    /// summaries; records are read back a chunk at a time when sent. Main thread only.
    /// </summary>
    internal sealed class DurableRecordQueue : IRecordQueue
    {
        internal const int MaxFailedReplays = 3;
        /// <summary>
        /// Largest record the queue keeps, in UTF-16 characters (about the same in bytes for this JSON). One SQLite query
        /// result has to fit Android's 2 MB CursorWindow, so a record never comes near it.
        /// </summary>
        internal const int MaxRecordChars = 512 * 1024;

        private readonly IQueueStore _store;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<int> _retentionDays;
        private readonly Func<long> _budgetBytes;
        /// <summary>Oldest first.</summary>
        private readonly List<StoredSession> _sessions = new List<StoredSession>();
        /// <summary>Observer ids of this run's records by record UUID. The ids aren't written to disk.</summary>
        private readonly Dictionary<string, long> _observerIds = new Dictionary<string, long>();
        private StoredSession _live;
        private long _diskBytes;
        private bool _overBudget;
        private bool _warnedWriteFailure;
        /// <summary>True while appends fail (a full disk), so records held in memory are capped like 3.0's lists.</summary>
        private bool _writeFailing;
        private readonly Func<int> _maxBufferedWhileWriteFails;
        private bool _warnedOverBudget;
        private bool _warnedTooLarge;

        /// <summary>Raised when records are dropped without being sent: (session, record count, reason, observer ids of this run's records).</summary>
        internal Action<string, int, AbxrObserver.DropReason, List<long>> OnDropped;

        public bool IsDurable => true;

        internal IQueueStore Store => _store;

        internal DurableRecordQueue(string root, Func<DateTime> utcNow = null, Func<int> retentionDays = null, Func<long> budgetBytes = null,
            Func<int> maxBufferedWhileWriteFails = null)
            : this(new FileQueueStore(root), utcNow, retentionDays, budgetBytes, maxBufferedWhileWriteFails)
        {
        }

        internal DurableRecordQueue(IQueueStore store, Func<DateTime> utcNow = null, Func<int> retentionDays = null, Func<long> budgetBytes = null,
            Func<int> maxBufferedWhileWriteFails = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _retentionDays = retentionDays ?? (() => Configuration.Instance.offlineRetentionDays);
            _budgetBytes = budgetBytes ?? (() => Configuration.Instance.offlineDiskBudgetMB * 1024L * 1024L);
            _maxBufferedWhileWriteFails = maxBufferedWhileWriteFails ?? (() => Configuration.Instance.maximumCachedItems);
        }

        /// <summary>The default folder, persistentDataPath/abxrlib/queue: the file store's root, and where the offline settings cache lives.</summary>
        internal static string DefaultRoot => Path.Combine(UnityEngine.Application.persistentDataPath, "abxrlib", "queue");

        // ── Start-up ─────────────────────────────────────────────

        /// <summary>
        /// Reads every summary. A session left unsealed (the app was killed) is sealed now, and its counts are taken from
        /// the records actually stored. A summary of a newer format is left alone; an older one is migrated. Records with no
        /// readable summary are deleted, since nothing could sign them in. Then retention and the disk budget are enforced.
        /// </summary>
        internal void Load()
        {
            _sessions.Clear();
            _live = null;
            _diskBytes = 0;
            try
            {
                foreach (var (key, json) in _store.ReadSummaries())
                {
                    SessionSummary summary = null;
                    try
                    {
                        var version = JObject.Parse(json).Value<int?>("formatVersion") ?? 0;
                        // A newer SDK wrote it (a downgrade): leave it for that SDK, and don't count it here.
                        if (version > SessionSummary.CurrentFormat) continue;
                        summary = JsonConvert.DeserializeObject<SessionSummary>(json);
                    }
                    catch (Exception ex) { Logcat.Warning($"Dropped an unreadable offline session summary: {ex.Message}"); }

                    if (summary == null || string.IsNullOrEmpty(summary.sessionId))
                    {
                        _store.DeleteSession(key);
                        continue;
                    }
                    summary.fileKey = key;
                    var session = new StoredSession { Summary = summary };
                    if (summary.formatVersion < SessionSummary.CurrentFormat)
                    {
                        summary.formatVersion = SessionSummary.CurrentFormat;
                        session.SummaryDirty = true;
                    }
                    if (!summary.sealedAt.HasValue)
                    {
                        summary.sealedAt = _utcNow();
                        session.SummaryDirty = true;
                    }
                    // The store is the truth: a kill between an append and its summary write leaves the count behind,
                    // and a session whose count says it's done would be deleted with records still unsent.
                    foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                    {
                        var state = summary.State(f);
                        int count = state.ackedCount + _store.CountAfter(key, f, state.acked);
                        if (count != state.count) { state.count = count; session.SummaryDirty = true; }
                    }
                    _sessions.Add(session);
                    _diskBytes += _store.SummarySize(key);
                    foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                        _diskBytes += _store.StreamSize(key, f);
                }
                _store.DeleteOrphans();
            }
            catch (Exception ex)
            {
                Logcat.Error($"Couldn't read the offline queue: {ex.Message}");
            }
            _sessions.Sort((a, b) => a.Summary.created.CompareTo(b.Summary.created));
            foreach (var s in _sessions) WriteSummaryIfDirty(s);
            DeleteFinished();
            foreach (var s in _sessions.ToList()) DropIfNeverSignedIn(s);
            EnforceRetention();
            EnforceBudget();
        }

        /// <summary>Drops sessions older than the retention period (offline RFC D3), as Dropped(Retention).</summary>
        internal void EnforceRetention()
        {
            int days = Math.Max(1, _retentionDays());
            var cutoff = _utcNow().AddDays(-days);
            foreach (var s in _sessions.ToList())
            {
                if (s == _live || s.Summary.created >= cutoff) continue;
                Logcat.Info($"Dropped offline session {s.SessionId}: it's older than {days} days.");
                Drop(s, AbxrObserver.DropReason.Retention);
            }
        }

        // ── IRecordQueue ─────────────────────────────────────────

        public bool Add(string sessionId, QueuedRecord record)
        {
            if (_overBudget)
            {
                if (!_warnedOverBudget)
                {
                    _warnedOverBudget = true;
                    Logcat.Warning("The offline queue is at its disk budget (Offline Disk Budget MB). New records are dropped until it uploads.");
                }
                return false;
            }
            string line = ToLine(record);
            if (line.Length > MaxRecordChars)
            {
                if (!_warnedTooLarge)
                {
                    _warnedTooLarge = true;
                    Logcat.Warning($"A record of {line.Length / 1024} KB is over the offline queue's {MaxRecordChars / 1024} KB limit per record, so it was dropped.");
                }
                return false;
            }
            var session = LiveFor(sessionId);
            var file = FileOf(record.Kind);
            // Records can't reach disk, so nothing reads them back to send either; hold no more than 3.0's queue did.
            int max = _writeFailing ? _maxBufferedWhileWriteFails() : 0;
            if (max > 0 && session.Buffered(file) >= max) return false;
            session.Buffers[(int)file].Add(line);
            long observerId = record.ObserverId;
            string recordId = record.RecordId;
            if (observerId != 0 && !string.IsNullOrEmpty(recordId)) _observerIds[recordId] = observerId;
            return true;
        }

        public int PendingLive(RecordStream stream)
        {
            if (_live == null) return 0;
            var s = _live.Summary;
            return stream == RecordStream.Storage
                ? s.storage.Pending + _live.Buffered(QueueFile.Storage)
                : s.records.Pending + s.telemetry.Pending + _live.Buffered(QueueFile.Records) + _live.Buffered(QueueFile.Telemetry);
        }

        public RecordChunk PeekLive(RecordStream stream, int max) => _live == null ? null : Peek(_live, stream, max);

        public void Ack(RecordChunk chunk)
        {
            if (!(chunk?.End is ChunkEnd end)) return;
            foreach (var r in chunk.Records)
            {
                string id = r.RecordId;
                if (id != null) _observerIds.Remove(id);
            }
            var session = _sessions.FirstOrDefault(s => s.Summary.fileKey == end.FileKey);
            if (session == null) return; // dropped or cleared while the chunk was out
            foreach (var part in end.Parts)
            {
                var state = session.Summary.State(part.File);
                if (part.Epoch != state.epoch) continue; // the stream was emptied since; those records went with it
                int newlyAcked = part.Positions.Count(e => e > state.acked);
                if (newlyAcked == 0) continue;
                state.acked = Math.Max(state.acked, part.Positions[part.Positions.Count - 1]);
                state.ackedCount = Math.Min(state.count, state.ackedCount + newlyAcked);
                session.SummaryDirty = true;
                try { _diskBytes -= _store.Acknowledged(session.Summary.fileKey, part.File, state.acked); }
                catch (Exception ex) { Logcat.Debug($"Couldn't remove acknowledged offline records: {ex.Message}"); }
            }
            if (session == _live) CompactLive();
            WriteSummaryIfDirty(session);
            if (session.IsSealed && session.Pending == 0) Delete(session);
            if (_overBudget && _diskBytes <= _budgetBytes()) { _overBudget = false; _warnedOverBudget = false; }
        }

        public void Flush()
        {
            foreach (var session in _sessions)
            {
                if (session.HasBuffered) FlushSession(session);
                WriteSummaryIfDirty(session);
            }
            EnforceBudget();
        }

        public List<long> SealLive()
        {
            if (_live == null) return null;
            var session = _live;
            _live = null;
            FlushSession(session);
            session.Summary.sealedAt = _utcNow();
            session.SummaryDirty = true;
            WriteSummaryIfDirty(session, durable: true);
            // Nothing left to send: there's nothing to replay either.
            if (session.Pending == 0) Delete(session);
            else DropIfNeverSignedIn(session);
            return null;
        }

        /// <summary>
        /// A session nothing signed in for (auth never started: unpaired with no prompt, or ended or killed first) can't
        /// be signed in again, so it's dropped now rather than counted as waiting to upload. True when it was dropped.
        /// </summary>
        private bool DropIfNeverSignedIn(StoredSession session)
        {
            if (session.SignIn?.payload != null) return false;
            Logcat.Info($"Dropped offline session {session.SessionId}: it never signed in, so it can't be signed in again.");
            Drop(session, AbxrObserver.DropReason.IdentityChanged);
            return true;
        }

        public List<long> ClearAll()
        {
            var ids = AbxrObserver.HasSendHandler ? _observerIds.Values.Where(id => id != 0).ToList() : null;
            _observerIds.Clear();
            // The live session can go on recording (ClearQueuedData doesn't end it), so its sign-in stays.
            var liveSignIn = _live?.SignIn;
            foreach (var s in _sessions.ToList()) Delete(s);
            _live = null;
            _overBudget = false;
            _warnedOverBudget = false;
            if (liveSignIn != null) SaveSignIn(liveSignIn);
            return ids;
        }

        public void SaveSignIn(SavedSignIn signIn)
        {
            if (signIn?.payload == null || string.IsNullOrEmpty(signIn.payload.sessionId)) return;
            var session = LiveFor(signIn.payload.sessionId);
            session.Summary.signIn = signIn;
            session.SummaryDirty = true;
            WriteSummaryIfDirty(session);
        }

        public int PendingRecords => _sessions.Sum(s => s.Pending);

        public int PendingSessions => _sessions.Count(s => s.Pending > 0);

        public List<QueuedRecord> PendingLiveForTesting()
        {
            if (_live == null) return new List<QueuedRecord>();
            FlushSession(_live);
            var all = new List<QueuedRecord>();
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                foreach (var (line, _) in _store.Read(_live.Summary.fileKey, f, _live.Summary.State(f).acked, int.MaxValue))
                    if (TryParseLine(line, out var r)) all.Add(r);
            return all;
        }

        // ── Replay support ───────────────────────────────────────

        /// <summary>Sealed sessions with something left to send, oldest first.</summary>
        internal List<StoredSession> SealedSessions() => _sessions.Where(s => s.IsSealed && s != _live).ToList();

        internal StoredSession LiveSession => _live;

        /// <summary>The oldest unacknowledged records of a session's stream, up to max. Null when there are none.</summary>
        internal RecordChunk Peek(StoredSession session, RecordStream stream, int max)
        {
            if (max <= 0 || !_sessions.Contains(session)) return null;
            if (session == _live) FlushSession(session);
            var chunk = new RecordChunk { SessionId = session.SessionId, Stream = stream };
            var end = new ChunkEnd { FileKey = session.Summary.fileKey };
            var files = stream == RecordStream.Storage ? new[] { QueueFile.Storage } : new[] { QueueFile.Records, QueueFile.Telemetry };
            foreach (var file in files)
            {
                int room = max - chunk.Records.Count;
                if (room <= 0) break;
                var lines = _store.Read(session.Summary.fileKey, file, session.Summary.State(file).acked, room);
                if (lines.Count == 0) continue;
                var part = new ChunkPart { File = file, Epoch = session.Summary.State(file).epoch };
                foreach (var (line, position) in lines)
                {
                    part.Positions.Add(position);
                    // An unreadable record (a write the app was killed in the middle of) is skipped, but still acknowledged.
                    if (!TryParseLine(line, out var record)) continue;
                    string id = record.RecordId;
                    if (id != null && _observerIds.TryGetValue(id, out long observerId)) record.ObserverId = observerId;
                    chunk.Records.Add(record);
                }
                end.Parts.Add(part);
            }
            if (end.Parts.Count == 0) return null;
            chunk.End = end;
            return chunk;
        }

        /// <summary>
        /// Deletes a sealed session that says it has records but has none to read (a summary written ahead of its data
        /// when power was lost, or deleted records), so a replay doesn't sign it in over and over. True when deleted.
        /// </summary>
        internal bool DiscardIfNothingToSend(StoredSession session)
        {
            if (!_sessions.Contains(session)) return true;
            if (session == _live || Peek(session, RecordStream.Data, 1) != null || Peek(session, RecordStream.Storage, 1) != null) return false;
            Logcat.Warning($"Dropped offline session {session.SessionId}: it has no records left to read.");
            Delete(session);
            return true;
        }

        internal void MarkReplayFailed(StoredSession session)
        {
            session.Summary.failedReplays++;
            session.SummaryDirty = true;
            WriteSummaryIfDirty(session);
        }

        /// <summary>Deletes a session and reports its unsent records dropped.</summary>
        internal void Drop(StoredSession session, AbxrObserver.DropReason reason)
        {
            int pending = session.Pending;
            Delete(session);
            if (session == _live) _live = null;
            if (pending > 0) OnDropped?.Invoke(session.SessionId, pending, reason, null);
        }

        // ── Internals ────────────────────────────────────────────

        private sealed class ChunkPart
        {
            public QueueFile File;
            public int Epoch;
            /// <summary>Each record's position, so an acknowledgement that overlaps an earlier one counts each record once.</summary>
            public readonly List<long> Positions = new List<long>();
        }

        private sealed class ChunkEnd
        {
            public string FileKey;
            public readonly List<ChunkPart> Parts = new List<ChunkPart>();
        }

        private static QueueFile FileOf(QueuedKind kind) => kind switch
        {
            QueuedKind.Telemetry => QueueFile.Telemetry,
            QueuedKind.Storage => QueueFile.Storage,
            _ => QueueFile.Records
        };

        /// <summary>The live session for a record or sign-in under this session ID, opening or replacing it as needed.</summary>
        private StoredSession LiveFor(string sessionId)
        {
            if (_live != null)
            {
                if (string.IsNullOrEmpty(sessionId) || _live.SessionId == sessionId) return _live;
                // Records from before anything signed in (the first frames, waiting for the ArborXR client) belong to the
                // session the first sign-in starts, as they did when they waited in memory.
                if (_live.SignIn == null)
                {
                    _live.Summary.sessionId = sessionId;
                    _live.SummaryDirty = true;
                    return _live;
                }
                SealLive();
            }
            _live = new StoredSession
            {
                Summary = new SessionSummary
                {
                    sessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString() : sessionId,
                    fileKey = Guid.NewGuid().ToString("N"),
                    created = _utcNow()
                },
                SummaryDirty = true
            };
            _sessions.Add(_live);
            return _live;
        }

        private void FlushSession(StoredSession session)
        {
            if (!session.HasBuffered) return;
            var summary = session.Summary;
            try
            {
                // The summary as it reads once the records are in, for a store that writes both at once (SQLite).
                foreach (QueueFile f in Enum.GetValues(typeof(QueueFile))) summary.State(f).count += session.Buffered(f);
                string summaryAfter = JsonConvert.SerializeObject(summary);
                foreach (QueueFile f in Enum.GetValues(typeof(QueueFile))) summary.State(f).count -= session.Buffered(f);

                bool summaryWritten = _store.Append(summary.fileKey, session.Buffers, summaryAfter, (f, bytes) =>
                {
                    _diskBytes += bytes;
                    summary.State(f).count += session.Buffered(f);
                    session.Buffers[(int)f].Clear();
                    session.SummaryDirty = true;
                });
                if (summaryWritten) session.SummaryDirty = false;
                _warnedWriteFailure = false;
                _writeFailing = false;
            }
            catch (Exception ex)
            {
                // Kept in memory; the next tick tries again.
                _writeFailing = true;
                if (!_warnedWriteFailure)
                {
                    _warnedWriteFailure = true;
                    Logcat.Warning($"Couldn't write the offline queue ({ex.Message}). Records stay in memory and the SDK tries again.");
                }
            }
            WriteSummaryIfDirty(session);
        }

        /// <summary>Empties a live stream the backend has answered for in full once it's large, so a long online session doesn't grow on disk.</summary>
        private void CompactLive()
        {
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            {
                if (_live.Buffered(f) > 0) continue;
                var state = _live.Summary.State(f);
                try
                {
                    long freed = _store.TryCompact(_live.Summary.fileKey, f, state.acked);
                    if (freed < 0) continue;
                    _diskBytes -= freed;
                    state.Reset();
                    _live.SummaryDirty = true;
                }
                catch (Exception ex) { Logcat.Debug($"Couldn't compact an offline queue stream: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Keeps the queue within its disk budget (offline RFC D3): telemetry goes first, oldest session first, then
        /// whole sessions, oldest first. The live session's records are never deleted; when only they are left over the
        /// budget, new records are refused until uploads make room.
        /// </summary>
        private void EnforceBudget()
        {
            long budget = _budgetBytes();
            if (_diskBytes <= budget) { _overBudget = false; return; }
            foreach (var s in _sessions.Where(s => s != _live).Concat(_live != null ? new[] { _live } : Array.Empty<StoredSession>()).ToList())
            {
                if (_diskBytes <= budget) break;
                var state = s.Summary.telemetry;
                try
                {
                    if (_store.StreamSize(s.Summary.fileKey, QueueFile.Telemetry) == 0) continue;
                    int dropped = state.Pending;
                    _diskBytes -= _store.DeleteStream(s.Summary.fileKey, QueueFile.Telemetry);
                    state.Reset();
                    s.SummaryDirty = true;
                    WriteSummaryIfDirty(s);
                    Logcat.Warning($"The offline queue is over its disk budget, so it dropped {dropped} telemetry records of session {s.SessionId}.");
                    if (dropped > 0) OnDropped?.Invoke(s.SessionId, dropped, AbxrObserver.DropReason.Retention, null);
                }
                catch (Exception ex) { Logcat.Debug($"Couldn't drop offline telemetry: {ex.Message}"); }
            }
            foreach (var s in _sessions.Where(s => s != _live).ToList())
            {
                if (_diskBytes <= budget) break;
                Logcat.Warning($"The offline queue is over its disk budget, so it dropped session {s.SessionId}.");
                Drop(s, AbxrObserver.DropReason.Retention);
            }
            _overBudget = _diskBytes > budget;
        }

        /// <summary>Deletes sealed sessions with nothing left to send.</summary>
        private void DeleteFinished()
        {
            foreach (var s in _sessions.Where(s => s.IsSealed && s.Pending == 0).ToList()) Delete(s);
        }

        private void Delete(StoredSession session)
        {
            _sessions.Remove(session);
            try { _diskBytes -= _store.DeleteSession(session.Summary.fileKey); }
            catch (Exception ex) { Logcat.Debug($"Couldn't delete offline session {session.SessionId}: {ex.Message}"); }
            if (_diskBytes < 0) _diskBytes = 0;
        }

        /// <summary>Writes the summary when it changed. durable forces it to the device first (sealing), which costs milliseconds, so routine writes skip it: Load recounts the records.</summary>
        private void WriteSummaryIfDirty(StoredSession session, bool durable = false)
        {
            if (!session.SummaryDirty || !_sessions.Contains(session)) return;
            try
            {
                _diskBytes += _store.WriteSummary(session.Summary.fileKey, JsonConvert.SerializeObject(session.Summary), durable);
                session.SummaryDirty = false;
            }
            catch (Exception ex)
            {
                if (!_warnedWriteFailure)
                {
                    _warnedWriteFailure = true;
                    Logcat.Warning($"Couldn't write an offline session summary ({ex.Message}). The SDK tries again.");
                }
            }
        }

        // ── Record format: {"k":"e|l|t|s","r":<payload>} ─────────

        internal static string ToLine(QueuedRecord record)
        {
            char k = record.Kind switch
            {
                QueuedKind.Event => 'e',
                QueuedKind.Log => 'l',
                QueuedKind.Telemetry => 't',
                _ => 's'
            };
            return "{\"k\":\"" + k + "\",\"r\":" + JsonConvert.SerializeObject(record.Payload) + "}";
        }

        internal static bool TryParseLine(string line, out QueuedRecord record)
        {
            record = null;
            try
            {
                var obj = JObject.Parse(line);
                var r = obj["r"];
                if (r == null || r.Type != JTokenType.Object) return false;
                switch (obj.Value<string>("k"))
                {
                    case "e": record = new QueuedRecord(QueuedKind.Event, r.ToObject<EventPayload>()); break;
                    case "l": record = new QueuedRecord(QueuedKind.Log, r.ToObject<LogPayload>()); break;
                    case "t": record = new QueuedRecord(QueuedKind.Telemetry, r.ToObject<TelemetryPayload>()); break;
                    case "s": record = new QueuedRecord(QueuedKind.Storage, r.ToObject<StoragePayload>()); break;
                    default: return false;
                }
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
#endif

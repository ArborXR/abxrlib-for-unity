// The on-disk queue is for headsets and desktops. WebGL stays online-only and keeps the in-memory queue (MemoryRecordQueue).
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>One of a session's three record files.</summary>
    internal enum QueueFile
    {
        /// <summary>Events and logs.</summary>
        Records,
        Telemetry,
        Storage
    }

    /// <summary>How much of one record file the backend has answered for.</summary>
    [Serializable]
    internal sealed class QueueFileState
    {
        /// <summary>Byte offset up to which every record has been accepted or permanently refused.</summary>
        public long acked;
        /// <summary>Lines written and lines acknowledged, for upload status.</summary>
        public int count;
        public int ackedCount;

        [JsonIgnore] public int Pending => Math.Max(0, count - ackedCount);
        /// <summary>Bumped when the file is emptied (compaction, the disk budget), so a chunk read before can't acknowledge offsets of the new contents.</summary>
        [JsonIgnore] public int epoch;

        internal void Reset()
        {
            acked = 0;
            count = 0;
            ackedCount = 0;
            epoch++;
        }
    }

    /// <summary>A session's summary file: everything needed to sign it in again and to find where its upload got to.</summary>
    [Serializable]
    internal sealed class SessionSummary
    {
        internal const int CurrentFormat = 1;

        public int formatVersion = CurrentFormat;
        public string sessionId;
        /// <summary>Names the session's files. Random, so a session ID the app set can't make a bad file name.</summary>
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
        internal readonly StringBuilder[] Buffers = { new StringBuilder(), new StringBuilder(), new StringBuilder() };
        internal readonly int[] BufferedCounts = new int[3];
        internal bool SummaryDirty;

        public string SessionId => Summary.sessionId;
        public bool IsSealed => Summary.sealedAt.HasValue;
        public SavedSignIn SignIn => Summary.signIn;
        public int Pending => Summary.records.Pending + Summary.telemetry.Pending + Summary.storage.Pending + BufferedCounts.Sum();
        internal bool HasBuffered => BufferedCounts[0] + BufferedCounts[1] + BufferedCounts[2] > 0;
    }

    /// <summary>
    /// The durable queue (offline RFC, SDK section). Each session is a summary file plus three append-only JSON-lines
    /// files (events and logs, telemetry, storage) under persistentDataPath/abxrlib/queue/. Records are buffered in memory
    /// and written once per transport tick; the backend's answers move a byte offset per file, and a sealed session is
    /// deleted once every file is acknowledged. Start-up reads only the summaries; records stream from disk when sent.
    /// Main thread only.
    /// </summary>
    internal sealed class DurableRecordQueue : IRecordQueue
    {
        internal const int MaxFailedReplays = 3;
        /// <summary>A live file that's been fully acknowledged is emptied once it reaches this size, so a long session online doesn't fill the budget.</summary>
        internal const long CompactAtBytes = 256 * 1024;
        private const string SummaryPrefix = "s-";

        private readonly string _root;
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
        private bool _warnedOverBudget;

        /// <summary>Raised when records are dropped without being sent: (session, record count, reason, observer ids of this run's records).</summary>
        internal Action<string, int, AbxrObserver.DropReason, List<long>> OnDropped;

        public bool IsDurable => true;

        internal string Root => _root;

        internal DurableRecordQueue(string root, Func<DateTime> utcNow = null, Func<int> retentionDays = null, Func<long> budgetBytes = null)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _retentionDays = retentionDays ?? (() => Configuration.Instance.offlineRetentionDays);
            _budgetBytes = budgetBytes ?? (() => Configuration.Instance.offlineDiskBudgetMB * 1024L * 1024L);
        }

        /// <summary>The default root, persistentDataPath/abxrlib/queue.</summary>
        internal static string DefaultRoot => Path.Combine(UnityEngine.Application.persistentDataPath, "abxrlib", "queue");

        // ── Start-up ─────────────────────────────────────────────

        /// <summary>
        /// Reads every summary. A session left unsealed (the app was killed) is sealed now. A summary of a newer format is
        /// left alone; an older one is migrated. Files with no readable summary are deleted, since nothing could sign them
        /// in. Then retention and the disk budget are enforced.
        /// </summary>
        internal void Load()
        {
            _sessions.Clear();
            _live = null;
            _diskBytes = 0;
            try
            {
                Directory.CreateDirectory(_root);
                var keep = new HashSet<string>();
                foreach (var path in Directory.GetFiles(_root, SummaryPrefix + "*.json"))
                {
                    if (!path.EndsWith(".json", StringComparison.Ordinal)) continue;
                    SessionSummary summary = null;
                    try
                    {
                        var json = File.ReadAllText(path);
                        var version = JObject.Parse(json).Value<int?>("formatVersion") ?? 0;
                        if (version > SessionSummary.CurrentFormat)
                        {
                            // A newer SDK wrote it (a downgrade): leave it for that SDK, and don't count it here.
                            keep.Add(Path.GetFileNameWithoutExtension(path).Substring(SummaryPrefix.Length));
                            continue;
                        }
                        summary = JsonConvert.DeserializeObject<SessionSummary>(json);
                    }
                    catch (Exception ex) { Logcat.Warning($"Dropped an unreadable offline session summary: {ex.Message}"); }

                    string key = Path.GetFileNameWithoutExtension(path).Substring(SummaryPrefix.Length);
                    if (summary == null || string.IsNullOrEmpty(summary.sessionId))
                    {
                        TryDelete(path);
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
                    keep.Add(key);
                    _sessions.Add(session);
                    _diskBytes += new FileInfo(path).Length;
                    foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                        _diskBytes += FileLength(FilePath(session, f));
                }
                foreach (var path in Directory.GetFiles(_root, SummaryPrefix + "*.jsonl"))
                {
                    string name = Path.GetFileName(path);
                    string key = name.Substring(SummaryPrefix.Length, name.IndexOf('.') - SummaryPrefix.Length);
                    if (!keep.Contains(key)) TryDelete(path);
                }
            }
            catch (Exception ex)
            {
                Logcat.Error($"Couldn't read the offline queue at {_root}: {ex.Message}");
            }
            _sessions.Sort((a, b) => a.Summary.created.CompareTo(b.Summary.created));
            foreach (var s in _sessions) WriteSummaryIfDirty(s);
            DeleteFinished();
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
            var session = LiveFor(sessionId);
            var file = FileOf(record.Kind);
            session.Buffers[(int)file].Append(ToLine(record)).Append('\n');
            session.BufferedCounts[(int)file]++;
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
                ? s.storage.Pending + _live.BufferedCounts[(int)QueueFile.Storage]
                : s.records.Pending + s.telemetry.Pending + _live.BufferedCounts[(int)QueueFile.Records] + _live.BufferedCounts[(int)QueueFile.Telemetry];
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
                if (part.Epoch != state.epoch) continue; // the file was emptied since; those records went with it
                int newlyAcked = part.LineEnds.Count(e => e > state.acked);
                if (newlyAcked == 0) continue;
                state.acked = Math.Max(state.acked, part.LineEnds[part.LineEnds.Count - 1]);
                state.ackedCount = Math.Min(state.count, state.ackedCount + newlyAcked);
                session.SummaryDirty = true;
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
            WriteSummaryIfDirty(session);
            // Nothing left to send: there's nothing to replay either.
            if (session.Pending == 0) Delete(session);
            return null;
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
                foreach (var (line, _) in ReadLines(FilePath(_live, f), _live.Summary.State(f).acked, int.MaxValue))
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
                var lines = ReadLines(FilePath(session, file), session.Summary.State(file).acked, room);
                if (lines.Count == 0) continue;
                var part = new ChunkPart { File = file, Epoch = session.Summary.State(file).epoch };
                foreach (var (line, lineEnd) in lines)
                {
                    part.LineEnds.Add(lineEnd);
                    // An unreadable line (a write the app was killed in the middle of) is skipped, but still acknowledged.
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
        /// when power was lost, or a deleted file), so a replay doesn't sign it in over and over. True when deleted.
        /// </summary>
        internal bool DiscardIfNothingToSend(StoredSession session)
        {
            if (!_sessions.Contains(session)) return true;
            if (session == _live || Peek(session, RecordStream.Data, 1) != null || Peek(session, RecordStream.Storage, 1) != null) return false;
            Logcat.Warning($"Dropped offline session {session.SessionId}: its record files have nothing left to read.");
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
            /// <summary>The byte offset just past each line read, so an acknowledgement that overlaps an earlier one counts each line once.</summary>
            public readonly List<long> LineEnds = new List<long>();
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
            try
            {
                Directory.CreateDirectory(_root);
                foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                {
                    int i = (int)f;
                    if (session.BufferedCounts[i] == 0) continue;
                    byte[] bytes = Encoding.UTF8.GetBytes(session.Buffers[i].ToString());
                    using (var fs = new FileStream(FilePath(session, f), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        fs.Write(bytes, 0, bytes.Length);
                    _diskBytes += bytes.Length;
                    session.Summary.State(f).count += session.BufferedCounts[i];
                    session.Buffers[i].Clear();
                    session.BufferedCounts[i] = 0;
                    session.SummaryDirty = true;
                }
                _warnedWriteFailure = false;
            }
            catch (Exception ex)
            {
                // Kept in memory; the next tick tries again.
                if (!_warnedWriteFailure)
                {
                    _warnedWriteFailure = true;
                    Logcat.Warning($"Couldn't write the offline queue ({ex.Message}). Records stay in memory and the SDK tries again.");
                }
            }
            WriteSummaryIfDirty(session);
        }

        /// <summary>Empties a live file the backend has answered for in full once it's large, so a long online session doesn't grow on disk.</summary>
        private void CompactLive()
        {
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            {
                var state = _live.Summary.State(f);
                if (state.acked < CompactAtBytes || _live.BufferedCounts[(int)f] > 0) continue;
                string path = FilePath(_live, f);
                long length = FileLength(path);
                if (length != state.acked) continue;
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) fs.SetLength(0);
                    _diskBytes -= length;
                    state.Reset();
                    _live.SummaryDirty = true;
                }
                catch (Exception ex) { Logcat.Debug($"Couldn't compact an offline queue file: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Keeps the queue within its disk budget (offline RFC D3): telemetry files go first, oldest session first, then
        /// whole sessions, oldest first. The live session's records file is never deleted; when only it is left over the
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
                string path = FilePath(s, QueueFile.Telemetry);
                long length = FileLength(path);
                if (length == 0) continue;
                int dropped = state.Pending;
                TryDelete(path);
                _diskBytes -= length;
                state.Reset();
                s.SummaryDirty = true;
                WriteSummaryIfDirty(s);
                Logcat.Warning($"The offline queue is over its disk budget, so it dropped {dropped} telemetry records of session {s.SessionId}.");
                if (dropped > 0) OnDropped?.Invoke(s.SessionId, dropped, AbxrObserver.DropReason.Retention, null);
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
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            {
                string path = FilePath(session, f);
                _diskBytes -= FileLength(path);
                TryDelete(path);
            }
            string summary = SummaryPath(session);
            _diskBytes -= FileLength(summary);
            TryDelete(summary);
            if (_diskBytes < 0) _diskBytes = 0;
        }

        private void WriteSummaryIfDirty(StoredSession session)
        {
            if (!session.SummaryDirty || !_sessions.Contains(session)) return;
            string path = SummaryPath(session);
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_root);
                long before = FileLength(path);
                File.WriteAllText(tmp, JsonConvert.SerializeObject(session.Summary));
                if (!File.Exists(path)) File.Move(tmp, path);
                else
                {
                    try { File.Replace(tmp, path, null); }
                    catch (PlatformNotSupportedException) { File.Delete(path); File.Move(tmp, path); }
                }
                _diskBytes += FileLength(path) - before;
                session.SummaryDirty = false;
            }
            catch (Exception ex)
            {
                TryDelete(tmp);
                if (!_warnedWriteFailure)
                {
                    _warnedWriteFailure = true;
                    Logcat.Warning($"Couldn't write an offline session summary ({ex.Message}). The SDK tries again.");
                }
            }
        }

        private string SummaryPath(StoredSession s) => Path.Combine(_root, SummaryPrefix + s.Summary.fileKey + ".json");

        private string FilePath(StoredSession s, QueueFile f) =>
            Path.Combine(_root, SummaryPrefix + s.Summary.fileKey + "." + f.ToString().ToLowerInvariant() + ".jsonl");

        private static long FileLength(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch { return 0; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Logcat.Debug($"Couldn't delete {path}: {ex.Message}"); }
        }

        // ── Line format: {"k":"e|l|t|s","r":<payload>} ───────────

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

        /// <summary>
        /// Up to max complete lines from offset, each with the byte offset just past it. A last line with no newline (torn by
        /// a kill, or still being written) isn't returned.
        /// </summary>
        internal static List<(string line, long end)> ReadLines(string path, long offset, int max)
        {
            var result = new List<(string, long)>();
            if (max <= 0 || !File.Exists(path)) return result;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (offset >= fs.Length) return result;
                fs.Position = offset;
                var buffer = new byte[16 * 1024];
                var line = new MemoryStream();
                long pos = offset;
                int read;
                while (result.Count < max && (read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read && result.Count < max; i++)
                    {
                        pos++;
                        if (buffer[i] != (byte)'\n') { line.WriteByte(buffer[i]); continue; }
                        result.Add((Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length), pos));
                        line.SetLength(0);
                    }
                }
            }
            return result;
        }
    }
}
#endif

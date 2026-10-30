using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>The kinds of record the REST transport queues. Events and logs share a file on disk; telemetry and storage get their own.</summary>
    internal enum QueuedKind
    {
        Event,
        Log,
        Telemetry,
        Storage
    }

    /// <summary>Which endpoint a record goes to: events, logs and telemetry to /v1/collect/data, storage to /v1/storage.</summary>
    internal enum RecordStream
    {
        Data,
        Storage
    }

    /// <summary>One record waiting to be sent.</summary>
    internal sealed class QueuedRecord
    {
        public readonly QueuedKind Kind;
        /// <summary>EventPayload, LogPayload, TelemetryPayload or StoragePayload, matching <see cref="Kind"/>.</summary>
        public readonly object Payload;

        public QueuedRecord(QueuedKind kind, object payload)
        {
            Kind = kind;
            Payload = payload;
        }

        public RecordStream Stream => Kind == QueuedKind.Storage ? RecordStream.Storage : RecordStream.Data;

        /// <summary>The record's UUID, which the backend uses to drop a duplicate (INS-527).</summary>
        public string RecordId => Kind switch
        {
            QueuedKind.Event => ((EventPayload)Payload).recordId,
            QueuedKind.Log => ((LogPayload)Payload).recordId,
            QueuedKind.Telemetry => ((TelemetryPayload)Payload).recordId,
            _ => ((StoragePayload)Payload).recordId
        };

        /// <summary>The observer id for this run (0 when nobody observed, or the record was loaded from disk).</summary>
        public long ObserverId
        {
            get => Kind switch
            {
                QueuedKind.Event => ((EventPayload)Payload).ObserverId,
                QueuedKind.Log => ((LogPayload)Payload).ObserverId,
                QueuedKind.Telemetry => ((TelemetryPayload)Payload).ObserverId,
                _ => ((StoragePayload)Payload).ObserverId
            };
            set
            {
                switch (Kind)
                {
                    case QueuedKind.Event: ((EventPayload)Payload).ObserverId = value; break;
                    case QueuedKind.Log: ((LogPayload)Payload).ObserverId = value; break;
                    case QueuedKind.Telemetry: ((TelemetryPayload)Payload).ObserverId = value; break;
                    default: ((StoragePayload)Payload).ObserverId = value; break;
                }
            }
        }

        /// <summary>The request body for a set of records of one stream.</summary>
        public static string ToJson(RecordStream stream, IReadOnlyList<QueuedRecord> records)
        {
            if (stream == RecordStream.Storage)
            {
                var storage = new List<StoragePayload>(records.Count);
                foreach (var r in records) storage.Add((StoragePayload)r.Payload);
                return JsonConvert.SerializeObject(new StoragePayloadWrapper { data = storage });
            }
            var data = new DataPayloadWrapper { @event = new List<EventPayload>(), telemetry = new List<TelemetryPayload>(), basicLog = new List<LogPayload>() };
            foreach (var r in records)
            {
                switch (r.Kind)
                {
                    case QueuedKind.Event: data.@event.Add((EventPayload)r.Payload); break;
                    case QueuedKind.Log: data.basicLog.Add((LogPayload)r.Payload); break;
                    case QueuedKind.Telemetry: data.telemetry.Add((TelemetryPayload)r.Payload); break;
                }
            }
            return JsonConvert.SerializeObject(data);
        }

        /// <summary>The observer ids of these records, or null when no send handler is attached (no list is built).</summary>
        public static List<long> ObserverIds(IEnumerable<QueuedRecord> records)
        {
            if (!AbxrObserver.HasSendHandler || records == null) return null;
            var ids = new List<long>();
            foreach (var r in records) AbxrObserver.AddId(ids, r.ObserverId);
            return ids;
        }
    }

    /// <summary>Some of one session's records, read for one request. Acknowledge it once the backend has answered for all of them.</summary>
    internal sealed class RecordChunk
    {
        public string SessionId;
        public RecordStream Stream;
        public readonly List<QueuedRecord> Records = new List<QueuedRecord>();
        /// <summary>Where the chunk ends, in the queue's own terms (list counts in memory, file offsets on disk).</summary>
        internal object End;
    }

    /// <summary>
    /// Where the REST transport keeps records until the backend accepts them. Records belong to a session, by the session
    /// ID they were recorded under. The live session is the one being recorded; sealing it ends it.
    /// Main thread only, like the rest of the record path.
    /// </summary>
    internal interface IRecordQueue
    {
        /// <summary>True when sessions outlive the app (the on-disk queue). False for the in-memory queue WebGL uses.</summary>
        bool IsDurable { get; }

        /// <summary>
        /// Appends to the live session. A different session ID ends the live session first, unless nothing has signed
        /// in for it yet (records from before the first sign-in belong to the session that sign-in starts). Null keeps
        /// the live session. False when the record was dropped because the queue is full.
        /// </summary>
        bool Add(string sessionId, QueuedRecord record);

        /// <summary>Records of the live session not yet acknowledged, including any not yet written.</summary>
        int PendingLive(RecordStream stream);

        /// <summary>The live session's oldest unacknowledged records of one stream, up to max. Null when there are none.</summary>
        RecordChunk PeekLive(RecordStream stream, int max);

        /// <summary>The backend accepted or permanently refused every record in the chunk, so it never goes out again.</summary>
        void Ack(RecordChunk chunk);

        /// <summary>Writes buffered records to disk. The in-memory queue has nothing to do.</summary>
        void Flush();

        /// <summary>
        /// Ends the live session (EndSession, StartNewSession, quit). On disk it's kept for replay; in memory its records
        /// are dropped, and their observer ids returned.
        /// </summary>
        List<long> SealLive();

        /// <summary>Deletes every session, the live one included. Returns the observer ids of the records dropped.</summary>
        List<long> ClearAll();

        /// <summary>What the live session's sign-in looked like, saved so the session can be signed in again later.</summary>
        void SaveSignIn(SavedSignIn signIn);

        /// <summary>Records waiting in every session, the live one included.</summary>
        int PendingRecords { get; }

        /// <summary>Sessions that still have records to send, the live one included when it has any.</summary>
        int PendingSessions { get; }

        /// <summary>For testing only. The live session's unacknowledged records, oldest first.</summary>
        List<QueuedRecord> PendingLiveForTesting();
    }

    /// <summary>
    /// The live session's sign-in without its credentials, so a session the device recorded offline can be signed in
    /// again once it's online (offline RFC D1). No app, org or pairing tokens, and never a PIN.
    /// </summary>
    [Serializable]
    internal sealed class SavedSignIn
    {
        /// <summary>The AuthPayload as sent, with every credential field and the auth mechanism cleared.</summary>
        public AuthPayload payload;
        /// <summary>"email" or "text" when the person answered a sign-in prompt of that type. Null otherwise.</summary>
        public string loginType;
        /// <summary>The answer to that prompt. Never a PIN.</summary>
        public string loginValue;
        /// <summary>The session's user data, as SetUserData merged it. The SSO claims are already in it; the SSO token isn't saved.</summary>
        public Dictionary<string, string> userData;
        /// <summary>True once the backend accepted a sign-in for this session, so it already knows its user.</summary>
        public bool signedInOnline;
        /// <summary>The app, org and app instance the session belongs to (offline RFC D6). Ids, not credentials.</summary>
        public string appKey;
        public string orgKey;
        public string instanceKey;

        /// <summary>A copy of payload with every credential cleared.</summary>
        internal static AuthPayload WithoutCredentials(AuthPayload source)
        {
            var copy = JsonConvert.DeserializeObject<AuthPayload>(JsonConvert.SerializeObject(source));
            copy.appId = null;
            copy.orgId = null;
            copy.authSecret = null;
            copy.appToken = null;
            copy.orgToken = null;
            copy.appInstanceToken = null;
            copy.SSOAccessToken = null;
            copy.authMechanism = null;
            return copy;
        }

        /// <summary>The app's id from an App Token's claims, or the legacy app id.</summary>
        internal static string AppKeyOf(AuthPayload p)
        {
            if (!string.IsNullOrEmpty(p?.appToken))
            {
                var claims = Utils.ExtractAppTokenData(p.appToken);
                if (claims != null && claims.TryGetValue("appId", out var id) && !string.IsNullOrEmpty(id)) return id;
                return "token:" + Utils.ComputeSha256Hash(p.appToken).Substring(0, 16);
            }
            return p?.appId ?? "";
        }

        /// <summary>The org's id from an org token's claims, or the legacy org id. Empty in paired mode.</summary>
        internal static string OrgKeyOf(AuthPayload p)
        {
            if (!string.IsNullOrEmpty(p?.orgToken))
            {
                var claims = Utils.ExtractAppTokenData(p.orgToken);
                if (claims != null && claims.TryGetValue("orgId", out var id) && !string.IsNullOrEmpty(id)) return id;
                return "token:" + Utils.ComputeSha256Hash(p.orgToken).Substring(0, 16);
            }
            return p?.orgId ?? "";
        }

        /// <summary>The paired app instance (its id is the device id in paired mode). Empty when not paired.</summary>
        internal static string InstanceKeyOf(AuthPayload p) => string.IsNullOrEmpty(p?.appInstanceToken) ? "" : p.deviceId ?? "";

        internal bool SameIdentity(string appKeyNow, string orgKeyNow, string instanceKeyNow) =>
            (appKey ?? "") == (appKeyNow ?? "") && (orgKey ?? "") == (orgKeyNow ?? "") && (instanceKey ?? "") == (instanceKeyNow ?? "");
    }

    /// <summary>
    /// The queue WebGL uses: four in-memory lists capped at Maximum Cached Items each, gone when the page closes. A sealed
    /// session's records are dropped, since nothing could sign it in again.
    /// </summary>
    internal sealed class MemoryRecordQueue : IRecordQueue
    {
        private readonly List<QueuedRecord> _events = new List<QueuedRecord>();
        private readonly List<QueuedRecord> _logs = new List<QueuedRecord>();
        private readonly List<QueuedRecord> _telemetry = new List<QueuedRecord>();
        private readonly List<QueuedRecord> _storage = new List<QueuedRecord>();
        /// <summary>The queues that have warned since they last had room.</summary>
        private readonly HashSet<string> _queuesAtLimit = new HashSet<string>();

        public bool IsDurable => false;

        public bool Add(string sessionId, QueuedRecord record)
        {
            var (list, name) = ListFor(record.Kind);
            if (IsQueueAtLimit(list, name)) return false;
            list.Add(record);
            return true;
        }

        public int PendingLive(RecordStream stream) =>
            stream == RecordStream.Storage ? _storage.Count : _events.Count + _logs.Count + _telemetry.Count;

        public RecordChunk PeekLive(RecordStream stream, int max)
        {
            if (max <= 0 || PendingLive(stream) == 0) return null;
            var chunk = new RecordChunk { Stream = stream };
            var counts = new int[4];
            foreach (var list in stream == RecordStream.Storage ? new[] { _storage } : new[] { _events, _logs, _telemetry })
            {
                for (int i = 0; i < list.Count && chunk.Records.Count < max; i++)
                {
                    chunk.Records.Add(list[i]);
                    counts[(int)list[i].Kind]++;
                }
            }
            chunk.End = counts;
            return chunk;
        }

        public void Ack(RecordChunk chunk)
        {
            if (!(chunk?.End is int[] counts)) return;
            // The chunk is the front of each list unless the lists were cleared since it was read; check before removing.
            foreach (QueuedKind kind in Enum.GetValues(typeof(QueuedKind)))
            {
                var (list, _) = ListFor(kind);
                int n = Math.Min(counts[(int)kind], list.Count);
                int matched = 0;
                for (int i = 0; i < n; i++)
                    if (chunk.Records.Contains(list[i])) matched++;
                    else break;
                list.RemoveRange(0, matched);
            }
        }

        public void Flush() { }

        public List<long> SealLive() => ClearAll();

        public List<long> ClearAll()
        {
            var dropped = QueuedRecord.ObserverIds(AllRecords());
            _events.Clear();
            _logs.Clear();
            _telemetry.Clear();
            _storage.Clear();
            return dropped;
        }

        public void SaveSignIn(SavedSignIn signIn) { }

        public int PendingRecords => _events.Count + _logs.Count + _telemetry.Count + _storage.Count;

        public int PendingSessions => PendingRecords > 0 ? 1 : 0;

        public List<QueuedRecord> PendingLiveForTesting() => AllRecords();

        private List<QueuedRecord> AllRecords()
        {
            var all = new List<QueuedRecord>(PendingRecords);
            all.AddRange(_events);
            all.AddRange(_logs);
            all.AddRange(_telemetry);
            all.AddRange(_storage);
            return all;
        }

        private (List<QueuedRecord>, string) ListFor(QueuedKind kind) => kind switch
        {
            QueuedKind.Event => (_events, "Event"),
            QueuedKind.Log => (_logs, "Log"),
            QueuedKind.Telemetry => (_telemetry, "Telemetry"),
            _ => (_storage, "Storage")
        };

        /// <summary>
        /// True when the queue is full, so the entry is dropped. Warns once each time a queue fills, not for every entry it
        /// drops (SDK-60 decision 1): at about six telemetry entries a second, that was a warning for each one.
        /// </summary>
        private bool IsQueueAtLimit(List<QueuedRecord> queue, string queueType)
        {
            int max = Configuration.Instance.maximumCachedItems;
            if (max <= 0 || queue.Count < max)
            {
                _queuesAtLimit.Remove(queueType);
                return false;
            }

            if (_queuesAtLimit.Add(queueType))
                Logcat.Warning($"{queueType} queue limit reached ({max}). New entries are dropped until it drains.");
            return true;
        }
    }
}

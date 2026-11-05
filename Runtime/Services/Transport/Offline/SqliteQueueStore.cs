// Android headsets only (offline RFC D13). Desktop and the Editor use FileQueueStore; WebGL has no durable queue.
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>
    /// The durable queue's store on Android: the OS's built-in SQLite through AbxrQueueDatabase (Plugins/Android/AbxrQueue.aar),
    /// one database in app-private storage. Positions are record ids. Each tick's records and the session's summary go in
    /// one transaction, records are read back in pages that stay well inside Android's 2 MB CursorWindow, and acknowledged
    /// records are deleted as the backend answers for them. Sizes are in characters, which is close to bytes for this JSON.
    /// The Java side keeps one open database per process, so every transport in a run shares it.
    /// </summary>
    internal sealed class SqliteQueueStore : IQueueStore
    {
        internal const string DatabaseName = "abxrlib-queue.db";
        /// <summary>Characters one page of records may hold. A single record is capped at DurableRecordQueue.MaxRecordChars.</summary>
        private const int PageChars = 1024 * 1024;

        private readonly AndroidJavaObject _db;

        private SqliteQueueStore(AndroidJavaObject db) => _db = db;

        /// <summary>
        /// Opens the queue database, or returns null when it can't be used (a newer SDK's schema, which is left as it is,
        /// or a failure to open), so the queue falls back to its files.
        /// </summary>
        internal static SqliteQueueStore TryOpen()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var database = new AndroidJavaClass("com.arborxr.abxrlib.queue.AbxrQueueDatabase");
                var db = database.CallStatic<AndroidJavaObject>("open", activity, DatabaseName);
                return db == null ? null : new SqliteQueueStore(db);
            }
            catch (Exception ex)
            {
                Logcat.Warning($"The offline queue couldn't open its database, so it keeps records in files instead: {ex.Message}");
                return null;
            }
        }

        public List<(string key, string json)> ReadSummaries()
        {
            var pairs = _db.Call<string[]>("readSummaries") ?? Array.Empty<string>();
            var result = new List<(string, string)>(pairs.Length / 2);
            for (int i = 0; i + 1 < pairs.Length; i += 2) result.Add((pairs[i], pairs[i + 1]));
            return result;
        }

        public void DeleteOrphans() => _db.Call("deleteOrphans");

        public int CountAfter(string key, QueueFile file, long after) => _db.Call<int>("countAfter", key, (int)file, after);

        public bool Append(string key, IReadOnlyList<string>[] lines, string summaryJson, Action<QueueFile, long> committed)
        {
            int total = 0;
            foreach (var fileLines in lines) total += fileLines?.Count ?? 0;
            if (total == 0) return false;
            var streams = new int[total];
            var flat = new string[total];
            int n = 0;
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            {
                var fileLines = lines[(int)f];
                if (fileLines == null) continue;
                foreach (var line in fileLines)
                {
                    streams[n] = (int)f;
                    flat[n++] = line;
                }
            }
            // One transaction: all of this tick's records and the summary, or none of them.
            var added = _db.Call<long[]>("append", key, streams, flat, summaryJson ?? "");
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
                if ((lines[(int)f]?.Count ?? 0) > 0) committed(f, added[(int)f]);
            return !string.IsNullOrEmpty(summaryJson);
        }

        public List<(string line, long position)> Read(string key, QueueFile file, long after, int max)
        {
            var pairs = _db.Call<string[]>("read", key, (int)file, after, max, PageChars) ?? Array.Empty<string>();
            var result = new List<(string, long)>(pairs.Length / 2);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                result.Add((pairs[i + 1], long.Parse(pairs[i], CultureInfo.InvariantCulture)));
            return result;
        }

        public long WriteSummary(string key, string json, bool durable) => _db.Call<long>("writeSummary", key, json, durable);

        public long Acknowledged(string key, QueueFile file, long through) => _db.Call<long>("acknowledged", key, (int)file, through);

        public long TryCompact(string key, QueueFile file, long acked) => -1; // acknowledged records are already deleted

        public long DeleteStream(string key, QueueFile file) => _db.Call<long>("deleteStream", key, (int)file);

        public long DeleteSession(string key) => _db.Call<long>("deleteSession", key);

        public long StreamSize(string key, QueueFile file) => _db.Call<long>("streamSize", key, (int)file);

        public long SummarySize(string key) => _db.Call<long>("summarySize", key);
    }
}
#endif

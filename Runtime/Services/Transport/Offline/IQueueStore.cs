#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections.Generic;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>
    /// Where the durable queue keeps its sessions (offline RFC D13): SQLite on Android headsets, append-only files on
    /// desktop and in the Editor. Sessions are named by their key; each has a summary (JSON) and three record streams.
    /// A position is opaque but increases within a stream: a byte offset in a file, a row id in SQLite. Every call can
    /// throw on an I/O failure, and an append that throws wrote nothing the caller hasn't been told about.
    /// Main thread only.
    /// </summary>
    internal interface IQueueStore
    {
        /// <summary>Every stored session's key and summary JSON, after the store's own recovery from a write the app was killed in.</summary>
        List<(string key, string json)> ReadSummaries();

        /// <summary>Deletes records that belong to no stored session.</summary>
        void DeleteOrphans();

        /// <summary>Complete records stored in a stream after a position.</summary>
        int CountAfter(string key, QueueFile file, long after);

        /// <summary>
        /// Appends records (one JSON line each, per stream) as one write where the store can. committed is called for
        /// each stream once its records are stored, with the bytes they added. summaryJson, when not empty, is the session's
        /// summary as it reads after the append; the result is true when the store wrote it in the same write.
        /// </summary>
        bool Append(string key, IReadOnlyList<string>[] lines, string summaryJson, Action<QueueFile, long> committed);

        /// <summary>Up to max records after a position, each with its own position, oldest first.</summary>
        List<(string line, long position)> Read(string key, QueueFile file, long after, int max);

        /// <summary>Writes a session's summary. durable forces it to the device first (sealing). Returns the change in bytes stored.</summary>
        long WriteSummary(string key, string json, bool durable);

        /// <summary>The backend answered for every record up to a position. Returns the bytes this frees.</summary>
        long Acknowledged(string key, QueueFile file, long through);

        /// <summary>Empties a fully acknowledged stream once it's large. Returns the bytes freed, or -1 when nothing changed.</summary>
        long TryCompact(string key, QueueFile file, long acked);

        /// <summary>Deletes one stream of a session (the disk budget drops telemetry first). Returns the bytes freed.</summary>
        long DeleteStream(string key, QueueFile file);

        /// <summary>Deletes a session and its records. Returns the bytes freed.</summary>
        long DeleteSession(string key);

        /// <summary>Bytes a stream holds.</summary>
        long StreamSize(string key, QueueFile file);

        /// <summary>Bytes a session's summary holds.</summary>
        long SummarySize(string key);
    }
}
#endif

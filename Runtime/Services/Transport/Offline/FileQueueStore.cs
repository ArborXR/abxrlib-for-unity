#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json.Linq;

namespace AbxrLib.Runtime.Services.Transport.Offline
{
    /// <summary>
    /// The durable queue's store on desktop and in the Editor (offline RFC D13): per session a summary file plus three
    /// append-only JSON-lines files under one folder. Positions are byte offsets, so a line's position is the offset just
    /// past it. A last line with no newline (torn by a kill) isn't read.
    /// </summary>
    internal sealed class FileQueueStore : IQueueStore
    {
        /// <summary>A stream that's been fully acknowledged is emptied once it reaches this size, so a long session online doesn't grow on disk.</summary>
        internal const long CompactAtBytes = 256 * 1024;
        private const string SummaryPrefix = "s-";

        private readonly string _root;

        internal FileQueueStore(string root) => _root = root ?? throw new ArgumentNullException(nameof(root));

        internal string Root => _root;

        public List<(string key, string json)> ReadSummaries()
        {
            Directory.CreateDirectory(_root);
            RecoverSummaryWrites();
            var result = new List<(string, string)>();
            foreach (var path in Directory.GetFiles(_root, SummaryPrefix + "*.json"))
            {
                if (!path.EndsWith(".json", StringComparison.Ordinal)) continue;
                string key = Path.GetFileNameWithoutExtension(path).Substring(SummaryPrefix.Length);
                string json;
                try { json = File.ReadAllText(path); }
                catch (Exception ex)
                {
                    Logcat.Warning($"Couldn't read an offline session summary: {ex.Message}");
                    json = null;
                }
                result.Add((key, json));
            }
            return result;
        }

        public void DeleteOrphans()
        {
            foreach (var path in Directory.GetFiles(_root, SummaryPrefix + "*.jsonl"))
            {
                string name = Path.GetFileName(path);
                string key = name.Substring(SummaryPrefix.Length, name.IndexOf('.') - SummaryPrefix.Length);
                if (!File.Exists(SummaryPath(key))) TryDelete(path);
            }
        }

        public int CountAfter(string key, QueueFile file, long after) => CountLines(FilePath(key, file), after);

        public bool Append(string key, IReadOnlyList<string>[] lines, string summaryJson, Action<QueueFile, long> committed)
        {
            Directory.CreateDirectory(_root);
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile)))
            {
                var fileLines = lines[(int)f];
                if (fileLines == null || fileLines.Count == 0) continue;
                var text = new StringBuilder();
                foreach (var line in fileLines) text.Append(line).Append('\n');
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
                using (var fs = new FileStream(FilePath(key, f), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    long before = fs.Length;
                    // Flushed here: FileStream buffers small writes, so a full disk would otherwise throw from Dispose,
                    // past the truncate.
                    try { fs.Write(bytes, 0, bytes.Length); fs.Flush(); }
                    catch
                    {
                        // The records are written again next tick: a partial write left behind would add lines no count
                        // knows about.
                        try { fs.SetLength(before); } catch (IOException) { }
                        throw;
                    }
                }
                committed(f, bytes.Length);
            }
            return false;
        }

        public List<(string line, long position)> Read(string key, QueueFile file, long after, int max) => ReadLines(FilePath(key, file), after, max);

        public long WriteSummary(string key, string json, bool durable)
        {
            string path = SummaryPath(key);
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_root);
                long before = FileLength(path);
                // A sealed summary goes to the device before the rename, so a power cut can't leave it empty.
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    if (durable) stream.Flush(true);
                }
                if (!File.Exists(path)) File.Move(tmp, path);
                else
                {
                    // A summary that can't be replaced stays stale, and a stale count can make Load delete the session, so
                    // any failure falls back to delete and move.
                    try { File.Replace(tmp, path, null); }
                    catch (Exception) { File.Delete(path); File.Move(tmp, path); }
                }
                return FileLength(path) - before;
            }
            catch
            {
                // With the summary already deleted by the fallback, the temp file is the only copy: Load recovers it.
                if (File.Exists(path)) TryDelete(tmp);
                throw;
            }
        }

        public long Acknowledged(string key, QueueFile file, long through) => 0; // lines stay until the file is compacted

        public long TryCompact(string key, QueueFile file, long acked)
        {
            if (acked < CompactAtBytes) return -1;
            string path = FilePath(key, file);
            long length = FileLength(path);
            if (length != acked) return -1;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) fs.SetLength(0);
            return length;
        }

        public long DeleteStream(string key, QueueFile file)
        {
            string path = FilePath(key, file);
            long length = FileLength(path);
            TryDelete(path);
            return length;
        }

        public long DeleteSession(string key)
        {
            long freed = 0;
            foreach (QueueFile f in Enum.GetValues(typeof(QueueFile))) freed += DeleteStream(key, f);
            string summary = SummaryPath(key);
            freed += FileLength(summary);
            TryDelete(summary);
            return freed;
        }

        public long StreamSize(string key, QueueFile file) => FileLength(FilePath(key, file));

        public long SummarySize(string key) => FileLength(SummaryPath(key));

        // ── Files ────────────────────────────────────────────────

        private string SummaryPath(string key) => Path.Combine(_root, SummaryPrefix + key + ".json");

        private string FilePath(string key, QueueFile f) =>
            Path.Combine(_root, SummaryPrefix + key + "." + f.ToString().ToLowerInvariant() + ".jsonl");

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

        /// <summary>
        /// A summary write the app was killed in: a temp file whose summary is missing was mid-rename (the delete-and-move
        /// fallback), so it's the only copy and becomes the summary if it reads. Any other temp file is stale.
        /// </summary>
        private void RecoverSummaryWrites()
        {
            foreach (var tmp in Directory.GetFiles(_root, SummaryPrefix + "*.json.tmp"))
            {
                string path = tmp.Substring(0, tmp.Length - ".tmp".Length);
                try
                {
                    if (!File.Exists(path))
                    {
                        JObject.Parse(File.ReadAllText(tmp));
                        File.Move(tmp, path);
                        continue;
                    }
                }
                catch (Exception) { /* unreadable: deleted below */ }
                TryDelete(tmp);
            }
        }

        /// <summary>Complete (newline-terminated) lines from offset to the end of the file.</summary>
        internal static int CountLines(string path, long offset)
        {
            if (!File.Exists(path)) return 0;
            int lines = 0;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (offset >= fs.Length) return 0;
                fs.Position = offset;
                var buffer = new byte[16 * 1024];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    for (int i = 0; i < read; i++)
                        if (buffer[i] == (byte)'\n') lines++;
            }
            return lines;
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

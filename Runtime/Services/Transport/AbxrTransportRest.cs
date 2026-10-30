using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using AbxrLib.Runtime.Services.Transport.Offline;
#endif

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>How one upload of a chunk ended.</summary>
    internal enum ChunkOutcome
    {
        /// <summary>The backend answered for every record: accepted, or refused one by one. Acknowledge the chunk.</summary>
        Done,
        /// <summary>No answer after the retries (offline, timeout, 408, 5xx). Leave it queued for later.</summary>
        Retry,
        /// <summary>429: wait RetryAfterSeconds.</summary>
        RateLimited,
        /// <summary>401: the token no longer works.</summary>
        Unauthorized,
        /// <summary>The queue was cleared while the chunk was out, so nothing is reported for it.</summary>
        Cancelled
    }

    internal readonly struct ChunkResult
    {
        public readonly ChunkOutcome Outcome;
        public readonly int RetryAfterSeconds;

        public ChunkResult(ChunkOutcome outcome, int retryAfterSeconds = 0)
        {
            Outcome = outcome;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }

    /// <summary>
    /// REST (UnityWebRequest) implementation of IAbxrTransport. Records go into a queue grouped by session (on disk, or in
    /// memory on WebGL) and are sent in chunks with the live session's token. On disk, a session that ends before it's
    /// all sent is kept, and the session replayer signs it in again and uploads it once the device is online (offline RFC).
    /// </summary>
    internal class AbxrTransportRest : IAbxrTransport
    {
        private const string AuthPath = "/v1/auth/token";
        private const string ConfigPath = "/v1/storage/config";
        private const string DataPath = "/v1/collect/data";
        private const string StoragePath = "/v1/storage";
        /// <summary>How long quit (or EndSession) may block sending the live session before the rest is left for replay.</summary>
        private const float QuitFlushBudgetSeconds = 3f;
        private static readonly WaitForSeconds WaitQuarterSecond = new WaitForSeconds(0.25f);

        private readonly AbxrAuthService _authService;
        private readonly MonoBehaviour _runner;
        private readonly IRecordQueue _queue;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        private readonly SessionReplayer _replayer;
        private Coroutine _replayCoroutine;
#endif
        private readonly object _lock = new();

        private float _nextDataSendAt;
        private float _nextStorageSendAt;
        private float _lastDataCallTime;
        private float _lastStorageCallTime;
        private Coroutine _tickCoroutine;
        /// <summary>Writes records to disk every tick, apart from the send loop, which a retrying send can hold for minutes.</summary>
        private Coroutine _flushCoroutine;
        private bool _stopped;
        /// <summary>Bumped when queued records are dropped (a clear, or sealing the in-memory queue), so a send still out for them stops and reports nothing more.</summary>
        private int _clearGeneration;
        /// <summary>The session a live chunk is out for, so a replay doesn't send the same records at the same time.</summary>
        private string _inFlightSessionId;
        /// <summary>Set after a live 401 asks auth for a fresh token, so a second 401 waits instead of asking again.</summary>
        private bool _liveReSignRequested;
        private int _uploadedThisRun;
        private global::Abxr.UploadStatus _lastStatus = new global::Abxr.UploadStatus(0, 0, 0);
        private float _lastStatusRaisedAt = float.MinValue;

        /// <summary>Testing only. Where the on-disk queue lives instead of persistentDataPath, for the transports created after it's set.</summary>
        internal static string QueueRootForTesting;

        public bool IsServiceTransport => false;

        private static Uri RestUri(string path) => new Uri(new Uri(Configuration.Instance.restUrl), path);

        /// <summary>Stops the tick coroutine so this transport can be released when replaced (e.g. by ArborInsights transport). Prevents leak and ongoing CPU use.</summary>
        internal void Stop()
        {
            _stopped = true;
            if (_tickCoroutine != null && _runner != null)
            {
                _runner.StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
            if (_flushCoroutine != null && _runner != null)
            {
                _runner.StopCoroutine(_flushCoroutine);
                _flushCoroutine = null;
            }
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            if (_replayCoroutine != null && _runner != null) _runner.StopCoroutine(_replayCoroutine);
            _replayCoroutine = null;
            _replayer?.Cancel();
#endif
            // What's on disk stays for the next transport; nothing buffered is lost.
            lock (_lock) { _queue.Flush(); }
        }

        public AbxrTransportRest(AbxrAuthService authService, MonoBehaviour runner, IRecordQueue queue = null)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _queue = queue ?? CreateQueue();
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            if (_queue is DurableRecordQueue durable)
            {
                durable.OnDropped = OnDurableDropped;
                _replayer = new SessionReplayer(durable, new ReplayHost(this));
            }
#endif
            _nextDataSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            _nextStorageSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            _tickCoroutine = _runner.StartCoroutine(TickCoroutine());
            _flushCoroutine = _runner.StartCoroutine(FlushCoroutine());
        }

        private static IRecordQueue CreateQueue()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new MemoryRecordQueue();
#else
            var queue = new DurableRecordQueue(QueueRootForTesting ?? DurableRecordQueue.DefaultRoot);
            queue.Load();
            return queue;
#endif
        }

        internal IRecordQueue QueueForTesting => _queue;

        private static readonly JsonSerializerSettings AuthPayloadSerializeSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete)
        {
            string url = RestUri(AuthPath).ToString();
            string json = JsonConvert.SerializeObject(payload, AuthPayloadSerializeSettings);
            using var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            yield return request.SendWebRequest();

            // Always pass response body when present so auth service gets the same error payload as service transport (ExtractAuthErrorMessage, OnFailed message).
            var result = ToAuthResult(request.result, request.responseCode, request.downloadHandler?.text,
                request.GetResponseHeader("Retry-After"), request.GetResponseHeader("Date"));
            if (!result.Success)
                Logcat.Warning($"AuthRequest failed: {(string.IsNullOrEmpty(result.Body) ? $"(empty, HTTP {result.StatusCode})" : result.Body)}");
            onComplete?.Invoke(result);
        }

        /// <summary>Maps a finished auth request to the result the auth service classifies. Takes the request's parts, not the request, so tests can pin the mapping.</summary>
        internal static AuthTransportResult ToAuthResult(UnityWebRequest.Result requestResult, long responseCode, string response,
            string retryAfterHeader = null, string dateHeader = null)
        {
            // Same success rule as service transport (AuthResponse.IsValidSuccess) so auth service sees consistent behavior.
            bool success = false;
            if (!string.IsNullOrEmpty(response))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<AuthResponse>(response);
                    success = AuthResponse.IsValidSuccess(parsed);
                }
                catch { /* treat as failure */ }
            }
            // Transport decides: API rejected credentials (do not retry) when HTTP 401 or 403.
            bool isAuthRejectedByApi = !success && (responseCode == 401 || responseCode == 403);
            bool networkError = requestResult == UnityWebRequest.Result.ConnectionError;
            int retryAfter = responseCode == 429 ? Utils.ParseRetryAfter(retryAfterHeader, DateTime.UtcNow) : 0;
            return new AuthTransportResult(success, response, isAuthRejectedByApi, responseCode, networkError, retryAfter,
                networkError ? null : Utils.ParseHttpDate(dateHeader));
        }

        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete)
        {
            string url = RestUri(ConfigPath).ToString();
            UnityWebRequest request = null;
            try
            {
                request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                _authService.SetAuthHeaders(request);
            }
            catch (Exception ex)
            {
                Logcat.Error($"GetConfig request creation failed: {ex.Message}");
                request?.Dispose();
                onComplete?.Invoke(false, ex.Message);
                yield break;
            }

            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string err = request.result switch
                    {
                        UnityWebRequest.Result.ConnectionError => $"Connection error: {request.error}",
                        UnityWebRequest.Result.DataProcessingError => $"Data processing error: {request.error}",
                        UnityWebRequest.Result.ProtocolError => $"Protocol error ({request.responseCode}): {request.error}",
                        _ => $"Unknown error: {request.error}"
                    };
                    onComplete?.Invoke(false, err);
                    yield break;
                }
                onComplete?.Invoke(true, request.downloadHandler?.text);
            }
            finally
            {
                request?.Dispose();
            }
        }

        // ── Recording ────────────────────────────────────────────

        /// <summary>Capture time from the device's clock (not Unity's), so a record replayed days later keeps when it happened.</summary>
        private static (string iso, long ms) Now()
        {
            var now = DateTimeOffset.UtcNow;
            return (now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), now.ToUnixTimeMilliseconds());
        }

        private static string NewRecordId() => Guid.NewGuid().ToString();

        public bool AddEvent(string name, Dictionary<string, string> meta, long observerId = 0)
        {
            var (iso, ms) = Now();
            var p = new EventPayload { timestamp = iso, preciseTimestamp = ms, name = name, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>(), recordId = NewRecordId(), ObserverId = observerId };
            return Enqueue(new QueuedRecord(QueuedKind.Event, p));
        }

        public bool AddTelemetry(string name, Dictionary<string, string> meta, long observerId = 0)
        {
            var (iso, ms) = Now();
            var p = new TelemetryPayload { timestamp = iso, preciseTimestamp = ms, name = name, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>(), recordId = NewRecordId(), ObserverId = observerId };
            return Enqueue(new QueuedRecord(QueuedKind.Telemetry, p));
        }

        public bool AddLog(string logLevel, string text, Dictionary<string, string> meta, long observerId = 0)
        {
            var (iso, ms) = Now();
            var p = new LogPayload { timestamp = iso, preciseTimestamp = ms, logLevel = logLevel, text = text, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>(), recordId = NewRecordId(), ObserverId = observerId };
            return Enqueue(new QueuedRecord(QueuedKind.Log, p));
        }

        public bool StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy, long observerId = 0)
        {
            var (iso, _) = Now();
            var p = new StoragePayload
            {
                timestamp = iso,
                keepPolicy = Utils.PascalToCamelCase(policy.ToString()),
                name = name,
                data = new List<Dictionary<string, string>> { entry ?? new Dictionary<string, string>() },
                scope = Utils.PascalToCamelCase(scope.ToString()),
                recordId = NewRecordId(),
                ObserverId = observerId
            };
            return Enqueue(new QueuedRecord(QueuedKind.Storage, p));
        }

        private bool Enqueue(QueuedRecord record)
        {
            lock (_lock)
            {
                if (!_queue.Add(_authService.SessionId, record)) return false;
                if (_queue.PendingLive(record.Stream) >= ChunkSize(record.Stream)) SetNextSend(record.Stream, 0);
            }
            return true;
        }

        private static int ChunkSize(RecordStream stream) => Math.Max(1, stream == RecordStream.Storage
            ? Configuration.Instance.storageEntriesPerSendAttempt
            : Configuration.Instance.dataEntriesPerSendAttempt);

        /// <summary>Sends at the next tick, and writes buffered records to disk now (the app may be losing focus or going to sleep).</summary>
        public void ForceSend()
        {
            _nextDataSendAt = 0;
            _nextStorageSendAt = 0;
            lock (_lock) { _queue.Flush(); }
        }

        // ── Storage reads ────────────────────────────────────────

        public IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete)
        {
            if (!_authService.Authenticated) { onComplete?.Invoke(null); yield break; }
            var queryParams = new Dictionary<string, string> { { "name", name }, { "scope", Utils.PascalToCamelCase(scope.ToString()) } };
            string url = Utils.BuildUrlWithParams(RestUri(StoragePath).ToString(), queryParams);
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            _authService.SetAuthHeaders(request);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string text = request.downloadHandler?.text;
                    var wrapper = JsonConvert.DeserializeObject<StoragePayloadWrapper>(text);
                    List<Dictionary<string, string>> result = wrapper?.data?.Count > 0 ? wrapper.data[0].data : null;
                    if (result == null && !string.IsNullOrEmpty(text))
                    {
                        var list = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(text);
                        if (list != null) result = list;
                    }
                    onComplete?.Invoke(result);
                }
                catch { onComplete?.Invoke(null); }
            }
            else
            {
                Logcat.Warning($"Storage GET failed: {request.error}");
                onComplete?.Invoke(null);
            }
        }

        public IEnumerator StorageDeleteCoroutine(global::Abxr.StorageScope scope, string name, Action<bool> onComplete)
        {
            if (!_authService.Authenticated) { onComplete?.Invoke(false); yield break; }
            var queryParams = new Dictionary<string, string> { { "scope", Utils.PascalToCamelCase(scope.ToString()) } };
            if (!string.IsNullOrEmpty(name)) queryParams.Add("name", name);
            string url = Utils.BuildUrlWithParams(RestUri(StoragePath).ToString(), queryParams);
            using var request = UnityWebRequest.Delete(url);
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            _authService.SetAuthHeaders(request);
            yield return request.SendWebRequest();
            onComplete?.Invoke(request.result == UnityWebRequest.Result.Success);
        }

        // ── Session lifecycle ────────────────────────────────────

        /// <summary>Sends what the live session can before the app exits. The caller seals the session right after, so on disk the rest is replayed next launch.</summary>
        public void OnQuit()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL is single-threaded: a UnityWebRequest only completes through browser callbacks on the main thread, so the
            // sync flush's Thread.Sleep spin never ends and freezes the tab. Send the tail without waiting instead. Leaving it to
            // ForceSend and the next tick would lose it: the caller clears the queues and auth state as soon as this returns.
            SendPendingWithoutWaiting();
#else
            FlushLiveSync();
#endif
        }

        public void SealSession()
        {
            List<long> dropped;
            lock (_lock)
            {
                dropped = _queue.SealLive();
                // In memory a sealed session is gone, so a send still out for it stops.
                if (!_queue.IsDurable) _clearGeneration++;
            }
            AbxrObserver.Sent(dropped, AbxrObserver.SendStatus.Dropped, dropReason: AbxrObserver.DropReason.SessionEnded);
            RaiseUploadStatus(force: true);
        }

        public void ClearAllPending(AbxrObserver.DropReason reason)
        {
            List<long> dropped;
            int pending;
            lock (_lock)
            {
                _clearGeneration++;
                pending = _queue.PendingRecords;
                dropped = _queue.ClearAll();
            }
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            _replayer?.Cancel();
#endif
            if (pending > 0 && _queue.IsDurable)
                Logcat.Info($"Dropped {pending} queued records ({reason}).");
            AbxrObserver.Sent(dropped, AbxrObserver.SendStatus.Dropped, dropReason: reason);
            RaiseUploadStatus(force: true);
        }

        public void SaveSignIn(SavedSignIn signIn)
        {
            lock (_lock) { _queue.SaveSignIn(signIn); }
        }

        public global::Abxr.UploadStatus GetUploadStatus()
        {
            lock (_lock) { return new global::Abxr.UploadStatus(_queue.PendingSessions, _queue.PendingRecords, _uploadedThisRun); }
        }

        /// <summary>Raises Abxr.OnUploadStatusChanged when the counts changed, at most once a second unless forced or everything just finished.</summary>
        private void RaiseUploadStatus(bool force = false)
        {
            var status = GetUploadStatus();
            if (status.Equals(_lastStatus)) return;
            bool finished = status.AllUploaded != _lastStatus.AllUploaded;
            if (!force && !finished && Time.realtimeSinceStartup - _lastStatusRaisedAt < 1f) return;
            _lastStatus = status;
            _lastStatusRaisedAt = Time.realtimeSinceStartup;
            global::Abxr.RaiseUploadStatusChanged(status);
        }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        private void OnDurableDropped(string sessionId, int count, AbxrObserver.DropReason reason, List<long> observerIds)
        {
            AbxrObserver.Sent(observerIds, AbxrObserver.SendStatus.Dropped, dropReason: reason);
        }
#endif

        /// <summary>WebGL quit flush (see <see cref="OnQuit"/>). Signs each chunk with the still-valid session token and lets the browser finish the request. One attempt, no re-queue: a retry or a re-queued chunk would go out after this session's auth is cleared.</summary>
        private void SendPendingWithoutWaiting()
        {
            if (!TryGetLiveToken(out string token, out string secret)) return;
            foreach (var stream in new[] { RecordStream.Data, RecordStream.Storage })
            {
                while (true)
                {
                    RecordChunk chunk;
                    lock (_lock)
                    {
                        chunk = _queue.PeekLive(stream, ChunkSize(stream));
                        if (chunk == null) break;
                        _queue.Ack(chunk);
                    }
                    PostWithoutWaiting(chunk, token, secret);
                }
            }
        }

        private void PostWithoutWaiting(RecordChunk chunk, string token, string secret)
        {
            string json = QueuedRecord.ToJson(chunk.Stream, chunk.Records);
            var ids = QueuedRecord.ObserverIds(chunk.Records);
            string kind = chunk.Stream == RecordStream.Storage ? "storage" : "data";
            var request = new UnityWebRequest(RestUri(PathFor(chunk.Stream)), "POST");
            Utils.BuildRequest(request, json);
            AbxrAuthService.SignRequest(request, token, secret, json);
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            request.SendWebRequest().completed += _ =>
            {
                bool sent = request.result == UnityWebRequest.Result.Success;
                if (!sent)
                    Logcat.Warning($"Quit flush ({kind}) failed ({request.responseCode}): {request.error}");
                ReportSent(ids, sent, request.responseCode, request.error);
                request.Dispose();
            };
        }

        /// <summary>
        /// Sends the live session synchronously, chunk by chunk, on quit and EndSession, while its token still works: the
        /// tick may never run again. Stops at the first failure or after a few seconds; what's left is sealed with the
        /// session and replayed once online.
        /// </summary>
        private void FlushLiveSync()
        {
            if (!TryGetLiveToken(out string token, out string secret)) return;
            float deadline = Time.realtimeSinceStartup + QuitFlushBudgetSeconds;
            foreach (var stream in new[] { RecordStream.Data, RecordStream.Storage })
            {
                while (Time.realtimeSinceStartup < deadline)
                {
                    RecordChunk chunk;
                    lock (_lock) { chunk = _queue.PeekLive(stream, ChunkSize(stream)); }
                    if (chunk == null) break;
                    if (!SendChunkSync(chunk, token, secret)) return;
                }
            }
        }

        private bool SendChunkSync(RecordChunk chunk, string token, string secret)
        {
            if (chunk.Records.Count == 0) { Acknowledge(chunk); return true; }
            UnityWebRequest request = null;
            string kind = chunk.Stream == RecordStream.Storage ? "storage" : "data";
            try
            {
                string json = QueuedRecord.ToJson(chunk.Stream, chunk.Records);
                request = new UnityWebRequest(RestUri(PathFor(chunk.Stream)), "POST");
                Utils.BuildRequest(request, json);
                AbxrAuthService.SignRequest(request, token, secret, json);
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                var op = request.SendWebRequest();
                while (!op.isDone) { Thread.Sleep(1); }
                bool sent = request.result == UnityWebRequest.Result.Success;
                if (!sent)
                    Logcat.Warning($"Sync flush ({kind}) failed ({request.responseCode}): {request.error}");
                ReportSent(QueuedRecord.ObserverIds(chunk.Records), sent, request.responseCode, request.error);
                if (sent) Acknowledge(chunk);
                return sent;
            }
            catch (Exception ex)
            {
                Logcat.Warning($"Sync flush ({kind}) failed: {ex.Message}");
                return false;
            }
            finally
            {
                request?.Dispose();
            }
        }

        private bool TryGetLiveToken(out string token, out string secret)
        {
            token = _authService.ResponseData?.Token;
            secret = _authService.ResponseData?.Secret;
            return _authService.Authenticated && !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(secret);
        }

        private void Acknowledge(RecordChunk chunk)
        {
            lock (_lock)
            {
                _queue.Ack(chunk);
                _uploadedThisRun += chunk.Records.Count;
            }
        }

        // ── Testing ──────────────────────────────────────────────

        /// <summary>For testing only. The live session's unsent events.</summary>
        public List<EventPayload> GetPendingEventsForTesting() => PendingOfKind<EventPayload>(QueuedKind.Event);

        /// <summary>For testing only. The live session's unsent logs.</summary>
        public List<LogPayload> GetPendingLogsForTesting() => PendingOfKind<LogPayload>(QueuedKind.Log);

        /// <summary>For testing only. The live session's unsent telemetry.</summary>
        public List<TelemetryPayload> GetPendingTelemetryForTesting() => PendingOfKind<TelemetryPayload>(QueuedKind.Telemetry);

        private List<T> PendingOfKind<T>(QueuedKind kind)
        {
            lock (_lock) { return _queue.PendingLiveForTesting().Where(r => r.Kind == kind).Select(r => (T)r.Payload).ToList(); }
        }

        // ── Sending ──────────────────────────────────────────────

        private IEnumerator TickCoroutine()
        {
            while (!_stopped)
            {
                yield return WaitQuarterSecond;
                if (_stopped) yield break;
                if (Time.time >= _nextDataSendAt)
                    yield return SendLive(RecordStream.Data);
                if (_stopped) yield break;
                if (Time.time >= _nextStorageSendAt)
                    yield return SendLive(RecordStream.Storage);
                if (_stopped) yield break;
                MaybeStartReplay();
            }
        }

        private IEnumerator FlushCoroutine()
        {
            while (!_stopped)
            {
                yield return WaitQuarterSecond;
                if (_stopped) yield break;
                lock (_lock) { _queue.Flush(); }
                RaiseUploadStatus();
            }
        }

        /// <summary>Chunks one live send may send back to back while the backend accepts them, so a backlog drains as fast as 3.0's single request did.</summary>
        private const int MaxChunksPerSend = 32;

        private IEnumerator SendLive(RecordStream stream)
        {
            bool storage = stream == RecordStream.Storage;
            float lastCall = storage ? _lastStorageCallTime : _lastDataCallTime;
            if (Time.time - lastCall < Configuration.Instance.maxCallFrequencySeconds) yield break;
            if (storage) _lastStorageCallTime = Time.time; else _lastDataCallTime = Time.time;
            SetNextSend(stream, Time.time + Configuration.Instance.sendNextBatchWaitSeconds);
            if (!TryGetLiveToken(out string token, out string secret)) yield break;

            for (int sent = 0; sent < MaxChunksPerSend && !_stopped; sent++)
            {
                bool more = false;
                yield return SendLiveChunk(stream, token, secret, m => more = m);
                if (!more) yield break;
            }
            SetNextSend(stream, 0);
        }

        /// <summary>Sends the live session's next chunk. more is true when it was accepted and more is waiting.</summary>
        private IEnumerator SendLiveChunk(RecordStream stream, string token, string secret, Action<bool> more)
        {
            more(false);
            RecordChunk chunk;
            lock (_lock) { chunk = _queue.PeekLive(stream, ChunkSize(stream)); }
            if (chunk == null) yield break;
            // Nothing readable in it (lines a kill left torn): acknowledge without sending.
            if (chunk.Records.Count == 0) { Acknowledge(chunk); more(true); yield break; }

            int generation = _clearGeneration;
            _inFlightSessionId = chunk.SessionId;
            ChunkResult result = default;
            // The token is the one the chunk's session signed in with. It stays valid after the session ends, so a chunk
            // that was out when the session was sealed still finishes as that session.
            yield return Upload(chunk.Stream, chunk.Records, token, secret, () => generation == _clearGeneration && !_stopped, r => result = r);
            _inFlightSessionId = null;

            switch (result.Outcome)
            {
                case ChunkOutcome.Done:
                    Acknowledge(chunk);
                    _liveReSignRequested = false;
                    // The device is online: send the rest without waiting, and let a replay start.
                    bool pending;
                    lock (_lock) { pending = _queue.PendingLive(stream) > 0; }
                    more(pending);
#if !(UNITY_WEBGL && !UNITY_EDITOR)
                    _replayer?.ResumeNow();
#endif
                    RaiseUploadStatus();
                    break;
                case ChunkOutcome.RateLimited:
                    SetNextSend(stream, Time.time + result.RetryAfterSeconds);
                    break;
                case ChunkOutcome.Unauthorized:
                    // The token stopped working before re-auth polling replaced it. Ask once for a new one for this session.
                    if (!_liveReSignRequested)
                    {
                        _liveReSignRequested = true;
                        _authService.RefreshSessionToken();
                        SetNextSend(stream, Time.time + 2f);
                    }
                    break;
            }
        }

        private void SetNextSend(RecordStream stream, float at)
        {
            if (stream == RecordStream.Storage) _nextStorageSendAt = at;
            else _nextDataSendAt = at;
        }

        private static string PathFor(RecordStream stream) => stream == RecordStream.Storage ? StoragePath : DataPath;

        /// <summary>
        /// Uploads records signed with token and secret, and reports each record's result to the observer. Offline, a
        /// timeout, 408 or 5xx retries a few times and then gives up (Retry). 429 and 401 return at once. Any other 4xx
        /// splits the records in halves until the backend's refusal is down to single records, which are dropped as
        /// Rejected, so one bad record can't hold up the others or come back forever.
        /// </summary>
        internal IEnumerator Upload(RecordStream stream, List<QueuedRecord> records, string token, string secret, Func<bool> stillValid, Action<ChunkResult> done)
        {
            yield return UploadPart(stream, records, token, secret, stillValid, done, new List<string>());
        }

        private IEnumerator UploadPart(RecordStream stream, List<QueuedRecord> records, string token, string secret, Func<bool> stillValid, Action<ChunkResult> done, List<string> rejected)
        {
            string kind = stream == RecordStream.Storage ? "Storage" : "Data";
            if (records.Count == 0) { done(new ChunkResult(ChunkOutcome.Done)); yield break; }
            string json;
            try { json = QueuedRecord.ToJson(stream, records); }
            catch (Exception ex)
            {
                Logcat.Error($"{kind} serialization failed, so {records.Count} records were dropped: {ex.Message}");
                AbxrObserver.Sent(QueuedRecord.ObserverIds(records), AbxrObserver.SendStatus.Dropped, 0, ex.Message, AbxrObserver.DropReason.Rejected);
                done(new ChunkResult(ChunkOutcome.Done));
                yield break;
            }

            int retries = 0;
            int maxRetries = Configuration.Instance.sendRetriesOnFailure;
            while (true)
            {
                if (!stillValid()) { done(new ChunkResult(ChunkOutcome.Cancelled)); yield break; }
                UnityWebRequest request = null;
                try
                {
                    request = new UnityWebRequest(RestUri(PathFor(stream)), "POST");
                    Utils.BuildRequest(request, json);
                    AbxrAuthService.SignRequest(request, token, secret, json);
                    request.timeout = Configuration.Instance.requestTimeoutSeconds;
                }
                catch (Exception ex)
                {
                    request?.Dispose();
                    Logcat.Error($"{kind} POST could not be built: {ex.Message}");
                    done(new ChunkResult(ChunkOutcome.Retry));
                    yield break;
                }

                yield return request.SendWebRequest();
                long status = request.responseCode;
                string error = request.error;
                var result = request.result;
                string retryAfterHeader = request.GetResponseHeader("Retry-After");
                request.Dispose();
                bool valid = stillValid();

                if (result == UnityWebRequest.Result.Success)
                {
                    if (valid) ReportSent(QueuedRecord.ObserverIds(records), true, status, null);
                    done(new ChunkResult(ChunkOutcome.Done));
                    yield break;
                }
                if (status == 429)
                {
                    int wait = Utils.ParseRetryAfter(retryAfterHeader, DateTime.UtcNow);
                    Logcat.Warning($"{kind} POST was rate limited. Trying again in {wait} seconds.");
                    done(new ChunkResult(ChunkOutcome.RateLimited, wait));
                    yield break;
                }
                if (status == 401)
                {
                    Logcat.Warning($"{kind} POST was refused (401): the session's token no longer works.");
                    done(new ChunkResult(ChunkOutcome.Unauthorized));
                    yield break;
                }
                bool transient = result == UnityWebRequest.Result.ConnectionError || status == 0 || status == 408 || status >= 500;
                if (transient)
                {
                    if (retries < maxRetries && valid)
                    {
                        retries++;
                        yield return new WaitForSecondsRealtime(Configuration.Instance.sendRetryIntervalSeconds);
                        continue;
                    }
                    if (valid)
                    {
                        Logcat.Warning($"{kind} POST failed after {retries + 1} attempts ({(status > 0 ? $"HTTP {status}" : error)}). The records stay queued.");
                        ReportSent(QueuedRecord.ObserverIds(records), false, status, error);
                    }
                    done(new ChunkResult(valid ? ChunkOutcome.Retry : ChunkOutcome.Cancelled));
                    yield break;
                }

                // Refused (400, 403, 413, 422, ...): retrying the same body gets the same answer.
                if (records.Count == 1)
                {
                    // One error per chunk, which release builds keep; the chunk's other refusals go to debug.
                    string message = $"{kind} POST failed: the backend refused a record ({status}: {error}), so it was dropped.";
                    if (rejected.Count == 0) Logcat.Error(message);
                    else Logcat.Debug(message);
                    rejected.Add(message);
                    if (valid)
                        AbxrObserver.Sent(QueuedRecord.ObserverIds(records), AbxrObserver.SendStatus.Dropped, (int)status, error, AbxrObserver.DropReason.Rejected);
                    done(new ChunkResult(ChunkOutcome.Done));
                    yield break;
                }
                Logcat.Debug($"{kind} POST was refused ({status}). Splitting the {records.Count} records to find the ones it refuses.");
                int half = records.Count / 2;
                foreach (var part in new[] { records.GetRange(0, half), records.GetRange(half, records.Count - half) })
                {
                    ChunkResult partResult = default;
                    yield return UploadPart(stream, part, token, secret, stillValid, r => partResult = r, rejected);
                    if (partResult.Outcome != ChunkOutcome.Done) { done(partResult); yield break; }
                }
                done(new ChunkResult(ChunkOutcome.Done));
                yield break;
            }
        }

        private static void ReportSent(List<long> recordIds, bool sent, long responseCode, string error)
        {
            AbxrObserver.Sent(recordIds, sent ? AbxrObserver.SendStatus.Sent : AbxrObserver.SendStatus.Failed, (int)responseCode, sent ? null : error);
        }

        // ── Replay ───────────────────────────────────────────────

        private void MaybeStartReplay()
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            // A token for the live session is the proof of connection: replay never runs before the live device auth succeeds.
            if (_replayer == null || _replayer.IsRunning || !TryGetLiveToken(out _, out _)) return;
            if (!_replayer.ShouldRun(Time.realtimeSinceStartup)) return;
            _replayCoroutine = _runner.StartCoroutine(RunReplay());
#endif
        }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        private IEnumerator RunReplay()
        {
            yield return _replayer.Run();
            _replayCoroutine = null;
            RaiseUploadStatus(force: true);
        }

        /// <summary>What the session replayer needs from the transport and the auth service.</summary>
        private sealed class ReplayHost : IReplayHost
        {
            private readonly AbxrTransportRest _transport;

            internal ReplayHost(AbxrTransportRest transport) => _transport = transport;

            public IEnumerator SignIn(AuthPayload payload, Action<AuthTransportResult> done) => _transport.AuthRequestCoroutine(payload, done);

            public IEnumerator Upload(RecordChunk chunk, string token, string secret, Action<ChunkResult> done)
            {
                int generation = _transport._clearGeneration;
                return _transport.Upload(chunk.Stream, chunk.Records, token, secret, () => generation == _transport._clearGeneration && !_transport._stopped, done);
            }

            public AuthPayload CurrentCredentials() => _transport._authService.CredentialsForReplay();

            public bool IsBusy(string sessionId) => sessionId != null && sessionId == _transport._inFlightSessionId;

            public bool IsRevokedInstance(AuthTransportResult result) => _transport._authService.IsPairedInstanceRevoked(result);

            public void RevokePairing() => _transport._authService.RevokePairingFromReplay();

            public int ChunkSize(RecordStream stream) => AbxrTransportRest.ChunkSize(stream);

            public float Now => Time.realtimeSinceStartup;

            public IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

            public void Acknowledge(RecordChunk chunk)
            {
                _transport.Acknowledge(chunk);
                _transport.RaiseUploadStatus();
            }
        }
#endif
    }
}

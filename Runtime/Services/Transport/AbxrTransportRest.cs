using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>REST (UnityWebRequest) implementation of IAbxrTransport. Queues data/storage and sends via HTTP with auth headers from auth service.</summary>
    internal class AbxrTransportRest : IAbxrTransport
    {
        private const string AuthPath = "/v1/auth/token";
        private const string ConfigPath = "/v1/storage/config";
        private const string DataPath = "/v1/collect/data";
        private const string StoragePath = "/v1/storage";
        private static readonly WaitForSeconds WaitQuarterSecond = new WaitForSeconds(0.25f);

        private readonly AbxrAuthService _authService;
        private readonly MonoBehaviour _runner;
        private readonly Uri _baseUri;
        private readonly Uri _dataUri;
        private readonly Uri _storageUri;

        private readonly List<EventPayload> _eventPayloads = new();
        private readonly List<TelemetryPayload> _telemetryPayloads = new();
        private readonly List<LogPayload> _logPayloads = new();
        private readonly List<StoragePayload> _storagePayloads = new();
        private readonly object _lock = new();

        private float _nextDataSendAt;
        private float _nextStorageSendAt;
        private float _lastDataCallTime;
        private float _lastStorageCallTime;
        private Coroutine _tickCoroutine;
        private bool _stopped;

        public bool IsServiceTransport => false;

        /// <summary>Stops the tick coroutine so this transport can be released when replaced (e.g. by ArborInsights transport). Prevents leak and ongoing CPU use.</summary>
        internal void Stop()
        {
            _stopped = true;
            if (_tickCoroutine != null && _runner != null)
            {
                _runner.StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        public AbxrTransportRest(AbxrAuthService authService, MonoBehaviour runner)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _baseUri = new Uri(Configuration.Instance.restUrl);
            _dataUri = new Uri(_baseUri, DataPath);
            _storageUri = new Uri(_baseUri, StoragePath);
            _nextDataSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            _nextStorageSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            _tickCoroutine = _runner.StartCoroutine(TickCoroutine());
        }

        private static readonly JsonSerializerSettings AuthPayloadSerializeSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<bool, string, bool> onComplete)
        {
            string url = new Uri(_baseUri, AuthPath).ToString();
            string json = JsonConvert.SerializeObject(payload, AuthPayloadSerializeSettings);
            using var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            yield return request.SendWebRequest();

            // Always pass response body when present so auth service gets the same error payload as service transport (ExtractAuthErrorMessage, OnFailed message).
            string response = request.downloadHandler?.text;
            long responseCode = request.responseCode;

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
            // Normalize empty failure body so auth service logs the same message for both transports.
            string responseBody = response ?? "";
            if (string.IsNullOrEmpty(responseBody) && !success)
                responseBody = "No response body.";
            if (!success)
                Logcat.Warning($"AuthRequest failed: {responseBody}");
            onComplete?.Invoke(success, responseBody, isAuthRejectedByApi);
        }

        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete)
        {
            string url = new Uri(_baseUri, ConfigPath).ToString();
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

        public void AddEvent(string name, Dictionary<string, string> meta)
        {
            long t = Utils.GetUnityTime();
            string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var p = new EventPayload { timestamp = iso, preciseTimestamp = t, name = name, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>() };
            lock (_lock)
            {
                if (IsQueueAtLimit(_eventPayloads, "Event")) return;
                _eventPayloads.Add(p);
                if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt) _nextDataSendAt = 0;
            }
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            long t = Utils.GetUnityTime();
            string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var p = new TelemetryPayload { timestamp = iso, preciseTimestamp = t, name = name, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>() };
            lock (_lock)
            {
                if (IsQueueAtLimit(_telemetryPayloads, "Telemetry")) return;
                _telemetryPayloads.Add(p);
                if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt) _nextDataSendAt = 0;
            }
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            long t = Utils.GetUnityTime();
            string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var p = new LogPayload { timestamp = iso, preciseTimestamp = t, logLevel = logLevel, text = text, meta = meta != null ? new Dictionary<string, string>(meta) : new Dictionary<string, string>() };
            lock (_lock)
            {
                if (IsQueueAtLimit(_logPayloads, "Log")) return;
                _logPayloads.Add(p);
                if (GetTotalDataCount() >= Configuration.Instance.dataEntriesPerSendAttempt) _nextDataSendAt = 0;
            }
        }

        public void ForceSend()
        {
            _nextDataSendAt = 0;
            _nextStorageSendAt = 0;
        }

        public void StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy)
        {
            long t = Utils.GetUnityTime();
            string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var p = new StoragePayload
            {
                timestamp = iso,
                keepPolicy = Utils.PascalToCamelCase(policy.ToString()),
                name = name,
                data = new List<Dictionary<string, string>> { entry ?? new Dictionary<string, string>() },
                scope = Utils.PascalToCamelCase(scope.ToString())
            };
            lock (_lock)
            {
                if (IsQueueAtLimit(_storagePayloads, "Storage")) return;
                _storagePayloads.Add(p);
                if (_storagePayloads.Count >= Configuration.Instance.storageEntriesPerSendAttempt) _nextStorageSendAt = 0;
            }
        }

        public IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete)
        {
            if (!_authService.Authenticated) { onComplete?.Invoke(null); yield break; }
            var queryParams = new Dictionary<string, string> { { "name", name }, { "scope", Utils.PascalToCamelCase(scope.ToString()) } };
            string url = Utils.BuildUrlWithParams(_storageUri.ToString(), queryParams);
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
            string url = Utils.BuildUrlWithParams(_storageUri.ToString(), queryParams);
            using var request = UnityWebRequest.Delete(url);
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            _authService.SetAuthHeaders(request);
            yield return request.SendWebRequest();
            onComplete?.Invoke(request.result == UnityWebRequest.Result.Success);
        }

        /// <summary>Flush and release. Sends any pending data and storage synchronously so it reaches the server before the app exits.</summary>
        public void OnQuit()
        {
            FlushDataSync();
            FlushStorageSync();
        }

        /// <summary>Synchronously send any queued events/telemetry/logs. Used on quit so data is sent before the process exits (ForceSend only sets flags; the tick may never run again).</summary>
        private void FlushDataSync()
        {
            if (!_authService.Authenticated) return;
            List<EventPayload> events;
            List<TelemetryPayload> telemetries;
            List<LogPayload> logs;
            lock (_lock)
            {
                if (_eventPayloads.Count == 0 && _telemetryPayloads.Count == 0 && _logPayloads.Count == 0) return;
                events = new List<EventPayload>(_eventPayloads);
                telemetries = new List<TelemetryPayload>(_telemetryPayloads);
                logs = new List<LogPayload>(_logPayloads);
                _eventPayloads.Clear();
                _telemetryPayloads.Clear();
                _logPayloads.Clear();
            }
            SendDataPayloadSync(events, telemetries, logs);
        }

        private void SendDataPayloadSync(List<EventPayload> events, List<TelemetryPayload> telemetries, List<LogPayload> logs)
        {
            UnityWebRequest request = null;
            try
            {
                var wrapper = new DataPayloadWrapper { @event = events, telemetry = telemetries, basicLog = logs };
                string json = JsonConvert.SerializeObject(wrapper);
                request = new UnityWebRequest(_dataUri, "POST");
                Utils.BuildRequest(request, json);
                _authService.SetAuthHeaders(request, json);
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                var op = request.SendWebRequest();
                while (!op.isDone) { Thread.Sleep(1); }
                if (request.result != UnityWebRequest.Result.Success)
                    Logcat.Warning($"Sync flush (data) failed ({request.responseCode}): {request.error}");
            }
            finally
            {
                request?.Dispose();
            }
        }

        /// <summary>Synchronously send any queued storage. Used on quit so data is sent before the process exits.</summary>
        private void FlushStorageSync()
        {
            if (!_authService.Authenticated) return;
            List<StoragePayload> toSend;
            lock (_lock)
            {
                if (_storagePayloads.Count == 0) return;
                toSend = new List<StoragePayload>(_storagePayloads);
                _storagePayloads.Clear();
            }
            SendStoragePayloadSync(toSend);
        }

        private void SendStoragePayloadSync(List<StoragePayload> toSend)
        {
            UnityWebRequest request = null;
            try
            {
                var wrapper = new StoragePayloadWrapper { data = toSend };
                string json = JsonConvert.SerializeObject(wrapper);
                request = new UnityWebRequest(_storageUri, "POST");
                Utils.BuildRequest(request, json);
                _authService.SetAuthHeaders(request, json);
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                var op = request.SendWebRequest();
                while (!op.isDone) { Thread.Sleep(1); }
                if (request.result != UnityWebRequest.Result.Success)
                    Logcat.Warning($"Sync flush (storage) failed ({request.responseCode}): {request.error}");
            }
            finally
            {
                request?.Dispose();
            }
        }

        public void ClearAllPending()
        {
            lock (_lock)
            {
                _eventPayloads.Clear();
                _telemetryPayloads.Clear();
                _logPayloads.Clear();
                _storagePayloads.Clear();
            }
        }

        /// <summary>For testing only. Returns a copy of pending event payloads.</summary>
        public List<EventPayload> GetPendingEventsForTesting()
        {
            lock (_lock) { return new List<EventPayload>(_eventPayloads); }
        }

        /// <summary>For testing only. Returns a copy of pending log payloads.</summary>
        public List<LogPayload> GetPendingLogsForTesting()
        {
            lock (_lock) { return new List<LogPayload>(_logPayloads); }
        }

        /// <summary>For testing only. Returns a copy of pending telemetry payloads.</summary>
        public List<TelemetryPayload> GetPendingTelemetryForTesting()
        {
            lock (_lock) { return new List<TelemetryPayload>(_telemetryPayloads); }
        }

        private IEnumerator TickCoroutine()
        {
            while (!_stopped)
            {
                yield return WaitQuarterSecond;
                if (_stopped) yield break;
                if (Time.time >= _nextDataSendAt)
                    yield return SendData();
                if (_stopped) yield break;
                if (Time.time >= _nextStorageSendAt)
                    yield return SendStorage();
            }
        }

        private int GetTotalDataCount()
        {
            return _eventPayloads.Count + _telemetryPayloads.Count + _logPayloads.Count;
        }

        private static bool IsQueueAtLimit<T>(List<T> queue, string queueType)
        {
            int max = Configuration.Instance.maximumCachedItems;
            if (max > 0 && queue.Count >= max) { Logcat.Warning($"{queueType} queue limit reached ({max})"); return true; }
            return false;
        }

        private IEnumerator SendData()
        {
            if (Time.time - _lastDataCallTime < Configuration.Instance.maxCallFrequencySeconds) yield break;
            _lastDataCallTime = Time.time;
            _nextDataSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            if (!_authService.Authenticated) yield break;
            List<EventPayload> events;
            List<TelemetryPayload> telemetries;
            List<LogPayload> logs;
            lock (_lock)
            {
                if (_eventPayloads.Count == 0 && _telemetryPayloads.Count == 0 && _logPayloads.Count == 0) yield break;
                events = new List<EventPayload>(_eventPayloads);
                telemetries = new List<TelemetryPayload>(_telemetryPayloads);
                logs = new List<LogPayload>(_logPayloads);
                _eventPayloads.Clear();
                _telemetryPayloads.Clear();
                _logPayloads.Clear();
            }
            yield return SendDataWithRetry(events, telemetries, logs);
        }

        private IEnumerator SendDataWithRetry(List<EventPayload> events, List<TelemetryPayload> telemetries, List<LogPayload> logs)
        {
            string json;
            try { json = JsonConvert.SerializeObject(new DataPayloadWrapper { @event = events, telemetry = telemetries, basicLog = logs }); }
            catch (Exception ex) { Logcat.Error($"Data serialization failed: {ex.Message}"); yield break; }
            int retryCount = 0;
            int maxRetries = Configuration.Instance.sendRetriesOnFailure;
            bool success = false;
            string lastError = "";
            while (retryCount <= maxRetries && !success)
            {
                UnityWebRequest request = null;
                bool created = false;
                bool dataRetryWait = false;
                bool dataRetryBreak = false;
                try
                {
                    request = new UnityWebRequest(_dataUri, "POST");
                    Utils.BuildRequest(request, json);
                    _authService.SetAuthHeaders(request, json);
                    request.timeout = Configuration.Instance.requestTimeoutSeconds;
                    created = true;
                }
                catch (Exception ex) { lastError = ex.Message; if (IsDataRetryableException(ex) && retryCount < maxRetries) { retryCount++; dataRetryWait = true; } else dataRetryBreak = true; }
                if (dataRetryBreak) break;
                if (dataRetryWait) { yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds); continue; }

                if (!created) break;
                yield return request.SendWebRequest();
                try
                {
                    if (request.result == UnityWebRequest.Result.Success) { success = true; }
                    else { lastError = request.error; if (IsDataRetryableError(request)) { retryCount++; yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds); continue; } break; }
                }
                finally { request?.Dispose(); }
            }
            if (!success)
            {
                Logcat.Error($"Data POST failed after {retryCount} attempts: {lastError}");
                _nextDataSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
                lock (_lock)
                {
                    foreach (var p in events) { if (!IsQueueAtLimit(_eventPayloads, "Event")) _eventPayloads.Insert(0, p); }
                    foreach (var p in telemetries) { if (!IsQueueAtLimit(_telemetryPayloads, "Telemetry")) _telemetryPayloads.Insert(0, p); }
                    foreach (var p in logs) { if (!IsQueueAtLimit(_logPayloads, "Log")) _logPayloads.Insert(0, p); }
                }
            }
        }

        private static bool IsDataRetryableError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;
            if (request.result == UnityWebRequest.Result.ProtocolError) return request.responseCode >= 500 && request.responseCode < 600;
            return false;
        }

        private static bool IsDataRetryableException(Exception ex)
        {
            return ex is System.Net.WebException || ex is System.Net.Sockets.SocketException || ex.Message.Contains("timeout") || ex.Message.Contains("connection");
        }

        private IEnumerator SendStorage()
        {
            if (Time.time - _lastStorageCallTime < Configuration.Instance.maxCallFrequencySeconds) yield break;
            _lastStorageCallTime = Time.time;
            _nextStorageSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
            if (!_authService.Authenticated) yield break;
            List<StoragePayload> toSend;
            lock (_lock)
            {
                if (_storagePayloads.Count == 0) yield break;
                toSend = new List<StoragePayload>(_storagePayloads);
                _storagePayloads.Clear();
            }
            yield return SendStorageWithRetry(toSend);
        }

        private IEnumerator SendStorageWithRetry(List<StoragePayload> toSend)
        {
            string json;
            try { json = JsonConvert.SerializeObject(new StoragePayloadWrapper { data = toSend }); }
            catch (Exception ex) { Logcat.Error($"Storage serialization failed: {ex.Message}"); yield break; }
            int retryCount = 0;
            int maxRetries = Configuration.Instance.sendRetriesOnFailure;
            bool success = false;
            string lastError = "";
            while (retryCount <= maxRetries && !success)
            {
                UnityWebRequest request = null;
                bool created = false;
                bool storageRetryWait = false;
                bool storageRetryBreak = false;
                try
                {
                    request = new UnityWebRequest(_storageUri, "POST");
                    Utils.BuildRequest(request, json);
                    _authService.SetAuthHeaders(request, json);
                    request.timeout = Configuration.Instance.requestTimeoutSeconds;
                    created = true;
                }
                catch (Exception ex) { lastError = ex.Message; if (IsStorageRetryableException(ex) && retryCount < maxRetries) { retryCount++; storageRetryWait = true; } else storageRetryBreak = true; }
                if (storageRetryBreak) break;
                if (storageRetryWait) { yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds); continue; }

                if (!created) break;
                yield return request.SendWebRequest();
                try
                {
                    if (request.result == UnityWebRequest.Result.Success) success = true;
                    else { lastError = request.error; if (IsStorageRetryableError(request)) { retryCount++; yield return new WaitForSeconds(Configuration.Instance.sendRetryIntervalSeconds); continue; } break; }
                }
                finally { request?.Dispose(); }
            }
            if (!success)
            {
                Logcat.Error($"Storage POST failed after {retryCount} attempts: {lastError}");
                _nextStorageSendAt = Time.time + Configuration.Instance.sendNextBatchWaitSeconds;
                lock (_lock) { foreach (var p in toSend) { if (!IsQueueAtLimit(_storagePayloads, "Storage")) _storagePayloads.Insert(0, p); } }
            }
        }

        private static bool IsStorageRetryableError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;
            if (request.result == UnityWebRequest.Result.ProtocolError) return request.responseCode >= 500 && request.responseCode < 600;
            return false;
        }

        private static bool IsStorageRetryableException(Exception ex)
        {
            return ex is System.Net.WebException || ex is System.Net.Sockets.SocketException || ex.Message.Contains("timeout") || ex.Message.Contains("connection");
        }

    }
}

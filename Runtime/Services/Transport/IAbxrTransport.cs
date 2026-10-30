using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>What one auth request produced, as the transport saw it.</summary>
    internal readonly struct AuthTransportResult
    {
        /// <summary>The response is a valid auth success (AuthResponse.IsValidSuccess).</summary>
        public readonly bool Success;
        /// <summary>The transport's own verdict that the API refused the credentials: REST 401/403, ArborInsightsClient getLastAuthRejected().</summary>
        public readonly bool IsAuthRejectedByApi;
        /// <summary>No HTTP response arrived: offline, DNS, refused connection, timeout, or TLS failure.</summary>
        public readonly bool NetworkError;
        /// <summary>Response body as received; null or empty when the response had none.</summary>
        public readonly string Body;
        /// <summary>HTTP status, or 0 when no response arrived or the transport has none (ArborInsightsClient).</summary>
        public readonly long StatusCode;
        /// <summary>A 429's Retry-After in seconds (default 60 when the header is missing). 0 for any other answer.</summary>
        public readonly int RetryAfterSeconds;
        /// <summary>The response's Date header, for correcting the request timestamp on a device whose clock is off. Null when absent.</summary>
        public readonly DateTime? ServerDate;

        public AuthTransportResult(bool success, string body, bool isAuthRejectedByApi, long statusCode = 0, bool networkError = false,
            int retryAfterSeconds = 0, DateTime? serverDate = null)
        {
            Success = success;
            Body = body;
            IsAuthRejectedByApi = isAuthRejectedByApi;
            StatusCode = statusCode;
            NetworkError = networkError;
            RetryAfterSeconds = retryAfterSeconds;
            ServerDate = serverDate;
        }
    }

    /// <summary>
    /// Transport abstraction for sending auth, config, data, and storage requests.
    /// Implementations: REST (UnityWebRequest) or ArborInsightsClient (device service).
    /// </summary>
    internal interface IAbxrTransport
    {
        /// <summary>True when this transport is the ArborInsightsClient (service) implementation.</summary>
        bool IsServiceTransport { get; }

        /// <summary>Perform auth request. onComplete receives the result with the transport's rejection verdict, HTTP status, and network-error flag, so the auth service can tell a refusal from a transient failure.</summary>
        IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete);

        /// <summary>Get app config JSON. onComplete(success, configJson).</summary>
        IEnumerator GetConfigCoroutine(Action<bool, string> onComplete);

        /// <summary>Queue or hand off one record. False when it was dropped because the queue is full. observerId is the observer id (0 when nobody observes); REST reports it with the send result.</summary>
        bool AddEvent(string name, Dictionary<string, string> meta, long observerId = 0);
        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool AddTelemetry(string name, Dictionary<string, string> meta, long observerId = 0);
        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool AddLog(string logLevel, string text, Dictionary<string, string> meta, long observerId = 0);
        void ForceSend();

        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy, long observerId = 0);
        IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete);
        IEnumerator StorageDeleteCoroutine(global::Abxr.StorageScope scope, string name, Action<bool> onComplete);

        /// <summary>Flush and release. REST: sends what the live session can before the app exits; service: Unbind.</summary>
        void OnQuit();

        /// <summary>
        /// Ends the live session and keeps what it didn't send (offline RFC D2: EndSession, StartNewSession, quit). REST on
        /// disk: sealed, then signed in again and uploaded once online. REST in memory (WebGL): dropped as SessionEnded.
        /// Service: no-op.
        /// </summary>
        void SealSession();

        /// <summary>Deletes every queued session, the live one included, and reports their records dropped for reason (unpair, revoke, clear, ClearQueuedData). Service: no-op.</summary>
        void ClearAllPending(AbxrObserver.DropReason reason);

        /// <summary>Saves the live session's sign-in (no credentials) so the session can be signed in again later. Service: no-op.</summary>
        void SaveSignIn(SavedSignIn signIn);

        /// <summary>What's waiting to upload. Service: nothing (it sends on its own).</summary>
        global::Abxr.UploadStatus GetUploadStatus();

        /// <summary>For testing only. Pending events (REST: the live session's queue; service: empty, not available on device).</summary>
        List<EventPayload> GetPendingEventsForTesting();
        /// <summary>For testing only. Pending logs (REST: the live session's queue; service: empty).</summary>
        List<LogPayload> GetPendingLogsForTesting();
        /// <summary>For testing only. Pending telemetry (REST: the live session's queue; service: empty).</summary>
        List<TelemetryPayload> GetPendingTelemetryForTesting();
    }
}

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
        /// <summary>Response body. Never empty on failure.</summary>
        public readonly string Body;
        /// <summary>HTTP status, or 0 when no response arrived or the transport has none (ArborInsightsClient).</summary>
        public readonly long StatusCode;

        public AuthTransportResult(bool success, string body, bool isAuthRejectedByApi, long statusCode = 0, bool networkError = false)
        {
            Success = success;
            Body = body;
            IsAuthRejectedByApi = isAuthRejectedByApi;
            StatusCode = statusCode;
            NetworkError = networkError;
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

        /// <summary>Queue or hand off one record. False when it was dropped because the queue is full. recordId is the observer id (0 when nobody observes); REST reports it with the batch's send result.</summary>
        bool AddEvent(string name, Dictionary<string, string> meta, long recordId = 0);
        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool AddTelemetry(string name, Dictionary<string, string> meta, long recordId = 0);
        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool AddLog(string logLevel, string text, Dictionary<string, string> meta, long recordId = 0);
        void ForceSend();

        /// <summary>See <see cref="AddEvent"/>.</summary>
        bool StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy, long recordId = 0);
        IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete);
        IEnumerator StorageDeleteCoroutine(global::Abxr.StorageScope scope, string name, Action<bool> onComplete);

        /// <summary>Flush and release. REST: ForceSend; service: Unbind.</summary>
        void OnQuit();

        /// <summary>Clear pending data/storage (e.g. for StartNewSession). REST: clear queues and report them dropped; Service: no-op.</summary>
        void ClearAllPending();

        /// <summary>For testing only. Pending events (REST: in-memory queue; service: empty, not available on device).</summary>
        List<EventPayload> GetPendingEventsForTesting();
        /// <summary>For testing only. Pending logs (REST: in-memory queue; service: empty).</summary>
        List<LogPayload> GetPendingLogsForTesting();
        /// <summary>For testing only. Pending telemetry (REST: in-memory queue; service: empty).</summary>
        List<TelemetryPayload> GetPendingTelemetryForTesting();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>
    /// Transport abstraction for sending auth, config, data, and storage requests.
    /// Implementations: REST (UnityWebRequest) or ArborInsightsClient (device service).
    /// </summary>
    internal interface IAbxrTransport
    {
        /// <summary>True when this transport is the ArborInsightsClient (service) implementation.</summary>
        bool IsServiceTransport { get; }

        /// <summary>Perform auth request. onComplete(success, responseJson). Auth service parses response and sets ResponseData. Both transports normalize to the same success/failure semantics.</summary>
        IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<bool, string> onComplete);

        /// <summary>Get app config JSON. onComplete(success, configJson).</summary>
        IEnumerator GetConfigCoroutine(Action<bool, string> onComplete);

        void AddEvent(string name, Dictionary<string, string> meta);
        void AddTelemetry(string name, Dictionary<string, string> meta);
        void AddLog(string logLevel, string text, Dictionary<string, string> meta);
        void ForceSend();

        void StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy);
        IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete);
        IEnumerator StorageDeleteCoroutine(global::Abxr.StorageScope scope, string name, Action<bool> onComplete);

        /// <summary>Flush and release. REST: ForceSend; service: Unbind.</summary>
        void OnQuit();

        /// <summary>Clear pending data/storage (e.g. for StartNewSession). REST: clear queues; Service: no-op.</summary>
        void ClearAllPending();

        /// <summary>For testing only. Pending events (REST: in-memory queue; service: empty, not available on device).</summary>
        List<EventPayload> GetPendingEventsForTesting();
        /// <summary>For testing only. Pending logs (REST: in-memory queue; service: empty).</summary>
        List<LogPayload> GetPendingLogsForTesting();
        /// <summary>For testing only. Pending telemetry (REST: in-memory queue; service: empty).</summary>
        List<TelemetryPayload> GetPendingTelemetryForTesting();
    }
}

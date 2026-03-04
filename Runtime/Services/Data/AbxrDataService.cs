using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Data
{
    /// <summary>
    /// Forwards event, telemetry, and log data to the current transport (REST or ArborInsightsClient).
    /// The transport handles queuing and sending; this service is a thin wrapper.
    /// </summary>
    public class AbxrDataService
    {
        private readonly Func<IAbxrTransport> _getTransport;

        internal AbxrDataService(MonoBehaviour coroutineRunner, Func<IAbxrTransport> getTransport)
        {
            _ = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _getTransport = getTransport ?? throw new ArgumentNullException(nameof(getTransport));
        }

        // AbxrTransportRest manages its own send schedule via its internal tick coroutine.
        public void Start() { }
        public void Stop() { }

        public void ForceSend() => _getTransport()?.ForceSend();

        public void AddEvent(string name, Dictionary<string, string> meta)
        {
            _getTransport()?.AddEvent(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            _getTransport()?.AddTelemetry(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            _getTransport()?.AddLog(logLevel ?? "info", text ?? "", meta ?? new Dictionary<string, string>());
        }
    }
}

using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Transport;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Data
{
    /// <summary>
    /// Forwards event, telemetry, and log data to the current transport (REST or ArborInsightsClient).
    /// The transport handles queuing and sending; this service is a thin wrapper that drops data while
    /// nothing should be recorded (an unpaired app with no pairing prompt open).
    /// </summary>
    public class AbxrDataService
    {
        private readonly Func<IAbxrTransport> _getTransport;
        private readonly Func<bool> _isRecording;

        internal AbxrDataService(MonoBehaviour coroutineRunner, Func<IAbxrTransport> getTransport, Func<bool> isRecording = null)
        {
            _ = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _getTransport = getTransport ?? throw new ArgumentNullException(nameof(getTransport));
            _isRecording = isRecording ?? (() => true);
        }

        private IAbxrTransport RecordingTransport => _isRecording() ? _getTransport() : null;

        public void ForceSend() => _getTransport()?.ForceSend();

        public void AddEvent(string name, Dictionary<string, string> meta)
        {
            RecordingTransport?.AddEvent(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            RecordingTransport?.AddTelemetry(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            RecordingTransport?.AddLog(logLevel ?? "info", text ?? "", meta ?? new Dictionary<string, string>());
        }
    }
}

using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Transport;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Data
{
    /// <summary>
    /// Forwards event, telemetry, and log data to the current transport (REST or ArborInsightsClient).
    /// The transport handles queuing and sending; this service drops data while nothing should be recorded (an unpaired
    /// app with no pairing prompt open), and reports each record to <see cref="AbxrObserver"/> when anyone observes.
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
            name ??= "";
            meta ??= new Dictionary<string, string>();
            var transport = RecordingTransport;
            if (!AbxrObserver.IsObserved) { transport?.AddEvent(name, meta); return; }
            long id = AbxrObserver.NextId();
            Report(id, AbxrObserver.RecordKind.Event, name, null, meta, transport, transport?.AddEvent(name, meta, id));
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            name ??= "";
            meta ??= new Dictionary<string, string>();
            var transport = RecordingTransport;
            if (!AbxrObserver.IsObserved) { transport?.AddTelemetry(name, meta); return; }
            long id = AbxrObserver.NextId();
            Report(id, AbxrObserver.RecordKind.Telemetry, name, null, meta, transport, transport?.AddTelemetry(name, meta, id));
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            logLevel ??= "info";
            text ??= "";
            meta ??= new Dictionary<string, string>();
            var transport = RecordingTransport;
            if (!AbxrObserver.IsObserved) { transport?.AddLog(logLevel, text, meta); return; }
            long id = AbxrObserver.NextId();
            Report(id, AbxrObserver.RecordKind.Log, text, logLevel, meta, transport, transport?.AddLog(logLevel, text, meta, id));
        }

        /// <summary>
        /// Raises record created, then handed to service when the service transport took it: it sends on its own, so
        /// the SDK never learns more. accepted is null when nothing was recording.
        /// </summary>
        internal static void Report(long id, AbxrObserver.RecordKind kind, string name, string level, Dictionary<string, string> data,
            IAbxrTransport transport, bool? accepted, AbxrObserver.DropReason notAcceptedReason = AbxrObserver.DropReason.NotRecording)
        {
            var drop = accepted == null ? notAcceptedReason
                : accepted.Value ? AbxrObserver.DropReason.None
                : AbxrObserver.DropReason.QueueFull;
            AbxrObserver.Created(id, kind, name, level, data, drop);
            if (drop == AbxrObserver.DropReason.None && transport.IsServiceTransport)
                AbxrObserver.Sent(id, AbxrObserver.SendStatus.HandedToService);
        }
    }
}

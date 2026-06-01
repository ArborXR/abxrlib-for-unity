using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services;

namespace AbxrLib.Runtime.Services.Data
{
    /// <summary>
    /// Forwards event, telemetry, and log data to the REST service.
    /// The REST service handles queuing and sending; this service is a thin wrapper.
    /// </summary>
    public class AbxrDataService
    {
        private readonly AbxrRestService _restService;

        internal AbxrDataService(AbxrRestService restService)
        {
            _restService = restService ?? throw new ArgumentNullException(nameof(restService));
        }

        public void ForceSend() => _restService.ForceSend();

        public void AddEvent(string name, Dictionary<string, string> meta)
        {
            _restService.AddEvent(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            _restService.AddTelemetry(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            _restService.AddLog(logLevel ?? "info", text ?? "", meta ?? new Dictionary<string, string>());
        }
    }
}

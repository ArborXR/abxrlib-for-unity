using System.Collections.Generic;
using System.Globalization;
using Abxr.Runtime.Core;
using UnityEngine;

namespace Abxr.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackSystemInfo : MonoBehaviour
    {
        private static int _lastFrameCount;
        private static float _lastTime;
        private static float _systemInfoTimer = 1f;
        private static float _frameRateTimer = 1f;
        private static bool _tracking;
    
        private void Start()
        {
            if (!Configuration.Instance.disableAutomaticTelemetry) _tracking = true;
        }
    
        private void Update()
        {
            _systemInfoTimer += Time.deltaTime;
            _frameRateTimer += Time.deltaTime;
            if (_systemInfoTimer >= Configuration.Instance.telemetryTrackingPeriodSeconds) CheckSystemInfo();
            if (_frameRateTimer >= Configuration.Instance.frameRateTrackingPeriodSeconds) CheckFrameRate();
        }

        public static void StartTracking() => _tracking = true;

        public static void SendAll()
        {
            CheckSystemInfo();
            CheckFrameRate();
        }

        private static void CheckSystemInfo()
        {
            if (_systemInfoTimer == 0f) return; // Make sure not to send twice in the same update
            _systemInfoTimer = 0; // Reset timer
            if (!_tracking) return;
        
            var batteryData = new Dictionary<string, string>
            {
                ["Percentage"] = (int)(SystemInfo.batteryLevel * 100 + 0.5) + "%",
                ["Status"] = SystemInfo.batteryStatus.ToString()
            };
            Core.Abxr.TelemetryEntry("Battery", batteryData);
        
            var memoryData = new Dictionary<string, string>
            {
                ["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1000000 + " MB",
                ["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1000000 + " MB",
                ["Total Unused Reserved"] = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1000000 + " MB"
            };
            Core.Abxr.TelemetryEntry("Memory", memoryData);
        }
    
        private static void CheckFrameRate()
        {
            if (_frameRateTimer == 0f) return; // Make sure not to send twice in the same update
            _frameRateTimer = 0; // Reset timer
            if (!_tracking) return;
        
            float timeDiff = Time.time - _lastTime;
            if (timeDiff == 0) return;
        
            float frameRate = (Time.frameCount - _lastFrameCount) / timeDiff;
            var telemetryData = new Dictionary<string, string>
            {
                ["Per Second"] = frameRate.ToString(CultureInfo.InvariantCulture)
            };
            Core.Abxr.TelemetryEntry("Frame Rate", telemetryData);
            _lastFrameCount = Time.frameCount;
            _lastTime = Time.time;
        }
    }
}
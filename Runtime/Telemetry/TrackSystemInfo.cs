using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackSystemInfo : MonoBehaviour
    {
        private static int _lastFrameCount;
        private static float _lastTime;
        private static float _systemInfoTimer = 1f;
        private static float _frameRateTimer = 1f;
        private static bool _tracking;
        
        // Reusable dictionaries to avoid creating new objects every frame
        private static readonly Dictionary<string, string> _batteryData = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _memoryData = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _frameRateData = new Dictionary<string, string>();
    
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
        
            // Clear and reuse battery data dictionary
            _batteryData.Clear();
            _batteryData["Percentage"] = (int)(SystemInfo.batteryLevel * 100 + 0.5) + "%";
            _batteryData["Status"] = SystemInfo.batteryStatus.ToString();
            Abxr.Telemetry("Battery", _batteryData);
        
            // Clear and reuse memory data dictionary
            _memoryData.Clear();
            _memoryData["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1000000 + " MB";
            _memoryData["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1000000 + " MB";
            _memoryData["Total Unused Reserved"] = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1000000 + " MB";
            Abxr.Telemetry("Memory", _memoryData);
        }
    
        private static void CheckFrameRate()
        {
            if (_frameRateTimer == 0f) return; // Make sure not to send twice in the same update
            _frameRateTimer = 0; // Reset timer
            if (!_tracking) return;
        
            float timeDiff = Time.time - _lastTime;
            if (timeDiff == 0) return;
        
            float frameRate = (Time.frameCount - _lastFrameCount) / timeDiff;
            
            // Clear and reuse frame rate data dictionary
            _frameRateData.Clear();
            _frameRateData["Per Second"] = frameRate.ToString(CultureInfo.InvariantCulture);
            Abxr.Telemetry("Frame Rate", _frameRateData);
            
            _lastFrameCount = Time.frameCount;
            _lastTime = Time.time;
        }
    }
}
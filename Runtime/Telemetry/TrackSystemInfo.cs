using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackSystemInfo : MonoBehaviour, System.IDisposable
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

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clear static dictionaries to prevent memory leaks
                _batteryData?.Clear();
                _memoryData?.Clear();
                _frameRateData?.Clear();
                
                // Reset tracking state
                _tracking = false;
            }
        }
    
        private void Update()
        {
            _systemInfoTimer += Time.deltaTime;
            _frameRateTimer += Time.deltaTime;
            if (_systemInfoTimer >= Configuration.Instance.telemetryTrackingPeriodSeconds) CheckSystemInfo();
            if (_frameRateTimer >= Configuration.Instance.frameRateTrackingPeriodSeconds) CheckFrameRate();
            
            // Periodically clean up dictionaries to prevent memory leaks
            if (Time.frameCount % 1000 == 0) // Every 1000 frames
            {
                CleanupDictionaries();
            }
        }

        public static void StartTracking() => _tracking = true;

        public static void SendAll()
        {
            CheckSystemInfo();
            CheckFrameRate();
        }

        private static void CheckSystemInfo()
        {
            if (!_tracking) return;
            
            // Reset timer first to prevent duplicate calls in the same frame
            _systemInfoTimer = 0f;
        
            // Clear and reuse battery data dictionary
            _batteryData.Clear();
            _batteryData["Percentage"] = (int)(SystemInfo.batteryLevel * 100 + 0.5) + "%";
            _batteryData["Status"] = SystemInfo.batteryStatus.ToString();
            Abxr.Telemetry("Battery", _batteryData);
        
            // Clear and reuse memory data dictionary with Unity version compatibility
            _memoryData.Clear();
            try
            {
                // Check if newer Profiler methods are available (Unity 2020.1+)
                var profilerType = typeof(UnityEngine.Profiling.Profiler);
                var getTotalAllocatedMethod = profilerType.GetMethod("GetTotalAllocatedMemoryLong");
                
                if (getTotalAllocatedMethod != null)
                {
                    // Use newer methods (Unity 2020.1+)
                    _memoryData["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1000000 + " MB";
                    _memoryData["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1000000 + " MB";
                    _memoryData["Total Unused Reserved"] = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1000000 + " MB";
                }
                else
                {
                    // Fallback to older methods (Unity 2019.x and earlier)
                    _memoryData["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory() / 1000000 + " MB";
                    _memoryData["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemory() / 1000000 + " MB";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: Memory profiling not available: {ex.Message}");
                _memoryData["Total Allocated"] = "N/A";
                _memoryData["Total Reserved"] = "N/A";
                _memoryData["Total Unused Reserved"] = "N/A";
            }
            Abxr.Telemetry("Memory", _memoryData);
        }
    
        private static void CheckFrameRate()
        {
            if (!_tracking) return;
            
            // Reset timer first to prevent duplicate calls in the same frame
            _frameRateTimer = 0f;
        
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
        
        /// <summary>
        /// Cleans up dictionaries to prevent memory leaks by limiting their size
        /// </summary>
        private static void CleanupDictionaries()
        {
            const int maxDictionarySize = 50; // Reasonable limit for telemetry data
            
            if (_batteryData.Count > maxDictionarySize)
            {
                _batteryData.Clear();
                Debug.LogWarning("AbxrLib: Battery data dictionary was growing too large, cleared to prevent memory leak");
            }
            
            if (_memoryData.Count > maxDictionarySize)
            {
                _memoryData.Clear();
                Debug.LogWarning("AbxrLib: Memory data dictionary was growing too large, cleared to prevent memory leak");
            }
            
            if (_frameRateData.Count > maxDictionarySize)
            {
                _frameRateData.Clear();
                Debug.LogWarning("AbxrLib: Frame rate data dictionary was growing too large, cleared to prevent memory leak");
            }
        }
    }
}
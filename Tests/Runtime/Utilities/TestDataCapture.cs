/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * Test Data Capture for ABXRLib Tests
 * 
 * Captures events, logs, and telemetry sent by the library for verification
 * in tests without requiring real network transmission.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Captures data sent by ABXRLib for test verification
    /// </summary>
    public class TestDataCapture
    {
        public class CapturedEvent
        {
            public string name;
            public Dictionary<string, string> meta;
            public Vector3? position;
            public DateTime timestamp;
            public float? duration;
        }
        
        public class CapturedLog
        {
            public string message;
            public string level;
            public Dictionary<string, string> meta;
            public DateTime timestamp;
        }
        
        public class CapturedTelemetry
        {
            public string name;
            public Dictionary<string, string> meta;
            public DateTime timestamp;
        }
        
        public List<CapturedEvent> Events { get; private set; } = new List<CapturedEvent>();
        public List<CapturedLog> Logs { get; private set; } = new List<CapturedLog>();
        public List<CapturedTelemetry> Telemetry { get; private set; } = new List<CapturedTelemetry>();
        
        public int EventCount => Events.Count;
        public int LogCount => Logs.Count;
        public int TelemetryCount => Telemetry.Count;
        
        /// <summary>
        /// Captures an event for verification
        /// </summary>
        public void CaptureEvent(string name, Dictionary<string, string> meta = null, Vector3? position = null, float? duration = null)
        {
            Events.Add(new CapturedEvent
            {
                name = name,
                meta = meta ?? new Dictionary<string, string>(),
                position = position,
                timestamp = DateTime.UtcNow,
                duration = duration
            });
            
            Debug.Log($"TestDataCapture: Captured event '{name}' with {meta?.Count ?? 0} metadata entries");
        }
        
        /// <summary>
        /// Captures a log entry for verification
        /// </summary>
        public void CaptureLog(string message, string level, Dictionary<string, string> meta = null)
        {
            Logs.Add(new CapturedLog
            {
                message = message,
                level = level,
                meta = meta ?? new Dictionary<string, string>(),
                timestamp = DateTime.UtcNow
            });
            
            Debug.Log($"TestDataCapture: Captured log '{level}' - {message}");
        }
        
        /// <summary>
        /// Captures telemetry data for verification
        /// </summary>
        public void CaptureTelemetry(string name, Dictionary<string, string> meta = null)
        {
            Telemetry.Add(new CapturedTelemetry
            {
                name = name,
                meta = meta ?? new Dictionary<string, string>(),
                timestamp = DateTime.UtcNow
            });
            
            Debug.Log($"TestDataCapture: Captured telemetry '{name}' with {meta?.Count ?? 0} metadata entries");
        }
        
        /// <summary>
        /// Verifies that an event with the given name was captured
        /// </summary>
        public bool WasEventCaptured(string eventName)
        {
            return Events.Exists(e => e.name == eventName);
        }
        
        /// <summary>
        /// Verifies that an event with the given name and metadata was captured
        /// </summary>
        public bool WasEventCaptured(string eventName, Dictionary<string, string> expectedMeta)
        {
            return Events.Exists(e => 
                e.name == eventName && 
                MetaMatches(e.meta, expectedMeta));
        }
        
        /// <summary>
        /// Verifies that a log with the given level was captured
        /// </summary>
        public bool WasLogCaptured(string level)
        {
            return Logs.Exists(l => l.level == level);
        }
        
        /// <summary>
        /// Verifies that telemetry with the given name was captured
        /// </summary>
        public bool WasTelemetryCaptured(string telemetryName)
        {
            return Telemetry.Exists(t => t.name == telemetryName);
        }
        
        /// <summary>
        /// Gets the most recent event with the given name
        /// </summary>
        public CapturedEvent GetLastEvent(string eventName)
        {
            return Events.FindLast(e => e.name == eventName);
        }
        
        /// <summary>
        /// Gets the most recent log with the given level
        /// </summary>
        public CapturedLog GetLastLog(string level)
        {
            return Logs.FindLast(l => l.level == level);
        }
        
        /// <summary>
        /// Gets the most recent telemetry with the given name
        /// </summary>
        public CapturedTelemetry GetLastTelemetry(string telemetryName)
        {
            return Telemetry.FindLast(t => t.name == telemetryName);
        }
        
        /// <summary>
        /// Gets all events with the given name
        /// </summary>
        public List<CapturedEvent> GetEvents(string eventName)
        {
            return Events.FindAll(e => e.name == eventName);
        }
        
        /// <summary>
        /// Gets all logs with the given level
        /// </summary>
        public List<CapturedLog> GetLogs(string level)
        {
            return Logs.FindAll(l => l.level == level);
        }
        
        /// <summary>
        /// Gets all telemetry with the given name
        /// </summary>
        public List<CapturedTelemetry> GetTelemetry(string telemetryName)
        {
            return Telemetry.FindAll(t => t.name == telemetryName);
        }
        
        /// <summary>
        /// Clears all captured data
        /// </summary>
        public void Clear()
        {
            Events.Clear();
            Logs.Clear();
            Telemetry.Clear();
            Debug.Log("TestDataCapture: Cleared all captured data");
        }
        
        /// <summary>
        /// Checks if metadata matches expected values
        /// </summary>
        private bool MetaMatches(Dictionary<string, string> actual, Dictionary<string, string> expected)
        {
            if (expected == null) return true;
            if (actual == null) return false;
            
            foreach (var kvp in expected)
            {
                if (!actual.ContainsKey(kvp.Key) || actual[kvp.Key] != kvp.Value)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Prints summary of captured data for debugging
        /// </summary>
        public void PrintSummary()
        {
            Debug.Log($"TestDataCapture Summary: {EventCount} events, {LogCount} logs, {TelemetryCount} telemetry entries");
            
            if (EventCount > 0)
            {
                Debug.Log("Events:");
                foreach (var evt in Events)
                {
                    Debug.Log($"  - {evt.name} ({evt.meta.Count} metadata entries)");
                }
            }
            
            if (LogCount > 0)
            {
                Debug.Log("Logs:");
                foreach (var log in Logs)
                {
                    Debug.Log($"  - {log.level}: {log.message}");
                }
            }
            
            if (TelemetryCount > 0)
            {
                Debug.Log("Telemetry:");
                foreach (var tel in Telemetry)
                {
                    Debug.Log($"  - {tel.name} ({tel.meta.Count} metadata entries)");
                }
            }
        }
    }
}

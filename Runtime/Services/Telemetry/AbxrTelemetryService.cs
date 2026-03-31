using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;
using UnityEngine.XR;

namespace AbxrLib.Runtime.Services.Telemetry
{
    internal sealed class AbxrTelemetryService
    {
        private readonly MonoBehaviour _runner;
        private readonly Func<bool> _authStartedForTelemetry;
        private List<Coroutine> _coroutines;
        
        private const string HmdName = "Head";
        private const string RightControllerName = "Right Controller";
        private const string LeftControllerName = "Left Controller";
        
        private InputDevice _rightController;
        private InputDevice _leftController;
        private InputDevice _hmd;
        
        private readonly Dictionary<InputFeatureUsage<bool>, bool> _rightTriggerValues = new();
        private readonly Dictionary<InputFeatureUsage<bool>, bool> _leftTriggerValues = new();

        // Cached once at class load: avoids reflection overhead every 10 s
        private static readonly bool _hasLongMemoryApi =
            typeof(UnityEngine.Profiling.Profiler).GetMethod("GetTotalAllocatedMemoryLong") != null;

        // Reused to avoid allocations in telemetry hot paths
        private readonly Dictionary<string, string> _batteryData = new Dictionary<string, string>(2);
        private readonly Dictionary<string, string> _memoryData = new Dictionary<string, string>(4);
        private readonly Dictionary<string, string> _positionData = new Dictionary<string, string>(3);
        private readonly Dictionary<string, string> _rotationData = new Dictionary<string, string>(4);
        private readonly Dictionary<string, string> _triggerData = new Dictionary<string, string>(1);

        public AbxrTelemetryService(MonoBehaviour runner, Func<bool> authStartedForTelemetry)
        {
            _runner = runner;
            _authStartedForTelemetry = authStartedForTelemetry ?? throw new ArgumentNullException(nameof(authStartedForTelemetry));
        }

        public void Start()
        {
            _coroutines = new List<Coroutine>();
            _coroutines.Add(_runner.StartCoroutine(StartWhenAuthBegins()));
        }

        private IEnumerator StartWhenAuthBegins()
        {
            yield return new WaitUntil(() => _authStartedForTelemetry());
            try
            {
                _leftController  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                InputDevices.deviceConnected += RegisterDevice;
            }
            catch (System.Exception ex)
            {
                Logcat.Warning($"TrackInputDevices - Failed to initialize XR device tracking: {ex.Message}");
            }

            var sysInfoWait = new WaitForSeconds(Configuration.Instance.telemetryTrackingPeriodSeconds);
            var locationWait = new WaitForSeconds(Configuration.Instance.positionTrackingPeriodSeconds);
            _coroutines.Add(_runner.StartCoroutine(SystemInfoLoop(sysInfoWait)));
            if (Configuration.Instance.headsetTracking)
            {
                _coroutines.Add(_runner.StartCoroutine(LocationDataLoop(locationWait)));
                _coroutines.Add(_runner.StartCoroutine(TriggerCheckLoop()));
            }
        }

        public void Stop()
        {
            if (_coroutines != null && _runner != null)
            {
                foreach (var coroutine in _coroutines)
                {
                    if (coroutine != null) _runner.StopCoroutine(coroutine);
                }
            }
            _coroutines = null;

            InputDevices.deviceConnected -= RegisterDevice;
            
            _rightController = default;
            _leftController = default;
            _hmd = default;
        }

        private IEnumerator SystemInfoLoop(WaitForSeconds wait)
        {
            while (true)
            {
                RecordSystemInfo();
                yield return wait;
            }
        }

        private IEnumerator LocationDataLoop(WaitForSeconds wait)
        {
            while (true)
            {
                RecordLocationData();
                yield return wait;
            }
        }
        
        private static readonly WaitForSeconds TriggerCheckInterval = new WaitForSeconds(0.25f);

        private IEnumerator TriggerCheckLoop()
        {
            while (true)
            {
                CheckTriggers();
                yield return TriggerCheckInterval;
            }
        }
        
        public void RecordSystemInfo()
        {
            if (!_authStartedForTelemetry()) return;
            _batteryData.Clear();
            _batteryData["Percentage"] = (int)(SystemInfo.batteryLevel * 100 + 0.5) + "%";
            _batteryData["Status"] = SystemInfo.batteryStatus.ToString();
            Abxr.Telemetry("Battery", _batteryData);

            _memoryData.Clear();
            try
            {
                if (_hasLongMemoryApi)
                {
                    // Unity 2020.1+
                    _memoryData["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1000000 + " MB";
                    _memoryData["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1000000 + " MB";
                    _memoryData["Total Unused Reserved"] = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1000000 + " MB";
                }
                else
                {
                    // Unity 2019.x and earlier
                    _memoryData["Total Allocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory() / 1000000 + " MB";
                    _memoryData["Total Reserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemory() / 1000000 + " MB";
                }
            }
            catch (System.Exception ex)
            {
                Logcat.Warning($"Memory profiling not available: {ex.Message}");
                _memoryData["Total Allocated"] = "N/A";
                _memoryData["Total Reserved"] = "N/A";
                _memoryData["Total Unused Reserved"] = "N/A";
            }
            
            Abxr.Telemetry("Memory", _memoryData);
        }
        
        public void RecordLocationData()
        {
            if (!_authStartedForTelemetry()) return;
            RecordLocationData(_rightController);
            RecordLocationData(_leftController);
            RecordLocationData(_hmd);
        }

        private void RecordLocationData(InputDevice device)
        {
            if (!device.isValid) return;
        
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            string deviceName = HmdName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) deviceName = RightControllerName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) deviceName = LeftControllerName;

            _positionData.Clear();
            _positionData["x"] = position.x.ToString(CultureInfo.InvariantCulture);
            _positionData["y"] = position.y.ToString(CultureInfo.InvariantCulture);
            _positionData["z"] = position.z.ToString(CultureInfo.InvariantCulture);
            Abxr.Telemetry(deviceName + " Position", _positionData);

            _rotationData.Clear();
            _rotationData["x"] = rotation.x.ToString(CultureInfo.InvariantCulture);
            _rotationData["y"] = rotation.y.ToString(CultureInfo.InvariantCulture);
            _rotationData["z"] = rotation.z.ToString(CultureInfo.InvariantCulture);
            _rotationData["w"] = rotation.w.ToString(CultureInfo.InvariantCulture);
            Abxr.Telemetry(deviceName + " Rotation", _rotationData);
        }

        private void CheckTriggers()
        {
            if (!_authStartedForTelemetry()) return;
            CheckTriggers(CommonUsages.primaryButton);
            CheckTriggers(CommonUsages.secondaryButton);
            CheckTriggers(CommonUsages.triggerButton);
            CheckTriggers(CommonUsages.gripButton);
        }

        private void CheckTriggers(InputFeatureUsage<bool> trigger)
        {
            if (_rightController.isValid)
            {
                _rightController.TryGetFeatureValue(trigger, out bool isPressed);
                _rightTriggerValues.TryGetValue(trigger, out bool wasPressed);
                if (isPressed != wasPressed)
                {
                    _triggerData.Clear();
                    _triggerData[trigger.name] = isPressed ? "Pressed" : "Released";
                    Abxr.Telemetry($"Right Controller {trigger.name}", _triggerData);
                    _rightTriggerValues[trigger] = isPressed;
                }
            }

            if (_leftController.isValid)
            {
                _leftController.TryGetFeatureValue(trigger, out bool isPressed);
                _leftTriggerValues.TryGetValue(trigger, out bool wasPressed);
                if (isPressed != wasPressed)
                {
                    _triggerData.Clear();
                    _triggerData[trigger.name] = isPressed ? "Pressed" : "Released";
                    Abxr.Telemetry($"Left Controller {trigger.name}", _triggerData);
                    _leftTriggerValues[trigger] = isPressed;
                }
            }
        }
        
        // Listen for hot-swaps and handle reconnects
        private void RegisterDevice(InputDevice device)
        {
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) _leftController = device;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) _rightController = device;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted)) _hmd = device;
        }
    }
}
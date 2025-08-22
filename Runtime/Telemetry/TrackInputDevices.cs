using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;
using UnityEngine.XR;

namespace AbxrLib.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackInputDevices : MonoBehaviour
    {
        private static float _timer = 1f;
    
        private static InputDevice _rightController;
        private static InputDevice _leftController;
        private static InputDevice _hmd;

        private const string HmdName = "Head";
        private const string RightControllerName = "Right Controller";
        private const string LeftControllerName = "Left Controller";

        private readonly Dictionary<InputFeatureUsage<bool>, bool> _rightTriggerValues = new();
        private readonly Dictionary<InputFeatureUsage<bool>, bool> _leftTriggerValues = new();

        private void Start()
        {
            _leftController  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            InputDevices.deviceConnected += RegisterDevice;
        }
    
        private void Update()
        {
            CheckTriggers(); // Always check for triggers
            _timer += Time.deltaTime;
            if (_timer >= Configuration.Instance.positionTrackingPeriodSeconds) SendLocationData();
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= RegisterDevice;
        }

        // Listen for hot-swaps and handle reconnects
        private static void RegisterDevice(InputDevice device)
        {
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) _leftController = device;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) _rightController = device;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted)) _hmd = device;
        }

        public static void SendLocationData()
        {
            if (_timer == 0f) return; // Make sure not to send twice in the same update
            _timer = 0; // Reset timer
            SendLocationData(_rightController);
            SendLocationData(_leftController);
            SendLocationData(_hmd);
        }

        private static void SendLocationData(InputDevice device)
        {
            if (!device.isValid) return;
        
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            string deviceName = HmdName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) deviceName = RightControllerName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) deviceName = LeftControllerName;

            var positionDict = new Dictionary<string, string>
            {
                ["x"] = position.x.ToString(CultureInfo.InvariantCulture),
                ["y"] = position.y.ToString(CultureInfo.InvariantCulture),
                ["z"] = position.z.ToString(CultureInfo.InvariantCulture)
            };
            var rotationDict = new Dictionary<string, string>
            {
                ["x"] = rotation.x.ToString(CultureInfo.InvariantCulture),
                ["y"] = rotation.y.ToString(CultureInfo.InvariantCulture),
                ["z"] = rotation.z.ToString(CultureInfo.InvariantCulture),
                ["w"] = rotation.w.ToString(CultureInfo.InvariantCulture)
            };
            Core.Abxr.TelemetryEntry(deviceName + " Position", positionDict);
            Core.Abxr.TelemetryEntry(deviceName + " Rotation", rotationDict);
        }

        private void CheckTriggers()
        {
            CheckTriggers(CommonUsages.primaryButton);
            CheckTriggers(CommonUsages.secondaryButton);
            CheckTriggers(CommonUsages.triggerButton);
            CheckTriggers(CommonUsages.gripButton);
        }

        private void CheckTriggers(InputFeatureUsage<bool> trigger)
        {
            if (_rightController.isValid)
            {
                _rightController.TryGetFeatureValue(trigger, out bool pressed);
                _rightTriggerValues.TryGetValue(trigger, out bool current);
                if (pressed != current)
                {
                    string action = "Pressed";
                    if (!pressed) action = "Released";
                    var telemetryData = new Dictionary<string, string>
                    {
                        [trigger.name] = action
                    };
                    Core.Abxr.TelemetryEntry($"Right Controller {trigger.name}", telemetryData);
                    _rightTriggerValues[trigger] = pressed;
                }
            }

            if (_leftController.isValid)
            {
                _leftController.TryGetFeatureValue(trigger, out bool pressed);
                _leftTriggerValues.TryGetValue(trigger, out bool current);
                if (pressed != current)
                {
                    string action = "Pressed";
                    if (!pressed) action = "Released";
                    var telemetryData = new Dictionary<string, string>
                    {
                        [trigger.name] = action
                    };
                    Core.Abxr.TelemetryEntry($"Left Controller {trigger.name}", telemetryData);
                    _leftTriggerValues[trigger] = pressed;
                }
            }
        }
    }
}
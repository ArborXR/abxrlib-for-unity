using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(100)] // Doesn't matter when this one runs
public class TrackInputDevices : MonoBehaviour
{
    public float positionUpdateIntervalSeconds = (float)(60.0 / Configuration.Instance.trackingUpdatesPerMinute);
    
    private InputDevice _rightController;
    private InputDevice _leftController;
    private InputDevice _hmd;

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
        
        InvokeRepeating(nameof(UpdateLocationData), 0, positionUpdateIntervalSeconds);
        
        InputDevices.deviceConnected += RegisterDevice;
    }
    
    private void Update()
    {
        CheckTriggers(); // Always check for triggers
    }

    private void OnDestroy()
    {
        InputDevices.deviceConnected -= RegisterDevice;
    }

    // Listen for hot-swaps and handle reconnects
    private void RegisterDevice(InputDevice device)
    {
        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) _leftController = device;
        if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) _rightController = device;
        if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted)) _hmd = device;
    }

    private void UpdateLocationData()
    {
        UpdateLocationData(_rightController);
        UpdateLocationData(_leftController);
        UpdateLocationData(_hmd);
    }

    private static void UpdateLocationData(InputDevice device)
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
        Abxr.TelemetryEntry(deviceName + " Position", positionDict);
        Abxr.TelemetryEntry(deviceName + " Rotation", rotationDict);
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
                Abxr.TelemetryEntry($"Right Controller {trigger.name}", telemetryData);
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
                Abxr.TelemetryEntry($"Left Controller {trigger.name}", telemetryData);
                _leftTriggerValues[trigger] = pressed;
            }
        }
    }
}
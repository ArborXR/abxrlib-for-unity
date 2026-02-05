using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;
using UnityEngine.XR;

namespace AbxrLib.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackInputDevices : MonoBehaviour, System.IDisposable
    {
        private static float _timer = 1f;
    
        private static InputDevice _rightController;
        private static InputDevice _leftController;
        private static InputDevice _hmd;

        private const string _hmdName = "Head";
        private const string _rightControllerName = "Right Controller";
        private const string _leftControllerName = "Left Controller";

        private readonly Dictionary<InputFeatureUsage<bool>, bool> _rightTriggerValues = new();
        private readonly Dictionary<InputFeatureUsage<bool>, bool> _leftTriggerValues = new();

        private void Start()
        {
            try
            {
                _leftController  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                InputDevices.deviceConnected += RegisterDevice;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: TrackInputDevices - Failed to initialize XR device tracking: {ex.Message}");
            }
        }
    
        private void Update()
        {
            CheckTriggers(); // Always check for triggers
            _timer += Time.deltaTime;
            if (_timer >= Configuration.Instance.positionTrackingPeriodSeconds) SendLocationData();
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
                // Unsubscribe from events
                InputDevices.deviceConnected -= RegisterDevice;
                
                // Clear static references
                _rightController = default(InputDevice);
                _leftController = default(InputDevice);
                _hmd = default(InputDevice);
                
                // Clear dictionaries
                _rightTriggerValues?.Clear();
                _leftTriggerValues?.Clear();
            }
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
            
            // Try to refresh device references if they're not valid (handles late initialization)
            if (!_hmd.isValid || !_rightController.isValid || !_leftController.isValid)
            {
                RefreshDeviceReferences();
            }
            
            SendLocationData(_rightController);
            SendLocationData(_leftController);
            SendLocationData(_hmd);
            
            // Fallback: If HMD is not available (e.g., in Unity Editor), use Camera.main as fallback
            if (!_hmd.isValid)
            {
                SendCameraFallbackData();
            }
        }
        
        /// <summary>
        /// Fallback method to send camera/headset position when XR devices aren't available.
        /// Used in Unity Editor or when XR isn't initialized.
        /// Uses the shared camera finding logic from TrackTargetGaze for consistency.
        /// </summary>
        private static void SendCameraFallbackData()
        {
            // Use the shared camera finding method from TrackTargetGaze for consistency
            Transform cameraTransform = TrackTargetGaze.FindCameraTransform();
            if (cameraTransform == null) return;
            
            Vector3 position = cameraTransform.position;
            Quaternion rotation = cameraTransform.rotation;
            
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
            Abxr.Telemetry(_hmdName + " Position", positionDict);
            Abxr.Telemetry(_hmdName + " Rotation", rotationDict);
        }
        
        /// <summary>
        /// Refreshes device references by querying XR input devices again.
        /// Useful when devices connect after initial Start() call.
        /// </summary>
        private static void RefreshDeviceReferences()
        {
            try
            {
                if (!_leftController.isValid)
                {
                    _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                }
                if (!_rightController.isValid)
                {
                    _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                }
                if (!_hmd.isValid)
                {
                    _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                }
            }
            catch (System.Exception)
            {
                // Silently fail - devices might not be available yet
                // This is expected in Editor or when XR is not initialized
            }
        }

        private static void SendLocationData(InputDevice device)
        {
            if (!device.isValid) return;
        
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            string deviceName = _hmdName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right)) deviceName = _rightControllerName;
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left)) deviceName = _leftControllerName;

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
            Abxr.Telemetry(deviceName + " Position", positionDict);
            Abxr.Telemetry(deviceName + " Rotation", rotationDict);
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
                _rightController.TryGetFeatureValue(trigger, out bool isPressed);
                _rightTriggerValues.TryGetValue(trigger, out bool wasPressed);
                if (isPressed != wasPressed)
                {
                    string action = "Pressed";
                    if (!isPressed) action = "Released";
                    var telemetryData = new Dictionary<string, string>
                    {
                        [trigger.name] = action
                    };
                    Abxr.Telemetry($"Right Controller {trigger.name}", telemetryData);
                    _rightTriggerValues[trigger] = isPressed;
                }
            }

            if (_leftController.isValid)
            {
                _leftController.TryGetFeatureValue(trigger, out bool isPressed);
                _leftTriggerValues.TryGetValue(trigger, out bool wasPressed);
                if (isPressed != wasPressed)
                {
                    string action = "Pressed";
                    if (!isPressed) action = "Released";
                    var telemetryData = new Dictionary<string, string>
                    {
                        [trigger.name] = action
                    };
                    Abxr.Telemetry($"Left Controller {trigger.name}", telemetryData);
                    _leftTriggerValues[trigger] = isPressed;
                }
            }
        }
    }
}
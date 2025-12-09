using System.Collections.Generic;
using AbxrLib.Runtime.UI.ExitPoll;
using UnityEngine;
using UnityEngine.XR;

namespace AbxrLib.Runtime.Common
{
    public class HeadsetDetector : MonoBehaviour
    {
        private const float _checkIntervalSeconds = 1f;
        private const string _newSessionString = "No, I need to log in as someone else.";
        private const string _continueSessionString = "Yes, I'd like to continue the current session.";
    
        private bool _sensorStatus = true;
        private float _lastCheckTime;

        private void Start()
        {
            try
            {
                // Check if XR is available before trying to get devices
                if (!IsXRAvailable())
                {
                    Debug.LogWarning("AbxrLib: HeadsetDetector - XR not available, headset detection disabled");
                    return;
                }
                
                //Debug.Log("AbxrLib: HeadsetDetector - XR device tracking initialized successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: HeadsetDetector - Failed to initialize XR device tracking: {ex.Message}");
            }
        }

        private void Update()
        {
            // Check at intervals to avoid excessive calls
            if (Time.time - _lastCheckTime >= _checkIntervalSeconds)
            {
                bool currentStatus = CheckProximitySensor();
                if (_sensorStatus && !currentStatus)
                {
                    OnHeadsetRemovedDetected();
                }
                else if (!_sensorStatus && currentStatus)
                {
                    OnHeadsetPutOnDetected();
                }
            
                _sensorStatus = currentStatus;
                _lastCheckTime = Time.time;
            }
        }
    
        private static bool CheckProximitySensor()
        {
            try
            {
                // Check if XR is available
                if (!IsXRAvailable()) 
                {
                    Debug.LogWarning("AbxrLib: XR not available, assuming non-VR mode");
                    return true; // Fallback to assuming headset is on
                }
                
                InputDevice headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (headset.isValid)
                {
                    // Try different proximity detection methods for different VR platforms
                    
                    // Method 1: Standard userPresence (Oculus, most OpenXR headsets)
                    if (headset.TryGetFeatureValue(CommonUsages.userPresence, out bool userPresent))
                    {
                        return userPresent;
                    }
                    
                    // Method 2: PICO-specific proximity detection
                    if (headset.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
                    {
                        // For PICO headsets, isTracked can indicate proximity
                        return isTracked;
                    }
                    
                    // Method 3: Try to detect PICO-specific features
                    if (IsPICOHeadset(headset))
                    {
                        return CheckPICOProximity(headset);
                    }
                    
                    // Method 4: Fallback to device position tracking
                    if (headset.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                    {
                        // If we can get position, assume headset is being worn
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning("AbxrLib: No valid headset device found");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: Proximity sensor check failed: {ex.Message}");
            }
        
            // Fallback: assume headset is on if no proximity data
            return true;
        }
        
        private static bool IsXRAvailable()
        {
            try
            {
#if UNITY_2020_1_OR_NEWER
                return UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName) && UnityEngine.XR.XRSettings.loadedDeviceName != "None";
#else
                return UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.loadedDeviceName != "None";
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: XR availability check failed: {ex.Message}");
                return false;
            }
        }
        
        private static bool IsPICOHeadset(InputDevice headset)
        {
            try
            {
                // Check if this is a PICO headset by name or manufacturer
                string deviceName = headset.name.ToLower();
                return deviceName.Contains("pico") || deviceName.Contains("neo");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: PICO headset detection failed: {ex.Message}");
                return false;
            }
        }
        
        private static bool CheckPICOProximity(InputDevice headset)
        {
            try
            {
                // PICO-specific proximity detection methods
                
                // Method 1: Check if device is being tracked (PICO specific)
                if (headset.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
                {
                    return isTracked;
                }
                
                // Method 2: Check device position for movement (PICO headsets may not have proximity sensor)
                if (headset.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                {
                    // If position is available and not at origin, assume headset is being worn
                    return position != Vector3.zero;
                }
                
                // Method 3: Check device rotation for movement
                if (headset.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                {
                    // If rotation is available and not identity, assume headset is being worn
                    return rotation != Quaternion.identity;
                }
                
                Debug.LogWarning("AbxrLib: PICO proximity detection methods failed, assuming headset is on");
                return true; // Fallback for PICO
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: PICO proximity check failed: {ex.Message}");
                return true; // Fallback for PICO
            }
        }
    
        private static void OnHeadsetRemovedDetected() { }
    
        private static void OnHeadsetPutOnDetected()
        {
            // Don't bother asking if they aren't acting on this event
            if (Abxr.OnHeadsetPutOnNewSession == null) return;
        
            Abxr.PollUser("Welcome back.\nAre you the same person who was using this headset before?",
                ExitPollHandler.PollType.MultipleChoice,
                new List<string>{_continueSessionString, _newSessionString},
                NewSessionCheck);
        }

        private static void NewSessionCheck(string response)
        {
            if (response == _newSessionString)
            {
                try
                {
                    Authentication.Authentication.ReAuthenticate();
                    Abxr.OnHeadsetPutOnNewSession?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"AbxrLib: HeadsetDetector - Error during re-authentication: {ex.Message}");
                }
            }
        }
    }
}
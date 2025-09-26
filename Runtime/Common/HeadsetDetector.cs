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
            InputDevice headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (headset.isValid)
            {
                if (headset.TryGetFeatureValue(CommonUsages.userPresence, out bool userPresent))
                {
                    return userPresent;
                }
            }
        
            // Fallback: assume headset is on if no proximity data
            return true;
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
                    Debug.LogError($"AbxrLib - HeadsetDetector: Error during re-authentication: {ex.Message}");
                }
            }
        }
    }
}
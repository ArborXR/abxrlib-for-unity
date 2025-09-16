using System.Collections.Generic;
using AbxrLib.Runtime.UI.ExitPoll;
using UnityEngine;
using UnityEngine.XR;

namespace AbxrLib.Runtime.Common
{
    public class HeadsetDetector : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 1f;
        private const string NewSessionString = "No, I need to log in as someone else.";
        private const string ContinueSessionString = "Yes, I’d like to continue the current session.";
    
        private bool sensorStatus = true;
        private float lastCheckTime;

        private void Update()
        {
            // Check at intervals to avoid excessive calls
            if (Time.time - lastCheckTime >= CheckIntervalSeconds)
            {
                bool currentStatus = CheckProximitySensor();
                if (sensorStatus && !currentStatus)
                {
                    OnHeadsetRemovedDetected();
                }
                else if (!sensorStatus && currentStatus)
                {
                    OnHeadsetPutOnDetected();
                }
            
                sensorStatus = currentStatus;
                lastCheckTime = Time.time;
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
                new List<string>{ContinueSessionString, NewSessionString},
                NewSessionCheck);
        }

        private static void NewSessionCheck(string response)
        {
            if (response == NewSessionString)
            {
                Authentication.Authentication.ReAuthenticate();
                Abxr.OnHeadsetPutOnNewSession?.Invoke();
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

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
    
    private void OnHeadsetPutOnDetected()
    {
        Abxr.PollUser("Welcome back.\nAre you the same person who was using this headset before?",
            ExitPollHandler.PollType.MultipleChoice,
            new List<string>{ContinueSessionString, NewSessionString},
            NewSessionCheck);
    }

    private static void NewSessionCheck(string response)
    {
        // We only care to reauthenticate if the app has logic defined for what to do in this scenario
        if (response == NewSessionString && Abxr.onHeadsetPutOnNewSession != null)
        {
            Authentication.ReAuthenticate();
            Abxr.onHeadsetPutOnNewSession?.Invoke();
        }
    }
}
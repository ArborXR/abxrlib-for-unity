using System;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Data;
using AbxrLib.Runtime.ServiceClient;
using AbxrLib.Runtime.Storage;
using AbxrLib.Runtime.Telemetry;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using Newtonsoft.Json;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public static class Initialize
    {
        public static readonly long StartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            var jsonVersion = typeof(JsonConvert).Assembly.GetName().Version;
            Debug.Log($"AbxrLib: Using Newtonsoft.Json version: {jsonVersion}");

            if (jsonVersion < new Version(13, 0, 0))
            {
                Debug.LogError("AbxrLib: Incompatible Newtonsoft.Json version loaded.");
            }
            
            ObjectAttacher.Attach<CoroutineRunner>("CoroutineRunner");
            ObjectAttacher.Attach<DeviceModel>("DeviceModel");
            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler"); // Needs to come before Auth in case auth needs keyboard
#if UNITY_ANDROID && !UNITY_EDITOR
            ObjectAttacher.Attach<ArborServiceClient>("ArborServiceClient");
#if PICO_ENTERPRISE_SDK_3
            ObjectAttacher.Attach<PicoQRCodeReader>("PicoQRCodeReader");
#endif
#endif
            ObjectAttacher.Attach<Authentication.Authentication>("Authentication");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
            ObjectAttacher.Attach<SceneChangeDetector>("SceneChangeDetector");
            ObjectAttacher.Attach<DataBatcher>("DataBatcher");
            ObjectAttacher.Attach<StorageBatcher>("StorageBatcher");
            ObjectAttacher.Attach<TrackSystemInfo>("TrackSystemInfo");
            ObjectAttacher.Attach<ApplicationQuitHandler>("ApplicationQuitHandler");
#if UNITY_ANDROID && !UNITY_EDITOR
            ObjectAttacher.Attach<HeadsetDetector>("HeadsetDetector");
            if (Configuration.Instance.headsetTracking)
            {
                ObjectAttacher.Attach<TrackInputDevices>("TrackInputDevices");
            }
#endif
            Debug.Log($"AbxrLib: Version {AbxrLibVersion.Version} Initialized.");
        }
    }
}
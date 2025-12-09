using System;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Events;
using AbxrLib.Runtime.Logs;
using AbxrLib.Runtime.ServiceClient;
using AbxrLib.Runtime.ServiceClient.AbxrInsightService;
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
            var version = typeof(JsonConvert).Assembly.GetName().Version;
            Debug.Log($"AbxrLib: Using Newtonsoft.Json version: {version}");

            if (version < new Version(13, 0, 0))
            {
                Debug.LogError("AbxrLib: Incompatible Newtonsoft.Json version loaded.");
            }
            
            ObjectAttacher.Attach<CoroutineRunner>("CoroutineRunner");
            ObjectAttacher.Attach<DeviceModel>("DeviceModel");
            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler"); // Needs to come before Auth in case auth needs keyboard

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[Initialize] Running on Android device - creating service clients");
            ObjectAttacher.Attach<ArborServiceClient>("ArborServiceClient");
            ObjectAttacher.Attach<AbxrInsightServiceClient>("AbxrInsightServiceClient");
#endif
            ObjectAttacher.Attach<Authentication.Authentication>("Authentication");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
            ObjectAttacher.Attach<SceneChangeDetector>("SceneChangeDetector");
            ObjectAttacher.Attach<EventBatcher>("EventBatcher");
            ObjectAttacher.Attach<LogBatcher>("LogBatcher");
            ObjectAttacher.Attach<StorageBatcher>("StorageBatcher");
            ObjectAttacher.Attach<TelemetryBatcher>("TelemetryBatcher");
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

    public class ObjectAttacher : MonoBehaviour
    {
        public static T Attach<T>(string name) where T : MonoBehaviour
        {
            Debug.Log($"[ObjectAttacher] Creating GameObject '{name}' with component {typeof(T).Name}");
            var go = new GameObject(name);
            DontDestroyOnLoad(go);
            var component = go.AddComponent<T>();
            Debug.Log($"[ObjectAttacher] Successfully created '{name}' - GameObject: {go.name}, Component enabled: {component.enabled}");
            return component;
        }
    }
}
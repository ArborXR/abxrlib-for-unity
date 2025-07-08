using System;
using Newtonsoft.Json;
using UnityEngine;

public static class Initialize
{
    public static readonly long StartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        var version = typeof(JsonConvert).Assembly.GetName().Version;
        Debug.Log($"AbxrLib - Using Newtonsoft.Json version: {version}");

        if (version < new Version(13, 0, 0))
        {
            Debug.LogError("AbxrLib - Incompatible Newtonsoft.Json version loaded.");
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        ObjectAttacher.Attach<ExceptionLogger>("ExceptionLogger");
        ObjectAttacher.Attach<DeviceModel>("DeviceModel");
#endif
        ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler"); // Needs to come before Auth in case auth needs keyboard
#if UNITY_ANDROID && !UNITY_EDITOR
        ObjectAttacher.Attach<ArborServiceClient>("ArborServiceClient");
#endif
        ObjectAttacher.Attach<Authentication>("Authentication");
        ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
        ObjectAttacher.Attach<SceneChangeDetector>("SceneChangeDetector");
        ObjectAttacher.Attach<EventBatcher>("EventBatcher");
        ObjectAttacher.Attach<LogBatcher>("LogBatcher");
        ObjectAttacher.Attach<StorageBatcher>("StorageBatcher");
        ObjectAttacher.Attach<TelemetryBatcher>("TelemetryBatcher");
        ObjectAttacher.Attach<TrackSystemInfo>("TrackSystemInfo");
#if UNITY_ANDROID && !UNITY_EDITOR
        ObjectAttacher.Attach<HeadsetDetector>("HeadsetDetector");
        if (Configuration.Instance.headsetTracking)
        {
            ObjectAttacher.Attach<TrackInputDevices>("TrackInputDevices");
        }
#endif
        Debug.Log($"AbxrLib - Version {AbxrVersion.Version} Initialized.");
    }
}

public class ObjectAttacher : MonoBehaviour
{
    public static T Attach<T>(string name) where T : MonoBehaviour
    {
        var go = new GameObject(name);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }
}
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AbxrLib.Runtime.Authentication
{
    public class DeviceModel : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string AbxrDetectDeviceModel();
#endif
        public static string deviceModel;

        public static readonly Dictionary<string, string> ModelTranslation = new()
        {
            { "G2 4K", "PICO G24K" },
            { "PICO G3", "PICO G3" },
            { "A7Q10", "PICO G3" },
            { "Neo 2", "PICO Neo 2" },
            { "Neo 3 Eye", "PICO Neo 3 Eye" },
            { "Neo 3", "PICO Neo 3" },
            { "PICO 4", "PICO 4" },
            { "A9210", "PICO 4" },
            { "PICO A9210", "PICO 4" },
            { "PICO 4 Ultra Enterprise", "PICO 4" },
            { "Vive Focus Plus", "HTC Vive Focus Plus" },
            { "Vive Flow", "HTC Vive Flow" },
            { "VIVE XR Series", "HTC Vive XR Elite" },
            { "Focus 3", "HTC Vive Focus 3" },
            { "Quest Pro", "Oculus Quest Pro" },
            { "Quest 2", "Oculus Quest 2" },
            { "Quest 3S", "Oculus Quest 3S" },
            { "Quest 3", "Oculus Quest 3" },
            { "Quest", "Oculus Quest 1" },
            { "Magic Leap 2", "Magic Leap 2" },
            { "VRX", "Lenovo VRX" },
            { "P1 PRO 4K ULTRA", "DPVR P1 Pro 4K Ultra" },
            { "P1 Pro B0EDSC4K", "DPVR P1 Pro 4K" },
            { "P1 Pro", "DPVR P1 Pro" },
            { "P2", "DPVR P2" }
        };
    
        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            deviceModel = GetSystemProperty("pxr.vendorhw.product.model");
            if (string.IsNullOrEmpty(deviceModel) || deviceModel == "Unknown")
            {
                deviceModel = GetSystemProperty("ro.product.model");
            }

            if (ModelTranslation.TryGetValue(deviceModel, out var value)) deviceModel = value;
#elif UNITY_WEBGL && !UNITY_EDITOR
            deviceModel = AbxrDetectDeviceModel() ?? "Unknown Browser";
#else
            deviceModel = "N/A";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string GetSystemProperty(string propertyName)
        {
            using var systemProperties = new AndroidJavaClass("android.os.SystemProperties");
            return systemProperties.CallStatic<string>("get", propertyName, "Unknown");
        }
#endif
    }
}
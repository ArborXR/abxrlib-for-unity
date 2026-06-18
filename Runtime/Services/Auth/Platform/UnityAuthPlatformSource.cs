using System;
using System.Reflection;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Default production source for platform-only auth values.
    /// </summary>
    internal sealed class UnityAuthPlatformSource : IAuthPlatformSource
    {
        internal static readonly UnityAuthPlatformSource Instance = new();

        private const string DeviceIdKey = "abxrlib_device_id";

        public bool IsAndroidPlayer
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsWebGlPlayer
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsStandalonePlayer
        {
            get
            {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public string AbsoluteUrl
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Application.absoluteURL ?? "";
#else
                return "";
#endif
            }
        }

        public string GetAndroidIntentParam(string key) => Utils.GetAndroidIntentParam(key);

        public string GetCommandLineArg(string key) => Utils.GetCommandLineArg(key);

        public string GetDesktopOrgToken()
        {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            return Utils.GetOrgTokenFromDesktopSources();
#else
            return "";
#endif
        }

        public string GetOrCreateWebGlDeviceId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(DeviceIdKey)) return PlayerPrefs.GetString(DeviceIdKey);

            string newGuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DeviceIdKey, newGuid);
            PlayerPrefs.Save();
            return newGuid;
#else
            return "";
#endif
        }

        public bool IsArborMdmConnected(ArborMdmClient arborMdmClient) => arborMdmClient != null && arborMdmClient.IsConnected();

        public string GetCurrentDeviceId() => Abxr.GetDeviceId();

        public string[] GetCurrentDeviceTags() => Abxr.GetDeviceTags();

        public string GetCurrentOrgId() => Abxr.GetOrgId();

        public string GetCurrentFingerprint() => Abxr.GetFingerprint();

        public string GetIpAddress() => Utils.GetIPAddress();

        public string GetAndroidManifestMetadata(string key) => Utils.GetAndroidManifestMetadata(key);

        public string GetXrdmVersion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var currentAssembly = Assembly.GetExecutingAssembly();
            AssemblyName[] referencedAssemblies = currentAssembly.GetReferencedAssemblies();
            foreach (AssemblyName assemblyName in referencedAssemblies)
            {
                if (assemblyName.Name == "XRDM.SDK.External.Unity")
                    return assemblyName.Version.ToString();
            }
#endif
            return "";
        }
    }
}

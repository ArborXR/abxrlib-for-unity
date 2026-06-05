using System.Collections.Generic;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Platform;

namespace AbxrLib.Tests.Runtime
{
    internal sealed class FakeAuthPlatformSource : IAuthPlatformSource
    {
        public bool IsAndroidPlayer { get; set; }
        public bool IsWebGlPlayer { get; set; }
        public bool IsStandalonePlayer { get; set; }

        public string AbsoluteUrl { get; set; } = "";
        public string DesktopOrgToken { get; set; } = "";
        public string WebGlDeviceId { get; set; } = "fake-webgl-device-id";
        public bool ArborMdmConnected { get; set; }
        public string CurrentDeviceId { get; set; } = "";
        public string CurrentOrgId { get; set; } = "";
        public string CurrentFingerprint { get; set; } = "";
        public string[] CurrentDeviceTags { get; set; }
        public string IpAddress { get; set; } = "0.0.0.0";
        public string XrdmVersion { get; set; } = "";

        public Dictionary<string, string> AndroidIntentParams { get; } = new();
        public Dictionary<string, string> AndroidManifestMetadata { get; } = new();
        public Dictionary<string, string> CommandLineArgs { get; } = new();

        public string GetAndroidIntentParam(string key) =>
            AndroidIntentParams.TryGetValue(key, out var value) ? value : "";

        public string GetCommandLineArg(string key) =>
            CommandLineArgs.TryGetValue(key, out var value) ? value : "";

        public string GetDesktopOrgToken() => DesktopOrgToken;

        public string GetOrCreateWebGlDeviceId() => WebGlDeviceId;

        public bool IsArborMdmConnected(ArborMdmClient arborMdmClient) => ArborMdmConnected;

        public string GetCurrentDeviceId() => CurrentDeviceId;

        public string[] GetCurrentDeviceTags() => CurrentDeviceTags;

        public string GetCurrentOrgId() => CurrentOrgId;

        public string GetCurrentFingerprint() => CurrentFingerprint;

        public string GetIpAddress() => IpAddress;

        public string GetAndroidManifestMetadata(string key) =>
            AndroidManifestMetadata.TryGetValue(key, out var value) ? value : "";

        public string GetXrdmVersion() => XrdmVersion;
    }
}

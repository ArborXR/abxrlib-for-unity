using AbxrLib.Runtime.Services.Platform;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Production implementations keep Unity/Android/WebGL preprocessor code here so
    /// AbxrAuthService can exercise platform behavior in normal tests by injecting a fake source.
    /// </summary>
    internal interface IAuthPlatformSource
    {
        bool IsAndroidPlayer { get; }
        bool IsWebGlPlayer { get; }
        bool IsStandalonePlayer { get; }

        string AbsoluteUrl { get; }

        string GetAndroidIntentParam(string key);
        string GetCommandLineArg(string key);
        string GetDesktopOrgToken();
        string GetOrCreateWebGlDeviceId();

        bool IsArborMdmConnected(ArborMdmClient arborMdmClient);
        string GetCurrentDeviceId();
        string[] GetCurrentDeviceTags();
        string GetCurrentOrgId();
        string GetCurrentFingerprint();

        string GetIpAddress();
        string GetAndroidManifestMetadata(string key);
        string GetXrdmVersion();
    }
}

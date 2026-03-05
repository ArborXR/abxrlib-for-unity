using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;

namespace AbxrLib.Runtime.Services.QRCodeReader
{
    /// <summary>
    /// Factory to attach the preferred QR reader and create the IAbxrQRCodeReader instance for the subsystem.
    /// Centralizes PICO_ENTERPRISE_SDK_3 usage for attach and create.
    /// </summary>
    internal static class AbxrQRCodeReaderFactory
    {
        private static bool _attachedPico;

        /// <summary>Attach the preferred reader component. Call from Initialize before Subsystem.Awake.</summary>
        public static void AttachPreferredReader()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            ObjectAttacher.Attach<AbxrQRCodeReaderPico>("AbxrQRCodeReaderPico");
            _attachedPico = true;
#else
            ObjectAttacher.Attach<AbxrQRCodeReaderCameraStream>("AbxrQRCodeReaderCameraStream");
            _attachedPico = false;
#endif
#else
            _attachedPico = false;
#endif
        }

        /// <summary>Create the QR reader instance and set AuthService on it. Call from AbxrSubsystem.Awake.</summary>
        public static IAbxrQRCodeReader Create(AbxrAuthService authService)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            if (_attachedPico)
            {
                var pico = AbxrQRCodeReaderPico.Instance;
                if (pico != null)
                {
                    AbxrQRCodeReaderPico.AuthService = authService;
                    return pico;
                }
            }
#else
            if (!_attachedPico)
            {
                var cam = AbxrQRCodeReaderCameraStream.Instance;
                if (cam != null)
                {
                    AbxrQRCodeReaderCameraStream.AuthService = authService;
                    return cam;
                }
            }
#endif
#endif
            // _attachedPico is only read in the Android block above; reference it here so the compiler does not warn in non-Android builds
            _ = _attachedPico;
            return new AbxrQRCodeReaderNone();
        }
    }
}

namespace AbxrLib.Runtime.Core.QRScanner
{
    internal static class QrScannerCoordinator
    {
        public static IQrScanner GetActiveScanner()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3
            if (QRCodeReaderPico.Instance is IQrScanner picoScanner && picoScanner.IsAvailable)
                return picoScanner;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            if (QRCodeReaderMeta.Instance is IQrScanner questScanner && questScanner.IsAvailable)
                return questScanner;
#endif
            return null;
        }
    }
}

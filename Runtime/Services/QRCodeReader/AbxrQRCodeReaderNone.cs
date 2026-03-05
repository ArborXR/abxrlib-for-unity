using System;
using UnityEngine;

namespace AbxrLib.Runtime.Services.QRCodeReader
{
    /// <summary>No-op QR reader when no scanner is available (e.g. Editor, non-Android, or disabled).</summary>
    internal class AbxrQRCodeReaderNone : IAbxrQRCodeReader
    {
        public bool IsAvailable => false;
        public bool IsCameraTexturePlaceable => false;
        public bool IsScanning() => false;
        public bool IsInitializing() => false;
        public bool AreCameraPermissionsDenied() => false;
        public void SetScanResultCallback(Action<string> callback) { }
        public void ScanQRCode() { }
        public void CancelScan() { }
        public Texture GetCameraTexture() => null;
    }
}

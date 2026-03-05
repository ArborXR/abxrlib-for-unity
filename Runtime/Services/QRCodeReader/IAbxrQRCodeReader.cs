using System;
using UnityEngine;

namespace AbxrLib.Runtime.Services.QRCodeReader
{
    /// <summary>
    /// QR code reader abstraction for auth input. Implementations: CameraStream (WebCamTexture + ZXing), Pico (PICO SDK), or None.
    /// </summary>
    internal interface IAbxrQRCodeReader
    {
        bool IsAvailable { get; }
        bool IsCameraTexturePlaceable { get; }
        bool IsScanning();
        bool IsInitializing();
        bool AreCameraPermissionsDenied();
        void SetScanResultCallback(Action<string> callback);
        void ScanQRCode();
        void CancelScan();
        Texture GetCameraTexture();
    }
}

using System;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    internal interface IQrScanner
    {
        bool IsAvailable { get; }
        bool IsScanning { get; }
        bool IsInitializing { get; }
        bool ArePermissionsDenied { get; }
        Texture GetCameraTexture();
        void SetScanResultCallback(Action<string> callback);
        void ScanQRCode();
        void CancelScan();
    }
}

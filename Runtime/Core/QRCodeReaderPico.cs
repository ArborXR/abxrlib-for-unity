/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - PICO QR Code Reader (SDK)
 *
 * Uses PICO's PXR_Enterprise SDK for QR scanning on PICO headsets. Camera access is handled
 * by the platform; use this on PICO instead of the general QRCodeReader when the SDK is available.
 *
 * Only activates when:
 * - PICO_ENTERPRISE_SDK_3 is defined and PXR_Enterprise is available
 * - Running on a PICO Enterprise device (product name contains "enterprise")
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3

using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using UnityEngine;
using Unity.XR.PXR;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// QR code reader for PICO headsets using PXR_Enterprise SDK (platform handles camera access).
    /// </summary>
    public class QRCodeReaderPico : MonoBehaviour
    {
        public static QRCodeReaderPico Instance;
        public static AbxrAuthService AuthService;

        private void Awake()
        {
            string productName = Unity.XR.PXR.PXR_System.GetProductName().ToLower();
            if (!productName.Contains("enterprise"))
            {
                Debug.LogWarning("[AbxrLib] Disabling PICO QR Code Scanner. Must be run on PICO Enterprise device.");
                return;
            }

            if (Instance == null)
            {
                Instance = this;
                Debug.Log("[AbxrLib] QRCodeReaderPico Instance activated successfully.");
            }
            else
            {
                Debug.LogWarning("[AbxrLib] QRCodeReaderPico Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            PXR_Enterprise.InitEnterpriseService();
            PXR_Enterprise.BindEnterpriseService(OnServiceBound);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private static void OnServiceBound(bool success) { }

        public void ScanQRCode()
        {
            PXR_Enterprise.ScanQRCode(OnQRCodeScanned);
        }

        private void OnQRCodeScanned(string scanResult)
        {
            if (string.IsNullOrEmpty(scanResult)) return;

            AuthService.SetInputSource("QRlms");

            Match match = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            if (match.Success)
            {
                string pin = match.Value;
                Debug.Log($"[AbxrLib] Extracted PIN from QR code: {pin}");
                AuthService.KeyboardAuthenticate(pin);
            }
            else
            {
                Debug.LogWarning($"[AbxrLib] Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                AuthService.KeyboardAuthenticate(null);
            }
        }
    }
}
#endif

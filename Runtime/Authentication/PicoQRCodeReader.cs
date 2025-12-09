/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - PICO QR Code Reader
 *
 * This component handles QR code reading on PICO headsets using PXR_Enterprise SDK.
 * It only activates when:
 * - Running on a PICO headset
 * - PXR_Enterprise class is available
 * - Authentication mechanism type is "assessmentPin"
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if PICO_ENTERPRISE_SDK_3
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.XR.PICO.TOBSupport;

namespace AbxrLib.Runtime.Authentication
{
    /// <summary>
    /// QR code reader for Pico headsets using PXR_Enterprise SDK.
    /// Only activates on Pico headsets when assessmentPin authentication is required.
    /// </summary>
    public class PicoQRCodeReader : MonoBehaviour
    {
        public static PicoQRCodeReader Instance;
        
        private void Awake()
        {
            string productName = Unity.XR.PXR.PXR_System.GetProductName().ToLower();
            if (!productName.Contains("enterprise"))
            {
                Debug.LogWarning("AbxrLib: Disabling QR Code Scanner. Must be run on PICO 4 [Ultra] Enterprise");
                return;
            }
            
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            PXR_Enterprise.InitEnterpriseService();
            PXR_Enterprise.BindEnterpriseService(OnServiceBound);
        }

        private static void OnServiceBound(bool success) { }
        
        public void ScanQRCode()
        {
            PXR_Enterprise.ScanQRCode(OnQRCodeScanned);
        }
        
        // Callback when QR code is scanned
        private void OnQRCodeScanned(string scanResult)
        {
            if (string.IsNullOrEmpty(scanResult)) return;
            
            // Set inputSource to "QRlms" for QR code authentication, even if invalid
            Authentication.SetInputSource("QRlms");
            
            // Extract PIN from QR code format "ABXR:123456"
            Match match = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            if (match.Success)
            {
                string pin = match.Value;
                Debug.Log($"AbxrLib: Extracted PIN from QR code: {pin}");
                StartCoroutine(Authentication.KeyboardAuthenticate(pin));
            }
            else
            {
                Debug.LogWarning($"AbxrLib: Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                StartCoroutine(Authentication.KeyboardAuthenticate(null, true));
            }
        }
    }
}
#endif
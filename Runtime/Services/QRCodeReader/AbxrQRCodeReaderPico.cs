/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - QR Code Reader (Pico SDK)
 *
 * Uses PICO's PXR_Enterprise SDK for QR scanning on PICO headsets. Implements IAbxrQRCodeReader.
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3

using System;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using UnityEngine;
using Unity.XR.PICO.TOBSupport;
using Unity.XR.PXR;

namespace AbxrLib.Runtime.Services.QRCodeReader
{
    /// <summary>
    /// QR code reader for PICO Enterprise devices using PXR_Enterprise SDK. Implements IAbxrQRCodeReader.
    /// </summary>
    internal class AbxrQRCodeReaderPico : MonoBehaviour, IAbxrQRCodeReader
    {
        public static AbxrQRCodeReaderPico Instance;
        internal static AbxrAuthService AuthService { get; set; }

        private const string PicoQrUnsupportedKey = "abxrlib_pico_qr_unsupported";

        private static bool _qrUnsupportedThisSession;

        public bool IsAvailable => Instance != null && !_qrUnsupportedThisSession;
        public bool IsCameraTexturePlaceable => false;
        public bool IsScanning() => false;
        public bool IsInitializing() => false;
        public bool AreCameraPermissionsDenied() => false;

        private Action<string> _scanResultCallback;

        private static bool GetPicoQrUnsupportedFromPrefs()
        {
            return PlayerPrefs.GetInt(PicoQrUnsupportedKey, 0) != 0;
        }

        private static void SetPicoQrUnsupportedInPrefs()
        {
            PlayerPrefs.SetInt(PicoQrUnsupportedKey, 1);
            PlayerPrefs.Save();
        }

        private static bool IsPicoEnterprise(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return false;
            return productName.ToLower().Contains("enterprise");
        }

        private void Awake()
        {
            string productName = PXR_System.GetProductName();
            if (!IsPicoEnterprise(productName))
            {
                Debug.LogWarning("[AbxrLib] Disabling PICO QR Code Scanner. Must be run on a PICO Enterprise device. Product: " + productName);
                return;
            }

            if (Instance == null)
            {
                _qrUnsupportedThisSession = GetPicoQrUnsupportedFromPrefs();
                Instance = this;
                Debug.Log("[AbxrLib] AbxrQRCodeReaderPico Instance activated successfully.");
            }
            else
            {
                Debug.LogWarning("[AbxrLib] AbxrQRCodeReaderPico Instance already exists. Destroying duplicate.");
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

        private void OnServiceBound(bool success)
        {
            if (!success && Instance == this)
            {
                _qrUnsupportedThisSession = true;
                SetPicoQrUnsupportedInPrefs();
                Debug.LogWarning("[AbxrLib] PICO QR Code Scanner: Enterprise service bind failed. QR scan disabled for this device (saved in preferences; device may be PICO 4 Enterprise rather than 4EU).");
            }
        }

        public void SetScanResultCallback(Action<string> callback)
        {
            _scanResultCallback = callback;
        }

        public void CancelScan()
        {
            if (_scanResultCallback != null)
            {
                var cb = _scanResultCallback;
                _scanResultCallback = null;
                cb?.Invoke(null);
            }
        }

        public void ScanQRCode()
        {
            try
            {
                PXR_Enterprise.ScanQRCode(OnQRCodeScanned);
            }
            catch (Exception e)
            {
                if (Instance == this)
                {
                    _qrUnsupportedThisSession = true;
                    SetPicoQrUnsupportedInPrefs();
                    Debug.LogWarning("[AbxrLib] PICO QR Code Scanner: ScanQRCode failed. QR scan disabled for this device (saved in preferences). " + e.Message);
                }
                if (_scanResultCallback != null)
                {
                    var cb = _scanResultCallback;
                    _scanResultCallback = null;
                    cb.Invoke(null);
                }
            }
        }

        public Texture GetCameraTexture() => null;

        private void OnQRCodeScanned(string scanResult)
        {
            if (_scanResultCallback != null)
            {
                string pin = null;
                if (!string.IsNullOrEmpty(scanResult))
                {
                    Match match = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
                    if (match.Success)
                    {
                        pin = match.Value;
                        Debug.Log($"[AbxrLib] Extracted PIN from QR code: {pin}");
                    }
                    else
                        Debug.LogWarning($"[AbxrLib] Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                }
                var cb = _scanResultCallback;
                _scanResultCallback = null;
                cb?.Invoke(pin);
                return;
            }
            if (string.IsNullOrEmpty(scanResult)) return;
            AuthService.SetInputSource("QRlms");
            Match authMatch = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            if (authMatch.Success)
            {
                string pin = authMatch.Value;
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

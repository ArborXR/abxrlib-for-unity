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
 * - Running on a PICO Enterprise device (product name contains "enterprise").
 *
 * QR is known to work on PICO 4 Enterprise Ultra (4EU). On PICO 4 Enterprise (non-Ultra) the SDK
 * may fail (bind or ScanQRCode); we allow the attempt and on first failure mark the reader as
 * unsupported and persist that in PlayerPrefs so we do not offer QR on future app launches.
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3

using System;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using UnityEngine;
using Unity.XR.PICO.TOBSupport;
using Unity.XR.PXR;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// QR code reader for PICO Enterprise devices using PXR_Enterprise SDK. Known to work on PICO 4 Enterprise Ultra (4EU);
    /// we also allow PICO 4 Enterprise and mark as unsupported on first bind/scan failure.
    /// </summary>
    public class QRCodeReaderPico : MonoBehaviour
    {
        public static QRCodeReaderPico Instance;
        public static AbxrAuthService AuthService;

        private const string PicoQrUnsupportedKey = "abxrlib_pico_qr_unsupported";

        /// <summary>
        /// True once we have seen a failure (bind or ScanQRCode), this session or a previous one (PlayerPrefs).
        /// </summary>
        private static bool _qrUnsupportedThisSession;

        /// <summary>
        /// True if the PICO QR reader is available (Instance exists and we have not marked it unsupported this device).
        /// </summary>
        public static bool IsAvailable => Instance != null && !_qrUnsupportedThisSession;

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
            string productName = Unity.XR.PXR.PXR_System.GetProductName();
            if (!IsPicoEnterprise(productName))
            {
                Debug.LogWarning("[AbxrLib] Disabling PICO QR Code Scanner. Must be run on a PICO Enterprise device. Product: " + productName);
                return;
            }

            if (Instance == null)
            {
                _qrUnsupportedThisSession = GetPicoQrUnsupportedFromPrefs();
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

        private void OnServiceBound(bool success)
        {
            if (!success && Instance == this)
            {
                _qrUnsupportedThisSession = true;
                SetPicoQrUnsupportedInPrefs();
                Debug.LogWarning("[AbxrLib] PICO QR Code Scanner: Enterprise service bind failed. QR scan disabled for this device (saved in preferences; device may be PICO 4 Enterprise rather than 4EU).");
            }
        }

        /// <summary>
        /// Set the one-shot callback for developer API (StartQRScanForAuthInput). When set, OnQRCodeScanned invokes this instead of KeyboardAuthenticate.
        /// </summary>
        public void SetScanResultCallback(Action<string> callback)
        {
            _scanResultCallback = callback;
        }

        /// <summary>
        /// Cancel an in-progress scan started with a callback; invokes the callback with null so the handler can close UI.
        /// </summary>
        public void CancelScanForAuthInput()
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

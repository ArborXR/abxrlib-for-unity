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
 * - PXR_System.GetProductName() matches SupportedPicoEnterpriseQrProductNameMarkers (enterprise + verified SKU markers, like Meta's device allowlist)
 * - BindEnterpriseService reports success (same idea as QRCodeReader.IsQRScanningAvailable() gating the UI)
 *
 * QR via PXR_Enterprise is verified on PICO 4 Enterprise Ultra (4EU); plain PICO 4 Enterprise is not offered
 * (SDK bind/scan often fails). On any bind/scan failure we still mark unsupported and persist in PlayerPrefs.
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
    /// QR code reader for PICO Enterprise devices using PXR_Enterprise SDK. Offered only on product names that pass
    /// SupportedPicoEnterpriseQrProductNameMarkers and after enterprise service bind succeeds (parallels QRCodeReader device checks).
    /// </summary>
    public class QRCodeReaderPico : MonoBehaviour
    {
        public static QRCodeReaderPico Instance;
        public static AbxrAuthService AuthService;

        private const string PicoQrUnsupportedKey = "abxrlib_pico_qr_unsupported";

        /// <summary>
        /// Substrings that must appear in PXR_System.GetProductName() (case-insensitive) for ScanQRCode to be offered,
        /// in addition to "enterprise". Matches PICO 4 Enterprise Ultra-class SKUs; extend if PICO documents more models.
        /// </summary>
        private static readonly string[] SupportedPicoEnterpriseQrProductNameMarkers =
        {
            "ultra",
        };

        /// <summary>
        /// True once we have seen a failure (bind or ScanQRCode), this session or a previous one (PlayerPrefs).
        /// </summary>
        private static bool _qrUnsupportedThisSession;

        private bool _enterpriseServiceBindSucceeded;

        /// <summary>
        /// True if the PICO QR reader can be used: supported product, bind succeeded, not marked unsupported.
        /// </summary>
        public static bool IsAvailable =>
            Instance != null && !_qrUnsupportedThisSession && Instance._enterpriseServiceBindSucceeded;

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

        /// <summary>
        /// True when product name indicates a headset we support for PXR_Enterprise ScanQRCode (enterprise + allowlist markers).
        /// </summary>
        private static bool IsPicoProductSupportedForQr(string productName)
        {
            if (!IsPicoEnterprise(productName)) return false;
            string p = productName.ToLowerInvariant();
            foreach (string marker in SupportedPicoEnterpriseQrProductNameMarkers)
            {
                if (string.IsNullOrEmpty(marker)) continue;
                if (p.Contains(marker.ToLowerInvariant()))
                    return true;
            }

            Logcat.Warning(
                "Disabling PICO QR Code Scanner. Product is Enterprise but not in the supported list for PXR_Enterprise ScanQRCode. Product: "
                + productName);
            return false;
        }

        /// <summary>
        /// Same gating as <see cref="IsAvailable"/> for custom UI; mirrors <see cref="QRCodeReader.IsQRScanningAvailable"/>.
        /// </summary>
        public bool IsQRScanningAvailable() => IsAvailable;

        private void Awake()
        {
            string productName = Unity.XR.PXR.PXR_System.GetProductName();
            if (!IsPicoProductSupportedForQr(productName))
            {
                if (!IsPicoEnterprise(productName))
                    Logcat.Warning("Disabling PICO QR Code Scanner. Must be run on a PICO Enterprise device. Product: " + productName);
                return;
            }

            if (Instance == null)
            {
                _qrUnsupportedThisSession = GetPicoQrUnsupportedFromPrefs();
                Instance = this;
                Logcat.Info("QRCodeReaderPico Instance activated successfully (product supported for QR).");
            }
            else
            {
                Logcat.Warning("QRCodeReaderPico Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Instance != this)
                return;
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
            if (Instance != this)
                return;
            if (success)
            {
                _enterpriseServiceBindSucceeded = true;
                Logcat.Info("PICO QR Code Scanner: Enterprise service bound; QR scan is available.");
            }
            else
            {
                _qrUnsupportedThisSession = true;
                SetPicoQrUnsupportedInPrefs();
                Logcat.Warning(
                    "PICO QR Code Scanner: Enterprise service bind failed. QR scan disabled for this device (saved in preferences).");
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
            if (!IsAvailable)
            {
                Logcat.Warning("PICO QR Code Scanner: ScanQRCode ignored (not available — product, bind, or prior failure).");
                if (_scanResultCallback != null)
                {
                    var cb = _scanResultCallback;
                    _scanResultCallback = null;
                    cb.Invoke(null);
                }
                return;
            }

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
                    Logcat.Warning("PICO QR Code Scanner: ScanQRCode failed. QR scan disabled for this device (saved in preferences). " + e.Message);
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
                        Logcat.Info($"Extracted PIN from QR code: {pin}");
                    }
                    else
                        Logcat.Warning($"Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
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
                Logcat.Info($"Extracted PIN from QR code: {pin}");
                AuthService.KeyboardAuthenticate(pin);
            }
            else
            {
                Logcat.Warning($"Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                AuthService.KeyboardAuthenticate(null);
            }
        }
    }
}
#endif

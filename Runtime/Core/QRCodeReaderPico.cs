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
 * - PXR_System.GetProductName() matches SupportedPicoEnterpriseQrProductNameMarkers (Pico 4 Ultra, Enterprise Ultra, or SKU a9210; plain Enterprise excluded)
 * - BindEnterpriseService reports success (same idea as QRCodeReader.IsQRScanningAvailable() gating the UI)
 *
 * QR via PXR_Enterprise is verified on Pico 4 Ultra and Pico 4 Enterprise Ultra; plain PICO 4 Enterprise is not offered
 * (SDK bind/scan often fails). On any bind/scan failure we still mark unsupported and persist in PlayerPrefs.
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;
using Unity.XR.PICO.TOBSupport;
using Unity.XR.PXR;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// QR code reader for PICO devices using PXR_Enterprise SDK. Offered on product names that match
    /// <see cref="SupportedPicoEnterpriseQrProductNameMarkers"/> and after enterprise service bind succeeds (parallels QRCodeReader device checks).
    /// </summary>
    public class QRCodeReaderPico : MonoBehaviour
    {
        public static QRCodeReaderPico Instance;
        public static AbxrAuthService AuthService;

        private const string PicoQrUnsupportedKey = "abxrlib_pico_qr_unsupported";

        /// <summary>Build / product string SKU marker (e.g. Pico 4 Enterprise Ultra); may appear without the word "ultra".</summary>
        private const string PicoProductNameSkuA9210 = "a9210";

        /// <summary>
        /// Substrings that must appear in the resolved product string (case-insensitive) for ScanQRCode to be offered:
        /// Pico 4 Ultra and Pico 4 Enterprise Ultra (marketing "ultra"), or SKU-only firmware (a9210). Plain PICO 4 Enterprise
        /// (no ultra, no a9210) is excluded. PXR_System.GetProductName() is often empty on OpenXR; we fall back to android.os.Build + SystemInfo.
        /// </summary>
        private static readonly string[] SupportedPicoEnterpriseQrProductNameMarkers =
        {
            "ultra",
            "a9210",
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

        /// <summary>PXR_Enterprise invokes the scan callback on a non-Unity thread; we deliver to main thread in <see cref="Update"/>.</summary>
        private readonly Queue<string> _pendingScanResults = new Queue<string>();

        private readonly object _pendingScanLock = new object();

        private static bool GetPicoQrUnsupportedFromPrefs()
        {
            return PlayerPrefs.GetInt(PicoQrUnsupportedKey, 0) != 0;
        }

        private static void SetPicoQrUnsupportedInPrefs()
        {
            PlayerPrefs.SetInt(PicoQrUnsupportedKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// True when product name indicates Pico 4 Ultra, Pico 4 Enterprise Ultra, or SKU a9210 (plain Enterprise excluded).
        /// </summary>
        private static bool IsPicoProductSupportedForQr(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return false;
            string p = productName.ToLowerInvariant();
            foreach (string marker in SupportedPicoEnterpriseQrProductNameMarkers)
            {
                if (string.IsNullOrEmpty(marker)) continue;
                if (p.Contains(marker.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Same gating as <see cref="IsAvailable"/> for custom UI; mirrors <see cref="QRCodeReader.IsQRScanningAvailable"/>.
        /// </summary>
        public bool IsQRScanningAvailable() => IsAvailable;

        private void Awake()
        {
            // PXR_System may throw or return empty if called from BeforeSceneLoad before the XR/PICO runtime is ready.
            // Start() runs before a coroutine's first yield, so we must not bind in Start() when Instance is set asynchronously.
            StartCoroutine(InitWhenPxrReady());
        }

        /// <summary>
        /// android.os.Build composite when PXR_System.GetProductName() is empty (typical with OpenXR).
        /// </summary>
        private static string TryGetAndroidBuildCompositeString()
        {
            try
            {
                using (AndroidJavaClass build = new AndroidJavaClass("android.os.Build"))
                {
                    string manufacturer = build.GetStatic<string>("MANUFACTURER") ?? "";
                    string model = build.GetStatic<string>("MODEL") ?? "";
                    string device = build.GetStatic<string>("DEVICE") ?? "";
                    string product = build.GetStatic<string>("PRODUCT") ?? "";
                    string combined = ($"{manufacturer} {model} {device} {product}").Trim();
                    return string.IsNullOrEmpty(combined) ? null : combined;
                }
            }
            catch (Exception ex)
            {
                Logcat.Warning("PICO QR: android.os.Build lookup failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Retries GetProductName across frames, then falls back to Build / SystemInfo when PXR returns empty (OpenXR).
        /// </summary>
        private IEnumerator InitWhenPxrReady()
        {
            const int maxAttempts = 120;
            string pxrName = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    pxrName = PXR_System.GetProductName();
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                        Logcat.Warning("PICO QR: PXR_System.GetProductName() failed (attempt " + attempt + "): " + ex.Message);
                    pxrName = null;
                }

                if (!string.IsNullOrEmpty(pxrName))
                    break;
                yield return null;
            }

            string productName = pxrName;
            if (string.IsNullOrEmpty(productName))
                productName = TryGetAndroidBuildCompositeString();

            if (string.IsNullOrEmpty(productName))
            {
                string sys = ($"{SystemInfo.deviceModel} {SystemInfo.deviceName}").Trim();
                if (!string.IsNullOrEmpty(sys))
                    productName = sys;
            }

            if (string.IsNullOrEmpty(productName))
            {
                Logcat.Warning(
                    "PICO QR: Could not resolve any product string (PXR empty after " + maxAttempts
                    + " frames, Build/SystemInfo empty). PICO QR scanner disabled for this session.");
                yield break;
            }

            if (!IsPicoProductSupportedForQr(productName))
            {
                Logcat.Warning(
                    "Disabling PICO QR Code Scanner. Supported: Pico 4 Ultra / Pico 4 Enterprise Ultra (name contains 'ultra') or SKU 'a9210'."
                    + "Plain PICO 4 Enterprise is not offered. Product: " + productName);
                yield break;
            }

            if (Instance != null)
            {
                Logcat.Warning("QRCodeReaderPico Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
                yield break;
            }

            _qrUnsupportedThisSession = GetPicoQrUnsupportedFromPrefs();
            Instance = this;
            Logcat.Info("QRCodeReaderPico Instance activated successfully (product supported for QR).");

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
            // Keyboard may not exist yet (PIN UI spawns later); refresh now and again over the next frames / seconds.
            KeyboardManager.RefreshQrButtonAvailability();
            StartCoroutine(DeferredRefreshKeyboardAfterBind());
        }

        /// <summary>
        /// Bind often completes before KeyboardManager exists. Refresh until the PIN pad has a chance to enable the QR button.
        /// </summary>
        private IEnumerator DeferredRefreshKeyboardAfterBind()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                KeyboardManager.RefreshQrButtonAvailability();
            }
            yield return new WaitForSeconds(0.35f);
            KeyboardManager.RefreshQrButtonAvailability();
            yield return new WaitForSeconds(1f);
            KeyboardManager.RefreshQrButtonAvailability();
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

            // Defer one frame so OpenXR/session focus and the compositor are settled before the platform ToB scanner UI appears.
            StartCoroutine(ScanQRCodeCoroutine());
        }

        private IEnumerator ScanQRCodeCoroutine()
        {
            yield return null;
            if (Instance != this || !IsAvailable)
            {
                if (_scanResultCallback != null)
                {
                    var cb = _scanResultCallback;
                    _scanResultCallback = null;
                    cb.Invoke(null);
                }
                yield break;
            }

            try
            {
                PXR_Enterprise.ScanQRCode(OnQRCodeScannedFromSdk);
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

        /// <summary>Called from PICO SDK (often not the Unity main thread). Do not touch Unity APIs here.</summary>
        private void OnQRCodeScannedFromSdk(string scanResult)
        {
            lock (_pendingScanLock)
            {
                _pendingScanResults.Enqueue(scanResult ?? "");
            }
        }

        private void Update()
        {
            if (Instance != this) return;
            while (true)
            {
                string raw;
                lock (_pendingScanLock)
                {
                    if (_pendingScanResults.Count == 0) break;
                    raw = _pendingScanResults.Dequeue();
                }

                ProcessQrScanResultOnMainThread(raw);
            }
        }

        /// <summary>Same rules as <see cref="QRCodeReader"/>: ABXR: digits (case-insensitive) or a bare 6-digit code.</summary>
        private static bool TryExtractPinFromQrPayload(string scanResult, out string pin)
        {
            pin = null;
            if (string.IsNullOrEmpty(scanResult)) return false;
            string s = scanResult.Trim();
            Match m = Regex.Match(s, @"(?i)(?<=ABXR:)\d+");
            if (m.Success)
            {
                pin = m.Value;
                return true;
            }

            m = Regex.Match(s, @"^\d{6}$");
            if (m.Success)
            {
                pin = m.Value;
                return true;
            }

            return false;
        }

        private void ProcessQrScanResultOnMainThread(string scanResult)
        {
            if (_scanResultCallback != null)
            {
                string callbackPin = null;
                if (!string.IsNullOrEmpty(scanResult))
                {
                    if (TryExtractPinFromQrPayload(scanResult, out callbackPin))
                        Logcat.Info("Extracted PIN from QR code: " + callbackPin);
                    else
                        Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
                }

                var cb = _scanResultCallback;
                _scanResultCallback = null;
                cb?.Invoke(callbackPin);
                return;
            }

            if (AuthService == null)
            {
                Logcat.Error("PICO QR: AuthService is null; cannot submit PIN. Ensure AbxrSubsystem initialized before scanning.");
                return;
            }

            if (string.IsNullOrEmpty(scanResult))
                return;

            AuthService.SetInputSource("QRlms");
            if (TryExtractPinFromQrPayload(scanResult, out string pin))
            {
                Logcat.Info("Extracted PIN from QR code: " + pin);
                AuthService.KeyboardAuthenticate(pin);
            }
            else
            {
                Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
                AuthService.KeyboardAuthenticate(null);
            }
        }
    }
}
#endif

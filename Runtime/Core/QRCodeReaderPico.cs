/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - PICO QR Code Reader (Camera Image path)
 *
 * Uses the PICO Unity SDK 3.4 PXR_CameraImage API to acquire RGBA frames in Unity,
 * and now shares the same scan-session UX as Quest:
 * - shared world-space scan panel
 * - shared cancel / restore PIN pad behavior
 * - shared QR payload parsing
 * - shared button state model
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3
using System;
using System.Collections;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.UI.Keyboard;
using Unity.XR.PXR;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public class QRCodeReaderPico : MonoBehaviour, IQrScanner
    {
        public static QRCodeReaderPico Instance;
        public static AbxrAuthService AuthService;

        private const float DecodeIntervalSeconds = 0.5f;
        private const float StartupTimeoutSeconds = 8f;

        private bool _isOfferedOnThisDevice;
        private bool _isScanning;
        private bool _isInitializing;
        private bool _cameraPermissionRequested;

        private Action<string> _scanResultCallback;
        private Coroutine _scanCoroutine;
        private Texture2D _latestPreviewTexture;

        private PicoEnterpriseCameraFrameProvider _cameraProvider;
        private QrScanPanel _panel;
        private ZXing.BarcodeReader _barcodeReader;
        private bool _restorePinPadOnCancel;

        public static bool IsAvailable => Instance != null && Instance._isOfferedOnThisDevice;

        bool IQrScanner.IsAvailable => _isOfferedOnThisDevice;
        bool IQrScanner.IsScanning => _isScanning;
        bool IQrScanner.IsInitializing => _isInitializing;
        bool IQrScanner.ArePermissionsDenied => _cameraPermissionRequested && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
        Texture IQrScanner.GetCameraTexture() => GetCameraTexture();
        void IQrScanner.CancelScan() => CancelScanForAuthInput();

        public Texture GetCameraTexture() => _latestPreviewTexture;

        private void Awake() => StartCoroutine(InitWhenPxrReady());

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
            if (string.IsNullOrEmpty(productName)) productName = TryGetAndroidBuildCompositeString();

            if (string.IsNullOrEmpty(productName))
            {
                string sys = (SystemInfo.deviceModel + " " + SystemInfo.deviceName).Trim();
                if (!string.IsNullOrEmpty(sys)) productName = sys;
            }

            if (string.IsNullOrEmpty(productName))
            {
                Logcat.Warning("PICO QR: Could not resolve a product string; QR scanner disabled for this session.");
                yield break;
            }

            if (!IsPicoProductSupportedForQr(productName))
            {
                Logcat.Warning("Disabling PICO QR Code Scanner on unsupported product: " + productName);
                yield break;
            }

            if (Instance != null)
            {
                Logcat.Warning("QRCodeReaderPico Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
                yield break;
            }

            Instance = this;
            _isOfferedOnThisDevice = true;
            EnsureRuntimeObjects();
            KeyboardManager.RefreshQrButtonAvailability();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopScanningInternal(false);
        }

        public void SetScanResultCallback(Action<string> callback) => _scanResultCallback = callback;

        public void CancelScanForAuthInput() => StopScanningInternal(true);

        public void ScanQRCode()
        {
            if (!IsAvailable)
            {
                Logcat.Warning("PICO QR Code Scanner: ScanQRCode ignored (not available).");
                InvokeAndClearCallback(null);
                return;
            }

            if (_isScanning || _isInitializing)
            {
                Logcat.Info("PICO QR Code Scanner: scan already in progress.");
                return;
            }

            EnsureRuntimeObjects();
            if (_cameraProvider == null || _panel == null || _barcodeReader == null)
            {
                Logcat.Error("PICO QR Code Scanner: runtime objects could not be created.");
                InvokeAndClearCallback(null);
                return;
            }

            StartCoroutine(RequestPermissionThenScanCoroutine());
        }

        private IEnumerator RequestPermissionThenScanCoroutine()
        {
            _isInitializing = true;

            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                _cameraPermissionRequested = true;
                bool? permissionGranted = null;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();

                callbacks.PermissionGranted += _ => permissionGranted = true;
                callbacks.PermissionDenied += _ => permissionGranted = false;

                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);

                while (!permissionGranted.HasValue)
                {
                    if (!_isInitializing) yield break;
                    yield return null;
                }

                if (!permissionGranted.Value)
                {
                    Logcat.Warning("PICO QR camera permission denied.");
                    _isInitializing = false;
                    InvokeAndClearCallback(null);
                    yield break;
                }
            }

            _restorePinPadOnCancel = KeyboardHandler.IsPinPadVisible();
            if (_restorePinPadOnCancel) KeyboardHandler.HidePinPad();

            _scanCoroutine = StartCoroutine(ScanLoopCoroutine());
        }

        private IEnumerator ScanLoopCoroutine()
        {
            _latestPreviewTexture = null;

            _panel.Show();
            _panel.SetStatus("Starting camera...");
            _panel.SetPreviewUvRect(new Rect(0f, 0f, 1f, 1f));
            _cameraProvider.StartCamera();

            float startupDeadline = Time.unscaledTime + StartupTimeoutSeconds;
            while (_isInitializing)
            {
                if (_cameraProvider.IsCapturing) break;

                if (!_cameraProvider.IsStarting)
                {
                    if (!string.IsNullOrEmpty(_cameraProvider.LastError))
                    {
                        Logcat.Warning("PICO QR camera failed to start: " + _cameraProvider.LastError);
                        StopScanningInternal(true);
                        yield break;
                    }

                    if (Time.unscaledTime > startupDeadline)
                    {
                        Logcat.Warning("PICO QR camera did not start before timeout.");
                        StopScanningInternal(true);
                        yield break;
                    }
                }

                yield return null;
            }

            if (!_isInitializing) yield break;

            _isInitializing = false;
            _isScanning = true;
            _panel.SetStatus("Look at QR Code");

            float nextDecodeTime = 0f;

            while (_isScanning)
            {
                if (_cameraProvider.TryGetLatestFrame(out CameraFrame frame))
                {
                    _latestPreviewTexture = frame.Texture;
                    _panel.SetPreviewTexture(frame.Texture);

                    if (Time.unscaledTime >= nextDecodeTime)
                    {
                        nextDecodeTime = Time.unscaledTime + DecodeIntervalSeconds;

                        string scanResult = QrCodeScanCommon.TryDecodeMatchingAbxrQr(_barcodeReader, frame.Pixels, frame.Width, frame.Height);
                        if (!string.IsNullOrEmpty(scanResult))
                        {
                            StopScanningInternal(false);
                            ProcessQrScanResult(scanResult);
                            yield break;
                        }
                    }
                }

                yield return null;
            }
        }

        private void StopScanningInternal(bool invokeCallbackWithNull)
        {
            if (_scanCoroutine != null)
            {
                StopCoroutine(_scanCoroutine);
                _scanCoroutine = null;
            }

            _isScanning = false;
            _isInitializing = false;
            _cameraProvider?.StopCamera();
            _panel?.Hide();

            if (invokeCallbackWithNull && _restorePinPadOnCancel) KeyboardHandler.ShowPinPad();

            _restorePinPadOnCancel = false;

            if (invokeCallbackWithNull) InvokeAndClearCallback(null);
        }

        private void EnsureRuntimeObjects()
        {
            _barcodeReader ??= QrCodeScanCommon.CreateBarcodeReader();

            if (_cameraProvider == null)
            {
                _cameraProvider = GetComponent<PicoEnterpriseCameraFrameProvider>();
                if (_cameraProvider == null) _cameraProvider = gameObject.AddComponent<PicoEnterpriseCameraFrameProvider>();
            }

            if (_panel == null)
            {
                _panel = GetComponentInChildren<QrScanPanel>(true);
                if (_panel == null) _panel = QrScanPanel.CreateRuntimePanel(transform, CancelScanForAuthInput);
            }
        }

        private void ProcessQrScanResult(string scanResult)
        {
            if (_scanResultCallback != null)
            {
                string callbackPin = null;
                if (!string.IsNullOrEmpty(scanResult) && !QrCodeScanCommon.TryExtractPinFromQrPayload(scanResult, out callbackPin))
                {
                    Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
                }

                InvokeAndClearCallback(callbackPin);
                return;
            }

            if (string.IsNullOrEmpty(scanResult)) return;

            AuthService.SetInputSource("QRlms");
            if (!QrCodeScanCommon.TryExtractPinFromQrPayload(scanResult, out string pin))
            {
                Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
            }

            AuthService.KeyboardAuthenticate(pin);
        }

        private void InvokeAndClearCallback(string value)
        {
            if (_scanResultCallback == null) return;

            Action<string> cb = _scanResultCallback;
            _scanResultCallback = null;
            cb?.Invoke(value);
        }

        private static bool IsPicoProductSupportedForQr(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return false;

            string p = productName.ToLowerInvariant();
            return p.Contains("pico");
        }

        private static string TryGetAndroidBuildCompositeString()
        {
            try
            {
                using var build = new AndroidJavaClass("android.os.Build");
                string manufacturer = build.GetStatic<string>("MANUFACTURER") ?? "";
                string model = build.GetStatic<string>("MODEL") ?? "";
                string device = build.GetStatic<string>("DEVICE") ?? "";
                string product = build.GetStatic<string>("PRODUCT") ?? "";
                string combined = (manufacturer + " " + model + " " + device + " " + product).Trim();
                return string.IsNullOrEmpty(combined) ? null : combined;
            }
            catch (Exception ex)
            {
                Logcat.Warning("PICO QR: android.os.Build lookup failed: " + ex.Message);
                return null;
            }
        }
    }
}
#endif

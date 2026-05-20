/*
 * Uses the PICO Unity SDK 3.4 PXR_CameraImage API to acquire RGBA frames in Unity.
 * Inherits shared state, permission flow, result handling, and stop logic
 * from QrScannerBase. Only Pico-specific device detection and the PXR camera
 * session live here.
 */
#if UNITY_ANDROID && !UNITY_EDITOR && PICO_SDK_3_4_OR_NEWER
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.XR.PXR;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Core.QRScanner
{
    public class QRCodeReaderPico : QrScannerBase
    {
        public static QRCodeReaderPico Instance;

        private const float DecodeIntervalSeconds = 0.5f;
        private const float StartupTimeoutSeconds = 8f;

        private PicoEnterpriseCameraFrameProvider _cameraProvider;
        private Texture2D _latestPreviewTexture;

        // Background-decode handoff. Written on a thread-pool thread, read on the main thread in the scan loop.
        // Reference/bool writes are atomic on the platforms we target, and a one-frame delay observing them is harmless here.
        private volatile bool _decodeInFlight;
        private volatile string _pendingResult;

        // Reused across decode dispatches so we don't allocate a fresh pixel array every decode interval.
        // Sized to the frame on first use and whenever the frame grows.
        private Color32[] _decodeScratch;

        // ── IQrScanner / QrScannerBase overrides ─────────────────────────────────────

        public static bool IsAvailable => Instance != null && Instance.IsOfferedOnThisDevice;

        public override Texture GetCameraTexture() => _latestPreviewTexture;

        protected override void ShutdownCameraBackend()
        {
            _cameraProvider?.StopCamera();
            _latestPreviewTexture = null;
        }

        protected override IEnumerator ScanLoopCoroutine()
        {
            _latestPreviewTexture = null;
            _decodeInFlight = false;
            _pendingResult = null;

            Panel?.Show();
            Panel?.SetStatus("Starting camera...");
            Panel?.SetPreviewUvRect(new Rect(0f, 0f, 1f, 1f));

            _cameraProvider.StartCamera();

            float startupDeadline = Time.unscaledTime + StartupTimeoutSeconds;
            while (IsInitializing)
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

            if (!IsInitializing) yield break;

            IsInitializing = false;
            IsScanning = true;

            Panel?.SetStatus("Look at QR Code");

            float nextDecodeTime = 0f;
            while (IsScanning)
            {
                if (_cameraProvider.TryGetLatestFrame(out CameraFrame frame))
                {
                    _latestPreviewTexture = frame.Texture;
                    Panel?.SetPreviewTexture(frame.Texture);

                    // Pick up a result produced by a finished background decode.
                    // Unity calls must run on the main thread, so the worker only sets a string and we act on it here.
                    string ready = _pendingResult;
                    if (!string.IsNullOrEmpty(ready))
                    {
                        _pendingResult = null;
                        StopScanningInternal(false);
                        ProcessQrScanResult(ready);
                        yield break;
                    }

                    // Kick off a new decode only when the previous one has finished and the throttle interval has elapsed.
                    // The in-flight guard prevents stacking decode tasks if a single decode runs longer than the interval.
                    if (!_decodeInFlight && Time.unscaledTime >= nextDecodeTime &&
                        frame.Pixels != null && frame.Width > 0 && frame.Height > 0)
                    {
                        nextDecodeTime = Time.unscaledTime + DecodeIntervalSeconds;

                        // Snapshot the frame on the main thread.
                        // The provider reuses its pixel buffer across frames, so the worker must not read
                        // frame pixels directly — it could be overwritten mid-decode.
                        int w = frame.Width;
                        int h = frame.Height;
                        int needed = w * h;
                        if (_decodeScratch == null || _decodeScratch.Length < needed)
                            _decodeScratch = new Color32[needed];
                        Array.Copy(frame.Pixels, _decodeScratch, needed);

                        Color32[] pixels = _decodeScratch;
                        var reader = BarcodeReader;
                        _decodeInFlight = true;

                        Task.Run(() =>
                        {
                            try
                            {
                                string r = QrCodeScanCommon.TryDecodeMatchingAbxrQr(reader, pixels, w, h);
                                if (!string.IsNullOrEmpty(r)) _pendingResult = r;
                            }
                            catch (Exception ex)
                            {
                                Logcat.Warning("PICO QR background decode error: " + ex.Message);
                            }
                            finally
                            {
                                _decodeInFlight = false;
                            }
                        });
                    }
                }

                yield return null;
            }
        }

        protected override void EnsureRuntimeObjects()
        {
            base.EnsureRuntimeObjects(); // panel + barcode reader

            if (_cameraProvider == null)
            {
                _cameraProvider = GetComponent<PicoEnterpriseCameraFrameProvider>();
                if (_cameraProvider == null) _cameraProvider = gameObject.AddComponent<PicoEnterpriseCameraFrameProvider>();
            }
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────────

        private void Awake() => StartCoroutine(InitWhenPxrReady());

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopScanningInternal(false);
        }

        // ── PICO device detection ─────────────────────────────────────────────────────

        private IEnumerator InitWhenPxrReady()
        {
            const int maxAttempts = 120;
            string pxrName = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try { pxrName = PXR_System.GetProductName(); }
                catch (Exception ex)
                {
                    if (attempt == 0)
                        Logcat.Warning("PICO QR: PXR_System.GetProductName() failed (attempt 0): " + ex.Message);
                    pxrName = null;
                }

                if (!string.IsNullOrEmpty(pxrName)) break;
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
                Logcat.Warning("PICO QR: Could not resolve a product string; QR scanner disabled.");
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
            IsOfferedOnThisDevice = true;
            EnsureRuntimeObjects();
            KeyboardManager.RefreshQrButtonAvailability();
        }

        private static bool IsPicoProductSupportedForQr(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return false;
            return productName.ToLowerInvariant().Contains("pico");
        }

        private static string TryGetAndroidBuildCompositeString()
        {
            try
            {
                using var build = new AndroidJavaClass("android.os.Build");
                string manufacturer = build.GetStatic<string>("MANUFACTURER") ?? "";
                string model        = build.GetStatic<string>("MODEL")        ?? "";
                string device       = build.GetStatic<string>("DEVICE")       ?? "";
                string product      = build.GetStatic<string>("PRODUCT")      ?? "";
                string combined     = (manufacturer + " " + model + " " + device + " " + product).Trim();
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

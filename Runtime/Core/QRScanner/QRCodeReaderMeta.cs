/*
 * Uses WebCamTexture + ZXing for supported Android XR devices.
 * Inherits shared state, permission flow, result handling, and stop logic
 * from QrScannerBase. Only Quest-specific camera management and the Meta
 * headset-camera / OpenXR feature checks live here.
 */
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Core.QRScanner
{
    public class QRCodeReaderMeta : QrScannerBase
    {
        public static QRCodeReaderMeta Instance;

        private const float StartupTimeoutSeconds = 8f;
        private const float FirstPermissionWarmupSeconds = 1.5f;
        private const float FocusReturnTimeoutSeconds = 5f;
        private const float DeviceDiscoveryTimeoutSeconds = 3f;
        private const float CameraRetryDelaySeconds = 0.35f;
        private const int CameraStartAttempts = 2;
        private const int RequestedWidth = 1920;
        private const int RequestedHeight = 1080;
        private const int RequestedFps = 30;

        private static readonly string[] SupportedDevices =
        {
            "Oculus Quest 3",
            "Oculus Quest 3S",
            "Oculus Quest Pro"
        };

        // ── Quest-specific state ─────────────────────────────────────────────────────
        private bool _headsetPermissionChecked;
        private bool _cameraStartupSucceeded;

        private WebCamTexture _webCamTexture;
        private RenderTexture _webCamRenderTexture;
        private Texture2D _readbackTexture;
        private Texture _latestPreviewTexture;

        // Background decode
        private volatile bool _decodeInProgress;
        private volatile string _pendingDecodeResult;

        // ── QrScannerBase overrides ───────────────────────────────────────────────────
        
        public static bool IsAvailable => Instance != null && Instance.IsOfferedOnThisDevice;

        public override Texture GetCameraTexture() => _latestPreviewTexture != null ? _latestPreviewTexture : _webCamTexture;

        protected override bool AreCameraPermissionsDenied()
        {
            bool cameraDenied = CameraPermissionRequested &&
                                !UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                                    UnityEngine.Android.Permission.Camera);
            bool headsetDenied = _headsetPermissionChecked && !HasHeadsetCameraPermission();
            return cameraDenied || headsetDenied;
        }

        /// <summary>
        /// After the base camera permission is granted, Quest needs to:
        ///   1. Check that the required OpenXR features are enabled.
        ///   2. Apply the first-grant warmup delay.
        ///   3. Wait for app focus to return after the permission dialog.
        ///   4. Verify the Meta headset-camera permission.
        /// </summary>
        protected override IEnumerator OnPermissionGranted(Action<bool> setResult)
        {
            if (!CheckOpenXRFeatures())
            {
                Logcat.Error("Cannot initialize Quest QR scanner: required OpenXR features are not enabled.");
                IsInitializing = false;
                InvokeAndClearCallback(null);
                setResult(false);
                yield break;
            }

            bool alreadyHadPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
            if (!alreadyHadPermission) yield return new WaitForSecondsRealtime(FirstPermissionWarmupSeconds);

            yield return StartCoroutine(WaitForAppFocusCoroutine());
            yield return null;
            yield return new WaitForEndOfFrame();

            _headsetPermissionChecked = true;
            if (!HasHeadsetCameraPermission())
            {
                Logcat.Warning("Quest QR scanner requires the Headset Cameras permission.");
                EnsureRuntimeObjects();
                bool pinPadWasVisible = KeyboardHandler.IsPinPadVisible();
                if (pinPadWasVisible) KeyboardHandler.HidePinPad();
                Panel.Show();
                Panel.SetStatus("Headset Cameras permission required");
                yield return new WaitForSecondsRealtime(3.0f);
                IsInitializing = false;
                Panel?.Hide();
                if (pinPadWasVisible) KeyboardHandler.ShowPinPad();
                InvokeAndClearCallback(null);
                setResult(false);
                yield break;
            }

            setResult(true);
        }

        protected override IEnumerator ScanLoopCoroutine()
        {
            Panel.Show();
            Panel.SetStatus("Starting camera...");
            Panel.SetPreviewUvRect(new Rect(0f, 0f, 1f, 1f));

            _cameraStartupSucceeded = false;
            yield return StartCoroutine(StartCameraCoroutine());

            if (!_cameraStartupSucceeded)
            {
                if (IsInitializing) StopScanningInternal(true);
                yield break;
            }

            IsInitializing = false;
            IsScanning = true;
            RefreshPreviewTexture();
            Panel.SetStatus("Look at QR Code");

            float nextDecodeTime = 0f;
            while (IsScanning)
            {
                if (_webCamTexture != null && _webCamTexture.isPlaying) RefreshPreviewTexture();

                if (_pendingDecodeResult != null)
                {
                    string scanResult = _pendingDecodeResult;
                    _pendingDecodeResult = null;
                    StopScanningInternal(false);
                    ProcessQrScanResult(scanResult);
                    yield break;
                }

                if (!_decodeInProgress && Time.unscaledTime >= nextDecodeTime)
                {
                    nextDecodeTime = Time.unscaledTime + DecodeIntervalSeconds;

                    if (TryGetLatestDecodePixels(out Color32[] pixels, out int width, out int height))
                    {
                        Color32[] pixelsCopy = pixels;
                        int w = width, h = height;
                        ZXing.BarcodeReader reader = BarcodeReader;
                        _decodeInProgress = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                string result = QrCodeScanCommon.TryDecodeMatchingAbxrQr(reader, pixelsCopy, w, h);
                                if (!string.IsNullOrEmpty(result)) _pendingDecodeResult = result;
                            }
                            finally
                            {
                                _decodeInProgress = false;
                            }
                        });
                    }
                }

                yield return null;
            }
        }

        protected override void ShutdownCameraBackend()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying) _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (_webCamRenderTexture != null)
            {
                _webCamRenderTexture.Release();
                Destroy(_webCamRenderTexture);
                _webCamRenderTexture = null;
            }

            if (_readbackTexture != null)
            {
                Destroy(_readbackTexture);
                _readbackTexture = null;
            }

            _latestPreviewTexture   = null;
            _cameraStartupSucceeded = false;
            _decodeInProgress       = false;
            _pendingDecodeResult    = null;
        }

        protected override void EnsureRuntimeObjects() => base.EnsureRuntimeObjects(); // panel + barcode reader

        // ── Unity lifecycle ───────────────────────────────────────────────────────────

        private void Awake() => TryActivateScanner();

        private void Start()
        {
            if (!IsOfferedOnThisDevice) StartCoroutine(DelayedDeviceCheck());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopScanningInternal(false);
            ShutdownCameraBackend();
        }

        // ── Device detection ──────────────────────────────────────────────────────────

        private void TryActivateScanner()
        {
            if (IsOfferedOnThisDevice) return;

            BarcodeReader ??= QrCodeScanCommon.CreateBarcodeReader();
            EnsureRuntimeObjects();

            if (Instance == null)
            {
                Instance = this;
                IsOfferedOnThisDevice = true;
                Logcat.Info($"Device '{DeviceModel.deviceModel}' is supported for QR code reading.");
                KeyboardManager.RefreshQrButtonAvailability();
            }
            else if (Instance != this)
            {
                Logcat.Warning("QRCodeReader Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        private IEnumerator DelayedDeviceCheck()
        {
            const int maxAttempts = 120;
            for (int i = 0; i < maxAttempts && !IsOfferedOnThisDevice; i++)
            {
                if (!string.IsNullOrEmpty(DeviceModel.deviceModel))
                {
                    TryActivateScanner();
                    yield break;
                }
                yield return null;
            }
        }

        // ── Camera startup ────────────────────────────────────────────────────────────

        private IEnumerator StartCameraCoroutine()
        {
            ShutdownCameraBackend();

            WebCamDevice? selectedCamera = null;
            float discoveryDeadline = Time.unscaledTime + DeviceDiscoveryTimeoutSeconds;
            while (IsInitializing && Time.unscaledTime < discoveryDeadline)
            {
                selectedCamera = SelectCameraForDevice(WebCamTexture.devices);
                if (selectedCamera.HasValue) break;
                yield return null;
            }

            if (!selectedCamera.HasValue)
            {
                Logcat.Error("No camera found. Cannot scan QR codes.");
                yield break;
            }

            for (int attempt = 1; attempt <= CameraStartAttempts && IsInitializing; attempt++)
            {
                yield return StartCoroutine(StartCameraAttemptCoroutine(selectedCamera.Value, attempt));
                if (_cameraStartupSucceeded) yield break;

                ShutdownCameraBackend();

                if (attempt < CameraStartAttempts)
                {
                    Logcat.Warning($"Quest QR camera attempt {attempt} did not produce a valid frame. Retrying.");
                    yield return new WaitForSecondsRealtime(CameraRetryDelaySeconds);
                    yield return StartCoroutine(WaitForAppFocusCoroutine());
                }
            }

            if (IsInitializing) Logcat.Warning("Quest QR camera did not start before timeout.");
        }

        private IEnumerator StartCameraAttemptCoroutine(WebCamDevice selectedCamera, int attemptNumber)
        {
            _webCamTexture = new WebCamTexture(selectedCamera.name, RequestedWidth, RequestedHeight, RequestedFps)
            {
                requestedFPS = RequestedFps
            };
            _webCamTexture.Play();

            float deadline = Time.unscaledTime + StartupTimeoutSeconds;
            int consecutiveGoodFrames = 0;

            while (IsInitializing && Time.unscaledTime < deadline)
            {
                if (!Application.isFocused)
                {
                    consecutiveGoodFrames = 0;
                    yield return null;
                    continue;
                }

                if (_webCamTexture != null && _webCamTexture.isPlaying &&
                    _webCamTexture.width > 16 && _webCamTexture.height > 16)
                {
                    EnsureReadbackTargets(_webCamTexture.width, _webCamTexture.height);

                    if (_webCamTexture.didUpdateThisFrame)
                    {
                        RefreshPreviewTexture();

                        if (TryGetLatestDecodePixels(out _, out _, out _))
                        {
                            if (++consecutiveGoodFrames >= 2)
                            {
                                _cameraStartupSucceeded = true;
                                yield break;
                            }
                        }
                        else
                        {
                            consecutiveGoodFrames = 0;
                        }
                    }
                }

                yield return null;
            }

            Logcat.Warning($"Quest QR camera start attempt {attemptNumber} timed out.");
        }

        private IEnumerator WaitForAppFocusCoroutine()
        {
            float deadline = Time.unscaledTime + FocusReturnTimeoutSeconds;
            while (IsInitializing && !Application.isFocused && Time.unscaledTime < deadline) yield return null;
        }

        // ── Pixel readback ────────────────────────────────────────────────────────────

        private bool TryGetLatestDecodePixels(out Color32[] pixels, out int width, out int height)
        {
            pixels = null; width = 0; height = 0;
            if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16 || _webCamTexture.height <= 16) return false;

            width  = _webCamTexture.width;
            height = _webCamTexture.height;

            try
            {
                Color32[] direct = _webCamTexture.GetPixels32();
                if (direct != null && direct.Length > 0 && HasMeaningfulPixelData(direct))
                {
                    pixels = direct;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logcat.Warning("Quest QR direct WebCamTexture read failed: " + ex.Message);
            }

            return TryReadPixelsViaRenderTexture(out pixels, out width, out height);
        }

        private bool TryReadPixelsViaRenderTexture(out Color32[] pixels, out int width, out int height)
        {
            pixels = null; width = 0; height = 0;
            if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16 || _webCamTexture.height <= 16) return false;

            EnsureReadbackTargets(_webCamTexture.width, _webCamTexture.height);
            if (_webCamRenderTexture == null || _readbackTexture == null) return false;

            try
            {
                if (!BlitWebCamToRenderTexture()) return false;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _webCamRenderTexture;
                _readbackTexture.ReadPixels(new Rect(0, 0, _webCamRenderTexture.width, _webCamRenderTexture.height), 0, 0, false);
                _readbackTexture.Apply(false, false);
                RenderTexture.active = previous;

                pixels = _readbackTexture.GetPixels32();
                width  = _readbackTexture.width;
                height = _readbackTexture.height;
                return pixels != null && pixels.Length > 0 && HasMeaningfulPixelData(pixels);
            }
            catch (Exception ex)
            {
                Logcat.Warning("Quest QR RenderTexture read failed: " + ex.Message);
                return false;
            }
        }

        private void RefreshPreviewTexture()
        {
            Texture preview = GetPreferredPreviewTexture();
            if (preview == null) return;
            _latestPreviewTexture = preview;
            Panel.SetPreviewTexture(preview);
        }

        private Texture GetPreferredPreviewTexture()
        {
            if (BlitWebCamToRenderTexture() && _webCamRenderTexture != null) return _webCamRenderTexture;
            return _webCamTexture;
        }

        private bool BlitWebCamToRenderTexture()
        {
            if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16 || _webCamTexture.height <= 16) return false;

            EnsureReadbackTargets(_webCamTexture.width, _webCamTexture.height);
            if (_webCamRenderTexture == null) return false;

            try
            {
                Graphics.Blit(_webCamTexture, _webCamRenderTexture);
                return true;
            }
            catch (Exception ex)
            {
                Logcat.Warning("Quest QR preview blit failed: " + ex.Message);
                return false;
            }
        }

        private void EnsureReadbackTargets(int width, int height)
        {
            if (_webCamRenderTexture == null || _webCamRenderTexture.width != width || _webCamRenderTexture.height != height)
            {
                if (_webCamRenderTexture != null)
                {
                    _webCamRenderTexture.Release();
                    Destroy(_webCamRenderTexture);
                }
                _webCamRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                _webCamRenderTexture.Create();
            }

            if (_readbackTexture == null || _readbackTexture.width != width || _readbackTexture.height != height)
            {
                if (_readbackTexture != null) Destroy(_readbackTexture);
                _readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
        }

        private static bool HasMeaningfulPixelData(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0) return false;
            int samples = Mathf.Min(128, pixels.Length);
            for (int i = 0; i < samples; i++)
            {
                Color32 px = pixels[i];
                if (px.r > 8 || px.g > 8 || px.b > 8) return true;
            }
            return false;
        }

        // ── Meta-specific helpers ─────────────────────────────────────────────────────

        private static bool HasHeadsetCameraPermission()
        {
            try
            {
                using AndroidJavaClass unityPlayer  = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject pkgManager  = activity.Call<AndroidJavaObject>("getPackageManager");
                int check = pkgManager.Call<int>("checkPermission",
                    "horizonos.permission.HEADSET_CAMERA",
                    activity.Call<string>("getPackageName"));
                return check == 0;
            }
            catch (Exception ex)
            {
                Logcat.Warning("Could not check HEADSET_CAMERA permission: " + ex.Message);
                return true;
            }
        }

        private static WebCamDevice? SelectCameraForDevice(WebCamDevice[] devices)
        {
            if (devices == null || devices.Length == 0) return null;

            string[] preferredNames = { "Camera 1" };
            string[] nameContains = { "front", "passthrough" };

            foreach (WebCamDevice d in devices)
                if (preferredNames.Any(p => d.name.Equals(p, StringComparison.OrdinalIgnoreCase))) return d;

            foreach (WebCamDevice d in devices)
                if (d.isFrontFacing) return d;

            foreach (WebCamDevice d in devices)
                if (nameContains.Any(k => d.name.ToLowerInvariant().Contains(k))) return d;

            return devices[0];
        }

        private static bool CheckOpenXRFeatures()
        {
            try
            {
                Type openXRSettingsType = null;
                Type metaSessionFeature = null;
                Type metaCameraFeature = null;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    if (!name.Contains("OpenXR") && !name.Contains("XR")) continue;

                    try
                    {
                        openXRSettingsType ??= assembly.GetType("UnityEngine.XR.OpenXR.OpenXRSettings");
                        metaSessionFeature ??= assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaSessionFeature")
                                            ?? assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaSessionFeature");
                        metaCameraFeature  ??= assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaCameraFeature")
                                            ?? assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaCameraFeature");
                    }
                    catch (Exception ex)
                    {
                        Logcat.Warning($"Error searching assembly {name}: {ex.Message}");
                    }
                }

                if (openXRSettingsType == null)
                {
                    Logcat.Warning("OpenXRSettings type not found — assuming features enabled.");
                    return true;
                }

                var instanceProp = openXRSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object settingsInstance = instanceProp?.GetValue(null);
                if (settingsInstance == null)
                {
                    Logcat.Warning("OpenXRSettings.Instance is null — assuming features enabled.");
                    return true;
                }

                var getFeatureMethod = openXRSettingsType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetFeature" && m.IsGenericMethod);

                if (getFeatureMethod == null)
                {
                    Logcat.Warning("OpenXRSettings.GetFeature not found — assuming features enabled.");
                    return true;
                }

                bool allEnabled = true;

                bool CheckFeature(Type featureType, string label)
                {
                    if (featureType == null) return true;
                    try
                    {
                        object feature = getFeatureMethod.MakeGenericMethod(featureType).Invoke(settingsInstance, null);
                        var enabledProp = featureType.GetProperty("enabled");
                        if (feature != null && enabledProp != null && !(bool)enabledProp.GetValue(feature))
                        {
                            Logcat.Error($"OpenXR feature '{label}' is NOT enabled in Project Settings.");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logcat.Warning($"Could not check '{label}' feature: {ex.Message}");
                    }
                    return true;
                }

                allEnabled &= CheckFeature(metaSessionFeature, "Meta Quest: Session");
                allEnabled &= CheckFeature(metaCameraFeature,  "Meta Quest: Camera (Passthrough)");

                if (!allEnabled) LogMetaOpenXRHelp();
                return allEnabled;
            }
            catch (Exception ex)
            {
                Logcat.Warning($"Error checking OpenXR features: {ex.Message} — assuming enabled.");
                return true;
            }
        }

        private static void LogMetaOpenXRHelp()
        {
            Logcat.Error("Enable the following in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups:");
            Logcat.Error("  Meta Quest Support");
            Logcat.Error("  Meta Quest: Camera (Passthrough)");
            Logcat.Error("  Meta Quest: Session");
        }

        // Keep the decode interval accessible to ScanLoopCoroutine via const
        private const float DecodeIntervalSeconds = 0.5f;
    }
}
#endif

/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - Quest / non-PICO QR Code Reader
 *
 * Uses WebCamTexture + ZXing for supported Android XR devices.
 * The scan-session UX now matches the PICO flow:
 * - shared world-space scan panel
 * - shared cancel / restore PIN pad behavior
 * - shared QR payload parsing
 * - shared button state model
 */
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public class QRCodeReader : MonoBehaviour, IQrScanner
    {
        public static QRCodeReader Instance;
        public static AbxrAuthService AuthService;

        private const float DecodeIntervalSeconds = 0.5f;
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

        private bool _isOfferedOnThisDevice;
        private bool _isScanning;
        private bool _isInitializing;
        private bool _cameraPermissionRequested;
        private bool _headsetPermissionChecked;
        private bool _cameraStartupSucceeded;

        private Action<string> _scanResultCallback;
        private Coroutine _scanCoroutine;

        private WebCamTexture _webCamTexture;
        private RenderTexture _webCamRenderTexture;
        private Texture2D _readbackTexture;
        private Texture _latestPreviewTexture;

        private ZXing.BarcodeReader _barcodeReader;
        private QrScanPanel _panel;
        private bool _restorePinPadOnCancel;

        // Background decode state
        private volatile bool _decodeInProgress;
        private volatile string _pendingDecodeResult;

        bool IQrScanner.IsAvailable => _isOfferedOnThisDevice;
        bool IQrScanner.IsScanning => _isScanning;
        bool IQrScanner.IsInitializing => _isInitializing;
        bool IQrScanner.ArePermissionsDenied => HasDeniedPermissions();
        Texture IQrScanner.GetCameraTexture() => GetCameraTexture();
        void IQrScanner.CancelScan() => CancelScanning();

        private void Awake() => TryActivateScanner();

        private void Start()
        {
            if (!_isOfferedOnThisDevice) StartCoroutine(DelayedDeviceCheck());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            StopScanningInternal(false);
            ShutdownCamera();
        }

        private void TryActivateScanner()
        {
            if (_isOfferedOnThisDevice) return;
            if (!IsDeviceSupported()) return;

            _barcodeReader ??= QrCodeScanCommon.CreateBarcodeReader();
            EnsureRuntimeObjects();

            if (Instance == null)
            {
                Instance = this;
                _isOfferedOnThisDevice = true;
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
            for (int i = 0; i < maxAttempts && !_isOfferedOnThisDevice; i++)
            {
                if (!string.IsNullOrEmpty(DeviceModel.deviceModel))
                {
                    if (IsDeviceSupported())
                    {
                        TryActivateScanner();
                    }
                    else
                    {
                        Logcat.Warning($"Device '{DeviceModel.deviceModel}' is not supported. QR code scanning will not be available.");
                    }

                    yield break;
                }

                yield return null;
            }
        }

        private void EnsureRuntimeObjects()
        {
            _barcodeReader ??= QrCodeScanCommon.CreateBarcodeReader();

            if (_panel == null)
            {
                _panel = GetComponentInChildren<QrScanPanel>(true);
                if (_panel == null) _panel = QrScanPanel.CreateRuntimePanel(transform, CancelScanning);
            }
        }

        private static bool IsDeviceSupported()
        {
            string model = DeviceModel.deviceModel ?? string.Empty;
            return SupportedDevices.Any(supportedDevice => model.Equals(supportedDevice, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsMetaDevice()
        {
            string model = DeviceModel.deviceModel ?? string.Empty;
            return model.Equals("Oculus Quest 3", StringComparison.OrdinalIgnoreCase) ||
                   model.Equals("Oculus Quest 3S", StringComparison.OrdinalIgnoreCase) ||
                   model.Equals("Oculus Quest Pro", StringComparison.OrdinalIgnoreCase);
        }

        public void SetScanResultCallback(Action<string> callback) => _scanResultCallback = callback;

        public Texture GetCameraTexture()
        {
            return _latestPreviewTexture != null ? _latestPreviewTexture : _webCamTexture;
        }

        public void ScanQRCode()
        {
            if (!_isOfferedOnThisDevice)
            {
                Logcat.Warning("Quest QR Code Scanner: ScanQRCode ignored (not available on this device).");
                InvokeAndClearCallback(null);
                return;
            }

            if (_isScanning || _isInitializing)
            {
                Logcat.Info("QR code scanning or initialization already in progress.");
                return;
            }

            EnsureRuntimeObjects();
            StartCoroutine(RequestPermissionThenScanCoroutine());
        }

        public void CancelScanning()
        {
            if (!_isScanning && !_isInitializing) return;

            StopScanningInternal(true);
            Logcat.Info("QR code scanning cancelled by user.");
        }

        public bool IsScanning() => _isScanning;
        public bool IsInitializing() => _isInitializing;

        public bool IsQRScanningAvailable() => _isOfferedOnThisDevice;

        public static bool AreCameraPermissionsDenied()
        {
            return Instance != null && Instance.HasDeniedPermissions();
        }

        private bool HasDeniedPermissions()
        {
            bool cameraDenied = _cameraPermissionRequested && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
            bool headsetDenied = _headsetPermissionChecked && IsMetaDevice() && !HasHeadsetCameraPermission();
            return cameraDenied || headsetDenied;
        }

        private IEnumerator RequestPermissionThenScanCoroutine()
        {
            _isInitializing = true;

            if (IsMetaDevice() && !CheckOpenXRFeatures())
            {
                Logcat.Error("Cannot initialize Quest QR scanner because required OpenXR features are not enabled.");
                _isInitializing = false;
                InvokeAndClearCallback(null);
                yield break;
            }

            // Always request camera permission. If already granted, Android fires the granted
            // callback immediately without showing a dialog. If previously denied, it shows
            // the dialog again so the user gets another chance each time they tap Scan QR Code.
            {
                _cameraPermissionRequested = true;
                bool alreadyHadPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
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
                    Logcat.Warning("Quest QR camera permission denied.");
                    _panel.Show();
                    _panel.SetStatus("Camera permission required");
                    yield return new WaitForSecondsRealtime(3.0f);
                    _isInitializing = false;
                    _panel?.Hide();
                    if (_restorePinPadOnCancel) KeyboardHandler.ShowPinPad();
                    _restorePinPadOnCancel = false;
                    InvokeAndClearCallback(null);
                    yield break;
                }

                // On Quest, the first permission grant can return before headset camera frames are actually flowing.
                // Give the runtime a bit more time to register and warm up before starting preview.
                if (!alreadyHadPermission) yield return new WaitForSecondsRealtime(FirstPermissionWarmupSeconds);

                // The Android permission dialog can temporarily take focus away from the app on Quest.
                // Starting WebCamTexture before focus fully returns is a common cause of a blank first preview.
                yield return StartCoroutine(WaitForAppFocusAfterPermissionCoroutine());
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            if (IsMetaDevice())
            {
                _headsetPermissionChecked = true;
                if (!HasHeadsetCameraPermission())
                {
                    Logcat.Warning("Quest QR scanner requires the Headset Cameras permission. Please enable it and try again.");
                    EnsureRuntimeObjects();
                    _restorePinPadOnCancel = KeyboardHandler.IsPinPadVisible();
                    if (_restorePinPadOnCancel) KeyboardHandler.HidePinPad();
                    _panel.Show();
                    _panel.SetStatus("Headset Cameras permission required");
                    yield return new WaitForSecondsRealtime(3.0f);
                    _isInitializing = false;
                    _panel?.Hide();
                    if (_restorePinPadOnCancel) KeyboardHandler.ShowPinPad();
                    _restorePinPadOnCancel = false;
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
            _panel.Show();
            _panel.SetStatus("Starting camera...");
            _panel.SetPreviewUvRect(new Rect(0f, 0f, 1f, 1f));

            _cameraStartupSucceeded = false;
            yield return StartCoroutine(StartCameraCoroutine());

            if (!_cameraStartupSucceeded)
            {
                if (_isInitializing) StopScanningInternal(true);
                yield break;
            }

            _isInitializing = false;
            _isScanning = true;
            RefreshPreviewTexture();
            _panel.SetStatus("Look at QR Code");

            float nextDecodeTime = 0f;
            while (_isScanning)
            {
                if (_webCamTexture != null && _webCamTexture.isPlaying) RefreshPreviewTexture();

                // Check if the background decode finished with a result
                if (_pendingDecodeResult != null)
                {
                    string scanResult = _pendingDecodeResult;
                    _pendingDecodeResult = null;
                    StopScanningInternal(false);
                    ProcessQrScanResult(scanResult);
                    yield break;
                }

                // Kick off a background decode if none is running and the interval has elapsed
                if (!_decodeInProgress && Time.unscaledTime >= nextDecodeTime)
                {
                    nextDecodeTime = Time.unscaledTime + DecodeIntervalSeconds;

                    if (TryGetLatestDecodePixels(out Color32[] pixels, out int width, out int height))
                    {
                        Color32[] pixelsCopy = pixels;
                        int w = width, h = height;
                        ZXing.BarcodeReader reader = _barcodeReader;
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

        private IEnumerator StartCameraCoroutine()
        {
            ShutdownCamera();

            WebCamDevice? selectedCamera = null;
            float discoveryDeadline = Time.unscaledTime + DeviceDiscoveryTimeoutSeconds;
            while (_isInitializing && Time.unscaledTime < discoveryDeadline)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                selectedCamera = SelectCameraForDevice(devices);
                if (selectedCamera.HasValue) break;
                yield return null;
            }

            if (!selectedCamera.HasValue)
            {
                Logcat.Error("No camera found. Cannot scan QR codes.");
                yield break;
            }

            for (int attempt = 1; attempt <= CameraStartAttempts && _isInitializing; attempt++)
            {
                yield return StartCoroutine(StartCameraAttemptCoroutine(selectedCamera.Value, attempt));
                if (_cameraStartupSucceeded) yield break;

                ShutdownCamera();

                if (attempt < CameraStartAttempts)
                {
                    Logcat.Warning($"Quest QR camera attempt {attempt} did not produce a valid preview frame. Retrying.");
                    yield return new WaitForSecondsRealtime(CameraRetryDelaySeconds);
                    yield return StartCoroutine(WaitForAppFocusAfterPermissionCoroutine());
                }
            }

            if (_isInitializing) Logcat.Warning("Quest QR camera did not start before timeout.");
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

            while (_isInitializing && Time.unscaledTime < deadline)
            {
                if (!Application.isFocused)
                {
                    consecutiveGoodFrames = 0;
                    yield return null;
                    continue;
                }

                if (_webCamTexture != null && _webCamTexture.isPlaying && _webCamTexture.width > 16 && _webCamTexture.height > 16)
                {
                    EnsureReadbackTargets(_webCamTexture.width, _webCamTexture.height);

                    if (_webCamTexture.didUpdateThisFrame)
                    {
                        RefreshPreviewTexture();

                        if (TryGetLatestDecodePixels(out _, out _, out _))
                        {
                            consecutiveGoodFrames++;
                            if (consecutiveGoodFrames >= 2)
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

            Logcat.Warning($"Quest QR camera start attempt {attemptNumber} timed out without a valid visible frame.");
        }

        private IEnumerator WaitForAppFocusAfterPermissionCoroutine()
        {
            float deadline = Time.unscaledTime + FocusReturnTimeoutSeconds;
            while (_isInitializing && !Application.isFocused && Time.unscaledTime < deadline)
            {
                yield return null;
            }
        }

        private bool TryGetLatestDecodePixels(out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16 || _webCamTexture.height <= 16) return false;

            width = _webCamTexture.width;
            height = _webCamTexture.height;

            try
            {
                Color32[] directPixels = _webCamTexture.GetPixels32();
                if (directPixels != null && directPixels.Length > 0 && HasMeaningfulPixelData(directPixels))
                {
                    pixels = directPixels;
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
            pixels = null;
            width = 0;
            height = 0;

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
                width = _readbackTexture.width;
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
            Texture previewTexture = GetPreferredPreviewTexture();
            if (previewTexture == null) return;

            _latestPreviewTexture = previewTexture;
            _panel.SetPreviewTexture(previewTexture);
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

        private void ShutdownCamera()
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

            _latestPreviewTexture = null;
            _cameraStartupSucceeded = false;
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
            _decodeInProgress = false;
            _pendingDecodeResult = null;
            _panel?.Hide();
            ShutdownCamera();

            if (invokeCallbackWithNull && _restorePinPadOnCancel) KeyboardHandler.ShowPinPad();

            _restorePinPadOnCancel = false;

            if (invokeCallbackWithNull) InvokeAndClearCallback(null);
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

        private static bool HasHeadsetCameraPermission()
        {
            if (!IsMetaDevice()) return true;

            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                int permissionCheck = packageManager.Call<int>("checkPermission", "horizonos.permission.HEADSET_CAMERA", currentActivity.Call<string>("getPackageName"));
                return permissionCheck == 0;
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

            string[] preferredNames;
            string[] nameContains;

            if (IsMetaDevice())
            {
                preferredNames = new[] { "Camera 1" };
                nameContains = new[] { "front", "passthrough" };
            }
            else
            {
                preferredNames = Array.Empty<string>();
                nameContains = new[] { "front", "camera", "passthrough" };
            }

            foreach (WebCamDevice device in devices)
            {
                if (preferredNames.Any(p => device.name.Equals(p, StringComparison.OrdinalIgnoreCase))) return device;
            }

            foreach (WebCamDevice device in devices)
            {
                if (device.isFrontFacing) return device;
            }

            foreach (WebCamDevice device in devices)
            {
                string lower = device.name.ToLowerInvariant();
                if (nameContains.Any(k => lower.Contains(k))) return device;
            }

            return devices[0];
        }

        private static bool CheckOpenXRFeatures()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type openXRSettingsType = null;
                Type metaSessionFeatureType = null;
                Type metaCameraFeatureType = null;

                foreach (Assembly assembly in assemblies)
                {
                    string assemblyName = assembly.GetName().Name;
                    if (!assemblyName.Contains("OpenXR") && !assemblyName.Contains("XR")) continue;

                    try
                    {
                        Type settingsType = assembly.GetType("UnityEngine.XR.OpenXR.OpenXRSettings");
                        if (settingsType != null) openXRSettingsType = settingsType;

                        if (metaSessionFeatureType == null)
                        {
                            metaSessionFeatureType = assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaSessionFeature")
                                                   ?? assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaSessionFeature");
                        }

                        if (metaCameraFeatureType == null)
                        {
                            metaCameraFeatureType = assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaCameraFeature")
                                                  ?? assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaCameraFeature");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logcat.Warning($"Error searching assembly {assemblyName}: {ex.Message}");
                    }
                }

                if (openXRSettingsType == null)
                {
                    Logcat.Warning("OpenXRSettings type not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true;
                }

                PropertyInfo instanceProperty = openXRSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null)
                {
                    Logcat.Warning("OpenXRSettings.Instance property not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true;
                }

                object openXRSettingsInstance = instanceProperty.GetValue(null);
                if (openXRSettingsInstance == null)
                {
                    Logcat.Warning("OpenXRSettings.Instance is null. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true;
                }

                MethodInfo getFeatureMethod = openXRSettingsType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "GetFeature" && method.IsGenericMethod);

                if (getFeatureMethod == null)
                {
                    Logcat.Warning("OpenXRSettings.GetFeature generic method not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true;
                }

                bool allFeaturesEnabled = true;
                string missingFeatures = string.Empty;

                if (metaSessionFeatureType != null)
                {
                    try
                    {
                        MethodInfo getSessionFeature = getFeatureMethod.MakeGenericMethod(metaSessionFeatureType);
                        object sessionFeature = getSessionFeature.Invoke(openXRSettingsInstance, null);
                        if (sessionFeature != null)
                        {
                            PropertyInfo enabledProperty = metaSessionFeatureType.GetProperty("enabled");
                            if (enabledProperty != null)
                            {
                                bool sessionEnabled = (bool)enabledProperty.GetValue(sessionFeature);
                                if (!sessionEnabled)
                                {
                                    Logcat.Error("OpenXR feature 'Meta Quest: Session' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Session, ";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logcat.Warning($"Could not check Meta Quest: Session feature: {ex.Message}");
                    }
                }

                if (metaCameraFeatureType != null)
                {
                    try
                    {
                        MethodInfo getCameraFeature = getFeatureMethod.MakeGenericMethod(metaCameraFeatureType);
                        object cameraFeature = getCameraFeature.Invoke(openXRSettingsInstance, null);
                        if (cameraFeature != null)
                        {
                            PropertyInfo enabledProperty = metaCameraFeatureType.GetProperty("enabled");
                            if (enabledProperty != null)
                            {
                                bool cameraEnabled = (bool)enabledProperty.GetValue(cameraFeature);
                                if (!cameraEnabled)
                                {
                                    Logcat.Error("OpenXR feature 'Meta Quest: Camera (Passthrough)' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Camera (Passthrough), ";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logcat.Warning($"Could not check Meta Quest: Camera (Passthrough) feature: {ex.Message}");
                    }
                }

                if (!allFeaturesEnabled)
                {
                    Logcat.Error($"Missing required OpenXR features: {missingFeatures.TrimEnd(',', ' ')}");
                    LogMetaOpenXRHelp();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logcat.Warning($"Error checking OpenXR features: {ex.Message}. Assuming features are enabled.");
                return true;
            }
        }

        private static void LogMetaOpenXRHelp()
        {
            if (!IsMetaDevice()) return;

            Logcat.Error("Please enable the following in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups:");
            Logcat.Error("Meta Quest Support");
            Logcat.Error("Meta Quest: Camera (Passthrough)");
            Logcat.Error("Meta Quest: Session");
        }
    }
}
#endif

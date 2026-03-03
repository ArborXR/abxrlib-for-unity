/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - Unified QR Code Reader
 *
 * Standard QR code reader for Android XR devices other than PICO. Uses WebCamTexture and ZXing.
 * PICO devices use QRCodeReaderPico (PICO SDK) instead, as platform camera access requires their SDK.
 *
 * Supported devices: Meta Quest 3/3S/Pro and others in SupportedDevices (PICO excluded).
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if UNITY_ANDROID && !UNITY_EDITOR

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;
using ZXing;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Unified QR code reader for Android XR devices (Meta Quest, PICO, etc.). Uses WebCamTexture + ZXing.
    /// Only activates on supported devices; camera discovery is device-aware for different headset vendors.
    /// </summary>
    public class QRCodeReader : MonoBehaviour
    {
        public static QRCodeReader Instance;
        public static AbxrAuthService AuthService;

        // Supported devices: Meta Quest and other non-PICO XR devices. PICO uses QRCodeReaderPico (SDK) instead.
        private static readonly string[] SupportedDevices =
        {
            "Oculus Quest 3",
            "Oculus Quest 3S",
            "Oculus Quest Pro"
        };
        
        // WebCamTexture (Meta's Passthrough Camera API routes through WebCamTexture on Quest)
        private WebCamTexture _webCamTexture;
        private RenderTexture _webCamRenderTexture; // RenderTexture for WebCamTexture processing
        
        private bool _isScanning;
        private bool _isInitializing; // True when camera is being initialized
        private bool _cameraInitialized;
        private Coroutine _scanningCoroutine;
        
        // ZXing barcode reader instance
        private BarcodeReader _barcodeReader;
        
        // Overlay UI for passthrough mode
        private GameObject _overlayCanvas;

        // One-shot callback when using StartQRScanForAuthInput (developer API). When set, scan result is delivered here instead of KeyboardAuthenticate.
        private Action<string> _scanResultCallback;
        
        private void Awake()
        {
            // Check if device is supported
            // Only check if device model is already available (may be empty at startup)
            if (!string.IsNullOrEmpty(DeviceModel.deviceModel))
            {
                if (!IsDeviceSupported())
                {
                    Debug.LogWarning($"[AbxrLib] Disabling QR Code Scanner. Device '{DeviceModel.deviceModel}' is not supported for QR code reading.");
                    return;
                }
                
                Debug.Log($"[AbxrLib] Device '{DeviceModel.deviceModel}' is supported for QR code reading.");
            }
            else
            {
                // Device model not available yet - will be checked in DelayedDeviceCheck()
                // Don't log a warning, this is expected at startup
                return;
            }
            
            // Initialize ZXing barcode reader
            try
            {
                _barcodeReader = new BarcodeReader();
                // Configure to only read QR codes
                _barcodeReader.Options.PossibleFormats = new System.Collections.Generic.List<BarcodeFormat> { BarcodeFormat.QR_CODE };
                Debug.Log("[AbxrLib] ZXing barcode reader initialized successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AbxrLib] Failed to initialize ZXing: {ex.Message}");
                return;
            }
            
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("[AbxrLib] QRCodeReader Instance activated successfully.");
            }
            else
            {
                Debug.LogWarning("[AbxrLib] QRCodeReader Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Always run delayed check if Instance wasn't set in Awake
            if (Instance == null)
            {
                StartCoroutine(DelayedDeviceCheck());
            }
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (_webCamRenderTexture != null)
            {
                _webCamRenderTexture.Release();
                Destroy(_webCamRenderTexture);
                _webCamRenderTexture = null;
            }
            
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                {
                    _webCamTexture.Stop();
                }
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }
        }
        
        /// <summary>
        /// Delayed device check coroutine - waits for device model to be available
        /// </summary>
        private IEnumerator DelayedDeviceCheck()
        {
            // Wait a short time for device model to be populated
            yield return new WaitForSeconds(0.5f);
            
            if (!string.IsNullOrEmpty(DeviceModel.deviceModel))
            {
                if (IsDeviceSupported())
                {
                    Debug.Log($"[AbxrLib] Device '{DeviceModel.deviceModel}' is QR Code Reader supported. Initializing...");
                    
                    // Initialize ZXing if not already done
                    if (_barcodeReader == null)
                    {
                        try
                        {
                            _barcodeReader = new BarcodeReader();
                            _barcodeReader.Options.PossibleFormats = new System.Collections.Generic.List<BarcodeFormat> { BarcodeFormat.QR_CODE };
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[AbxrLib] Failed to initialize ZXing in delayed check: {ex.Message}");
                            yield break;
                        }
                    }
                    
                    if (Instance == null)
                    {
                        Instance = this;
                    }
                    
                    // Request camera permissions proactively when device is supported
#if UNITY_ANDROID && !UNITY_EDITOR
                    if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                    {
                        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                        // Wait a moment for permission dialog to appear
                        yield return new WaitForSeconds(0.5f);
                    }
#endif
                }
                else
                {
                    Debug.LogWarning($"[AbxrLib] Device '{DeviceModel.deviceModel}' is not supported. QR code scanning will not be available.");
                }
            }
            else
            {
                Debug.LogWarning("[AbxrLib] Device model not available after delay. QR code scanning will not be available.");
            }
        }
        
        /// <summary>
        /// Check if the current device is supported for QR code reading
        /// </summary>
        private static bool IsDeviceSupported()
        {
            string model = DeviceModel.deviceModel ?? "";
            return SupportedDevices.Any(supportedDevice => model.Equals(supportedDevice, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True when the device is a Meta Quest (Quest 3, 3S, Pro). Used for Quest-specific permissions and OpenXR checks.
        /// </summary>
        private static bool IsMetaDevice()
        {
            string model = DeviceModel.deviceModel ?? "";
            return model.Equals("Oculus Quest 3", System.StringComparison.OrdinalIgnoreCase) ||
                   model.Equals("Oculus Quest 3S", System.StringComparison.OrdinalIgnoreCase) ||
                   model.Equals("Oculus Quest Pro", System.StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Check if required OpenXR features are enabled in Project Settings
        /// </summary>
        /// <param name="verbose">If true, logs success messages. If false, only logs errors.</param>
        private static bool CheckOpenXRFeatures(bool verbose = true)
        {
            try
            {
                // Use reflection to access OpenXRSettings
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                System.Type openXRSettingsType = null;
                System.Type metaSessionFeatureType = null;
                System.Type metaCameraFeatureType = null;
                
                // Find OpenXRSettings type
                foreach (Assembly assembly in assemblies)
                {
                    string assemblyName = assembly.GetName().Name;
                    if (assemblyName.Contains("OpenXR") || assemblyName.Contains("XR"))
                    {
                        try
                        {
                            // Try to find OpenXRSettings
                            System.Type settingsType = assembly.GetType("UnityEngine.XR.OpenXR.OpenXRSettings");
                            if (settingsType != null)
                            {
                                openXRSettingsType = settingsType;
                            }
                            
                            // Try to find Meta feature types
                            if (metaSessionFeatureType == null)
                            {
                                metaSessionFeatureType = assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaSessionFeature");
                                if (metaSessionFeatureType == null)
                                {
                                    metaSessionFeatureType = assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaSessionFeature");
                                }
                            }
                            
                            if (metaCameraFeatureType == null)
                            {
                                metaCameraFeatureType = assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaCameraFeature");
                                if (metaCameraFeatureType == null)
                                {
                                    metaCameraFeatureType = assembly.GetType("Unity.XR.OpenXR.Features.MetaQuestSupport.MetaCameraFeature");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[AbxrLib] Error searching assembly {assemblyName}: {ex.Message}");
                        }
                    }
                }
                
                if (openXRSettingsType == null)
                {
                    Debug.LogWarning("[AbxrLib] OpenXRSettings type not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                // Get Instance property
                PropertyInfo instanceProperty = openXRSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null)
                {
                    Debug.LogWarning("[AbxrLib] OpenXRSettings.Instance property not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                object openXRSettingsInstance = instanceProperty.GetValue(null);
                if (openXRSettingsInstance == null)
                {
                    Debug.LogWarning("[AbxrLib] OpenXRSettings.Instance is null. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                // Get GetFeature method - handle overloads by getting all methods and finding the generic one
                MethodInfo getFeatureMethod = null;
                MethodInfo[] allMethods = openXRSettingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (MethodInfo method in allMethods)
                {
                    if (method.Name == "GetFeature" && method.IsGenericMethod)
                    {
                        getFeatureMethod = method;
                        break;
                    }
                }
                
                if (getFeatureMethod == null)
                {
                    Debug.LogWarning("[AbxrLib] OpenXRSettings.GetFeature generic method not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                bool allFeaturesEnabled = true;
                string missingFeatures = "";
                
                // Check Meta Quest: Session feature
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
                                    Debug.LogError("[AbxrLib] OpenXR feature 'Meta Quest: Session' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Session, ";
                                }
                                else if (verbose)
                                {
                                    Debug.Log("[AbxrLib] OpenXR feature 'Meta Quest: Session' is enabled.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[AbxrLib] MetaSessionFeature not found. It may not be installed or configured.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[AbxrLib] Could not check Meta Quest: Session feature: {ex.Message}");
                    }
                }
                else
                {
                    // MetaSessionFeature type not found - this is expected in some Unity versions, continue without verification
                }
                
                // Check Meta Quest: Camera (Passthrough) feature
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
                                    Debug.LogError("[AbxrLib] OpenXR feature 'Meta Quest: Camera (Passthrough)' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Camera (Passthrough), ";
                                }
                                else if (verbose)
                                {
                                    Debug.Log("[AbxrLib] OpenXR feature 'Meta Quest: Camera (Passthrough)' is enabled.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[AbxrLib] MetaCameraFeature not found. It may not be installed or configured.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[AbxrLib] Could not check Meta Quest: Camera (Passthrough) feature: {ex.Message}");
                    }
                }
                else
                {
                    // MetaCameraFeature type not found - this is expected in some Unity versions, continue without verification
                }
                
                if (!allFeaturesEnabled)
                {
                    Debug.LogError($"[AbxrLib] Missing required OpenXR features: {missingFeatures.TrimEnd(',', ' ')}");
                    LogMetaOpenXRHelp();
                    return false;
                }
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AbxrLib] Error checking OpenXR features: {ex.Message}. Assuming features are enabled.");
                return true; // Don't block if we can't check
            }
        }
        
        private static void LogMetaOpenXRHelp()
        {
            if (!IsMetaDevice()) return;
            Debug.LogError("[AbxrLib] Please enable the following in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups:");
            Debug.LogError("[AbxrLib]   - Meta Quest Support");
            Debug.LogError("[AbxrLib]   - Meta Quest: Camera (Passthrough)");
            Debug.LogError("[AbxrLib]   - Meta Quest: Session");
        }
        
        /// <summary>
        /// Start scanning for QR codes
        /// </summary>
        public void ScanQRCode()
        {
            if (_isScanning || _isInitializing)
            {
                Debug.Log("[AbxrLib] QR code scanning or initialization already in progress");
                return;
            }
            
            // Check if QR scanning is available (device support, permissions)
            if (!IsQRScanningAvailable())
            {
                Debug.LogWarning("[AbxrLib] QR code scanning is not available. Check device support and camera permissions.");
                return;
            }
            
            // Check if camera needs to be initialized or reinitialized
            if (!_cameraInitialized || _webCamTexture == null)
            {
                _isInitializing = true; // Set initializing state so button shows "Initializing..."
                _cameraInitialized = false;
                StartCoroutine(InitializeCamera());
            }
            else
            {
                // Camera already initialized, start scanning immediately
                _isScanning = true;
                StartScanning();
            }
        }
        
        /// <summary>
        /// Cancel/stop QR code scanning
        /// </summary>
        public void CancelScanning()
        {
            if (!_isScanning && !_isInitializing)
            {
                return;
            }
            
            // Reset both states
            _isInitializing = false;
            StopScanning();
            Debug.Log("[AbxrLib] QR code scanning cancelled by user");
        }
        
        /// <summary>
        /// Check if currently scanning for QR codes
        /// </summary>
        public bool IsScanning() => _isScanning;

        /// <summary>
        /// Check if camera is currently being initialized
        /// </summary>
        public bool IsInitializing() => _isInitializing;

        /// <summary>
        /// Check if QR code scanning is available (device supported, permissions granted)
        /// </summary>
        public bool IsQRScanningAvailable()
        {
            // Check if device is supported
            if (!IsDeviceSupported())
            {
                Debug.LogWarning("[AbxrLib] QR scanning not available - device not supported");
                return false;
            }
            
            // Check camera permissions
#if UNITY_ANDROID && !UNITY_EDITOR
            bool cameraPermissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
            if (!cameraPermissionGranted)
            {
                Debug.LogWarning("[AbxrLib] QR scanning not available - CAMERA permission not granted");
                return false;
            }
            
            // On Meta Quest, HEADSET_CAMERA is required for passthrough camera; other devices use standard Camera only
            if (IsMetaDevice())
            {
                try
                {
                    using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                    int permissionCheck = packageManager.Call<int>("checkPermission", "horizonos.permission.HEADSET_CAMERA",
                        currentActivity.Call<string>("getPackageName"));
                    if (permissionCheck != 0)
                    {
                        Debug.LogWarning("[AbxrLib] QR scanning not available - HEADSET_CAMERA permission check failed (Quest)");
                        return false;
                    }
                }
                catch (System.Exception)
                {
                    Debug.LogWarning("[AbxrLib] QR scanning not available - HEADSET_CAMERA permission check failed");
                    return false;
                }
            }
#endif
            return true;
        }
        
        /// <summary>
        /// Open the app's settings page where users can enable HEADSET_CAMERA permission
        /// </summary>
        public void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                string packageName = currentActivity.Call<string>("getPackageName");
                    
                // Create intent to open app settings
                using AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
                using AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
                // Create URI for app-specific settings
                AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + packageName);
                        
                // Create intent with ACTION_APPLICATION_DETAILS_SETTINGS
                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
                        
                // Add FLAG_ACTIVITY_NEW_TASK flag
                int flagNewTask = 0x10000000; // FLAG_ACTIVITY_NEW_TASK
                intent.Call<AndroidJavaObject>("addFlags", flagNewTask);
                        
                // Start the activity
                currentActivity.Call("startActivity", intent);
                Debug.Log("[AbxrLib] Opened app settings. Please enable 'Headset Cameras' permission and return to the app.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AbxrLib] Failed to open app settings: {ex.Message}");
                Debug.LogWarning("[AbxrLib] Please manually open Quest Settings > Privacy & Safety > App Permissions > Headset Cameras");
            }
#else
            Debug.LogWarning("[AbxrLib] OpenAppSettings() is only available on Android devices.");
#endif
        }
        
        /// <summary>
        /// Check if camera permissions are denied (for UI feedback)
        /// </summary>
        public static bool AreCameraPermissionsDenied()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                return true;
            if (IsMetaDevice())
            {
                try
                {
                    using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                    int permissionCheck = packageManager.Call<int>("checkPermission", "horizonos.permission.HEADSET_CAMERA",
                        currentActivity.Call<string>("getPackageName"));
                    if (permissionCheck == -1) return true;
                }
                catch (System.Exception) { return false; }
            }
#endif
            return false;
        }
        
        /// <summary>
        /// Initialize camera using WebCamTexture (Meta's Passthrough Camera API routes through WebCamTexture on Quest)
        /// </summary>
        private IEnumerator InitializeCamera()
        {
            // On Meta Quest, verify OpenXR camera features are enabled; other devices skip this
            if (IsMetaDevice() && !CheckOpenXRFeatures())
            {
                Debug.LogError("[AbxrLib] Cannot initialize camera - required OpenXR features are not enabled.");
                LogMetaOpenXRHelp();
                _isInitializing = false;
                yield break;
            }

            // Request camera permission on Android
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                yield return new WaitForSeconds(0.5f);
                
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Debug.LogError("[AbxrLib] Camera permission denied. Cannot scan QR codes.");
                    _isInitializing = false; // Reset on failure
                    yield break;
                }
            }
#endif
            
            yield return StartCoroutine(InitializeWebCamTexture());
            
            if (!_cameraInitialized)
            {
                Debug.LogError("[AbxrLib] Failed to initialize camera. QR code scanning will not be available.");
                _isInitializing = false; // Reset on failure
            }
        }
        
        /// <summary>
        /// Select the best camera for the current device. Device-aware so Meta, PICO, and future devices can use different names/paths.
        /// </summary>
        private static WebCamDevice? SelectCameraForDevice(WebCamDevice[] devices)
        {
            if (devices == null || devices.Length == 0) return null;

            string model = DeviceModel.deviceModel ?? "";
            string[] preferredNames = null;
            string[] nameContains = null;

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
                if (preferredNames != null && preferredNames.Any(p => device.name.Equals(p, System.StringComparison.OrdinalIgnoreCase)))
                    return device;
            }
            foreach (WebCamDevice device in devices)
            {
                if (device.isFrontFacing) return device;
            }
            foreach (WebCamDevice device in devices)
            {
                string lower = device.name.ToLower();
                if (nameContains != null && nameContains.Any(k => lower.Contains(k)))
                    return device;
            }
            Debug.Log($"[AbxrLib] No preferred camera found for device '{model}'. Using first available: '{devices[0].name}'");
            return devices[0];
        }

        /// <summary>
        /// Initialize WebCamTexture with device-aware camera selection.
        /// </summary>
        private IEnumerator InitializeWebCamTexture()
        {
            // Request camera permissions on Android
#if UNITY_ANDROID && !UNITY_EDITOR
            // Request standard camera permission
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                yield return new WaitForSeconds(0.5f);
                
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Debug.LogError("[AbxrLib] Camera permission denied. Cannot scan QR codes.");
                    _isInitializing = false; // Reset on failure
                    yield break;
                }
            }
            
            if (IsMetaDevice())
            {
                try
                {
                    using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                    int permissionCheck = packageManager.Call<int>("checkPermission", "horizonos.permission.HEADSET_CAMERA",
                        currentActivity.Call<string>("getPackageName"));
                    if (permissionCheck != 0)
                        Debug.LogWarning("[AbxrLib] HEADSET_CAMERA permission not granted. Quest camera may not work.");
                }
                catch (System.Exception ex) { Debug.LogWarning($"[AbxrLib] Could not check HEADSET_CAMERA: {ex.Message}"); }
            }
#endif

            WebCamDevice[] devices = WebCamTexture.devices;
            WebCamDevice? selectedCamera = SelectCameraForDevice(devices);
            
            if (!selectedCamera.HasValue)
            {
                Debug.LogError("[AbxrLib] No camera found. Cannot scan QR codes.");
                yield break;
            }
            
            // Create WebCamTexture with explicit resolution and FPS (based on working implementation)
            // Try higher resolution first (1920x1080), fallback to system default if that fails
            int requestedWidth = 1920;
            int requestedHeight = 1080;
            int requestedFPS = 30;
            
            _webCamTexture = new WebCamTexture(selectedCamera.Value.name, requestedWidth, requestedHeight, requestedFPS);
            _webCamTexture.requestedFPS = requestedFPS; // Explicitly set FPS
            _webCamTexture.Play();
            
            // Wait for camera to start and get valid dimensions
            int waitCount = 0;
            while (waitCount < 50 && (_webCamTexture.width <= 0 || _webCamTexture.height <= 0))
            {
                yield return new WaitForSeconds(0.1f);
                waitCount++;
                if (waitCount % 10 == 0)
                {
                    Debug.Log($"[AbxrLib] Waiting for WebCamTexture dimensions... (attempt {waitCount}/50, size: {_webCamTexture.width}x{_webCamTexture.height}, isPlaying: {_webCamTexture.isPlaying})");
                }
            }
            
            if (_webCamTexture.width > 0 && _webCamTexture.height > 0)
            {
                // Create RenderTexture for WebCamTexture
                if (_webCamRenderTexture == null)
                {
                    _webCamRenderTexture = new RenderTexture(_webCamTexture.width, _webCamTexture.height, 0, RenderTextureFormat.ARGB32);
                    _webCamRenderTexture.Create();
                }
                
                // TEST PHASE: Wait for camera to start producing non-black frames (separate from display)
                int waitForFrames = 0;
                bool gotValidFrame = false;
                while (waitForFrames < 50 && !gotValidFrame) // Wait up to 5 seconds
                {
                    yield return new WaitForSeconds(0.1f);
                    waitForFrames++;
                    
                    // Use dedicated test method to check camera validity
                    if (_webCamTexture.width > 0 && _webCamTexture.height > 0 && _webCamTexture.isPlaying)
                    {
                        gotValidFrame = TestCameraValidity(_webCamTexture, sampleSize: 1000, threshold: 0.01f, verbose: false);
                        
                        if (gotValidFrame)
                        {
                            Debug.Log($"[AbxrLib] Camera initialized successfully after {waitForFrames * 0.1f:F1} seconds");
                        }
                    }
                }
                
                if (!gotValidFrame)
                {
                    // Try other cameras as fallback (skip the one we already tried)
                    WebCamDevice? fallbackCamera = null;
                    foreach (WebCamDevice device in devices)
                    {
                        if (device.name != selectedCamera.Value.name)
                        {
                            fallbackCamera = device;
                            break;
                        }
                    }
                    
                    if (fallbackCamera.HasValue)
                    {
                        // Stop and destroy current camera
                        if (_webCamTexture != null)
                        {
                            _webCamTexture.Stop();
                            Destroy(_webCamTexture);
                            _webCamTexture = null;
                        }
                        
                        // Try fallback camera with same settings
                        _webCamTexture = new WebCamTexture(fallbackCamera.Value.name, requestedWidth, requestedHeight, requestedFPS);
                        _webCamTexture.requestedFPS = requestedFPS;
                        _webCamTexture.Play();
                        
                        // Wait for dimensions
                        waitCount = 0;
                        while (waitCount < 30 && (_webCamTexture.width <= 0 || _webCamTexture.height <= 0))
                        {
                            yield return new WaitForSeconds(0.1f);
                            waitCount++;
                        }
                        
                        if (_webCamTexture.width > 0 && _webCamTexture.height > 0)
                        {
                            // Test fallback camera - wait a bit longer for it to initialize
                            int fallbackWait = 0;
                            while (fallbackWait < 50 && !gotValidFrame)
                            {
                                yield return new WaitForSeconds(0.1f);
                                fallbackWait++;
                                gotValidFrame = TestCameraValidity(_webCamTexture, sampleSize: 1000, threshold: 0.01f, verbose: false);
                            }
                            
                            if (gotValidFrame)
                            {
                                selectedCamera = fallbackCamera; // Update reference
                            }
                        }
                    }
                }
                
                _cameraInitialized = true;
                
                _isInitializing = false; // Initialization complete
                _isScanning = true; // Now actually scanning
                StartScanning();
            }
            else
            {
                Debug.LogError($"[AbxrLib] Failed to initialize WebCamTexture. Final state: isPlaying={_webCamTexture.isPlaying}, size={_webCamTexture.width}x{_webCamTexture.height}");
                Debug.LogError("[AbxrLib] TROUBLESHOOTING:");
                Debug.LogError("[AbxrLib] Check camera permissions and that a camera is available for this device.");
                _isInitializing = false; // Reset on failure
                if (_webCamTexture != null)
                {
                    _webCamTexture.Stop();
                    Destroy(_webCamTexture);
                    _webCamTexture = null;
                }
            }
        }
        
        /// <summary>
        /// Set the one-shot callback for developer API (StartQRScanForAuthInput). When set, OnQRCodeScanned invokes this instead of KeyboardAuthenticate, and we do not create the built-in overlay.
        /// </summary>
        public void SetScanResultCallback(Action<string> callback)
        {
            _scanResultCallback = callback;
        }

        /// <summary>
        /// Returns the camera texture (WebCamTexture) for embedding in custom UI. Null if not available or not yet initialized.
        /// </summary>
        public Texture GetCameraTexture()
        {
            return _webCamTexture;
        }

        /// <summary>
        /// Start the scanning coroutine
        /// </summary>
        private void StartScanning()
        {
            // Check if we have a valid camera source
            if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamRenderTexture == null)
            {
                Debug.LogError($"[AbxrLib] Camera not ready for scanning. WebCamTexture: {_webCamTexture != null}, isPlaying: {_webCamTexture?.isPlaying}, RenderTexture: {_webCamRenderTexture != null}");
                return;
            }
            
            _isScanning = true;
            // When using developer API (callback set), do not create overlay so they can use GetCameraTexture() in their own UI
            if (_scanResultCallback == null)
                CreateOverlayUI();
            
            // Update camera texture in overlay if it already exists
            if (_overlayCanvas != null)
            {
                RawImage[] rawImages = _overlayCanvas.GetComponentsInChildren<RawImage>();
                bool found = false;
                foreach (RawImage img in rawImages)
                {
                    if (img.gameObject.name == "CameraDisplay")
                    {
                        // Use WebCamTexture
                        if (_webCamTexture != null)
                        {
                            img.texture = _webCamTexture;
                        }
                        
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogWarning("[AbxrLib] CameraDisplay RawImage not found in overlay UI");
                }
            }
            else
            {
                Debug.LogWarning($"[AbxrLib] Cannot update overlay - overlayCanvas: {_overlayCanvas != null}");
            }
            
            _scanningCoroutine = StartCoroutine(ScanForQRCode());
        }
        
        /// <summary>
        /// Stop scanning
        /// </summary>
        private void StopScanning()
        {
            _isScanning = false;
            if (_scanningCoroutine != null)
            {
                StopCoroutine(_scanningCoroutine);
                _scanningCoroutine = null;
            }
            if (_scanResultCallback != null)
            {
                var cb = _scanResultCallback;
                _scanResultCallback = null;
                cb?.Invoke(null);
            }
            DestroyOverlayUI();
        }
        
        /// <summary>
        /// Coroutine to continuously scan for QR codes
        /// </summary>
        private IEnumerator ScanForQRCode()
        {
            int scanCount = 0;
            
            while (_isScanning)
            {
                Texture2D snapshot = null;
                bool frameError = false;
                
                try
                {
                    if (_webCamTexture != null && _webCamTexture.isPlaying)
                    {
                        // Use WebCamTexture fallback
                        if (_webCamTexture.width > 0 && _webCamTexture.height > 0)
                        {
                            // Try direct GetPixels32 first (more reliable on some Android devices)
                            try
                            {
                                Color32[] pixels = _webCamTexture.GetPixels32();
                                if (pixels != null && pixels.Length > 0)
                                {
                                    // Check if we have non-black pixels
                                    bool hasNonBlack = false;
                                    for (int i = 0; i < Mathf.Min(100, pixels.Length); i++)
                                    {
                                        if (pixels[i].r > 10 || pixels[i].g > 10 || pixels[i].b > 10)
                                        {
                                            hasNonBlack = true;
                                            break;
                                        }
                                    }
                                    
                                    if (hasNonBlack)
                                    {
                                        // Create snapshot from pixels directly
                                        snapshot = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGB24, false);
                                        snapshot.SetPixels32(pixels);
                                        snapshot.Apply();
                                    }
                                    else
                                    {
                                        // All black, try RenderTexture approach
                                        Graphics.Blit(_webCamTexture, _webCamRenderTexture);
                                        RenderTexture.active = _webCamRenderTexture;
                                        snapshot = new Texture2D(_webCamRenderTexture.width, _webCamRenderTexture.height, TextureFormat.RGB24, false);
                                        snapshot.ReadPixels(new Rect(0, 0, _webCamRenderTexture.width, _webCamRenderTexture.height), 0, 0);
                                        snapshot.Apply();
                                        RenderTexture.active = null;
                                    }
                                }
                                else
                                {
                                    // Fallback to RenderTexture
                                    Graphics.Blit(_webCamTexture, _webCamRenderTexture);
                                    RenderTexture.active = _webCamRenderTexture;
                                    snapshot = new Texture2D(_webCamRenderTexture.width, _webCamRenderTexture.height, TextureFormat.RGB24, false);
                                    snapshot.ReadPixels(new Rect(0, 0, _webCamRenderTexture.width, _webCamRenderTexture.height), 0, 0);
                                    snapshot.Apply();
                                    RenderTexture.active = null;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"[AbxrLib] Error getting pixels directly from WebCamTexture: {ex.Message}. Trying RenderTexture approach...");
                                // Fallback to RenderTexture approach
                                Graphics.Blit(_webCamTexture, _webCamRenderTexture);
                                RenderTexture.active = _webCamRenderTexture;
                                snapshot = new Texture2D(_webCamRenderTexture.width, _webCamRenderTexture.height, TextureFormat.RGB24, false);
                                snapshot.ReadPixels(new Rect(0, 0, _webCamRenderTexture.width, _webCamRenderTexture.height), 0, 0);
                                snapshot.Apply();
                                RenderTexture.active = null;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[AbxrLib] WebCamTexture not ready (scan #{scanCount}): width={_webCamTexture.width}, height={_webCamTexture.height}, isPlaying={_webCamTexture.isPlaying}");
                            frameError = true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[AbxrLib] No camera source available (scan #{scanCount})");
                        frameError = true;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AbxrLib] Error capturing camera frame: {ex.Message}\n{ex.StackTrace}");
                    frameError = true;
                }
                
                if (frameError || snapshot == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }
                
                try
                {
                    // Decode QR code using ZXing
                    string result = DecodeQRCode(snapshot);
                    
                    scanCount++;
                    
                    // Log when QR codes are detected (even if they don't have ABXR: prefix)
                    if (!string.IsNullOrEmpty(result))
                    {
                        // Only process QR codes that start with "ABXR:"
                        if (result.StartsWith("ABXR:", System.StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[AbxrLib] QR code detected: '{result}' (scan #{scanCount})");
                            // Process the QR code result
                            OnQRCodeScanned(result);
                            if (snapshot != null) Destroy(snapshot);
                            yield break; // Stop scanning after successful read
                        }
                        // QR code found but doesn't have ABXR: prefix - ignore silently
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AbxrLib] Error in QR scanning loop: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    if (snapshot != null)
                    {
                        Destroy(snapshot);
                    }
                }
                
                // Scan every few frames to avoid performance issues
                yield return new WaitForSeconds(0.1f);
            }
            
            Debug.Log($"[AbxrLib] QR code scanning stopped. Total scans: {scanCount}");
        }
        
        /// <summary>
        /// Test if a camera source is producing valid (non-black) frames
        /// This is a separate test method that can be called independently of display
        /// </summary>
        /// <param name="texture">The texture to test (WebCamTexture or Texture2D)</param>
        /// <param name="sampleSize">Number of pixels to sample (default 1000)</param>
        /// <param name="threshold">Brightness threshold to consider non-black (0-1, default 0.01)</param>
        /// <param name="verbose">Whether to log detailed test results (default false - only logs major milestones)</param>
        /// <returns>True if texture has non-black pixels, false otherwise</returns>
        private static bool TestCameraValidity(Texture texture, int sampleSize = 1000, float threshold = 0.01f, bool verbose = false)
        {
            if (texture == null)
            {
                Debug.LogWarning("[AbxrLib] TestCameraValidity - texture is null");
                return false;
            }
            
            try
            {
                Color32[] pixels = null;
                int width = 0;
                int height = 0;
                
                // Handle different texture types
                if (texture is WebCamTexture webCam)
                {
                    if (webCam.width <= 0 || webCam.height <= 0 || !webCam.isPlaying)
                    {
                        Debug.LogWarning($"[AbxrLib] TestCameraValidity - WebCamTexture not ready: {webCam.width}x{webCam.height}, isPlaying: {webCam.isPlaying}");
                        return false;
                    }
                    pixels = webCam.GetPixels32();
                    width = webCam.width;
                    height = webCam.height;
                }
                else if (texture is Texture2D tex2d)
                {
                    if (tex2d.width <= 0 || tex2d.height <= 0)
                    {
                        Debug.LogWarning($"[AbxrLib] TestCameraValidity - Texture2D invalid size: {tex2d.width}x{tex2d.height}");
                        return false;
                    }
                    pixels = tex2d.GetPixels32();
                    width = tex2d.width;
                    height = tex2d.height;
                }
                else
                {
                    Debug.LogWarning($"[AbxrLib] TestCameraValidity - Unsupported texture type: {texture.GetType().Name}");
                    return false;
                }
                
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogWarning($"[AbxrLib] TestCameraValidity - No pixel data in texture ({width}x{height})");
                    return false;
                }
                
                // Sample pixels to check for non-black content
                int actualSampleSize = Mathf.Min(sampleSize, pixels.Length);
                int nonBlackCount = 0;
                float totalBrightness = 0f;
                Color32 firstPixel = pixels[0];
                bool allSame = true;
                
                for (int i = 0; i < actualSampleSize; i++)
                {
                    int index = (i * pixels.Length) / actualSampleSize;
                    Color32 pixel = pixels[index];
                    
                    // Calculate brightness
                    float brightness = (pixel.r + pixel.g + pixel.b) / 3f / 255f;
                    totalBrightness += brightness;
                    
                    // Check if non-black (above threshold)
                    if (brightness > threshold)
                    {
                        nonBlackCount++;
                    }
                    
                    // Check if all pixels are the same
                    if (allSame && !pixel.Equals(firstPixel))
                    {
                        allSame = false;
                    }
                }
                
                float avgBrightness = totalBrightness / actualSampleSize;
                float nonBlackPercentage = (float)nonBlackCount / actualSampleSize * 100f;
                
                // Consider valid if:
                // 1. At least 1% of pixels are non-black, OR
                // 2. Average brightness is above threshold, OR
                // 3. Pixels are not all the same (indicates variation)
                bool isValid = nonBlackPercentage > 1f || avgBrightness > threshold || !allSame;
                
                // Only log when verbose is true (for major milestones)
                if (verbose)
                {
                    if (isValid)
                    {
                        Debug.Log($"[AbxrLib] Camera Validity Test PASSED - Camera is producing valid frames ({width}x{height}, brightness: {avgBrightness:F3})");
                    }
                    else
                    {
                        Debug.LogWarning($"[AbxrLib] Camera Validity Test FAILED - Camera appears to be producing black/empty frames ({width}x{height}, brightness: {avgBrightness:F3})");
                    }
                }
                
                return isValid;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AbxrLib] TestCameraValidity error: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Decode QR code from texture using ZXing
        /// </summary>
        private string DecodeQRCode(Texture2D texture)
        {
            if (_barcodeReader == null)
            {
                Debug.LogWarning("[AbxrLib] barcodeReader is null, cannot decode QR code");
                return null;
            }
            
            if (texture == null)
            {
                Debug.LogWarning("[AbxrLib] texture is null, cannot decode QR code");
                return null;
            }
            
            try
            {
                // Get pixel data from texture
                Color32[] pixels = texture.GetPixels32();
                
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogWarning($"[AbxrLib] No pixel data in texture ({texture.width}x{texture.height})");
                    return null;
                }
                
                // Decode QR code using ZXing
                Result result = _barcodeReader.Decode(pixels, texture.width, texture.height);
                
                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    return result.Text;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AbxrLib] QR code decoding error: {ex.Message}\n{ex.StackTrace}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Callback when QR code is scanned
        /// </summary>
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
                _isScanning = false;
                if (_scanningCoroutine != null)
                {
                    StopCoroutine(_scanningCoroutine);
                    _scanningCoroutine = null;
                }
                DestroyOverlayUI();
                cb?.Invoke(pin);
                return;
            }
            StopScanning();
            if (string.IsNullOrEmpty(scanResult)) return;
            Match m = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            if (m.Success)
            {
                string pin = m.Value;
                Debug.Log($"[AbxrLib] Extracted PIN from QR code: {pin}");
                AuthService.SetInputSource("QRlms");
                AuthService.KeyboardAuthenticate(pin);
            }
            else
            {
                Debug.LogWarning($"[AbxrLib] Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                AuthService.SetInputSource("QRlms");
                AuthService.KeyboardAuthenticate(null);
            }
        }
        
        /// <summary>
        /// Create overlay UI for passthrough mode with camera feed
        /// </summary>
        private void CreateOverlayUI()
        {
            if (_overlayCanvas != null)
            {
                return; // Already created
            }
            
            // Ensure EventSystem exists for UI button interaction
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // Create canvas root
            _overlayCanvas = new GameObject("QRScanOverlay");
            _overlayCanvas.transform.SetParent(transform);
            
            // Add Canvas component (World Space)
            Canvas canvas = _overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            
            // Add CanvasScaler for proper sizing
            CanvasScaler scaler = _overlayCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster for button interaction
            _overlayCanvas.AddComponent<GraphicRaycaster>();
            
            // Set canvas size and position (smaller, more compact overlay)
            RectTransform canvasRect = _overlayCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(0.6f, 0.4f); // 60cm x 40cm in world space
            canvasRect.localScale = Vector3.one;
            
            // Add FaceCamera component to position it in front of user
            FaceCamera faceCamera = _overlayCanvas.AddComponent<FaceCamera>();
            faceCamera.faceCamera = true;
            faceCamera.distanceFromCamera = 0.9f; // Closer to camera to appear in front of PIN pad buttons
            faceCamera.verticalOffset = 0f;
            faceCamera.useConfigurationValues = false;
            
            // Create background panel
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(_overlayCanvas.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f); // More opaque black background
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;
            
            // Create camera feed display (background, fills most of panel)
            GameObject cameraDisplay = new GameObject("CameraDisplay");
            cameraDisplay.transform.SetParent(panel.transform, false);
            RawImage cameraImage = cameraDisplay.AddComponent<RawImage>();
            cameraImage.raycastTarget = false; // Don't block interactions
            
            // Set texture if available
            if (_webCamTexture != null)
            {
                cameraImage.texture = _webCamTexture;
                cameraImage.uvRect = new Rect(0, 0, 1, 1);
            }
            else
            {
                // Set a placeholder color so we can see the area
                cameraImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                Debug.LogWarning("[AbxrLib] No camera texture available when creating overlay. Will update when camera is ready.");
            }
            
            RectTransform cameraRect = cameraDisplay.GetComponent<RectTransform>();
            cameraRect.anchorMin = new Vector2(0.05f, 0.1f);
            cameraRect.anchorMax = new Vector2(0.95f, 0.95f);
            cameraRect.sizeDelta = Vector2.zero;
            cameraRect.anchoredPosition = Vector2.zero;
            
            // Set sorting order to ensure it's visible in front of PIN pad buttons
            canvas.sortingOrder = 200; // Very high sorting order to appear in front of PIN pad (which uses default 0)
            
            // Create text label (top portion, over camera feed)
            GameObject label = new GameObject("Label");
            label.transform.SetParent(panel.transform, false);
            Text labelText = label.AddComponent<Text>();
            
            // Show message
            labelText.text = "Point camera at QR code\nScanning for ABXR: codes...";
            
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            
            // Add outline/shadow effect for better visibility over camera feed
            Outline labelOutline = label.AddComponent<Outline>();
            labelOutline.effectColor = Color.black;
            labelOutline.effectDistance = new Vector2(2, 2);
            
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.1f, 0.05f);
            labelRect.anchorMax = new Vector2(0.9f, 0.35f); // Slightly taller to fit more text
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
        }
        
        /// <summary>
        /// Destroy overlay UI
        /// </summary>
        private void DestroyOverlayUI()
        {
            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas);
                _overlayCanvas = null;
            }
        }
    }
}
#endif
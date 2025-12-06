/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - Meta Quest QR Code Reader
 *
 * This component handles QR code reading on Meta Quest headsets using the forward-facing camera.
 * It only activates when:
 * - Running on a supported Meta Quest headset (Quest 3, Quest 3S, Quest Pro)
 * - Quest 2 is excluded due to insufficient camera quality
 * - Authentication mechanism type is "assessmentPin"
 *
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */
#if META_QR_AVAILABLE
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using AbxrLib.Runtime.UI;

namespace AbxrLib.Runtime.Authentication
{
    /// <summary>
    /// QR code reader for Meta Quest headsets using forward-facing camera.
    /// Only activates on supported Meta Quest headsets when assessmentPin authentication is required.
    /// </summary>
    public class MetaQRCodeReader : MonoBehaviour
    {
        public static MetaQRCodeReader Instance;
        
        // List of supported Meta Quest devices (excludes Quest 2 due to camera quality)
        private static readonly string[] SupportedDevices = 
        {
            "Oculus Quest 3",
            "Oculus Quest 3S",
            "Oculus Quest Pro",
            "Oculus Quest 1" // Can be tested and removed if needed
        };
        
        // List of excluded devices
        private static readonly string[] ExcludedDevices = 
        {
            "Oculus Quest 2"
        };
        
        private WebCamTexture webCamTexture;
        private bool isScanning = false;
        private bool cameraInitialized = false;
        private Coroutine scanningCoroutine;
        
        // ZXing barcode reader instance
        private BarcodeReader barcodeReader;
        
        // Overlay UI for passthrough mode
        private GameObject overlayCanvas;
        private Button cancelButton;
        
        private void Awake()
        {
            // Check if device is supported
            if (!IsDeviceSupported())
            {
                Debug.LogWarning($"AbxrLib: Disabling QR Code Scanner. Device '{DeviceModel.deviceModel}' is not supported for QR code reading.");
                return;
            }
            
            // Initialize ZXing barcode reader
            try
            {
                barcodeReader = new BarcodeReader();
                // Configure to only read QR codes
                barcodeReader.Options.PossibleFormats = new System.Collections.Generic.List<BarcodeFormat> { BarcodeFormat.QR_CODE };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Failed to initialize ZXing: {ex.Message}");
                return;
            }
            
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Initialize camera access (will be done when ScanQRCode is called)
        }
        
        private void OnDestroy()
        {
            StopScanning();
            DestroyOverlayUI();
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                {
                    webCamTexture.Stop();
                }
                Destroy(webCamTexture);
            }
        }
        
        /// <summary>
        /// Check if the current device is supported for QR code reading
        /// </summary>
        private bool IsDeviceSupported()
        {
            string deviceModel = DeviceModel.deviceModel;
            
            if (string.IsNullOrEmpty(deviceModel))
            {
                return false;
            }
            
            // Check excluded devices first
            foreach (string excluded in ExcludedDevices)
            {
                if (deviceModel.Contains(excluded))
                {
                    return false;
                }
            }
            
            // Check supported devices
            foreach (string supported in SupportedDevices)
            {
                if (deviceModel.Contains(supported))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Start scanning for QR codes
        /// </summary>
        public void ScanQRCode()
        {
            if (isScanning)
            {
                Debug.Log("AbxrLib: QR code scanning already in progress");
                return;
            }
            
            if (!cameraInitialized)
            {
                StartCoroutine(InitializeCamera());
            }
            else
            {
                StartScanning();
            }
        }
        
        /// <summary>
        /// Cancel/stop QR code scanning
        /// </summary>
        public void CancelScanning()
        {
            if (!isScanning)
            {
                return;
            }
            
            StopScanning();
            Debug.Log("AbxrLib: QR code scanning cancelled by user");
        }
        
        /// <summary>
        /// Check if currently scanning for QR codes
        /// </summary>
        public bool IsScanning()
        {
            return isScanning;
        }
        
        /// <summary>
        /// Initialize the camera
        /// </summary>
        private IEnumerator InitializeCamera()
        {
            // Request camera permission on Android
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                yield return new WaitForSeconds(0.5f);
                
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Debug.LogError("AbxrLib: Camera permission denied. Cannot scan QR codes.");
                    yield break;
                }
            }
#endif
            
            // Find the forward-facing camera
            WebCamDevice[] devices = WebCamTexture.devices;
            WebCamDevice? frontCamera = null;
            
            foreach (WebCamDevice device in devices)
            {
                // Look for forward-facing camera (typically named with "front" or specific Quest camera names)
                string deviceName = device.name.ToLower();
                if (deviceName.Contains("front") || deviceName.Contains("passthrough") || deviceName.Contains("quest"))
                {
                    frontCamera = device;
                    break;
                }
            }
            
            // If no specific front camera found, use the first available camera
            if (!frontCamera.HasValue && devices.Length > 0)
            {
                frontCamera = devices[0];
            }
            
            if (!frontCamera.HasValue)
            {
                Debug.LogError("AbxrLib: No camera found. Cannot scan QR codes.");
                yield break;
            }
            
            // Create WebCamTexture
            webCamTexture = new WebCamTexture(frontCamera.Value.name, 640, 480);
            webCamTexture.Play();
            
            // Wait for camera to start
            yield return new WaitForSeconds(0.5f);
            
            if (webCamTexture.isPlaying && webCamTexture.width > 0)
            {
                cameraInitialized = true;
                Debug.Log($"AbxrLib: Camera initialized: {frontCamera.Value.name}");
                StartScanning();
            }
            else
            {
                Debug.LogError("AbxrLib: Failed to initialize camera.");
                if (webCamTexture != null)
                {
                    webCamTexture.Stop();
                    Destroy(webCamTexture);
                    webCamTexture = null;
                }
            }
        }
        
        /// <summary>
        /// Start the scanning coroutine
        /// </summary>
        private void StartScanning()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying)
            {
                Debug.LogError("AbxrLib: Camera not ready for scanning.");
                return;
            }
            
            isScanning = true;
            CreateOverlayUI();
            scanningCoroutine = StartCoroutine(ScanForQRCode());
        }
        
        /// <summary>
        /// Stop scanning
        /// </summary>
        private void StopScanning()
        {
            isScanning = false;
            if (scanningCoroutine != null)
            {
                StopCoroutine(scanningCoroutine);
                scanningCoroutine = null;
            }
            DestroyOverlayUI();
        }
        
        /// <summary>
        /// Coroutine to continuously scan for QR codes
        /// </summary>
        private IEnumerator ScanForQRCode()
        {
            while (isScanning && webCamTexture != null && webCamTexture.isPlaying)
            {
                if (webCamTexture.width > 0 && webCamTexture.height > 0)
                {
                    // Get camera texture data
                    Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                    snapshot.SetPixels(webCamTexture.GetPixels());
                    snapshot.Apply();
                    
                    // Decode QR code using ZXing
                    string result = DecodeQRCode(snapshot);
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        // Only process QR codes that start with "ABXR:"
                        if (result.StartsWith("ABXR:", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // Process the QR code result
                            OnQRCodeScanned(result);
                            Destroy(snapshot);
                            yield break; // Stop scanning after successful read
                        }
                        else
                        {
                            // QR code found but doesn't have ABXR: prefix - ignore it and continue scanning
                            Debug.Log($"AbxrLib: Ignoring QR code without ABXR: prefix: {result}");
                        }
                    }
                    
                    Destroy(snapshot);
                }
                
                // Scan every few frames to avoid performance issues
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        /// <summary>
        /// Decode QR code from texture using ZXing
        /// </summary>
        private string DecodeQRCode(Texture2D texture)
        {
            if (barcodeReader == null)
            {
                return null;
            }
            
            try
            {
                // Get pixel data from texture
                Color32[] pixels = texture.GetPixels32();
                
                // Decode QR code using ZXing
                Result result = barcodeReader.Decode(pixels, texture.width, texture.height);
                
                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    return result.Text;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: QR code decoding error: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Callback when QR code is scanned
        /// </summary>
        private void OnQRCodeScanned(string scanResult)
        {
            StopScanning();
            
            if (string.IsNullOrEmpty(scanResult))
            {
                return;
            }
            
            // Extract PIN from QR code format "ABXR:123456"
            Match match = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            if (match.Success)
            {
                string pin = match.Value;
                Debug.Log($"AbxrLib: Extracted PIN from QR code: {pin}");
                // Set inputSource to "QRlms" for QR code authentication
                Authentication.SetInputSource("QRlms");
                StartCoroutine(Authentication.KeyboardAuthenticate(pin, false));
            }
            else
            {
                Debug.LogWarning($"AbxrLib: Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                // Set inputSource to "QRlms" even for invalid QR codes
                Authentication.SetInputSource("QRlms");
                StartCoroutine(Authentication.KeyboardAuthenticate(null, true));
            }
        }
        
        /// <summary>
        /// Create overlay UI for passthrough mode with cancel button
        /// </summary>
        private void CreateOverlayUI()
        {
            if (overlayCanvas != null)
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
            overlayCanvas = new GameObject("MetaQRScanOverlay");
            overlayCanvas.transform.SetParent(transform);
            
            // Add Canvas component (World Space)
            Canvas canvas = overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            
            // Add CanvasScaler for proper sizing
            CanvasScaler scaler = overlayCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster for button interaction
            overlayCanvas.AddComponent<GraphicRaycaster>();
            
            // Set canvas size and position (1 meter in front, scaled appropriately)
            RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(0.5f, 0.3f); // 50cm x 30cm in world space
            canvasRect.localScale = Vector3.one;
            
            // Add FaceCamera component to position it in front of user
            FaceCamera faceCamera = overlayCanvas.AddComponent<FaceCamera>();
            faceCamera.faceCamera = true;
            faceCamera.distanceFromCamera = 1.0f;
            faceCamera.verticalOffset = 0.2f; // Slightly above center
            faceCamera.useConfigurationValues = false;
            
            // Create background panel
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(overlayCanvas.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;
            
            // Create text label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(panel.transform, false);
            Text labelText = label.AddComponent<Text>();
            labelText.text = "Scanning QR Code...\nPoint camera at QR code";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 24;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.5f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
            
            // Create cancel button
            GameObject buttonObj = new GameObject("CancelButton");
            buttonObj.transform.SetParent(panel.transform, false);
            
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red button
            
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.25f, 0);
            buttonRect.anchorMax = new Vector2(0.75f, 0.4f);
            buttonRect.sizeDelta = Vector2.zero;
            buttonRect.anchoredPosition = Vector2.zero;
            
            // Create button text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.text = "Cancel Scanning";
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 20;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            buttonTextRect.anchoredPosition = Vector2.zero;
            
            // Add click listener
            button.onClick.AddListener(() => CancelScanning());
            
            cancelButton = button;
            
            Debug.Log("AbxrLib: Created QR scanning overlay UI for passthrough mode");
        }
        
        /// <summary>
        /// Destroy overlay UI
        /// </summary>
        private void DestroyOverlayUI()
        {
            if (overlayCanvas != null)
            {
                Destroy(overlayCanvas);
                overlayCanvas = null;
                cancelButton = null;
            }
        }
    }
}
#endif


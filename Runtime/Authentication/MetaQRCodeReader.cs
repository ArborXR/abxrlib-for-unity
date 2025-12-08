/*
 * Copyright (c) 2025 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - Meta Quest QR Code Reader
 *
 * This component handles QR code reading on Meta Quest headsets using Meta's Passthrough Camera API.
 * 
 * Requirements:
 * - Quest 3 (or 3S) running Horizon OS v74+ (when Passthrough Camera API was released)
 * - Unity 6+ with Meta XR Core SDK v74+
 * - horizonos.permission.HEADSET_CAMERA permission in AndroidManifest
 * 
 * Implementation:
 * - Uses WebCamTexture as primary method (Meta's Passthrough Camera API routes through WebCamTexture on Quest)
 * - Falls back to reflection-based OVR/OpenXR Passthrough Camera API if WebCamTexture fails
 * 
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
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Unity.Collections;
using ZXing;
using AbxrLib.Runtime.UI;
using AbxrLib.Runtime.UI.Keyboard;

namespace AbxrLib.Runtime.Authentication
{
    /// <summary>
    /// QR code reader for Meta Quest headsets using forward-facing camera via Meta's Passthrough Camera API.
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
        
        // Meta Passthrough Camera API components
        // Primary: WebCamTexture (Meta's Passthrough Camera API routes through WebCamTexture on Quest)
        private WebCamTexture webCamTexture;
        private RenderTexture webCamRenderTexture; // RenderTexture for WebCamTexture processing
        
        // Fallback: Reflection-based OVR/OpenXR Passthrough Camera API
        private Component ovrPassthroughLayer; // OVRPassthroughLayer accessed via reflection
        private object cameraSubsystem; // Camera subsystem (if using subsystem-based API)
        private RenderTexture passthroughRenderTexture; // RenderTexture to capture passthrough camera feed
        private Texture2D passthroughTexture; // Texture2D snapshot from passthrough
        
        private bool isScanning = false;
        private bool cameraInitialized = false;
        private Coroutine scanningCoroutine;
        private bool usingWebCamTexture = false; // True when using WebCamTexture (primary method)
        
        // Reflection cache for OVR/OpenXR types
        private static System.Type ovrPassthroughLayerType;
        private static System.Type openXRPassthroughCameraType;
        private static PropertyInfo passthroughTextureProperty;
        private static MethodInfo requestPassthroughMethod;
        private static MethodInfo getCameraFrameMethod;
        private static bool ovrTypesInitialized = false;
        private static bool usingOpenXR = false;
        
        // ZXing barcode reader instance
        private BarcodeReader barcodeReader;
        
        // Overlay UI for passthrough mode
        private GameObject overlayCanvas;
        
        // Decode attempt counter for logging
        private int decodeAttemptCount = 0;
        
        private void Awake()
        {
            Debug.Log($"AbxrLib: MetaQRCodeReader.Awake() called. Device model: '{DeviceModel.deviceModel}'");
            
            // Check if device is supported
            if (!IsDeviceSupported())
            {
                Debug.LogWarning($"AbxrLib: Disabling QR Code Scanner. Device '{DeviceModel.deviceModel}' is not supported for QR code reading.");
                return;
            }
            
            Debug.Log($"AbxrLib: Device '{DeviceModel.deviceModel}' is supported for QR code reading.");
            
            // Check OpenXR features are enabled
            if (!CheckOpenXRFeatures())
            {
                Debug.LogError("AbxrLib: Required OpenXR features are not enabled. QR code scanning will not work.");
                Debug.LogError("AbxrLib: Please enable the following in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups:");
                Debug.LogError("AbxrLib:   - Meta Quest Support");
                Debug.LogError("AbxrLib:   - Meta Quest: Camera (Passthrough)");
                Debug.LogError("AbxrLib:   - Meta Quest: Session");
                return;
            }
            
            // Initialize OVR types via reflection
            InitializeOVRTypes();
            
            // Initialize ZXing barcode reader
            try
            {
                barcodeReader = new BarcodeReader();
                // Configure to only read QR codes
                barcodeReader.Options.PossibleFormats = new System.Collections.Generic.List<BarcodeFormat> { BarcodeFormat.QR_CODE };
                Debug.Log("AbxrLib: ZXing barcode reader initialized successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Failed to initialize ZXing: {ex.Message}");
                return;
            }
            
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("AbxrLib: MetaQRCodeReader.Instance set successfully.");
            }
            else
            {
                Debug.LogWarning("AbxrLib: MetaQRCodeReader.Instance already exists. Destroying duplicate.");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Always run delayed check if Instance wasn't set in Awake
            if (Instance == null)
            {
                Debug.Log("AbxrLib: MetaQRCodeReader Instance not set in Awake. Starting delayed device check...");
                StartCoroutine(DelayedDeviceCheck());
            }
        }
        
        private void OnDestroy()
        {
            if (passthroughRenderTexture != null)
            {
                passthroughRenderTexture.Release();
                Destroy(passthroughRenderTexture);
                passthroughRenderTexture = null;
            }
            
            if (webCamRenderTexture != null)
            {
                webCamRenderTexture.Release();
                Destroy(webCamRenderTexture);
                webCamRenderTexture = null;
            }
            
            if (passthroughTexture != null)
            {
                Destroy(passthroughTexture);
                passthroughTexture = null;
            }
            
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                {
                    webCamTexture.Stop();
                }
                Destroy(webCamTexture);
                webCamTexture = null;
            }
            
            
            if (ovrPassthroughLayer != null)
            {
                Destroy(ovrPassthroughLayer);
                ovrPassthroughLayer = null;
            }
            
            cameraSubsystem = null; // Subsystems are managed by Unity, no need to destroy
        }
        
        /// <summary>
        /// Initialize OVR types via reflection
        /// </summary>
        private static void InitializeOVRTypes()
        {
            if (ovrTypesInitialized)
            {
                return;
            }
            
            try
            {
                // Search through all loaded assemblies for OVR types
                System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                Debug.Log($"AbxrLib: Searching {assemblies.Length} loaded assemblies for OVRPassthroughLayer...");
                
                // Try different type names for OVR SDK
                string[] ovrTypeNames = {
                    "OVRPassthroughLayer",
                    "PassthroughLayer",
                    "OVR.PassthroughLayer"
                };
                
                // Try OpenXR Meta types - search more thoroughly
                // Note: Unity OpenXR Meta package uses UnityEngine.XR.OpenXR.Features.Meta namespace
                string[] openXRTypeNames = {
                    "UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRPassthroughLayer",  // Found in logs!
                    "UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature",  // Found in logs!
                    "UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRCameraSubsystem",  // Found in logs!
                    "Unity.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",
                    "Unity.XR.Meta.OpenXR.PassthroughCamera",
                    "Unity.XR.MetaOpenXR.PassthroughCamera",
                    "Unity.XR.MetaOpenXR.Camera",
                    "Unity.XR.MetaOpenXR.Features.PassthroughCamera",
                    "Unity.XR.MetaOpenXR.Features.Camera",
                    "Meta.XR.SDK.Passthrough.PassthroughLayer",
                    "Unity.XR.Meta.OpenXR.Passthrough",
                    "Unity.XR.OpenXR.Features.MetaQuestSupport.PassthroughCamera",
                    "Unity.XR.OpenXR.Features.MetaQuestSupport.Passthrough",
                    "Unity.XR.OpenXR.Features.MetaQuestSupport.Camera",
                    "PassthroughCamera",
                    "MetaPassthroughCamera",
                    "MetaQuestPassthroughCamera",
                    "MetaQuestCamera"
                };
                
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    string assemblyName = assembly.GetName().Name;
                    
                    // Skip system assemblies for performance, but include Unity.XR assemblies
                    if (assemblyName.StartsWith("System.") || 
                        assemblyName.StartsWith("UnityEngine.") ||
                        (assemblyName.StartsWith("Unity.") && !assemblyName.Contains("XR")) ||
                        assemblyName.StartsWith("mscorlib") ||
                        assemblyName.StartsWith("netstandard"))
                    {
                        continue;
                    }
                    
                    // Log assemblies that might contain passthrough camera types
                    if (assemblyName.Contains("XR") || assemblyName.Contains("Meta") || assemblyName.Contains("Oculus") || assemblyName.Contains("Passthrough"))
                    {
                        Debug.Log($"AbxrLib: Searching assembly: {assemblyName}");
                        
                        // If this is the MetaOpenXR assembly, log all types for debugging
                        if (assemblyName == "Unity.XR.MetaOpenXR")
                        {
                            try
                            {
                                System.Type[] allTypes = assembly.GetTypes();
                                Debug.Log($"AbxrLib: Unity.XR.MetaOpenXR assembly contains {allTypes.Length} types:");
                                foreach (System.Type type in allTypes)
                                {
                                    if (type.Name.Contains("Camera") || type.Name.Contains("Passthrough") || type.Name.Contains("Meta"))
                                    {
                                        Debug.Log($"AbxrLib:   - {type.FullName}");
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"AbxrLib: Could not enumerate types in Unity.XR.MetaOpenXR: {ex.Message}");
                            }
                        }
                    }
                    
                    // Try OVR types first
                    if (ovrPassthroughLayerType == null)
                    {
                        foreach (string typeName in ovrTypeNames)
                        {
                            try
                            {
                                ovrPassthroughLayerType = assembly.GetType(typeName);
                                if (ovrPassthroughLayerType != null)
                                {
                                    Debug.Log($"AbxrLib: Found OVR {typeName} in assembly: {assemblyName}");
                                    usingOpenXR = false;
                                    break;
                                }
                            }
                            catch
                            {
                                // Continue searching
                            }
                        }
                    }
                    
                    // Try OpenXR types - prioritize CameraSubsystem
                    if (openXRPassthroughCameraType == null || !openXRPassthroughCameraType.Name.Contains("CameraSubsystem"))
                    {
                        // First, try to find CameraSubsystem specifically
                        System.Type cameraSubsystemType = assembly.GetType("UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRCameraSubsystem");
                        if (cameraSubsystemType != null)
                        {
                            Debug.Log($"AbxrLib: Found OpenXR MetaOpenXRCameraSubsystem in assembly: {assemblyName}");
                            openXRPassthroughCameraType = cameraSubsystemType;
                            usingOpenXR = true;
                        }
                        else
                        {
                            // Fallback to other OpenXR types
                            foreach (string typeName in openXRTypeNames)
                            {
                                try
                                {
                                    System.Type foundType = assembly.GetType(typeName);
                                    if (foundType != null)
                                    {
                                        // Only use if it's a CameraSubsystem, or if we haven't found anything yet
                                        if (foundType.Name.Contains("CameraSubsystem"))
                                        {
                                            Debug.Log($"AbxrLib: Found OpenXR {typeName} (CameraSubsystem) in assembly: {assemblyName}");
                                            openXRPassthroughCameraType = foundType;
                                            usingOpenXR = true;
                                            break;
                                        }
                                        else if (openXRPassthroughCameraType == null)
                                        {
                                            Debug.Log($"AbxrLib: Found OpenXR {typeName} in assembly: {assemblyName} (will use if no CameraSubsystem found)");
                                            openXRPassthroughCameraType = foundType;
                                            usingOpenXR = true;
                                        }
                                    }
                                }
                                catch
                                {
                                    // Continue searching
                                }
                            }
                        }
                        
                        // If not found by exact name, try searching all types in the assembly
                        if (openXRPassthroughCameraType == null && (assemblyName.Contains("Meta") || assemblyName.Contains("OpenXR")))
                        {
                            try
                            {
                                System.Type[] types = assembly.GetTypes();
                                foreach (System.Type type in types)
                                {
                                    string typeName = type.Name;
                                    string fullName = type.FullName ?? "";
                                    
                                    // Look for camera-related types in Meta namespace
                                    // Priority: CameraSubsystem > ARCameraFeature > PassthroughLayer
                                    if (fullName.Contains("UnityEngine.XR.OpenXR.Features.Meta"))
                                    {
                                        if (typeName.Contains("CameraSubsystem") && !typeName.Contains("Provider"))
                                        {
                                            // Prioritize CameraSubsystem - use it immediately
                                            Debug.Log($"AbxrLib: Found camera subsystem type: {type.FullName} in assembly: {assemblyName}");
                                            openXRPassthroughCameraType = type;
                                            usingOpenXR = true;
                                            break; // Use subsystem immediately, don't look for PassthroughLayer
                                        }
                                        else if (typeName.Contains("CameraSubsystem") && !typeName.Contains("Provider"))
                                        {
                                            // Found CameraSubsystem - use it immediately
                                            Debug.Log($"AbxrLib: Found camera subsystem type: {type.FullName} in assembly: {assemblyName}");
                                            openXRPassthroughCameraType = type;
                                            usingOpenXR = true;
                                            break; // Use subsystem immediately
                                        }
                                        else if ((typeName.Contains("ARCameraFeature") && !typeName.Contains("Provider")) ||
                                                 (typeName.Contains("Passthrough") && typeName.Contains("Layer")))
                                        {
                                            // Only use these if we haven't found a subsystem yet
                                            if (openXRPassthroughCameraType == null || !openXRPassthroughCameraType.Name.Contains("CameraSubsystem"))
                                            {
                                                Debug.Log($"AbxrLib: Found potential passthrough camera type: {type.FullName} in assembly: {assemblyName} (will use if no subsystem found)");
                                                // Store as fallback, but continue searching for CameraSubsystem
                                                if (openXRPassthroughCameraType == null)
                                                {
                                                    openXRPassthroughCameraType = type;
                                                    usingOpenXR = true;
                                                }
                                            }
                                        }
                                    }
                                    // Fallback: look for types with both Passthrough and Camera
                                    else if ((typeName.Contains("Passthrough") && typeName.Contains("Camera")) ||
                                             (typeName.Contains("Camera") && typeName.Contains("Passthrough")))
                                    {
                                        Debug.Log($"AbxrLib: Found potential passthrough camera type: {type.FullName} in assembly: {assemblyName}");
                                        openXRPassthroughCameraType = type;
                                        usingOpenXR = true;
                                        break;
                                    }
                                }
                            }
                            catch (System.Reflection.ReflectionTypeLoadException ex)
                            {
                                // Some types might not be loadable, continue
                                Debug.LogWarning($"AbxrLib: Could not load all types from {assemblyName}: {ex.Message}");
                            }
                            catch
                            {
                                // Continue searching
                            }
                        }
                    }
                    
                    // If we found a CameraSubsystem, use it immediately (don't continue searching)
                    if (openXRPassthroughCameraType != null && openXRPassthroughCameraType.Name.Contains("CameraSubsystem"))
                    {
                        Debug.Log($"AbxrLib: CameraSubsystem found, stopping search. Using: {openXRPassthroughCameraType.FullName}");
                        break;
                    }
                    
                    if (ovrPassthroughLayerType != null || openXRPassthroughCameraType != null)
                    {
                        break;
                    }
                }
                
                // Fallback: Try direct type lookup with common assembly names
                if (ovrPassthroughLayerType == null && openXRPassthroughCameraType == null)
                {
                    string[] assemblyNames = { "Assembly-CSharp", "Oculus.VR", "Meta.XR.SDK.Core", "Meta.XR.SDK.Passthrough", 
                                               "Unity.XR.Meta.OpenXR", "Unity.XR.MetaOpenXR", "Unity.XR.OpenXR", 
                                               "Unity.XR.OpenXR.Features.MetaQuestSupport" };
                    foreach (string assemblyName in assemblyNames)
                    {
                        // Try OVR types
                        if (ovrPassthroughLayerType == null)
                        {
                            foreach (string typeName in ovrTypeNames)
                            {
                                try
                                {
                                    string fullTypeName = $"{typeName}, {assemblyName}";
                                    ovrPassthroughLayerType = System.Type.GetType(fullTypeName);
                                    if (ovrPassthroughLayerType != null)
                                    {
                                        Debug.Log($"AbxrLib: Found OVR {typeName} via direct lookup: {fullTypeName}");
                                        usingOpenXR = false;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // Continue searching
                                }
                            }
                        }
                        
                        // Try OpenXR types
                        if (openXRPassthroughCameraType == null)
                        {
                            foreach (string typeName in openXRTypeNames)
                            {
                                try
                                {
                                    string fullTypeName = $"{typeName}, {assemblyName}";
                                    openXRPassthroughCameraType = System.Type.GetType(fullTypeName);
                                    if (openXRPassthroughCameraType != null)
                                    {
                                        Debug.Log($"AbxrLib: Found OpenXR {typeName} via direct lookup: {fullTypeName}");
                                        usingOpenXR = true;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // Continue searching
                                }
                            }
                        }
                        
                        if (ovrPassthroughLayerType != null || openXRPassthroughCameraType != null)
                        {
                            break;
                        }
                    }
                }
                
                // Use whichever type we found
                System.Type passthroughType = ovrPassthroughLayerType ?? openXRPassthroughCameraType;
                
                if (passthroughType != null)
                {
                    Debug.Log($"AbxrLib: Found passthrough type: {passthroughType.FullName} (OpenXR: {usingOpenXR})");
                    
                    // Log ALL properties and methods for debugging
                    System.Reflection.PropertyInfo[] allProperties = passthroughType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    System.Reflection.MethodInfo[] allMethods = passthroughType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    
                    Debug.Log($"AbxrLib: {passthroughType.Name} has {allProperties.Length} properties and {allMethods.Length} methods:");
                    foreach (var prop in allProperties)
                    {
                        Debug.Log($"AbxrLib:   Property: {prop.Name} (Type: {prop.PropertyType.Name}, Static: {prop.GetGetMethod()?.IsStatic ?? false})");
                    }
                    Debug.Log($"AbxrLib: All methods on {passthroughType.Name}:");
                    foreach (var method in allMethods)
                    {
                        string paramList = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Debug.Log($"AbxrLib:   Method: {method.Name}({paramList}) (Static: {method.IsStatic}, Return: {method.ReturnType.Name})");
                    }
                    
                    // Get the texture property (try multiple possible names, including case variations)
                    string[] texturePropertyNames = { 
                        "texture", "Texture", "cameraTexture", "CameraTexture", "passthroughTexture", "PassthroughTexture",
                        "camera", "Camera", "frame", "Frame", "image", "Image", "output", "Output",
                        "texture2D", "Texture2D", "renderTexture", "RenderTexture"
                    };
                    foreach (string propName in texturePropertyNames)
                    {
                        passthroughTextureProperty = passthroughType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        if (passthroughTextureProperty != null)
                        {
                            // Check if it returns a Texture type
                            if (typeof(Texture).IsAssignableFrom(passthroughTextureProperty.PropertyType) ||
                                passthroughTextureProperty.PropertyType == typeof(Texture2D) ||
                                passthroughTextureProperty.PropertyType == typeof(RenderTexture))
                            {
                                Debug.Log($"AbxrLib: Found texture property: {propName} (Type: {passthroughTextureProperty.PropertyType.Name})");
                                break;
                            }
                            else
                            {
                                passthroughTextureProperty = null; // Not a texture type, continue searching
                            }
                        }
                    }
                    
                    // Get method to request passthrough (if needed)
                    requestPassthroughMethod = passthroughType.GetMethod("RequestPassthrough", BindingFlags.Public | BindingFlags.Instance);
                    if (requestPassthroughMethod == null)
                    {
                        requestPassthroughMethod = passthroughType.GetMethod("EnablePassthrough", BindingFlags.Public | BindingFlags.Instance);
                    }
                    
                    // For OpenXR, try GetCameraFrame method (both instance and static)
                    if (usingOpenXR)
                    {
                        getCameraFrameMethod = passthroughType.GetMethod("GetCameraFrame", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                        if (getCameraFrameMethod == null)
                        {
                            getCameraFrameMethod = passthroughType.GetMethod("GetCameraTexture", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                        }
                        if (getCameraFrameMethod == null)
                        {
                            getCameraFrameMethod = passthroughType.GetMethod("TryGetCameraFrame", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                        }
                    }
                    
                    Debug.Log($"AbxrLib: Passthrough types initialized. Texture property: {passthroughTextureProperty != null}, " +
                             $"Request method: {requestPassthroughMethod != null}, GetCameraFrame: {getCameraFrameMethod != null}");
                }
                else
                {
                    Debug.LogWarning("AbxrLib: Passthrough camera type not found in any loaded assembly. Meta Passthrough Camera API may not be available.");
                    Debug.LogWarning("AbxrLib: Available assemblies containing 'OVR' or 'Meta':");
                    foreach (System.Reflection.Assembly assembly in assemblies)
                    {
                        string assemblyName = assembly.GetName().Name;
                        if (assemblyName.Contains("OVR") || assemblyName.Contains("Meta") || assemblyName.Contains("Oculus") || assemblyName.Contains("OpenXR"))
                        {
                            Debug.LogWarning($"AbxrLib:   - {assemblyName}");
                            
                            // List ALL types in MetaQuestSupport assembly to see what's available
                            if (assemblyName.Contains("MetaQuestSupport") || assemblyName.Contains("Meta.OpenXR"))
                            {
                                try
                                {
                                    System.Type[] types = assembly.GetTypes();
                                    Debug.LogWarning($"AbxrLib: ALL types in {assemblyName} ({types.Length} total):");
                                    foreach (System.Type type in types)
                                    {
                                        Debug.LogWarning($"AbxrLib:     - {type.FullName}");
                                    }
                                }
                                catch (System.Reflection.ReflectionTypeLoadException ex)
                                {
                                    Debug.LogWarning($"AbxrLib: Could not load all types from {assemblyName}: {ex.Message}");
                                    if (ex.Types != null)
                                    {
                                        Debug.LogWarning($"AbxrLib: Loadable types ({ex.Types.Length}):");
                                        foreach (System.Type type in ex.Types)
                                        {
                                            if (type != null)
                                            {
                                                Debug.LogWarning($"AbxrLib:     - {type.FullName}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Error initializing OVR types: {ex.Message}\n{ex.StackTrace}");
            }
            
            ovrTypesInitialized = true;
        }
        
        /// <summary>
        /// Delayed device check coroutine
        /// </summary>
        private IEnumerator DelayedDeviceCheck()
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                yield return new WaitForSeconds(0.5f * attempt); // Increasing delays: 0.5s, 1s, 1.5s
                
                Debug.Log($"AbxrLib: Delayed device check attempt {attempt}/3. Device model: '{DeviceModel.deviceModel}'");
                
                if (!string.IsNullOrEmpty(DeviceModel.deviceModel))
                {
                    if (IsDeviceSupported())
                    {
                        Debug.Log($"AbxrLib: Device '{DeviceModel.deviceModel}' is now detected as supported. Initializing...");
                        
                        // Initialize OVR types
                        InitializeOVRTypes();
                        
                        // Initialize ZXing if not already done
                        if (barcodeReader == null)
                        {
                            try
                            {
                                barcodeReader = new BarcodeReader();
                                barcodeReader.Options.PossibleFormats = new System.Collections.Generic.List<BarcodeFormat> { BarcodeFormat.QR_CODE };
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"AbxrLib: Failed to initialize ZXing in delayed check: {ex.Message}");
                                yield break;
                            }
                        }
                        
                        if (Instance == null)
                        {
                            Instance = this;
                            Debug.Log("AbxrLib: MetaQRCodeReader.Instance set successfully after delayed check.");
                        }
                        yield break;
                    }
                }
            }
            
            Debug.LogWarning("AbxrLib: Device check failed after 3 attempts. QR code scanning will not be available.");
        }
        
        /// <summary>
        /// Check if the current device is supported for QR code reading
        /// </summary>
        private bool IsDeviceSupported()
        {
            string deviceModel = DeviceModel.deviceModel;
            
            Debug.Log($"AbxrLib: Checking device support for: '{deviceModel}'");
            
            if (string.IsNullOrEmpty(deviceModel))
            {
                Debug.Log("AbxrLib: Device model is empty, device not supported.");
                return false;
            }
            
            // Check excluded devices first
            foreach (string excludedDevice in ExcludedDevices)
            {
                if (deviceModel.Equals(excludedDevice, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"AbxrLib: Device '{deviceModel}' is explicitly excluded.");
                    return false;
                }
            }
            
            // Check supported devices
            foreach (string supportedDevice in SupportedDevices)
            {
                if (deviceModel.Equals(supportedDevice, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"AbxrLib: Device '{deviceModel}' is supported (matches '{supportedDevice}').");
                    return true;
                }
            }
            
            Debug.Log($"AbxrLib: Device '{deviceModel}' is not in the supported list.");
            return false;
        }
        
        /// <summary>
        /// Check if required OpenXR features are enabled in Project Settings
        /// </summary>
        private bool CheckOpenXRFeatures()
        {
            try
            {
                // Use reflection to access OpenXRSettings
                System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                System.Type openXRSettingsType = null;
                System.Type metaSessionFeatureType = null;
                System.Type metaCameraFeatureType = null;
                
                // Find OpenXRSettings type
                foreach (System.Reflection.Assembly assembly in assemblies)
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
                                Debug.Log($"AbxrLib: Found OpenXRSettings in assembly: {assemblyName}");
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
                            Debug.LogWarning($"AbxrLib: Error searching assembly {assemblyName}: {ex.Message}");
                        }
                    }
                }
                
                if (openXRSettingsType == null)
                {
                    Debug.LogWarning("AbxrLib: OpenXRSettings type not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                // Get Instance property
                System.Reflection.PropertyInfo instanceProperty = openXRSettingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProperty == null)
                {
                    Debug.LogWarning("AbxrLib: OpenXRSettings.Instance property not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                object openXRSettingsInstance = instanceProperty.GetValue(null);
                if (openXRSettingsInstance == null)
                {
                    Debug.LogWarning("AbxrLib: OpenXRSettings.Instance is null. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                // Get GetFeature method - handle overloads by getting all methods and finding the generic one
                System.Reflection.MethodInfo getFeatureMethod = null;
                System.Reflection.MethodInfo[] allMethods = openXRSettingsType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (System.Reflection.MethodInfo method in allMethods)
                {
                    if (method.Name == "GetFeature" && method.IsGenericMethod)
                    {
                        getFeatureMethod = method;
                        break;
                    }
                }
                
                if (getFeatureMethod == null)
                {
                    Debug.LogWarning("AbxrLib: OpenXRSettings.GetFeature generic method not found. Cannot verify OpenXR features. Assuming they are enabled.");
                    return true; // Don't block if we can't check
                }
                
                bool allFeaturesEnabled = true;
                string missingFeatures = "";
                
                // Check Meta Quest: Session feature
                if (metaSessionFeatureType != null)
                {
                    try
                    {
                        System.Reflection.MethodInfo getSessionFeature = getFeatureMethod.MakeGenericMethod(metaSessionFeatureType);
                        object sessionFeature = getSessionFeature.Invoke(openXRSettingsInstance, null);
                        if (sessionFeature != null)
                        {
                            System.Reflection.PropertyInfo enabledProperty = metaSessionFeatureType.GetProperty("enabled");
                            if (enabledProperty != null)
                            {
                                bool sessionEnabled = (bool)enabledProperty.GetValue(sessionFeature);
                                if (!sessionEnabled)
                                {
                                    Debug.LogError("AbxrLib: OpenXR feature 'Meta Quest: Session' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Session, ";
                                }
                                else
                                {
                                    Debug.Log("AbxrLib: OpenXR feature 'Meta Quest: Session' is enabled.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("AbxrLib: MetaSessionFeature not found. It may not be installed or configured.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Could not check Meta Quest: Session feature: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("AbxrLib: MetaSessionFeature type not found. Cannot verify if 'Meta Quest: Session' is enabled.");
                }
                
                // Check Meta Quest: Camera (Passthrough) feature
                if (metaCameraFeatureType != null)
                {
                    try
                    {
                        System.Reflection.MethodInfo getCameraFeature = getFeatureMethod.MakeGenericMethod(metaCameraFeatureType);
                        object cameraFeature = getCameraFeature.Invoke(openXRSettingsInstance, null);
                        if (cameraFeature != null)
                        {
                            System.Reflection.PropertyInfo enabledProperty = metaCameraFeatureType.GetProperty("enabled");
                            if (enabledProperty != null)
                            {
                                bool cameraEnabled = (bool)enabledProperty.GetValue(cameraFeature);
                                if (!cameraEnabled)
                                {
                                    Debug.LogError("AbxrLib: OpenXR feature 'Meta Quest: Camera (Passthrough)' is NOT enabled in Project Settings.");
                                    allFeaturesEnabled = false;
                                    missingFeatures += "Meta Quest: Camera (Passthrough), ";
                                }
                                else
                                {
                                    Debug.Log("AbxrLib: OpenXR feature 'Meta Quest: Camera (Passthrough)' is enabled.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("AbxrLib: MetaCameraFeature not found. It may not be installed or configured.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Could not check Meta Quest: Camera (Passthrough) feature: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("AbxrLib: MetaCameraFeature type not found. Cannot verify if 'Meta Quest: Camera (Passthrough)' is enabled.");
                }
                
                if (!allFeaturesEnabled)
                {
                    Debug.LogError($"AbxrLib: Missing required OpenXR features: {missingFeatures.TrimEnd(',', ' ')}");
                    Debug.LogError("AbxrLib: Please enable these features in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups");
                    return false;
                }
                
                Debug.Log("AbxrLib: All required OpenXR features are enabled.");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: Error checking OpenXR features: {ex.Message}. Assuming features are enabled.");
                return true; // Don't block if we can't check
            }
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
            
            // Check if camera needs to be initialized or reinitialized
            if (!cameraInitialized || (ovrPassthroughLayer == null && webCamTexture == null))
            {
                Debug.Log("AbxrLib: Camera not initialized. Initializing...");
                cameraInitialized = false;
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
        /// Initialize camera - try WebCamTexture first (Meta's recommended approach), then reflection, then native plugin
        /// </summary>
        private IEnumerator InitializeCamera()
        {
            // Verify OpenXR features are enabled before attempting camera initialization
            if (!CheckOpenXRFeatures())
            {
                Debug.LogError("AbxrLib: Cannot initialize camera - required OpenXR features are not enabled.");
                Debug.LogError("AbxrLib: Please enable the following in Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups:");
                Debug.LogError("AbxrLib:   - Meta Quest Support");
                Debug.LogError("AbxrLib:   - Meta Quest: Camera (Passthrough)");
                Debug.LogError("AbxrLib:   - Meta Quest: Session");
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
                    Debug.LogError("AbxrLib: Camera permission denied. Cannot scan QR codes.");
                    yield break;
                }
            }
#endif
            
            // PRIORITY 1: Try WebCamTexture first (Meta's Passthrough Camera API is accessed via WebCamTexture on Quest)
            Debug.Log("AbxrLib: Attempting WebCamTexture (Meta's recommended Passthrough Camera API access method)...");
            yield return StartCoroutine(InitializeWebCamTexture());
            if (cameraInitialized && webCamTexture != null && webCamTexture.isPlaying)
            {
                // TEST PHASE: Verify we're getting non-black frames using dedicated test method
                Debug.Log("AbxrLib: [TEST PHASE] Testing WebCamTexture validity before display...");
                yield return new WaitForSeconds(0.5f);
                bool hasValidFrames = false;
                for (int check = 0; check < 10; check++)
                {
                    if (webCamTexture.width > 0 && webCamTexture.height > 0)
                    {
                        // Use dedicated test method
                        hasValidFrames = TestCameraValidity(webCamTexture, sampleSize: 1000, threshold: 0.01f);
                        
                        if (hasValidFrames)
                        {
                            Debug.Log("AbxrLib: [TEST PHASE] WebCamTexture validity test PASSED. Proceeding to display and scanning.");
                            usingWebCamTexture = true;
                            StartScanning();
                            yield break;
                        }
                    }
                    yield return new WaitForSeconds(0.2f);
                }
                
                if (!hasValidFrames)
                {
                    Debug.LogWarning("AbxrLib: WebCamTexture initialized but frames are black. Trying reflection-based approach...");
                    // Clean up WebCamTexture and try next method
                    if (webCamTexture != null)
                    {
                        webCamTexture.Stop();
                        Destroy(webCamTexture);
                        webCamTexture = null;
                    }
                    // Mark that we're NOT using WebCamTexture anymore
                    usingWebCamTexture = false;
                    cameraInitialized = false;
                }
            }
            
            // PRIORITY 2: Try reflection-based approach (OVR/OpenXR Passthrough Camera API)
            Debug.Log("AbxrLib: Attempting reflection-based passthrough camera access...");
            InitializeOVRTypes();
            
            System.Type passthroughType = ovrPassthroughLayerType ?? openXRPassthroughCameraType;
            
            if (passthroughType == null)
            {
                Debug.LogError("AbxrLib: Passthrough camera type not found via reflection. Meta Passthrough Camera API types not available.");
                Debug.LogError("AbxrLib: SETUP REQUIRED:");
                Debug.LogError("AbxrLib: 1. Open Unity Project Settings > XR Plug-in Management > OpenXR");
                Debug.LogError("AbxrLib: 2. Under 'Features', enable 'Meta Quest Camera Passthrough'");
                Debug.LogError("AbxrLib: 3. Ensure Meta XR Core SDK v74+ is installed");
                Debug.LogError("AbxrLib: 4. Grant camera permission in Quest Settings > Privacy > Camera Access");
                Debug.LogError("AbxrLib: 5. WebCamTexture may work without Passthrough Camera API, but requires HEADSET_CAMERA permission");
                yield break;
            }
            
            // Create passthrough component or access subsystem
            bool initError = false;
            System.Type subsystemManagerType = null;
            bool needToWaitForSubsystem = false;
            
            try
            {
                // Check if it's a MonoBehaviour (can be added as component)
                bool isMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(passthroughType);
                
                if (isMonoBehaviour)
                {
                    // Create component for OVR SDK or MetaOpenXRPassthroughLayer
                    ovrPassthroughLayer = gameObject.AddComponent(passthroughType);
                    Debug.Log($"AbxrLib: Created passthrough component: {passthroughType.Name}");
                    
                    // Try to request passthrough if method exists
                    if (requestPassthroughMethod != null)
                    {
                        requestPassthroughMethod.Invoke(ovrPassthroughLayer, null);
                        Debug.Log("AbxrLib: Requested passthrough access.");
                    }
                }
                else if (passthroughType.Name.Contains("CameraSubsystem"))
                {
                    // It's a subsystem - access via XRSubsystemManager using reflection
                    Debug.Log($"AbxrLib: Found camera subsystem type: {passthroughType.Name}. Attempting to access via XRSubsystemManager...");
                    
                    // Try to get the subsystem using XRSubsystemManager or SubsystemManager via reflection
                    try
                    {
                        // Log all assemblies that might contain subsystem managers
                        Debug.Log("AbxrLib: Searching for XRSubsystemManager or SubsystemManager...");
                        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            string assemblyName = assembly.GetName().Name;
                            if (assemblyName.Contains("XR") || assemblyName.Contains("Subsystem") || assemblyName.Contains("UnityEngine"))
                            {
                                try
                                {
                                    System.Type[] types = assembly.GetTypes();
                                    foreach (System.Type type in types)
                                    {
                                        if (type.Name == "XRSubsystemManager" || type.Name == "SubsystemManager")
                                        {
                                            Debug.Log($"AbxrLib: Found {type.Name} in assembly: {assemblyName}, full name: {type.FullName}");
                                            if (subsystemManagerType == null)
                                            {
                                                subsystemManagerType = type;
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        // Try to find XRSubsystemManager via reflection
                        if (subsystemManagerType == null)
                        {
                            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    subsystemManagerType = assembly.GetType("UnityEngine.XR.XRSubsystemManager");
                                    if (subsystemManagerType != null)
                                    {
                                        Debug.Log($"AbxrLib: Found XRSubsystemManager in assembly: {assembly.GetName().Name}");
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        // Try SubsystemManager from SubsystemsImplementation namespace
                        if (subsystemManagerType == null)
                        {
                            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    subsystemManagerType = assembly.GetType("UnityEngine.SubsystemsImplementation.SubsystemManager");
                                    if (subsystemManagerType != null)
                                    {
                                        Debug.Log($"AbxrLib: Found SubsystemManager in assembly: {assembly.GetName().Name}");
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        if (subsystemManagerType == null)
                        {
                            // Try alternative namespaces via direct type lookup
                            string[] possibleNames = {
                                "UnityEngine.XR.XRSubsystemManager",
                                "UnityEngine.SubsystemsImplementation.XRSubsystemManager",
                                "UnityEngine.SubsystemsImplementation.SubsystemManager",
                                "Unity.XR.CoreUtils.XRSubsystemManager"
                            };
                            
                            foreach (string typeName in possibleNames)
                            {
                                try
                                {
                                    subsystemManagerType = System.Type.GetType(typeName);
                                    if (subsystemManagerType != null)
                                    {
                                        Debug.Log($"AbxrLib: Found subsystem manager via direct type lookup: {typeName}");
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        if (subsystemManagerType != null)
                        {
                            // Check if it's SubsystemManager (uses GetSubsystems with List) or XRSubsystemManager (uses GetSubsystem)
                            bool isSubsystemManager = subsystemManagerType.Name == "SubsystemManager";
                            
                            if (isSubsystemManager)
                            {
                                // SubsystemManager uses GetSubsystems<T>(List<T> subsystems)
                                System.Reflection.MethodInfo getSubsystemsMethod = subsystemManagerType.GetMethod("GetSubsystems", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                
                                if (getSubsystemsMethod != null)
                                {
                                    // Make it generic with the camera subsystem type
                                    System.Reflection.MethodInfo genericMethod = getSubsystemsMethod.MakeGenericMethod(passthroughType);
                                    
                                    // Create a List<T> to pass to GetSubsystems
                                    System.Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(passthroughType);
                                    System.Collections.IList subsystemsList = System.Activator.CreateInstance(listType) as System.Collections.IList;
                                    
                                    // Call GetSubsystems<T>(List<T>)
                                    genericMethod.Invoke(null, new object[] { subsystemsList });
                                    
                                    if (subsystemsList != null && subsystemsList.Count > 0)
                                    {
                                        object subsystem = subsystemsList[0];
                                        Debug.Log($"AbxrLib: Successfully retrieved camera subsystem via SubsystemManager: {passthroughType.Name}");
                                        cameraSubsystem = subsystem; // Store for later use
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"AbxrLib: Camera subsystem {passthroughType.Name} is not available via SubsystemManager. It may need to be started first.");
                                        Debug.LogWarning($"AbxrLib: Trying to start the subsystem...");
                                        
                                        // Try SubsystemManager.StartSubsystem
                                        System.Reflection.MethodInfo startSubsystemMethod = subsystemManagerType.GetMethod("StartSubsystem", 
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                        if (startSubsystemMethod != null)
                                        {
                                            System.Reflection.MethodInfo startGenericMethod = startSubsystemMethod.MakeGenericMethod(passthroughType);
                                            startGenericMethod.Invoke(null, null);
                                            Debug.Log($"AbxrLib: Attempted to start camera subsystem via SubsystemManager");
                                            needToWaitForSubsystem = true; // Mark that we need to wait and retry
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"AbxrLib: Could not find GetSubsystems method on SubsystemManager");
                                }
                            }
                            else
                            {
                                // XRSubsystemManager uses GetSubsystem<T>()
                                System.Reflection.MethodInfo getSubsystemMethod = subsystemManagerType.GetMethod("GetSubsystem", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                
                                if (getSubsystemMethod != null)
                                {
                                    // Make it generic with the camera subsystem type
                                    System.Reflection.MethodInfo genericMethod = getSubsystemMethod.MakeGenericMethod(passthroughType);
                                    object subsystem = genericMethod.Invoke(null, null);
                                    if (subsystem != null)
                                    {
                                        Debug.Log($"AbxrLib: Successfully retrieved camera subsystem via XRSubsystemManager: {passthroughType.Name}");
                                        cameraSubsystem = subsystem; // Store for later use
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"AbxrLib: Camera subsystem {passthroughType.Name} is not available. It may need to be started first.");
                                        Debug.LogWarning($"AbxrLib: Trying to start the subsystem...");
                                        
                                        // Try XRSubsystemManager.StartSubsystem
                                        System.Reflection.MethodInfo startSubsystemMethod = subsystemManagerType.GetMethod("StartSubsystem", 
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                        if (startSubsystemMethod != null)
                                        {
                                            System.Reflection.MethodInfo startGenericMethod = startSubsystemMethod.MakeGenericMethod(passthroughType);
                                            startGenericMethod.Invoke(null, null);
                                            Debug.Log($"AbxrLib: Attempted to start camera subsystem via XRSubsystemManager");
                                            needToWaitForSubsystem = true; // Mark that we need to wait and retry
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"AbxrLib: Could not find GetSubsystem method on XRSubsystemManager");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"AbxrLib: Could not find XRSubsystemManager type. Camera subsystem access may not be available.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Error accessing XRSubsystemManager: {ex.Message}");
                    }
                }
                else if (usingOpenXR && getCameraFrameMethod != null)
                {
                    // OpenXR might use static methods, try that first
                    Debug.Log("AbxrLib: Using OpenXR Passthrough Camera API (static methods)");
                    // For OpenXR, we might not need to create a component
                }
                else
                {
                    Debug.LogWarning($"AbxrLib: Passthrough type {passthroughType.Name} is not a MonoBehaviour or CameraSubsystem. Attempting to use as component anyway...");
                    try
                    {
                        ovrPassthroughLayer = gameObject.AddComponent(passthroughType);
                        Debug.Log($"AbxrLib: Created passthrough component: {passthroughType.Name}");
                    }
                    catch (System.Exception ex2)
                    {
                        Debug.LogError($"AbxrLib: Cannot add {passthroughType.Name} as component: {ex2.Message}");
                        initError = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Error creating passthrough layer: {ex.Message}\n{ex.StackTrace}");
                initError = true;
            }
            
            // If we started the subsystem, wait and try again (outside try-catch to allow yield)
            if (needToWaitForSubsystem && subsystemManagerType != null)
            {
                yield return new WaitForSeconds(0.5f);
                
                try
                {
                    System.Type passthroughTypeForRetry = ovrPassthroughLayerType ?? openXRPassthroughCameraType;
                    bool isSubsystemManager = subsystemManagerType.Name == "SubsystemManager";
                    
                    if (isSubsystemManager)
                    {
                        // SubsystemManager uses GetSubsystems<T>(List<T>)
                        System.Reflection.MethodInfo getSubsystemsMethod = subsystemManagerType.GetMethod("GetSubsystems", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (getSubsystemsMethod != null)
                        {
                            System.Reflection.MethodInfo genericMethod = getSubsystemsMethod.MakeGenericMethod(passthroughTypeForRetry);
                            System.Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(passthroughTypeForRetry);
                            System.Collections.IList subsystemsList = System.Activator.CreateInstance(listType) as System.Collections.IList;
                            genericMethod.Invoke(null, new object[] { subsystemsList });
                            
                            if (subsystemsList != null && subsystemsList.Count > 0)
                            {
                                object subsystem = subsystemsList[0];
                                Debug.Log($"AbxrLib: Successfully retrieved camera subsystem after starting via SubsystemManager: {passthroughTypeForRetry.Name}");
                                cameraSubsystem = subsystem;
                            }
                        }
                    }
                    else
                    {
                        // XRSubsystemManager uses GetSubsystem<T>()
                        System.Reflection.MethodInfo getSubsystemMethod = subsystemManagerType.GetMethod("GetSubsystem", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (getSubsystemMethod != null)
                        {
                            System.Reflection.MethodInfo genericMethod = getSubsystemMethod.MakeGenericMethod(passthroughTypeForRetry);
                            object subsystem = genericMethod.Invoke(null, null);
                            if (subsystem != null)
                            {
                                Debug.Log($"AbxrLib: Successfully retrieved camera subsystem after starting via XRSubsystemManager: {passthroughTypeForRetry.Name}");
                                cameraSubsystem = subsystem;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: Error retrieving subsystem after start: {ex.Message}");
                }
            }
            
            if (initError)
            {
                yield break;
            }
            
            // Wait for passthrough to initialize
            yield return new WaitForSeconds(0.5f);
            
            // Get the texture from passthrough layer or subsystem
            Texture passthroughTex = null;
            bool textureError = false;
            bool needsStart = false;
            System.Type subsystemTypeForStart = null;
            try
            {
                object sourceObject = ovrPassthroughLayer != null ? (object)ovrPassthroughLayer : cameraSubsystem;
                
                if (passthroughTextureProperty != null && sourceObject != null)
                {
                    object textureObj = passthroughTextureProperty.GetValue(sourceObject);
                    passthroughTex = textureObj as Texture;
                }
                else if (cameraSubsystem != null)
                {
                    // For XRCameraSubsystem, we should use TryAcquireLatestCpuImage instead of texture property
                    // But first, let's check if there's a direct texture property (some implementations may have it)
                    System.Type subsystemType = cameraSubsystem.GetType();
                    System.Reflection.PropertyInfo[] properties = subsystemType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    Debug.Log($"AbxrLib: Checking {properties.Length} properties on camera subsystem for texture...");
                    foreach (var prop in properties)
                    {
                        if (typeof(Texture).IsAssignableFrom(prop.PropertyType) || prop.PropertyType == typeof(Texture2D) || prop.PropertyType == typeof(RenderTexture))
                        {
                            try
                            {
                                object textureObj = prop.GetValue(cameraSubsystem);
                                passthroughTex = textureObj as Texture;
                                if (passthroughTex != null)
                                {
                                    Debug.Log($"AbxrLib: Found texture property '{prop.Name}' (Type: {prop.PropertyType.Name}) on camera subsystem.");
                                    passthroughTextureProperty = prop; // Cache for later use
                                    break;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"AbxrLib: Error getting texture from property '{prop.Name}': {ex.Message}");
                            }
                        }
                    }
                    
                    // If no direct texture property, we'll use TryAcquireLatestCpuImage in the scanning loop
                    if (passthroughTex == null)
                    {
                        Debug.Log("AbxrLib: No direct texture property found on camera subsystem. Will use TryAcquireLatestCpuImage for frame access.");
                        subsystemTypeForStart = subsystemType;
                        
                        // CRITICAL: Check for AR Camera Manager component (required by Meta OpenXR Camera documentation)
                        // Documentation states: "To use Passthrough in your scene, you must have an AR Camera Manager component attached to your camera"
                        try
                        {
                            Camera mainCamera = Camera.main;
                            if (mainCamera == null)
                            {
                                // Try to find any camera
                                Camera[] allCameras = FindObjectsOfType<Camera>();
                                if (allCameras.Length > 0)
                                {
                                    mainCamera = allCameras[0];
                                    Debug.Log($"AbxrLib: Camera.main was null, using first camera found: {mainCamera.name}");
                                }
                            }
                            
                            if (mainCamera != null)
                            {
                                // Log which camera we're checking (helpful for debugging)
                                string cameraPath = GetGameObjectPath(mainCamera.gameObject);
                                Debug.Log($"AbxrLib: Checking camera for AR Camera Manager: '{mainCamera.name}' (Path: {cameraPath})");
                                
                                // NOTE: We're using TryAcquireLatestCpuImage to get CPU frames from the front-facing camera,
                                // which is independent of the scene camera's rendering settings. The camera background/Clear Flags
                                // settings are only needed if rendering passthrough through the scene camera, which we're not doing.
                                // However, AR Camera Manager may still be required to enable the passthrough camera feature.
                                
                                // Try to find AR Camera Manager type via reflection (AR Foundation)
                                System.Type arCameraManagerType = null;
                                foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                                {
                                    arCameraManagerType = assembly.GetType("UnityEngine.XR.ARFoundation.ARCameraManager");
                                    if (arCameraManagerType != null) break;
                                }
                                
                                if (arCameraManagerType != null)
                                {
                                    // Check if AR Camera Manager is already attached
                                    Component existingManager = mainCamera.GetComponent(arCameraManagerType);
                                    
                                    if (existingManager == null)
                                    {
                                        Debug.LogWarning("AbxrLib: AR Camera Manager component not found on camera. Adding it (required for Meta Passthrough Camera)...");
                                        try
                                        {
                                            Component arCameraManager = mainCamera.gameObject.AddComponent(arCameraManagerType);
                                            Debug.Log("AbxrLib: Successfully added AR Camera Manager component to camera.");
                                            
                                            // Try to enable it via reflection (it might have an 'enabled' property)
                                            System.Reflection.PropertyInfo enabledProp = arCameraManagerType.GetProperty("enabled");
                                            if (enabledProp != null)
                                            {
                                                enabledProp.SetValue(arCameraManager, true);
                                                Debug.Log("AbxrLib: AR Camera Manager component enabled.");
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Debug.LogError($"AbxrLib: Failed to add AR Camera Manager component: {ex.Message}");
                                            Debug.LogError("AbxrLib: Please manually add AR Camera Manager component to your Main Camera in the Unity Editor.");
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log("AbxrLib: AR Camera Manager component found on camera.");
                                        
                                        // Check if it's enabled
                                        System.Reflection.PropertyInfo enabledProp = arCameraManagerType.GetProperty("enabled");
                                        if (enabledProp != null)
                                        {
                                            bool isEnabled = (bool)enabledProp.GetValue(existingManager);
                                            Debug.Log($"AbxrLib: AR Camera Manager enabled: {isEnabled}");
                                            
                                            if (!isEnabled)
                                            {
                                                Debug.LogWarning("AbxrLib: AR Camera Manager is disabled. Enabling it...");
                                                enabledProp.SetValue(existingManager, true);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning("AbxrLib: AR Foundation package not found. AR Camera Manager component may be required for Meta Passthrough Camera.");
                                    Debug.LogWarning("AbxrLib: Please install AR Foundation package and add AR Camera Manager component to your Main Camera.");
                                    Debug.LogWarning("AbxrLib: Install via: Window > Package Manager > Unity Registry > AR Foundation");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("AbxrLib: No camera found in scene. AR Camera Manager component cannot be added.");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"AbxrLib: Error checking/adding AR Camera Manager: {ex.Message}");
                        }
                        
                        // Check and configure camera subsystem
                        try
                        {
                            // Check current and requested camera feature
                            System.Reflection.PropertyInfo currentCameraProp = subsystemType.GetProperty("currentCamera", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            System.Reflection.PropertyInfo requestedCameraProp = subsystemType.GetProperty("requestedCamera", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            if (currentCameraProp != null)
                            {
                                object currentCamera = currentCameraProp.GetValue(cameraSubsystem);
                                Debug.Log($"AbxrLib: Camera subsystem currentCamera: {currentCamera}");
                            }
                            
                            if (requestedCameraProp != null)
                            {
                                object requestedCamera = requestedCameraProp.GetValue(cameraSubsystem);
                                Debug.Log($"AbxrLib: Camera subsystem requestedCamera: {requestedCamera}");
                                
                                // Try to set requestedCamera to request the camera feature
                                // The Feature enum might have values like None, WorldFacingCamera, UserFacingCamera, etc.
                                // For QR code scanning, we need the front-facing (user-facing) camera
                                System.Type featureType = requestedCameraProp.PropertyType;
                                if (featureType.IsEnum)
                                {
                                    // Log all available enum values for debugging
                                    System.Array enumValues = System.Enum.GetValues(featureType);
                                    Debug.Log($"AbxrLib: Available camera Feature enum values:");
                                    foreach (object enumValue in enumValues)
                                    {
                                        long longValue = System.Convert.ToInt64(enumValue);
                                        Debug.Log($"AbxrLib:   - {enumValue} ({longValue})");
                                    }
                                    
                                    // Prefer UserFacingCamera for QR code scanning (front-facing camera)
                                    // Fallback to any non-zero value if UserFacingCamera not found
                                    object preferredValue = null;
                                    object fallbackValue = null;
                                    
                                    foreach (object enumValue in enumValues)
                                    {
                                        long longValue = System.Convert.ToInt64(enumValue);
                                        string enumName = enumValue.ToString();
                                        
                                        // Skip None/0 value
                                        if (longValue == 0) continue;
                                        
                                        // Prefer UserFacingCamera for QR code scanning (front-facing camera)
                                        // Avoid WorldFacingCamera (back-facing camera)
                                        if (enumName.Contains("User") || enumName.Contains("Front"))
                                        {
                                            preferredValue = enumValue;
                                            Debug.Log($"AbxrLib: Found preferred camera feature for QR scanning: {enumValue} ({longValue})");
                                            break; // Use this immediately
                                        }
                                        
                                        // Keep first non-zero as fallback (but prefer not WorldFacingCamera)
                                        if (fallbackValue == null && !enumName.Contains("World"))
                                        {
                                            fallbackValue = enumValue;
                                        }
                                    }
                                    
                                    // Use preferred value, or fallback to first non-zero
                                    object valueToSet = preferredValue ?? fallbackValue;
                                    
                                    if (valueToSet != null)
                                    {
                                        Debug.Log($"AbxrLib: Attempting to set requestedCamera to: {valueToSet} (for QR code scanning)");
                                        try
                                        {
                                            requestedCameraProp.SetValue(cameraSubsystem, valueToSet);
                                            Debug.Log($"AbxrLib: Successfully set requestedCamera to: {valueToSet}");
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Debug.LogWarning($"AbxrLib: Could not set requestedCamera to {valueToSet}: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning("AbxrLib: No valid camera feature enum value found to set.");
                                    }
                                }
                            }
                            
                            // Check permission first
                            System.Reflection.PropertyInfo permissionProp = subsystemType.GetProperty("permissionGranted", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            if (permissionProp != null)
                            {
                                bool hasPermission = (bool)permissionProp.GetValue(cameraSubsystem);
                                Debug.Log($"AbxrLib: Camera subsystem permission status: {hasPermission}");
                                
                                if (!hasPermission)
                                {
                                    Debug.LogWarning("AbxrLib: Camera subsystem does not have permission. Ensure HEADSET_CAMERA permission is granted in Quest Settings > Privacy > Camera Access.");
                                    Debug.LogWarning("AbxrLib: Also verify 'Meta Quest: Camera (Passthrough)' is enabled in Project Settings > XR Plug-in Management > OpenXR > Features");
                                }
                            }
                            
                            // Check if subsystem is running
                            System.Reflection.PropertyInfo runningProp = subsystemType.GetProperty("running", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            if (runningProp != null)
                            {
                                bool isRunning = (bool)runningProp.GetValue(cameraSubsystem);
                                Debug.Log($"AbxrLib: Camera subsystem running status: {isRunning}");
                                
                                if (!isRunning)
                                {
                                    // Try to start the subsystem
                                    System.Reflection.MethodInfo startMethod = subsystemType.GetMethod("Start", 
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                    
                                    if (startMethod != null)
                                    {
                                        Debug.Log("AbxrLib: Starting camera subsystem...");
                                        startMethod.Invoke(cameraSubsystem, null);
                                        needsStart = true;
                                    }
                                    else
                                    {
                                        Debug.LogWarning("AbxrLib: Could not find Start() method on camera subsystem.");
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning("AbxrLib: Could not find 'running' property on camera subsystem.");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"AbxrLib: Error checking/starting camera subsystem: {ex.Message}");
                        }
                        
                        // Mark as initialized - we'll use TryAcquireLatestCpuImage in the scanning loop
                        cameraInitialized = true;
                        Debug.Log("AbxrLib: Camera subsystem initialized. Ready to use TryAcquireLatestCpuImage for frame access.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Error getting passthrough texture: {ex.Message}\n{ex.StackTrace}");
                textureError = true;
            }
            
            // Wait a moment after setting requestedCamera for the subsystem to process (outside try-catch to allow yield)
            if (cameraSubsystem != null && subsystemTypeForStart != null)
            {
                yield return new WaitForSeconds(0.5f);
                
                // Re-check requestedCamera and currentCamera after delay
                try
                {
                    System.Reflection.PropertyInfo requestedCameraPropAfterDelay = subsystemTypeForStart.GetProperty("requestedCamera", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    System.Reflection.PropertyInfo currentCameraPropAfterDelay = subsystemTypeForStart.GetProperty("currentCamera", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (requestedCameraPropAfterDelay != null && cameraSubsystem != null)
                    {
                        object newRequestedCamera = requestedCameraPropAfterDelay.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: requestedCamera after delay: {newRequestedCamera}");
                        
                        // If requestedCamera reverted to None, try setting it again
                        if (newRequestedCamera != null && newRequestedCamera.ToString() == "None")
                        {
                            Debug.LogWarning("AbxrLib: requestedCamera reverted to None after being set. This may indicate:");
                            Debug.LogWarning("AbxrLib: 1. OpenXR feature 'Meta Quest: Camera (Passthrough)' is not enabled in Project Settings");
                            Debug.LogWarning("AbxrLib: 2. Permission is not granted (check Quest Settings > Privacy > Camera Access)");
                            Debug.LogWarning("AbxrLib: 3. AR Camera Manager component may not be properly configured");
                            
                            // Try to find and set UserFacingCamera again
                            System.Type featureType = requestedCameraPropAfterDelay.PropertyType;
                            if (featureType != null && featureType.IsEnum)
                            {
                                System.Array enumValues = System.Enum.GetValues(featureType);
                                foreach (object enumValue in enumValues)
                                {
                                    long longValue = System.Convert.ToInt64(enumValue);
                                    string enumName = enumValue.ToString();
                                    
                                    if (longValue != 0 && (enumName.Contains("User") || enumName.Contains("Front")))
                                    {
                                        Debug.Log($"AbxrLib: Re-attempting to set requestedCamera to: {enumValue}");
                                        try
                                        {
                                            requestedCameraPropAfterDelay.SetValue(cameraSubsystem, enumValue);
                                            Debug.Log($"AbxrLib: Re-set requestedCamera to: {enumValue}");
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Debug.LogWarning($"AbxrLib: Failed to re-set requestedCamera: {ex.Message}");
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (currentCameraPropAfterDelay != null && cameraSubsystem != null)
                    {
                        object newCurrentCamera = currentCameraPropAfterDelay.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: currentCamera after delay: {newCurrentCamera}");
                        
                        if (newCurrentCamera != null && newCurrentCamera.ToString() == "None")
                        {
                            Debug.LogWarning("AbxrLib: currentCamera is still None. The camera feature may not be active.");
                            Debug.LogWarning("AbxrLib: Verify 'Meta Quest: Camera (Passthrough)' is enabled in Project Settings > XR Plug-in Management > OpenXR > Features");
                        }
                    }
                    
                    // Re-check permission after delay
                    System.Reflection.PropertyInfo permissionPropAfterDelay = subsystemTypeForStart.GetProperty("permissionGranted", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (permissionPropAfterDelay != null && cameraSubsystem != null)
                    {
                        bool hasPermissionAfterDelay = (bool)permissionPropAfterDelay.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: Camera subsystem permission status after delay: {hasPermissionAfterDelay}");
                        
                        if (hasPermissionAfterDelay)
                        {
                            Debug.Log("AbxrLib: Permission granted! Camera should now be accessible.");
                        }
                        else
                        {
                            Debug.LogWarning("AbxrLib: Permission still not granted after delay. Camera may not be accessible.");
                            Debug.LogWarning("AbxrLib: CRITICAL: Without permission, TryAcquireLatestCpuImage will always return false.");
                            Debug.LogWarning("AbxrLib: Please verify:");
                            Debug.LogWarning("AbxrLib: 1. 'Meta Quest: Camera (Passthrough)' is enabled in Project Settings > XR Plug-in Management > OpenXR > Features");
                            Debug.LogWarning("AbxrLib: 2. Camera permission is granted in Quest Settings > Privacy > Camera Access");
                            Debug.LogWarning("AbxrLib: 3. AR Camera Manager component is attached and enabled on Main Camera");
                            Debug.LogWarning("AbxrLib: 4. Camera Clear Flags is set to Solid Color with alpha=0");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: Error re-checking camera subsystem after delay: {ex.Message}");
                }
            }
            
            // Wait a bit for subsystem to start if we tried to start it (outside try-catch to allow yield)
            if (needsStart && subsystemTypeForStart != null && cameraSubsystem != null)
            {
                yield return new WaitForSeconds(0.5f);
                
                // Check running status and permission again
                try
                {
                    System.Reflection.PropertyInfo runningProp = subsystemTypeForStart.GetProperty("running", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (runningProp != null)
                    {
                        bool isRunning = (bool)runningProp.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: Camera subsystem running status after Start(): {isRunning}");
                        
                        if (!isRunning)
                        {
                            Debug.LogWarning("AbxrLib: Camera subsystem did not start. It may need permissions or additional configuration.");
                        }
                    }
                    
                    // Re-check permission after start
                    System.Reflection.PropertyInfo permissionProp = subsystemTypeForStart.GetProperty("permissionGranted", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (permissionProp != null)
                    {
                        bool hasPermission = (bool)permissionProp.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: Camera subsystem permission status after Start(): {hasPermission}");
                        
                        if (!hasPermission)
                        {
                            Debug.LogError("AbxrLib: CRITICAL: Camera subsystem permission is still false after starting.");
                            Debug.LogError("AbxrLib: Please verify:");
                            Debug.LogError("AbxrLib: 1. 'Meta Quest: Camera (Passthrough)' is enabled in Project Settings > XR Plug-in Management > OpenXR > Features");
                            Debug.LogError("AbxrLib: 2. Camera permission is granted in Quest Settings > Privacy > Camera Access");
                            Debug.LogError("AbxrLib: 3. Unity OpenXR Meta package is installed and up to date");
                        }
                    }
                    
                    // Check current/requested camera after start
                    System.Reflection.PropertyInfo currentCameraProp = subsystemTypeForStart.GetProperty("currentCamera", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    System.Reflection.PropertyInfo requestedCameraProp = subsystemTypeForStart.GetProperty("requestedCamera", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (currentCameraProp != null)
                    {
                        object currentCamera = currentCameraProp.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: Camera subsystem currentCamera after Start(): {currentCamera}");
                    }
                    
                    if (requestedCameraProp != null)
                    {
                        object requestedCamera = requestedCameraProp.GetValue(cameraSubsystem);
                        Debug.Log($"AbxrLib: Camera subsystem requestedCamera after Start(): {requestedCamera}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: Error checking status after start: {ex.Message}");
                }
            }
            
            // If we're using TryAcquireLatestCpuImage, start scanning now
            if (cameraInitialized && passthroughTex == null && cameraSubsystem != null && passthroughTextureProperty == null)
            {
                StartScanning();
                yield break; // Exit early - we don't need a texture property
            }
            
            if (textureError)
            {
                yield break;
            }
            
            if (passthroughTex != null)
            {
                Debug.Log($"AbxrLib: Got passthrough texture: {passthroughTex.width}x{passthroughTex.height}, type: {passthroughTex.GetType().Name}");
                
                // Create RenderTexture to capture passthrough feed
                if (passthroughRenderTexture == null)
                {
                    passthroughRenderTexture = new RenderTexture(passthroughTex.width, passthroughTex.height, 0, RenderTextureFormat.ARGB32);
                    passthroughRenderTexture.Create();
                    Debug.Log($"AbxrLib: Created RenderTexture for passthrough: {passthroughRenderTexture.width}x{passthroughRenderTexture.height}");
                }
                
                cameraInitialized = true;
                Debug.Log("AbxrLib: Passthrough camera initialized successfully.");
                StartScanning();
            }
            else if (cameraSubsystem == null)
            {
                Debug.LogWarning("AbxrLib: Passthrough texture is null and no camera subsystem available. Waiting for texture to become available...");
                
                // Wait and retry
                for (int i = 0; i < 10; i++)
                {
                    yield return new WaitForSeconds(0.2f);
                    
                    try
                    {
                        object sourceObject = ovrPassthroughLayer != null ? (object)ovrPassthroughLayer : cameraSubsystem;
                        if (passthroughTextureProperty != null && sourceObject != null)
                        {
                            object textureObj = passthroughTextureProperty.GetValue(sourceObject);
                            passthroughTex = textureObj as Texture;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Error getting passthrough texture in retry {i + 1}: {ex.Message}");
                    }
                    
                    if (passthroughTex != null)
                    {
                        Debug.Log($"AbxrLib: Passthrough texture available after {i + 1} attempts: {passthroughTex.width}x{passthroughTex.height}");
                        
                        if (passthroughRenderTexture == null)
                        {
                            passthroughRenderTexture = new RenderTexture(passthroughTex.width, passthroughTex.height, 0, RenderTextureFormat.ARGB32);
                            passthroughRenderTexture.Create();
                        }
                        
                        cameraInitialized = true;
                        StartScanning();
                        yield break;
                    }
                }
                
                Debug.LogError("AbxrLib: Passthrough texture never became available.");
            }
            
            if (passthroughTextureProperty == null)
            {
                Debug.LogError("AbxrLib: Could not find texture property on OVRPassthroughLayer.");
            }
        }
        
        /// <summary>
        /// Initialize WebCamTexture (Meta's Passthrough Camera API routes through WebCamTexture on Quest 3+)
        /// </summary>
        private IEnumerator InitializeWebCamTexture()
        {
            Debug.Log("AbxrLib: Initializing WebCamTexture (Meta's Passthrough Camera API access method)...");
            usingWebCamTexture = true;
            
            // Request camera permissions on Android
#if UNITY_ANDROID && !UNITY_EDITOR
            // Request standard camera permission
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
            
            // Check headset camera permission (Quest-specific)
            // Note: This permission may need to be granted in system settings
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
                using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    // Check if permission exists in manifest
                    int permissionCheck = packageManager.Call<int>("checkPermission", "horizonos.permission.HEADSET_CAMERA", 
                        currentActivity.Call<string>("getPackageName"));
                    
                    if (permissionCheck == 0) // PERMISSION_GRANTED
                    {
                        Debug.Log("AbxrLib: HEADSET_CAMERA permission is granted.");
                    }
                    else
                    {
                                Debug.LogWarning("AbxrLib: HEADSET_CAMERA permission check returned: " + permissionCheck + 
                                       " (0=granted, -1=denied).");
                        Debug.LogWarning("AbxrLib: IMPORTANT: To enable camera access on Quest:");
                        Debug.LogWarning("AbxrLib: 1. Enable 'Meta Quest Camera Passthrough' feature in OpenXR Package Settings");
                        Debug.LogWarning("AbxrLib: 2. Grant camera permission in Quest Settings > Privacy > Camera Access");
                        Debug.LogWarning("AbxrLib: 3. Ensure Meta XR Core SDK v74+ is installed");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: Could not check HEADSET_CAMERA permission: {ex.Message}");
            }
#endif
            
            // Find the forward-facing camera
            WebCamDevice[] devices = WebCamTexture.devices;
            WebCamDevice? frontCamera = null;
            
            Debug.Log($"AbxrLib: Found {devices.Length} camera device(s):");
            for (int i = 0; i < devices.Length; i++)
            {
                Debug.Log($"AbxrLib:   Camera {i}: '{devices[i].name}' (isFrontFacing: {devices[i].isFrontFacing})");
            }
            
            // First, try to find a camera marked as front-facing
            foreach (WebCamDevice device in devices)
            {
                if (device.isFrontFacing)
                {
                    frontCamera = device;
                    Debug.Log($"AbxrLib: Found front-facing camera: '{device.name}'");
                    break;
                }
            }
            
            // If no front-facing camera found, look by name
            if (!frontCamera.HasValue)
            {
                foreach (WebCamDevice device in devices)
                {
                    string deviceName = device.name.ToLower();
                    if (deviceName.Contains("front") || deviceName.Contains("passthrough") || 
                        deviceName.Contains("quest") || deviceName.Contains("color"))
                    {
                        frontCamera = device;
                        Debug.Log($"AbxrLib: Found camera by name pattern: '{device.name}'");
                        break;
                    }
                }
            }
            
            // If still no camera found, use the first available camera
            if (!frontCamera.HasValue && devices.Length > 0)
            {
                frontCamera = devices[0];
                Debug.Log($"AbxrLib: Using first available camera: '{devices[0].name}'");
            }
            
            if (!frontCamera.HasValue)
            {
                Debug.LogError("AbxrLib: No camera found. Cannot scan QR codes.");
                yield break;
            }
            
            // Create WebCamTexture - let system decide optimal settings
            webCamTexture = new WebCamTexture(frontCamera.Value.name);
            webCamTexture.Play();
            
            Debug.Log($"AbxrLib: Starting WebCamTexture: '{frontCamera.Value.name}', isFrontFacing: {frontCamera.Value.isFrontFacing}");
            
            // Wait for camera to start and get valid dimensions
            int waitCount = 0;
            while (waitCount < 50 && (webCamTexture.width <= 0 || webCamTexture.height <= 0))
            {
                yield return new WaitForSeconds(0.1f);
                waitCount++;
                if (waitCount % 10 == 0)
                {
                    Debug.Log($"AbxrLib: Waiting for WebCamTexture dimensions... (attempt {waitCount}/50, size: {webCamTexture.width}x{webCamTexture.height}, isPlaying: {webCamTexture.isPlaying})");
                }
            }
            
            if (webCamTexture.width > 0 && webCamTexture.height > 0)
            {
                Debug.Log($"AbxrLib: WebCamTexture dimensions ready: {webCamTexture.width}x{webCamTexture.height}");
                
                // Create RenderTexture for WebCamTexture
                if (webCamRenderTexture == null)
                {
                    webCamRenderTexture = new RenderTexture(webCamTexture.width, webCamTexture.height, 0, RenderTextureFormat.ARGB32);
                    webCamRenderTexture.Create();
                    Debug.Log($"AbxrLib: Created RenderTexture for WebCamTexture: {webCamRenderTexture.width}x{webCamRenderTexture.height}");
                }
                
                // TEST PHASE: Wait for camera to start producing non-black frames (separate from display)
                Debug.Log("AbxrLib: [TEST PHASE] Testing WebCamTexture for valid (non-black) frames...");
                int waitForFrames = 0;
                bool gotValidFrame = false;
                while (waitForFrames < 50 && !gotValidFrame) // Wait up to 5 seconds
                {
                    yield return new WaitForSeconds(0.1f);
                    waitForFrames++;
                    
                    // Use dedicated test method to check camera validity
                    if (webCamTexture.width > 0 && webCamTexture.height > 0 && webCamTexture.isPlaying)
                    {
                        gotValidFrame = TestCameraValidity(webCamTexture, sampleSize: 1000, threshold: 0.01f);
                        
                        if (gotValidFrame)
                        {
                            Debug.Log($"AbxrLib: [TEST PHASE] WebCamTexture validity test PASSED after {waitForFrames * 0.1f} seconds");
                        }
                        else if (waitForFrames % 10 == 0)
                        {
                            Debug.Log($"AbxrLib: [TEST PHASE] Still testing... (attempt {waitForFrames}/50, camera producing black frames)");
                        }
                    }
                }
                
                if (!gotValidFrame)
                {
                    Debug.LogWarning($"AbxrLib: WebCamTexture did not produce valid frames after {waitForFrames * 0.1f} seconds. Camera may not be accessible or may need additional permissions.");
                    Debug.LogWarning("AbxrLib: TROUBLESHOOTING:");
                    Debug.LogWarning("AbxrLib: 1. Check Quest Settings > Privacy > Camera Access - ensure app has permission");
                    Debug.LogWarning("AbxrLib: 2. Enable 'Meta Quest Camera Passthrough' in OpenXR Package Settings");
                    Debug.LogWarning("AbxrLib: 3. Verify HEADSET_CAMERA permission is in AndroidManifest.xml (should be auto-added)");
                    Debug.LogWarning("AbxrLib: 4. On Quest 3+, WebCamTexture should route to Passthrough Camera API if properly configured");
                }
                
                cameraInitialized = true;
                Debug.Log($"AbxrLib: WebCamTexture initialized: {frontCamera.Value.name}, size: {webCamTexture.width}x{webCamTexture.height}, isPlaying: {webCamTexture.isPlaying}, gotValidFrame: {gotValidFrame}");
                StartScanning();
            }
            else
            {
                Debug.LogError($"AbxrLib: Failed to initialize WebCamTexture. Final state: isPlaying={webCamTexture.isPlaying}, size={webCamTexture.width}x{webCamTexture.height}");
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
            // TEST PHASE: Verify camera is producing valid frames before displaying
            if (usingWebCamTexture && webCamTexture != null && webCamTexture.isPlaying)
            {
                Debug.Log("AbxrLib: [TEST PHASE] Testing camera validity before displaying in overlay...");
                bool isValid = TestCameraValidity(webCamTexture, sampleSize: 1000, threshold: 0.01f);
                if (!isValid)
                {
                    Debug.LogWarning("AbxrLib: [TEST PHASE] Camera validity test FAILED - camera is producing black frames. Display may be black.");
                    Debug.LogWarning("AbxrLib: This may indicate:");
                    Debug.LogWarning("AbxrLib: 1. OpenXR features not enabled (Meta Quest: Camera Passthrough, Meta Quest: Session)");
                    Debug.LogWarning("AbxrLib: 2. Camera permission not granted");
                    Debug.LogWarning("AbxrLib: 3. AR Camera Manager not configured");
                    // Continue anyway - user can see the black screen and know something is wrong
                }
            }
            
            // Check if we have a valid camera source
            bool hasWebCam = usingWebCamTexture && webCamTexture != null && webCamTexture.isPlaying && webCamRenderTexture != null;
            
            // For passthrough: either we have a texture property (needs passthroughRenderTexture) 
            // OR we have a camera subsystem that supports TryAcquireLatestCpuImage (no texture property needed)
            bool hasPassthroughTexture = !usingWebCamTexture && (ovrPassthroughLayer != null || cameraSubsystem != null) && passthroughRenderTexture != null;
            bool hasPassthroughCpuImage = !usingWebCamTexture && cameraSubsystem != null && passthroughTextureProperty == null;
            bool hasPassthrough = hasPassthroughTexture || hasPassthroughCpuImage;
            
            // Detailed debug logging
            Debug.Log($"AbxrLib: StartScanning() check - usingWebCamTexture: {usingWebCamTexture}, " +
                     $"cameraSubsystem: {cameraSubsystem != null}, passthroughTextureProperty: {passthroughTextureProperty != null}, " +
                     $"passthroughRenderTexture: {passthroughRenderTexture != null}, " +
                     $"ovrPassthroughLayer: {ovrPassthroughLayer != null}");
            
            if (!hasWebCam && !hasPassthrough)
            {
                Debug.LogError($"AbxrLib: Camera not ready for scanning. WebCam: {hasWebCam}, Passthrough: {hasPassthrough} (Texture: {hasPassthroughTexture}, CpuImage: {hasPassthroughCpuImage})");
                Debug.LogError($"AbxrLib: Debug details - usingWebCamTexture: {usingWebCamTexture}, cameraSubsystem: {cameraSubsystem != null}, passthroughTextureProperty: {passthroughTextureProperty != null}");
                return;
            }
            
            isScanning = true;
            CreateOverlayUI();
            
            // Update camera texture in overlay if it already exists
            if (overlayCanvas != null)
            {
                RawImage[] rawImages = overlayCanvas.GetComponentsInChildren<RawImage>();
                bool found = false;
                foreach (RawImage img in rawImages)
                {
                    if (img.gameObject.name == "CameraDisplay")
                    {
                        // Use WebCamTexture if available (primary method), otherwise passthrough RenderTexture
                        if (usingWebCamTexture && webCamTexture != null)
                        {
                            img.texture = webCamTexture;
                            Debug.Log($"AbxrLib: Updated WebCamTexture in overlay UI: {webCamTexture.width}x{webCamTexture.height}, isPlaying: {webCamTexture.isPlaying}");
                        }
                        else if (passthroughRenderTexture != null)
                        {
                            img.texture = passthroughRenderTexture;
                            Debug.Log($"AbxrLib: Updated passthrough texture in overlay UI: {passthroughRenderTexture.width}x{passthroughRenderTexture.height}");
                        }
                        
                        Debug.Log($"AbxrLib: Texture assigned: {img.texture != null}, " +
                                 $"RawImage active: {img.gameObject.activeInHierarchy}, Canvas active: {overlayCanvas.activeInHierarchy}");
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogWarning("AbxrLib: CameraDisplay RawImage not found in overlay UI");
                }
            }
            else
            {
                Debug.LogWarning($"AbxrLib: Cannot update overlay - overlayCanvas: {overlayCanvas != null}");
            }
            
            scanningCoroutine = StartCoroutine(ScanForQRCode());
            
            // Start a coroutine to capture a debug screenshot 2 seconds after overlay appears
            StartCoroutine(CaptureDebugScreenshot());
        }
        
        /// <summary>
        /// Capture a debug screenshot 2 seconds after overlay appears to verify camera feed
        /// </summary>
        private IEnumerator CaptureDebugScreenshot()
        {
            yield return new WaitForSeconds(2.0f);
            
            Debug.Log("AbxrLib: Capturing debug screenshot to verify camera feed...");
            
            try
            {
                Texture2D screenshot = null;
                
                if (usingWebCamTexture && webCamTexture != null && webCamTexture.isPlaying)
                {
                    // Capture from WebCamTexture
                    if (webCamTexture.width > 0 && webCamTexture.height > 0)
                    {
                        try
                        {
                            Color32[] pixels = webCamTexture.GetPixels32();
                            if (pixels != null && pixels.Length > 0)
                            {
                                screenshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                                screenshot.SetPixels32(pixels);
                                screenshot.Apply();
                            }
                        }
                        catch
                        {
                            // Fallback to RenderTexture
                            Graphics.Blit(webCamTexture, webCamRenderTexture);
                            RenderTexture.active = webCamRenderTexture;
                            screenshot = new Texture2D(webCamRenderTexture.width, webCamRenderTexture.height, TextureFormat.RGB24, false);
                            screenshot.ReadPixels(new Rect(0, 0, webCamRenderTexture.width, webCamRenderTexture.height), 0, 0);
                            screenshot.Apply();
                            RenderTexture.active = null;
                        }
                    }
                }
                else if (passthroughRenderTexture != null)
                {
                    // Capture from passthrough RenderTexture
                    RenderTexture.active = passthroughRenderTexture;
                    screenshot = new Texture2D(passthroughRenderTexture.width, passthroughRenderTexture.height, TextureFormat.RGB24, false);
                    screenshot.ReadPixels(new Rect(0, 0, passthroughRenderTexture.width, passthroughRenderTexture.height), 0, 0);
                    screenshot.Apply();
                    RenderTexture.active = null;
                }
                
                if (screenshot != null)
                {
                    // Convert to PNG bytes
                    byte[] pngData = screenshot.EncodeToPNG();
                    
                    // Save to persistent data path
                    string filename = $"MetaQRDebug_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);
                    
                    System.IO.File.WriteAllBytes(filepath, pngData);
                    
                    // Log pixel statistics
                    Color32[] pixels = screenshot.GetPixels32();
                    if (pixels != null && pixels.Length > 0)
                    {
                        float totalBrightness = 0f;
                        int sampleSize = Mathf.Min(1000, pixels.Length);
                        int nonBlackCount = 0;
                        for (int i = 0; i < sampleSize; i++)
                        {
                            int index = (i * pixels.Length) / sampleSize;
                            Color32 c = pixels[index];
                            float brightness = (c.r + c.g + c.b) / 3f / 255f;
                            totalBrightness += brightness;
                            if (brightness > 0.01f) nonBlackCount++;
                        }
                        float avgBrightness = totalBrightness / sampleSize;
                        
                        Debug.Log($"AbxrLib: Debug screenshot saved: {filepath}");
                        Debug.Log($"AbxrLib: Screenshot stats - Size: {screenshot.width}x{screenshot.height}, " +
                                 $"Avg brightness: {avgBrightness:F3}, Non-black pixels: {nonBlackCount}/{sampleSize} ({100f * nonBlackCount / sampleSize:F1}%)");
                        Debug.Log($"AbxrLib: To retrieve screenshot, run: adb pull {filepath}");
                    }
                    
                    Destroy(screenshot);
                }
                else
                {
                    Debug.LogWarning("AbxrLib: Could not capture debug screenshot - no valid camera source");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Error capturing debug screenshot: {ex.Message}\n{ex.StackTrace}");
            }
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
            int scanCount = 0;
            string cameraType = usingWebCamTexture ? "WebCamTexture (Meta Passthrough Camera API)" : "Meta Passthrough Camera API (Reflection)";
            Debug.Log($"AbxrLib: Starting QR code scanning loop using {cameraType}...");
            
            while (isScanning)
            {
                Texture2D snapshot = null;
                bool frameError = false;
                
                try
                {
                    if (usingWebCamTexture && webCamTexture != null && webCamTexture.isPlaying)
                    {
                        // Use WebCamTexture fallback
                        if (webCamTexture.width > 0 && webCamTexture.height > 0)
                        {
                            // Try direct GetPixels32 first (more reliable on some Android devices)
                            try
                            {
                                Color32[] pixels = webCamTexture.GetPixels32();
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
                                        snapshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                                        snapshot.SetPixels32(pixels);
                                        snapshot.Apply();
                                    }
                                    else
                                    {
                                        // All black, try RenderTexture approach
                                        Graphics.Blit(webCamTexture, webCamRenderTexture);
                                        RenderTexture.active = webCamRenderTexture;
                                        snapshot = new Texture2D(webCamRenderTexture.width, webCamRenderTexture.height, TextureFormat.RGB24, false);
                                        snapshot.ReadPixels(new Rect(0, 0, webCamRenderTexture.width, webCamRenderTexture.height), 0, 0);
                                        snapshot.Apply();
                                        RenderTexture.active = null;
                                    }
                                }
                                else
                                {
                                    // Fallback to RenderTexture
                                    Graphics.Blit(webCamTexture, webCamRenderTexture);
                                    RenderTexture.active = webCamRenderTexture;
                                    snapshot = new Texture2D(webCamRenderTexture.width, webCamRenderTexture.height, TextureFormat.RGB24, false);
                                    snapshot.ReadPixels(new Rect(0, 0, webCamRenderTexture.width, webCamRenderTexture.height), 0, 0);
                                    snapshot.Apply();
                                    RenderTexture.active = null;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"AbxrLib: Error getting pixels directly from WebCamTexture: {ex.Message}. Trying RenderTexture approach...");
                                // Fallback to RenderTexture approach
                                Graphics.Blit(webCamTexture, webCamRenderTexture);
                                RenderTexture.active = webCamRenderTexture;
                                snapshot = new Texture2D(webCamRenderTexture.width, webCamRenderTexture.height, TextureFormat.RGB24, false);
                                snapshot.ReadPixels(new Rect(0, 0, webCamRenderTexture.width, webCamRenderTexture.height), 0, 0);
                                snapshot.Apply();
                                RenderTexture.active = null;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"AbxrLib: WebCamTexture not ready (scan #{scanCount}): width={webCamTexture.width}, height={webCamTexture.height}, isPlaying={webCamTexture.isPlaying}");
                            frameError = true;
                        }
                    }
                    else if (cameraSubsystem != null && passthroughTextureProperty == null)
                    {
                        // Use TryAcquireLatestCpuImage for XRCameraSubsystem
                        try
                        {
                            // Check subsystem status before attempting to acquire image
                            System.Type subsystemType = cameraSubsystem.GetType();
                            System.Reflection.PropertyInfo runningProp = subsystemType.GetProperty("running", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            System.Reflection.PropertyInfo permissionProp = subsystemType.GetProperty("permissionGranted", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            bool isRunning = false;
                            bool hasPermission = false;
                            if (runningProp != null)
                            {
                                isRunning = (bool)runningProp.GetValue(cameraSubsystem);
                            }
                            if (permissionProp != null)
                            {
                                hasPermission = (bool)permissionProp.GetValue(cameraSubsystem);
                            }
                            
                            if (scanCount % 30 == 0) // Log every 30 scans to avoid spam
                            {
                                Debug.Log($"AbxrLib: TryAcquireLatestCpuImage attempt #{scanCount} - Subsystem running: {isRunning}, Permission: {hasPermission}");
                            }
                            
                            // Use reflection to access TryAcquireLatestCpuImage (AR Foundation types not directly available)
                            System.Reflection.MethodInfo tryAcquireMethod = subsystemType.GetMethod("TryAcquireLatestCpuImage", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            if (tryAcquireMethod != null)
                            {
                                // Find XRCpuImage type via reflection
                                System.Type xrCpuImageType = null;
                                foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                                {
                                    xrCpuImageType = assembly.GetType("UnityEngine.XR.ARSubsystems.XRCpuImage");
                                    if (xrCpuImageType != null) break;
                                }
                                
                                if (xrCpuImageType == null)
                                {
                                    Debug.LogWarning($"AbxrLib: Could not find XRCpuImage type (scan #{scanCount})");
                                    frameError = true;
                                }
                                else
                                {
                                    // XRCpuImage is a struct, so we need to use out parameter correctly with reflection
                                    // Create a default instance of the struct
                                    object cpuImageObj = System.Activator.CreateInstance(xrCpuImageType);
                                    
                                    // Call TryAcquireLatestCpuImage using reflection with out parameter
                                    // The out parameter will modify the boxed struct
                                    object[] parameters = new object[] { cpuImageObj };
                                    bool success = (bool)tryAcquireMethod.Invoke(cameraSubsystem, parameters);
                                    
                                    if (scanCount % 30 == 0) // Log every 30 scans
                                    {
                                        Debug.Log($"AbxrLib: TryAcquireLatestCpuImage returned: {success} (scan #{scanCount})");
                                    }
                                    
                                    if (success)
                                    {
                                        // Get the out parameter (the modified struct)
                                        object cpuImage = parameters[0];
                                        
                                        // Get image dimensions via reflection
                                        System.Reflection.PropertyInfo widthProp = xrCpuImageType.GetProperty("width");
                                        System.Reflection.PropertyInfo heightProp = xrCpuImageType.GetProperty("height");
                                        
                                        if (widthProp != null && heightProp != null)
                                        {
                                            int width = (int)widthProp.GetValue(cpuImage);
                                            int height = (int)heightProp.GetValue(cpuImage);
                                            
                                            if (width > 0 && height > 0)
                                            {
                                                // Create ConversionParams via reflection
                                                System.Type conversionParamsType = xrCpuImageType.GetNestedType("ConversionParams");
                                                if (conversionParamsType != null)
                                                {
                                                    object conversionParams = System.Activator.CreateInstance(conversionParamsType);
                                                    
                                                    // Set conversion parameters
                                                    System.Reflection.PropertyInfo inputRectProp = conversionParamsType.GetProperty("inputRect");
                                                    System.Reflection.PropertyInfo outputDimensionsProp = conversionParamsType.GetProperty("outputDimensions");
                                                    System.Reflection.PropertyInfo outputFormatProp = conversionParamsType.GetProperty("outputFormat");
                                                    System.Reflection.PropertyInfo transformationProp = conversionParamsType.GetProperty("transformation");
                                                    
                                                    if (inputRectProp != null) inputRectProp.SetValue(conversionParams, new Rect(0, 0, width, height));
                                                    if (outputDimensionsProp != null) outputDimensionsProp.SetValue(conversionParams, new Vector2Int(width, height));
                                                    if (outputFormatProp != null) outputFormatProp.SetValue(conversionParams, TextureFormat.RGBA32);
                                                    
                                                    // Get Transformation enum type
                                                    System.Type transformationType = xrCpuImageType.GetNestedType("Transformation");
                                                    if (transformationType != null && transformationProp != null)
                                                    {
                                                        // Get None value (typically 0)
                                                        object noneTransformation = System.Enum.ToObject(transformationType, 0);
                                                        transformationProp.SetValue(conversionParams, noneTransformation);
                                                    }
                                                    
                                                    // Call Convert method via reflection
                                                    System.Reflection.MethodInfo convertMethod = xrCpuImageType.GetMethod("Convert", 
                                                        new System.Type[] { conversionParamsType });
                                                    
                                                    if (convertMethod != null)
                                                    {
                                                        NativeArray<byte> convertedData = (NativeArray<byte>)convertMethod.Invoke(cpuImage, new object[] { conversionParams });
                                                        
                                                        if (convertedData.IsCreated && convertedData.Length > 0)
                                                        {
                                                            // Create Texture2D from converted data
                                                            snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
                                                            snapshot.LoadRawTextureData(convertedData);
                                                            snapshot.Apply();
                                                            
                                                            // Dispose the native array
                                                            convertedData.Dispose();
                                                            
                                                            Debug.Log($"AbxrLib: Successfully acquired camera frame via TryAcquireLatestCpuImage: {width}x{height}");
                                                        }
                                                        else
                                                        {
                                                            Debug.LogWarning($"AbxrLib: TryAcquireLatestCpuImage Convert returned empty data (scan #{scanCount})");
                                                            frameError = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Debug.LogWarning($"AbxrLib: Could not find Convert method on XRCpuImage (scan #{scanCount})");
                                                        frameError = true;
                                                    }
                                                }
                                                else
                                                {
                                                    Debug.LogWarning($"AbxrLib: Could not find ConversionParams type (scan #{scanCount})");
                                                    frameError = true;
                                                }
                                                
                                                // Dispose XRCpuImage via reflection
                                                System.Reflection.MethodInfo disposeMethod = xrCpuImageType.GetMethod("Dispose");
                                                if (disposeMethod != null)
                                                {
                                                    disposeMethod.Invoke(cpuImage, null);
                                                }
                                            }
                                            else
                                            {
                                                Debug.LogWarning($"AbxrLib: Invalid XRCpuImage dimensions: {width}x{height} (scan #{scanCount})");
                                                // Dispose XRCpuImage
                                                System.Reflection.MethodInfo disposeMethod = xrCpuImageType.GetMethod("Dispose");
                                                if (disposeMethod != null)
                                                {
                                                    disposeMethod.Invoke(cpuImage, null);
                                                }
                                                frameError = true;
                                            }
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"AbxrLib: Could not get width/height properties from XRCpuImage (scan #{scanCount})");
                                            frameError = true;
                                        }
                                    }
                                    else
                                    {
                                        // TryAcquireLatestCpuImage returned false - no frame available yet
                                        if (scanCount % 30 == 0) // Log every 30 scans
                                        {
                                            Debug.LogWarning($"AbxrLib: TryAcquireLatestCpuImage returned false (scan #{scanCount}) - Subsystem running: {isRunning}, Permission: {hasPermission}");
                                        }
                                        frameError = true;
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"AbxrLib: Could not find TryAcquireLatestCpuImage method on camera subsystem (scan #{scanCount})");
                                frameError = true;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"AbxrLib: Error using TryAcquireLatestCpuImage: {ex.Message}\n{ex.StackTrace}");
                            frameError = true;
                        }
                    }
                    else if ((ovrPassthroughLayer != null || cameraSubsystem != null) && passthroughRenderTexture != null)
                    {
                        // Use Passthrough Camera API with texture property
                        Texture passthroughTex = null;
                        try
                        {
                            object sourceObject = ovrPassthroughLayer != null ? (object)ovrPassthroughLayer : cameraSubsystem;
                            if (passthroughTextureProperty != null && sourceObject != null)
                            {
                                object textureObj = passthroughTextureProperty.GetValue(sourceObject);
                                passthroughTex = textureObj as Texture;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"AbxrLib: Error getting passthrough texture: {ex.Message}\n{ex.StackTrace}");
                            frameError = true;
                        }
                        
                        if (passthroughTex == null)
                        {
                            Debug.LogWarning($"AbxrLib: Passthrough texture is null (scan #{scanCount})");
                            frameError = true;
                        }
                        else
                        {
                            // Copy passthrough texture to RenderTexture
                            Graphics.Blit(passthroughTex, passthroughRenderTexture);
                            
                            // Read from RenderTexture
                            RenderTexture.active = passthroughRenderTexture;
                            snapshot = new Texture2D(passthroughRenderTexture.width, passthroughRenderTexture.height, TextureFormat.RGB24, false);
                            snapshot.ReadPixels(new Rect(0, 0, passthroughRenderTexture.width, passthroughRenderTexture.height), 0, 0);
                            snapshot.Apply();
                            RenderTexture.active = null;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"AbxrLib: No camera source available (scan #{scanCount})");
                        frameError = true;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"AbxrLib: Error capturing camera frame: {ex.Message}\n{ex.StackTrace}");
                    frameError = true;
                }
                
                if (frameError || snapshot == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }
                
                try
                {
                    // Log camera state on first scan
                    if (scanCount == 0)
                    {
                        string cameraInfo = usingWebCamTexture 
                            ? $"WebCamTexture: {webCamTexture.width}x{webCamTexture.height}, isPlaying: {webCamTexture.isPlaying}"
                            : "Passthrough Camera API (Reflection)";
                        Debug.Log($"AbxrLib: Camera state - {cameraInfo}, snapshot: {snapshot.width}x{snapshot.height}");
                        
                        // Sample pixels to verify we have actual image data
                        Color32[] pixels32 = snapshot.GetPixels32();
                        if (pixels32 != null && pixels32.Length > 0)
                        {
                            int sampleCount = Mathf.Min(5, pixels32.Length);
                            string pixelSamples = "";
                            float totalBrightness = 0f;
                            for (int i = 0; i < sampleCount; i++)
                            {
                                int index = (i * pixels32.Length) / sampleCount;
                                Color32 c = pixels32[index];
                                pixelSamples += $"P{i}:({c.r},{c.g},{c.b}) ";
                                totalBrightness += (c.r + c.g + c.b) / 3f / 255f;
                            }
                            float avgBrightness = totalBrightness / sampleCount;
                            Debug.Log($"AbxrLib: Camera pixel samples (first scan): {pixelSamples}, Avg brightness: {avgBrightness:F3}");
                        }
                    }
                    
                    // Decode QR code using ZXing
                    string result = DecodeQRCode(snapshot);
                    
                    scanCount++;
                    if (scanCount % 30 == 0) // Log every 30 scans (every ~3 seconds)
                    {
                        string cameraInfo = usingWebCamTexture 
                            ? $"WebCamTexture: {webCamTexture.width}x{webCamTexture.height}"
                            : "Passthrough Camera API (Reflection)";
                        Debug.Log($"AbxrLib: Scanning... (scan #{scanCount}, {cameraInfo}, texture valid: {snapshot != null})");
                    }
                    
                    // Log when QR codes are detected (even if they don't have ABXR: prefix)
                    if (!string.IsNullOrEmpty(result))
                    {
                        Debug.Log($"AbxrLib: *** QR CODE DETECTED: '{result}' ***");
                        
                        // Only process QR codes that start with "ABXR:"
                        if (result.StartsWith("ABXR:", System.StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"AbxrLib: Valid ABXR QR code found: '{result}'");
                            // Process the QR code result
                            OnQRCodeScanned(result);
                            if (snapshot != null) Destroy(snapshot);
                            yield break; // Stop scanning after successful read
                        }
                        else
                        {
                            // QR code found but doesn't have ABXR: prefix - ignore it and continue scanning
                            Debug.Log($"AbxrLib: QR code detected but doesn't have ABXR: prefix (will ignore): '{result}'");
                        }
                    }
                    else if (scanCount % 60 == 0) // Log every 60 scans (every ~6 seconds) that no QR code found
                    {
                        Debug.Log($"AbxrLib: No QR code detected in scan #{scanCount}. Make sure QR code is in camera view and well-lit.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"AbxrLib: Error in QR scanning loop: {ex.Message}\n{ex.StackTrace}");
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
            
            Debug.Log($"AbxrLib: QR code scanning loop ended. Total scans: {scanCount}");
        }
        
        /// <summary>
        /// Test if a camera source is producing valid (non-black) frames
        /// This is a separate test method that can be called independently of display
        /// </summary>
        /// <param name="texture">The texture to test (WebCamTexture or Texture2D)</param>
        /// <param name="sampleSize">Number of pixels to sample (default 1000)</param>
        /// <param name="threshold">Brightness threshold to consider non-black (0-1, default 0.01)</param>
        /// <returns>True if texture has non-black pixels, false otherwise</returns>
        private bool TestCameraValidity(Texture texture, int sampleSize = 1000, float threshold = 0.01f)
        {
            if (texture == null)
            {
                Debug.LogWarning("AbxrLib: TestCameraValidity - texture is null");
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
                        Debug.LogWarning($"AbxrLib: TestCameraValidity - WebCamTexture not ready: {webCam.width}x{webCam.height}, isPlaying: {webCam.isPlaying}");
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
                        Debug.LogWarning($"AbxrLib: TestCameraValidity - Texture2D invalid size: {tex2d.width}x{tex2d.height}");
                        return false;
                    }
                    pixels = tex2d.GetPixels32();
                    width = tex2d.width;
                    height = tex2d.height;
                }
                else
                {
                    Debug.LogWarning($"AbxrLib: TestCameraValidity - Unsupported texture type: {texture.GetType().Name}");
                    return false;
                }
                
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogWarning($"AbxrLib: TestCameraValidity - No pixel data in texture ({width}x{height})");
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
                
                // Log detailed test results
                Debug.Log($"AbxrLib: Camera Validity Test - Texture: {width}x{height}, " +
                         $"Samples: {actualSampleSize}, Avg brightness: {avgBrightness:F3}, " +
                         $"Non-black pixels: {nonBlackCount}/{actualSampleSize} ({nonBlackPercentage:F1}%), " +
                         $"All pixels same: {allSame}");
                
                // Consider valid if:
                // 1. At least 1% of pixels are non-black, OR
                // 2. Average brightness is above threshold, OR
                // 3. Pixels are not all the same (indicates variation)
                bool isValid = nonBlackPercentage > 1f || avgBrightness > threshold || !allSame;
                
                if (isValid)
                {
                    Debug.Log($"AbxrLib: Camera Validity Test PASSED - Camera is producing valid frames");
                }
                else
                {
                    Debug.LogWarning($"AbxrLib: Camera Validity Test FAILED - Camera appears to be producing black/empty frames");
                }
                
                return isValid;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: TestCameraValidity error: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Decode QR code from texture using ZXing
        /// </summary>
        private string DecodeQRCode(Texture2D texture)
        {
            if (barcodeReader == null)
            {
                Debug.LogWarning("AbxrLib: barcodeReader is null, cannot decode QR code");
                return null;
            }
            
            if (texture == null)
            {
                Debug.LogWarning("AbxrLib: texture is null, cannot decode QR code");
                return null;
            }
            
            try
            {
                // Get pixel data from texture
                Color32[] pixels = texture.GetPixels32();
                
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogWarning($"AbxrLib: No pixel data in texture ({texture.width}x{texture.height})");
                    return null;
                }
                
                // Log pixel statistics on first decode attempt
                decodeAttemptCount++;
                if (decodeAttemptCount == 1)
                {
                    // Calculate average brightness to verify we have actual image data
                    float totalBrightness = 0f;
                    int sampleSize = Mathf.Min(1000, pixels.Length);
                    for (int i = 0; i < sampleSize; i++)
                    {
                        int index = (i * pixels.Length) / sampleSize;
                        Color32 c = pixels[index];
                        totalBrightness += (c.r + c.g + c.b) / 3f / 255f;
                    }
                    float avgBrightness = totalBrightness / sampleSize;
                    
                    // Check if all pixels are the same (likely black/empty)
                    bool allSame = true;
                    Color32 firstPixel = pixels[0];
                    for (int i = 1; i < Mathf.Min(100, pixels.Length); i++)
                    {
                        if (!pixels[i].Equals(firstPixel))
                        {
                            allSame = false;
                            break;
                        }
                    }
                    
                    Debug.Log($"AbxrLib: Decoding attempt #{decodeAttemptCount} - Texture: {texture.width}x{texture.height}, " +
                             $"Pixels: {pixels.Length}, Avg brightness: {avgBrightness:F3}, All pixels same: {allSame}");
                }
                
                // Decode QR code using ZXing
                Result result = barcodeReader.Decode(pixels, texture.width, texture.height);
                
                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    Debug.Log($"AbxrLib: QR code decoded successfully: '{result.Text}' (attempt #{decodeAttemptCount})");
                    return result.Text;
                }
                else if (decodeAttemptCount % 100 == 0) // Log every 100 attempts
                {
                    Debug.Log($"AbxrLib: ZXing decode attempt #{decodeAttemptCount} - No QR code found in frame");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrLib: QR code decoding error: {ex.Message}\n{ex.StackTrace}");
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
        /// Create overlay UI for passthrough mode with camera feed
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
            
            // Set canvas size and position (smaller, more compact overlay)
            RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(0.4f, 0.25f); // 40cm x 25cm in world space
            canvasRect.localScale = Vector3.one;
            
            // Add FaceCamera component to position it in front of user
            FaceCamera faceCamera = overlayCanvas.AddComponent<FaceCamera>();
            faceCamera.faceCamera = true;
            faceCamera.distanceFromCamera = 1.2f; // Slightly further away
            faceCamera.verticalOffset = 0.15f; // Slightly above center
            faceCamera.useConfigurationValues = false;
            
            // Create background panel
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(overlayCanvas.transform, false);
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
            
            // Set texture if available (prefer passthrough, fallback to WebCamTexture)
            if (passthroughRenderTexture != null)
            {
                cameraImage.texture = passthroughRenderTexture;
                cameraImage.uvRect = new Rect(0, 0, 1, 1);
                Debug.Log($"AbxrLib: Passthrough texture assigned to overlay: {passthroughRenderTexture.width}x{passthroughRenderTexture.height}");
            }
            else if (webCamTexture != null)
            {
                cameraImage.texture = webCamTexture;
                cameraImage.uvRect = new Rect(0, 0, 1, 1);
                Debug.Log($"AbxrLib: WebCamTexture assigned to overlay: {webCamTexture.width}x{webCamTexture.height}, isPlaying: {webCamTexture.isPlaying}");
            }
            else
            {
                // Set a placeholder color so we can see the area
                cameraImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                Debug.LogWarning("AbxrLib: No camera texture available when creating overlay. Will update when camera is ready.");
            }
            
            RectTransform cameraRect = cameraDisplay.GetComponent<RectTransform>();
            cameraRect.anchorMin = new Vector2(0.05f, 0.3f);
            cameraRect.anchorMax = new Vector2(0.95f, 0.95f);
            cameraRect.sizeDelta = Vector2.zero;
            cameraRect.anchoredPosition = Vector2.zero;
            
            // Set sorting order to ensure it's visible
            canvas.sortingOrder = 100; // High sorting order to ensure visibility
            
            // Create text label (top portion, over camera feed)
            GameObject label = new GameObject("Label");
            label.transform.SetParent(panel.transform, false);
            Text labelText = label.AddComponent<Text>();
            
            // Show message based on camera method
            if (usingWebCamTexture)
            {
                labelText.text = "Point camera at QR code\nScanning for ABXR: codes...";
            }
            else
            {
                labelText.text = "Scanning QR Code\nPoint camera at QR code";
            }
            
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
            
            Debug.Log($"AbxrLib: Created QR scanning overlay UI for passthrough mode with camera feed. " +
                     $"Canvas active: {overlayCanvas.activeInHierarchy}, Canvas renderMode: {canvas.renderMode}, " +
                     $"Canvas sortingOrder: {canvas.sortingOrder}, CameraDisplay RawImage: {cameraImage != null}, " +
                     $"CameraDisplay texture: {cameraImage.texture != null}");
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
            }
        }
        
        /// <summary>
        /// Get the full hierarchy path of a GameObject (e.g., "Player/Camera Offset/Main Camera")
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return "null";
            
            string path = obj.name;
            Transform current = obj.transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
    }
}
#endif

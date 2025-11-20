/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Pico QR Code Reader
 * 
 * This component handles QR code reading on Pico headsets using PXR_Enterprise SDK.
 * It only activates when:
 * - Running on a Pico headset
 * - PXR_Enterprise class is available
 * - Authentication mechanism type is "assessmentPin"
 * 
 * QR codes should be in the format "ABXR:123456" where 123456 is the 6-digit PIN.
 */

using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Authentication
{
    /// <summary>
    /// QR code reader for Pico headsets using PXR_Enterprise SDK.
    /// Only activates on Pico headsets when assessmentPin authentication is required.
    /// </summary>
    public class PicoQRCodeReader : MonoBehaviour
    {
        private static PicoQRCodeReader _instance;
        private static bool _isInitialized = false;
        private static bool _isScanning = false;
        private static bool _isPXRAvailable = false;
        
        // Reflection references to PXR_Enterprise class
        private static Type _pxrEnterpriseType = null;
        private static MethodInfo _initEnterpriseServiceMethod = null;
        private static MethodInfo _bindEnterpriseServiceMethod = null;
        private static MethodInfo _scanQRCodeMethod = null;
        
        /// <summary>
        /// Check if PXR_Enterprise is available on this device
        /// </summary>
        public static bool IsPXRAvailable()
        {
            if (_isPXRAvailable && _pxrEnterpriseType != null)
            {
                return true;
            }
            
            // Check if we're on Android (Pico headsets run Android)
#if UNITY_ANDROID && !UNITY_EDITOR
            // Try to find PXR_Enterprise class using reflection
            try
            {
                // First try the most common assembly name
                _pxrEnterpriseType = Type.GetType("Unity.XR.PXR.PXR_Enterprise, Assembly-CSharp");
                
                // If not found, search through all loaded assemblies
                if (_pxrEnterpriseType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            _pxrEnterpriseType = assembly.GetType("Unity.XR.PXR.PXR_Enterprise");
                            if (_pxrEnterpriseType != null)
                            {
                                break;
                            }
                        }
                        catch
                        {
                            // Continue searching other assemblies
                            continue;
                        }
                    }
                }
                
                if (_pxrEnterpriseType != null)
                {
                    // Get the methods we need
                    _initEnterpriseServiceMethod = _pxrEnterpriseType.GetMethod("InitEnterpriseService", 
                        BindingFlags.Public | BindingFlags.Static, 
                        null, 
                        new Type[] { typeof(bool) }, 
                        null);
                    
                    _bindEnterpriseServiceMethod = _pxrEnterpriseType.GetMethod("BindEnterpriseService", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    _scanQRCodeMethod = _pxrEnterpriseType.GetMethod("ScanQRCode", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (_initEnterpriseServiceMethod != null && 
                        _bindEnterpriseServiceMethod != null && 
                        _scanQRCodeMethod != null)
                    {
                        _isPXRAvailable = true;
                        Debug.Log("AbxrLib: PXR_Enterprise SDK detected and available");
                        return true;
                    }
                }
            }
            catch
            {
                // Silently fail - PXR_Enterprise not available (expected for non-Pico devices)
            }
#endif
            
            return false;
        }
        
        /// <summary>
        /// Check if we're running on a Pico headset
        /// </summary>
        private static bool IsPicoHeadset()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string deviceModel = DeviceModel.deviceModel?.ToLower() ?? "";
            return deviceModel.Contains("pico") || deviceModel.Contains("neo");
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Initialize QR code reader if Pico headset and PXR_Enterprise are available
        /// </summary>
        public static void InitializeIfAvailable()
        {
            // Only initialize once
            if (_isInitialized)
            {
                return;
            }
            
            // Check if we're on a Pico headset
            if (!IsPicoHeadset())
            {
                return; // Silently skip - not a Pico device
            }
            
            // Check if PXR_Enterprise is available
            if (!IsPXRAvailable())
            {
                return; // Silently skip - PXR_Enterprise not available
            }
            
            // Create the component instance
            GameObject go = new GameObject("PicoQRCodeReader");
            _instance = go.AddComponent<PicoQRCodeReader>();
            DontDestroyOnLoad(go);
            
            _isInitialized = true;
            Debug.Log("AbxrLib: Pico QR code reader initialized");
        }
        
        /// <summary>
        /// Start QR code scanning (only if assessmentPin authentication is active)
        /// </summary>
        public static void StartScanning()
        {
            if (!_isInitialized || _instance == null)
            {
                return;
            }
            
            if (_isScanning)
            {
                return; // Already scanning
            }
            
            _instance.StartCoroutine(_instance.CreateQRReader());
        }
        
        /// <summary>
        /// Stop QR code scanning
        /// </summary>
        public static void StopScanning()
        {
            _isScanning = false;
        }
        
        private void Start()
        {
            // Component is created via InitializeIfAvailable, don't auto-start here
            // Subscribe to keyboard events to stop scanning when keyboard is destroyed
            KeyboardHandler.OnKeyboardDestroyed += OnKeyboardDestroyed;
        }
        
        private void OnKeyboardDestroyed()
        {
            // Stop scanning when keyboard is destroyed (user may have manually entered PIN)
            StopScanning();
        }
        
        private IEnumerator CreateQRReader()
        {
            if (!IsPXRAvailable())
            {
                yield break;
            }
            
            // Initialize PXR Enterprise Service
            bool initSuccess = false;
            try
            {
                _initEnterpriseServiceMethod?.Invoke(null, new object[] { true });
                initSuccess = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Failed to initialize PXR Enterprise Service: {ex.Message}");
                _isScanning = false;
                yield break;
            }
            
            if (!initSuccess)
            {
                yield break;
            }
            
            // Wait a frame to ensure initialization completes
            yield return null;
            
            // Bind Enterprise Service with callback
            // The callback signature is: void BindEnterpriseService(Action<int> callback)
            try
            {
                if (_bindEnterpriseServiceMethod != null)
                {
                    // Create callback delegate using Action<int>
                    Action<int> callback = OnEnterpriseServiceBound;
                    
                    _bindEnterpriseServiceMethod.Invoke(null, new object[] { callback });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Failed to bind PXR Enterprise Service: {ex.Message}");
                _isScanning = false;
            }
        }
        
        /// <summary>
        /// Callback when Enterprise Service is bound
        /// </summary>
        private void OnEnterpriseServiceBound(int result)
        {
            Debug.Log($"AbxrLib: PXR Enterprise Service bind result: {result}");
            
            if (result == 0) // Success
            {
                StartCoroutine(ScanCode());
            }
            else
            {
                Debug.LogWarning($"AbxrLib: PXR Enterprise Service bind failed with result: {result}");
                _isScanning = false;
            }
        }
        
        private IEnumerator ScanCode()
        {
            if (!IsPXRAvailable() || _scanQRCodeMethod == null)
            {
                yield break;
            }
            
            _isScanning = true;
            
            try
            {
                // Create callback delegate for QR code scan result
                // The callback signature is: void ScanQRCode(Action<string> callback)
                Action<string> callback = OnQRCodeScanned;
                
                _scanQRCodeMethod.Invoke(null, new object[] { callback });
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib: Failed to start QR code scanning: {ex.Message}");
                _isScanning = false;
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Callback when QR code is scanned
        /// </summary>
        private void OnQRCodeScanned(string scanResult)
        {
            _isScanning = false;
            
            if (scanResult == null)
            {
                Debug.Log("AbxrLib: QR code scanning failed (null result)");
                // Restart scanning after a delay
                StartCoroutine(RestartScanningAfterDelay());
                return;
            }
            
            if (scanResult == "-2")
            {
                Debug.Log("AbxrLib: QR code scanning is not supported by this device");
                return; // Don't retry if not supported
            }
            
            Debug.Log($"AbxrLib: QR code scanned successfully: {scanResult}");
            
            // Extract PIN from QR code format "ABXR:123456"
            Match match = Regex.Match(scanResult, @"(?<=ABXR:)\d+");
            
            if (match.Success)
            {
                string pin = match.Value;
                Debug.Log($"AbxrLib: Extracted PIN from QR code: {pin}");
                
                // Attempt authentication with the PIN
                StartCoroutine(AuthenticateWithPIN(pin));
            }
            else
            {
                Debug.LogWarning($"AbxrLib: Invalid QR code format (expected ABXR:XXXXXX): {scanResult}");
                // Restart scanning after a delay to allow user to scan another code
                StartCoroutine(RestartScanningAfterDelay());
            }
        }
        
        /// <summary>
        /// Authenticate with the PIN extracted from QR code
        /// </summary>
        private IEnumerator AuthenticateWithPIN(string pin)
        {
            yield return Authentication.KeyboardAuthenticate(pin);
            
            // Check if authentication was successful
            // If KeyboardAuthenticate succeeds, it will call NotifyAuthCompleted and we're done
            // If it fails, KeyboardAuthenticate will show the keyboard again and increment failed attempts
            // We should restart scanning in case the user wants to try another QR code
            if (!Authentication.FullyAuthenticated())
            {
                Debug.LogWarning("AbxrLib: QR code PIN authentication failed, showing keyboard for manual entry");
                // Restart scanning after a delay to allow user to scan another code or use keyboard
                StartCoroutine(RestartScanningAfterDelay());
            }
            else
            {
                Debug.Log("AbxrLib: QR code PIN authentication successful");
                // Stop scanning since authentication succeeded
                _isScanning = false;
            }
        }
        
        /// <summary>
        /// Restart scanning after a delay
        /// </summary>
        private IEnumerator RestartScanningAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);
            
            // Only restart if we're still in assessmentPin mode and not fully authenticated
            if (!Authentication.FullyAuthenticated() && _isInitialized)
            {
                StartScanning();
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from keyboard events
            KeyboardHandler.OnKeyboardDestroyed -= OnKeyboardDestroyed;
            
            _isScanning = false;
            _isInitialized = false;
            _instance = null;
        }
    }
}


using System.Linq;
using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using TMPro;
using UnityEngine;

namespace AbxrLib.Runtime.UI.Keyboard
{
    public class KeyboardHandler : MonoBehaviour
    {
        public static event Action OnKeyboardCreated;
        public static event Action OnKeyboardDestroyed;
    
        public enum KeyboardType
        {
            PinPad,
            FullKeyboard
        }
    
        private static GameObject _keyboardPrefab;
        private static GameObject _pinPadPrefab;
        private static GameObject _keyboardInstance;
        private static GameObject _pinPadInstance;
    
        private const string ProcessingText = "Processing";
        private static bool _processingSubmit;

        private static TextMeshProUGUI _prompt;
       
        private void Start()
        {
            LoadPrefabs();
        }

        /// <summary>
        /// Load keyboard and PIN pad prefabs from Configuration or Resources.
        /// Safe to call from static code (e.g. before Start); used on first Create() if needed.
        /// </summary>
        private static void LoadPrefabs()
        {
            var config = Configuration.Instance;
            
            // Try to use configuration prefabs first, fall back to Resources.Load
            _keyboardPrefab = config != null ? config.KeyboardPrefab : null;
            _pinPadPrefab = config != null ? config.PinPrefab : null;
            
            if (!_keyboardPrefab)
            {
                _keyboardPrefab = Resources.Load<GameObject>("Prefabs/AbxrKeyboard" + RigDetector.PrefabSuffix());
            }
            else
            {
                Debug.Log("[AbxrLib] KeyboardHandler - Using custom keyboard prefab from configuration");
            }
            
            if (!_pinPadPrefab)
            {
                _pinPadPrefab = Resources.Load<GameObject>("Prefabs/AbxrPinPad" + RigDetector.PrefabSuffix());
            }
            else
            {
                Debug.Log("[AbxrLib] KeyboardHandler - Using custom PIN pad prefab from configuration");
            }
            
            if (!_keyboardPrefab)
            {
                Debug.LogError("[AbxrLib] KeyboardHandler - Failed to load keyboard prefab from both configuration and Resources. Assign Keyboard Prefab in AbxrLib Configuration or ensure package Resources are available.");
            }
        }
    
        public static void Destroy()
        {
            if (_keyboardInstance)
            {
                Destroy(_keyboardInstance);
            }

            if (_pinPadInstance)
            {
                Destroy(_pinPadInstance);
            }
            
            // Restore laser pointer states to their original configuration
            LaserPointerManager.RestoreLaserPointerStates();
        
            OnKeyboardDestroyed?.Invoke();
        }
        
        /// <summary>
        /// Reload prefabs from configuration. Useful when configuration changes at runtime.
        /// </summary>
        public static void RefreshPrefabs()
        {
            LoadPrefabs();
            Debug.Log("[AbxrLib] KeyboardHandler - Prefabs refreshed from configuration");
        }

        public static void SetPrompt(string prompt)
        {
            if (_prompt != null)
            {
                _prompt.text = prompt;
            }
        }

        public static void Create(KeyboardType keyboardType)
        {
            _processingSubmit = false;

            // Ensure prefabs are loaded (handles Create() being called before KeyboardHandler.Start())
            if (_keyboardPrefab == null || _pinPadPrefab == null)
                LoadPrefabs();
            
            if (keyboardType == KeyboardType.PinPad)
            {
                if (_pinPadPrefab == null)
                {
                    Debug.LogError("[AbxrLib] KeyboardHandler - Cannot show PIN pad: prefab not found. Assign Pin Prefab in AbxrLib Configuration or ensure package Resources are available.");
                    return;
                }
                if (_pinPadInstance) return; // Prevent duplicate PIN pad creation
                _pinPadInstance = Instantiate(_pinPadPrefab);
                
                // Ensure PIN pad FaceCamera uses configuration values
                var pinPadFaceCamera = _pinPadInstance.GetComponent<FaceCamera>();
                if (pinPadFaceCamera != null)
                {
                    pinPadFaceCamera.useConfigurationValues = true;
                }
                _prompt = _pinPadInstance.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.name == "DynamicMessage");
                LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_pinPadInstance);
            }
            else if (keyboardType == KeyboardType.FullKeyboard)
            {
                if (_keyboardPrefab == null)
                {
                    Debug.LogError("[AbxrLib] KeyboardHandler - Cannot show keyboard: prefab not found. Assign Keyboard Prefab in AbxrLib Configuration or ensure package Resources are available.");
                    return;
                }
                if( _keyboardInstance) return; // Prevent duplicate full keyboard creation
                _keyboardInstance = Instantiate(_keyboardPrefab);
                
                // Ensure FaceCamera uses configuration values
                var faceCamera = _keyboardInstance.GetComponent<FaceCamera>();
                if (faceCamera != null)
                {
                    faceCamera.useConfigurationValues = true;
                }
                    
                _prompt = _keyboardInstance.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.name == "DynamicMessage");
                LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_keyboardInstance);
            }
        
            // Enable laser pointers for keyboard/PIN pad interaction
            LaserPointerManager.EnableLaserPointersForInteraction();
            
            OnKeyboardCreated?.Invoke();
        }
    
      
    
        public static IEnumerator ProcessingVisual()
        {
            _processingSubmit = true;
            SetPrompt(ProcessingText);
            while (_processingSubmit)
            {
                string currentText = _prompt.text;
                _prompt.text = currentText.Length > ProcessingText.Length + 10 ? ProcessingText : $":{_prompt.text}:";
                yield return new WaitForSeconds(0.5f); // Wait before running again
            }
        }

    }
}
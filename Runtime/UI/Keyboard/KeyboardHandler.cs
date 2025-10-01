using System.Linq;
using System;
using System.Collections;
using AbxrLib.Runtime.Common;
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
        private static GameObject _panelPrefab;
        private static GameObject _keyboardInstance;
        private static GameObject _pinPadInstance;
        private static GameObject _panelInstance;
    
        private const string ProcessingText = "Processing";
        private static bool _processingSubmit;

        private static TextMeshProUGUI _prompt;
       
        private void Start()
        {
            LoadPrefabs();
        }
        
        private void LoadPrefabs()
        {
            var config = Configuration.Instance;
            
            // Try to use configuration prefabs first, fall back to Resources.Load
            _keyboardPrefab = config.KeyboardPrefab;
            _pinPadPrefab = config.PinPrefab;
            _panelPrefab = config.PanelPrefab;
            
            // Fall back to Resources.Load if configuration prefabs are not set
            if (!_keyboardPrefab)
            {
                _keyboardPrefab = Resources.Load<GameObject>("Prefabs/AbxrKeyboard" + RigDetector.PrefabSuffix());
                if (_keyboardPrefab)
                {
                    Debug.Log("AbxrLib - KeyboardHandler: Using default keyboard prefab from Resources");
                }
            }
            else
            {
                Debug.Log("AbxrLib - KeyboardHandler: Using custom keyboard prefab from configuration");
            }
            
            if (!_pinPadPrefab)
            {
                _pinPadPrefab = Resources.Load<GameObject>("Prefabs/AbxrPinPad" + RigDetector.PrefabSuffix());
                if (_pinPadPrefab)
                {
                    Debug.Log("AbxrLib - KeyboardHandler: Using default PIN pad prefab from Resources");
                }
            }
            else
            {
                Debug.Log("AbxrLib - KeyboardHandler: Using custom PIN pad prefab from configuration");
            }
            
            if (!_panelPrefab)
            {
                _panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
                if (_panelPrefab)
                {
                    Debug.Log("AbxrLib - KeyboardHandler: Using default panel prefab from Resources");
                }
            }
            else
            {
                Debug.Log("AbxrLib - KeyboardHandler: Using custom panel prefab from configuration");
            }
            
            if (!_keyboardPrefab)
            {
                Debug.LogError("AbxrLib - KeyboardHandler: Failed to load keyboard prefab from both configuration and Resources");
            }
        }
    
        public static void Destroy()
        {
            if (_keyboardInstance)
            {
                Destroy(_keyboardInstance);
            }

            if (_panelInstance)
            {
                Destroy(_panelInstance);
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
            var keyboardHandler = FindObjectOfType<KeyboardHandler>();
            if (keyboardHandler != null)
            {
                keyboardHandler.LoadPrefabs();
                Debug.Log("AbxrLib - KeyboardHandler: Prefabs refreshed from configuration");
            }
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
            
            if (keyboardType == KeyboardType.PinPad)
            {
                if (_pinPadInstance) return; // Prevent duplicate PIN pad creation
                _pinPadInstance = Instantiate(_pinPadPrefab);
                _prompt = _pinPadInstance.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.name == "DynamicMessage");
                
            }
            else if (keyboardType == KeyboardType.FullKeyboard)
            {
                if( _keyboardInstance) return; // Prevent duplicate full keyboard creation
                _keyboardInstance = Instantiate(_keyboardPrefab);
                
                if(_panelInstance) return; // Prevent duplicate text prompt panel creation
                _panelInstance = Instantiate(_panelPrefab);
                    
                _prompt = _panelInstance.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.name == "DynamicMessage");
                
                // Center the panel below the keyboard
                CenterPanelBelowKeyboard();
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

        private static void CenterPanelBelowKeyboard()
        {
            if (_keyboardInstance == null || _panelInstance == null) return;

            // Get the RectTransform components
            RectTransform keyboardRect = _keyboardInstance.GetComponent<RectTransform>();
            RectTransform panelRect = _panelInstance.GetComponentInChildren<RectTransform>();

            if (keyboardRect == null || panelRect == null) return;

            // Get the canvas to work with screen dimensions
            Canvas canvas = keyboardRect.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Get canvas size
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect.sizeDelta;

            // Get keyboard's current position
            float keyboardX = keyboardRect.anchoredPosition.x;
            float keyboardY = keyboardRect.anchoredPosition.y;

            // Position panel above the keyboard with proper spacing
            float spacing = 2f; // Fixed spacing in design units for consistent behavior across devices
            float panelY = keyboardY + spacing;

            // Add horizontal offset to align panel's visual center with keyboard's visual center
            // Use fixed pixel offset instead of percentage for consistent behavior across devices
            float panelX = keyboardX - 10f; // 10 design units to the right (adjust as needed)
            
            // Apply additional left offset for Unity editor player (not VR)
            if (Application.isEditor && RigDetector.PrefabSuffix() == "_Default")
            {
                panelX += 8f; // Move 8f to the right for Unity editor player
            }

            // Set panel anchors to match keyboard's anchor system
            panelRect.anchorMin = keyboardRect.anchorMin;
            panelRect.anchorMax = keyboardRect.anchorMax;
            panelRect.anchoredPosition = new Vector2(panelX, panelY);
        }
    }
}
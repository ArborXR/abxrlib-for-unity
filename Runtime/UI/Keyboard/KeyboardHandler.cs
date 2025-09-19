using System.Linq;
using System;
using System.Collections;
using AbxrLib.Runtime.Common;
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
            _keyboardPrefab = Resources.Load<GameObject>("Prefabs/AbxrKeyboard" + RigDetector.PrefabSuffix());
            _pinPadPrefab = Resources.Load<GameObject>("Prefabs/AbxrPinPad" + RigDetector.PrefabSuffix());
            _panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
            if (!_keyboardPrefab)
            {
                Debug.LogError("AbxrLib - Failed to load keyboard prefab");
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
        
            OnKeyboardDestroyed?.Invoke();
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
            }
        
            
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
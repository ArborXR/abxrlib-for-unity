using System.Linq;
using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        
        // Blocks all pointer-down input for a short window after the UI appears,
        // preventing accidental button presses caused by the same gesture that opened the panel.
        private const float InputGuardDuration = 0.5f;
        private static float _inputUnlockTime;

        /// <summary>Returns true when pointer-down input is currently blocked by the opening guard.</summary>
        public static bool IsInputGuarded => Time.unscaledTime < _inputUnlockTime;

        /// <summary>Starts the half-second input guard. Call immediately after showing any UI panel.</summary>
        public static void StartInputGuard()
        {
            _inputUnlockTime = Time.unscaledTime + InputGuardDuration;
        }
       
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
            _keyboardPrefab = config?.KeyboardPrefab;
            _pinPadPrefab = config?.PinPrefab;
            
            if (!_keyboardPrefab)
            {
                _keyboardPrefab = Resources.Load<GameObject>("Prefabs/AbxrKeyboard" + RigDetector.PrefabSuffix());
            }
            else
            {
                Logcat.Info("KeyboardHandler - Using custom keyboard prefab from configuration");
            }
            
            if (!_pinPadPrefab)
            {
                _pinPadPrefab = Resources.Load<GameObject>("Prefabs/AbxrPinPad" + RigDetector.PrefabSuffix());
            }
            else
            {
                Logcat.Info("KeyboardHandler - Using custom PIN pad prefab from configuration");
            }
            
            if (!_keyboardPrefab)
            {
                Logcat.Error("KeyboardHandler - Failed to load keyboard prefab from both configuration and Resources. Assign Keyboard Prefab in AbxrLib Configuration or ensure package Resources are available.");
            }
        }
    
        public static void Destroy()
        {
            if (_keyboardInstance) Destroy(_keyboardInstance);
            if (_pinPadInstance) Destroy(_pinPadInstance);
            IsPairing = false;
            _skipLabel = null;
            _deviceNameStep = DeviceNameStep.None;
            
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
            Logcat.Debug("KeyboardHandler - Prefabs refreshed from configuration");
        }

        public static void SetPrompt(string prompt)
        {
            if (_prompt != null) _prompt.text = prompt;
        }

        // ── Passcode pairing ─────────────────────────────────────────

        private enum DeviceNameStep { None, Skippable, Required }
        private static DeviceNameStep _deviceNameStep;

        /// <summary>True while the PIN pad shows the pairing passcode, so the QR button stays hidden.</summary>
        public static bool IsPairing { get; private set; }

        /// <summary>
        /// Sets the prompt for a new step. On a skippable name step with no skip button on the keyboard, it says how
        /// to skip: submitting the name blank pairs without one.
        /// </summary>
        public static void SetStepPrompt(string prompt)
        {
            bool hasSkipButton = _keyboardInstance != null && SkipButton(_keyboardInstance) != null;
            SetPrompt(_deviceNameStep == DeviceNameStep.Skippable && !hasSkipButton ? $"{prompt}\nLeave it blank to skip." : prompt);
        }

        /// <summary>
        /// Switches the PIN pad between pairing and sign-in. While pairing, its skip button reads "Not now" and shows
        /// unless <see cref="Configuration.enablePairingDismiss"/> is off, and the QR button is hidden.
        /// </summary>
        public static void SetPairingMode(bool pairing)
        {
            IsPairing = pairing;
            if (_pinPadInstance == null) return;
            var manager = _pinPadInstance.GetComponentInChildren<KeyboardManager>(true);
            if (manager == null) return;

            if (manager.skipButton != null)
            {
                var label = manager.skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    if (pairing && _skipLabel == null) _skipLabel = label.text;
                    if (pairing) label.text = NotNowLabel;
                    else if (_skipLabel != null) label.text = _skipLabel;
                }
                var config = Configuration.Instance;
                manager.skipButton.gameObject.SetActive(pairing ? config == null || config.enablePairingDismiss : true);
                if (!pairing) ApplyPinPadGuestAccessSetting(_pinPadInstance);
            }

            if (pairing && manager.qrCodeButton != null) manager.qrCodeButton.gameObject.SetActive(false);
            else if (!pairing) KeyboardManager.RefreshQrButtonAvailability();
        }

        /// <summary>Marks the keyboard as the name step. A custom keyboard with a skip button shows it only when the name is optional.</summary>
        public static void SetDeviceNameMode(bool skippable)
        {
            _deviceNameStep = skippable ? DeviceNameStep.Skippable : DeviceNameStep.Required;
            Button skip = _keyboardInstance != null ? SkipButton(_keyboardInstance) : null;
            if (skip != null) skip.gameObject.SetActive(skippable);
        }

        /// <summary>Removes the PIN pad only, when pairing moves on to the keyboard.</summary>
        public static void DestroyPinPad()
        {
            if (_pinPadInstance) Destroy(_pinPadInstance);
            _pinPadInstance = null;
            IsPairing = false;
            _skipLabel = null;
        }

        /// <summary>Removes the keyboard only, when pairing goes back to the passcode.</summary>
        public static void DestroyKeyboard()
        {
            if (_keyboardInstance) Destroy(_keyboardInstance);
            _keyboardInstance = null;
            _deviceNameStep = DeviceNameStep.None;
        }

        private const string NotNowLabel = "Not now";
        private static string _skipLabel;

        private static Button SkipButton(GameObject root) => root.GetComponentInChildren<KeyboardManager>(true)?.skipButton;

        public static bool IsPinPadVisible() => _pinPadInstance != null && _pinPadInstance.activeSelf;

        public static void HidePinPad()
        {
            if (_pinPadInstance != null) _pinPadInstance.SetActive(false);
        }

        public static void ShowPinPad()
        {
            if (_pinPadInstance != null)
            {
                _pinPadInstance.SetActive(true);
                LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_pinPadInstance);
                LaserPointerManager.EnableLaserPointersForInteraction();
                StartInputGuard();  // Guard against accidental input
            }
        }

        /// <summary>Stops the Processing animation so the prompt can show an error or new message (e.g. after auth failure).</summary>
        public static void StopProcessing() => _processingSubmit = false;

        public static void Create(KeyboardType keyboardType)
        {
            _processingSubmit = false;

            // Ensure prefabs are loaded (handles Create() being called before KeyboardHandler.Start())
            if (_keyboardPrefab == null || _pinPadPrefab == null) LoadPrefabs();
            
            if (keyboardType == KeyboardType.PinPad)
            {
                if (_pinPadPrefab == null)
                {
                    Logcat.Error("KeyboardHandler - Cannot show PIN pad: prefab not found. Assign Pin Prefab in AbxrLib Configuration or ensure package Resources are available.");
                    return;
                }
                if (_pinPadInstance) return; // Prevent duplicate PIN pad creation
                _pinPadInstance = Instantiate(_pinPadPrefab);
                
                // Ensure PIN pad FaceCamera uses configuration values
                var pinPadFaceCamera = _pinPadInstance.GetComponent<FaceCamera>();
                if (pinPadFaceCamera != null) pinPadFaceCamera.useConfigurationValues = true;
                _prompt = _pinPadInstance.GetComponentsInChildren<TextMeshProUGUI>()
                    .FirstOrDefault(t => t.name == "DynamicMessage");
                ApplyPinPadGuestAccessSetting(_pinPadInstance);
                LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_pinPadInstance);
            }
            else if (keyboardType == KeyboardType.FullKeyboard)
            {
                if (_keyboardPrefab == null)
                {
                    Logcat.Error("KeyboardHandler - Cannot show keyboard: prefab not found. Assign Keyboard Prefab in AbxrLib Configuration or ensure package Resources are available.");
                    return;
                }
                if( _keyboardInstance) return; // Prevent duplicate full keyboard creation
                _keyboardInstance = Instantiate(_keyboardPrefab);
                
                // Ensure FaceCamera uses configuration values
                var faceCamera = _keyboardInstance.GetComponent<FaceCamera>();
                if (faceCamera != null) faceCamera.useConfigurationValues = true;
                    
                _prompt = _keyboardInstance.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "DynamicMessage");
                // PanelCanvas is a sibling of KeyboardCanvas, placed in front in local Z; its Images and
                // DynamicMessage TMP (raycastTarget on) otherwise win XR ray hits before the key canvas.
                DisableRaycastOnKeyboardPanelChrome(_keyboardInstance);
                LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_keyboardInstance);
            }
        
            // Enable laser pointers for keyboard/PIN pad interaction
            LaserPointerManager.EnableLaserPointersForInteraction();

            // Block input briefly so the same gesture that opened the UI can't accidentally activate a button the moment it appears
            StartInputGuard();
            
            OnKeyboardCreated?.Invoke();
        }
    
      
    
        /// <summary>
        /// Hides Guest Access when <see cref="Configuration.enablePinPadGuestAccess"/> is false.
        /// Works for default and custom PIN prefabs that assign <see cref="KeyboardManager.skipButton"/>.
        /// </summary>
        private static void ApplyPinPadGuestAccessSetting(GameObject pinPadRoot)
        {
            if (pinPadRoot == null) return;
            var config = Configuration.Instance;
            if (config == null || config.enablePinPadGuestAccess) return;
            var manager = pinPadRoot.GetComponentInChildren<KeyboardManager>(true);
            if (manager == null || manager.skipButton == null) return;
            manager.skipButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// AbxrKeyboard root has two world-space canvases: PanelCanvas (branding, DynamicMessage prompt)
        /// and KeyboardCanvas (keys). PanelCanvas is offset in local Z in front of the key canvas; decorative
        /// Graphics there must not raycast or controller rays hit chrome instead of keys (often the top row).
        /// </summary>
        private static void DisableRaycastOnKeyboardPanelChrome(GameObject keyboardRoot)
        {
            if (keyboardRoot == null) return;
            var panel = keyboardRoot.transform.Find("PanelCanvas");
            if (panel == null) return;
            foreach (var g in panel.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || !g.raycastTarget) continue;
                g.raycastTarget = false;
            }
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
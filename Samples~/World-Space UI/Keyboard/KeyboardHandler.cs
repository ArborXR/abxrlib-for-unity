using System.Linq;
using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Pairing;
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

        /// <summary>True while a keyboard or PIN pad is open.</summary>
        public static bool IsOpen => _keyboardInstance || _pinPadInstance;

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
            _processingSubmit = false; // ProcessingVisual can still tick once this frame, before the instance goes
            if (_keyboardInstance) Destroy(_keyboardInstance);
            if (_pinPadInstance) Destroy(_pinPadInstance);
            // Destroy waits for the end of the frame, so a prompt opened again in this frame must not find the old one.
            _keyboardInstance = null;
            _pinPadInstance = null;
            _prompt = null;
            ResetPairingState();
            
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
        private enum PinPadView { Keypad, Gate, Confirm }

        private static DeviceNameStep _deviceNameStep;
        /// <summary>The keyboard has no confirm panel, so the person types the existing name to join it.</summary>
        private static bool _joinByTyping;
        /// <summary>Set once the person leaves the gate for the keypad, so a failed passcode asks again on the keypad.</summary>
        private static bool _gatePassed;

        private const string NotNowLabel = "Not now";
        private const string BackLabel = "Back";
        private static string _skipLabel;

        /// <summary>True while the PIN pad shows a pairing step, so the QR button stays hidden.</summary>
        public static bool IsPairing { get; private set; }

        /// <summary>
        /// Sets the prompt for a new step. On a skippable name step with no skip button, it says that a blank name skips;
        /// when the join step falls back to the keyboard, it says to type the name.
        /// </summary>
        public static void SetStepPrompt(string prompt)
        {
            bool hasSkipButton = _keyboardInstance != null && SkipButton(_keyboardInstance) != null;
            if (_joinByTyping)
                prompt = $"{prompt}\nSubmit {Abxr.GetLastPairingRedeemResult().DeviceName} to add it, or type a different name.";
            else if (_deviceNameStep == DeviceNameStep.Skippable && !hasSkipButton)
                prompt = $"{prompt}\nLeave it blank to skip.";
            SetPrompt(prompt);
        }

        /// <summary>
        /// Switches the PIN pad between pairing and sign-in. While pairing the QR button is hidden, and the keypad's skip
        /// button goes back to the gate. A PIN pad without a gate keeps it as "Not now", shown unless
        /// <see cref="Configuration.enablePairingDismiss"/> is off.
        /// </summary>
        public static void SetPairingMode(bool pairing)
        {
            IsPairing = pairing;
            KeyboardManager manager = PinPadManager();
            if (manager == null) return;
            // A passcode is exactly six digits, so the keypad stops there. KeyboardKey checks the limit, since TMP doesn't
            // apply it when text is set from code.
            if (manager.inputField != null) manager.inputField.characterLimit = pairing ? PairingOutcomes.PasscodeLength : 0;

            bool dismissAllowed = Configuration.Instance == null || Configuration.Instance.enablePairingDismiss;
            if (manager.skipButton != null)
            {
                var label = manager.skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    if (pairing && _skipLabel == null) _skipLabel = label.text;
                    if (pairing) label.text = manager.pairingGate != null ? BackLabel : NotNowLabel;
                    else if (_skipLabel != null) label.text = _skipLabel;
                }
                manager.skipButton.gameObject.SetActive(!pairing || manager.pairingGate != null || dismissAllowed);
                if (!pairing) ApplyPinPadGuestAccessSetting(_pinPadInstance);
            }
            if (manager.gateNotNowButton != null) manager.gateNotNowButton.gameObject.SetActive(dismissAllowed);

            if (pairing && manager.qrCodeButton != null) manager.qrCodeButton.gameObject.SetActive(false);
            else if (!pairing)
            {
                ShowView(manager, PinPadView.Keypad);
                KeyboardManager.RefreshQrButtonAvailability();
            }
        }

        /// <summary>The passcode step: the gate the first time, then the keypad, including after a failed passcode.</summary>
        public static void ShowPairingPasscode()
        {
            KeyboardManager manager = PinPadManager();
            if (manager == null) return;
            ShowView(manager, manager.pairingGate != null && !_gatePassed ? PinPadView.Gate : PinPadView.Keypad);
        }

        /// <summary>The gate's "Enter Pairing Passcode" button.</summary>
        public static void LeavePairingGate()
        {
            _gatePassed = true;
            KeyboardManager manager = PinPadManager();
            if (manager != null) ShowView(manager, PinPadView.Keypad);
            StartInputGuard();
        }

        /// <summary>The keypad's Back button while pairing. False when this PIN pad has no gate, so the button means "Not now".</summary>
        public static bool ReturnToPairingGate()
        {
            KeyboardManager manager = PinPadManager();
            if (manager == null || manager.pairingGate == null) return false;
            _gatePassed = false;
            ShowView(manager, PinPadView.Gate);
            StartInputGuard();
            return true;
        }

        /// <summary>
        /// The join step: the PIN pad's confirm panel, labelled with the existing headset's name. False when the PIN
        /// pad has none, so the caller falls back to typing the name on the keyboard.
        /// </summary>
        public static bool ShowPairingConfirm()
        {
            KeyboardManager manager = PinPadManager();
            if (manager == null || manager.pairingConfirm == null) return false;
            string name = Abxr.GetLastPairingRedeemResult().DeviceName;
            if (manager.confirmJoinLabel != null) manager.confirmJoinLabel.text = $"Add to {name}";
            ShowView(manager, PinPadView.Confirm);
            return true;
        }

        /// <summary>Marks the keyboard as the name step. A custom keyboard with a skip button shows it only when the name is optional.</summary>
        public static void SetDeviceNameMode(bool skippable, bool joinByTyping = false)
        {
            _deviceNameStep = skippable ? DeviceNameStep.Skippable : DeviceNameStep.Required;
            _joinByTyping = joinByTyping;
            Button skip = _keyboardInstance != null ? SkipButton(_keyboardInstance) : null;
            if (skip != null) skip.gameObject.SetActive(skippable);
        }

        /// <summary>Removes the PIN pad only, when pairing moves on to the keyboard.</summary>
        public static void DestroyPinPad()
        {
            if (_pinPadInstance) Destroy(_pinPadInstance);
            _pinPadInstance = null;
            _processingSubmit = false;
            IsPairing = false;
            _skipLabel = null;
        }

        /// <summary>Removes the keyboard only, when pairing goes back to the PIN pad.</summary>
        public static void DestroyKeyboard()
        {
            if (_keyboardInstance) Destroy(_keyboardInstance);
            _keyboardInstance = null;
            _processingSubmit = false;
            _deviceNameStep = DeviceNameStep.None;
            _joinByTyping = false;
        }

        /// <summary>Clears what an Editor play session left behind when domain reload is off: the prompt objects died with it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _keyboardInstance = null;
            _pinPadInstance = null;
            _prompt = null;
            _processingSubmit = false;
            _inputUnlockTime = 0;
            ResetPairingState();
            LaserPointerManager.ForceCleanup();
            KeyboardManager.Instance = null;
        }

        private static void ResetPairingState()
        {
            IsPairing = false;
            _skipLabel = null;
            _gatePassed = false;
            _deviceNameStep = DeviceNameStep.None;
            _joinByTyping = false;
        }

        /// <summary>Shows one of the PIN pad's panels. The prompt follows the panel, since the confirm has its own title.</summary>
        private static void ShowView(KeyboardManager manager, PinPadView view)
        {
            if (manager.keypadGroup != null) manager.keypadGroup.SetActive(view == PinPadView.Keypad);
            if (manager.pairingGate != null) manager.pairingGate.SetActive(view == PinPadView.Gate);
            if (manager.pairingConfirm != null) manager.pairingConfirm.SetActive(view == PinPadView.Confirm);
            _prompt = view == PinPadView.Confirm && manager.confirmTitle != null
                ? manager.confirmTitle
                : _pinPadInstance.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "DynamicMessage");
        }

        private static KeyboardManager PinPadManager() =>
            _pinPadInstance != null ? _pinPadInstance.GetComponentInChildren<KeyboardManager>(true) : null;

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
                // An open prompt must outlive a scene load: the SDK still waits on its input, and only KeyboardHandler's
                // Destroy methods close it.
                DontDestroyOnLoad(_pinPadInstance);
                
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
                DontDestroyOnLoad(_keyboardInstance); // Outlives a scene load, like the PIN pad.
                
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
            while (_processingSubmit && _prompt != null)
            {
                string currentText = _prompt.text;
                _prompt.text = currentText.Length > ProcessingText.Length + 10 ? ProcessingText : $":{_prompt.text}:";
                yield return new WaitForSeconds(0.5f); // Wait before running again
            }
        }
    }
}
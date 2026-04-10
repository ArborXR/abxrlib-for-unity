using TMPro;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.Keyboard
{
    public class KeyboardManager : MonoBehaviour
    {
        public static KeyboardManager Instance;
        public static AbxrAuthService AuthService;
        public Button shiftButton1;
        public Button shiftButton2;
        public Button deleteButton;
        public Button spaceButton;
        public Button submitButton;
        public Button qrCodeButton;
        public Button skipButton;

        public TMP_InputField inputField;
#if UNITY_ANDROID && !UNITY_EDITOR 
        // Cache button state to avoid repeated logs
        private bool? _lastQRButtonState = null;
#endif
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Trigger on press (OnPointerDown) instead of release (onClick)
            // onClick listeners removed to prevent double-firing
            AddPointerDownHandler(shiftButton1, HandleShift);
            AddPointerDownHandler(shiftButton2, HandleShift);
            AddPointerDownHandler(spaceButton, Space);
            AddPointerDownHandler(deleteButton, Delete);
            AddPointerDownHandler(submitButton, Submit);
            AddPointerDownHandler(qrCodeButton, QRCode);
            AddPointerDownHandler(skipButton, Skip);
        }

        private void OnEnable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // PIN/keyboard prefab may enable after PICO enterprise bind completes; clear cache so we do not keep a stale "hidden" state.
            _lastQRButtonState = null;
            CheckAndEnableQRButton();
#endif
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On VR, the controller ray often hits the input field's rect before the keys.
            // Disable the input field as a raycast target so pointer events reach KeyboardKey
            // components (same input path as the PIN pad, which has no large input field).
            if (inputField != null && inputField.targetGraphic != null) inputField.targetGraphic.raycastTarget = false;
#endif
            // Check for QR reader instances immediately
            CheckAndEnableQRButton();
            
            // Also check after a short delay in case initialization hasn't completed yet
            StartCoroutine(DelayedQRButtonCheck());
        }
        
        private System.Collections.IEnumerator DelayedQRButtonCheck()
        {
            // Wait a frame to ensure all Awake() methods have completed
            yield return null;
            
            // Check again after a short delay
            yield return new WaitForSeconds(0.5f);
            
            CheckAndEnableQRButton();
            
            // Periodically recheck in case permissions are granted later
            while (true)
            {
                yield return new WaitForSeconds(2.0f); // Check every 2 seconds
                CheckAndEnableQRButton();
            }
        }
        
        /// <summary>
        /// Call when PICO enterprise bind (or other QR availability) changes after keyboard UI already ran its initial check.
        /// </summary>
        public static void RefreshQrButtonAvailability()
        {
            if (Instance == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Instance._lastQRButtonState = null;
            Instance.CheckAndEnableQRButton();
#endif
        }

        private void CheckAndEnableQRButton()
        {
            if (qrCodeButton == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            bool isAvailable = QrScannerCoordinator.GetActiveScanner() != null;
            if (_lastQRButtonState != isAvailable)
            {
                qrCodeButton.gameObject.SetActive(isAvailable);
                if (isAvailable)
                    Logcat.Info("QR Code button enabled.");
                else
                    Logcat.Warning("QR Code button hidden - no supported QR scanner is active for this device.");
                _lastQRButtonState = isAvailable;
            }
#endif
        }

        private void AddPointerDownHandler(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            
            // Get or add EventTrigger component
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            
            // Create entry for pointer down event
            var downEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };
            downEntry.callback.AddListener(_ => {
                // Ignore input during the opening guard window to prevent accidental presses
                if (KeyboardHandler.IsInputGuarded) return;
                action();
            });
            trigger.triggers.Add(downEntry);
            
            // Add pointer up handler to reset button state
            var upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener(_ => { 
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            trigger.triggers.Add(upEntry);
            
            // Add pointer exit handler to reset button state when cursor leaves
            var exitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener(_ => { 
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            trigger.triggers.Add(exitEntry);
        }

        private void Space() => inputField.text += " ";

        private void Delete()
        {
            if (inputField.text.Length > 0)
            {
                int length = inputField.text.Length - 1;
                inputField.text = inputField.text.Substring(0, length);
            }
        }

        private void Submit()
        {
            try
            {
                StartCoroutine(KeyboardHandler.ProcessingVisual());
                // Ensure inputSource is set to "user" for manual keyboard input
                AuthService.SetInputSource("user");
                AbxrSubsystem.Instance.SubmitInput(inputField.text);
                inputField.text = "";
            }
            catch (System.Exception ex)
            {
                Logcat.Error($"KeyboardManager - Error during authentication submission: {ex.Message}");
                // Stop processing visual and clear input on error
                inputField.text = "";
            }
        }
        
        private static void Skip() => AbxrSubsystem.Instance.SubmitInput("**skip**");

        private void QRCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            IQrScanner scanner = QrScannerCoordinator.GetActiveScanner();
            if (scanner != null)
            {
                if (scanner.IsScanning || scanner.IsInitializing)
                    scanner.CancelScan();
                else
                    scanner.ScanQRCode();
            }
#endif
            inputField.text = "";
        }

        private void HandleShift()
        {
            // Notify all KeyboardKey instances to toggle their shift state
            KeyboardKey[] allKeys = FindObjectsOfType<KeyboardKey>();
            foreach (KeyboardKey key in allKeys)
            {
                key.ToggleShift();
            }
        }
    }
}
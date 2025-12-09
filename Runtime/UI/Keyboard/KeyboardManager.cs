using AbxrLib.Runtime.Authentication;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.Keyboard
{
    public class KeyboardManager : MonoBehaviour
    {
        public static KeyboardManager Instance;
        public Button shiftButton1;
        public Button shiftButton2;
        public Button deleteButton;
        public Button spaceButton;
        public Button submitButton;
        public Button qrCodeButton;

        public TMP_InputField inputField;
        
        // Cache button state to avoid repeated logs
        private bool? _lastQRButtonState = null;
    
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
        }

        private void Start()
        {
            // Check for QR reader instances immediately
            CheckAndEnableQRButton();
            
            // Also check after a short delay in case initialization hasn't completed yet
            StartCoroutine(DelayedQRButtonCheck());
            
            // Start coroutine to update button text based on scanning state
            StartCoroutine(UpdateQRButtonText());
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
        
        private void CheckAndEnableQRButton()
        {
            if (qrCodeButton == null) return;
            
#if PICO_ENTERPRISE_SDK_3
            // Only enable QR button if PICO Enterprise SDK 3+ is available and instance exists
            if (PicoQRCodeReader.Instance != null)
            {
                bool shouldShow = true;
                if (_lastQRButtonState != shouldShow)
                {
                    qrCodeButton.gameObject.SetActive(shouldShow);
                    Debug.Log("AbxrLib: QR Code button enabled for PICO (SDK 3+)");
                    _lastQRButtonState = shouldShow;
                }
            }
            else
            {
                // Instance not available - hide button
                if (_lastQRButtonState != false)
                {
                    qrCodeButton.gameObject.SetActive(false);
                    _lastQRButtonState = false;
                }
            }
#endif
#if META_QR_AVAILABLE
            if (MetaQRCodeReader.Instance != null)
            {
                // Only show button if QR scanning is available (device supported, features enabled, permissions granted)
                bool isAvailable = MetaQRCodeReader.Instance.IsQRScanningAvailable();
                
                // Only log and update if state changed
                if (_lastQRButtonState != isAvailable)
                {
                    qrCodeButton.gameObject.SetActive(isAvailable);
                    
                    if (isAvailable)
                    {
                        Debug.Log("AbxrLib: QR Code button enabled for Meta (device supported, permissions granted)");
                    }
                    else
                    {
                        Debug.LogWarning("AbxrLib: QR Code button hidden - QR scanning not available. Check device support and camera permissions.");
                    }
                    _lastQRButtonState = isAvailable;
                }
            }
            else
            {
                if (_lastQRButtonState != false)
                {
                    qrCodeButton.gameObject.SetActive(false);
                    Debug.LogWarning("AbxrLib: MetaQRCodeReader.Instance is null. Button will remain hidden.");
                    _lastQRButtonState = false;
                }
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
            var downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener(_ => { action(); });
            trigger.triggers.Add(downEntry);
            
            // Add pointer up handler to reset button state
            var upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener(_ => { 
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            });
            trigger.triggers.Add(upEntry);
            
            // Add pointer exit handler to reset button state when cursor leaves
            var exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener(_ => { 
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            });
            trigger.triggers.Add(exitEntry);
        }

        private void Space()
        {
            inputField.text += " ";
        }
    
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
                Authentication.Authentication.SetInputSource("user");
                StartCoroutine(Authentication.Authentication.KeyboardAuthenticate(inputField.text));
                inputField.text = "";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: KeyboardManager - Error during authentication submission: {ex.Message}");
                // Stop processing visual and clear input on error
                inputField.text = "";
            }
        }
        
        private void QRCode()
        {
#if PICO_ENTERPRISE_SDK_3
            // Toggle: if already scanning, cancel; otherwise start scanning
            if (PicoQRCodeReader.Instance != null)
            {
                // PICO doesn't have cancel, so just start scanning
                PicoQRCodeReader.Instance.ScanQRCode();
            }
#endif
#if META_QR_AVAILABLE
            // Toggle: if already scanning, cancel; otherwise start scanning
            if (MetaQRCodeReader.Instance != null)
            {
                if (MetaQRCodeReader.Instance.IsScanning())
                {
                    MetaQRCodeReader.Instance.CancelScanning();
                }
                else
                {
                    MetaQRCodeReader.Instance.ScanQRCode();
                }
                // Update button text immediately after toggling
                UpdateQRButtonTextImmediate();
            }
#endif
            inputField.text = "";
        }
        
        /// <summary>
        /// Coroutine to periodically update QR button text based on scanning state
        /// </summary>
        private System.Collections.IEnumerator UpdateQRButtonText()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.2f); // Check every 0.2 seconds
                UpdateQRButtonTextImmediate();
            }
        }
        
        /// <summary>
        /// Update QR button text based on current scanning state
        /// </summary>
        private void UpdateQRButtonTextImmediate()
        {
            if (qrCodeButton == null) return;
            
            // Find TextMeshProUGUI component in button's children
            TextMeshProUGUI buttonText = qrCodeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText == null) return;
            
            bool isScanning = false;
            bool isInitializing = false;
            bool permissionsDenied = false;
            
#if PICO_ENTERPRISE_SDK_3
            // PICO doesn't have IsScanning, so we can't toggle text for it
#endif
#if META_QR_AVAILABLE
            if (MetaQRCodeReader.Instance != null)
            {
                isScanning = MetaQRCodeReader.Instance.IsScanning();
                isInitializing = MetaQRCodeReader.Instance.IsInitializing();
                permissionsDenied = MetaQRCodeReader.AreCameraPermissionsDenied();
            }
#endif
            
            // Update text based on state (priority: permissions denied > scanning > initializing > idle)
            if (permissionsDenied)
            {
                buttonText.text = "Not Available";
            }
            else if (isScanning)
            {
                buttonText.text = "Stop Scanning";
            }
            else if (isInitializing)
            {
                buttonText.text = "Initializing...";
            }
            else
            {
                buttonText.text = "Scan QR Code";
            }
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
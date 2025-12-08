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
#if PICO_ENTERPRISE_SDK
            if (PicoQRCodeReader.Instance != null)
            {
                qrCodeButton.gameObject.SetActive(true);
                Debug.Log("AbxrLib: QR Code button enabled for PICO");
            }
#endif
#if META_QR_AVAILABLE
            if (MetaQRCodeReader.Instance != null)
            {
                qrCodeButton.gameObject.SetActive(true);
                Debug.Log("AbxrLib: QR Code button enabled for Meta");
            }
            else
            {
                Debug.LogWarning("AbxrLib: MetaQRCodeReader.Instance is null. Button will remain hidden.");
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
            EventTrigger.Entry downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener((data) => { action(); });
            trigger.triggers.Add(downEntry);
            
            // Add pointer up handler to reset button state
            EventTrigger.Entry upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener((data) => { 
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            });
            trigger.triggers.Add(upEntry);
            
            // Add pointer exit handler to reset button state when cursor leaves
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { 
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
#if PICO_ENTERPRISE_SDK
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
            TMPro.TextMeshProUGUI buttonText = qrCodeButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText == null) return;
            
            bool isScanning = false;
            
#if PICO_ENTERPRISE_SDK
            // PICO doesn't have IsScanning, so we can't toggle text for it
#endif
#if META_QR_AVAILABLE
            if (MetaQRCodeReader.Instance != null)
            {
                isScanning = MetaQRCodeReader.Instance.IsScanning();
            }
#endif
            
            // Update text based on scanning state
            if (isScanning)
            {
                buttonText.text = "Stop Scanning";
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
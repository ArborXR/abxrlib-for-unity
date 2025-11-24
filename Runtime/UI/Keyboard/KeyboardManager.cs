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
            if (PicoQRCodeReader.Instance != null) qrCodeButton.gameObject.SetActive(true);
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
            PicoQRCodeReader.Instance?.ScanQRCode();
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
using TMPro;
using UnityEngine;
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

        public TMP_InputField inputField;
    
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            spaceButton?.onClick.AddListener(Space);
            deleteButton.onClick.AddListener(Delete);
            submitButton.onClick.AddListener(Submit);
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
                Debug.LogError($"AbxrLib - KeyboardManager: Error during authentication submission: {ex.Message}");
                // Stop processing visual and clear input on error
                inputField.text = "";
            }
        }
    }
}
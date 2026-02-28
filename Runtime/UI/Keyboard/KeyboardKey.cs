using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.Keyboard
{
    public class KeyboardKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string character;
        public string shiftCharacter;

        public TextMeshProUGUI keyLabel;

        private Button _thisKey;

        private bool _isShifted = false;

        private void Start()
        {
            // Shift buttons are now handled by KeyboardManager with pointer down events
            _thisKey = GetComponent<Button>();
            // Trigger on press (OnPointerDown) instead of release (onClick)
            // onClick listener removed to prevent double-firing
            character = keyLabel.text;
            shiftCharacter = keyLabel.text.ToUpper();

            const string numbers = "1234567890.-";
            if (numbers.Contains(keyLabel.text))
            {
                shiftCharacter = GetShiftCharacter();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Trigger on press instead of release
            if (_thisKey != null && _thisKey.interactable)
            {
                // Let the button handle its press state
                _thisKey.OnPointerDown(eventData);
                // Trigger the key action
                TypeKey();
                // Immediately reset the button state after action
                StartCoroutine(ResetButtonState());
            }
        }

        private System.Collections.IEnumerator ResetButtonState()
        {
            // Wait one frame to ensure the press state is set, then reset
            yield return null;
            if (_thisKey != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Reset button state when pointer is released
            if (_thisKey != null)
            {
                _thisKey.OnPointerUp(eventData);
                // Deselect the button to reset visual state
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Reset button state when pointer leaves
            if (_thisKey != null)
            {
                _thisKey.OnPointerExit(eventData);
                // Deselect the button to reset visual state
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        private string GetShiftCharacter()
        {
            switch (keyLabel.text)
            {
                case "1":
                    return "!";
                case "2":
                    return "@";
                case "3":
                    return "#";
                case "4":
                    return "$";
                case "5":
                    return "%";
                case "6":
                    return "^";
                case "7":
                    return "&";
                case "8":
                    return "*";
                case "9":
                    return "(";
                case "0":
                    return ")";
                case ".":
                    return ",";
                case "-":
                    return "_";
                default:
                    break;
            }
            return string.Empty;
        }

        public void ToggleShift()
        {
            _isShifted = !_isShifted;

            keyLabel.text = _isShifted ? shiftCharacter : character;
        }

        private void TypeKey()
        {
            if (_isShifted)
            {
                KeyboardManager.Instance.inputField.text += shiftCharacter;
            }
            else
            {
                KeyboardManager.Instance.inputField.text += character;
            }
        }
    }
}
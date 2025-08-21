using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abxr.Runtime.UI.Keyboard
{
    public class KeyboardKey : MonoBehaviour
    {
        public string character;
        public string shiftCharacter;

        public TextMeshProUGUI keyLabel;

        private Button _thisKey;

        private bool _isShifted = false;

        private void Start()
        {
            KeyboardManager.Instance.shiftButton1?.onClick.AddListener(HandleShift);
            KeyboardManager.Instance.shiftButton2?.onClick.AddListener(HandleShift);
            _thisKey = GetComponent<Button>();
            _thisKey.onClick.AddListener(TypeKey);
            character = keyLabel.text;
            shiftCharacter = keyLabel.text.ToUpper();

            const string numbers = "1234567890";
            if (numbers.Contains(keyLabel.text))
            {
                shiftCharacter = GetShiftCharacter();
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
                default:
                    break;
            }
            return string.Empty;
        }

        private void HandleShift()
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
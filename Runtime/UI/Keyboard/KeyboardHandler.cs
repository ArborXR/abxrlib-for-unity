using System;
using System.Collections;
using TMPro;
using UnityEngine;

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
        
        OnKeyboardDestroyed?.Invoke();
    }

    public static void SetPrompt(string prompt)
    {
        _prompt.text = prompt;
    }

    public static void Create(KeyboardType keyboardType)
    {
        _processingSubmit = false;
        if (_panelInstance) return;
        
        if (keyboardType == KeyboardType.PinPad) _keyboardInstance = Instantiate(_pinPadPrefab);
        else if (keyboardType == KeyboardType.FullKeyboard) _keyboardInstance = Instantiate(_keyboardPrefab);
        
        _panelInstance = Instantiate(_panelPrefab);
        _prompt = _panelInstance.GetComponentInChildren<TextMeshProUGUI>();
        OnKeyboardCreated?.Invoke();
    }
    
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
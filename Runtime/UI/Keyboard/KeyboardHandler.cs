using System;
using System.Collections;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class KeyboardHandler : MonoBehaviour
{
    private GameObject _keyboardPrefab;
    private GameObject _keyboardInstance;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_keyboardInstance != null)
        {
            Destroy(_keyboardInstance);
        }

        _keyboardInstance = Instantiate(_keyboardPrefab, Camera.main.transform);
        NonNativeKeyboard.Instance.OnTextSubmitted += HandleTextSubmitted;
    }
    
    public static bool ProcessingSubmit;
    private const string ProcessingText = "Processing";
    
    private void Start()
    {
        _keyboardPrefab = Resources.Load<GameObject>("Prefabs/AbxrKeyboard");
        if (_keyboardPrefab != null)
        {
            Instantiate(_keyboardPrefab, Camera.main.transform);
        }
        else
        {
            Debug.LogError("Failed to load keyboard prefab");
        }
        
        NonNativeKeyboard.Instance.OnTextSubmitted += HandleTextSubmitted;
    }
    
    private void HandleTextSubmitted(object sender, EventArgs e)
    {
        if (ProcessingSubmit) return;
        
        StartCoroutine(ProcessingVisual());
        var keyboard = (NonNativeKeyboard)sender;
        StartCoroutine(Authentication.KeyboardAuthenticate(keyboard.InputField.text));
    }
    
    private static IEnumerator ProcessingVisual()
    {
        ProcessingSubmit = true;
        NonNativeKeyboard.Instance.Prompt.text = ProcessingText;
        while (ProcessingSubmit)
        {
            string currentText = NonNativeKeyboard.Instance.Prompt.text;
            NonNativeKeyboard.Instance.Prompt.text = currentText.Length > ProcessingText.Length + 10 ?
                ProcessingText :
                $":{NonNativeKeyboard.Instance.Prompt.text}:";
            
            yield return new WaitForSeconds(0.5f); // Wait before running again
        }
    }
}
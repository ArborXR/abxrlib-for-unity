using System;
using System.Collections;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using UnityEngine;

public class KeyboardHandler : MonoBehaviour
{
    public static bool ProcessingSubmit;
    private const string ProcessingText = "Processing";
    
    private void Start()
    {
        GameObject keyboard = Resources.Load<GameObject>("Prefabs/AbxrKeyboard");
        if (keyboard != null)
        {
            Instantiate(keyboard, Camera.main.transform);
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
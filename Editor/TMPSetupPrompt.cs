#if UNITY_EDITOR
using UnityEditor;
using TMPro;

[InitializeOnLoad]
public static class TMPSetupPrompt
{
    static TMPSetupPrompt()
    {
        // Delay so Unity has time to initialize all menus
        EditorApplication.delayCall += TryPromptTMPImport;
    }

    private static void TryPromptTMPImport()
    {
        // This will trigger the TMP Essentials Import Prompt
        if (TMP_Settings.instance) ;
    }
}
#endif
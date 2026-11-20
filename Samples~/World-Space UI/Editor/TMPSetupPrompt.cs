#if UNITY_EDITOR
using TMPro;
using UnityEditor;

namespace AbxrLib.WorldSpace.Editor
{
    /// <summary>
    /// Nudges Unity into showing its own "import TMP essential resources" prompt.
    ///
    /// Ships with the world-space objects rather than with the core package: the fonts and shaders are only needed
    /// to draw the keyboard, PIN pad, and exit poll, so a core-only project should never be asked for them.
    /// </summary>
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
            if (TMP_Settings.instance) { }
        }
    }
}
#endif

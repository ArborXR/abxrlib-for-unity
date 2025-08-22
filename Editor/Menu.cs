using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace AbxrLib.Editor
{
    public class Menu
    {
        private static Configuration _config;
    
        [MenuItem("Analytics for XR/Configuration", priority = 1)]
        private static void Configuration()
        {
            Selection.activeObject = Core.GetConfig();
        }
    
        [MenuItem("Analytics for XR/Documentation", priority = 2)]
        private static void Documentation()
        {
            Application.OpenURL("https://github.com/ArborXR/abxrlib-for-unity?tab=readme-ov-file#table-of-contents");
        }
    }
}

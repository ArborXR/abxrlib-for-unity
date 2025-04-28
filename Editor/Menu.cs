using UnityEngine;
using UnityEditor;

public class Menu
{
    private static Configuration _config;
    
    [MenuItem("ArborXR Insights/Configuration", priority = 1)]
    private static void Configuration()
    {
        Selection.activeObject = Core.GetConfig();
    }
    
    [MenuItem("ArborXR Insights/Documentation", priority = 2)]
    private static void Documentation()
    {
        Application.OpenURL("https://github.com/ArborXR/abxrlib-for-unity?tab=readme-ov-file#table-of-contents");
    }
}

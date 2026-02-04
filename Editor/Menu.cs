using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Telemetry;
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

        [MenuItem("Analytics for XR/Create Abxr Target", priority = 3)]
        private static void CreateAbxrTarget()
        {
            // Create a new GameObject with AbxrTarget component
            GameObject targetObject = new GameObject("AbxrTarget");
            targetObject.AddComponent<AbxrTarget>();

            // Position it at the scene origin (user can move it)
            targetObject.transform.position = Vector3.zero;

            // Select the newly created object
            Selection.activeGameObject = targetObject;

            // Register undo operation
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Abxr Target");

            Debug.Log("AbxrLib: Created AbxrTarget GameObject. Set the 'Target Name' field in the Inspector to customize the name.");
        }
    }
}

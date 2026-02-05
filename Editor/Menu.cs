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
            AbxrTarget abxrTarget = targetObject.AddComponent<AbxrTarget>();

            // Generate and set a unique targetName
            // Note: Reset() will also be called automatically, but we set it here for immediate feedback
            abxrTarget.targetName = GenerateUniqueTargetName();

            // Position it at the scene origin (user can move it)
            targetObject.transform.position = Vector3.zero;

            // Select the newly created object
            Selection.activeGameObject = targetObject;

            // Register undo operation
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Abxr Target");

            Debug.Log($"AbxrLib: Created AbxrTarget GameObject with targetName '{abxrTarget.targetName}'. You can edit the 'Target Name' field in the Inspector to customize it.");
        }

        /// <summary>
        /// Generates a unique targetName in the format "AbxrTarget1", "AbxrTarget2", etc.
        /// Finds all existing AbxrTarget components and assigns the next available number.
        /// </summary>
        /// <returns>A unique targetName</returns>
        private static string GenerateUniqueTargetName()
        {
            // Find all AbxrTarget components in the scene
            AbxrTarget[] allTargets = UnityEngine.Object.FindObjectsOfType<AbxrTarget>();
            
            int maxNumber = 0;
            string baseName = "AbxrTarget";
            
            // Find the highest number used
            foreach (var target in allTargets)
            {
                if (target == null) continue;
                
                // Skip targets that don't have a targetName set (they use GameObject name, which might be "AbxrTarget")
                // These are likely newly created targets that haven't been assigned a name yet
                if (string.IsNullOrEmpty(target.targetName)) continue;
                
                string targetName = target.GetTargetName();
                
                // Check if this name matches our pattern "AbxrTarget" followed by a number
                if (targetName.StartsWith(baseName))
                {
                    string suffix = targetName.Substring(baseName.Length);
                    if (int.TryParse(suffix, out int number))
                    {
                        if (number > maxNumber)
                        {
                            maxNumber = number;
                        }
                    }
                }
            }
            
            // Return the next available number
            return $"{baseName}{maxNumber + 1}";
        }
    }
}

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
            abxrTarget.SetTargetName(GenerateUniqueTargetName());

            // Position the target intelligently:
            // 1. If an object is selected, parent to it and position at its location
            // 2. Otherwise, try to position at scene view camera focus point
            // 3. Fall back to scene origin
            Vector3 targetPosition = Vector3.zero;
            Transform parentTransform = null;

            if (Selection.activeGameObject != null)
            {
                // Parent to selected object and position at its location
                parentTransform = Selection.activeGameObject.transform;
                targetPosition = parentTransform.position;
            }
            else
            {
                // Try to get scene view camera focus point
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    // Get the pivot point (center of view) from the scene view
                    targetPosition = sceneView.pivot;
                }
            }

            // Set parent if we have one
            if (parentTransform != null)
            {
                targetObject.transform.SetParent(parentTransform);
                // When parented, use local position (0,0,0) to center it on the parent
                targetObject.transform.localPosition = Vector3.zero;
                targetObject.transform.localRotation = Quaternion.identity;
                targetObject.transform.localScale = Vector3.one;
            }
            else
            {
                // No parent, use world position
                targetObject.transform.position = targetPosition;
            }

            // Select the newly created object
            Selection.activeGameObject = targetObject;

            // Register undo operation
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Abxr Target");

            string locationInfo = parentTransform != null 
                ? $"parented to '{parentTransform.name}'" 
                : $"at position {targetPosition}";
            Debug.Log($"AbxrLib: Created AbxrTarget GameObject with targetName '{abxrTarget.GetTargetName()}' {locationInfo}. You can edit the 'Target Name' field in the Inspector to customize it.");
        }

        /// <summary>
        /// Generates a unique targetName in the format "AbxrTarget1", "AbxrTarget2", etc.
        /// Finds all existing AbxrTarget components and assigns the next available number.
        /// </summary>
        /// <returns>A unique targetName</returns>
        private static string GenerateUniqueTargetName()
        {
            // Use cached registry instead of FindObjectsOfType for performance
            AbxrTarget[] allTargets = AbxrTarget.GetAllTargets();
            
            int maxNumber = 0;
            string baseName = "AbxrTarget";
            
            // Find the highest number used
            foreach (var target in allTargets)
            {
                if (target == null) continue;
                
                // Skip targets that don't have a custom targetName set (they use GameObject name, which might be "AbxrTarget")
                // These are likely newly created targets that haven't been assigned a name yet
                if (!target.HasCustomTargetName()) continue;
                
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

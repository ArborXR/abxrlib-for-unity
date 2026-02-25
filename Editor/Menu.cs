using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Telemetry;
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
            GameObject targetObject = new GameObject("AbxrTarget");
            AbxrTarget abxrTarget = targetObject.AddComponent<AbxrTarget>();
            abxrTarget.SetTargetName(GenerateUniqueTargetName());

            Vector3 targetPosition = Vector3.zero;
            Transform parentTransform = null;

            if (Selection.activeGameObject != null)
            {
                parentTransform = Selection.activeGameObject.transform;
                targetPosition = parentTransform.position;
            }
            else
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                    targetPosition = sceneView.pivot;
            }

            if (parentTransform != null)
            {
                targetObject.transform.SetParent(parentTransform);
                targetObject.transform.localPosition = Vector3.zero;
                targetObject.transform.localRotation = Quaternion.identity;
                targetObject.transform.localScale = Vector3.one;
            }
            else
            {
                targetObject.transform.position = targetPosition;
            }

            Selection.activeGameObject = targetObject;
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Abxr Target");

            string locationInfo = parentTransform != null
                ? $"parented to '{parentTransform.name}'"
                : $"at position {targetPosition}";
            Debug.Log($"AbxrLib: Created AbxrTarget GameObject with targetName '{abxrTarget.GetTargetName()}' {locationInfo}. You can edit the 'Target Name' field in the Inspector to customize it.");
        }

        private static string GenerateUniqueTargetName()
        {
            AbxrTarget[] allTargets = AbxrTarget.GetAllTargets();
            int maxNumber = 0;
            const string baseName = "AbxrTarget";
            foreach (var target in allTargets)
            {
                if (target == null) continue;
                if (!target.HasCustomTargetName()) continue;
                string targetName = target.GetTargetName();
                if (targetName.StartsWith(baseName))
                {
                    string suffix = targetName.Substring(baseName.Length);
                    if (int.TryParse(suffix, out int number) && number > maxNumber)
                        maxNumber = number;
                }
            }
            return $"{baseName}{maxNumber + 1}";
        }
    }
}

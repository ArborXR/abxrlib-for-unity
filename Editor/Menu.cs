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

        private const string IncludeAllConfigMenu = "Analytics for XR/Diagnostics/Include all config values";

        [MenuItem("Analytics for XR/Diagnostics/Copy report", priority = 4)]
        private static void CopyDiagnostics()
        {
            SetupDiagnostics.CopyToClipboard();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("AbxrLib",
                    "Diagnostics copied to the clipboard. Paste them into your support request.\n\n" +
                    "Tokens and secrets are never included; the report only says whether they are set.", "OK");
        }

        [MenuItem(IncludeAllConfigMenu, priority = 5)]
        private static void ToggleIncludeAllConfig() =>
            SetupDiagnostics.IncludeAllConfig = !SetupDiagnostics.IncludeAllConfig;

        // The validate function is where Unity lets a menu item draw its checkmark. Fully qualified because this
        // class is also called Menu.
        [MenuItem(IncludeAllConfigMenu, true)]
        private static bool ToggleIncludeAllConfigValidate()
        {
            UnityEditor.Menu.SetChecked(IncludeAllConfigMenu, SetupDiagnostics.IncludeAllConfig);
            return true;
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
            Logcat.Info($"Created AbxrTarget GameObject with targetName '{abxrTarget.GetTargetName()}' {locationInfo}. You can edit the 'Target Name' field in the Inspector to customize it.");
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

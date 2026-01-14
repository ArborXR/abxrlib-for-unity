#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;
using UnityEngine;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    public class MetaDefineManager
    {
        static MetaDefineManager()
        {
            CheckAndSetMetaDefine();
        }
        
        private static void CheckAndSetMetaDefine()
        {
            // Also check for Oculus Integration package
            bool hasOculusIntegration = 
                AppDomain.CurrentDomain.GetAssemblies().Any(a => 
                    a.GetName().Name.Contains("Oculus") || 
                    a.GetName().Name.Contains("OVR"));
            
            // Check for OpenXR (which is used with Quest)
            bool hasOpenXR = 
                AppDomain.CurrentDomain.GetAssemblies().Any(a => 
                    a.GetName().Name.Contains("Unity.XR.OpenXR") ||
                    a.GetName().Name.Contains("OpenXR"));
            
            // Check if Oculus Settings asset exists (indicates Quest support)
            bool hasOculusSettings = AssetDatabase.FindAssets("Oculus Settings").Length > 0;
            
            if (hasOculusIntegration || (hasOpenXR && hasOculusSettings))
            {
                Debug.Log("AbxrLib: Meta/Quest SDK detected. Setting META_QR_AVAILABLE define symbol.");
                AddDefineForAllPlatforms("META_QR_AVAILABLE");
            }
            else if (hasOpenXR)
            {
                // If OpenXR is present, we might be targeting Quest, so enable it for Android
                Debug.Log("AbxrLib: OpenXR detected. Setting META_QR_AVAILABLE for Android builds.");
                AddDefineForPlatform("META_QR_AVAILABLE", BuildTargetGroup.Android);
            }
            else
            {
                Debug.Log("AbxrLib: Meta SDK not detected. META_QR_AVAILABLE will not be set.");
                Debug.Log("AbxrLib: To enable Meta QR scanning, ensure Meta SDK or OpenXR is installed.");
            }
        }
        
        private static void AddDefineForAllPlatforms(string define)
        {
            // Add for Android (most important for Quest)
            AddDefineForPlatform(define, BuildTargetGroup.Android);
            
            // Also add for current selected platform
            var currentTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (currentTarget != BuildTargetGroup.Android)
            {
                AddDefineForPlatform(define, currentTarget);
            }
        }
        
        private static void AddDefineForPlatform(string define, BuildTargetGroup target)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!defines.Contains(define))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, 
                    string.IsNullOrEmpty(defines) ? define : defines + ";" + define);
                Debug.Log($"AbxrLib: Added {define} to {target} scripting define symbols.");
            }
        }
    }
}
#endif


#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;
using UnityEngine;
using AbxrLib.Runtime.Core;

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
                Logcat.Debug("Meta / OpenXR SDK detected.");
                AddDefineForAllPlatforms("META_QR_AVAILABLE");
            }
            else if (hasOpenXR)
            {
                // If OpenXR is present, we might be targeting Quest, so enable it for Android
                Logcat.Debug("OpenXR detected.");
                AddDefineForPlatform("META_QR_AVAILABLE", BuildTargetGroup.Android);
            }
            else
            {
                Logcat.Debug("Meta SDK not detected.");
                Logcat.Debug("To enable Meta QR scanning, ensure Meta SDK or OpenXR is installed.");
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
                Logcat.Debug($"Added {define} to {target} scripting define symbols.");
            }
        }
    }
}
#endif


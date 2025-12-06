#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    public class MetaDefineManager
    {
        static MetaDefineManager()
        {
            // Check for Meta/Oculus SDK availability
            bool hasMetaSDK =
                AppDomain.CurrentDomain.GetAssemblies().Any(a => 
                    a.GetName().Name == "Oculus.VR" || 
                    a.GetName().Name == "Meta.XR.SDK.Core" ||
                    a.GetName().Name.Contains("Meta.XR"));
            
            if (hasMetaSDK)
            {
                AddDefine("META_QR_AVAILABLE");
            }
        }

        private static void AddDefine(string define)
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!defines.Contains(define))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines + ";" + define);
            }
        }
    }
}
#endif


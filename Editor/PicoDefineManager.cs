#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    public class PicoDefineManager
    {
        static PicoDefineManager()
        {
            bool hasPicoIntegrationSDK =
                AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.XR.PICO");
            if (hasPicoIntegrationSDK)
            {
                AddDefine("PICO_ENTERPRISE_SDK");
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
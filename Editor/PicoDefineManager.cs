#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Linq;
using System.Reflection;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    public class PicoDefineManager
    {
        private static Version GetSDKVersion()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var asm in assemblies)
                {
                    // Look for Unity.XR.PXR.PXR_System by name (no direct type reference)
                    var systemType = asm.GetType("Unity.XR.PXR.PXR_System", throwOnError: false);
                    if (systemType == null) continue;

                    var method = systemType.GetMethod(
                        "GetSDKVersion",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                    if (method == null) return null; // type exists but method doesn't

                    var str = method.Invoke(null, null) as string;
                    if (string.IsNullOrWhiteSpace(str)) return null;

                    // First try direct parse
                    if (Version.TryParse(str, out var version)) return version;

                    // sanitize prefix like "3.0.0.1_beta"
                    var cleaned = new string(str
                        .TakeWhile(c => char.IsDigit(c) || c == '.')
                        .ToArray());

                    return Version.TryParse(cleaned, out version) ? version : null;
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        static PicoDefineManager()
        {
            bool hasPicoIntegrationSDK =
                AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.XR.PICO");
            if (!hasPicoIntegrationSDK) return;
            
            var requiredSdkVersion = new Version("3.0.0");
            if (GetSDKVersion() >= requiredSdkVersion)
            {
                AddDefine("PICO_ENTERPRISE_SDK_3");
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
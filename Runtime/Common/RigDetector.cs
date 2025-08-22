using System;
using System.Linq;
using UnityEngine;

namespace AbxrLib.Runtime.Common
{
    public static class RigDetector
    {
        private static string _prefabSuffix = "";
    
        public static string PrefabSuffix()
        {
            if (!string.IsNullOrEmpty(_prefabSuffix)) return _prefabSuffix;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsOVRCameraRigInUse()) _prefabSuffix = "_Meta";
        else _prefabSuffix = "_OpenXR";
#else
            else _prefabSuffix = "_Default";
#endif
            return _prefabSuffix;
        }
    
        public static bool IsXRRigInUse()
        {
            return IsTypeInScene("UnityEngine.XR.Interaction.Toolkit.XRRig");
        }

        public static bool IsOVRCameraRigInUse()
        {
            return IsTypeInScene("OVRCameraRig");
        }

        private static bool IsTypeInScene(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(typeName, false);
                    if (type == null) continue;

                    var objects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
                    foreach (var obj in objects)
                    {
                        var components = ((GameObject)obj).GetComponents<Component>();
                        if (components.Any(c => c && c.GetType() == type))
                        {
                            return true;
                        }
                    }
                }
                catch { /* ignore */ }
            }
            return false;
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName, false);
                    if (type != null)
                        return type;
                }
                catch { }
            }
            return null;
        }
    }
}
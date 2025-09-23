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
            _prefabSuffix = "_Default";
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
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var targetType = assembly.GetType(typeName, false);
                    if (targetType == null) continue;

                    var gameObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
                    foreach (var gameObject in gameObjects)
                    {
                        var components = ((GameObject)gameObject).GetComponents<Component>();
                        if (components.Any(component => component && component.GetType() == targetType))
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
                    var foundType = assembly.GetType(typeName, false);
                    if (foundType != null)
                        return foundType;
                }
                catch { }
            }
            return null;
        }
    }
}
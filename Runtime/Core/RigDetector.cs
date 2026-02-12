using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public static class RigDetector
    {
        private static string _prefabSuffix = "";
        
        // Cache for type detection to avoid repeated reflection calls
        private static readonly Dictionary<string, System.Type> TypeCache = new();
        private static readonly Dictionary<string, bool> SceneTypeCache = new();
        private static float _lastSceneCheckTime = 0f;
        private const float SceneCheckCacheDuration = 5f; // Cache scene checks for 5 seconds
    
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
            return IsTypeInSceneCached("UnityEngine.XR.Interaction.Toolkit.XRRig");
        }

        public static bool IsOVRCameraRigInUse()
        {
            return IsTypeInSceneCached("OVRCameraRig");
        }

        /// <summary>
        /// Cached version of IsTypeInScene that avoids repeated expensive operations
        /// </summary>
        private static bool IsTypeInSceneCached(string typeName)
        {
            // Check if we have a recent cached result
            if (Time.time - _lastSceneCheckTime < SceneCheckCacheDuration)
            {
                if (SceneTypeCache.TryGetValue(typeName, out bool cachedResult))
                {
                    return cachedResult;
                }
            }
            else
            {
                // Clear cache if it's too old
                SceneTypeCache.Clear();
            }

            // Perform the actual check
            bool result = IsTypeInScene(typeName);
            
            // Cache the result
            SceneTypeCache[typeName] = result;
            _lastSceneCheckTime = Time.time;
            
            return result;
        }

        private static bool IsTypeInScene(string typeName)
        {
            try
            {
                // First, try to get the type from cache
                var targetType = GetCachedType(typeName);
                if (targetType == null) return false;

                // Try multiple approaches to find objects of specific type, with robust error handling
                try
                {
                    // Method 1: Try the generic FindObjectsOfType<T>() approach first (most reliable)
                    var findObjectsOfTypeGenericMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", System.Type.EmptyTypes);
                    if (findObjectsOfTypeGenericMethod != null)
                    {
                        var genericMethod = findObjectsOfTypeGenericMethod.MakeGenericMethod(targetType);
                        var objects = (Component[])genericMethod.Invoke(null, null);
                        return objects != null && objects.Length > 0;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: Generic FindObjectsOfType method failed for {typeName}: {ex.Message}");
                }

                try
                {
                    // Method 2: Try the Type-based FindObjectsOfType approach
                    var findObjectsOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(System.Type) });
                    if (findObjectsOfTypeMethod != null)
                    {
                        var objects = (Component[])findObjectsOfTypeMethod.Invoke(null, new object[] { targetType });
                        return objects != null && objects.Length > 0;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: Type-based FindObjectsOfType method failed for {typeName}: {ex.Message}");
                }

                try
                {
                    // Method 3: Try the newer FindObjectsOfType with includeInactive parameter
                    var findObjectsOfTypeWithIncludeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(System.Type), typeof(bool) });
                    if (findObjectsOfTypeWithIncludeMethod != null)
                    {
                        var objects = (Component[])findObjectsOfTypeWithIncludeMethod.Invoke(null, new object[] { targetType, false });
                        return objects != null && objects.Length > 0;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrLib: FindObjectsOfType with includeInactive method failed for {typeName}: {ex.Message}");
                }

                // Fallback to the original method if all reflection approaches fail
                return IsTypeInSceneFallback(targetType);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Type detection failed for {typeName}: {ex.Message}");
                return false;
            }
        }

        private static bool IsTypeInSceneFallback(System.Type targetType)
        {
            try
            {
                var gameObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
                foreach (var gameObject in gameObjects)
                {
                    try
                    {
                        var components = ((GameObject)gameObject).GetComponents<Component>();
                        if (components.Any(component => component && component.GetType() == targetType))
                        {
                            return true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Error checking components on GameObject {gameObject.name}: {ex.Message}");
                        continue;
                    }
                }
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Fallback type detection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a cached type or finds it and caches it for future use
        /// </summary>
        private static System.Type GetCachedType(string typeName)
        {
            // Check cache first
            if (TypeCache.TryGetValue(typeName, out System.Type cachedType))
            {
                return cachedType;
            }

            // Find the type and cache it
            var foundType = FindType(typeName);
            if (foundType != null)
            {
                TypeCache[typeName] = foundType;
            }
            
            return foundType;
        }

        private static System.Type FindType(string typeName)
        {
            try
            {
                // Try common assemblies first for better performance
                var commonAssemblies = new[] 
                {
                    "Unity.XR.Interaction.Toolkit",
                    "Unity.XR.CoreUtils", 
                    "Unity.XR.Management",
                    "Oculus.VR",
                    "UnityEngine"
                };

                foreach (var assemblyName in commonAssemblies)
                {
                    try
                    {
                        var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == assemblyName);
                        if (assembly != null)
                        {
                            var foundType = assembly.GetType(typeName, false);
                            if (foundType != null)
                                return foundType;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Error searching assembly {assemblyName} for type {typeName}: {ex.Message}");
                    }
                }

                // Fallback to scanning all assemblies
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var foundType = assembly.GetType(typeName, false);
                        if (foundType != null)
                            return foundType;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"AbxrLib: Error searching assembly {assembly.GetName().Name} for type {typeName}: {ex.Message}");
                    }
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib: Type finding failed for {typeName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears all caches. Call this when scene changes or when you want to force refresh.
        /// </summary>
        public static void ClearCache()
        {
            TypeCache.Clear();
            SceneTypeCache.Clear();
            _lastSceneCheckTime = 0f;
        }
    }
}
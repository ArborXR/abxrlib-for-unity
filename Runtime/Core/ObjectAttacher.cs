using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public class ObjectAttacher : MonoBehaviour
    {
        private const string RootName = "[AbxrLib]";
        private static Transform _rootTransform;

        private static Transform RootTransform
        {
            get
            {
                if (_rootTransform != null) return _rootTransform;

                // Try to find an existing root if someone created it manually
                var existing = GameObject.Find(RootName);
                if (existing != null)
                {
                    _rootTransform = existing.transform;
                    if (existing.scene.buildIndex != -1) // not already in DDOL
                    {
                        DontDestroyOnLoad(existing);
                    }
                }
                else
                {
                    // Create our own root
                    var root = new GameObject(RootName);
                    DontDestroyOnLoad(root);
                    _rootTransform = root.transform;
                }

                return _rootTransform;
            }
        }
        
        public static T Attach<T>(string componentName) where T : MonoBehaviour
        {
            var go = new GameObject(componentName);
            go.transform.SetParent(RootTransform, worldPositionStays: false);
            return go.AddComponent<T>();
        }
    }
}
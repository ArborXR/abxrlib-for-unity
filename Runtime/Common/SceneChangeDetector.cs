using System.Collections.Generic;
using Abxr.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abxr.Runtime.Common
{
    public class SceneChangeDetector : MonoBehaviour
    {
        public static string CurrentSceneName;
    
        private void Start()
        {
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnActiveSceneLoaded;
            SceneManager.sceneUnloaded += OnActiveSceneUnloaded;
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks or unwanted calls
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnActiveSceneLoaded;
            SceneManager.sceneUnloaded -= OnActiveSceneUnloaded;
        }
    
        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            CurrentSceneName = newScene.name;
            if (!Configuration.Instance.disableSceneEvents)
            {
                Core.Abxr.Event("Scene Changed", new Dictionary<string, string> { ["Scene Name"] = newScene.name });
            }
        }
    
        private static void OnActiveSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Configuration.Instance.disableSceneEvents)
            {
                Core.Abxr.Event("Scene Loaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
            }
        }
    
        private static void OnActiveSceneUnloaded(Scene scene)
        {
            if (!Configuration.Instance.disableSceneEvents)
            {
                Core.Abxr.Event("Scene Unloaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
            }
        }
    }
}
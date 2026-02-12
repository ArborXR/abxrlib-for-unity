using System.Collections.Generic;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine.SceneManagement;

namespace AbxrLib.Runtime.Core
{
    public class SceneChangeDetector
    {
        public static string CurrentSceneName;
    
        public void Start()
        {
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnActiveSceneLoaded;
            SceneManager.sceneUnloaded += OnActiveSceneUnloaded;
        }

        public void Stop()
        {
            // Unsubscribe to prevent memory leaks or unwanted calls
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnActiveSceneLoaded;
            SceneManager.sceneUnloaded -= OnActiveSceneUnloaded;
        }
    
        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            CurrentSceneName = newScene.name;
            
            // Clean up laser pointer manager to prevent memory leaks from destroyed objects
            LaserPointerManager.OnSceneChanged();
            
            // Clear RigDetector cache since scene objects have changed
            RigDetector.ClearCache();
            
            if (!Configuration.Instance.disableSceneEvents)
            {
                Abxr.Event("Scene Changed", new Dictionary<string, string> { ["Scene Name"] = newScene.name });
            }
        }
    
        private static void OnActiveSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Configuration.Instance.disableSceneEvents)
            {
                Abxr.Event("Scene Loaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
            }
        }
    
        private static void OnActiveSceneUnloaded(Scene scene)
        {
            if (!Configuration.Instance.disableSceneEvents)
            {
                Abxr.Event("Scene Unloaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
            }
        }
    }
}
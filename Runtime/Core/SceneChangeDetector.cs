using System;
using System.Collections.Generic;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine.SceneManagement;

namespace AbxrLib.Runtime.Core
{
    public class SceneChangeDetector
    {
        public static string CurrentSceneName;

        private readonly Func<bool> _authStartedForSceneAnalytics;

        /// <param name="authStartedForSceneAnalytics">When false, scene load/change/unload events are not sent (still updates <see cref="CurrentSceneName"/> and runs laser/rig cleanup).</param>
        public SceneChangeDetector(Func<bool> authStartedForSceneAnalytics)
        {
            _authStartedForSceneAnalytics = authStartedForSceneAnalytics ?? throw new ArgumentNullException(nameof(authStartedForSceneAnalytics));
        }

        public void Start()
        {
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Stop()
        {
            // Unsubscribe to prevent memory leaks or unwanted calls
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            CurrentSceneName = newScene.name;

            // Clean up laser pointer manager to prevent memory leaks from destroyed objects
            LaserPointerManager.OnSceneChanged();

            // Clear RigDetector cache since scene objects have changed
            RigDetector.ClearCache();

            if (Configuration.Instance.enableSceneEvents && _authStartedForSceneAnalytics())
                Abxr.Event("Scene Changed", new Dictionary<string, string> { ["Scene Name"] = newScene.name });
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Configuration.Instance.enableSceneEvents && _authStartedForSceneAnalytics())
                Abxr.Event("Scene Loaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (Configuration.Instance.enableSceneEvents && _authStartedForSceneAnalytics())
                Abxr.Event("Scene Unloaded", new Dictionary<string, string> { ["Scene Name"] = scene.name });
        }
    }
}

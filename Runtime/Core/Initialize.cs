using AbxrLib.Runtime.Core.UI;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public static class Initialize
    {
        /// <summary>
        /// When true, OnBeforeSceneLoad will not create the AbxrSubsystem.
        /// Allows tests (or other code) to skip the default subsystem so they create their own and avoid one full init that would be destroyed in SetUp.
        /// </summary>
        public static bool SkipCreatingSubsystemInInitialize { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            // Creates the keyboard, PIN pad, exit poll, and QR reader when the world-space objects are installed.
            // Does nothing in a core-only project, where the app supplies its own input UI via
            // Abxr.OnInputRequested.
            AbxrUi.AttachSceneObjects();

            bool skip = SkipCreatingSubsystemInInitialize || AbxrSubsystem.Instance != null;
#if ABXR_TEST_RUNNER_PLAYER
            skip = true; // Test Runner Player build: tests create their own subsystem; avoid redundant init.
#endif
            if (skip) return;
            var go = new GameObject("[AbxrLib]");
            go.AddComponent<AbxrSubsystem>();
        }
    }
}
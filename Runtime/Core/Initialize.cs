using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using AbxrLib.Runtime.Core.QRScanner;
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
            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_SDK_3_4_OR_NEWER
            ObjectAttacher.Attach<QRCodeReaderPico>("QRCodeReaderPico");
#else
            ObjectAttacher.Attach<QRCodeReaderMeta>("QRCodeReaderMeta");
#endif
#endif
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
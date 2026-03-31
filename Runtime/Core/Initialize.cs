using AbxrLib.Runtime;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public static class Initialize
    {
        private static bool _handlersAttached;

        /// <summary>
        /// When true, runtime setup will not create the <see cref="AbxrSubsystem"/>.
        /// Allows tests (or other code) to skip the default subsystem so they create their own and avoid one full init that would be destroyed in SetUp.
        /// Handlers are still attached when full startup runs (see <see cref="ShouldRunFullStartupBeforeSceneLoad"/>).
        /// </summary>
        public static bool SkipCreatingSubsystemInInitialize { get; set; }

        /// <summary>Loads <c>Resources/AbxrLib</c> and validates so <see cref="Configuration.Instance"/> is ready. Same as <see cref="Configuration.EnsureConfigurationLoaded"/>.</summary>
        public static void EnsureConfigurationLoaded() => Configuration.EnsureConfigurationLoaded();

        /// <summary>
        /// Attaches keyboard, exit poll, and (on Android) QR handlers and creates <see cref="AbxrSubsystem"/> when appropriate.
        /// Idempotent. Used when startup was deferred, from <see cref="Abxr.Initialize"/> or <see cref="Abxr.StartAuthentication"/>.
        /// </summary>
        public static void CreateRuntimeIfNeeded()
        {
            Configuration.EnsureConfigurationLoaded();
            AttachHandlersIfNeeded();
            TryCreateSubsystemIfNeeded();
        }

        internal static void ResetForTesting()
        {
            _handlersAttached = false;
        }

        /// <summary>
        /// When <see cref="Configuration.enableAutoStartAuthentication"/> is true, AbxrLib always performs full startup at <c>BeforeSceneLoad</c> (subsystem + handlers).
        /// Otherwise, <see cref="Configuration.enableAutoInitialize"/> controls eager vs deferred startup.
        /// </summary>
        internal static bool ShouldRunFullStartupBeforeSceneLoad()
        {
            var c = Configuration.Instance;
            return c.enableAutoStartAuthentication || c.enableAutoInitialize;
        }

        private static void AttachHandlersIfNeeded()
        {
            if (_handlersAttached) return;
            _handlersAttached = true;

            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_ENTERPRISE_SDK_3
            ObjectAttacher.Attach<QRCodeReaderPico>("QRCodeReaderPico");
#else
            ObjectAttacher.Attach<QRCodeReader>("QRCodeReader");
#endif
#endif
        }

        private static void TryCreateSubsystemIfNeeded()
        {
#if ABXR_TEST_RUNNER_PLAYER
            return;
#endif
            if (SkipCreatingSubsystemInInitialize) return;
            if (AbxrSubsystem.Instance != null) return;

            var go = new GameObject("[AbxrLib]");
            go.AddComponent<AbxrSubsystem>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Configuration.EnsureConfigurationLoaded();
            if (!ShouldRunFullStartupBeforeSceneLoad())
                return;

            AttachHandlersIfNeeded();
            TryCreateSubsystemIfNeeded();
        }
    }
}

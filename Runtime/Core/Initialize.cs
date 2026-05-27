using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using AbxrLib.Runtime.Core.QRScanner;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    public static class Initialize
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            if (!Configuration.AutomaticInitializationEnabled) return;

            CreateSubsystemIfNeeded();
        }

        internal static AbxrSubsystem CreateSubsystemIfNeeded()
        {
            if (AbxrSubsystem.Instance != null) return AbxrSubsystem.Instance;

            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_SDK_3_4_OR_NEWER
            ObjectAttacher.Attach<QRCodeReaderPico>("QRCodeReaderPico");
#else
            ObjectAttacher.Attach<QRCodeReaderMeta>("QRCodeReaderMeta");
#endif
#endif
            var go = new GameObject("[AbxrLib]");
            return go.AddComponent<AbxrSubsystem>();
        }
    }
}

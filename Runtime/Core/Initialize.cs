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
            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");
#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_SDK_3_4_OR_NEWER
            ObjectAttacher.Attach<QRCodeReaderPico>("QRCodeReaderPico");
#else
            ObjectAttacher.Attach<QRCodeReaderMeta>("QRCodeReaderMeta");
#endif
#endif
            if (AbxrSubsystem.Instance != null) return;
            var go = new GameObject("[AbxrLib]");
            go.AddComponent<AbxrSubsystem>();
        }
    }
}
using AbxrLib.Runtime.Services.QRCodeReader;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
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
            AbxrQRCodeReaderFactory.AttachPreferredReader();
#endif
            if (AbxrSubsystem.Instance != null) return;
            var go = new GameObject("[AbxrLib]");
            go.AddComponent<AbxrSubsystem>();
        }
    }
}
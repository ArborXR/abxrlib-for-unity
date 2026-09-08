using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.ExitPoll
{
    // Only ever instantiated by loading a shipped prefab, so the managed linker cannot see it referenced.
    [Preserve]
    public class ExitPollThumbs : MonoBehaviour
    {
        public Button thumbsUpButton;
        public Button thumbsDownButton;
    
        private void Start()
        {
            thumbsUpButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("up"));
            thumbsDownButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("down"));
        }
    }
}
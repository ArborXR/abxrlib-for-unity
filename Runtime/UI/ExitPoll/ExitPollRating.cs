using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.ExitPoll
{
    // Only ever instantiated by loading a shipped prefab, so the managed linker cannot see it referenced.
    [Preserve]
    public class ExitPollRating : MonoBehaviour
    {
        public Button oneRatingButton;
        public Button twoRatingButton;
        public Button threeRatingButton;
        public Button fourRatingButton;
        public Button fiveRatingButton;
    
        private void Start()
        {
            oneRatingButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("1"));
            twoRatingButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("2"));
            threeRatingButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("3"));
            fourRatingButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("4"));
            fiveRatingButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("5"));
        }
    }
}
using UnityEngine;
using UnityEngine.UI;

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
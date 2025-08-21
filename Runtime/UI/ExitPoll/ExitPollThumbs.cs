using UnityEngine;
using UnityEngine.UI;

namespace AbxrLib
{
	public class ExitPollThumbs : MonoBehaviour
{
    public Button thumbsUpButton;
    public Button thumbsDownButton;
    
    private void Start()
    {
        thumbsUpButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("up"));
        thumbsDownButton.onClick.AddListener(() => ExitPollHandler.OnButtonClicked("down"));
    }
}}

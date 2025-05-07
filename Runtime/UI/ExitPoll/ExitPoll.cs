using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExitPoll : MonoBehaviour
{
    public static ExitPoll Instance { get; private set; }
    
    private const string PollEventString = "poll";
    private const string PollResponseString = "answer";
    private const string PollQuestionString = "prompt";
    
    public Button thumbsUpButton;
    public Button thumbsDownButton;
    public Button oneRatingButton;
    public Button twoRatingButton;
    public Button threeRatingButton;
    public Button fourRatingButton;
    public Button fiveRatingButton;

    private static GameObject _panelInstance;
    private static TextMeshProUGUI _prompt;

    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        thumbsUpButton?.onClick.AddListener(OnThumbsUpClick);
        thumbsDownButton?.onClick.AddListener(OnThumbsDownClick);
        oneRatingButton?.onClick.AddListener(OnOneRatingClick);
        twoRatingButton?.onClick.AddListener(OnTwoRatingClick);
        threeRatingButton?.onClick.AddListener(OnThreeRatingClick);
        fourRatingButton?.onClick.AddListener(OnFourRatingClick);
        fiveRatingButton?.onClick.AddListener(OnFiveRatingClick);
    }

    public static void CreatePanel(GameObject prefab, string prompt)
    {
        _panelInstance = Instantiate(prefab);
        _prompt = _panelInstance.GetComponentInChildren<TextMeshProUGUI>();
        _prompt.text = prompt;
    }

    private void Destroy()
    {
        Destroy(gameObject);
        Destroy(_panelInstance);
    }
    
    private void OnThumbsUpClick()
    {
        PollEvent(_prompt.text, "up");
        Destroy();
    }
    
    private void OnThumbsDownClick()
    {
        PollEvent(_prompt.text, "down");
        Destroy();
    }

    private void OnOneRatingClick()
    {
        PollEvent(_prompt.text, "1");
        Destroy();
    }
    
    private void OnTwoRatingClick()
    {
        PollEvent(_prompt.text, "2");
        Destroy();
    }
    
    private void OnThreeRatingClick()
    {
        PollEvent(_prompt.text, "3");
        Destroy();
    }
    
    private void OnFourRatingClick()
    {
        PollEvent(_prompt.text, "4");
        Destroy();
    }
    
    private void OnFiveRatingClick()
    {
        PollEvent(_prompt.text, "5");
        Destroy();
    }
    
    private static void PollEvent(string prompt, string response)
    {
        Abxr.Event(PollEventString, new Dictionary<string, string>
        {
            [PollQuestionString] = prompt,
            [PollResponseString] = response
        });
        ExitPollHandler.ProcessNextPoll();
    }
}
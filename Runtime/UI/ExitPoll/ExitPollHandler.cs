using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExitPollHandler : MonoBehaviour
{
    private static GameObject _panelPrefab;
    private static GameObject _ratingPrefab;
    private static GameObject _thumbsPrefab;
    private static GameObject _multiPrefab;
    private static GameObject _multiButtonPrefab;
    private static GameObject _pollInstance;
    private static GameObject _panelInstance;
    
    private const string PollEventString = "poll";
    private const string PollResponseString = "answer";
    private const string PollQuestionString = "prompt";

    private static string _prompt;
    
    public enum PollType
    {
        Thumbs,
        Rating,
        MultipleChoice
    }
    
    private static readonly List<Tuple<string, PollType>> Polls = new();
    private static readonly Dictionary<string, List<string>> Responses = new();
    private static bool _isProcessing;

    private void Start()
    {
        _ratingPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollRating");
        _thumbsPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollThumbs");
        _multiPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollMulti");
        _multiButtonPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollMultiButton");
        _panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
        if (_ratingPrefab == null)
        {
            Debug.LogError("AbxrLib - Failed to load exit poll prefab");
        }
    }
    
    public static void AddPoll(string prompt, PollType pollType, List<string> responses)
    {
        Polls.Add(new Tuple<string, PollType>(prompt, pollType));
        if (responses != null) Responses[prompt] = responses;
        
        if (!_isProcessing) ProcessPoll();
    }

    private static void ProcessNextPoll()
    {
        if (Polls.Count > 0) ProcessPoll();
        _isProcessing = false;
    }

    private static void CreatePoll(PollType pollType, string prompt)
    {
        if (pollType == PollType.Rating)
        {
            _pollInstance = Instantiate(_ratingPrefab);
        }
        else if (pollType == PollType.Thumbs)
        {
            _pollInstance = Instantiate(_thumbsPrefab);
        }
        else if (pollType == PollType.MultipleChoice)
        {
            _pollInstance = Instantiate(_multiPrefab);
            Transform panel = _pollInstance.transform.Find("Panel");
            RectTransform panelTransform = panel.GetComponentInChildren<RectTransform>();
            float panelShift = 0.06f - (Responses[prompt].Count - 2) * 0.03f;
            panelTransform.transform.position += new Vector3(0, panelShift, 0);
            foreach (var response in Responses[prompt])
            {
                AddButton(response, panelTransform);
            }
        }
    }
    
    private static void CreatePanel(GameObject prefab, string prompt)
    {
        _panelInstance = Instantiate(prefab);
        TextMeshProUGUI panelText = _panelInstance.GetComponentInChildren<TextMeshProUGUI>();
        panelText.text = prompt;
        _prompt = prompt;
    }

    private static void ProcessPoll()
    {
        _isProcessing = true;

        Tuple<string, PollType> poll = Polls[0];
        string prompt = poll.Item1;
        CreatePanel(_panelPrefab, prompt);
        CreatePoll(poll.Item2, prompt);
        Polls.RemoveAt(0);
        Responses.Remove(prompt);
    }
    
    private static void AddButton(string response, RectTransform panel)
    {
        GameObject newButtonObj = Instantiate(_multiButtonPrefab, panel);
        Button btn = newButtonObj.GetComponent<Button>();
        var textObj = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
        textObj.text = response;
        btn.onClick.AddListener(() => OnButtonClicked(response));
    }

    public static void OnButtonClicked(string response)
    {
        Destroy(_pollInstance);
        Destroy(_panelInstance);
        Abxr.Event(PollEventString, new Dictionary<string, string>
        {
            [PollQuestionString] = _prompt,
            [PollResponseString] = response
        });
        ProcessNextPoll();
    }
}
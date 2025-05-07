using System;
using System.Collections.Generic;
using UnityEngine;

public class ExitPollHandler : MonoBehaviour
{
    private static GameObject _panelPrefab;
    private static GameObject _ratingPrefab;
    private static GameObject _thumbsPrefab;
    
    public enum PollType
    {
        Thumbs,
        Rating
    }
    
    private static readonly List<Tuple<string, PollType>> Polls = new();
    private static bool _isProcessing;

    private void Start()
    {
        _ratingPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollRating");
        _thumbsPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollThumbs");
        _panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
        if (_ratingPrefab == null)
        {
            Debug.LogError("AbxrLib - Failed to load exit poll prefab");
        }
    }
    
    public static void AddPoll(string prompt, PollType pollType)
    {
        Polls.Add(new Tuple<string, PollType>(prompt, pollType));
        
        if (!_isProcessing) ProcessPoll();
    }

    public static void ProcessNextPoll()
    {
        if (Polls.Count > 0) ProcessPoll();
        _isProcessing = false;
    }

    private static void CreatePoll(PollType pollType, string prompt)
    {
        if (pollType == PollType.Rating)
        {
            Instantiate(_ratingPrefab);
        }
        else if (pollType == PollType.Thumbs)
        {
            Instantiate(_thumbsPrefab);
        }
        
        ExitPoll.CreatePanel(_panelPrefab, prompt);
    }

    private static void ProcessPoll()
    {
        _isProcessing = true;

        Tuple<string, PollType> poll = Polls[0];
        CreatePoll(poll.Item2, poll.Item1);
        Polls.RemoveAt(0);
    }
}
using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.UI.Keyboard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.ExitPoll
{
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
        private static readonly Dictionary<string, Action<string>> Callbacks = new();
        private static bool _isProcessing;

        private void Start()
        {
            KeyboardHandler.OnKeyboardCreated += PauseExitPolling;
            KeyboardHandler.OnKeyboardDestroyed += ResumeExitPolling;
            _ratingPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollRating" + RigDetector.PrefabSuffix());
            _thumbsPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollThumbs" + RigDetector.PrefabSuffix());
            _multiPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollMulti" + RigDetector.PrefabSuffix());
            _multiButtonPrefab = Resources.Load<GameObject>("Prefabs/AbxrExitPollMultiButton");
            _panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
            if (!_ratingPrefab)
            {
                Debug.LogError("AbxrLib - Failed to load exit poll prefab");
            }
        }

        private void OnDisable()
        {
            KeyboardHandler.OnKeyboardCreated -= PauseExitPolling;
            KeyboardHandler.OnKeyboardDestroyed -= ResumeExitPolling;
        }
    
        public static void AddPoll(string prompt, PollType pollType, List<string> responses, Action<string> callback)
        {
            Polls.Add(new Tuple<string, PollType>(prompt, pollType));
            if (responses != null) Responses[prompt] = responses;
            if (callback != null) Callbacks[prompt] = callback;
        
            if (!_isProcessing) ProcessPoll();
        }

        private static void ProcessNextPoll()
        {
            if (Polls.Count > 0) ProcessPoll();
            _isProcessing = false;
        }

        private static void CreatePoll(PollType pollType)
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
                float panelShift = 0.02f - (Responses[_prompt].Count - 2) * 0.03f;
                panelTransform.transform.position += new Vector3(0, panelShift, 0);
                foreach (var response in Responses[_prompt])
                {
                    AddButton(response, panelTransform);
                }
            }
        }
    
        private static void CreatePanel(GameObject prefab)
        {
            _panelInstance = Instantiate(prefab);
            TextMeshProUGUI panelText = _panelInstance.GetComponentInChildren<TextMeshProUGUI>();
            panelText.text = _prompt;
        }

        private static void ProcessPoll()
        {
            _isProcessing = true;

            Tuple<string, PollType> poll = Polls[0];
            _prompt = poll.Item1;
            CreatePanel(_panelPrefab);
            CreatePoll(poll.Item2);
            Polls.RemoveAt(0);
            Responses.Remove(_prompt);
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
            if (Callbacks.TryGetValue(_prompt, out var callback)) callback.Invoke(response);
            Abxr.Event(PollEventString, new Dictionary<string, string>
            {
                [PollQuestionString] = _prompt,
                [PollResponseString] = response
            });
            ProcessNextPoll();
        }
    
        private static void PauseExitPolling()
        {
            _isProcessing = true;
            if (_pollInstance) _pollInstance.SetActive(false);
            if (_panelInstance) _panelInstance.SetActive(false);
        }

        private static void ResumeExitPolling()
        {
            if (_pollInstance) _pollInstance.SetActive(true);
            if (_panelInstance) _panelInstance.SetActive(true);
            if (!_pollInstance) ProcessNextPoll();
        }
    }
}
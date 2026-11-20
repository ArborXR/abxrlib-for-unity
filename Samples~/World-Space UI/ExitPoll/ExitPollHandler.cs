using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Types;
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

        private static readonly PollQueue Polls = new(ShowPoll, OnPollsDone);

        /// <summary>True while a poll is up, including one hidden behind the keyboard.</summary>
        public static bool IsOpen => _pollInstance;

        /// <summary>Runs coroutines for LaserPointerManager when no keyboard is up. Lives under the DontDestroyOnLoad root.</summary>
        internal static ExitPollHandler Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
                Logcat.Error("Failed to load exit poll prefab");
            }
        }

        private void OnDisable()
        {
            KeyboardHandler.OnKeyboardCreated -= PauseExitPolling;
            KeyboardHandler.OnKeyboardDestroyed -= ResumeExitPolling;
        }
    
        public static void AddPoll(string prompt, PollType pollType, List<string> responses, Action<string> callback)
        {
            Polls.Add(new QueuedPoll(prompt, pollType, responses, callback));
        }

        private static void OnPollsDone()
        {
            // The answer's callback may have opened a keyboard, which still needs the rays.
            if (!KeyboardHandler.IsOpen) LaserPointerManager.RestoreLaserPointerStates();
        }

        /// <summary>Clears what an Editor play session left behind when domain reload is off: its polls died with it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Polls.Clear();
            _pollInstance = null;
            _panelInstance = null;
        }

        private static void CreatePoll(QueuedPoll poll)
        {
            if (poll.Type == PollType.Rating)
            {
                _pollInstance = Instantiate(_ratingPrefab);
            }
            else if (poll.Type == PollType.Thumbs)
            {
                _pollInstance = Instantiate(_thumbsPrefab);
            }
            else if (poll.Type == PollType.MultipleChoice)
            {
                _pollInstance = Instantiate(_multiPrefab);
                Transform panel = _pollInstance.transform.Find("Panel");
                RectTransform panelTransform = panel.GetComponentInChildren<RectTransform>();
                float panelShift = 0.02f - (poll.Responses.Count - 2) * 0.03f;
                panelTransform.transform.position += new Vector3(0, panelShift, 0);
                foreach (var response in poll.Responses)
                {
                    AddButton(response, panelTransform);
                }
            }
            if (!_pollInstance) return;
            // An open poll must outlive a scene load: it holds the queue until it's answered.
            DontDestroyOnLoad(_pollInstance);
            // Like the keyboard: the _Default and _Meta prefabs carry no tracked-device raycaster, and the rig's rays
            // may be off.
            LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_pollInstance);
            LaserPointerManager.EnableLaserPointersForInteraction();
            EventSystemFallback.Ensure();
        }
    
        private static void CreatePanel(GameObject prefab, string prompt)
        {
            _panelInstance = Instantiate(prefab);
            DontDestroyOnLoad(_panelInstance); // Outlives a scene load, like the poll.
            // The backdrop has nothing to press, and on a flat screen its raycaster would compete with the poll's for taps.
            foreach (var raycaster in _panelInstance.GetComponentsInChildren<GraphicRaycaster>()) raycaster.enabled = false;
            TextMeshProUGUI panelText = _panelInstance.GetComponentInChildren<TextMeshProUGUI>();
            panelText.text = prompt;
        }

        // The poll and its backdrop share a plane, so left to distance the backdrop can draw over the poll: on a flat
        // screen, where both are centred on the view, it does.
        private static void DrawPollOverBackdrop()
        {
            if (!_pollInstance || !_panelInstance) return;
            Canvas pollCanvas = _pollInstance.GetComponentInChildren<Canvas>();
            Canvas panelCanvas = _panelInstance.GetComponentInChildren<Canvas>();
            if (pollCanvas && panelCanvas) pollCanvas.sortingOrder = panelCanvas.sortingOrder + 1;
        }

        // Draws the queue's current poll. One that can't be drawn leaves nothing behind: a backdrop with no poll on it
        // can't be answered, so it would sit there and hold up every poll after it.
        private static bool ShowPoll(QueuedPoll poll)
        {
            // Destroy waits for the end of the frame, so the poll just answered still looks alive here.
            _pollInstance = null;
            _panelInstance = null;
            try
            {
                CreatePanel(_panelPrefab, poll.Prompt);
                CreatePoll(poll);
                if (!_pollInstance)
                {
                    Logcat.Error($"Couldn't show the poll \"{poll.Prompt}\": no {poll.Type} poll prefab");
                    DestroyInstances();
                    return false;
                }
                FaceCamera.FitTogether(_panelInstance, _pollInstance); // The poll sits on its backdrop, so they fit as one.
                DrawPollOverBackdrop();
                return true;
            }
            catch
            {
                DestroyInstances();
                throw; // The queue logs it and moves on to the next poll.
            }
        }

        private static void DestroyInstances()
        {
            if (_pollInstance) Destroy(_pollInstance);
            if (_panelInstance) Destroy(_panelInstance);
            _pollInstance = null;
            _panelInstance = null;
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
            QueuedPoll answered = Polls.Answer(response);
            // The SDK records the answer to the app's PollUser, so AbxrObserver reports it as automatic.
            using (AbxrObserver.AutomaticScope())
            {
                Abxr.Event(PollEventString, new Dictionary<string, string>
                {
                    [PollQuestionString] = answered?.Prompt,
                    [PollResponseString] = response
                });
            }
            Polls.ShowNext();
        }
    
        private static void PauseExitPolling()
        {
            Polls.Pause();
            if (_pollInstance) _pollInstance.SetActive(false);
            if (_panelInstance) _panelInstance.SetActive(false);
        }

        private static void ResumeExitPolling()
        {
            if (_panelInstance) _panelInstance.SetActive(true);
            if (_pollInstance)
            {
                _pollInstance.SetActive(true);
                // Closing the keyboard put the rays back the way it found them.
                LaserPointerManager.EnableLaserPointersForInteraction();
            }
            else Polls.ShowNext();
        }
    }
}
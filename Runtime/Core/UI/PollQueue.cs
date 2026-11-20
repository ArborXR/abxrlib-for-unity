/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 *
 * The queue behind the World-Space UI's exit poll: shows the polls an app asks for one at a time, in order.
 *
 * Each poll keeps its own responses and callback. They used to be kept in dictionaries keyed by the question, so the
 * same multiple-choice question asked twice lost its responses when the first copy opened, and the second copy threw
 * and left the queue stuck.
 */

using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Core.UI
{
    /// <summary>A poll an app asked for, with its own responses and callback.</summary>
    internal sealed class QueuedPoll
    {
        public readonly string Prompt;
        public readonly PollType Type;
        public readonly List<string> Responses;
        public readonly Action<string> Callback;

        public QueuedPoll(string prompt, PollType type, List<string> responses, Action<string> callback)
        {
            Prompt = prompt;
            Type = type;
            Responses = responses;
            Callback = callback;
        }
    }

    /// <summary>
    /// Shows queued polls one at a time. It holds no UI: <c>show</c> draws a poll and says whether it could, and
    /// <c>onIdle</c> runs when the last one is done.
    /// </summary>
    internal sealed class PollQueue
    {
        private readonly Queue<QueuedPoll> _waiting = new();
        private readonly Func<QueuedPoll, bool> _show;
        private readonly Action _onIdle;

        /// <summary>The poll on screen, or null.</summary>
        public QueuedPoll Current { get; private set; }

        /// <summary>True while a poll is up or polling is paused. A poll added then waits its turn.</summary>
        public bool IsBusy { get; private set; }

        public int Waiting => _waiting.Count;

        public PollQueue(Func<QueuedPoll, bool> show, Action onIdle)
        {
            _show = show;
            _onIdle = onIdle;
        }

        public void Add(QueuedPoll poll)
        {
            _waiting.Enqueue(poll);
            if (!IsBusy) ShowNext();
        }

        /// <summary>
        /// Runs the current poll's callback with the answer and returns that poll. Call <see cref="ShowNext"/> after.
        /// A callback that throws is logged, so the app's bug can't stop the answer being recorded or the next poll opening.
        /// </summary>
        public QueuedPoll Answer(string response)
        {
            QueuedPoll answered = Current;
            Current = null;
            try
            {
                answered?.Callback?.Invoke(response);
            }
            catch (Exception e)
            {
                Logcat.Error($"The callback for the poll \"{answered.Prompt}\" threw: {e}");
            }
            return answered;
        }

        /// <summary>
        /// Shows the next poll that can be shown. A poll that can't (its <c>show</c> returned false or threw) is dropped,
        /// so it can't hold up the ones behind it. With none left, the queue goes idle.
        /// </summary>
        public void ShowNext()
        {
            while (_waiting.Count > 0)
            {
                Current = _waiting.Dequeue();
                IsBusy = true;
                bool shown;
                try
                {
                    shown = _show(Current);
                }
                catch (Exception e)
                {
                    Logcat.Error($"Couldn't show the poll \"{Current.Prompt}\": {e}");
                    shown = false;
                }
                if (shown) return;
                Current = null;
            }
            Current = null;
            IsBusy = false;
            _onIdle?.Invoke();
        }

        /// <summary>
        /// Holds new polls back, for example while the keyboard is up. The current poll stays current. Polls show again
        /// when the current one is answered, or on <see cref="ShowNext"/> when there's none on screen.
        /// </summary>
        public void Pause() => IsBusy = true;

        public void Clear()
        {
            _waiting.Clear();
            Current = null;
            IsBusy = false;
        }
    }
}

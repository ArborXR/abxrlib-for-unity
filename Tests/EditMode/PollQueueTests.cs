// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the queue behind the World-Space UI's exit poll (SDK-96). The same multiple-choice question asked
// twice used to lose its responses when the first copy opened; the second copy then threw, left its backdrop up and
// stuck the queue, so no later poll opened.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class PollQueueTests
{
    private const string Question = "What should we cover next?";

    private List<QueuedPoll> _shown;
    private int _idle;
    private Func<QueuedPoll, bool> _show;
    private PollQueue _queue;

    [SetUp]
    public void SetUp()
    {
        _shown = new List<QueuedPoll>();
        _idle = 0;
        _show = poll => { _shown.Add(poll); return true; };
        _queue = new PollQueue(poll => _show(poll), () => _idle++);
    }

    private static QueuedPoll Poll(string prompt, List<string> responses = null, Action<string> callback = null) =>
        new(prompt, responses == null ? PollType.Thumbs : PollType.MultipleChoice, responses, callback);

    [Test]
    public void TheSameQuestionTwice_ShowsBoth_EachWithItsOwnResponsesAndCallback()
    {
        string first = null, second = null;
        var firstResponses = new List<string> { "Fire safety", "First aid" };
        var secondResponses = new List<string> { "Ladders", "Lifting", "Lockout" };
        _queue.Add(Poll(Question, firstResponses, a => first = a));
        _queue.Add(Poll(Question, secondResponses, a => second = a));

        Assert.AreEqual(1, _shown.Count, "only the first poll should be up");
        Assert.AreSame(firstResponses, _queue.Current.Responses);

        _queue.Answer("First aid");
        _queue.ShowNext();

        Assert.AreEqual("First aid", first);
        Assert.IsNull(second, "the first answer reached the second poll's callback");
        Assert.AreEqual(2, _shown.Count, "the second poll didn't open");
        Assert.AreSame(secondResponses, _queue.Current.Responses, "the second poll lost its responses");

        _queue.Answer("Lifting");
        _queue.ShowNext();

        Assert.AreEqual("First aid", first, "the second answer reached the first poll's callback");
        Assert.AreEqual("Lifting", second);
        Assert.IsNull(_queue.Current);
        Assert.IsFalse(_queue.IsBusy);
        Assert.AreEqual(1, _idle);
    }

    [Test]
    public void TheSameQuestionTwice_QueuedBehindAnotherPoll_EachShowsItsOwnResponses()
    {
        // How the headset hit it: both copies waited behind another poll. The old handler showed the first copy with
        // the second copy's responses, removed them, and then had none for the second copy.
        var firstResponses = new List<string> { "Fire safety", "First aid" };
        var secondResponses = new List<string> { "Ladders", "Lifting", "Lockout" };
        _queue.Add(Poll("How was it?"));
        _queue.Add(Poll(Question, firstResponses));
        _queue.Add(Poll(Question, secondResponses));

        _queue.Answer("up");
        _queue.ShowNext();
        Assert.AreSame(firstResponses, _queue.Current.Responses, "the first copy showed the second copy's responses");

        _queue.Answer("First aid");
        _queue.ShowNext();
        Assert.AreSame(secondResponses, _queue.Current.Responses, "the second copy lost its responses");
    }

    [Test]
    public void Answer_ReturnsThePollItAnswered()
    {
        var poll = Poll(Question);
        _queue.Add(poll);

        Assert.AreSame(poll, _queue.Answer("yes"));
        Assert.IsNull(_queue.Current);
    }

    [Test]
    public void APollAddedWhileOneIsUp_WaitsForTheAnswer()
    {
        _queue.Add(Poll("first"));
        _queue.Add(Poll("second"));

        CollectionAssert.AreEqual(new[] { "first" }, _shown.ConvertAll(p => p.Prompt));
        Assert.AreEqual(1, _queue.Waiting);
    }

    [Test]
    public void APollThatThrowsWhenShown_IsDropped_AndTheNextOneShows()
    {
        _show = poll =>
        {
            if (poll.Prompt == "broken") throw new KeyNotFoundException("no responses");
            _shown.Add(poll);
            return true;
        };
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Couldn't show the poll \"broken\"")));

        _queue.Add(Poll("first"));
        _queue.Add(Poll("broken"));
        _queue.Add(Poll("third"));
        _queue.Answer("yes");
        _queue.ShowNext();

        Assert.AreEqual("third", _queue.Current.Prompt);
        CollectionAssert.AreEqual(new[] { "first", "third" }, _shown.ConvertAll(p => p.Prompt));
    }

    [Test]
    public void TheLastPollFailingToShow_LeavesTheQueueIdle()
    {
        _show = _ => false;

        _queue.Add(Poll("broken"));

        Assert.IsNull(_queue.Current);
        Assert.IsFalse(_queue.IsBusy, "a poll that couldn't be shown left the queue stuck");
        Assert.AreEqual(1, _idle);

        _show = poll => { _shown.Add(poll); return true; };
        _queue.Add(Poll("next"));
        Assert.AreEqual("next", _queue.Current.Prompt, "a poll added after the failure didn't open");
    }

    [Test]
    public void Paused_HoldsNewPollsUntilShowNext()
    {
        _queue.Pause();
        _queue.Add(Poll("held"));

        Assert.IsEmpty(_shown);
        Assert.IsTrue(_queue.IsBusy);

        _queue.ShowNext();
        Assert.AreEqual("held", _queue.Current.Prompt);
    }

    [Test]
    public void APollAddedByACallback_ShowsAfterIt()
    {
        _queue.Add(Poll("first", callback: _ => _queue.Add(Poll("follow-up"))));

        _queue.Answer("yes");
        Assert.AreEqual(1, _shown.Count, "the follow-up opened before the first poll was done");

        _queue.ShowNext();
        Assert.AreEqual("follow-up", _queue.Current.Prompt);
    }
}

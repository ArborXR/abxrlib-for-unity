// Copyright (c) 2026 ArborXR. All rights reserved.
// Verifies what an authentication input request does in a core-only project. The package test project
// never imports the World-Space UI sample, so with no app OnInputRequested handler either, the request
// has no way to reach the user. These tests pin down that this dead end is loud (a one-time warning)
// rather than a silent auth hang - the exact failure the core/UI split must never reintroduce.
// They also pin down when ending a session closes the sign-in prompt, using a registered stand-in UI.
using System.Text.RegularExpressions;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AuthInputUiTests : AbxrPlayModeTestBase
{
    /// <summary>
    /// Fires an input request through the auth service's wired callback - the same path a real backend
    /// request takes (auth service -> subsystem dispatch -> PresentKeyboard) - after clearing the handler
    /// the base SetUp assigned and every AbxrUi registration, so the subsystem sees a core-only project.
    /// </summary>
    private static void RequestInputWithNothingToShowIt(string type)
    {
        Abxr.OnInputRequested = null;
        AbxrUi.ResetForTesting();
        Assert.IsNull(AbxrUi.AuthUi, "The package test project must not have an auth UI registered.");

        AbxrSubsystem.Instance.AuthServiceForTesting.OnInputRequested?.Invoke(type, "PIN", "", "");
    }

    [Test]
    public void AuthInputRequest_WithNoAuthUiAndNoHandler_WarnsThatProjectCannotAsk()
    {
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("no way to ask")));
        RequestInputWithNothingToShowIt("assessmentPin");
    }

    [Test]
    public void AuthInputRequest_WithNoAuthUiAndNoHandler_DoesNotThrow()
    {
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("no way to ask")));
        Assert.DoesNotThrow(() => RequestInputWithNothingToShowIt("text"));
    }

    [Test]
    public void AuthInputRequest_HandlerAssignedAfterDroppedRequest_ReceivesTheRequest()
    {
        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("no way to ask")));
        RequestInputWithNothingToShowIt("assessmentPin");

        // Invoking the wired callback above bypassed the service's own request bookkeeping, so mark the
        // request pending the way a real RequestInput would have - the replay is gated on it.
        AbxrSubsystem.Instance.AuthServiceForTesting.SetInputRequestPendingForTesting(true);

        int calls = 0;
        string receivedType = null;
        Abxr.OnInputRequested = (type, prompt, domain, error) => { calls++; receivedType = type; };

        Assert.AreEqual(1, calls, "Assigning a handler while a dropped request is still pending must replay it.");
        Assert.AreEqual("assessmentPin", receivedType);

        // The stash is consumed by the replay: a later handler assignment must not receive it again.
        Abxr.OnInputRequested = (type, prompt, domain, error) => { calls++; };
        Assert.AreEqual(1, calls, "A replayed request must not replay a second time.");
    }

    // ── Closing the SDK's sign-in prompt when its session is cleared ────────
    // The World-Space UI prompt outlives scene loads, so one the session no longer waits on would stay on screen for good.

    /// <summary>Registers a UI and sends a sign-in request through the auth service's wired callback, as the backend would.</summary>
    private static CountingAuthUi ShowSignInPromptInSdkUi()
    {
        var ui = new CountingAuthUi();
        AbxrUi.RegisterAuthUi(ui);
        Abxr.OnInputRequested = null; // no app handler, so the SDK's UI shows the request
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        auth.OnInputRequested?.Invoke("assessmentPin", "PIN", "", "");
        auth.SetInputRequestPendingForTesting(true);
        Assert.AreEqual(1, ui.ShowCalls, "The SDK's UI should have shown the request.");
        return ui;
    }

    [Test]
    public void EndSession_WithASignInPromptOpen_ClosesIt()
    {
        var ui = ShowSignInPromptInSdkUi();
        try
        {
            Abxr.EndSession();

            Assert.AreEqual(1, ui.HideCalls);
            Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsInputRequestPending);
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    [Test]
    public void EndSession_WhileASignInSubmitIsProcessing_ClosesThePrompt()
    {
        // After Submit the request is no longer pending, but the prompt still shows Processing, and the cleared session
        // drops the answer that would have closed it.
        var ui = ShowSignInPromptInSdkUi();
        var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
        try
        {
            auth.SetInputRequestPendingForTesting(false);
            auth.SetUserAuthSubmitInFlightForTesting(true);

            Abxr.EndSession();

            Assert.AreEqual(1, ui.HideCalls);
            Assert.IsFalse(auth.IsUserAuthSubmitInFlight);
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    [Test]
    public void EndSession_FromAnOnAuthCompletedHandlerAfterAFailedSubmit_ClosesThePrompt()
    {
        // The failed submit reports through OnAuthCompleted(false) before it would ask again; an app that ends the session
        // there must still get the prompt closed, since the cleared session won't ask again.
        // This fixture never signs in; a cleared session has the (empty) auth mechanism a submit reads.
        AbxrSubsystem.Instance.AuthServiceForTesting.ClearSessionAndPrepareForNew();
        var ui = ShowSignInPromptInSdkUi();
        Abxr.OnAuthCompleted += (success, error) => { if (!success) Abxr.EndSession(); };
        // No sign-in attempt is running in this fixture, so the submit fails at once and logs the failure.
        LogAssert.ignoreFailingMessages = true;
        try
        {
            Abxr.OnInputSubmitted("123456");

            Assert.AreEqual(1, ui.HideCalls);
            Assert.IsFalse(AbxrSubsystem.Instance.AuthServiceForTesting.IsInputRequestPending, "The ended session must not ask again.");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            AbxrUi.UnregisterAuthUi(ui);
        }
    }

    [Test]
    public void EndSession_WithAHandlerAssignedAfterTheSdkShowedThePrompt_StillClosesIt()
    {
        // Assigning OnInputRequested while the SDK's prompt is up leaves the request with that prompt (the setter only
        // replays a dropped request), so the SDK must still close it.
        var ui = ShowSignInPromptInSdkUi();
        try
        {
            Abxr.OnInputRequested = (type, prompt, domain, error) => { };

            Abxr.EndSession();

            Assert.AreEqual(1, ui.HideCalls);
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    [Test]
    public void EndSession_WithTheRequestShownByTheAppsHandler_LeavesTheUiAlone()
    {
        var ui = new CountingAuthUi();
        AbxrUi.RegisterAuthUi(ui);
        try
        {
            // The app's own handler receives the request, so it isn't the SDK's UI to close.
            Abxr.OnInputRequested = (type, prompt, domain, error) => { };
            var auth = AbxrSubsystem.Instance.AuthServiceForTesting;
            auth.OnInputRequested?.Invoke("assessmentPin", "PIN", "", "");
            auth.SetInputRequestPendingForTesting(true);

            Abxr.EndSession();

            Assert.AreEqual(0, ui.ShowCalls);
            Assert.AreEqual(0, ui.HideCalls);
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    [Test]
    public void EndSession_WithNoPromptOpen_LeavesTheUiAlone()
    {
        var ui = new CountingAuthUi();
        AbxrUi.RegisterAuthUi(ui);
        Abxr.OnInputRequested = null;
        try
        {
            Abxr.EndSession();

            Assert.AreEqual(0, ui.HideCalls, "Nothing was waiting on input, so an app's own use of the UI is not the SDK's to close.");
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    [Test]
    public void AppQuit_WithASignInPromptOpen_LeavesTheUiToGoWithTheApp()
    {
        // Closing it at quit would only let a poll queued behind it open while everything shuts down.
        var ui = ShowSignInPromptInSdkUi();
        try
        {
            AbxrSubsystem.Instance.OnApplicationQuitHandler();

            Assert.AreEqual(0, ui.HideCalls);
        }
        finally { AbxrUi.UnregisterAuthUi(ui); }
    }

    private sealed class CountingAuthUi : IAbxrAuthUi
    {
        public int ShowCalls { get; private set; }
        public int HideCalls { get; private set; }
        public void Show(AuthUiKind kind) => ShowCalls++;
        public void SetPrompt(string prompt) { }
        public void Hide() => HideCalls++;
        public void StopProcessing() { }
    }
}

// Copyright (c) 2026 ArborXR. All rights reserved.
// Verifies what an authentication input request does in a core-only project. The package test project
// never imports the World-Space UI sample, so with no app OnInputRequested handler either, the request
// has no way to reach the user. These tests pin down that this dead end is loud (a one-time warning)
// rather than a silent auth hang - the exact failure the core/UI split must never reintroduce.
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
}

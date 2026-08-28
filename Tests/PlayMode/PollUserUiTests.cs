// Copyright (c) 2026 ArborXR. All rights reserved.
// Verifies what Abxr.PollUser does in a core-only project. The package test project never imports
// the World-Space UI sample, so no IAbxrPollUi is registered and a queued poll has nowhere to go.
// The poll is dropped by design; these tests pin down that the drop is loud (a one-time warning)
// rather than silent.
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class PollUserUiTests : AbxrPlayModeTestBase
{
    [Test]
    public void PollUser_WithNoPollUiRegistered_WarnsThatPollWasDropped()
    {
        // The base SetUp created a live subsystem; drop every AbxrUi registration (and the warn-once
        // latch, which other tests may have tripped) so this test sees the same state as a core-only
        // project on its first poll.
        AbxrUi.ResetForTesting();
        Assert.IsNull(AbxrUi.PollUi, "The package test project must not have a poll UI registered.");

        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Abxr.PollUser")));
        Abxr.PollUser("Was this training helpful?", PollType.Thumbs);
    }

    [Test]
    public void PollUser_WithNoPollUiRegistered_DoesNotThrow()
    {
        AbxrUi.ResetForTesting();

        LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Abxr.PollUser")));
        Assert.DoesNotThrow(() => Abxr.PollUser("Rate this experience.", PollType.Rating));
    }
}

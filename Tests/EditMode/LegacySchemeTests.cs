// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the latch an open inspector or wizard uses to keep a control up once it has shown (SDK-87, SDK-89).
using AbxrLib.Editor;
using NUnit.Framework;

[TestFixture]
public class LegacySchemeTests
{
    // ── The latch ─────────────────────────────────────────────────

    [Test]
    public void ShownLatch_HoldsFromTheFirstTrue_UntilReset()
    {
        var latch = new SetupWizardChecks.ShownLatch();

        Assert.IsFalse(latch.Holds(false));
        Assert.IsTrue(latch.Holds(true));
        Assert.IsTrue(latch.Holds(false), "What showed once stays for the life of the window.");
        latch.Reset();
        Assert.IsFalse(latch.Holds(false));
    }
}

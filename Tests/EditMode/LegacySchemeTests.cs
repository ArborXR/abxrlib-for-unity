// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for when the Configuration inspector and the Setup Wizard show the legacy scheme (SDK-89): its
// fields and the Use App Tokens checkbox show only in a project that is already on it. Also the latch an open
// inspector or wizard uses to keep a control up once it has shown (SDK-87, SDK-89).
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class LegacySchemeTests
{
    private const string AppId = "12345678-1234-1234-1234-123456789012";
    private const string OrgId = "87654321-4321-4321-4321-210987654321";

    private AppConfig _config;

    [SetUp]
    public void SetUp() => _config = ScriptableObject.CreateInstance<AppConfig>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    // ── The rule ──────────────────────────────────────────────────

    [Test]
    public void ANewProject_IsNotShownTheLegacyScheme()
    {
        Assert.IsTrue(_config.useAppTokens, "A new configuration starts on App Tokens.");
        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsFalse(new SetupWizardChecks.ShownLatch().Holds(SetupWizardChecks.ShowsLegacyScheme(_config)), "No checkbox either.");
    }

    [TestCase("production")]
    [TestCase("development")]
    [TestCase("production_custom")]
    public void AProjectSavedOnTheLegacyScheme_IsShownIt(string buildType)
    {
        _config.useAppTokens = false;
        _config.buildType = buildType;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config));
    }

    [Test]
    public void ALegacyProjectWithNothingSavedYet_IsStillShownIt()
    {
        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config), "Otherwise it could never be filled in or switched.");
    }

    [Test]
    public void AProjectOnAppTokens_WithLegacyValuesStillSaved_IsNotShownTheLegacyScheme()
    {
        // Pins what the rule does today. The values still ship inside the build; see SDK-89 for what to do about them.
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config));
    }

    [Test]
    public void WithoutAConfig_TheLegacySchemeDoesNotShow()
    {
        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(null));
    }

    // ── Switching in an open window ───────────────────────────────

    [Test]
    public void TickingUseAppTokens_KeepsTheCheckboxUntilTheWindowCloses()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config)));

        _config.useAppTokens = true;

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config), "The legacy fields give way to the App Token.");
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config)), "A mistaken tick can be undone.");
        checkbox.Reset();
        Assert.IsFalse(checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config)), "A window opened after that offers no way back.");
    }

    [Test]
    public void UntickingAgain_BringsTheLegacyFieldsBack()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config));
        _config.useAppTokens = true;
        checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config));

        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsLegacyScheme(_config)));
    }

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

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
        // The fields stay hidden. The values still ship inside the build, which the leftover warning below says.
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config));
    }

    [Test]
    public void TheLegacyNote_SaysTheSchemeIsLegacy_AndThatNewProjectsUseAnAppToken()
    {
        StringAssert.Contains("legacy scheme", SetupWizardChecks.LegacySchemeNote);
        StringAssert.Contains("new projects use an App Token", SetupWizardChecks.LegacySchemeNote);
        StringAssert.Contains("keeps working", SetupWizardChecks.LegacySchemeNote);
    }

    [Test]
    public void WithoutAConfig_TheLegacySchemeDoesNotShow()
    {
        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(null));
    }

    // ── Legacy values left on a project that uses App Tokens ──────

    [Test]
    public void LeftoverLegacyValues_NamesEachSavedValue()
    {
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        CollectionAssert.AreEqual(new[] { "Application ID", "Organization ID", "Authorization Secret" },
            SetupWizardChecks.LeftoverLegacyValues(_config));
    }

    [TestCase(AppId, null, null, "Application ID")]
    [TestCase("", OrgId, "", "Organization ID")]
    [TestCase(null, null, "secret", "Authorization Secret")]
    public void LeftoverLegacyValues_NamesOnlyWhatIsSaved(string appId, string orgId, string authSecret, string expected)
    {
        _config.useAppTokens = true;
        _config.appID = appId;
        _config.orgID = orgId;
        _config.authSecret = authSecret;

        CollectionAssert.AreEqual(new[] { expected }, SetupWizardChecks.LeftoverLegacyValues(_config));
    }

    [Test]
    public void AProjectOnAppTokens_WithNoLegacyValues_HasNoWarning()
    {
        Assert.IsTrue(_config.useAppTokens);

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config));
    }

    [Test]
    public void ALegacyProject_HasNoLeftoverWarning_SinceItsValuesAreInUse()
    {
        _config.useAppTokens = false;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config));
    }

    [Test]
    public void WithoutAConfig_ThereIsNoLeftoverWarning()
    {
        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(null));
        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(null));
    }

    [Test]
    public void TheLeftoverWarning_WithAnOrganizationId_DoesNotCallItUnused()
    {
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        string warning = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);

        StringAssert.Contains("Application ID, Organization ID, Authorization Secret are still saved", warning);
        StringAssert.Contains("ship inside every build", warning);
        StringAssert.Contains("Abxr.GetOrgId() still returns the Organization ID", warning);
        StringAssert.DoesNotContain("doesn't read", warning);
        StringAssert.DoesNotContain("unused", warning);
    }

    [Test]
    public void TheLeftoverWarning_WithoutAnOrganizationId_SaysTheSdkDoesNotReadIt()
    {
        _config.useAppTokens = true;
        _config.authSecret = "secret";

        string warning = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);

        StringAssert.Contains("Authorization Secret is still saved", warning);
        StringAssert.Contains("ships inside every build", warning);
        StringAssert.Contains("The SDK doesn't read it on App Tokens. Clear it.", warning);
    }

    [Test]
    public void TheLeftoverWarning_NeverPrintsAValue()
    {
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "a-secret-value";

        string warning = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);

        StringAssert.DoesNotContain(AppId, warning);
        StringAssert.DoesNotContain(OrgId, warning);
        StringAssert.DoesNotContain("a-secret-value", warning);
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

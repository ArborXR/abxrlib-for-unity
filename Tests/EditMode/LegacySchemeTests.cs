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
    public void ANewProject_IsShownNeitherTheLegacySchemeNorTheCheckbox()
    {
        Assert.IsTrue(_config.useAppTokens, "A new configuration starts on App Tokens.");
        Assert.IsFalse(SetupWizardChecks.HasLegacyValues(_config), "And with no legacy values.");

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config));
    }

    [Test]
    public void AnAssetSavedBeforeTheCheckboxExisted_KeepsTheCheckbox_WhichIsItsWayBack()
    {
        // Such an asset has no value for Use App Tokens, so it loads as ticked, with its legacy credentials saved and
        // no App Token.
        Assert.IsTrue(_config.useAppTokens);
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";
        Assert.IsFalse(SetupWizardChecks.CredentialsAreValid(_config), "It can't sign in as it opens.");

        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config));
        Assert.IsTrue(new SetupWizardChecks.ShownLatch().Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)),
            "In a window that has only just opened, too.");

        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config), "Unticking brings its fields back.");
        Assert.IsTrue(SetupWizardChecks.CredentialsAreValid(_config), "And the credentials it shipped with work again.");
    }

    [TestCase(true, null, null, null, ExpectedResult = false)] // a new project
    [TestCase(true, "", "", "", ExpectedResult = false)]
    [TestCase(true, AppId, null, null, ExpectedResult = true)]
    [TestCase(true, null, OrgId, null, ExpectedResult = true)]
    [TestCase(true, null, null, "secret", ExpectedResult = true)]
    [TestCase(false, null, null, null, ExpectedResult = true)] // on the legacy scheme with nothing saved yet
    [TestCase(false, AppId, OrgId, "secret", ExpectedResult = true)]
    public bool ShowsUseAppTokensToggle_OnTheLegacyScheme_OrWhileLegacyValuesAreSaved(bool useAppTokens, string appId, string orgId, string authSecret)
    {
        _config.useAppTokens = useAppTokens;
        _config.appID = appId;
        _config.orgID = orgId;
        _config.authSecret = authSecret;

        return SetupWizardChecks.ShowsUseAppTokensToggle(_config);
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
    public void AProjectOnAppTokens_WithLegacyValuesStillSaved_GetsTheCheckbox_NotTheFields()
    {
        // The fields stay hidden, so the leftover warning below says the values still ship, and the checkbox is the
        // way back to them.
        _config.useAppTokens = true;
        _config.appToken = "app.token.jwt";
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config));
        Assert.IsNotNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config));
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
        Assert.IsFalse(SetupWizardChecks.HasLegacyValues(null));
        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(null));
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
        StringAssert.Contains("Untick Use App Tokens to go back to the legacy scheme, or clear them", warning);
    }

    [Test]
    public void TheLeftoverWarning_WithoutAnOrganizationId_SaysTheSdkDoesNotReadIt()
    {
        _config.useAppTokens = true;
        _config.authSecret = "secret";

        string warning = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);

        StringAssert.Contains("Authorization Secret is still saved", warning);
        StringAssert.Contains("ships inside every build", warning);
        StringAssert.Contains("The SDK doesn't read it on App Tokens.", warning);
        StringAssert.Contains("or clear it, which also removes that checkbox", warning);
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

    [Test]
    public void ClearLeftoverLegacyValues_EmptiesThem_AndNothingElse()
    {
        _config.useAppTokens = true;
        _config.buildType = "development";
        _config.appToken = "app.token.jwt";
        _config.orgToken = "org.token.jwt";
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        SetupWizardChecks.ClearLeftoverLegacyValues(_config);

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config), "The warning and its button go once they are cleared.");
        Assert.IsTrue(_config.useAppTokens);
        Assert.AreEqual("development", _config.buildType);
        Assert.AreEqual("app.token.jwt", _config.appToken);
        Assert.AreEqual("org.token.jwt", _config.orgToken);
    }

    [Test]
    public void ClearLeftoverLegacyValues_OnALegacyProject_LeavesItsCredentials()
    {
        _config.useAppTokens = false;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        SetupWizardChecks.ClearLeftoverLegacyValues(_config);

        Assert.AreEqual(AppId, _config.appID);
        Assert.AreEqual(OrgId, _config.orgID);
        Assert.AreEqual("secret", _config.authSecret);
    }

    [Test]
    public void ClearLeftoverLegacyValues_WithoutAConfig_DoesNothing()
    {
        Assert.DoesNotThrow(() => SetupWizardChecks.ClearLeftoverLegacyValues(null));
    }

    // ── Switching, and the way back ───────────────────────────────

    [Test]
    public void TickingUseAppTokens_WithLegacyValuesSaved_KeepsTheCheckboxInAnyWindow()
    {
        _config.useAppTokens = false;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        _config.useAppTokens = true;

        Assert.IsFalse(SetupWizardChecks.ShowsLegacyScheme(_config), "The legacy fields give way to the App Token.");
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config),
            "Not only in the window that ticked it: after a recompile, in the other editor, in a later session.");
    }

    [Test]
    public void ClearingTheLegacyValues_RemovesTheCheckboxToo()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = true;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)));

        // What the Clear button does in both editors.
        SetupWizardChecks.ClearLeftoverLegacyValues(_config);
        checkbox.Reset();

        Assert.IsFalse(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)), "Nothing is left to switch back to.");
    }

    [Test]
    public void TickingUseAppTokens_WithNothingSaved_KeepsTheCheckboxUntilTheWindowCloses()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)));

        _config.useAppTokens = true;

        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config), "No legacy values keep it there.");
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)), "A mistaken tick can be undone.");
        checkbox.Reset();
        Assert.IsFalse(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)), "A window opened after that has nothing to go back to.");
    }

    [Test]
    public void UntickingAgain_BringsTheLegacyFieldsBack()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config));
        _config.useAppTokens = true;
        checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config));

        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsTrue(checkbox.Holds(SetupWizardChecks.ShowsUseAppTokensToggle(_config)));
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

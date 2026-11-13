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
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, new SetupWizardChecks.ShownLatch()),
            "In a window that has only just opened, too.");

        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config), "Unticking brings its fields back.");
        Assert.IsTrue(SetupWizardChecks.CredentialsAreValid(_config), "And the credentials it shipped with work again.");
    }

    [Test]
    public void LegacyCredentialsWithNoAppToken_AreToldToUntickOrPasteAnAppToken()
    {
        Assert.IsTrue(_config.useAppTokens);
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        string problem = SetupWizardChecks.DescribeCredentialProblem(_config);

        StringAssert.Contains("Untick Use App Tokens to keep using them", problem);
        StringAssert.Contains("or paste an App Token", problem);
    }

    [Test]
    public void ANewProjectWithNoAppToken_IsOnlyAskedForOne()
    {
        string problem = SetupWizardChecks.DescribeCredentialProblem(_config);

        StringAssert.StartsWith("App Token is required.", problem);
        StringAssert.DoesNotContain("Untick", problem);
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

    [Test]
    public void TheShippingNote_OnAppTokens_NamesNoLegacyCredential()
    {
        _config.useAppTokens = true;

        string note = SetupWizardChecks.ShippingNote(_config);

        StringAssert.Contains("Never ship an Organization Token", note);
        StringAssert.DoesNotContain("Authorization Secret", note);
        StringAssert.DoesNotContain("Organization ID", note);
    }

    [Test]
    public void TheShippingNote_OnTheLegacyScheme_NamesItsOwnCredentials()
    {
        _config.useAppTokens = false;

        string note = SetupWizardChecks.ShippingNote(_config);

        StringAssert.Contains("Organization ID with its Authorization Secret", note);
        StringAssert.DoesNotContain("App Token", note);
    }

    [Test]
    public void InProduction_ASavedOrganizationIdOrSecret_IsSaidToShip()
    {
        _config.useAppTokens = false;
        _config.buildType = "production";
        _config.appID = AppId;
        _config.orgID = OrgId;

        string hint = SetupWizardChecks.LegacyOrgFieldsHint(_config, out bool warn);

        StringAssert.Contains("ship inside the build", hint);
        StringAssert.DoesNotContain("NOT be included", hint);
        Assert.IsTrue(warn);
    }

    [Test]
    public void InProduction_WithNeitherSaved_TheHintOnlyExplains()
    {
        _config.useAppTokens = false;
        _config.buildType = "production";
        _config.appID = AppId;

        string hint = SetupWizardChecks.LegacyOrgFieldsHint(_config, out bool warn);

        StringAssert.Contains("the device's MDM supplies them", hint);
        Assert.IsFalse(warn);
    }

    [TestCase(false, "production_custom", ExpectedResult = true)]
    [TestCase(false, "development", ExpectedResult = false)]
    [TestCase(false, "unknown", ExpectedResult = false)]
    [TestCase(true, "production", ExpectedResult = false)] // not on the legacy scheme
    [TestCase(true, "production_custom", ExpectedResult = false)]
    public bool LegacyOrgFieldsHint_IsOnlyForTheLegacyScheme(bool useAppTokens, string buildType)
    {
        _config.useAppTokens = useAppTokens;
        _config.buildType = buildType;
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        string hint = SetupWizardChecks.LegacyOrgFieldsHint(_config, out bool warn);

        Assert.IsFalse(warn);
        return hint != null;
    }

    [TestCase("production", "", ExpectedResult = true)]
    [TestCase("production", null, ExpectedResult = true)]
    [TestCase("production", "not-a-uuid", ExpectedResult = false)] // it fails the credential check until fixed or cleared
    [TestCase("production", OrgId, ExpectedResult = false)]
    [TestCase("development", "", ExpectedResult = false)]
    [TestCase("production_custom", "", ExpectedResult = false)]
    [TestCase("unknown", "", ExpectedResult = false)]
    public bool LegacyOrgFieldIsLocked_OnlyWhenEmptyInProduction(string buildType, string value)
    {
        _config.useAppTokens = false;
        _config.buildType = buildType;

        return SetupWizardChecks.LegacyOrgFieldIsLocked(_config, value);
    }

    [Test]
    public void AMalformedOrganizationIdInProduction_CanBeReachedToFixIt()
    {
        _config.useAppTokens = false;
        _config.buildType = "production";
        _config.appID = AppId;
        _config.orgID = "not-a-uuid";

        Assert.IsFalse(SetupWizardChecks.CredentialsAreValid(_config), "The wizard asks for a fix.");
        Assert.IsFalse(SetupWizardChecks.LegacyOrgFieldIsLocked(_config, _config.orgID), "So the field must take one.");
    }

    // ── Legacy values left on a project that uses App Tokens ──────

    private void OnAppTokensWithLegacyValues(string appToken = "app.token.jwt")
    {
        _config.useAppTokens = true;
        _config.appToken = appToken;
        _config.appID = AppId;
        _config.orgID = OrgId;
        _config.authSecret = "secret";
    }

    [Test]
    public void LeftoverLegacyValues_NamesEachSavedValue()
    {
        OnAppTokensWithLegacyValues();

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
        _config.appToken = "app.token.jwt";

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverValuesNotice(_config, out bool offersClear));
        Assert.IsFalse(offersClear);
    }

    [Test]
    public void ALegacyProject_HasNoLeftoverLegacyValues_SinceTheyAreInUse()
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
        Assert.IsNull(SetupWizardChecks.SwitchBackMessage(null));
        Assert.IsNull(SetupWizardChecks.LeftoverValuesNotice(null, out bool offersClear));
        Assert.IsFalse(offersClear);
    }

    [Test]
    public void TheLeftoverWarning_WithAnOrganizationId_DoesNotCallItUnused()
    {
        OnAppTokensWithLegacyValues();

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
        _config.appToken = "app.token.jwt";
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
        OnAppTokensWithLegacyValues();
        _config.authSecret = "a-secret-value";

        string warning = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);

        StringAssert.DoesNotContain(AppId, warning);
        StringAssert.DoesNotContain(OrgId, warning);
        StringAssert.DoesNotContain("a-secret-value", warning);
        StringAssert.DoesNotContain("app.token.jwt", warning);
    }

    [Test]
    public void WithAnAppToken_TheNoticeIsTheLeftoverWarning_AndOffersClear()
    {
        OnAppTokensWithLegacyValues();

        string notice = SetupWizardChecks.LeftoverValuesNotice(_config, out bool offersClear);

        Assert.AreEqual(SetupWizardChecks.LeftoverLegacyValuesWarning(_config), notice);
        Assert.IsTrue(offersClear);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void WithNoAppToken_TheLegacyValuesAreNotLeftovers_AndNothingOffersToClearThem(string appToken)
    {
        // How an asset last saved before Use App Tokens existed opens. Its legacy values are its only credentials.
        OnAppTokensWithLegacyValues(appToken);

        Assert.IsNull(SetupWizardChecks.LeftoverLegacyValuesWarning(_config));
        string notice = SetupWizardChecks.LeftoverValuesNotice(_config, out bool offersClear);

        Assert.IsFalse(offersClear, "One click must not delete the only credentials the project has.");
        StringAssert.Contains("Untick Use App Tokens to keep using them, or paste an App Token", notice);
        Assert.AreEqual(notice, SetupWizardChecks.DescribeCredentialProblem(_config), "The wizard's top box says the same.");
    }

    [Test]
    public void ClearLeftoverLegacyValues_WithNoAppToken_LeavesTheOnlyCredentialsAlone()
    {
        OnAppTokensWithLegacyValues(appToken: "");

        SetupWizardChecks.ClearLeftoverLegacyValues(_config);

        Assert.AreEqual(AppId, _config.appID);
        Assert.AreEqual(OrgId, _config.orgID);
        Assert.AreEqual("secret", _config.authSecret);
    }

    [Test]
    public void ClearLeftoverLegacyValues_EmptiesThem_AndNothingElse()
    {
        OnAppTokensWithLegacyValues();
        _config.buildType = "development";
        _config.orgToken = "org.token.jwt";

        SetupWizardChecks.ClearLeftoverLegacyValues(_config);

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverValuesNotice(_config, out _), "The warning and its button go once they are cleared.");
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

    // ── Tokens left on a project that uses the legacy scheme ──────

    private void OnTheLegacySchemeWithTokens(string appId = AppId)
    {
        _config.useAppTokens = false;
        _config.appID = appId;
        _config.appToken = "app.token.jwt";
        _config.orgToken = "org.token.jwt";
    }

    [Test]
    public void LeftoverTokens_NamesEachSavedToken()
    {
        OnTheLegacySchemeWithTokens();

        CollectionAssert.AreEqual(new[] { "App Token", "Organization Token" }, SetupWizardChecks.LeftoverTokens(_config));
    }

    [Test]
    public void AProjectOnAppTokens_HasNoLeftoverTokens_SinceTheyAreInUse()
    {
        _config.useAppTokens = true;
        _config.appToken = "app.token.jwt";
        _config.orgToken = "org.token.jwt";

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverTokens(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverTokensWarning(_config));
        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverTokens(null));
    }

    [Test]
    public void TheLeftoverTokensWarning_SaysTheyShip_AndNeverPrintsOne()
    {
        OnTheLegacySchemeWithTokens();

        string warning = SetupWizardChecks.LeftoverValuesNotice(_config, out bool offersClear);

        Assert.IsTrue(offersClear);
        StringAssert.Contains("App Token, Organization Token are still saved and ship inside every build", warning);
        StringAssert.Contains("The SDK doesn't read them on the legacy scheme", warning);
        StringAssert.Contains("Tick Use App Tokens to go back to App Tokens, or clear them", warning);
        StringAssert.DoesNotContain("app.token.jwt", warning);
        StringAssert.DoesNotContain("org.token.jwt", warning);
        Assert.AreEqual("Clear saved tokens", SetupWizardChecks.ClearLeftoverValuesLabel(_config));
    }

    [Test]
    public void OnAppTokens_TheClearButtonIsForTheLegacyValues()
    {
        OnAppTokensWithLegacyValues();

        Assert.AreEqual("Clear legacy values", SetupWizardChecks.ClearLeftoverValuesLabel(_config));
    }

    [TestCase(null)]
    [TestCase("")]
    public void WithNoApplicationId_TheTokensAreNotLeftovers_AndNothingOffersToClearThem(string appId)
    {
        // A project on App Tokens that unticked the checkbox: its App Token is its only credential.
        OnTheLegacySchemeWithTokens(appId);

        Assert.IsNull(SetupWizardChecks.LeftoverTokensWarning(_config));
        string notice = SetupWizardChecks.LeftoverValuesNotice(_config, out bool offersClear);

        Assert.IsFalse(offersClear);
        StringAssert.Contains("Tick Use App Tokens to keep using it, or enter an Application ID", notice);
        Assert.AreEqual(notice, SetupWizardChecks.DescribeCredentialProblem(_config));

        SetupWizardChecks.ClearLeftoverTokens(_config);
        Assert.AreEqual("app.token.jwt", _config.appToken, "Its only credential is left alone.");
    }

    [Test]
    public void ClearLeftoverTokens_EmptiesThem_AndNothingElse()
    {
        OnTheLegacySchemeWithTokens();
        _config.orgID = OrgId;
        _config.authSecret = "secret";

        SetupWizardChecks.ClearLeftoverTokens(_config);

        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverTokens(_config));
        Assert.IsNull(SetupWizardChecks.LeftoverValuesNotice(_config, out _));
        Assert.IsFalse(_config.useAppTokens);
        Assert.AreEqual(AppId, _config.appID);
        Assert.AreEqual(OrgId, _config.orgID);
        Assert.AreEqual("secret", _config.authSecret);
    }

    [Test]
    public void ClearLeftoverTokens_OnAppTokens_LeavesItsCredentials()
    {
        _config.useAppTokens = true;
        _config.appToken = "app.token.jwt";
        _config.orgToken = "org.token.jwt";

        SetupWizardChecks.ClearLeftoverTokens(_config);
        SetupWizardChecks.ClearLeftoverTokens(null);

        Assert.AreEqual("app.token.jwt", _config.appToken);
        Assert.AreEqual("org.token.jwt", _config.orgToken);
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
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, new SetupWizardChecks.ShownLatch()),
            "Not only in the window that ticked it: after a recompile, in the other editor, in a later session.");
    }

    [Test]
    public void TheClearButton_EmptiesTheLegacyValues_AndRemovesTheCheckboxToo()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        OnAppTokensWithLegacyValues();
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox));

        bool cleared = SetupWizardChecks.ClearLeftoverValues(_config, checkbox);

        Assert.IsTrue(cleared);
        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverLegacyValues(_config));
        Assert.AreEqual("app.token.jwt", _config.appToken);
        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox), "Nothing is left to switch back to.");
        Assert.IsTrue(UnityEditor.EditorUtility.IsDirty(_config), "The asset is marked changed, so the save writes it.");
    }

    [Test]
    public void ClearingInOneWindow_RemovesTheCheckboxFromTheOther()
    {
        var thisWindow = new SetupWizardChecks.ShownLatch();
        var otherWindow = new SetupWizardChecks.ShownLatch();
        OnAppTokensWithLegacyValues();
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, thisWindow));
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, otherWindow));

        SetupWizardChecks.ClearLeftoverValues(_config, thisWindow);

        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config, otherWindow),
            "Saved values are read fresh, not latched, so the other window follows.");
    }

    [Test]
    public void TheClearButton_OnTheLegacyScheme_EmptiesTheTokens()
    {
        OnTheLegacySchemeWithTokens();

        bool cleared = SetupWizardChecks.ClearLeftoverValues(_config, new SetupWizardChecks.ShownLatch());

        Assert.IsTrue(cleared);
        CollectionAssert.IsEmpty(SetupWizardChecks.LeftoverTokens(_config));
        Assert.AreEqual(AppId, _config.appID);
    }

    [Test]
    public void TheClearButton_IsOneUndoStep()
    {
        OnAppTokensWithLegacyValues();
        UnityEditor.Undo.IncrementCurrentGroup();

        SetupWizardChecks.ClearLeftoverValues(_config, null);
        UnityEditor.Undo.FlushUndoRecordObjects();
        UnityEditor.Undo.PerformUndo();

        Assert.AreEqual(AppId, _config.appID);
        Assert.AreEqual(OrgId, _config.orgID);
        Assert.AreEqual("secret", _config.authSecret);
    }

    [Test]
    public void TheClearButton_WhereNothingMayBeCleared_ChangesNothing()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        OnAppTokensWithLegacyValues(appToken: "");
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox));

        bool cleared = SetupWizardChecks.ClearLeftoverValues(_config, checkbox);

        Assert.IsFalse(cleared);
        Assert.AreEqual(AppId, _config.appID);
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox), "The checkbox stays: it is the way back to those credentials.");
        Assert.IsFalse(SetupWizardChecks.ClearLeftoverValues(null, checkbox));
    }

    [Test]
    public void TickingUseAppTokens_WithNothingSaved_KeepsTheCheckboxUntilTheWindowCloses()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox));

        _config.useAppTokens = true;

        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config), "No legacy values keep it there.");
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox), "A mistaken tick can be undone.");
        checkbox.Reset();
        Assert.IsFalse(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox), "A window opened after that has nothing to go back to.");
    }

    [Test]
    public void UntickingAgain_BringsTheLegacyFieldsBack()
    {
        var checkbox = new SetupWizardChecks.ShownLatch();
        _config.useAppTokens = false;
        SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox);
        _config.useAppTokens = true;
        SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox);

        _config.useAppTokens = false;

        Assert.IsTrue(SetupWizardChecks.ShowsLegacyScheme(_config));
        Assert.IsTrue(SetupWizardChecks.ShowsUseAppTokensToggle(_config, checkbox));
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

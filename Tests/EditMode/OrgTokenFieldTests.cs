// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for when the Configuration inspector and the Setup Wizard show the Organization Token field (SDK-87):
// for Production (Custom APK), and whenever the project has one saved. Also the hint under it, and the latch that
// keeps it up in an open window while it is being edited.
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OrgTokenFieldTests
{
    private const string AppToken = "app.token.jwt";
    private const string OrgToken = "org.token.jwt";

    private AppConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AppConfig>();
        _config.useAppTokens = true;
        _config.appToken = AppToken;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    // ── The rule ──────────────────────────────────────────────────

    [TestCase("production_custom", "", ExpectedResult = true)]
    [TestCase("production_custom", OrgToken, ExpectedResult = true)]
    [TestCase("development", "", ExpectedResult = false)]
    [TestCase("development", null, ExpectedResult = false)]
    [TestCase("development", OrgToken, ExpectedResult = true)] // an existing project keeps using it
    [TestCase("development", AppToken, ExpectedResult = true)] // what the wizard's old button wrote
    [TestCase("development", "not-a-token", ExpectedResult = true)]
    [TestCase("production", "", ExpectedResult = false)]
    [TestCase("production", OrgToken, ExpectedResult = true)] // unused, but it ships inside the build
    [TestCase("production", "not-a-token", ExpectedResult = true)]
    [TestCase("unknown", "", ExpectedResult = false)]
    [TestCase("unknown", OrgToken, ExpectedResult = true)] // the runtime still sends it
    public bool ShowsOrgTokenField_ForCustomApk_OrASavedValue(string buildType, string orgToken)
    {
        _config.buildType = buildType;
        _config.orgToken = orgToken;

        return SetupWizardChecks.ShowsOrgTokenField(_config);
    }

    [TestCase("production_custom")]
    [TestCase("development")]
    [TestCase("production")]
    public void ShowsOrgTokenField_OnTheLegacyScheme_IsFalse(string buildType)
    {
        _config.useAppTokens = false;
        _config.buildType = buildType;
        _config.orgToken = OrgToken;

        Assert.IsFalse(SetupWizardChecks.ShowsOrgTokenField(_config));
        Assert.IsNull(SetupWizardChecks.OrgTokenFieldHint(_config, out _));
    }

    [Test]
    public void WithoutAConfig_NothingShows()
    {
        Assert.IsFalse(SetupWizardChecks.ShowsOrgTokenField(null));
        Assert.IsFalse(new SetupWizardChecks.OrgTokenFieldLatch().Shows(null));
        Assert.IsNull(SetupWizardChecks.OrgTokenFieldHint(null, out bool warn));
        Assert.IsFalse(warn);
    }

    // ── The hint ──────────────────────────────────────────────────

    [TestCase("development", OrgToken, "Clear it to use the App Token instead", false)]
    [TestCase("development", AppToken, "This is a copy of the App Token", false)]
    [TestCase("development", "not-a-token", "can't sign in with it", true)]
    [TestCase("development", "", "Development builds use the App Token", false)]
    [TestCase("production", OrgToken, "ships inside the build", true)]
    [TestCase("production", "not-a-token", "this value isn't one", true)]
    [TestCase("Production", OrgToken, "Production doesn't use this Organization Token", true)] // the runtime ignores case here
    [TestCase("production", "", "as Production expects", false)]
    [TestCase("unknown", OrgToken, "is still sent", true)]
    [TestCase("", OrgToken, "is still sent", true)]
    public void OrgTokenFieldHint_SaysWhatTheSavedValueDoes(string buildType, string orgToken, string expected, bool expectWarn)
    {
        _config.buildType = buildType;
        _config.orgToken = orgToken;

        StringAssert.Contains(expected, SetupWizardChecks.OrgTokenFieldHint(_config, out bool warn));
        Assert.AreEqual(expectWarn, warn);
    }

    [Test]
    public void OrgTokenFieldHint_ForAnUnrecognisedBuildType_DoesNotSayTheTokenIsUnused()
    {
        _config.buildType = "unknown";
        _config.orgToken = OrgToken;

        StringAssert.DoesNotContain("doesn't use", SetupWizardChecks.OrgTokenFieldHint(_config, out _));
    }

    [TestCase("")]
    [TestCase(OrgToken)]
    public void OrgTokenFieldHint_ForCustomApk_IsNull(string orgToken)
    {
        _config.buildType = "production_custom";
        _config.orgToken = orgToken;

        Assert.IsNull(SetupWizardChecks.OrgTokenFieldHint(_config, out bool warn));
        Assert.IsFalse(warn);
    }

    // ── The latch for an open window ──────────────────────────────

    [TestCase("development")]
    [TestCase("production")]
    public void Latch_KeepsTheFieldAfterItIsEmptied_UntilReset(string buildType)
    {
        var latch = new SetupWizardChecks.OrgTokenFieldLatch();
        _config.buildType = buildType;
        _config.orgToken = OrgToken;
        Assert.IsTrue(latch.Shows(_config));

        _config.orgToken = "";

        Assert.IsTrue(latch.Shows(_config), "Select all, delete, then paste must still have a field to paste into.");
        Assert.IsFalse(SetupWizardChecks.ShowsOrgTokenField(_config), "The rule itself hides an empty field.");
        latch.Reset();
        Assert.IsFalse(latch.Shows(_config), "A window opened after that has no field.");
    }

    [Test]
    public void Latch_WithoutASavedValue_FollowsTheRule()
    {
        var latch = new SetupWizardChecks.OrgTokenFieldLatch();
        _config.buildType = "development";
        _config.orgToken = "";

        Assert.IsFalse(latch.Shows(_config));
    }

    [Test]
    public void Latch_AfterAnEmptyCustomApkField_DoesNotCarryIntoDevelopment()
    {
        var latch = new SetupWizardChecks.OrgTokenFieldLatch();
        _config.buildType = "production_custom";
        _config.orgToken = "";
        Assert.IsTrue(latch.Shows(_config));

        _config.buildType = "development";

        Assert.IsFalse(latch.Shows(_config), "Only a field that held a value stays.");
    }

    [Test]
    public void Latch_OnTheLegacyScheme_HidesTheField()
    {
        var latch = new SetupWizardChecks.OrgTokenFieldLatch();
        _config.buildType = "development";
        _config.orgToken = OrgToken;
        Assert.IsTrue(latch.Shows(_config));

        _config.useAppTokens = false;

        Assert.IsFalse(latch.Shows(_config));
    }
}

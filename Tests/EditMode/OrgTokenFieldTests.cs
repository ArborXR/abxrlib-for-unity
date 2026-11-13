// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for when the Configuration inspector and the Setup Wizard show the Organization Token field (SDK-87):
// only for Production (Custom APK), apart from a value an existing project already has.
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OrgTokenFieldTests
{
    private const string OrgToken = "org.token.jwt";

    private AppConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AppConfig>();
        _config.useAppTokens = true;
        _config.appToken = "app.token.jwt";
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    [TestCase("production_custom", "", ExpectedResult = true)]
    [TestCase("production_custom", OrgToken, ExpectedResult = true)]
    [TestCase("development", "", ExpectedResult = false)]
    [TestCase("development", null, ExpectedResult = false)]
    [TestCase("development", OrgToken, ExpectedResult = true)] // an existing project keeps using it
    [TestCase("development", "not-a-token", ExpectedResult = true)]
    [TestCase("production", "", ExpectedResult = false)]
    [TestCase("production", OrgToken, ExpectedResult = false)] // ignored at runtime
    [TestCase("production", "not-a-token", ExpectedResult = true)] // fails the credential check until it is cleared
    [TestCase("unknown", OrgToken, ExpectedResult = false)] // reads as Production
    public bool ShowsOrgTokenField_OnlyForCustomApk_OrAValueAlreadyThere(string buildType, string orgToken)
    {
        _config.buildType = buildType;
        _config.orgToken = orgToken;

        return SetupWizardChecks.ShowsOrgTokenField(_config);
    }

    [Test]
    public void ShowsOrgTokenField_OnTheLegacyScheme_IsFalse()
    {
        _config.useAppTokens = false;
        _config.buildType = "production_custom";
        _config.orgToken = OrgToken;

        Assert.IsFalse(SetupWizardChecks.ShowsOrgTokenField(_config));
    }

    [Test]
    public void OrgTokenFieldHint_InDevelopment_SaysClearingItUsesTheAppToken()
    {
        _config.buildType = "development";
        _config.orgToken = OrgToken;

        StringAssert.Contains("Clear it to use the App Token instead", SetupWizardChecks.OrgTokenFieldHint(_config));
    }

    [Test]
    public void OrgTokenFieldHint_ForALeftoverValueInProduction_SaysToClearIt()
    {
        _config.buildType = "production";
        _config.orgToken = "not-a-token";

        StringAssert.Contains("Production doesn't use an Organization Token", SetupWizardChecks.OrgTokenFieldHint(_config));
    }

    [TestCase("production_custom", OrgToken)]
    [TestCase("development", "")]
    [TestCase("production", "")]
    [TestCase("production", OrgToken)]
    public void OrgTokenFieldHint_WhereTheFieldIsRequiredOrHidden_IsNull(string buildType, string orgToken)
    {
        _config.buildType = buildType;
        _config.orgToken = orgToken;

        Assert.IsNull(SetupWizardChecks.OrgTokenFieldHint(_config));
    }
}

// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins the build hook's contract: an invalid configuration is reported as a warning and never as an exception, so
// nothing here can fail a build.
using System.Text.RegularExpressions;
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class BuildValidationTests
{
    private AppConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AppConfig>();
        Core.SetConfigForTesting(_config);
    }

    [TearDown]
    public void TearDown()
    {
        Core.SetConfigForTesting(null);
        Object.DestroyImmediate(_config);
    }

    [Test]
    public void OnPreprocessBuild_WithMissingAppToken_WarnsAndDoesNotThrow()
    {
        _config.useAppTokens = true;
        _config.appToken = "";

        LogAssert.Expect(LogType.Warning, new Regex("AbxrLib setup: App Token is required"));

        Assert.DoesNotThrow(() => new BuildValidation().OnPreprocessBuild(null));
    }

    [Test]
    public void OnPreprocessBuild_WithMalformedAppToken_WarnsAndDoesNotThrow()
    {
        _config.useAppTokens = true;
        _config.appToken = "not-a-token";

        LogAssert.Expect(LogType.Warning, new Regex("AbxrLib setup: App Token does not look like a token"));

        Assert.DoesNotThrow(() => new BuildValidation().OnPreprocessBuild(null));
    }

    private const string AppToken = "app.token.jwt";
    private const string OrgToken = "org.token.jwt";

    [Test]
    public void OnPreprocessBuild_DevelopmentWithoutAnOrgToken_WarnsThatItReportsToTheAppsOrg()
    {
        _config.useAppTokens = true;
        _config.buildType = "development";
        _config.appToken = AppToken;
        _config.orgToken = "";

        LogAssert.Expect(LogType.Warning, new Regex("AbxrLib setup: Build Type is Development and the App Token is used as the Organization Token"));

        Assert.DoesNotThrow(() => new BuildValidation().OnPreprocessBuild(null));
    }

    // The build hook's warning, and the builds that must stay quiet about it.
    [TestCase("development", true, "", ExpectedResult = true)]
    [TestCase("development", true, null, ExpectedResult = true)]
    [TestCase("development", true, AppToken, ExpectedResult = true)] // what the wizard's old button wrote
    [TestCase("development", true, OrgToken, ExpectedResult = false)]
    [TestCase("production", true, "", ExpectedResult = false)]
    [TestCase("production", true, OrgToken, ExpectedResult = false)]
    [TestCase("production_custom", true, OrgToken, ExpectedResult = false)]
    [TestCase("production_custom", true, "", ExpectedResult = false)]
    [TestCase("development", false, "", ExpectedResult = false)] // the legacy scheme has no App Token to stand in
    public bool DescribeDevelopmentAppTokenOrg_OnlyForADevelopmentBuildUsingItsAppToken(string buildType, bool useAppTokens, string orgToken)
    {
        _config.useAppTokens = useAppTokens;
        _config.buildType = buildType;
        _config.appToken = AppToken;
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.orgToken = orgToken;

        return SetupWizardChecks.DescribeDevelopmentAppTokenOrg(_config) != null;
    }

    [Test]
    public void DescribeDevelopmentAppTokenOrg_WithoutAnAppToken_LeavesItToTheCredentialWarning()
    {
        _config.useAppTokens = true;
        _config.buildType = "development";
        _config.appToken = "";

        Assert.IsNull(SetupWizardChecks.DescribeDevelopmentAppTokenOrg(_config));
    }
}

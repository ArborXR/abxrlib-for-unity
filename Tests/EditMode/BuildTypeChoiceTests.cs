// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the Build Type choice in the Configuration inspector and the Setup Wizard (SDK-90): Production
// (Custom APK) is offered only to a project already saved on it, and leaving it out moves no other option.
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BuildTypeChoiceTests
{
    private AppConfig _config;

    [SetUp]
    public void SetUp() => _config = ScriptableObject.CreateInstance<AppConfig>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    // ── Who is offered Production (Custom APK) ────────────────────

    [Test]
    public void ANewProject_IsOfferedProductionAndDevelopmentOnly()
    {
        Assert.AreEqual("production", _config.buildType, "A new configuration starts on Production.");
        Assert.IsFalse(SetupWizardChecks.UsesCustomApk(_config));
        CollectionAssert.AreEqual(new[] { "Production", "Development" },
            SetupWizardChecks.BuildTypeLabels(SetupWizardChecks.UsesCustomApk(_config)));
    }

    [TestCase("production", ExpectedResult = false)]
    [TestCase("development", ExpectedResult = false)]
    [TestCase("production_custom", ExpectedResult = true)]
    [TestCase("Production_Custom", ExpectedResult = false)] // the runtime matches this value exactly too
    [TestCase("unknown", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool UsesCustomApk_OnlyWhenSavedAsCustomApk(string buildType)
    {
        _config.buildType = buildType;

        return SetupWizardChecks.UsesCustomApk(_config);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AProjectSavedAsCustomApk_KeepsTheOption_OnEitherScheme(bool useAppTokens)
    {
        _config.useAppTokens = useAppTokens;
        _config.buildType = "production_custom";

        CollectionAssert.AreEqual(new[] { "Production", "Development", "Production (Custom APK)" },
            SetupWizardChecks.BuildTypeLabels(SetupWizardChecks.UsesCustomApk(_config)));
    }

    [Test]
    public void WithoutAConfig_CustomApkIsNotOffered()
    {
        Assert.IsFalse(SetupWizardChecks.UsesCustomApk(null));
    }

    // ── The popup's index mapping ─────────────────────────────────

    [TestCase("production", ExpectedResult = 0)]
    [TestCase("development", ExpectedResult = 1)]
    [TestCase("production_custom", ExpectedResult = 2)]
    [TestCase("unknown", ExpectedResult = 0)] // reads as Production
    [TestCase("Development", ExpectedResult = 0)]
    [TestCase("", ExpectedResult = 0)]
    [TestCase(null, ExpectedResult = 0)]
    public int BuildTypeIndex_ForEachStoredValue(string buildType) => SetupWizardChecks.BuildTypeIndex(buildType);

    [TestCase(0, ExpectedResult = "production")]
    [TestCase(1, ExpectedResult = "development")]
    [TestCase(2, ExpectedResult = "production_custom")]
    public string BuildTypeAtIndex_IsTheSameWhetherOrNotCustomApkIsOffered(int index) => SetupWizardChecks.BuildTypeAtIndex(index);

    [Test]
    public void LeavingCustomApkOut_MovesNoOtherOption()
    {
        string[] without = SetupWizardChecks.BuildTypeLabels(false);
        string[] with = SetupWizardChecks.BuildTypeLabels(true);

        Assert.AreEqual(2, without.Length);
        Assert.AreEqual(3, with.Length);
        Assert.AreEqual(with[0], without[0]);
        Assert.AreEqual(with[1], without[1]);
    }

    [TestCase("production")]
    [TestCase("development")]
    [TestCase("production_custom")]
    [TestCase("unknown")]
    [TestCase("")]
    [TestCase(null)]
    public void EveryStoredValue_SelectsAnOptionThePopupHas(string buildType)
    {
        _config.buildType = buildType;
        string[] labels = SetupWizardChecks.BuildTypeLabels(SetupWizardChecks.UsesCustomApk(_config));

        int index = SetupWizardChecks.BuildTypeIndex(buildType);

        Assert.Less(index, labels.Length, "A stored value must never select an option the popup left out.");
    }

    [TestCase("production")]
    [TestCase("development")]
    [TestCase("production_custom")]
    public void ARecognisedValue_RoundTripsThroughThePopup(string buildType)
    {
        Assert.AreEqual(buildType, SetupWizardChecks.BuildTypeAtIndex(SetupWizardChecks.BuildTypeIndex(buildType)));
    }

    [Test]
    public void AnUnrecognisedValue_ShowsAsProduction_AndIsOnlyRewrittenByAChange()
    {
        _config.buildType = "unknown";
        string[] labels = SetupWizardChecks.BuildTypeLabels(SetupWizardChecks.UsesCustomApk(_config));

        int shown = SetupWizardChecks.BuildTypeIndex(_config.buildType);

        Assert.AreEqual("Production", labels[shown]);
        // The editors write back only when the selection differs from the one shown, so drawing leaves "unknown" alone.
        Assert.AreEqual("unknown", _config.buildType);
        Assert.AreEqual("development", SetupWizardChecks.BuildTypeAtIndex(1), "Choosing Development is a change, and is written.");
    }

    [Test]
    public void AnUnrecognisedValue_StillGetsTheHintThatItsOrganizationTokenIsSent()
    {
        _config.useAppTokens = true;
        _config.buildType = "unknown";
        _config.orgToken = "org.token.jwt";

        StringAssert.Contains("is still sent", SetupWizardChecks.OrgTokenFieldHint(_config, out bool warn));
        Assert.IsTrue(warn);
    }

    // ── Switching in an open window ───────────────────────────────

    [Test]
    public void SwitchingAwayFromCustomApk_KeepsTheOptionUntilTheWindowCloses()
    {
        var option = new SetupWizardChecks.ShownLatch();
        _config.buildType = "production_custom";
        Assert.IsTrue(option.Holds(SetupWizardChecks.UsesCustomApk(_config)));

        _config.buildType = "production";

        Assert.IsFalse(SetupWizardChecks.UsesCustomApk(_config));
        Assert.IsTrue(option.Holds(SetupWizardChecks.UsesCustomApk(_config)), "A mistaken switch can be undone.");
        option.Reset();
        Assert.IsFalse(option.Holds(SetupWizardChecks.UsesCustomApk(_config)), "A window opened after that doesn't offer it.");
    }

    [Test]
    public void AProjectNeverOnCustomApk_IsNotOfferedIt_WhateverItSwitchesBetween()
    {
        var option = new SetupWizardChecks.ShownLatch();
        _config.buildType = "production";
        Assert.IsFalse(option.Holds(SetupWizardChecks.UsesCustomApk(_config)));

        _config.buildType = "development";

        Assert.IsFalse(option.Holds(SetupWizardChecks.UsesCustomApk(_config)));
    }
}

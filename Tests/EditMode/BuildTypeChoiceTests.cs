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
    [TestCase(-1, ExpectedResult = "production")] // outside the list: its nearest end
    [TestCase(99, ExpectedResult = "production_custom")]
    public string BuildTypeAtIndex_ForEachIndex(int index) => SetupWizardChecks.BuildTypeAtIndex(index);

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

    [TestCase("unknown")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("Production")]
    public void AnUnrecognisedValue_ShowsAsProduction_AndDrawingThePopupWritesNothing(string stored)
    {
        _config.buildType = stored;
        bool offers = SetupWizardChecks.UsesCustomApk(_config);
        int shown = SetupWizardChecks.BuildTypeIndex(stored);

        Assert.AreEqual("Production", SetupWizardChecks.BuildTypeLabels(offers)[shown]);
        Assert.IsNull(SetupWizardChecks.BuildTypePicked(stored, offers, shown), "The popup returns the index it shows until the developer picks another.");
        Assert.AreEqual("development", SetupWizardChecks.BuildTypePicked(stored, offers, 1), "Choosing Development is a change, and is written.");
    }

    // What both editors do with the popup's answer: (stored value, whether Custom APK is offered, picked index).
    [TestCase("production", false, 0, ExpectedResult = null)]
    [TestCase("production", false, 1, ExpectedResult = "development")]
    [TestCase("development", false, 1, ExpectedResult = null)]
    [TestCase("development", false, 0, ExpectedResult = "production")]
    [TestCase("production_custom", true, 2, ExpectedResult = null)]
    [TestCase("production_custom", true, 0, ExpectedResult = "production")]
    [TestCase("production_custom", true, 1, ExpectedResult = "development")]
    [TestCase("production", true, 2, ExpectedResult = "production_custom")] // switching back while the option is still up
    [TestCase("production", false, 2, ExpectedResult = null)] // not on offer
    [TestCase("production", false, -1, ExpectedResult = null)]
    [TestCase("production", true, 3, ExpectedResult = null)]
    public string BuildTypePicked_WritesOnlyAChangeToAnOptionOnOffer(string stored, bool offersCustomApk, int pickedIndex) =>
        SetupWizardChecks.BuildTypePicked(stored, offersCustomApk, pickedIndex);

    [Test]
    public void AnUnrecognisedValue_StillGetsTheHintThatItsOrganizationTokenIsSent()
    {
        _config.useAppTokens = true;
        _config.buildType = "unknown";
        _config.orgToken = "org.token.jwt";

        StringAssert.Contains("is still sent", SetupWizardChecks.OrgTokenFieldHint(_config, out bool warn));
        Assert.IsTrue(warn);
    }

    // ── The tooltip and the wizard's hint ─────────────────────────

    [TestCase(true)]
    [TestCase(false)]
    public void TheTooltip_MentionsCustomApk_OnlyWhereItIsOffered(bool useAppTokens)
    {
        _config.useAppTokens = useAppTokens;

        StringAssert.DoesNotContain("Custom APK", SetupWizardChecks.BuildTypeTooltip(_config, offersCustomApk: false));
        StringAssert.Contains("Production (Custom APK):", SetupWizardChecks.BuildTypeTooltip(_config, offersCustomApk: true));
    }

    [Test]
    public void TheTooltip_OnAppTokens_NamesNoLegacyField()
    {
        _config.useAppTokens = true;

        string tooltip = SetupWizardChecks.BuildTypeTooltip(_config, offersCustomApk: true);

        StringAssert.DoesNotContain("Organization ID", tooltip);
        StringAssert.DoesNotContain("Authorization Secret", tooltip);
        StringAssert.DoesNotContain("OrgID", tooltip);
        StringAssert.Contains("the Organization Token is required", tooltip);
    }

    [Test]
    public void TheTooltip_OnTheLegacyScheme_NamesItsOwnFields_AndSaysTheyStillShip()
    {
        _config.useAppTokens = false;

        string tooltip = SetupWizardChecks.BuildTypeTooltip(_config, offersCustomApk: true);

        StringAssert.Contains("Organization ID and Authorization Secret are required", tooltip);
        StringAssert.DoesNotContain("Organization Token", tooltip);
        StringAssert.Contains("ship inside the build", tooltip);
        StringAssert.DoesNotContain("will NOT be included", tooltip);
        StringAssert.DoesNotContain("not used", tooltip);
    }

    [Test]
    public void TheHint_ForDevelopment_SaysWhereEachSchemeSignsIn()
    {
        _config.useAppTokens = true;
        StringAssert.Contains("the organization that owns the app", SetupWizardChecks.BuildTypeHint(_config, 1));

        _config.useAppTokens = false;
        string legacy = SetupWizardChecks.BuildTypeHint(_config, 1);
        StringAssert.Contains("the Organization ID and Authorization Secret saved here", legacy);
        StringAssert.DoesNotContain("owns the app", legacy);
        StringAssert.DoesNotContain("pairing", legacy);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TheHint_HasALineForEveryOption(bool useAppTokens)
    {
        _config.useAppTokens = useAppTokens;

        StringAssert.StartsWith("Shared builds", SetupWizardChecks.BuildTypeHint(_config, 0));
        StringAssert.StartsWith("Your own test builds", SetupWizardChecks.BuildTypeHint(_config, 1));
        StringAssert.StartsWith("One customer per build", SetupWizardChecks.BuildTypeHint(_config, 2));
    }

    [Test]
    public void TheCustomApkNote_SaysTheOptionGoesAfterSwitchingAway_AndHowToRestoreIt()
    {
        string note = SetupWizardChecks.CustomApkOptionNote(offersCustomApk: true);

        StringAssert.Contains("once this window is reopened", note);
        StringAssert.Contains("buildType: production_custom in Assets/Resources/AbxrLib.asset", note);
    }

    [Test]
    public void TheCustomApkNote_IsOnlyForAProjectOfferedTheOption()
    {
        Assert.IsNull(SetupWizardChecks.CustomApkOptionNote(offersCustomApk: false));
    }

    [Test]
    public void TheLegacyFieldText_MentionsCustomApk_OnlyWhereItIsOffered()
    {
        StringAssert.DoesNotContain("Custom APK", SetupWizardChecks.LegacyOrgFieldTooltip(offersCustomApk: false));
        StringAssert.Contains("Development and Custom APK builds", SetupWizardChecks.LegacyOrgFieldTooltip(offersCustomApk: true));
        StringAssert.DoesNotContain("custom APK", SetupWizardChecks.LegacyOrgValuesRule(offersCustomApk: false));
        StringAssert.Contains("Development builds and custom APKs", SetupWizardChecks.LegacyOrgValuesRule(offersCustomApk: true));
    }

    // ── Nothing issues a customer's Organization Token ────────────

    // The Portal only issues App Tokens, so a customer's Organization Token is "not self-serve, contact ArborXR".
    // These are the phrasings 3.0 removed, in any sentence that is about an Organization Token.
    private static readonly System.Text.RegularExpressions.Regex OrgTokenMention =
        new System.Text.RegularExpressions.Regex(@"org(anization)?\s+token", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex SupplyClaim = new System.Text.RegularExpressions.Regex(
        @"ArborXR\s+(supplies|provides|issues)|(supplied|provided|issued)\s+by\s+ArborXR|from\s+(the\s+)?ArborXR\s+portal",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>The sentences in a file that are about an Organization Token and say where one comes from.</summary>
    private static System.Collections.Generic.List<string> SupplyClaims(string text)
    {
        // A C# string split across lines with " + reads as one sentence.
        text = System.Text.RegularExpressions.Regex.Replace(text, "\"\\s*\\+\\s*\\$?\"", "");
        var claims = new System.Collections.Generic.List<string>();
        foreach (string sentence in System.Text.RegularExpressions.Regex.Split(text, @"(?<=[.!?])\s+|\r?\n"))
            if (OrgTokenMention.IsMatch(sentence) && SupplyClaim.IsMatch(sentence)) claims.Add(sentence.Trim());
        return claims;
    }

    private static string PackageRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "..", ".."));

    [Test]
    public void NoTextSaysWhereACustomersOrganizationTokenComesFrom()
    {
        string root = PackageRoot();
        var files = new System.Collections.Generic.List<string>
        {
            System.IO.Path.Combine(root, "README.md"),
            System.IO.Path.Combine(root, "INTEGRATION_INSTRUCTIONS.md"),
            System.IO.Path.Combine(root, "AGENTS.md"),
            System.IO.Path.Combine(root, "Runtime", "Core", "AppConfig.cs"),
        };
        files.AddRange(System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Editor"), "*.cs", System.IO.SearchOption.AllDirectories));

        foreach (string file in files)
        {
            Assert.IsTrue(System.IO.File.Exists(file), file);
            CollectionAssert.IsEmpty(SupplyClaims(System.IO.File.ReadAllText(file)), System.IO.Path.GetFileName(file));
        }
    }

    [TestCase("That mode is for single-customer APKs, where ArborXR supplies the customer's org token (not self-serve).")]
    [TestCase("Only a Custom APK build needs an Org Token in Configuration, and ArborXR supplies it.")]
    [TestCase("A specific customer's Organization Token is not self-serve in the portal - ArborXR provides it.")]
    [TestCase("Required for Production (Custom APK). Set the customer's org token from ArborXR Portal.")]
    [TestCase("The Organization Token is supplied by ArborXR.")]
    [TestCase("\"A customer's Organization Token is one that ArborXR \" +\n                \"issues on request.\"")]
    public void TheScan_CatchesThePhrasingsThatWereRemoved(string text)
    {
        CollectionAssert.IsNotEmpty(SupplyClaims(text));
    }

    [TestCase("A customer's Organization Token is not self-serve in the portal: contact ArborXR.")]
    [TestCase("App Token (JWT) from ArborXR Portal - identifies app and publisher.")]
    [TestCase("ArborXR provides device management. Set the Org Token only for Custom APK builds.")]
    [TestCase("The device supplies it.")]
    public void TheScan_LeavesOtherTextAlone(string text)
    {
        CollectionAssert.IsEmpty(SupplyClaims(text));
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

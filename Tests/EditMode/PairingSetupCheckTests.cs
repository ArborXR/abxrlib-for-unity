// Copyright (c) 2026 ArborXR. All rights reserved.
// The Setup Wizard's passcode pairing row (SDK-60, SDK-59 RFC §06). None of its outcomes is a Problem, so the build
// hook never warns about pairing.
using System.Linq;
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PairingSetupCheckTests
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

    private static SetupWizardChecks.Check PairingRow() =>
        SetupWizardChecks.Run().Single(check => check.Title.Contains("airing"));

    [Test]
    public void AnInvalidPairingUrl_Warns()
    {
        _config.pairingUrl = "api.xrdm.app";

        var row = PairingRow();

        Assert.AreEqual("Pairing URL isn't a valid URL", row.Title);
        Assert.AreEqual(SetupWizardChecks.Severity.Warning, row.Severity);
    }

    [Test]
    public void TheDefaults_AreNeverAProblem()
    {
        Assert.AreEqual("https://api.xrdm.app/", _config.pairingUrl);
        Assert.IsTrue(_config.enablePairingDismiss);

        Assert.AreNotEqual(SetupWizardChecks.Severity.Problem, PairingRow().Severity);
    }

    [Test]
    public void HidingTheDismiss_IsNoted()
    {
        _config.enablePairingDismiss = false;

        StringAssert.Contains("Enable Pairing Dismiss is off", PairingRow().Detail);
    }

    [Test]
    public void AProjectThatNeverCallsPairing_IsInfoOnly()
    {
        Assume.That(SetupWizardChecks.ProjectCallsPairing(), Is.False, "This host project calls the pairing API.");

        var row = PairingRow();

        Assert.AreEqual("Passcode pairing isn't wired in", row.Title);
        Assert.AreEqual(SetupWizardChecks.Severity.Info, row.Severity);
    }
}

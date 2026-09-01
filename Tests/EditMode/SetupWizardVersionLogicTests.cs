// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins the setup wizard's pure version logic. Both behaviors were review findings on the 3.0 branch:
// versions must compare numerically (1.12 is newer than 1.8, which lexicographic comparison gets wrong),
// and the duplicate-copy survivor must be chosen by version, never by asset-database discovery order.
using System.Collections.Generic;
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;

[TestFixture]
public class SetupWizardVersionLogicTests
{
    [TestCase("2021.3", true, 2021, 3, 0)]
    [TestCase("2021.3.57f2", true, 2021, 3, 57)]
    [TestCase("6000.0.36f1", true, 6000, 0, 36)]
    [TestCase("2021", false, 0, 0, 0)]
    [TestCase("garbage", false, 0, 0, 0)]
    public void TryParseUnityVersion_ReadsMajorMinorPatch(string version, bool expectParsed, int major, int minor, int patch)
    {
        bool parsed = SetupWizardChecks.TryParseUnityVersion(version, out int actualMajor, out int actualMinor, out int actualPatch);

        Assert.AreEqual(expectParsed, parsed, version);
        if (!expectParsed) return;
        Assert.AreEqual(major, actualMajor);
        Assert.AreEqual(minor, actualMinor);
        Assert.AreEqual(patch, actualPatch);
    }

    [Test]
    public void CompareVersions_ComparesNumerically_NotLexicographically()
    {
        Assert.Less(SetupWizardChecks.CompareVersions("1.8.1", "1.12.0"), 0);
        Assert.Greater(SetupWizardChecks.CompareVersions("1.12.0", "1.8.1"), 0);
        Assert.AreEqual(0, SetupWizardChecks.CompareVersions("2.0.11", "2.0.11"));
        Assert.AreEqual(0, SetupWizardChecks.CompareVersions("1.0", "1.0.0"));
    }

    [Test]
    public void CopyToKeep_PrefersTheInstalledVersion_EvenOverANewerFolder()
    {
        // The same source the production code reads: the installed manifest, falling back to the constant.
        string installed = UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(SetupWizardChecks).Assembly)?.version ?? AbxrLibVersion.Version;

        // The decoy is deliberately newer than any real version: preference for the installed version must
        // beat the newest-by-version fallback, not coincide with it.
        var copies = new List<string>
        {
            "Assets/Samples/AbxrLib for Unity/999.0.0",
            "Assets/Samples/AbxrLib for Unity/" + installed
        };

        Assert.AreEqual(copies[1], SetupWizardChecks.CopyToKeep(copies));
    }

    [Test]
    public void CopyToKeep_WithNoInstalledMatch_PicksNewestByVersionRegardlessOfOrder()
    {
        var newestLast = new List<string> { "Assets/Samples/X/1.9.0", "Assets/Samples/X/1.10.0" };
        var newestFirst = new List<string> { "Assets/Samples/X/1.10.0", "Assets/Samples/X/1.9.0" };

        Assert.AreEqual("Assets/Samples/X/1.10.0", SetupWizardChecks.CopyToKeep(newestLast));
        Assert.AreEqual("Assets/Samples/X/1.10.0", SetupWizardChecks.CopyToKeep(newestFirst));
    }
}

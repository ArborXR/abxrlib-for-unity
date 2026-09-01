// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins the hand-maintained AbxrLibVersion.Version to package.json's version. Several Editor features key
// off one or the other - Package Manager names sample import folders from the manifest, while the wizard
// stamps and logs the constant - and nothing fails outright when the two drift: the symptoms are indirect,
// like a stale-import warning that re-importing never clears. This test makes the drift itself the failure,
// so a release bump that touches only one of the two files turns the suite red.
using AbxrLib.Runtime.Core;
using NUnit.Framework;

[TestFixture]
public class VersionSyncTests
{
    [Test]
    public void AbxrLibVersionConstant_MatchesPackageManifest()
    {
#if UNITY_EDITOR
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AbxrLibVersion).Assembly);
        if (package == null)
            Assert.Ignore("AbxrLib is not installed as a package here, so there is no manifest to compare.");

        Assert.AreEqual(package.version, AbxrLibVersion.Version,
            "Runtime/Core/AbxrLibVersion.cs and package.json disagree. The two are synced by hand - bump both " +
            "in the same commit.");
#else
        Assert.Ignore("The package manifest is only readable in the Editor.");
#endif
    }
}

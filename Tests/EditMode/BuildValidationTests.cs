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
}

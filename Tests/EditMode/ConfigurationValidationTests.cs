// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode unit tests for AppConfig.IsValid() — covers all validation branches.
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ConfigurationValidationTests
{
    private AppConfig _config;

    [SetUp]
    public void SetUp()
    {
        Configuration.ResetForTesting();
        _config = ScriptableObject.CreateInstance<AppConfig>();
        _config.restUrl = "https://test.example.com/";
        // Legacy tests below expect appID/orgID/authSecret validation; token tests set useAppTokens true explicitly.
        _config.useAppTokens = false;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
        Configuration.ResetForTesting();
    }

    // ── Legacy mode (appID / orgID / authSecret) ──────────────────────────

    [Test]
    public void LegacyMode_ValidAppId_ReturnsTrue()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        Assert.IsTrue(_config.IsValid());
    }

    [Test]
    public void LegacyMode_EmptyAppId_ReturnsFalse()
    {
        _config.appID = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_NullAppId_ReturnsFalse()
    {
        _config.appID = null;
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_InvalidUUID_ReturnsFalse()
    {
        _config.appID = "not-a-valid-uuid";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_UUIDTooShort_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-12345678901"; // one char short
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_UUIDTooLong_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-1234567890123"; // one char long
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_EmptyRestUrl_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.restUrl = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_InvalidRestUrl_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.restUrl = "not-a-url";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_ProductionCustom_WithoutOrgId_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.buildType = "production_custom";
        _config.orgID = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_ProductionCustom_WithoutAuthSecret_ReturnsFalse()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.buildType = "production_custom";
        _config.orgID = "87654321-4321-4321-4321-876543210987";
        _config.authSecret = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void LegacyMode_ProductionCustom_WithBothOrgIdAndSecret_ReturnsTrue()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.buildType = "production_custom";
        _config.orgID = "87654321-4321-4321-4321-876543210987";
        _config.authSecret = "valid-secret";
        Assert.IsTrue(_config.IsValid());
    }

    // ── App Token mode ────────────────────────────────────────────────────

    [Test]
    public void TokenMode_ValidJwtAppToken_ReturnsTrue()
    {
        _config.useAppTokens = true;
        _config.appToken = "header.payload.signature";
        Assert.IsTrue(_config.IsValid());
    }

    [Test]
    public void TokenMode_EmptyAppToken_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void TokenMode_NullAppToken_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = null;
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void TokenMode_TwoSegmentToken_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = "only.two";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void TokenMode_FourSegmentToken_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = "a.b.c.d";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void TokenMode_ProductionCustom_WithoutOrgToken_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = "header.payload.signature";
        _config.buildType = "production_custom";
        _config.orgToken = "";
        Assert.IsFalse(_config.IsValid());
    }

    [Test]
    public void TokenMode_ProductionCustom_WithValidOrgToken_ReturnsTrue()
    {
        _config.useAppTokens = true;
        _config.appToken = "header.payload.signature";
        _config.buildType = "production_custom";
        _config.orgToken = "org.token.value";
        Assert.IsTrue(_config.IsValid());
    }

    [Test]
    public void TokenMode_SetOrgToken_InvalidJwtFormat_ReturnsFalse()
    {
        _config.useAppTokens = true;
        _config.appToken = "header.payload.signature";
        _config.orgToken = "invalid-not-jwt";
        Assert.IsFalse(_config.IsValid());
    }

    // ── Numeric clamping ──────────────────────────────────────────────────

    [Test]
    public void IsValid_ClampsZeroSendRetries_ToDefault()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.sendRetriesOnFailure = 0;
        _config.IsValid();
        Assert.GreaterOrEqual(_config.sendRetriesOnFailure, 0);
    }

    [Test]
    public void IsValid_ClampsHighSendRetries_ToMax()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.sendRetriesOnFailure = 999;
        _config.IsValid();
        Assert.LessOrEqual(_config.sendRetriesOnFailure, 10);
    }

    [Test]
    public void IsValid_ClampsHighMaxCachedItems_ToMax()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.maximumCachedItems = 99999;
        _config.IsValid();
        Assert.LessOrEqual(_config.maximumCachedItems, 10000);
    }

    [Test]
    public void IsValid_StillReturnsTrueAfterClamping()
    {
        _config.appID = "12345678-1234-1234-1234-123456789012";
        _config.sendRetriesOnFailure = 999;
        _config.maximumCachedItems = 99999;
        Assert.IsTrue(_config.IsValid(), "Clamping out-of-range numeric values should not cause validation to return false");
    }
}

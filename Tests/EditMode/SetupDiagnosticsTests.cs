// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins the diagnostics report's redaction contract and its two config modes. The report is meant to be pasted
// into support requests, so the one thing it must never do is carry a token, the auth secret, or a unit-test
// PIN - in either mode.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbxrLib.Editor;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SetupDiagnosticsTests
{
    private const string AppToken = "header.payload.signature";
    private const string OrgToken = "orgh.orgp.orgs";
    private const string AuthSecret = "s3cret-value";
    private const string TestPin = "9876";
    private const string AppId = "12345678-1234-1234-1234-123456789012";

    private AppConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AppConfig>();
        _config.useAppTokens = true;
        _config.appToken = AppToken;
        _config.orgToken = OrgToken;
        _config.authSecret = AuthSecret;
        _config.appID = AppId;
        _config.unitTestAuthPin = TestPin;
        _config.unitTestSsoAccessToken = "sso-" + AuthSecret;
        Core.SetConfigForTesting(_config);
    }

    [TearDown]
    public void TearDown()
    {
        Core.SetConfigForTesting(null);
        Object.DestroyImmediate(_config);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Report_NeverContainsSecrets_InEitherMode(bool includeAll)
    {
        string report = SetupDiagnostics.Build(includeAll);

        Assert.That(report, Does.Not.Contain(AppToken));
        Assert.That(report, Does.Not.Contain(OrgToken));
        Assert.That(report, Does.Not.Contain(AuthSecret));
        Assert.That(report, Does.Not.Contain(TestPin));
        Assert.That(report, Does.Not.Contain("unitTestAuthPin"));
        Assert.That(report, Does.Not.Contain("unitTestSsoAccessToken"));
    }

    [Test]
    public void Report_DescribesSecrets_AndPrintsIdentity()
    {
        string report = SetupDiagnostics.Build(includeAllConfig: false);

        Assert.That(report, Does.Contain("appToken: set (JWT)"));
        Assert.That(report, Does.Contain("orgToken: set (JWT)"));
        Assert.That(report, Does.Contain("authSecret: set"));
        Assert.That(report, Does.Contain("appID: " + AppId));
        Assert.That(report, Does.Contain("restUrl: " + _config.restUrl));
    }

    [Test]
    public void ChangedOnly_OmitsDefaultTuningFields_KeepsChangedOnes()
    {
        _config.telemetryTrackingPeriodSeconds = 42f;

        string report = SetupDiagnostics.Build(includeAllConfig: false);

        Assert.That(report, Does.Contain("telemetryTrackingPeriodSeconds: 42"));
        Assert.That(report, Does.Not.Contain("frameRateTrackingPeriodSeconds"));
    }

    [Test]
    public void ChangedOnly_WithAllDefaults_SaysSo()
    {
        string report = SetupDiagnostics.Build(includeAllConfig: false);

        Assert.That(report, Does.Contain("other settings: all defaults"));
    }

    [Test]
    public void IncludeAll_PrintsEveryTuningField_WithInvariantNumbers()
    {
        string report = SetupDiagnostics.Build(includeAllConfig: true);

        Assert.That(report, Does.Contain("frameRateTrackingPeriodSeconds: 0.5"));
        Assert.That(report, Does.Contain("maximumCachedItems: 1024"));
        Assert.That(report, Does.Contain("enableArborMdmClient: true"));
        Assert.That(report, Does.Not.Contain("other settings: all defaults"));
    }

    [Test]
    public void Report_HasEverySection()
    {
        string report = SetupDiagnostics.Build(includeAllConfig: false);

        foreach (string section in new[] { "Package", "Editor", "Android player settings", "Headset support", "Config", "Sign-in UI", "Setup checks" })
            Assert.That(report, Does.Contain("\n" + section), section);
    }

    [Test]
    public void TryGetLoadedConfig_WithInjectedFixture_ReportsLoaded()
    {
        Core.ConfigState state = Core.TryGetLoadedConfig(out AppConfig loaded);

        Assert.AreEqual(Core.ConfigState.Loaded, state);
        Assert.AreSame(_config, loaded);
        Assert.That(SetupDiagnostics.Build(includeAllConfig: false), Does.Contain("config: Assets/Resources/AbxrLib.asset"));
    }

    // ── Allowlist completeness ────────────────────────────────────────────
    // The report is fail-closed: a field not on a list never prints. These tests make sure "never prints" is a decision
    // someone made, not a field nobody noticed.

    private static IEnumerable<string> AppConfigFieldNames() =>
        typeof(AppConfig).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name);

    private static IEnumerable<string> ListedFieldNames() =>
        SetupDiagnostics.IdentityFields
            .Concat(SetupDiagnostics.TuningFields.Select(f => f.Name))
            .Concat(SetupDiagnostics.ExcludedFields);

    [Test]
    public void EveryAppConfigField_IsReportedOrExplicitlyExcluded()
    {
        var listed = new HashSet<string>(ListedFieldNames());
        List<string> unaccounted = AppConfigFieldNames().Where(n => !listed.Contains(n)).ToList();

        Assert.That(unaccounted, Is.Empty,
            "Add each new AppConfig field to the diagnostics report (through DescribeSecret if it is a secret) or to " +
            "SetupDiagnostics.ExcludedFields: " + string.Join(", ", unaccounted));
    }

    [Test]
    public void ListedFields_AllStillExistOnAppConfig()
    {
        var actual = new HashSet<string>(AppConfigFieldNames());
        List<string> stale = ListedFieldNames().Where(n => !actual.Contains(n)).ToList();

        Assert.That(stale, Is.Empty, "Remove from SetupDiagnostics: " + string.Join(", ", stale));
    }

    [Test]
    public void ListedFields_AppearInExactlyOneList()
    {
        List<string> all = ListedFieldNames().ToList();

        Assert.AreEqual(all.Distinct().Count(), all.Count, "A field is in more than one SetupDiagnostics list.");
    }

    [TestCase(null, true, "not set")]
    [TestCase("", true, "not set")]
    [TestCase("a.b.c", true, "set (JWT)")]
    [TestCase("not-a-token", true, "set (not a JWT)")]
    [TestCase("anything", false, "set")]
    public void DescribeSecret_NeverEchoesTheValue(string value, bool expectJwt, string expected)
    {
        string described = SetupDiagnostics.DescribeSecret(value, expectJwt);

        Assert.AreEqual(expected, described);
        if (!string.IsNullOrEmpty(value)) Assert.That(described, Does.Not.Contain(value));
    }

    [TestCase("https://lib-backend.xrdm.app/", "lib-backend.xrdm.app")]
    [TestCase("https://lib-backend.xrdm.dev/v1/", "lib-backend.xrdm.dev")]
    [TestCase("not a url", "(invalid url)")]
    [TestCase("", "(not set)")]
    [TestCase(null, "(not set)")]
    public void HostOf_ReturnsHostOnly(string url, string expected) =>
        Assert.AreEqual(expected, EnvironmentSummary.HostOf(url));

    [Test]
    public void Describe_CarriesUnityBuildTypeAndHost_NotTheToken()
    {
        var runtime = new Configuration
        {
            buildType = "development",
            useAppTokens = true,
            appToken = AppToken,
            restUrl = "https://lib-backend.xrdm.dev/"
        };

        string line = EnvironmentSummary.Describe(runtime);

        Assert.That(line, Does.Contain("Unity " + Application.unityVersion));
        Assert.That(line, Does.Contain("buildType=development"));
        Assert.That(line, Does.Contain("tokens=on"));
        Assert.That(line, Does.Contain("host=lib-backend.xrdm.dev"));
        Assert.That(line, Does.Not.Contain(AppToken));
    }
}

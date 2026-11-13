// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for RuntimeAuthConfig.HasOrgCredential, which decides whether an org identity beats a stored pairing (SDK-60),
// and for the Development build's App Token standing in as the org token (SDK-85).
using AbxrLib.Runtime.Types;
using NUnit.Framework;

[TestFixture]
public class RuntimeAuthConfigTests
{
    [TestCase(true, "org.token.jwt", null, null, ExpectedResult = true)]
    [TestCase(true, null, "87654321-4321-4321-4321-210987654321", "secret", ExpectedResult = true)] // a dynamic org token's inputs
    [TestCase(false, null, "87654321-4321-4321-4321-210987654321", "secret", ExpectedResult = true)]
    [TestCase(true, "", null, null, ExpectedResult = false)]
    [TestCase(false, null, "87654321-4321-4321-4321-210987654321", "", ExpectedResult = false)] // GetOrgId() falls back to config
    [TestCase(false, null, "", "secret", ExpectedResult = false)]
    [TestCase(false, null, null, null, ExpectedResult = false)]
    public bool HasOrgCredential_NeedsATokenOrAnIdWithItsSecret(bool useAppTokens, string orgToken, string orgId, string authSecret) =>
        new RuntimeAuthConfig { useAppTokens = useAppTokens, orgToken = orgToken, orgId = orgId, authSecret = authSecret }.HasOrgCredential();

    private const string AppToken = "app.token.jwt";
    private const string OrgToken = "org.token.jwt";
    private const string OrgId = "87654321-4321-4321-4321-210987654321";

    // SDK-85: in Development the App Token stands in as the org token only when no source supplied an org. Every source
    // leaves its credential in these fields before the fallback runs: an org token from config, the ArborXR client's
    // dynamic token, the org_token intent or URL parameter; or an org id and secret from SetOrgId and SetAuthSecret.
    [TestCase("development", true, null, null, null, ExpectedResult = AppToken)]
    [TestCase("development", true, "", null, null, ExpectedResult = AppToken)]
    [TestCase("development", true, null, OrgId, null, ExpectedResult = AppToken)] // an org id alone isn't a credential
    [TestCase("development", true, OrgToken, null, null, ExpectedResult = OrgToken)]
    [TestCase("development", true, null, OrgId, "secret", ExpectedResult = null)]
    [TestCase("production", true, null, null, null, ExpectedResult = null)]
    [TestCase("production_custom", true, null, null, null, ExpectedResult = null)]
    [TestCase("production_custom", true, OrgToken, null, null, ExpectedResult = OrgToken)]
    [TestCase("development", false, null, null, null, ExpectedResult = null)] // the legacy scheme has no App Token
    [TestCase("development", false, null, OrgId, "secret", ExpectedResult = null)]
    public string UseAppTokenAsOrgTokenInDevelopment_OnlyWhenNothingSuppliedAnOrg(string buildType, bool useAppTokens, string orgToken, string orgId, string authSecret)
    {
        var auth = new RuntimeAuthConfig { buildType = buildType, useAppTokens = useAppTokens, appToken = AppToken, orgToken = orgToken, orgId = orgId, authSecret = authSecret };
        auth.UseAppTokenAsOrgTokenInDevelopment();
        return auth.orgToken;
    }
}

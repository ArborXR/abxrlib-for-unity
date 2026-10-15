// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for RuntimeAuthConfig.HasOrgCredential, which decides whether an org identity beats a stored pairing (SDK-60).
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
}

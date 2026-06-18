using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Credential-source and build-type behavior for app-token and legacy auth modes.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator Auth_AppTokens_ProductionCustom_UsesConfiguredOrgToken_AndOmitsLegacyFields()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            // Populate legacy fields too, so the test proves app-token mode does not leak them.
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "legacy-secret-should-not-leak";

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.IsNotNull(req.BodyJson, "auth body should be JSON");
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"], "appToken should be sent in the body");
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"], "orgToken should be sent in the body");
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_AppTokens_Production_IgnoresConfiguredOrgToken_AndUsesRuntimeOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            // Production/shared builds must not send the build-time org token from config.
            // Runtime org identification comes from MDM, query/intent, or these explicit overrides.
            Abxr.SetOrgId("00000000-0000-0000-0000-000000000033");
            Abxr.SetAuthSecret("runtime-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);

            var orgToken = (string)req.BodyJson["orgToken"];
            Assert.IsFalse(string.IsNullOrEmpty(orgToken), "production auth still needs runtime org identification before sending");
            Assert.AreNotEqual(FakeOrgToken, orgToken,
                "production app-token auth should ignore the configured orgToken and use runtime-provided org credentials instead");
            Assert.AreEqual(3, orgToken.Split('.').Length, "runtime orgToken should be JWT-shaped");

            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_AppTokens_Development_UsesConfiguredOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "development";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "development builds may use the configured orgToken for local/custom testing");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_Legacy_Production_IgnoresConfiguredOrgCredentials_AndUsesRuntimeOrgCredentials()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "configured-secret-should-not-be-sent";

            // Production/shared builds do not trust orgID/authSecret from the build-time config.
            Abxr.SetOrgId("00000000-0000-0000-0000-000000000033");
            Abxr.SetAuthSecret("runtime-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000033", (string)req.BodyJson["orgId"],
                "production legacy auth should use runtime-provided orgId, not the configured orgID");
            Assert.AreEqual("runtime-fingerprint-secret", (string)req.BodyJson["authSecret"],
                "production legacy auth should use runtime-provided authSecret/fingerprint, not the configured authSecret");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
        }

        [UnityTest]
        public IEnumerator Auth_Legacy_Development_UsesConfiguredOrgCredentials()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "development";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "configured-development-secret";

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000022", (string)req.BodyJson["orgId"],
                "development legacy auth should use the configured orgID");
            Assert.AreEqual("configured-development-secret", (string)req.BodyJson["authSecret"],
                "development legacy auth should use the configured authSecret");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
        }

        [UnityTest]
        public IEnumerator Auth_Legacy_ProductionCustom_UsesConfiguredOrgCredentials_AndOmitsAppTokens()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production_custom";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "legacy-secret";

            // Populate app-token fields too, so the test proves legacy mode does not leak them.
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000022", (string)req.BodyJson["orgId"]);
            Assert.AreEqual("legacy-secret", (string)req.BodyJson["authSecret"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
        }

        [UnityTest]
        public IEnumerator Auth_Fails_BeforeNetwork_When_OrganizationIdentificationUnavailable()
        {
            Configuration.Instance.orgToken = null;

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait(timeoutSeconds: 3f);

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("Organization identification unavailable"));
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "credential validation should fail before a REST auth request is sent");
        }

        [UnityTest]
        public IEnumerator Auth_LegacyMode_MissingOrgCredentials_FailsBeforeNetwork()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production_custom";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = null;
            c.authSecret = null;
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait(timeoutSeconds: 3f);

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("Organization identification unavailable"));
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "invalid legacy credentials should fail before a REST auth request is sent");
        }

        [UnityTest]
        public IEnumerator Auth_AppTokenMode_BuildsDynamicOrgToken_FromOverrides()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = null;

            Abxr.SetOrgId("00000000-0000-0000-0000-000000000022");
            Abxr.SetAuthSecret("device-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            var dynamicOrgToken = (string)req.BodyJson["orgToken"];
            Assert.IsFalse(string.IsNullOrEmpty(dynamicOrgToken),
                "dynamic orgToken should be generated from orgId + authSecret overrides");
            Assert.AreEqual(3, dynamicOrgToken.Split('.').Length,
                "dynamic orgToken should be a JWT-shaped compact token");

            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture]
    public class PlatformConditionalAuthTests : AbxrIntegrationTestFixture
    {
        private const string AlternateOrgToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhbHRlcm5hdGUtb3JnIn0.c2ln";

        private static string BuildHandoffJson()
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "Token", ValidJwtWithExpiration },
                { "Secret", "handoff-secret" },
                { "AppId", "00000000-0000-0000-0000-000000000001" },
                { "UserId", "handoff-user" },
                { "UserData", new Dictionary<string, string> { { "source", "handoff" } } },
                { "Modules", Array.Empty<object>() }
            });
        }

        private IEnumerator RecreateSubsystemWith(FakeAuthPlatformSource platformSource)
        {
            AbxrTestHooks.DestroySubsystemForTest(clearConfiguration: false);
            yield return null;
            AbxrTestHooks.SetPlatformSourceForTest(platformSource);
            AbxrTestHooks.CreateSubsystemForTest();
            yield return null;
            Assert.IsTrue(AbxrTestHooks.HasSubsystemInstance, "AbxrSubsystem should have been recreated with the fake platform source.");
        }

        [UnityTest]
        public IEnumerator WebGl_Production_UsesOrgTokenFromUrl_AndPersistentDeviceId()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = null;

            var platform = new FakeAuthPlatformSource
            {
                IsWebGlPlayer = true,
                AbsoluteUrl = "https://example.test/index.html?org_token=" + Uri.EscapeDataString(FakeOrgToken),
                WebGlDeviceId = "webgl-device-123"
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "WebGL production builds should accept runtime org_token from the page URL.");
            Assert.AreEqual("webgl-device-123", (string)req.BodyJson["deviceId"],
                "WebGL should use the persistent WebGL device id source instead of SystemInfo.deviceUniqueIdentifier.");
        }

        [UnityTest]
        public IEnumerator WebGl_ProductionCustom_IgnoresUrlCredentials_AndRequiresNormalPinPrompt()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            QueueAssessmentPinConfig();

            var platform = new FakeAuthPlatformSource
            {
                IsWebGlPlayer = true,
                AbsoluteUrl = "https://example.test/index.html?org_token=" + Uri.EscapeDataString(AlternateOrgToken) + "&assessment_pin=123456",
                WebGlDeviceId = "webgl-custom-device"
            };
            yield return RecreateSubsystemWith(platform);

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) =>
            {
                inputRequested = true;
                Abxr.OnInputSubmitted("manual-pin");
            };

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsTrue(inputRequested,
                "production_custom should ignore URL assessment_pin and keep the normal app-driven prompt flow.");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "Expected device auth plus user auth after manual PIN submission.");
            Assert.AreEqual(FakeOrgToken, (string)requests[0].BodyJson["orgToken"],
                "production_custom should keep the configured org token even when the URL has a different org_token.");
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
            Assert.AreEqual("manual-pin", (string)mechanism?["prompt"],
                "URL PIN should not be auto-submitted for production_custom builds.");
        }

        [UnityTest]
        public IEnumerator WebGl_AssessmentPinUrl_AutoSubmits_FirstUserAuthAttempt()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = null;

            QueueAssessmentPinConfig();

            var platform = new FakeAuthPlatformSource
            {
                IsWebGlPlayer = true,
                AbsoluteUrl = "https://example.test/index.html?org_token=" + Uri.EscapeDataString(FakeOrgToken) + "&assessment_pin=123456",
                WebGlDeviceId = "webgl-autopin-device"
            };
            yield return RecreateSubsystemWith(platform);

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "The first WebGL assessment_pin should be auto-submitted without raising OnInputRequested.");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "Expected device auth followed by the auto-submitted user auth.");
            Assert.IsNull(requests[0].BodyJson["authMechanism"]);
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
            Assert.AreEqual("123456", (string)mechanism?["prompt"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
        }

        [UnityTest]
        public IEnumerator WebGl_AuthHandoffFromUrl_SkipsDeviceAuth_ButFetchesConfig()
        {
            var platform = new FakeAuthPlatformSource
            {
                IsWebGlPlayer = true,
                AbsoluteUrl = "https://example.test/index.html?auth_handoff=" + Uri.EscapeDataString(BuildHandoffJson()),
                WebGlDeviceId = "webgl-handoff-device"
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "A valid WebGL auth_handoff should adopt the supplied session and skip device auth.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count,
                "The adopted handoff session should still fetch config using the supplied auth headers.");
            Assert.IsTrue(AbxrTestHooks.GetAuthServiceForTest()?.SessionUsedAuthHandoff() ?? false);
        }

        [UnityTest]
        public IEnumerator Standalone_Production_UsesOrgTokenFromDesktopSources()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = AlternateOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsStandalonePlayer = true,
                DesktopOrgToken = FakeOrgToken
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "Standalone production builds should accept org_token from desktop sources such as CLI/file.");
            Assert.AreNotEqual(AlternateOrgToken, (string)req.BodyJson["orgToken"],
                "production standalone builds should not trust build-time orgToken when desktop sources provide one.");
            Assert.AreEqual("none", (string)req.BodyJson["partner"],
                "Desktop auth should not be marked as Arbor MDM sourced.");
        }

        [UnityTest]
        public IEnumerator Standalone_Development_UsesOrgTokenFromDesktopSources_AndOverridesConfiguredOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "development";
            c.appToken = FakeAppToken;
            c.orgToken = AlternateOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsStandalonePlayer = true,
                DesktopOrgToken = FakeOrgToken
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "Standalone development builds are not production_custom, so desktop org_token input should be accepted.");
            Assert.AreNotEqual(AlternateOrgToken, (string)req.BodyJson["orgToken"],
                "desktop org_token should override configured development orgToken.");
            Assert.AreEqual("none", (string)req.BodyJson["partner"],
                "Desktop auth should not be marked as Arbor MDM sourced.");
        }

        [UnityTest]
        public IEnumerator Standalone_ProductionCustom_IgnoresDesktopOrgToken_AndKeepsConfiguredOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsStandalonePlayer = true,
                DesktopOrgToken = AlternateOrgToken
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "production_custom standalone builds should keep configured org credentials and ignore desktop org_token input.");
        }

        [UnityTest]
        public IEnumerator Standalone_AuthHandoffFromCommandLine_SkipsDeviceAuth_ButFetchesConfig()
        {
            var platform = new FakeAuthPlatformSource
            {
                IsStandalonePlayer = true
            };
            platform.CommandLineArgs["auth_handoff"] = BuildHandoffJson();
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "A valid standalone auth_handoff command-line argument should adopt the supplied session and skip device auth.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count,
                "The adopted handoff session should still fetch config using the supplied auth headers.");
            Assert.IsTrue(AbxrTestHooks.GetAuthServiceForTest()?.SessionUsedAuthHandoff() ?? false);
        }

        [UnityTest]
        public IEnumerator Android_IntentOrgToken_AndSessionMetadata_AreApplied()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = null;
            c.recordIpAddress = true;

            var platform = new FakeAuthPlatformSource
            {
                IsAndroidPlayer = true,
                IpAddress = "192.0.2.10",
                XrdmVersion = "1.2.3.4"
            };
            platform.AndroidIntentParams["org_token"] = FakeOrgToken;
            platform.AndroidManifestMetadata["com.arborxr.abxrlib.build_fingerprint"] = "android-build-fingerprint";
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "Android production builds should accept org_token from the launch intent when no org token is configured.");
            Assert.AreEqual("192.0.2.10", (string)req.BodyJson["ipAddress"]);
            Assert.AreEqual("android-build-fingerprint", (string)req.BodyJson["buildFingerprint"]);
            Assert.AreEqual("1.2.3.4", (string)req.BodyJson["xrdmVersion"]);
        }

        [UnityTest]
        public IEnumerator Android_AuthHandoffFromIntent_SkipsDeviceAuth_ButFetchesConfig()
        {
            var platform = new FakeAuthPlatformSource
            {
                IsAndroidPlayer = true
            };
            platform.AndroidIntentParams["auth_handoff"] = BuildHandoffJson();
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "A valid Android auth_handoff intent extra should adopt the supplied session and skip device auth.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count,
                "The adopted handoff session should still fetch config using the supplied auth headers.");
            Assert.IsTrue(AbxrTestHooks.GetAuthServiceForTest()?.SessionUsedAuthHandoff() ?? false);
        }

        [UnityTest]
        public IEnumerator Android_ArborMdmNonProductionCustom_AppTokens_BuildsDynamicOrgTokenFromMdm()
        {
            const string mdmOrgId = "00000000-0000-0000-0000-000000004242";
            const string mdmFingerprint = "mdm-fingerprint-app-token-secret";

            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "development";
            c.appToken = FakeAppToken;
            c.orgToken = AlternateOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsAndroidPlayer = true,
                ArborMdmConnected = true,
                CurrentDeviceId = "mdm-device-app-token",
                CurrentOrgId = mdmOrgId,
                CurrentFingerprint = mdmFingerprint,
                CurrentDeviceTags = new[] { "classroom", "app-token" }
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);

            var dynamicOrgToken = (string)req.BodyJson["orgToken"];
            Assert.AreEqual(Utils.BuildOrgTokenDynamic(mdmOrgId, mdmFingerprint), dynamicOrgToken,
                "non-production_custom app-token auth should build orgToken from Arbor MDM org id + fingerprint.");
            Assert.AreNotEqual(AlternateOrgToken, dynamicOrgToken,
                "non-production_custom app-token auth should replace configured orgToken with Arbor MDM data when MDM is connected.");

            var orgTokenPayload = Utils.TryDecodeJwtPayload(dynamicOrgToken);
            Assert.IsNotNull(orgTokenPayload, "Arbor MDM dynamic org token payload should be decodable.");
            Assert.AreEqual(mdmOrgId, (string)orgTokenPayload["orgId"],
                "Arbor MDM dynamic org token should be built from the MDM org id.");

            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
            Assert.AreEqual("mdm-device-app-token", (string)req.BodyJson["deviceId"]);
            Assert.AreEqual("arborxr", (string)req.BodyJson["partner"]);
            CollectionAssert.AreEqual(
                new[] { "classroom", "app-token" },
                req.BodyJson["tags"]?.Select(t => (string)t).ToArray());
        }

        [UnityTest]
        public IEnumerator Android_ArborMdmNonProductionCustom_Legacy_UsesMdmOrgCredentials()
        {
            const string configuredOrgId = "00000000-0000-0000-0000-000000000022";
            const string mdmOrgId = "00000000-0000-0000-0000-000000004242";
            const string configuredAuthSecret = "configured-development-secret";
            const string mdmFingerprint = "mdm-fingerprint-legacy-secret";

            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "development";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = configuredOrgId;
            c.authSecret = configuredAuthSecret;
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsAndroidPlayer = true,
                ArborMdmConnected = true,
                CurrentDeviceId = "mdm-device-legacy",
                CurrentOrgId = mdmOrgId,
                CurrentFingerprint = mdmFingerprint,
                CurrentDeviceTags = new[] { "classroom", "legacy" }
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual(mdmOrgId, (string)req.BodyJson["orgId"],
                "non-production_custom legacy auth should replace configured orgID with the Arbor MDM org id.");
            Assert.AreEqual(mdmFingerprint, (string)req.BodyJson["authSecret"],
                "non-production_custom legacy auth should replace configured authSecret with the Arbor MDM fingerprint.");
            Assert.AreNotEqual(configuredOrgId, (string)req.BodyJson["orgId"]);
            Assert.AreNotEqual(configuredAuthSecret, (string)req.BodyJson["authSecret"]);

            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
            Assert.AreEqual("mdm-device-legacy", (string)req.BodyJson["deviceId"]);
            Assert.AreEqual("arborxr", (string)req.BodyJson["partner"]);
            CollectionAssert.AreEqual(
                new[] { "classroom", "legacy" },
                req.BodyJson["tags"]?.Select(t => (string)t).ToArray());
        }

        [UnityTest]
        public IEnumerator Android_ArborMdmProductionCustom_AppliesDevicePartnerAndTags_WithoutReplacingConfiguredCredentials()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            var platform = new FakeAuthPlatformSource
            {
                IsAndroidPlayer = true,
                ArborMdmConnected = true,
                CurrentDeviceId = "mdm-device-42",
                CurrentOrgId = "00000000-0000-0000-0000-000000004242",
                CurrentFingerprint = "mdm-fingerprint-secret",
                CurrentDeviceTags = new[] { "classroom", "pilot" }
            };
            yield return RecreateSubsystemWith(platform);

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "production_custom must keep configured org credentials even when Arbor MDM is connected.");
            Assert.AreEqual("mdm-device-42", (string)req.BodyJson["deviceId"]);
            Assert.AreEqual("arborxr", (string)req.BodyJson["partner"]);
            CollectionAssert.AreEqual(
                new[] { "classroom", "pilot" },
                req.BodyJson["tags"]?.Select(t => (string)t).ToArray());
        }
    }
}

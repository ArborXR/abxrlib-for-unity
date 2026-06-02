using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="RuntimeAuthConfig.ValidateAuthFields"/> and <see cref="AuthResponse.IsValidSuccess"/>
    /// </summary>
    [TestFixture]
    public class ValidationTests
    {
        // A structurally-valid JWT (three base64url segments). Signature is fake;
        // ValidateAuthFields only checks shape, not signature correctness.
        private const string FakeJwt = "eyJ0eXAiOiJKV1QifQ.eyJzdWIiOiJ0ZXN0In0.c2ln";
        private const string FakeUuid = "00000000-0000-0000-0000-000000000001";

        // ── App-token mode ───────────────────────────────────────────

        [Test]
        public void AppTokens_Production_RequiresAppToken_OrgTokenOptional()
        {
            Assert.IsNull(Run(useAppTokens: true, buildType: "production", appToken: FakeJwt));
            Assert.IsNotNull(Run(useAppTokens: true, buildType: "production", appToken: null));
            Assert.IsNotNull(Run(useAppTokens: true, buildType: "production", appToken: "not-a-jwt"));
        }

        [Test]
        public void AppTokens_ProductionCustom_RequiresOrgToken()
        {
            Assert.IsNotNull(Run(useAppTokens: true, buildType: "production_custom", appToken: FakeJwt, orgToken: null));
            Assert.IsNull(Run(useAppTokens: true, buildType: "production_custom", appToken: FakeJwt, orgToken: FakeJwt));
        }

        [Test]
        public void AppTokens_InvalidOrgToken_FailsValidation()
        {
            Assert.IsNotNull(Run(useAppTokens: true, buildType: "production", appToken: FakeJwt, orgToken: "garbage"));
        }

        // ── Legacy (appId / orgId / authSecret) mode ─────────────────

        [Test]
        public void Legacy_Production_RequiresValidAppIdUuid()
        {
            Assert.IsNull(Run(useAppTokens: false, buildType: "production", appId: FakeUuid));
            Assert.IsNotNull(Run(useAppTokens: false, buildType: "production", appId: "not-a-uuid"));
            Assert.IsNotNull(Run(useAppTokens: false, buildType: "production", appId: null));
        }

        [Test]
        public void Legacy_ProductionCustom_RequiresOrgIdAndAuthSecret()
        {
            // Missing both
            Assert.IsNotNull(Run(useAppTokens: false, buildType: "production_custom", appId: FakeUuid));
            // OrgId only
            Assert.IsNotNull(Run(useAppTokens: false, buildType: "production_custom", appId: FakeUuid, orgId: FakeUuid));
            // Both present and valid
            Assert.IsNull(Run(useAppTokens: false, buildType: "production_custom", appId: FakeUuid, orgId: FakeUuid, authSecret: "secret"));
        }

        // ── Build type credential extraction ────────────────────────

        [TestCase("production", false)]
        [TestCase("development", true)]
        [TestCase("production_custom", true)]
        public void ExtractConfigData_AppTokens_BuildTypeControlsConfiguredOrgToken(string buildType, bool expectsConfiguredOrgToken)
        {
            var config = NewConfig(buildType, useAppTokens: true);
            try
            {
                var data = Utils.ExtractConfigData(config);

                Assert.IsTrue(data.isValid, data.errorMessage);
                Assert.AreEqual(buildType, data.buildType);
                Assert.IsTrue(data.useAppTokens);
                Assert.AreEqual(FakeJwt, data.appToken);
                Assert.AreEqual(expectsConfiguredOrgToken ? FakeJwt : null, data.orgToken,
                    "Production/shared app-token builds should not read orgToken from config; development and production_custom should.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [TestCase("production", false)]
        [TestCase("development", true)]
        [TestCase("production_custom", true)]
        public void ExtractConfigData_Legacy_BuildTypeControlsConfiguredOrgCredentials(string buildType, bool expectsConfiguredOrgCredentials)
        {
            var config = NewConfig(buildType, useAppTokens: false);
            try
            {
                var data = Utils.ExtractConfigData(config);

                Assert.IsTrue(data.isValid, data.errorMessage);
                Assert.AreEqual(buildType, data.buildType);
                Assert.IsFalse(data.useAppTokens);
                Assert.AreEqual(FakeUuid, data.appId);
                Assert.AreEqual(expectsConfiguredOrgCredentials ? "00000000-0000-0000-0000-000000000002" : null, data.orgId,
                    "Production/shared legacy builds should not read orgID from config; development and production_custom should.");
                Assert.AreEqual(expectsConfiguredOrgCredentials ? "legacy-secret" : null, data.authSecret,
                    "Production/shared legacy builds should not read authSecret from config; development and production_custom should.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [TestCase("production")]
        [TestCase("development")]
        [TestCase("production_custom")]
        public void PreparePayloadForAuth_AppTokens_RequiresOrgToken_ForEveryBuildType(string buildType)
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = true,
                buildType = buildType,
                appToken = FakeJwt,
                orgToken = null,
            };

            var error = runtime.PreparePayloadForAuth(new AuthPayload());

            Assert.AreEqual("Organization identification unavailable.", error,
                "The backend app-token contract requires both appToken and orgToken before sending, regardless of build type.");
        }

        [TestCase("production")]
        [TestCase("development")]
        [TestCase("production_custom")]
        public void PreparePayloadForAuth_Legacy_RequiresOrgCredentials_ForEveryBuildType(string buildType)
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = false,
                buildType = buildType,
                appId = FakeUuid,
                orgId = null,
                authSecret = null,
            };

            var error = runtime.PreparePayloadForAuth(new AuthPayload());

            Assert.AreEqual("Organization identification unavailable.", error,
                "The backend legacy contract requires appId, orgId, and authSecret before sending, regardless of build type.");
        }

        // ── RuntimeAuthConfig payload preparation ───────────────────

        [Test]
        public void CopyAuthFieldsTo_AppTokenMode_CopiesTokens_AndClearsLegacyFields()
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = true,
                buildType = "production_custom",
                appToken = FakeJwt,
                orgToken = FakeJwt,
                appId = FakeUuid,
                orgId = "00000000-0000-0000-0000-000000000002",
                authSecret = "legacy-secret",
                deviceId = "device-1",
                partner = null,
                tags = new[] { "tag-a" },
            };

            var payload = new AuthPayload
            {
                appId = "old-app-id",
                orgId = "old-org-id",
                authSecret = "old-secret",
            };

            runtime.CopyAuthFieldsTo(payload);

            Assert.AreEqual(FakeJwt, payload.appToken);
            Assert.AreEqual(FakeJwt, payload.orgToken);
            Assert.IsNull(payload.appId);
            Assert.IsNull(payload.orgId);
            Assert.IsNull(payload.authSecret);
            Assert.AreEqual("device-1", payload.deviceId);
            Assert.AreEqual("none", payload.partner);
            CollectionAssert.AreEqual(new[] { "tag-a" }, payload.tags);
        }

        [Test]
        public void CopyAuthFieldsTo_LegacyMode_CopiesLegacyFields_AndClearsTokens()
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = false,
                buildType = "production_custom",
                appToken = FakeJwt,
                orgToken = FakeJwt,
                appId = FakeUuid,
                orgId = "00000000-0000-0000-0000-000000000002",
                authSecret = "legacy-secret",
                deviceId = "device-1",
                partner = "arborxr",
                tags = new[] { "tag-b" },
            };

            var payload = new AuthPayload
            {
                appToken = "old-app-token",
                orgToken = "old-org-token",
            };

            runtime.CopyAuthFieldsTo(payload);

            Assert.AreEqual(FakeUuid, payload.appId);
            Assert.AreEqual("00000000-0000-0000-0000-000000000002", payload.orgId);
            Assert.AreEqual("legacy-secret", payload.authSecret);
            Assert.IsNull(payload.appToken);
            Assert.IsNull(payload.orgToken);
            Assert.AreEqual("device-1", payload.deviceId);
            Assert.AreEqual("arborxr", payload.partner);
            CollectionAssert.AreEqual(new[] { "tag-b" }, payload.tags);
        }

        [Test]
        public void PreparePayloadForAuth_ReturnsValidationError_AndDoesNotCopy_WhenInvalid()
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = true,
                buildType = "production_custom",
                appToken = FakeJwt,
                orgToken = null,
            };
            var payload = new AuthPayload { appToken = "old-app-token" };

            var error = runtime.PreparePayloadForAuth(payload);

            Assert.AreEqual("Organization identification unavailable.", error);
            Assert.AreEqual("old-app-token", payload.appToken,
                "invalid auth config should not partially overwrite the outgoing payload");
        }

        [Test]
        public void PreparePayloadForAuth_CopiesSelectedMode_WhenValid()
        {
            var runtime = new RuntimeAuthConfig
            {
                useAppTokens = false,
                buildType = "production_custom",
                appToken = FakeJwt,
                orgToken = FakeJwt,
                appId = FakeUuid,
                orgId = "00000000-0000-0000-0000-000000000002",
                authSecret = "legacy-secret",
            };
            var payload = new AuthPayload { appToken = "old-app-token", orgToken = "old-org-token" };

            var error = runtime.PreparePayloadForAuth(payload);

            Assert.IsNull(error);
            Assert.AreEqual(FakeUuid, payload.appId);
            Assert.AreEqual("00000000-0000-0000-0000-000000000002", payload.orgId);
            Assert.AreEqual("legacy-secret", payload.authSecret);
            Assert.IsNull(payload.appToken);
            Assert.IsNull(payload.orgToken);
        }

        // ── AuthResponse.IsValidSuccess ──────────────────────────────

        [Test]
        public void AuthResponse_TokenAndSecret_IsSuccess()
        {
            Assert.IsTrue(AuthResponse.IsValidSuccess(new AuthResponse { Token = "anything", Secret = "secret" }));
        }

        [Test]
        public void AuthResponse_TokenWithoutSecret_IsFailure()
        {
            Assert.IsFalse(AuthResponse.IsValidSuccess(new AuthResponse { Token = "anything" }));
        }

        [Test]
        public void AuthResponse_ModulesAlone_IsFailure_ForRestOnly()
        {
            var resp = new AuthResponse
            {
                Modules = new System.Collections.Generic.List<ModuleData>
                {
                    new ModuleData { Id = "m1", Name = "Module 1" },
                },
            };
            Assert.IsFalse(AuthResponse.IsValidSuccess(resp));
        }

        [Test]
        public void AuthResponse_AppIdAlone_IsFailure_ForRestOnly()
        {
            Assert.IsFalse(AuthResponse.IsValidSuccess(new AuthResponse { AppId = "some-app" }));
        }

        [Test]
        public void AuthResponse_EmptyOrNull_IsFailure()
        {
            Assert.IsFalse(AuthResponse.IsValidSuccess(null));
            Assert.IsFalse(AuthResponse.IsValidSuccess(new AuthResponse()));
        }

        // ── helper ───────────────────────────────────────────────────

        private static AppConfig NewConfig(string buildType, bool useAppTokens)
        {
            var config = ScriptableObject.CreateInstance<AppConfig>();
            config.buildType = buildType;
            config.useAppTokens = useAppTokens;
            config.appID = FakeUuid;
            config.orgID = "00000000-0000-0000-0000-000000000002";
            config.authSecret = "legacy-secret";
            config.appToken = FakeJwt;
            config.orgToken = FakeJwt;
            config.restUrl = "https://example.test/";
            return config;
        }

        private static string Run(bool useAppTokens, string buildType, string appId = null, string orgId = null,
            string authSecret = null, string appToken = null, string orgToken = null)
        {
            return RuntimeAuthConfig.ValidateAuthFields(useAppTokens, buildType, appId, orgId, authSecret, appToken, orgToken);
        }
    }
}

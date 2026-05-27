using AbxrLib.Runtime.Types;
using NUnit.Framework;

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

        // ── AuthResponse.IsValidSuccess ──────────────────────────────

        [Test]
        public void AuthResponse_TokenAlone_IsSuccess()
        {
            Assert.IsTrue(AuthResponse.IsValidSuccess(new AuthResponse { Token = "anything" }));
        }

        [Test]
        public void AuthResponse_ModulesAlone_IsSuccess()
        {
            var resp = new AuthResponse
            {
                Modules = new System.Collections.Generic.List<ModuleData>
                {
                    new ModuleData { Id = "m1", Name = "Module 1" },
                },
            };
            Assert.IsTrue(AuthResponse.IsValidSuccess(resp));
        }

        [Test]
        public void AuthResponse_AppIdAlone_IsSuccess_TwoStage()
        {
            // No token/modules but AppId present -> second-stage required, still considered a valid success
            Assert.IsTrue(AuthResponse.IsValidSuccess(new AuthResponse { AppId = "some-app" }));
        }

        [Test]
        public void AuthResponse_EmptyOrNull_IsFailure()
        {
            Assert.IsFalse(AuthResponse.IsValidSuccess(null));
            Assert.IsFalse(AuthResponse.IsValidSuccess(new AuthResponse()));
        }

        // ── helper ───────────────────────────────────────────────────

        private static string Run(bool useAppTokens, string buildType, string appId = null, string orgId = null,
            string authSecret = null, string appToken = null, string orgToken = null)
        {
            return RuntimeAuthConfig.ValidateAuthFields(useAppTokens, buildType, appId, orgId, authSecret, appToken, orgToken);
        }
    }
}

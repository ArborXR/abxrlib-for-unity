using System;
using System.Collections.Generic;
using System.Text;
using AbxrLib.Runtime.Services.Auth;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for the SSO/JWT user-data merge helper used by AbxrAuthService.
    /// </summary>
    [TestFixture]
    public class SsoUserDataMergerTests
    {
        [Test]
        public void AccessTokenHasUsableIdentity_ReturnsTrue_ForEmailOnlyClaim()
        {
            string token = JwtWithPayloadJson("{\"email\":\"learner@example.com\"}");

            Assert.IsTrue(SsoUserDataMerger.AccessTokenHasUsableIdentity(token));
        }

        [Test]
        public void AccessTokenHasUsableIdentity_ReturnsFalse_WhenPayloadHasNoIdentityClaims()
        {
            string token = JwtWithPayloadJson("{\"role\":\"learner\"}");

            Assert.IsFalse(SsoUserDataMerger.AccessTokenHasUsableIdentity(token));
        }

        [Test]
        public void TryMergeAccessTokenIntoUserData_AddsClaimsAndBackfillsEmail()
        {
            var userData = new Dictionary<string, string>
            {
                { "cohort", "A" }
            };
            string token = JwtWithPayloadJson("{\"sub\":\"sso-subject\",\"preferred_username\":\"sso.preferred@example.com\"}");

            bool changed = SsoUserDataMerger.TryMergeAccessTokenIntoUserData(token, userData);

            Assert.IsTrue(changed);
            Assert.AreEqual("A", userData["cohort"]);
            Assert.AreEqual("sso-subject", userData["sub"]);
            Assert.AreEqual("sso.preferred@example.com", userData["preferred_username"]);
            Assert.AreEqual("sso.preferred@example.com", userData["email"]);
        }

        [Test]
        public void TryMergeAccessTokenIntoUserData_StoresConflictingClaimsWithSsoPrefixAndSuffix()
        {
            var userData = new Dictionary<string, string>
            {
                { "sub", "existing-sub" },
                { "sso_sub", "existing-sso-sub" }
            };
            string token = JwtWithPayloadJson("{\"sub\":\"jwt-sub\"}");

            bool changed = SsoUserDataMerger.TryMergeAccessTokenIntoUserData(token, userData);

            Assert.IsTrue(changed);
            Assert.AreEqual("existing-sub", userData["sub"]);
            Assert.AreEqual("existing-sso-sub", userData["sso_sub"]);
            Assert.AreEqual("jwt-sub", userData["sso_sub_1"]);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-jwt")]
        public void TryMergeAccessTokenIntoUserData_ReturnsFalse_ForMissingOrInvalidJwt(string token)
        {
            var userData = new Dictionary<string, string>();

            Assert.IsFalse(SsoUserDataMerger.TryMergeAccessTokenIntoUserData(token, userData));
            Assert.AreEqual(0, userData.Count);
        }

        private static string JwtWithPayloadJson(string payloadJson)
        {
            const string headerJson = "{\"typ\":\"JWT\",\"alg\":\"HS256\"}";
            return $"{Base64UrlEncode(headerJson)}.{Base64UrlEncode(payloadJson)}.c2ln";
        }

        private static string Base64UrlEncode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}

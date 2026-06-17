using System;
using System.Collections.Generic;
using System.Text;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for auth_handoff payload normalization and serialization.
    /// </summary>
    [TestFixture]
    public class AuthHandoffPayloadTests
    {
        [Test]
        public void Normalize_ReturnsTrimmedRawJson()
        {
            const string json = "{\"Token\":\"token\"}";

            Assert.AreEqual(json, AuthHandoffPayload.Normalize("  " + json + "  "));
        }

        [Test]
        public void Normalize_DecodesBase64Json()
        {
            const string json = "{\"Token\":\"token\"}";
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            Assert.AreEqual(json, AuthHandoffPayload.Normalize(encoded));
        }

        [Test]
        public void Normalize_ReturnsRawNonBase64Value_ForAuthResponseValidation()
        {
            Assert.AreEqual("not-json-and-not-base64", AuthHandoffPayload.Normalize(" not-json-and-not-base64 "));
        }

        [Test]
        public void Normalize_ReturnsNull_WhenBase64DecodesToNonJson()
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-json"));

            Assert.IsNull(AuthHandoffPayload.Normalize(encoded));
        }

        [Test]
        public void Build_IncludesSessionCredentialsRuntimeFieldsReturnPackageAndJwtExpiry()
        {
            var response = new AuthResponse
            {
                Token = "token-123",
                Secret = "secret-123",
                AppId = "app-from-response",
                UserId = "user-123",
                UserData = new Dictionary<string, string>
                {
                    { "id", "learner-123" },
                    { "email", "learner@example.com" }
                }
            };
            var payload = new AuthPayload
            {
                appId = "app-from-payload",
                deviceId = "device-123",
                appToken = "app-token-123",
                orgToken = "org-token-123",
                orgId = "org-123"
            };
            var expiry = DateTimeOffset.FromUnixTimeMilliseconds(4102444800000L).UtcDateTime;

            string json = AuthHandoffPayload.Build(response, payload, expiry, "com.example.launcher");
            var handoff = JObject.Parse(json);

            Assert.AreEqual("token-123", (string)handoff["Token"]);
            Assert.AreEqual("secret-123", (string)handoff["Secret"]);
            Assert.AreEqual("app-from-response", (string)handoff["AppId"]);
            Assert.AreEqual("user-123", (string)handoff["UserId"]);
            Assert.AreEqual("learner-123", (string)handoff["UserData"]?["id"]);
            Assert.AreEqual("learner@example.com", (string)handoff["UserData"]?["email"]);
            Assert.AreEqual("device-123", (string)handoff["DeviceId"]);
            Assert.AreEqual("app-token-123", (string)handoff["AppToken"]);
            Assert.AreEqual("org-token-123", (string)handoff["OrgToken"]);
            Assert.AreEqual("org-123", (string)handoff["OrgId"]);
            Assert.AreEqual(4102444800000L, (long)handoff["TokenExpirationMs"]);
            Assert.AreEqual("com.example.launcher", (string)handoff["ReturnToPackage"]);
        }

        [Test]
        public void Build_UsesPayloadAppId_WhenResponseAppIdMissing()
        {
            var response = new AuthResponse
            {
                Token = "token-123",
                Secret = "secret-123"
            };
            var payload = new AuthPayload
            {
                appId = "payload-app-id"
            };

            string json = AuthHandoffPayload.Build(response, payload, DateTime.UtcNow.AddMinutes(5));
            var handoff = JObject.Parse(json);

            Assert.AreEqual("payload-app-id", (string)handoff["AppId"]);
            Assert.IsFalse(handoff.ContainsKey("ReturnToPackage"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    [TestFixture]
    public class AuthSessionStateTests
    {
        [Test]
        public void TryApply_NormalizesResponse_AndCapturesSessionState()
        {
            var sessionState = new AuthSessionState();
            var response = new AuthResponse
            {
                Token = JwtWithPayloadJson("{\"exp\":4102444800}"),
                Secret = "secret",
                UserData = null,
                Modules = new List<ModuleData>
                {
                    new ModuleData { Id = "second", Order = 2 },
                    new ModuleData { Id = "first", Order = 1 }
                }
            };

            Assert.IsTrue(sessionState.TryApply(response, "test"));

            Assert.AreSame(response, sessionState.ResponseData);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(4102444800).UtcDateTime, sessionState.TokenExpiryUtc);
            Assert.IsNotNull(sessionState.ResponseData.UserData);
            Assert.IsNotNull(sessionState.UserDataSnapshot);
            Assert.AreNotSame(sessionState.ResponseData.UserData, sessionState.UserDataSnapshot);
            Assert.AreEqual("first", sessionState.ResponseData.Modules[0].Id);
            Assert.AreEqual("second", sessionState.ResponseData.Modules[1].Id);
        }

        [Test]
        public void SetUserDataSnapshot_CopiesInputDictionary()
        {
            var sessionState = new AuthSessionState();
            var userData = new Dictionary<string, string> { ["id"] = "learner-1" };

            sessionState.SetUserDataSnapshot(userData);
            userData["id"] = "changed";

            Assert.AreEqual("learner-1", sessionState.UserDataSnapshot["id"]);
        }

        [Test]
        public void Clear_ResetsSessionState()
        {
            var sessionState = new AuthSessionState();
            sessionState.SetResponseData(new AuthResponse
            {
                Token = "token",
                Secret = "secret",
                UserData = new Dictionary<string, string> { ["id"] = "learner-1" }
            });
            sessionState.SetTokenExpiryUtc(DateTime.UtcNow.AddMinutes(5));
            sessionState.MarkAuthenticated();

            sessionState.Clear();

            Assert.IsFalse(sessionState.Authenticated);
            Assert.IsNotNull(sessionState.ResponseData);
            Assert.AreEqual(DateTime.MinValue, sessionState.TokenExpiryUtc);
            Assert.IsNull(sessionState.UserDataSnapshot);
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

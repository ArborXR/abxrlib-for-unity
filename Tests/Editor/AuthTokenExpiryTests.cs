using System;
using System.Text;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for JWT expiration handling in <see cref="AuthSessionState"/>.
    /// </summary>
    [TestFixture]
    public class AuthTokenExpiryTests
    {
        [Test]
        public void TrySetTokenExpiryFromJwt_ReturnsFalse_WhenTokenCannotBeDecoded()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Invalid JWT token format - expected 3 parts, got 1"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Failed to decode JWT token"));

            Assert.IsFalse(InvokeTrySetTokenExpiryFromJwt("not-a-jwt"));
        }

        [TestCase("{}")]
        [TestCase("{\"sub\":\"user-without-exp\"}")]
        [TestCase("{\"exp\":null}")]
        public void TrySetTokenExpiryFromJwt_ReturnsFalse_WhenExpirationMissingOrNull(string payloadJson)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] JWT token missing expiration field"));

            Assert.IsFalse(InvokeTrySetTokenExpiryFromJwt(JwtWithPayloadJson(payloadJson)));
        }

        [TestCase("{\"exp\":\"not-a-number\"}")]
        [TestCase("{\"exp\":253402300800}")]
        public void TrySetTokenExpiryFromJwt_ReturnsFalse_WhenExpirationIsInvalid(string payloadJson)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Invalid JWT token expiration:"));

            Assert.IsFalse(InvokeTrySetTokenExpiryFromJwt(JwtWithPayloadJson(payloadJson)));
        }

        [Test]
        public void TrySetTokenExpiryFromJwt_ReturnsTrue_WhenExpirationIsValid()
        {
            var sessionState = new AuthSessionState();
            string token = JwtWithPayloadJson("{\"exp\":4102444800}");

            Assert.IsTrue(sessionState.TrySetTokenExpiryFromJwt(token));
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(4102444800).UtcDateTime, sessionState.TokenExpiryUtc);
        }

        private static bool InvokeTrySetTokenExpiryFromJwt(string token) =>
            new AuthSessionState().TrySetTokenExpiryFromJwt(token);

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

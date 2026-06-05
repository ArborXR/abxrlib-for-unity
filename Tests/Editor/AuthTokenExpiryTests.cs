using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for JWT expiration handling in <see cref="AbxrAuthService"/>.
    /// These use reflection because the production method is intentionally private, and they avoid PlayMode/backend setup.
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
            var service = NewUninitializedAuthServiceForTokenExpiryTests();
            string token = JwtWithPayloadJson("{\"exp\":4102444800}");

            Assert.IsTrue(InvokeTrySetTokenExpiryFromJwt(service, token));
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(4102444800).UtcDateTime, GetTokenExpiry(service));
        }

        private static bool InvokeTrySetTokenExpiryFromJwt(string token) =>
            InvokeTrySetTokenExpiryFromJwt(NewUninitializedAuthServiceForTokenExpiryTests(), token);

        private static bool InvokeTrySetTokenExpiryFromJwt(AbxrAuthService service, string token)
        {
            MethodInfo method = typeof(AbxrAuthService).GetMethod("TrySetTokenExpiryFromJwt", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected AbxrAuthService.TrySetTokenExpiryFromJwt to exist.");
            return (bool)method.Invoke(service, new object[] { token });
        }

        private static DateTime GetTokenExpiry(AbxrAuthService service)
        {
            FieldInfo field = typeof(AbxrAuthService).GetField("_tokenExpiry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected AbxrAuthService._tokenExpiry to exist.");
            return (DateTime)field.GetValue(service);
        }

        private static AbxrAuthService NewUninitializedAuthServiceForTokenExpiryTests() =>
            (AbxrAuthService)FormatterServices.GetUninitializedObject(typeof(AbxrAuthService));

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

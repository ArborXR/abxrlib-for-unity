using System.Reflection;
using NUnit.Framework;
using UnityEngine.Networking;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Integration tests for the auth flow. Test methods are split by behavior area while sharing one fixture lifecycle and helper surface.
    /// </summary>
    [TestFixture]
    public partial class AuthenticationTests : AbxrIntegrationTestFixture
    {
        private static void SetPrivateBoolForTest(object target, string fieldName, bool value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected {target.GetType().Name}.{fieldName} to exist for test setup.");
            Assert.AreEqual(typeof(bool), field.FieldType,
                $"Expected {target.GetType().Name}.{fieldName} to be a bool field.");
            field.SetValue(target, value);
        }

        private static void AssertAuthHeadersNotSet(UnityWebRequest request)
        {
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("Authorization")),
                "AuthHeaderSigner should not set Authorization when tokens are unavailable.");
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("x-abxrlib-timestamp")),
                "AuthHeaderSigner should not set x-abxrlib-timestamp when tokens are unavailable.");
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("x-abxrlib-hash")),
                "AuthHeaderSigner should not set x-abxrlib-hash when tokens are unavailable.");
        }
    }
}

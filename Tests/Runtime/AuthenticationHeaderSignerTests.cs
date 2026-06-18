using System.Collections;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Runtime coverage for auth header signing edge cases.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator AuthHeaderSigner_MissingTokenOrResponseData_DoesNotSetHeaders()
        {
            LogAssert.Expect(LogType.Error, new Regex(
                @"\[AbxrLib\] Cannot set auth headers - authentication tokens are missing"));

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-missing-token"))
            {
                bool signed = AuthHeaderSigner.TrySetAuthHeaders(request,
                    new AuthResponse { Secret = "secret-without-token" }, "{\"event\":\"test\"}");
                Assert.IsFalse(signed);
                AssertAuthHeadersNotSet(request);
            }

            LogAssert.Expect(LogType.Error, new Regex(
                @"\[AbxrLib\] Cannot set auth headers - authentication tokens are missing"));

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-null-response"))
            {
                bool signed = AuthHeaderSigner.TrySetAuthHeaders(request, null);
                Assert.IsFalse(signed);
                AssertAuthHeadersNotSet(request);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthHeaderSigner_WithJson_IncludesJsonCrcInHash()
        {
            const string token = "header-token";
            const string secret = "header-secret";
            const string json = "{\"event\":\"test\",\"value\":42}";

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-with-json"))
            {
                bool signed = AuthHeaderSigner.TrySetAuthHeaders(request, new AuthResponse
                {
                    Token = token,
                    Secret = secret
                }, json);
                Assert.IsTrue(signed);

                string timestamp = request.GetRequestHeader("x-abxrlib-timestamp");
                string actualHash = request.GetRequestHeader("x-abxrlib-hash");
                uint jsonCrc = Utils.ComputeCRC(json);
                string expectedHash = Utils.ComputeSha256Hash(token + secret + timestamp + jsonCrc);
                string hashWithoutJsonCrc = Utils.ComputeSha256Hash(token + secret + timestamp);

                Assert.AreEqual("Bearer " + token, request.GetRequestHeader("Authorization"));
                Assert.IsFalse(string.IsNullOrEmpty(timestamp), "AuthHeaderSigner should set a timestamp before computing the hash.");
                Assert.AreEqual(expectedHash, actualHash,
                    "AuthHeaderSigner should append the CRC of the supplied JSON to the hash string.");
                Assert.AreNotEqual(hashWithoutJsonCrc, actualHash,
                    "Passing JSON should produce a different hash than token + secret + timestamp alone.");
            }

            yield return null;
        }
    }
}

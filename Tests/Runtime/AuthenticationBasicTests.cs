using System.Collections;
using System.Linq;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Basic successful authentication behavior and request/header shape checks.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator Auth_Succeeds_With_Default_Backend_Response()
        {
            // No scenario queued — the server's default 201 response with a valid AuthResponse applies.
            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, $"expected auth success, got error: {LastAuthError}");
            Assert.IsNull(LastAuthError);

            // Verify the client actually called the endpoints we expect.
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count, "expected exactly one /v1/auth/token call");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count, "expected exactly one /v1/storage/config call");
        }

        [UnityTest]
        public IEnumerator Auth_TrySetRestUrl_AfterAuthenticationStarted_ReturnsFalse_AndKeepsCurrentUrl()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            string originalRestUrl = Configuration.Instance.restUrl;
            bool changed = Abxr.TrySetRestUrl("https://example.invalid/", out string errorMessage);

            Assert.IsFalse(changed, "restUrl should be locked after authentication has started.");
            Assert.That(errorMessage, Does.Contain("restUrl cannot be changed after authentication has started"));
            Assert.AreEqual(originalRestUrl, Configuration.Instance.restUrl,
                "failed TrySetRestUrl calls should not mutate the active runtime configuration.");
        }

        [UnityTest]
        public IEnumerator Auth_SetDeviceId_BeforeAuthentication_UsesOverrideInAuthPayload()
        {
            Abxr.SetDeviceId("runtime-device-override");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("runtime-device-override", (string)req.BodyJson["deviceId"],
                "SetDeviceId should update runtime auth before the first authentication request is sent.");
        }

        [UnityTest]
        public IEnumerator Auth_DeviceAuth_Does_Not_Send_AuthMechanism()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.IsNull(req.BodyJson["authMechanism"], "device auth should not send authMechanism; user auth sends it only after config/input requires it");
        }

        [UnityTest]
        public IEnumerator Auth_Request_Omits_ClientOnly_BuildType()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.IsNull(req.BodyJson["buildType"],
                "buildType is a Unity/client credential-selection value; the current backend auth schema does not consume it.");
        }

        [UnityTest]
        public IEnumerator Config_Request_Includes_AuthHeaders_After_DeviceAuth()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess);

            var req = FakeBackend.GetRequests("/v1/storage/config").Single();
            Assert.That(GetHeader(req, "Authorization"), Does.StartWith("Bearer "));
            Assert.IsNotEmpty(GetHeader(req, "x-abxrlib-timestamp"));
            Assert.IsNotEmpty(GetHeader(req, "x-abxrlib-hash"));
        }
    }
}

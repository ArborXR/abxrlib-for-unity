using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture]
    public class AuthHandoffTests : AbxrIntegrationTestFixture
    {
        [UnityTest]
        public IEnumerator AuthHandoff_RawJson_SkipsDeviceAuth_ButStillGetsConfigWithHandoffHeaders()
        {
            AbxrTestHooks.SetAuthHandoffPayloadForTest(HandoffJson(
                userData: new Dictionary<string, object>
                {
                    { "id", "learner-7" },
                    { "email", "handoff@example.com" },
                }));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(
                0,
                FakeBackend.GetRequests("/v1/auth/token").Count,
                "handoff receiver should adopt the provided session instead of POSTing device auth");

            var configReq = FakeBackend.GetRequests("/v1/storage/config").Single();

            Assert.AreEqual("Bearer " + ValidJwtWithExpiration, GetHeader(configReq, "Authorization"));
            Assert.IsNotEmpty(GetHeader(configReq, "x-abxrlib-timestamp"));
            Assert.IsNotEmpty(GetHeader(configReq, "x-abxrlib-hash"));

            var response = Abxr.GetAuthResponse();

            Assert.IsNotNull(response);
            Assert.AreEqual("handoff-user-id", response.UserId?.ToString());
            Assert.AreEqual("learner-7", Abxr.GetUserData()["id"]);
            Assert.AreEqual("handoff@example.com", Abxr.GetUserData()["email"]);
        }

        [UnityTest]
        public IEnumerator AuthHandoff_ConfigRequiresPin_DoesNotPrompt_AndDoesNotPostUserAuth()
        {
            QueueAssessmentPinConfig();

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            AbxrTestHooks.SetAuthHandoffPayloadForTest(HandoffJson());

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.IsFalse(
                inputRequested,
                "handoff sessions already have launcher-provided identity, so config authMechanism should not trigger a second PIN/email prompt");

            Assert.AreEqual(
                0,
                FakeBackend.GetRequests("/v1/auth/token").Count,
                "handoff should skip both device auth and follow-up user-auth POSTs");

            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator AuthHandoff_Base64Payload_AppliesSession_SortsModules_AndStoresReturnPackageOnce()
        {
            object modules = new object[]
            {
                new Dictionary<string, object>
                {
                    { "id", "module-2" },
                    { "name", "Second" },
                    { "target", "scene-2" },
                    { "order", 2 },
                },
                new Dictionary<string, object>
                {
                    { "id", "module-1" },
                    { "name", "First" },
                    { "target", "scene-1" },
                    { "order", 1 },
                },
            };

            AbxrTestHooks.SetAuthHandoffPayloadForTest(Base64Utf8(HandoffJson(
                modules: modules,
                returnToPackage: "com.example.launcher")));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count);

            var response = Abxr.GetAuthResponse();

            Assert.IsNotNull(response?.Modules);
            Assert.AreEqual(2, response.Modules.Count);
            Assert.AreEqual("module-1", response.Modules[0].Id);
            Assert.AreEqual("module-2", response.Modules[1].Id);

            var authService = AbxrTestHooks.GetAuthServiceForTest();

            Assert.IsNotNull(authService);
            Assert.AreEqual("com.example.launcher", authService.GetAndClearReturnToPackage());

            Assert.IsNull(
                authService.GetAndClearReturnToPackage(),
                "ReturnToPackage should be consumed once so return-to-launcher cannot loop forever");
        }

        [UnityTest]
        public IEnumerator AuthHandoff_InvalidPayload_FallsBackToNormalDeviceAuth()
        {
            AbxrTestHooks.SetAuthHandoffPayloadForTest("not-json-and-not-base64");

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[AbxrLib\] Authentication response handling failed"));

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@"\[AbxrLib\] auth_handoff was present but the session could not be applied"));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(
                1,
                FakeBackend.GetRequests("/v1/auth/token").Count,
                "invalid handoff should fall back to normal device auth, not leave the app unauthenticated");

            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator AuthHandoff_MissingSecret_FallsBackToNormalDeviceAuth()
        {
            AbxrTestHooks.SetAuthHandoffPayloadForTest(HandoffJson(secret: null));

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@"\[AbxrLib\] auth_handoff was present but the session could not be applied"));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            Assert.AreEqual(
                1,
                FakeBackend.GetRequests("/v1/auth/token").Count,
                "handoff must include token and secret, otherwise signed follow-up requests would be impossible");
        }

        [UnityTest]
        public IEnumerator AuthHandoff_BuiltPayload_IncludesCurrentSessionCredentialsAndReturnPackage()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(
                    userId: "anon-42",
                    userData: new Dictionary<string, object>
                    {
                        { "id", "learner-42" },
                        { "email", "learner@example.com" },
                    }));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            string json = AbxrTestHooks.GetHandoffJsonForTest(includeReturnToPackage: true);

            Assert.IsNotNull(json);

            var handoff = JObject.Parse(json);

            Assert.AreEqual(ValidJwtWithExpiration, (string)handoff["Token"]);
            Assert.AreEqual("test-secret", (string)handoff["Secret"]);
            Assert.AreEqual(FakeAppId, (string)handoff["AppId"]);
            Assert.AreEqual("anon-42", (string)handoff["UserId"]);

            Assert.AreEqual("learner-42", (string)handoff["UserData"]?["id"]);
            Assert.AreEqual("learner@example.com", (string)handoff["UserData"]?["email"]);

            Assert.AreEqual(FakeAppToken, (string)handoff["AppToken"]);
            Assert.AreEqual(FakeOrgToken, (string)handoff["OrgToken"]);

            Assert.AreEqual(
                4102444800000L,
                (long)handoff["TokenExpirationMs"],
                "handoff expiration should come from the auth JWT exp claim");

            Assert.IsTrue(handoff.ContainsKey("ReturnToPackage"));
        }
    }
}

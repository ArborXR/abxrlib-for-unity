using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Auth response validation and response-shape variations.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator Auth_Fails_When_Response_Has_AppId_But_No_Token()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: new Dictionary<string, object> { { "appId", FakeAppId } });

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();

            Assert.IsFalse(LastAuthSuccess);
            Assert.AreEqual("Authentication request returned an invalid response.", LastAuthError);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "REST auth success requires token+secret, so config should not be fetched for appId-only responses");
        }

        [UnityTest]
        public IEnumerator Auth_Fails_When_Response_Has_Token_But_No_Secret()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(secret: null));

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();

            Assert.IsFalse(LastAuthSuccess);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "REST auth success requires the API secret so signed follow-up requests can be made");
        }

        [UnityTest]
        public IEnumerator Auth_Fails_When_Response_Has_Modules_But_No_Token()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: new Dictionary<string, object>
                {
                    {
                        "modules",
                        new object[]
                        {
                            new Dictionary<string, object>
                            {
                                { "id", "module-1" },
                                { "name", "Module 1" },
                                { "target", "scene-1" },
                                { "order", 0 }
                            }
                        }
                    }
                });

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();

            Assert.IsFalse(LastAuthSuccess);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "modules are LMS data attached to a successful token response, not a standalone REST auth success");
        }

        [UnityTest]
        public IEnumerator Auth_Response_WithTokenAndModules_Succeeds_And_SortsModulesByOrder()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(
                    modules: new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "id", "module-2" },
                            { "name", "Second" },
                            { "target", "scene-2" },
                            { "order", 2 }
                        },
                        new Dictionary<string, object>
                        {
                            { "id", "module-1" },
                            { "name", "First" },
                            { "target", "scene-1" },
                            { "order", 1 }
                        },
                    }));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            var modules = Abxr.GetAuthResponse().Modules;
            Assert.AreEqual(2, modules.Count);
            Assert.AreEqual("module-1", modules[0].Id);
            Assert.AreEqual("module-2", modules[1].Id);
        }
    }
}

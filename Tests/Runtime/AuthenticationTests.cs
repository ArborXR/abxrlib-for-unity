using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Integration tests for the auth flow. Each test scripts the fake backend, calls
    /// <c>Abxr.StartAuthentication()</c>, waits for <c>OnAuthCompleted</c>, and asserts.
    ///
    /// Pattern:
    /// <code>
    /// [UnityTest]
    /// public IEnumerator Some_Scenario()
    /// {
    ///     FakeBackend.QueueScenario("/v1/auth/token", status: ..., body: ...);
    ///     yield return RunAuthAndWait();
    ///     Assert.IsTrue(LastAuthSuccess);
    ///     // optionally inspect FakeBackend.GetRequests(...)
    /// }
    /// </code>
    ///
    /// Scenario queue semantics: queued scenarios for a given (method, path) are consumed in order,
    /// and the last one is sticky once the queue empties. So <c>QueueScenario(500); QueueScenario(200)</c>
    /// means "fail the first call, succeed every call after that".
    /// </summary>
    [TestFixture]
    public class AuthenticationTests : AbxrIntegrationTestFixture
    {
        private const string ValidJwtWithExpiration = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJleHAiOjQxMDI0NDQ4MDB9.c2ln";
        private const string FakeAppId = "00000000-0000-0000-0000-000000000001";

        private static Dictionary<string, object> AuthBody(
            string token = ValidJwtWithExpiration,
            string secret = "test-secret",
            object userData = null,
            string userId = "test-user-id",
            object modules = null,
            string appId = FakeAppId,
            string packageName = "com.example.testapp")
        {
            var body = new Dictionary<string, object>();
            if (token != null) body["token"] = token;
            if (secret != null) body["secret"] = secret;
            if (userId != null) body["userId"] = userId;
            if (userData != null) body["userData"] = userData;
            if (appId != null) body["appId"] = appId;
            if (packageName != null) body["packageName"] = packageName;
            body["modules"] = modules ?? new object[0];
            return body;
        }

        private static string GetHeader(RecordedRequest request, string name)
        {
            if (request?.Headers == null) return null;
            foreach (var kvp in request.Headers)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        // ── Happy path ──────────────────────────────────────────────

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
        public IEnumerator Auth_Request_Includes_AppToken_In_Body()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.IsNotNull(req.BodyJson, "auth body should be JSON");
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"], "appToken should be sent in the body");
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"], "orgToken should be sent in the body");
            // Legacy fields should be omitted when useAppTokens=true (NullValueHandling.Ignore on the serializer).
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        // ── Rejection paths ────────────────────────────────────────
        
        [UnityTest]
        public IEnumerator Auth_Fails_On_401_With_Server_Error_Message()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 401,
                body: new Dictionary<string, object> { { "detail", "Invalid app token" } });

            // AbxrLib logs an Error when auth is rejected;
            // declare it expected so the test framework doesn't treat it as an unhandled error.
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("Invalid app token"));
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
        }

        [UnityTest]
        public IEnumerator Auth_Fails_On_500_With_Explicit_Error_No_Retry()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 500,
                body: new Dictionary<string, object> { { "detail", "server exploded" } });

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("server exploded"));
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
        }

        // ── Retry path ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Auth_Rejection_Latches_Within_Session()
        {
            // First call: 401 → terminal rejection → _credentialsRejectedByApi latches
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 401,
                body: new Dictionary<string, object> { { "detail", "Invalid app token" } });
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait();
            Assert.IsFalse(LastAuthSuccess);
            int requestsAfterFirst = FakeBackend.GetRequests("/v1/auth/token").Count;
            Assert.AreEqual(1, requestsAfterFirst);

            // Second call should not hit the backend at all — AuthService.Authenticate() early-exits
            // because _credentialsRejectedByApi is set, firing OnFailed with the rejection message.
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait(timeoutSeconds: 3f);
            Assert.IsFalse(LastAuthSuccess);
            Assert.AreEqual(
                requestsAfterFirst,
                FakeBackend.GetRequests("/v1/auth/token").Count,
                "After rejection latches, subsequent StartAuthentication() calls should not send new requests.");
        }


        [UnityTest]
        public IEnumerator Auth_ConfigRequiresPin_RequestsInput_ThenSucceeds()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    {
                        "authMechanism",
                        new Dictionary<string, object>
                        {
                            { "type", "assessmentPin" },
                            { "prompt", "Enter your test PIN" },
                            { "inputSource", "user" },
                        }
                    },
                });

            bool inputRequested = false;
            string requestedType = null;
            string requestedPrompt = null;

            Abxr.OnInputRequested = (type, prompt, domain, error) =>
            {
                inputRequested = true;
                requestedType = type;
                requestedPrompt = prompt;
                Abxr.OnInputSubmitted("123456");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(inputRequested, "Expected the SDK to request PIN input from the app.");
            Assert.AreEqual("assessmentPin", requestedType);
            Assert.AreEqual("Enter your test PIN", requestedPrompt);
            Assert.IsTrue(LastAuthSuccess, $"expected auth success after submitting PIN, got error: {LastAuthError}");
            Assert.AreEqual(2, FakeBackend.GetRequests("/v1/auth/token").Count,
                "Expected one device-auth request and one user-auth request after PIN submission.");
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
        public IEnumerator Auth_ProductionCustom_Sends_Production_BuildType()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("production", (string)req.BodyJson["buildType"],
                "production_custom is a Unity/client configuration value; the REST API receives production.");
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

        [UnityTest]
        public IEnumerator Auth_Request_Includes_LegacyCredentials_When_UseAppTokensFalse()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production_custom";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "legacy-secret";
            c.appToken = null;
            c.orgToken = null;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000022", (string)req.BodyJson["orgId"]);
            Assert.AreEqual("legacy-secret", (string)req.BodyJson["authSecret"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
        }

        [UnityTest]
        public IEnumerator Auth_Fails_BeforeNetwork_When_OrganizationIdentificationUnavailable()
        {
            Configuration.Instance.orgToken = null;

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait(timeoutSeconds: 3f);

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("Organization identification unavailable"));
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "credential validation should fail before a REST auth request is sent");
        }

        [UnityTest]
        public IEnumerator Auth_PinSubmission_Sends_AssessmentPin_And_ReusesSessionId()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    {
                        "authMechanism",
                        new Dictionary<string, object>
                        {
                            { "type", "assessmentPin" },
                            { "prompt", "Enter your test PIN" },
                            { "inputSource", "user" },
                        }
                    },
                });

            Abxr.OnInputRequested = (type, prompt, domain, error) => Abxr.OnInputSubmitted("123456");

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected device auth followed by user auth");
            Assert.IsNull(requests[0].BodyJson["authMechanism"], "device auth must not send authMechanism");
            Assert.AreEqual((string)requests[0].BodyJson["sessionId"], (string)requests[1].BodyJson["sessionId"],
                "user auth should update the same backend session");

            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism["type"]);
            Assert.AreEqual("123456", (string)mechanism["prompt"]);
            Assert.AreEqual("user", (string)mechanism["inputSource"]);
        }

        [UnityTest]
        public IEnumerator Auth_EmailInput_AppendsDomain_And_SendsEmailAuthMechanism()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    {
                        "authMechanism",
                        new Dictionary<string, object>
                        {
                            { "type", "email" },
                            { "prompt", "Enter school email" },
                            { "domain", "school.edu" },
                            { "inputSource", "user" },
                        }
                    },
                });

            string requestedDomain = null;
            Abxr.OnInputRequested = (type, prompt, domain, error) =>
            {
                requestedDomain = domain;
                Abxr.OnInputSubmitted("learner");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.AreEqual("school.edu", requestedDomain);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count);
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("email", (string)mechanism["type"]);
            Assert.AreEqual("learner@school.edu", (string)mechanism["prompt"]);
            Assert.AreEqual("user", (string)mechanism["inputSource"]);
            Assert.IsNull(mechanism["domain"], "domain is client-side prompt/config data and should not be sent in authMechanism");

            var userData = Abxr.GetUserData();
            Assert.IsNotNull(userData);
            Assert.AreEqual("learner@school.edu", userData["email"]);
        }

        // ── Response shape variations ──────────────────────────────

        [UnityTest]
        public IEnumerator Config_Endpoint_Override_Merges_Into_Runtime()
        {
            // /v1/storage/config can override a subset of runtime config (see Configuration.ApplyConfigPayload).
            // Values arrive as strings in the portal payload; the runtime parses them.
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    { "sendNextBatchWait", "47" },
                    { "maximumCachedItems", "999" },
                });

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess);
            Assert.AreEqual(47, Configuration.Instance.sendNextBatchWaitSeconds,
                "sendNextBatchWait from /v1/storage/config should have been applied");
            Assert.AreEqual(999, Configuration.Instance.maximumCachedItems,
                "maximumCachedItems from /v1/storage/config should have been applied");
        }

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
                                { "order", 0 },
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
        public IEnumerator Auth_ConfigFailure_Continues_AsAnonymous_WithoutInput()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 500,
                body: new Dictionary<string, object> { { "detail", "config unavailable" } });

            bool inputRequested = false;
            Abxr.OnInputRequested = (type, prompt, domain, error) => inputRequested = true;

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested, "config failures should keep the device-authenticated anonymous session rather than prompting for a synthetic authMechanism");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator Auth_ConfigAuthMechanismNone_DoesNotRequestInput_AndSucceeds()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    {
                        "authMechanism",
                        new Dictionary<string, object>
                        {
                            { "type", "none" },
                            { "prompt", "ignored" },
                            { "inputSource", "user" },
                        }
                    },
                });

            bool inputRequested = false;
            Abxr.OnInputRequested = (type, prompt, domain, error) => inputRequested = true;

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested, "authMechanism.type=none should mean no user-auth prompt");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
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
                            { "order", 2 },
                        },
                        new Dictionary<string, object>
                        {
                            { "id", "module-1" },
                            { "name", "First" },
                            { "target", "scene-1" },
                            { "order", 1 },
                        },
                    }));

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            var modules = Abxr.GetAuthResponse().Modules;
            Assert.AreEqual(2, modules.Count);
            Assert.AreEqual("module-1", modules[0].Id);
            Assert.AreEqual("module-2", modules[1].Id);
        }

        [UnityTest]
        public IEnumerator SetUserData_AfterAuth_Sends_CustomAuthMechanism_And_DoesNotFireAuthCompleted()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            int authCompletedCount = 0;

            Action<bool, string> authCompletedHandler = (success, error) => authCompletedCount++;
            Abxr.OnAuthCompleted += authCompletedHandler;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            Abxr.SetUserData("learner-42", new Dictionary<string, string>
            {
                { "email", "learner@example.com" },
                { "cohort", "alpha" },
                { "type", "should-not-leak" },
                { "prompt", "should-not-leak" },
                { "inputSource", "should-not-leak" },
            });

            yield return WaitUntil(() => syncDone, 5f, "SetUserData re-auth completed");

            Abxr.OnAuthCompleted -= authCompletedHandler;
            Abxr.OnUserDataSyncCompleted = null;

            Assert.IsTrue(syncSuccess, syncError);
            Assert.AreEqual(0, authCompletedCount, "SetUserData re-auth should report through OnUserDataSyncCompleted, not OnAuthCompleted.");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected initial auth plus SetUserData custom re-auth");
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)mechanism["type"]);
            Assert.AreEqual("user", (string)mechanism["inputSource"]);
            Assert.AreEqual("learner-42", (string)mechanism["id"]);
            Assert.AreEqual("learner@example.com", (string)mechanism["email"]);
            Assert.AreEqual("alpha", (string)mechanism["cohort"]);
            Assert.IsNull(mechanism["prompt"], "reserved user-data field prompt should not be forwarded as custom auth data");
        }

    }
}

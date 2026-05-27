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
    }
}

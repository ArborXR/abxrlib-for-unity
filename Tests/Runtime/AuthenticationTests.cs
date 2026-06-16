using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
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
        public IEnumerator Auth_AppTokens_ProductionCustom_UsesConfiguredOrgToken_AndOmitsLegacyFields()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            // Populate legacy fields too, so the test proves app-token mode does not leak them.
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "legacy-secret-should-not-leak";

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.IsNotNull(req.BodyJson, "auth body should be JSON");
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"], "appToken should be sent in the body");
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"], "orgToken should be sent in the body");
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        // ── Build type credential-source behavior ──────────────────

        [UnityTest]
        public IEnumerator Auth_AppTokens_Production_IgnoresConfiguredOrgToken_AndUsesRuntimeOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            // Production/shared builds must not send the build-time org token from config.
            // Runtime org identification comes from MDM, query/intent, or these explicit overrides.
            Abxr.SetOrgId("00000000-0000-0000-0000-000000000033");
            Abxr.SetAuthSecret("runtime-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);

            var orgToken = (string)req.BodyJson["orgToken"];
            Assert.IsFalse(string.IsNullOrEmpty(orgToken), "production auth still needs runtime org identification before sending");
            Assert.AreNotEqual(FakeOrgToken, orgToken,
                "production app-token auth should ignore the configured orgToken and use runtime-provided org credentials instead");
            Assert.AreEqual(3, orgToken.Split('.').Length, "runtime orgToken should be JWT-shaped");

            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_AppTokens_Development_UsesConfiguredOrgToken()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "development";
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            Assert.AreEqual(FakeOrgToken, (string)req.BodyJson["orgToken"],
                "development builds may use the configured orgToken for local/custom testing");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_Legacy_Production_IgnoresConfiguredOrgCredentials_AndUsesRuntimeOrgCredentials()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "configured-secret-should-not-be-sent";

            // Production/shared builds do not trust orgID/authSecret from the build-time config.
            Abxr.SetOrgId("00000000-0000-0000-0000-000000000033");
            Abxr.SetAuthSecret("runtime-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000033", (string)req.BodyJson["orgId"],
                "production legacy auth should use runtime-provided orgId, not the configured orgID");
            Assert.AreEqual("runtime-fingerprint-secret", (string)req.BodyJson["authSecret"],
                "production legacy auth should use runtime-provided authSecret/fingerprint, not the configured authSecret");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
        }

        [UnityTest]
        public IEnumerator Auth_Legacy_Development_UsesConfiguredOrgCredentials()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "development";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "configured-development-secret";

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual("00000000-0000-0000-0000-000000000011", (string)req.BodyJson["appId"]);
            Assert.AreEqual("00000000-0000-0000-0000-000000000022", (string)req.BodyJson["orgId"],
                "development legacy auth should use the configured orgID");
            Assert.AreEqual("configured-development-secret", (string)req.BodyJson["authSecret"],
                "development legacy auth should use the configured authSecret");
            Assert.IsNull(req.BodyJson["buildType"]);
            Assert.IsNull(req.BodyJson["appToken"]);
            Assert.IsNull(req.BodyJson["orgToken"]);
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
            Configuration.Instance.sendRetriesOnFailure = 2;

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
        public IEnumerator Auth_Retries_Transient_500_EmptyBody_Then_Succeeds()
        {
            Configuration.Instance.sendRetriesOnFailure = 1;
            Configuration.Instance.sendRetryIntervalSeconds = 1;

            FakeBackend.QueueEmptyBodyScenario(
                path: "/v1/auth/token",
                status: 500);
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody());

            yield return RunAuthAndWait(timeoutSeconds: 6f);

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.AreEqual(2, FakeBackend.GetRequests("/v1/auth/token").Count,
                "transient server failures should retry when sendRetriesOnFailure allows it");
        }

        [UnityTest]
        public IEnumerator Auth_RetryableDeviceAuthFailure_StopsAfterMaxRetries()
        {
            Configuration.Instance.sendRetriesOnFailure = 2;
            Configuration.Instance.sendRetryIntervalSeconds = 1;

            FakeBackend.QueueEmptyBodyScenario(path: "/v1/auth/token", status: 500);

            LogAssert.Expect(LogType.Error, new Regex(
                @"\[AbxrLib\] Authentication failure: Authentication request failed \(HTTP 500\)\."));

            yield return RunAuthAndWait(timeoutSeconds: 8f);

            Assert.IsFalse(LastAuthSuccess);
            Assert.AreEqual("Authentication request failed (HTTP 500).", LastAuthError);
            Assert.AreEqual(3, FakeBackend.GetRequests("/v1/auth/token").Count,
                "sendRetriesOnFailure counts retries after the initial device-auth attempt, so 2 retries means 3 total attempts.");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "the auth flow should not fetch config after device auth exhausts its retry budget.");

            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");
            Assert.IsFalse(service.Authenticated,
                "auth should remain unauthenticated after exhausting retryable device-auth failures.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "the failed retry sequence should clear the active auth attempt flag.");
        }

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
        public IEnumerator Auth_StartAuthentication_WhenServiceStopping_IsNoOp()
        {
            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");

            int authCompletedCount = 0;
            Action<bool, string> authCompletedHandler = (_, _) => authCompletedCount++;
            Abxr.OnAuthCompleted += authCompletedHandler;
            try
            {
                service.Shutdown();
                Abxr.StartAuthentication();

                yield return null;
                yield return null;
            }
            finally
            {
                Abxr.OnAuthCompleted -= authCompletedHandler;
            }

            Assert.AreEqual(0, authCompletedCount,
                "StartAuthentication should be ignored after the auth service has begun stopping.");
            Assert.IsFalse(service.Authenticated,
                "a no-op StartAuthentication call should not mark the service authenticated.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "a no-op StartAuthentication call should not start a new auth attempt.");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "stopping auth should not send a device-auth request.");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "stopping auth should not fetch config.");
        }

        [UnityTest]
        public IEnumerator Auth_AttemptInactiveAfterConfig_FailsBeforeUserAuthPrompt()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                delayMs: 1000,
                body: new Dictionary<string, object>
                {
                    {
                        "authMechanism",
                        new Dictionary<string, object>
                        {
                            { "type", "assessmentPin" },
                            { "prompt", "Enter delayed PIN" },
                            { "inputSource", "user" }
                        }
                    }
                });

            bool inputRequested = false;
            int authCompletedCount = 0;
            bool authSuccess = true;
            string authError = null;
            Action<bool, string> authCompletedHandler = (success, error) =>
            {
                authCompletedCount++;
                authSuccess = success;
                authError = error;
            };

            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;
            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure: Auth stopped or attempt inactive"));

            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");

            Abxr.OnAuthCompleted += authCompletedHandler;
            try
            {
                Abxr.StartAuthentication();

                yield return WaitUntil(
                    () => FakeBackend.GetRequests("/v1/storage/config").Count >= 1,
                    timeoutSeconds: 3f,
                    description: "delayed config request reached the backend");

                SetPrivateBoolForTest(service, "_attemptActive", false);

                yield return WaitUntil(
                    () => authCompletedCount > 0,
                    timeoutSeconds: 5f,
                    description: "auth completed after attempt was marked inactive");
            }
            finally
            {
                Abxr.OnAuthCompleted -= authCompletedHandler;
            }

            Assert.AreEqual(1, authCompletedCount,
                "marking an in-flight auth attempt inactive should fail the current attempt exactly once.");
            Assert.IsFalse(authSuccess);
            Assert.AreEqual("Auth stopped or attempt inactive", authError);
            Assert.IsFalse(inputRequested,
                "the inactive-at-config guard should run before user input is requested.");
            Assert.IsFalse(service.Authenticated,
                "an inactive attempt should not complete authentication.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "the inactive-at-config guard should leave the service with no active auth attempt.");

            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count,
                "only the initial device-auth request should be sent before the inactive guard fires.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count,
                "config should have been fetched before the inactive guard aborted the flow.");
        }

        [UnityTest]
        public IEnumerator Auth_SubmitInput_WithoutPendingRequest_IsIgnored()
        {
            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] OnInputSubmitted was ignored: no input request is pending\. Call OnInputSubmitted only once, after OnInputRequested has been invoked\."));

            Assert.IsFalse(Abxr.IsAuthInputRequestPending(),
                "test should start with no auth input request pending");

            Abxr.OnInputSubmitted("orphan-input");
            yield return null;

            Assert.IsFalse(Abxr.IsAuthInputRequestPending(),
                "submitting input without a pending request should remain a no-op");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "orphan input should not start or advance authentication");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count,
                "orphan input should not fetch config");
        }


        [UnityTest]
        public IEnumerator Auth_SetAuthHeaders_MissingTokenOrResponseData_DoesNotSetHeaders()
        {
            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");

            SetAuthResponseForTest(service, new AuthResponse { Secret = "secret-without-token" });
            LogAssert.Expect(LogType.Error, new Regex(
                @"\[AbxrLib\] Cannot set auth headers - authentication tokens are missing"));

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-missing-token"))
            {
                service.SetAuthHeaders(request, "{\"event\":\"test\"}");
                AssertAuthHeadersNotSet(request);
            }

            SetAuthResponseForTest(service, null);
            LogAssert.Expect(LogType.Error, new Regex(
                @"\[AbxrLib\] Cannot set auth headers - authentication tokens are missing"));

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-null-response"))
            {
                service.SetAuthHeaders(request);
                AssertAuthHeadersNotSet(request);
            }

            yield return null;
        }


        [UnityTest]
        public IEnumerator Auth_SetAuthHeaders_WithJson_IncludesJsonCrcInHash()
        {
            const string token = "header-token";
            const string secret = "header-secret";
            const string json = "{\"event\":\"test\",\"value\":42}";

            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");

            SetAuthResponseForTest(service, new AuthResponse
            {
                Token = token,
                Secret = secret
            });

            using (var request = UnityWebRequest.Get(FakeBackend.BaseUrl + "/headers-with-json"))
            {
                service.SetAuthHeaders(request, json);

                string timestamp = request.GetRequestHeader("x-abxrlib-timestamp");
                string actualHash = request.GetRequestHeader("x-abxrlib-hash");
                uint jsonCrc = Utils.ComputeCRC(json);
                string expectedHash = Utils.ComputeSha256Hash(token + secret + timestamp + jsonCrc);
                string hashWithoutJsonCrc = Utils.ComputeSha256Hash(token + secret + timestamp);

                Assert.AreEqual("Bearer " + token, request.GetRequestHeader("Authorization"));
                Assert.IsFalse(string.IsNullOrEmpty(timestamp), "SetAuthHeaders should set a timestamp before computing the hash.");
                Assert.AreEqual(expectedHash, actualHash,
                    "SetAuthHeaders should append the CRC of the supplied JSON to the hash string.");
                Assert.AreNotEqual(hashWithoutJsonCrc, actualHash,
                    "Passing JSON should produce a different hash than token + secret + timestamp alone.");
            }

            yield return null;
        }


        [UnityTest]
        public IEnumerator Auth_ConfigRequiresPin_RequestsInput_ThenSucceeds()
        {
            QueueAssessmentPinConfig();

            bool inputRequested = false;
            string requestedType = null;
            string requestedPrompt = null;

            Abxr.OnInputRequested = (type, prompt, _, _) =>
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

        [UnityTest]
        public IEnumerator Auth_Legacy_ProductionCustom_UsesConfiguredOrgCredentials_AndOmitsAppTokens()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production_custom";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = "00000000-0000-0000-0000-000000000022";
            c.authSecret = "legacy-secret";

            // Populate app-token fields too, so the test proves legacy mode does not leak them.
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

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
        public IEnumerator Auth_LegacyMode_MissingOrgCredentials_FailsBeforeNetwork()
        {
            var c = Configuration.Instance;
            c.useAppTokens = false;
            c.buildType = "production_custom";
            c.appID = "00000000-0000-0000-0000-000000000011";
            c.orgID = null;
            c.authSecret = null;
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure"));

            yield return RunAuthAndWait(timeoutSeconds: 3f);

            Assert.IsFalse(LastAuthSuccess);
            Assert.That(LastAuthError, Does.Contain("Organization identification unavailable"));
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "invalid legacy credentials should fail before a REST auth request is sent");
        }

        [UnityTest]
        public IEnumerator Auth_AppTokenMode_BuildsDynamicOrgToken_FromOverrides()
        {
            var c = Configuration.Instance;
            c.useAppTokens = true;
            c.buildType = "production";
            c.appToken = FakeAppToken;
            c.orgToken = null;

            Abxr.SetOrgId("00000000-0000-0000-0000-000000000022");
            Abxr.SetAuthSecret("device-fingerprint-secret");

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var req = FakeBackend.GetRequests("/v1/auth/token").Single();
            Assert.AreEqual(FakeAppToken, (string)req.BodyJson["appToken"]);
            var dynamicOrgToken = (string)req.BodyJson["orgToken"];
            Assert.IsFalse(string.IsNullOrEmpty(dynamicOrgToken),
                "dynamic orgToken should be generated from orgId + authSecret overrides");
            Assert.AreEqual(3, dynamicOrgToken.Split('.').Length,
                "dynamic orgToken should be a JWT-shaped compact token");

            Assert.IsNull(req.BodyJson["appId"]);
            Assert.IsNull(req.BodyJson["orgId"]);
            Assert.IsNull(req.BodyJson["authSecret"]);
        }

        [UnityTest]
        public IEnumerator Auth_PinSubmission_Sends_AssessmentPin_And_ReusesSessionId()
        {
            QueueAssessmentPinConfig();

            Abxr.OnInputRequested = (_, _, _, _) => Abxr.OnInputSubmitted("123456");

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected device auth followed by user auth");
            Assert.IsNull(requests[0].BodyJson["authMechanism"], "device auth must not send authMechanism");
            Assert.AreEqual((string)requests[0].BodyJson["sessionId"], (string)requests[1].BodyJson["sessionId"],
                "user auth should update the same backend session");

            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
            Assert.AreEqual("123456", (string)mechanism?["prompt"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
        }

        [UnityTest]
        public IEnumerator Auth_PinSubmission_DoesNotMutateConfiguredPrompt_WhileUserAuthIsInFlight()
        {
            const string configuredPrompt = "Enter the protected assessment PIN";

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));
            QueueAssessmentPinConfig(configuredPrompt);
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "pinStatus", "accepted" } }),
                delayMs: 3000);

            bool authDone = false;
            bool authSuccess = false;
            string authError = null;
            Action<bool, string> authCompletedHandler = (success, error) =>
            {
                authDone = true;
                authSuccess = success;
                authError = error;
            };

            Abxr.OnInputRequested = (type, prompt, _, _) =>
            {
                Assert.AreEqual("assessmentPin", type);
                Assert.AreEqual(configuredPrompt, prompt);
                Abxr.OnInputSubmitted("654321");
            };

            Abxr.OnAuthCompleted += authCompletedHandler;
            try
            {
                Abxr.StartAuthentication();

                yield return WaitUntil(
                    () => FakeBackend.GetRequests("/v1/auth/token").Count >= 2,
                    3f,
                    "user-auth request reached the backend");

                var service = AbxrTestHooks.GetAuthServiceForTest();
                Assert.IsNotNull(service, "auth service should exist while auth is in flight");
                Assert.AreEqual(
                    configuredPrompt,
                    service.GetAuthMechanismPromptForTest(),
                    "submitted PIN should not replace the configured authMechanism prompt while the request is in flight");

                var requests = FakeBackend.GetRequests("/v1/auth/token");
                var mechanism = requests[1].BodyJson["authMechanism"];
                Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
                Assert.AreEqual("654321", (string)mechanism?["prompt"],
                    "the submitted PIN should still be sent in the request payload");
                Assert.AreEqual("user", (string)mechanism?["inputSource"]);

                yield return WaitUntil(() => authDone, 7f, "auth completed after delayed user-auth response");
            }
            finally
            {
                Abxr.OnAuthCompleted -= authCompletedHandler;
            }

            Assert.IsTrue(authSuccess, authError);
        }

        [UnityTest]
        public IEnumerator Auth_PinSubmission_Fails_ThenRePrompts_AndSucceeds()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));
            const string backendPinError = "Invalid assessment pin or the assessment is already active";

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 401,
                body: new Dictionary<string, object> { { "detail", backendPinError } });
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "pinStatus", "accepted" } }));

            QueueAssessmentPinConfig();

            var requestedTypes = new List<string>();
            var requestedPrompts = new List<string>();
            var requestedErrors = new List<string>();
            var submittedPins = new List<string>();
            var authSuccesses = new List<bool>();
            var authErrors = new List<string>();

            Action<bool, string> authCompletedHandler = (success, error) =>
            {
                authSuccesses.Add(success);
                authErrors.Add(error);
            };

            Abxr.OnInputRequested = (type, prompt, _, error) =>
            {
                requestedTypes.Add(type);
                requestedPrompts.Add(prompt);
                requestedErrors.Add(error);

                if (requestedTypes.Count == 1)
                {
                    submittedPins.Add("000000");
                    Abxr.OnInputSubmitted("000000");
                }
                else if (requestedTypes.Count == 2)
                {
                    submittedPins.Add("123456");
                    Abxr.OnInputSubmitted("123456");
                }
            };

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] Authentication failure: Invalid assessment pin or the assessment is already active"));

            Abxr.OnAuthCompleted += authCompletedHandler;
            Abxr.StartAuthentication();

            float elapsed = 0f;
            while (!authSuccesses.Contains(true) && elapsed < 10f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Abxr.OnAuthCompleted -= authCompletedHandler;

            Assert.Contains(true, authSuccesses, "expected final auth success after retrying with the corrected PIN");
            CollectionAssert.AreEqual(new[] { false, true }, authSuccesses,
                "bad PIN should emit a failed auth event, then the corrected PIN should complete auth successfully");
            Assert.AreEqual(backendPinError, authErrors[0],
                "OnAuthCompleted should keep the detailed backend error for logging/diagnostics.");
            Assert.IsNull(authErrors[1]);

            CollectionAssert.AreEqual(new[] { "assessmentPin", "assessmentPin" }, requestedTypes);
            CollectionAssert.AreEqual(new[] { "Enter your test PIN", "Enter your test PIN" }, requestedPrompts);
            CollectionAssert.AreEqual(new[] { "", "Authentication Failed" }, requestedErrors,
                "the retry prompt should show a short generic message instead of the full backend PIN failure detail");
            CollectionAssert.AreEqual(new[] { "000000", "123456" }, submittedPins);
            Assert.IsFalse(Abxr.IsAuthInputRequestPending(),
                "successful retry should clear the pending-input state");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(3, requests.Count,
                "expected device auth, failed user-auth PIN, then successful user-auth PIN retry");
            Assert.IsNull(requests[0].BodyJson["authMechanism"],
                "device auth must not send authMechanism");
            Assert.AreEqual((string)requests[0].BodyJson["sessionId"], (string)requests[1].BodyJson["sessionId"],
                "failed PIN auth should use the same backend session as device auth");
            Assert.AreEqual((string)requests[0].BodyJson["sessionId"], (string)requests[2].BodyJson["sessionId"],
                "successful PIN retry should continue the same backend session");

            var failedMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)failedMechanism?["type"]);
            Assert.AreEqual("000000", (string)failedMechanism?["prompt"]);
            Assert.AreEqual("user", (string)failedMechanism?["inputSource"]);

            var retryMechanism = requests[2].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)retryMechanism?["type"]);
            Assert.AreEqual("123456", (string)retryMechanism?["prompt"]);
            Assert.AreEqual("user", (string)retryMechanism?["inputSource"]);

            var userData = Abxr.GetUserData();
            Assert.IsNotNull(userData);
            Assert.AreEqual("accepted", userData["pinStatus"],
                "successful retry should replace the device-auth user data with the accepted PIN response data");
        }

        [UnityTest]
        public IEnumerator Auth_SkipKeyboardInput_CompletesAuth_WithoutUserAuthPost()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));

            QueueAssessmentPinConfig();

            bool inputRequested = false;
            string requestedType = null;
            string requestedPrompt = null;
            string requestedError = null;
            int authRequestCountAtPrompt = -1;

            LogAssert.Expect(LogType.Warning, new Regex(@"\[AbxrLib\] Skipping user authentication\."));

            Abxr.OnInputRequested = (type, prompt, _, error) =>
            {
                inputRequested = true;
                requestedType = type;
                requestedPrompt = prompt;
                requestedError = error;
                authRequestCountAtPrompt = FakeBackend.GetRequests("/v1/auth/token").Count;

                Assert.IsTrue(Abxr.IsAuthInputRequestPending(),
                    "input should be marked pending while the app's OnInputRequested handler is running");

                Abxr.OnInputSubmitted("**skip**");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(inputRequested, "Expected the SDK to request keyboard/PIN input before skip was submitted.");
            Assert.AreEqual("assessmentPin", requestedType);
            Assert.AreEqual("Enter your test PIN", requestedPrompt);
            Assert.AreEqual("", requestedError);
            Assert.AreEqual(1, authRequestCountAtPrompt,
                "the prompt should happen after device auth and before any user-auth request");

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsNull(LastAuthError);
            Assert.IsFalse(Abxr.IsAuthInputRequestPending(),
                "submitting **skip** should clear the pending-input state");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(1, requests.Count,
                "**skip** should accept the device-authenticated session and avoid the follow-up user-auth POST.");
            Assert.IsNull(requests[0].BodyJson["authMechanism"],
                "device auth must not send authMechanism; skip should not create a user-auth request.");

            var userData = Abxr.GetUserData();
            Assert.IsNotNull(userData);
            Assert.AreEqual("device", userData["authMode"],
                "skip should preserve the original device-auth response data rather than replacing it with user-auth data.");
        }

        // ── MDM SSO ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Auth_MdmSsoIdentity_SkipsPinPrompt_AndSyncsSsoClaims()
        {
            var ssoToken = JwtWithClaims(new Dictionary<string, object>
            {
                { "sub", "sso-subject-123" },
                { "email", "sso.user@example.com" },
                { "preferred_username", "sso.user@example.com" },
                { "name", "SSO User" }
            });

            AbxrTestHooks.SetSsoForTest(isAuthenticated: true, accessToken: ssoToken);

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "device" },
                    { "email", "backend@example.com" }
                }));
            QueueAssessmentPinConfig("Enter SSO-protected PIN");
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "sso-synced" },
                }));

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "Valid MDM SSO identity should bypass the configured assessmentPin prompt.");
            Assert.IsFalse(Abxr.IsAuthInputRequestPending(),
                "SSO bypass should not leave keyboard input pending.");

            yield return WaitUntil(
                () => syncDone,
                timeoutSeconds: 5f,
                description: "merged SSO user data should be synced with a custom re-auth request");

            Abxr.OnUserDataSyncCompleted = null;
            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count,
                "Expected device auth plus one custom user-data sync. There should be no assessmentPin user-auth POST.");

            Assert.IsNull(requests[0].BodyJson["authMechanism"],
                "Initial device auth must not send authMechanism.");

            var syncMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)syncMechanism?["type"]);
            Assert.AreEqual("user", (string)syncMechanism?["inputSource"]);
            Assert.AreEqual("device", (string)syncMechanism?["authMode"],
                "The SSO sync should send the merged device-auth userData back to the backend.");
            Assert.AreEqual("sso-subject-123", (string)syncMechanism?["sub"]);
            Assert.AreEqual("sso.user@example.com", (string)syncMechanism?["sso_email"],
                "Existing backend email should not be overwritten; conflicting SSO email should be prefixed.");
            Assert.AreEqual("SSO User", (string)syncMechanism?["name"]);
            Assert.IsNull(syncMechanism?["assessmentPin"],
                "SSO bypass should not submit an assessmentPin auth mechanism.");
        }

        [UnityTest]
        public IEnumerator Auth_MdmSsoPreferredUsername_SetsUserDataEmail_WhenBackendEmailMissing()
        {
            const string ssoEmail = "sso.preferred@example.com";
            var ssoToken = JwtWithClaims(new Dictionary<string, object>
            {
                { "sub", "sso-subject-email-fallback" },
                { "preferred_username", ssoEmail }
            });

            AbxrTestHooks.SetSsoForTest(isAuthenticated: true, accessToken: ssoToken);

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "device" }
                }));
            QueueAssessmentPinConfig("Enter SSO-protected PIN");
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "sso-email-synced" }
                }));

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            Dictionary<string, string> userDataAtAuthCompleted = null;
            Action<bool, string> captureUserDataAtAuthCompleted = (success, _) =>
            {
                if (success) userDataAtAuthCompleted = Abxr.GetUserData();
            };

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            Abxr.OnAuthCompleted += captureUserDataAtAuthCompleted;
            try
            {
                yield return RunAuthAndWait();
            }
            finally
            {
                Abxr.OnAuthCompleted -= captureUserDataAtAuthCompleted;
            }

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "Valid MDM SSO identity should bypass the configured assessmentPin prompt.");

            Assert.IsNotNull(userDataAtAuthCompleted,
                "OnAuthCompleted should see the SSO-merged userData before the follow-up sync request starts.");
            Assert.AreEqual("device", userDataAtAuthCompleted["authMode"]);
            Assert.AreEqual("sso-subject-email-fallback", userDataAtAuthCompleted["sub"]);
            Assert.AreEqual(ssoEmail, userDataAtAuthCompleted["preferred_username"]);
            Assert.AreEqual(ssoEmail, userDataAtAuthCompleted["email"],
                "EnsureEmailFromSsoJwtClaims should promote preferred_username into userData.email when backend userData has no email.");
            Assert.IsFalse(userDataAtAuthCompleted.ContainsKey("sso_email"),
                "The SSO email fallback should use the canonical email key when there is no existing backend email conflict.");

            yield return WaitUntil(
                () => syncDone,
                timeoutSeconds: 5f,
                description: "SSO email fallback userData should be synced with a custom re-auth request");

            Abxr.OnUserDataSyncCompleted = null;
            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count,
                "Expected device auth plus one custom user-data sync. There should be no assessmentPin user-auth POST.");

            Assert.IsNull(requests[0].BodyJson["authMechanism"],
                "Initial device auth must not send authMechanism.");

            var syncMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)syncMechanism?["type"]);
            Assert.AreEqual("user", (string)syncMechanism?["inputSource"]);
            Assert.AreEqual("device", (string)syncMechanism?["authMode"],
                "The SSO sync should preserve existing device-auth userData.");
            Assert.AreEqual(ssoEmail, (string)syncMechanism?["preferred_username"],
                "The original SSO preferred_username claim should be preserved in userData.");
            Assert.AreEqual(ssoEmail, (string)syncMechanism?["email"],
                "The custom sync request should include the userData.email value populated from preferred_username.");
            Assert.IsNull(syncMechanism?["sso_email"],
                "No prefixed SSO email should be sent when the backend response did not already contain email.");
            Assert.IsNull(syncMechanism?["assessmentPin"],
                "SSO bypass should not submit an assessmentPin auth mechanism.");
        }

        [UnityTest]
        public IEnumerator Auth_MdmSsoClaimConflicts_PreservesBackendUserData_AndStoresSsoValuesWithPrefix()
        {
            var ssoToken = JwtWithClaims(new Dictionary<string, object>
            {
                { "sub", "sso-subject-456" },
                { "cohort", "sso-cohort" },
                { "role", "sso-role" },
                { "email", "sso.user@example.com" }
            });

            AbxrTestHooks.SetSsoForTest(isAuthenticated: true, accessToken: ssoToken);

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "device" },
                    { "cohort", "backend-cohort" },
                    { "role", "backend-role" },
                    { "sso_role", "existing-prefixed-role" },
                    { "email", "backend@example.com" }
                }));
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object>
                {
                    { "authMode", "sso-conflicts-synced" },
                }));

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            yield return WaitUntil(
                () => syncDone,
                timeoutSeconds: 5f,
                description: "SSO claim conflicts should be synced with prefixed userData keys");

            Abxr.OnUserDataSyncCompleted = null;
            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count,
                "Expected device auth plus one custom user-data sync for merged SSO claims.");

            var syncMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)syncMechanism?["type"]);
            Assert.AreEqual("user", (string)syncMechanism?["inputSource"]);

            Assert.AreEqual("sso-subject-456", (string)syncMechanism?["sub"],
                "Non-conflicting SSO claims should be copied into userData directly.");

            Assert.AreEqual("backend-cohort", (string)syncMechanism?["cohort"],
                "Existing backend userData should not be overwritten by an SSO claim with the same key.");
            Assert.AreEqual("sso-cohort", (string)syncMechanism?["sso_cohort"],
                "Conflicting SSO claim values should be stored under sso_<key>.");

            Assert.AreEqual("backend-role", (string)syncMechanism?["role"],
                "The original conflicting userData key should keep the backend value.");
            Assert.AreEqual("existing-prefixed-role", (string)syncMechanism?["sso_role"],
                "If the first prefixed key already exists, it should not be overwritten.");
            Assert.AreEqual("sso-role", (string)syncMechanism?["sso_role_1"],
                "When sso_<key> already exists, the SSO value should use the next available suffixed key.");

            Assert.AreEqual("backend@example.com", (string)syncMechanism?["email"],
                "Existing backend email should not be overwritten by SSO email.");
            Assert.AreEqual("sso.user@example.com", (string)syncMechanism?["sso_email"],
                "Conflicting SSO email should be stored under the prefixed key like other userData conflicts.");
        }

        [UnityTest]
        public IEnumerator Auth_MdmSsoTokenWithoutIdentity_DoesNotBypassPinPrompt()
        {
            var ssoTokenWithoutIdentity = JwtWithClaims(new Dictionary<string, object>
            {
                { "aud", "example-audience" },
                { "iss", "example-issuer" }
            });

            AbxrTestHooks.SetSsoForTest(isAuthenticated: true, accessToken: ssoTokenWithoutIdentity);

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));
            QueueAssessmentPinConfig();
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "pinStatus", "accepted" } }));
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "sso-claims-synced-after-pin" } }));

            bool inputRequested = false;
            Abxr.OnInputRequested = (type, prompt, _, _) =>
            {
                inputRequested = true;
                Assert.AreEqual("assessmentPin", type);
                Assert.AreEqual("Enter your test PIN", prompt);
                Abxr.OnInputSubmitted("123456");
            };

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsTrue(inputRequested,
                "SSO token without usable identity claims should not bypass user authentication.");

            yield return WaitUntil(
                () => syncDone,
                timeoutSeconds: 5f,
                description: "decodable non-identity SSO claims are synced after explicit PIN auth");

            Abxr.OnUserDataSyncCompleted = null;
            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(3, requests.Count,
                "Expected device auth, explicit PIN user auth, then current SSO-claim sync behavior after successful PIN auth.");

            Assert.IsNull(requests[0].BodyJson["authMechanism"]);

            var pinMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)pinMechanism?["type"]);
            Assert.AreEqual("123456", (string)pinMechanism?["prompt"]);
            Assert.AreEqual("user", (string)pinMechanism?["inputSource"]);

            var syncMechanism = requests[2].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)syncMechanism?["type"]);
            Assert.IsNull(syncMechanism?["assessmentPin"],
                "The follow-up SSO claim sync should not masquerade as a PIN auth request.");
        }

        [UnityTest]
        public IEnumerator Auth_GetConfig_WhenLearnerLauncherModeEnabled_ForcesAssessmentPinEvenWhenConfigSaysNone()
        {
            Configuration.Instance.enableLearnerLauncherMode = true;
            QueueAuthMechanismConfig("none", "Enter learner-launcher PIN");

            bool inputRequested = false;
            string requestedType = null;
            string requestedPrompt = null;
            string requestedError = null;
            Abxr.OnInputRequested = (type, prompt, _, error) =>
            {
                inputRequested = true;
                requestedType = type;
                requestedPrompt = prompt;
                requestedError = error;
                Abxr.OnInputSubmitted("learner-pin-123");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsTrue(inputRequested,
                "Learner Launcher Mode should require learner input even when GET config says no user auth is required.");
            Assert.AreEqual("assessmentPin", requestedType);
            Assert.AreEqual("Enter learner-launcher PIN", requestedPrompt);
            Assert.AreEqual("", requestedError);

            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected device auth followed by forced learner PIN auth");
            Assert.IsNull(requests[0].BodyJson["authMechanism"]);

            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
            Assert.AreEqual("learner-pin-123", (string)mechanism?["prompt"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
        }

        [UnityTest]
        public IEnumerator Auth_MdmSsoIdentity_DoesNotBypassPrompt_WhenLearnerLauncherModeEnabled()
        {
            Configuration.Instance.enableLearnerLauncherMode = true;

            var ssoToken = JwtWithClaims(new Dictionary<string, object>
            {
                { "sub", "sso-subject-123" },
                { "email", "sso.user@example.com" }
            });

            AbxrTestHooks.SetSsoForTest(isAuthenticated: true, accessToken: ssoToken);

            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "device" } }));
            QueueAssessmentPinConfig("Enter learner-launcher PIN");
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "pinStatus", "accepted" } }));
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "authMode", "sso-synced-after-learner-auth" } }));

            bool inputRequested = false;
            Abxr.OnInputRequested = (type, prompt, _, _) =>
            {
                inputRequested = true;
                Assert.AreEqual("assessmentPin", type);
                Assert.AreEqual("Enter learner-launcher PIN", prompt);
                Abxr.OnInputSubmitted("123456");
            };

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsTrue(inputRequested,
                "Learner Launcher Mode should force learner auth even when MDM SSO identity exists.");

            yield return WaitUntil(
                () => syncDone,
                timeoutSeconds: 5f,
                description: "SSO claims should sync only after explicit learner auth in Learner Launcher Mode");

            Abxr.OnUserDataSyncCompleted = null;
            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(3, requests.Count,
                "Expected device auth, explicit learner PIN auth, then SSO user-data sync.");

            var pinMechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)pinMechanism?["type"]);
            Assert.AreEqual("123456", (string)pinMechanism?["prompt"]);

            var syncMechanism = requests[2].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)syncMechanism?["type"]);
            Assert.AreEqual("sso-subject-123", (string)syncMechanism?["sub"]);
            Assert.IsNull(syncMechanism?["assessmentPin"],
                "Learner Launcher Mode should use a real learner PIN request before any SSO claim sync.");
        }

        [UnityTest]
        public IEnumerator Auth_EmailInput_AppendsDomain_And_SendsEmailAuthMechanism()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody());
            QueueAuthMechanismConfig("email", "Enter school email", domain: "school.edu");
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "email", "learner@school.edu" } }));

            string requestedDomain = null;
            Abxr.OnInputRequested = (_, _, domain, _) =>
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
            Assert.AreEqual("email", (string)mechanism?["type"]);
            Assert.AreEqual("learner@school.edu", (string)mechanism?["prompt"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
            Assert.IsNull(mechanism?["domain"], "domain is client-side prompt/config data and should not be sent in authMechanism");

            var userData = Abxr.GetUserData();
            Assert.IsNotNull(userData);
            Assert.AreEqual("learner@school.edu", userData["email"]);
        }

        [UnityTest]
        public IEnumerator Auth_TextInput_Sends_TextAuthMechanism()
        {
            QueueAuthMechanismConfig("text", "Enter learner id");

            bool inputRequested = false;
            string requestedType = null;
            string requestedPrompt = null;
            Abxr.OnInputRequested = (type, prompt, _, _) =>
            {
                inputRequested = true;
                requestedType = type;
                requestedPrompt = prompt;
                Abxr.OnInputSubmitted("learner-123");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(inputRequested, "Expected the SDK to request text input from the app.");
            Assert.AreEqual("text", requestedType);
            Assert.AreEqual("Enter learner id", requestedPrompt);
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected device auth followed by text user auth");
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("text", (string)mechanism?["type"]);
            Assert.AreEqual("learner-123", (string)mechanism?["prompt"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
        }

        // ── Response shape variations ──────────────────────────────

        [UnityTest]
        public IEnumerator Config_Endpoint_Override_Merges_Into_Runtime()
        {
            // /v1/storage/config can override a subset of runtime config (see Configuration.ApplyConfigPayload)
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 200,
                body: new Dictionary<string, object>
                {
                    { "sendNextBatchWait", "47" },
                    { "maximumCachedItems", "999" }
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
        public IEnumerator Auth_ConfigFailure_Continues_AsAnonymous_WithoutInput()
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: 500,
                body: new Dictionary<string, object> { { "detail", "config unavailable" } });

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "config failures should keep the device-authenticated anonymous session rather than prompting for a synthetic authMechanism");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator Auth_ConfigAuthMechanismNone_DoesNotRequestInput_AndSucceeds()
        {
            QueueAuthMechanismConfig("none", "ignored");

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

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

        private static void SetPrivateBoolForTest(object target, string fieldName, bool value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected {target.GetType().Name}.{fieldName} to exist for test setup.");
            Assert.AreEqual(typeof(bool), field.FieldType,
                $"Expected {target.GetType().Name}.{fieldName} to be a bool field.");
            field.SetValue(target, value);
        }

        private static void SetAuthResponseForTest(object authService, AuthResponse response)
        {
            var responseDataSetter = authService.GetType()
                .GetProperty("ResponseData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetSetMethod(nonPublic: true);

            Assert.IsNotNull(responseDataSetter, "Expected AbxrAuthService.ResponseData to have a private setter for test setup.");
            responseDataSetter.Invoke(authService, new object[] { response });
        }

        private static void AssertAuthHeadersNotSet(UnityWebRequest request)
        {
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("Authorization")),
                "SetAuthHeaders should not set Authorization when tokens are unavailable.");
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("x-abxrlib-timestamp")),
                "SetAuthHeaders should not set x-abxrlib-timestamp when tokens are unavailable.");
            Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("x-abxrlib-hash")),
                "SetAuthHeaders should not set x-abxrlib-hash when tokens are unavailable.");
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

            Action<bool, string> authCompletedHandler = (_, _) => authCompletedCount++;
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
                { "inputSource", "should-not-leak" }
            });

            yield return WaitUntil(() => syncDone, 5f, "SetUserData re-auth completed");

            Abxr.OnAuthCompleted -= authCompletedHandler;
            Abxr.OnUserDataSyncCompleted = null;

            Assert.IsTrue(syncSuccess, syncError);
            Assert.AreEqual(0, authCompletedCount, "SetUserData re-auth should report through OnUserDataSyncCompleted, not OnAuthCompleted.");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected initial auth plus SetUserData custom re-auth");
            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)mechanism?["type"]);
            Assert.AreEqual("user", (string)mechanism?["inputSource"]);
            Assert.AreEqual("learner-42", (string)mechanism?["id"]);
            Assert.AreEqual("learner@example.com", (string)mechanism?["email"]);
            Assert.AreEqual("alpha", (string)mechanism?["cohort"]);
            Assert.IsNull(mechanism?["prompt"], "reserved user-data field prompt should not be forwarded as custom auth data");
        }
    }
}

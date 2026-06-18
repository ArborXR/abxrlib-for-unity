using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Failure, retry, latch, and stopped/inactive attempt behavior.
    /// </summary>
    public partial class AuthenticationTests
    {
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

        [UnityTest]
        public IEnumerator Auth_Retries_Transient_500_EmptyBody_Then_Succeeds()
        {
            Configuration.Instance.sendRetriesOnFailure = 1;
            Configuration.Instance.sendRetryIntervalSeconds = 1;

            FakeBackend.QueueEmptyBodyScenario(path: "/v1/auth/token", status: 500);
            FakeBackend.QueueScenario(path: "/v1/auth/token", status: 201, body: AuthBody());

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
    }
}

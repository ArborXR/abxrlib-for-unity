using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Token-expiry polling and automatic re-authentication behavior.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator Auth_ReAuthPoll_WhenTokenNearExpiry_StartsNewAuthentication()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "phase", "initial" } }));
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "phase", "reauth" } }));

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "the re-auth poll should only trigger after the initial auth attempt is idle.");

            service.SetTokenExpiryForTest(DateTime.UtcNow.AddSeconds(30));

            bool reauthDone = false;
            bool reauthSuccess = false;
            string reauthError = null;
            Action<bool, string> reauthCompletedHandler = (success, error) =>
            {
                reauthDone = true;
                reauthSuccess = success;
                reauthError = error;
            };

            Abxr.OnAuthCompleted += reauthCompletedHandler;
            try
            {
                Assert.IsTrue(service.ReAuthPollerForTest.TryTriggerReAuthIfNeeded(),
                    "near-expiry tokens should trigger a re-auth attempt.");

                yield return WaitUntil(() => reauthDone, 5f, "re-auth poll triggered authentication completed");
            }
            finally
            {
                Abxr.OnAuthCompleted -= reauthCompletedHandler;
            }

            Assert.IsTrue(reauthSuccess, reauthError);
            Assert.IsTrue(service.Authenticated,
                "the service should remain authenticated after poll-triggered re-auth succeeds.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "the poll-triggered re-auth attempt should complete and clear the active flag.");
            Assert.AreEqual("reauth", Abxr.GetUserData()["phase"],
                "poll-triggered re-auth should apply the fresh auth response.");

            var authRequests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, authRequests.Count,
                "near-expiry tokens should trigger exactly one additional device-auth request.");
            Assert.IsNull(authRequests[1].BodyJson["authMechanism"],
                "poll-triggered re-auth should be a device-auth refresh, not user auth.");
            Assert.AreEqual(2, FakeBackend.GetRequests("/v1/storage/config").Count,
                "poll-triggered re-auth should run the normal post-auth config fetch.");
        }

        [UnityTest]
        public IEnumerator Auth_ReAuthPoll_WhenTokenNotNearExpiry_DoesNotStartAuthentication()
        {
            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            var service = AbxrTestHooks.GetAuthServiceForTest();
            Assert.IsNotNull(service, "Auth service should exist after fixture setup.");
            service.SetTokenExpiryForTest(DateTime.UtcNow.AddMinutes(10));

            int authRequestsBeforePoll = FakeBackend.GetRequests("/v1/auth/token").Count;
            int configRequestsBeforePoll = FakeBackend.GetRequests("/v1/storage/config").Count;

            Assert.IsFalse(service.ReAuthPollerForTest.TryTriggerReAuthIfNeeded(),
                "tokens outside the re-auth threshold should not trigger a re-auth attempt.");

            yield return null;

            Assert.AreEqual(authRequestsBeforePoll, FakeBackend.GetRequests("/v1/auth/token").Count,
                "tokens outside the re-auth threshold should not start a new auth request.");
            Assert.AreEqual(configRequestsBeforePoll, FakeBackend.GetRequests("/v1/storage/config").Count,
                "tokens outside the re-auth threshold should not fetch config again.");
            Assert.IsFalse(service.IsAuthenticationAttemptActive,
                "a non-expiring token poll should leave auth idle.");
        }
    }
}

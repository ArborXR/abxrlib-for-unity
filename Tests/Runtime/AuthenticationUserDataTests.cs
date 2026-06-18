using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// SetUserData validation, merge, and custom re-auth sync behavior.
    /// </summary>
    public partial class AuthenticationTests
    {
        [UnityTest]
        public IEnumerator SetUserData_BeforeAuth_IsIgnored_AndDoesNotSendRequests()
        {
            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] Cannot set user data - not authenticated\. Call Authenticate\(\) first\."));

            Abxr.SetUserData("learner-before-auth", new Dictionary<string, string>
            {
                { "email", "before-auth@example.com" }
            });
            yield return null;

            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/auth/token").Count,
                "SetUserData before authentication should be a no-op and should not start auth implicitly.");
            Assert.AreEqual(0, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator SetUserData_WhileUserDataSyncInProgress_IsIgnored()
        {
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                body: AuthBody(userData: new Dictionary<string, object> { { "email", "initial@example.com" } }));
            FakeBackend.QueueScenario(
                path: "/v1/auth/token",
                status: 201,
                delayMs: 250,
                body: AuthBody(userData: new Dictionary<string, object> { { "email", "first-sync@example.com" } }));

            yield return RunAuthAndWait();
            Assert.IsTrue(LastAuthSuccess, LastAuthError);

            bool syncDone = false;
            bool syncSuccess = false;
            string syncError = null;
            Abxr.OnUserDataSyncCompleted = (success, error) =>
            {
                syncDone = true;
                syncSuccess = success;
                syncError = error;
            };

            Abxr.SetUserData("first-sync", new Dictionary<string, string>
            {
                { "cohort", "alpha" }
            });

            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] Authentication in progress\. Unable to sync user data\."));

            Abxr.SetUserData("second-sync", new Dictionary<string, string>
            {
                { "cohort", "beta" }
            });

            yield return WaitUntil(() => syncDone, 5f, "first SetUserData re-auth completed");
            Abxr.OnUserDataSyncCompleted = null;

            Assert.IsTrue(syncSuccess, syncError);

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count,
                "the second SetUserData call should be ignored while the first custom re-auth is active.");

            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("custom", (string)mechanism?["type"]);
            Assert.AreEqual("first-sync", (string)mechanism?["id"]);
            Assert.AreEqual("alpha", (string)mechanism?["cohort"]);
            Assert.AreNotEqual("second-sync", (string)mechanism?["id"],
                "ignored SetUserData calls should not replace the in-flight custom auth payload.");
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

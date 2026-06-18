using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// MDM SSO identity merge, conflict, and prompt-bypass behavior.
    /// </summary>
    public partial class AuthenticationTests
    {
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
                "SsoUserDataMerger should promote preferred_username into userData.email when backend userData has no email.");
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
    }
}

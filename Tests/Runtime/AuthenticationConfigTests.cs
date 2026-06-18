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
    /// GET config behavior, runtime config merging, and config authMechanism fallbacks.
    /// </summary>
    public partial class AuthenticationTests
    {
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
        public IEnumerator Auth_ConfigUnsupportedAuthMechanismType_DoesNotRequestInput_AndSucceeds()
        {
            QueueAuthMechanismConfig("magicLink", "Open your email");

            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] Unsupported authMechanism\.type 'magicLink' from configuration; continuing without user authentication\."));

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "unsupported authMechanism types should be ignored rather than producing an unusable input prompt.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count,
                "unsupported config authMechanism should not trigger a follow-up user-auth request.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }

        [UnityTest]
        public IEnumerator Auth_MalformedConfigResponse_Continues_AsAnonymous_WithoutInput()
        {
            FakeBackend.QueueRawScenario(
                path: "/v1/storage/config",
                status: 200,
                raw: "{not-valid-json",
                method: "GET");

            LogAssert.Expect(LogType.Error, new Regex(@"\[AbxrLib\] GetConfiguration response handling failed:"));
            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] GET config failed \(.*\); continuing with Configuration defaults and no user auth prompt \(authMechanism cleared\)\."));

            bool inputRequested = false;
            Abxr.OnInputRequested = (_, _, _, _) => inputRequested = true;

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            Assert.IsFalse(inputRequested,
                "malformed GET config responses should fall back to anonymous device auth instead of prompting for user auth.");
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
            Assert.AreEqual(1, FakeBackend.GetRequests("/v1/storage/config").Count);
        }
    }
}

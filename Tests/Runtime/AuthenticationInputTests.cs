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
    /// PIN/email/text input prompts, submissions, retries, and skip behavior.
    /// </summary>
    public partial class AuthenticationTests
    {
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
        public IEnumerator Auth_SubmitInput_Sends_PerRequestInputSource()
        {
            QueueAssessmentPinConfig();

            Abxr.OnInputRequested = (_, _, _, _) =>
            {
                var service = AbxrTestHooks.GetAuthServiceForTest();
                Assert.IsNotNull(service, "auth service should exist while auth input is pending");
                service.SubmitInput("123456", "QRlms");
            };

            yield return RunAuthAndWait();

            Assert.IsTrue(LastAuthSuccess, LastAuthError);
            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(2, requests.Count, "expected device auth followed by sourced user auth");

            var mechanism = requests[1].BodyJson["authMechanism"];
            Assert.AreEqual("assessmentPin", (string)mechanism?["type"]);
            Assert.AreEqual("123456", (string)mechanism?["prompt"]);
            Assert.AreEqual("QRlms", (string)mechanism?["inputSource"]);
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

                AbxrTestHooks.SkipUserAuthenticationForTest();
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
                "skipping user authentication should clear the pending-input state");

            var requests = FakeBackend.GetRequests("/v1/auth/token");
            Assert.AreEqual(1, requests.Count,
                "skipping user authentication should accept the device-authenticated session and avoid the follow-up user-auth POST.");
            Assert.IsNull(requests[0].BodyJson["authMechanism"],
                "device auth must not send authMechanism; skip should not create a user-auth request.");

            var userData = Abxr.GetUserData();
            Assert.IsNotNull(userData);
            Assert.AreEqual("device", userData["authMode"],
                "skip should preserve the original device-auth response data rather than replacing it with user-auth data.");
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
    }
}

using System.Collections.Generic;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    [TestFixture]
    public class AuthRequestBuilderTests
    {
        [Test]
        public void BuildPayload_ForDeviceAuth_DoesNotSendAuthMechanism()
        {
            var sessionPayload = new AuthPayload
            {
                sessionId = "session-id",
                authMechanism = new Dictionary<string, string> { ["type"] = "stale" }
            };

            AuthPayload requestPayload = AuthRequestBuilder.BuildPayload(
                sessionPayload, AuthRequestStage.Device, sessionAuthMechanism: null, userDataSnapshot: null);

            Assert.AreNotSame(sessionPayload, requestPayload);
            Assert.AreEqual("session-id", requestPayload.sessionId);
            Assert.IsNull(requestPayload.authMechanism);
            Assert.AreEqual("stale", sessionPayload.authMechanism["type"]);
        }

        [Test]
        public void BuildRequestAuthMechanism_ForUserInput_UsesSubmittedPromptAndSource()
        {
            var mechanism = new AuthMechanism
            {
                type = AuthMechanismResolver.AssessmentPin,
                prompt = "Configured prompt",
                inputSource = AuthMechanismResolver.UserInputSource
            };

            var requestMechanism = AuthRequestBuilder.BuildRequestAuthMechanism(
                AuthRequestStage.UserInput,
                mechanism,
                userDataSnapshot: null,
                submittedAuthPrompt: "123456",
                submittedInputSource: AuthMechanismResolver.QrLmsInputSource);

            Assert.AreEqual(AuthMechanismResolver.AssessmentPin, requestMechanism["type"]);
            Assert.AreEqual("123456", requestMechanism["prompt"]);
            Assert.AreEqual(AuthMechanismResolver.QrLmsInputSource, requestMechanism["inputSource"]);
        }

        [Test]
        public void BuildSubmittedAuthPrompt_ForEmail_AppendsDomainWhenMissing()
        {
            var mechanism = new AuthMechanism
            {
                type = AuthMechanismResolver.Email,
                domain = "school.edu"
            };

            Assert.AreEqual("learner@school.edu", AuthRequestBuilder.BuildSubmittedAuthPrompt(mechanism, "learner"));
            Assert.AreEqual("learner@example.com", AuthRequestBuilder.BuildSubmittedAuthPrompt(mechanism, "learner@example.com"));
        }

        [Test]
        public void BuildRequestAuthMechanism_ForUserDataSync_SendsCustomShapeAndSkipsReservedKeys()
        {
            var userData = new Dictionary<string, string>
            {
                ["id"] = "learner-1",
                ["cohort"] = "alpha",
                ["type"] = "reserved-type",
                ["prompt"] = "reserved-prompt",
                ["inputSource"] = "reserved-source"
            };

            var requestMechanism = AuthRequestBuilder.BuildRequestAuthMechanism(
                AuthRequestStage.UserDataSync,
                sessionAuthMechanism: null,
                userDataSnapshot: userData);

            Assert.AreEqual("custom", requestMechanism["type"]);
            Assert.AreEqual(AuthMechanismResolver.UserInputSource, requestMechanism["inputSource"]);
            Assert.AreEqual("learner-1", requestMechanism["id"]);
            Assert.AreEqual("alpha", requestMechanism["cohort"]);
            Assert.IsFalse(requestMechanism.ContainsValue("reserved-type"));
            Assert.IsFalse(requestMechanism.ContainsValue("reserved-prompt"));
            Assert.IsFalse(requestMechanism.ContainsValue("reserved-source"));
        }
    }
}

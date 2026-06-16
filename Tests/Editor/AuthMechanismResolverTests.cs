using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for the auth-mechanism normalization rules used by AbxrAuthService.
    /// </summary>
    [TestFixture]
    public class AuthMechanismResolverTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("none")]
        [TestCase("NoNe")]
        public void CopyForSession_ReturnsNull_WhenUserAuthIsNotRequired(string type)
        {
            var source = type == null ? null : new AuthMechanism { type = type, prompt = "Prompt" };

            Assert.IsNull(AuthMechanismResolver.CopyForSession(source));
        }

        [Test]
        public void CopyForSession_NormalizesSupportedTypes_AndDefaultsInputSource()
        {
            var copy = AuthMechanismResolver.CopyForSession(new AuthMechanism
            {
                type = "EMAIL",
                prompt = "Enter email",
                domain = "school.edu",
                inputSource = "",
                allowGuest = true
            });

            Assert.IsNotNull(copy);
            Assert.AreEqual(AuthMechanismResolver.Email, copy.type);
            Assert.AreEqual("Enter email", copy.prompt);
            Assert.AreEqual("school.edu", copy.domain);
            Assert.AreEqual(AuthMechanismResolver.UserInputSource, copy.inputSource);
            Assert.AreEqual(true, copy.allowGuest);
        }

        [Test]
        public void CopyForSession_ReturnsNullAndLogsWarning_ForUnsupportedType()
        {
            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[AbxrLib\] Unsupported authMechanism\.type 'qrCode' from configuration; continuing without user authentication\."));

            Assert.IsNull(AuthMechanismResolver.CopyForSession(new AuthMechanism
            {
                type = "qrCode",
                prompt = "Scan code"
            }));
        }

        [Test]
        public void ResolveConfigMechanism_WhenLearnerLauncherModeEnabled_ForcesAssessmentPin()
        {
            var mechanism = AuthMechanismResolver.ResolveConfigMechanism(new AuthMechanism
            {
                type = "none",
                prompt = "Enter learner PIN",
                domain = "district.example"
            }, learnerLauncherModeEnabled: true);

            Assert.IsNotNull(mechanism);
            Assert.AreEqual(AuthMechanismResolver.AssessmentPin, mechanism.type);
            Assert.AreEqual("Enter learner PIN", mechanism.prompt);
            Assert.AreEqual("district.example", mechanism.domain);
            Assert.AreEqual(AuthMechanismResolver.UserInputSource, mechanism.inputSource);
        }

        [Test]
        public void ResolveConfigMechanism_WhenLearnerLauncherModeDisabled_UsesNormalizedConfigMechanism()
        {
            var mechanism = AuthMechanismResolver.ResolveConfigMechanism(new AuthMechanism
            {
                type = "Text",
                prompt = "Enter learner id",
                inputSource = "qr"
            }, learnerLauncherModeEnabled: false);

            Assert.IsNotNull(mechanism);
            Assert.AreEqual(AuthMechanismResolver.Text, mechanism.type);
            Assert.AreEqual("Enter learner id", mechanism.prompt);
            Assert.AreEqual("qr", mechanism.inputSource);
        }

        [Test]
        public void ForceAssessmentPin_PreservesExistingPromptDomainAndInputSource()
        {
            var mechanism = AuthMechanismResolver.ForceAssessmentPin(new AuthMechanism
            {
                type = "email",
                prompt = "Original prompt",
                domain = "school.edu",
                inputSource = "launcher"
            });

            Assert.AreEqual(AuthMechanismResolver.AssessmentPin, mechanism.type);
            Assert.AreEqual("Original prompt", mechanism.prompt);
            Assert.AreEqual("school.edu", mechanism.domain);
            Assert.AreEqual("launcher", mechanism.inputSource);
        }

        [Test]
        public void IsRequestMeaningful_RequiresNonEmptyType()
        {
            Assert.IsFalse(AuthMechanismResolver.IsRequestMeaningful(null));
            Assert.IsFalse(AuthMechanismResolver.IsRequestMeaningful(new Dictionary<string, string>()));
            Assert.IsFalse(AuthMechanismResolver.IsRequestMeaningful(new Dictionary<string, string> { { "type", "" } }));
            Assert.IsTrue(AuthMechanismResolver.IsRequestMeaningful(new Dictionary<string, string> { { "type", "custom" } }));
        }
    }
}

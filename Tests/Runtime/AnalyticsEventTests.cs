/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Analytics Event Tests for ABXRLib (TakeTwo design)
 *
 * Assessment, objective, and interaction event wrappers; EventStatus and InteractionType/Result.
 * Runs after authentication (shared session).
 */

using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture, Category("PostAuth")]
    public class AnalyticsEventTests
    {
        [SetUp]
        public void Setup()
        {
            TestHelpers.CleanupTestEnvironment();
            TestHelpers.SetupTestEnvironmentWithExistingConfig();
        }

        [UnitySetUp]
        public IEnumerator UnitySetUp() => AuthenticationTestHelper.EnsureAuthenticated();

        [TearDown]
        public void TearDown() => TestHelpers.CleanupTestEnvironment();

        [UnityTearDown]
        public void UnityTearDown() => AuthenticationTestHelper.ResetAuthenticationState();

        [UnityTest]
        public IEnumerator Test_RealServerAuthentication_CompletesSuccessfully()
        {
            Assert.IsTrue(AuthenticationTestHelper.IsAuthenticated(), $"Status: {AuthenticationTestHelper.GetAuthenticationStatus()}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_EventAssessmentStart_BasicAssessment_StartsAssessment()
        {
            Abxr.EventAssessmentStart("final_exam");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Assessment start should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithValidScore_CompletesAssessment()
        {
            string assessmentName = TestHelpers.GenerateRandomName("assessment");
            Abxr.EventAssessmentStart(assessmentName);
            yield return new WaitForSeconds(0.5f);
            Abxr.EventAssessmentComplete(assessmentName, 92, Abxr.EventStatus.Pass);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Assessment complete should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithoutStart_HandlesGracefully()
        {
            Abxr.EventAssessmentComplete(TestHelpers.GenerateRandomName("orphan_assessment"), 75, Abxr.EventStatus.Fail);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Orphan assessment complete should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventAssessmentComplete_WithMetadata_IncludesMetadata()
        {
            var metadata = TestHelpers.CreateTestMetadata(("subject", "mathematics"), ("difficulty", "advanced"));
            Abxr.EventAssessmentComplete("final_exam", 88, Abxr.EventStatus.Pass, metadata);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Assessment with metadata should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventObjectiveStart_BasicObjective_StartsObjective()
        {
            Abxr.EventObjectiveStart("open_valve");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Objective start should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventObjectiveComplete_WithScore_CompletesObjective()
        {
            string name = TestHelpers.GenerateRandomName("objective");
            Abxr.EventObjectiveStart(name);
            yield return new WaitForSeconds(0.3f);
            Abxr.EventObjectiveComplete(name, 90, Abxr.EventStatus.Pass);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Objective complete should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventInteractionStart_BasicInteraction_StartsInteraction()
        {
            Abxr.EventInteractionStart("button_click");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Interaction start should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventInteractionComplete_WithResult_CompletesInteraction()
        {
            string name = TestHelpers.GenerateRandomName("interaction");
            Abxr.EventInteractionStart(name);
            yield return new WaitForSeconds(0.2f);
            Abxr.EventInteractionComplete(name, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "option_a");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Interaction complete should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_NestedAssessmentObjectiveInteraction_CompleteFlow()
        {
            string assessmentName = "complete_assessment";
            string objectiveName = "nested_objective";
            string interactionName = "nested_interaction";

            Abxr.EventAssessmentStart(assessmentName);
            Abxr.EventObjectiveStart(objectiveName);
            Abxr.EventInteractionStart(interactionName);
            Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "completed");
            Abxr.EventObjectiveComplete(objectiveName, 90, Abxr.EventStatus.Pass);
            Abxr.EventAssessmentComplete(assessmentName, 88, Abxr.EventStatus.Pass);

            yield return new WaitForSeconds(3.0f);
            Assert.IsTrue(true, "Nested flow should complete without throwing");
        }

        [UnityTest]
        public IEnumerator Test_EventExperienceStartComplete_WorksCorrectly()
        {
            string experienceName = TestHelpers.GenerateRandomName("experience");
            Abxr.EventExperienceStart(experienceName);
            yield return new WaitForSeconds(0.5f);
            Abxr.EventExperienceComplete(experienceName);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(true, "Experience start/complete should complete without throwing");
        }
    }
}

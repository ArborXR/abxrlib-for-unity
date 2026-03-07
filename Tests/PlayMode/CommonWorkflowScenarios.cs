// Copyright (c) 2026 ArborXR. All rights reserved.
// Common workflow scenarios seen from users of the Unity package. Use PerformAuth / RunEndSession from base; add more scenarios as needed.
// All scenarios use useAppTokens=true, buildType=production_custom so they work consistently when Configuration has app token and org token set for unit tests.
using System.Collections;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class CommonWorkflowScenarios : AbxrPlayModeTestBase
{
    /// <summary>Sets runtime auth to production_custom with app/org tokens from Configuration so auth succeeds when config is set up for unit tests.</summary>
    private void SetProductionCustomAuth()
    {
        var c = Configuration.Instance;
        SetRuntimeAuth(new RuntimeAuthConfig
        {
            useAppTokens = true,
            buildType = "production_custom",
            appToken = c?.appToken ?? "",
            orgToken = c?.orgToken ?? ""
        });
    }

    [UnityTest]
    public IEnumerator FullAssessmentWithObjectivesAndInteractions_Scenario()
    {
        SetProductionCustomAuth();
        // 1. Authenticate using the framework
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed for this scenario.");

        // 2. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 3. Random name used for assessment, objectives, and interactions
        string randomName = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        int objectiveNum = 0;

        // 4. Start assessment
        Debug.Log("[AbxrLib] (Test) EventAssessmentStart: Assessment_" + randomName);
        Abxr.EventAssessmentStart("Assessment_" + randomName);

        // 5. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 6. Generic event
        Debug.Log("[AbxrLib] (Test) Event: item_grabbed");
        Abxr.Event("item_grabbed", new Abxr.Dict().With("item_type", "tool").With("item_value", "hammer"), sendTelemetry: false);

        // 7. Start objective
        string objectiveName = $"Objective_{randomName}_{objectiveNum}";
        Debug.Log("[AbxrLib] (Test) EventObjectiveStart: " + objectiveName);
        Abxr.EventObjectiveStart(objectiveName);

        // 8. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 9. Interaction start
        Debug.Log("[AbxrLib] (Test) EventInteractionStart: select_option_a");
        Abxr.EventInteractionStart("select_option_a");

        // 10. Wait 1.5 seconds
        yield return new WaitForSeconds(1.5f);

        // 11. Interaction complete
        Debug.Log("[AbxrLib] (Test) EventInteractionComplete: select_option_a");
        Abxr.EventInteractionComplete("select_option_a", Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "true");

        // 12. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 13. Objective complete (score 80–100, Pass)
        int objectiveScore = Random.Range(80, 101);
        Debug.Log("[AbxrLib] (Test) EventObjectiveComplete: " + objectiveName + " score: " + objectiveScore);
        Abxr.EventObjectiveComplete(objectiveName, objectiveScore, Abxr.EventStatus.Pass);

        // 14. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 15. Assessment complete (score 80–100, Pass)
        int assessmentScore = Random.Range(80, 101);
        Debug.Log("[AbxrLib] (Test) EventAssessmentComplete: Assessment_" + randomName + " score: " + assessmentScore + " result: " + Abxr.EventStatus.Pass.ToString());
        Abxr.EventAssessmentComplete("Assessment_" + randomName, assessmentScore, Abxr.EventStatus.Pass);
    }

    [UnityTest]
    public IEnumerator NoAssessmentWithEventAndObjective_Scenario()
    {
        SetProductionCustomAuth();
        // 1. Authenticate using the framework (no assessment in this scenario)
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed for this scenario.");

        // 2. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 3. Random objective name (no assessment in this scenario)
        string randomName = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string objectiveName = "Objective_" + randomName;

        // 4. Generic event
        Debug.Log("[AbxrLib] (Test) Event: option_selected");
        Abxr.Event("option_selected", new Abxr.Dict().With("door", "tool").With("item_value", "2"), sendTelemetry: false);

        // 5. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 6. Start objective (no assessment)
        Debug.Log("[AbxrLib] (Test) EventObjectiveStart: " + objectiveName);
        Abxr.EventObjectiveStart(objectiveName);

        // 7. Wait 1 second
        yield return new WaitForSeconds(1f);

        // 8. Trigger the on-quit handler (close running events, send all)
        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }
}

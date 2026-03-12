// Copyright (c) 2026 ArborXR. All rights reserved.
// Common workflow scenarios seen from users of the Unity package. Use PerformAuth / RunEndSession from base; add more scenarios as needed.
// All scenarios use useAppTokens=true, buildType=production_custom so they work consistently when Configuration has app token and org token set for unit tests.
// Scenarios align with developer-portal docs: Events, Logs, Experience, Level, Critical, Timed events, Register, Storage, Telemetry.
using System.Collections;
using System.Collections.Generic;
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
        // Narrative: Full pass path – assessment start, generic event + position, one objective with interaction (correct), timed segment, logs, assessment complete with meta.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed for this scenario.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string randomName = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string objectiveName = "Objective_" + randomName + "_0";

        Logcat.Info("(Test) EventAssessmentStart: Assessment_" + randomName);
        Abxr.EventAssessmentStart("Assessment_" + randomName);
        Logcat.Info("(Test) LogInfo: Assessment started (meta: assessment)");
        Abxr.LogInfo("Assessment started", new Abxr.Dict().With("assessment", "Assessment_" + randomName));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: item_grabbed (meta: item_type=tool, item_value=hammer)");
        Abxr.Event("item_grabbed", new Abxr.Dict().With("item_type", "tool").With("item_value", "hammer"), sendTelemetry: false);
        Logcat.Info("(Test) Event: workstation_approached position=(1.2,0,-0.5) meta: station=bench_1");
        Abxr.Event("workstation_approached", new Vector3(1.2f, 0f, -0.5f), new Abxr.Dict().With("station", "bench_1"));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: " + objectiveName);
        Abxr.EventObjectiveStart(objectiveName);
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) EventInteractionStart: select_option_a");
        Abxr.EventInteractionStart("select_option_a");
        yield return new WaitForSeconds(0.3f);
        Logcat.Info("(Test) EventInteractionComplete: select_option_a Select Correct \"true\"");
        Abxr.EventInteractionComplete("select_option_a", Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "true");
        Logcat.Info("(Test) StartTimedEvent: objective_task_" + randomName);
        Abxr.StartTimedEvent("objective_task_" + randomName);
        yield return new WaitForSeconds(0.25f);
        Logcat.Info("(Test) Event: objective_task_" + randomName + " (duration from StartTimedEvent)");
        Abxr.Event("objective_task_" + randomName, new Abxr.Dict().With("objective", objectiveName), sendTelemetry: false);
        int objectiveScore = Random.Range(80, 200);
        Logcat.Info("(Test) EventObjectiveComplete: " + objectiveName + " score=" + objectiveScore + " Pass");
        Abxr.EventObjectiveComplete(objectiveName, objectiveScore, Abxr.EventStatus.Pass);
        Logcat.Info("(Test) Log Debug: Objective completed (meta: score)");
        Abxr.Log("Objective completed", Abxr.LogLevel.Debug, new Abxr.Dict().With("score", objectiveScore.ToString()));
        yield return new WaitForSeconds(0.2f);

        int assessmentScore = Random.Range(80, 200);
        Logcat.Info("(Test) EventAssessmentComplete: Assessment_" + randomName + " score=" + assessmentScore + " Pass (meta: max_score=100)");
        Abxr.EventAssessmentComplete("Assessment_" + randomName, assessmentScore, Abxr.EventStatus.Pass, new Abxr.Dict().With("max_score", "100"));
    }

    [UnityTest]
    public IEnumerator NoAssessmentWithEventAndObjective_Scenario()
    {
        // Narrative: Freeform exploration – menu events, option selected, objective started (no assessment wrapper), positions, logs; session ends without assessment complete.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed for this scenario.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string objectiveName = "explore_zone_" + tag;

        Logcat.Info("(Test) Event: menu_opened (meta: menu=content_picker)");
        Abxr.Event("menu_opened", new Abxr.Dict().With("menu", "content_picker"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) Event: option_selected (meta: option=zone_tour, item_value=2)");
        Abxr.Event("option_selected", new Abxr.Dict().With("option", "zone_tour").With("item_value", "2"), sendTelemetry: false);
        Logcat.Info("(Test) LogInfo: User selected zone tour");
        Abxr.LogInfo("User selected zone tour", new Abxr.Dict().With("option", "zone_tour"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) EventObjectiveStart: " + objectiveName + " (no assessment wrapper)");
        Abxr.EventObjectiveStart(objectiveName);
        Logcat.Info("(Test) Event: zone_entered position=(0,0,3) meta: zone=warehouse_a");
        Abxr.Event("zone_entered", new Vector3(0f, 0f, 3f), new Abxr.Dict().With("zone", "warehouse_a"));
        yield return new WaitForSeconds(0.25f);
        Logcat.Info("(Test) Event: item_inspected (meta: item_type=door, item_value=safety_exit)");
        Abxr.Event("item_inspected", new Abxr.Dict().With("item_type", "door").With("item_value", "safety_exit"), sendTelemetry: false);
        Logcat.Info("(Test) LogDebug: Exploration in progress");
        Abxr.LogDebug("Exploration in progress", new Abxr.Dict().With("objective", objectiveName));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: viewer_position position=(1.5,1.6,2) meta: event=mid_tour");
        Abxr.Event("viewer_position", new Vector3(1.5f, 1.6f, 2f), new Abxr.Dict().With("event", "mid_tour"));
        Logcat.Info("(Test) (Objective not completed – tearDown will run quit handler)");
        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator ExperienceOnly_NoScore_Scenario()
    {
        // Narrative: User chooses scene/video, we log load success, track positions and watch duration,
        // then a second video fails to load (critical event) and we complete the experience.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string experienceName = "video_gallery_" + tag;

        Logcat.Info("(Test) EventExperienceStart: " + experienceName);
        Abxr.EventExperienceStart(experienceName);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: content_selected (meta: content_type=video, content_id=safety_intro_01, source=gallery)");
        Abxr.Event("content_selected", new Abxr.Dict().With("content_type", "video").With("content_id", "safety_intro_01").With("source", "gallery"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) LogInfo: Loading experience content (meta: content_id)");
        Abxr.LogInfo("Loading experience content", new Abxr.Dict().With("content_id", "safety_intro_01"));
        yield return new WaitForSeconds(0.3f);
        Logcat.Info("(Test) Log Info: Experience loaded successfully (meta: duration_ms, format)");
        Abxr.Log("Experience loaded successfully", Abxr.LogLevel.Info, new Abxr.Dict().With("duration_ms", "420").With("format", "360"));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: viewer_position position=(0,1.6,-2) meta: event=playback_started");
        Abxr.Event("viewer_position", new Vector3(0f, 1.6f, -2f), new Abxr.Dict().With("event", "playback_started"));
        yield return new WaitForSeconds(0.25f);
        Logcat.Info("(Test) Event: viewer_position position=(0.2,1.55,-1.9) meta: event=mid_playback");
        Abxr.Event("viewer_position", new Vector3(0.2f, 1.55f, -1.9f), new Abxr.Dict().With("event", "mid_playback"));
        yield return new WaitForSeconds(0.25f);

        Logcat.Info("(Test) StartTimedEvent: video_watch_safety_intro_01");
        Abxr.StartTimedEvent("video_watch_safety_intro_01");
        yield return new WaitForSeconds(0.5f);
        Logcat.Info("(Test) Event: video_watch_safety_intro_01 (duration from StartTimedEvent, meta: content_id, completed)");
        Abxr.Event("video_watch_safety_intro_01", new Abxr.Dict().With("content_id", "safety_intro_01").With("completed", "true"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: video_play_requested (meta: content_id=safety_advanced_02)");
        Abxr.Event("video_play_requested", new Abxr.Dict().With("content_id", "safety_advanced_02").With("source", "gallery"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) EventCritical: video_load_failed (meta: content_id, error_code=STREAM_TIMEOUT)");
        Abxr.EventCritical("video_load_failed", new Abxr.Dict().With("content_id", "safety_advanced_02").With("error_code", "STREAM_TIMEOUT"));
        Logcat.Info("(Test) LogError: Second video failed to load");
        Abxr.LogError("Second video failed to load", new Abxr.Dict().With("content_id", "safety_advanced_02").With("reason", "stream_timeout"));
        yield return new WaitForSeconds(0.1f);
        Logcat.Info("(Test) EventExperienceComplete: " + experienceName);
        Abxr.EventExperienceComplete(experienceName);

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator AssessmentFail_Scenario()
    {
        // Narrative: Quiz with multiple questions, some correct/incorrect interactions, logs, timed per question; final score fails.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "safety_quiz_" + tag;

        Logcat.Info("(Test) LogInfo: Quiz started");
        Abxr.LogInfo("Quiz started", new Abxr.Dict().With("assessment", assessmentName));
        Logcat.Info("(Test) EventAssessmentStart: " + assessmentName);
        Abxr.EventAssessmentStart(assessmentName);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: q1_ppe_required (Q1)");
        Abxr.EventObjectiveStart("q1_ppe_required_" + tag);
        Logcat.Info("(Test) EventInteractionStart: select_ppe_answer");
        Abxr.EventInteractionStart("select_ppe_answer_" + tag);
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) EventInteractionComplete: select_ppe_answer Select Correct \"gloves_and_goggles\"");
        Abxr.EventInteractionComplete("select_ppe_answer_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "gloves_and_goggles");
        Logcat.Info("(Test) EventObjectiveComplete: q1 score=100 Pass");
        Abxr.EventObjectiveComplete("q1_ppe_required_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) EventObjectiveStart: q2_emergency_exit (Q2)");
        Abxr.EventObjectiveStart("q2_emergency_exit_" + tag);
        Logcat.Info("(Test) EventInteractionStart: select_exit_answer");
        Abxr.EventInteractionStart("select_exit_answer_" + tag);
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) EventInteractionComplete: select_exit_answer Select Incorrect \"nearest_door\"");
        Abxr.EventInteractionComplete("select_exit_answer_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Incorrect, "nearest_door");
        Logcat.Info("(Test) EventObjectiveComplete: q2 score=0 Fail");
        Abxr.EventObjectiveComplete("q2_emergency_exit_" + tag, 0, Abxr.EventStatus.Fail);
        Logcat.Info("(Test) LogWarn: Incorrect answer recorded");
        Abxr.LogWarn("Incorrect answer recorded", new Abxr.Dict().With("objective", "q2_emergency_exit_" + tag));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: q3_fire_extinguisher (Q3)");
        Abxr.EventObjectiveStart("q3_fire_extinguisher_" + tag);
        Logcat.Info("(Test) StartTimedEvent: q3_time_" + tag);
        Abxr.StartTimedEvent("q3_time_" + tag);
        yield return new WaitForSeconds(0.35f);
        Logcat.Info("(Test) Event: q3_time (duration from StartTimedEvent)");
        Abxr.Event("q3_time_" + tag, new Abxr.Dict().With("objective", "q3_fire_extinguisher_" + tag), sendTelemetry: false);
        Logcat.Info("(Test) EventInteractionComplete: select_extinguisher Select Incorrect \"class_a_only\"");
        Abxr.EventInteractionComplete("select_extinguisher_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Incorrect, "class_a_only");
        Logcat.Info("(Test) EventObjectiveComplete: q3 score=0 Fail");
        Abxr.EventObjectiveComplete("q3_fire_extinguisher_" + tag, 0, Abxr.EventStatus.Fail);
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) Log Info: Assessment complete – below passing threshold (meta: score=25)");
        Abxr.Log("Assessment complete – below passing threshold", Abxr.LogLevel.Info, new Abxr.Dict().With("score", "25"));
        Logcat.Info("(Test) EventAssessmentComplete: " + assessmentName + " score=25 Fail");
        Abxr.EventAssessmentComplete(assessmentName, 25, Abxr.EventStatus.Fail);
    }

    [UnityTest]
    public IEnumerator AssessmentIncomplete_Scenario()
    {
        // Narrative: User starts training, completes one objective, logs progress, then leaves/times out without finishing (incomplete).
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "equipment_training_" + tag;

        Logcat.Info("(Test) EventAssessmentStart: " + assessmentName);
        Abxr.EventAssessmentStart(assessmentName);
        Logcat.Info("(Test) LogInfo: Training module loaded");
        Abxr.LogInfo("Training module loaded", new Abxr.Dict().With("module", assessmentName));
        yield return new WaitForSeconds(0.25f);

        Logcat.Info("(Test) EventObjectiveStart: intro_video");
        Abxr.EventObjectiveStart("intro_video_" + tag);
        Logcat.Info("(Test) Event: video_started (meta: video_id=intro)");
        Abxr.Event("video_started", new Abxr.Dict().With("video_id", "intro").With("objective", "intro_video_" + tag), sendTelemetry: false);
        yield return new WaitForSeconds(0.3f);
        Logcat.Info("(Test) EventObjectiveComplete: intro_video score=100 Complete");
        Abxr.EventObjectiveComplete("intro_video_" + tag, 100, Abxr.EventStatus.Complete);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: hands_on_drill (user will not complete)");
        Abxr.EventObjectiveStart("hands_on_drill_" + tag);
        Logcat.Info("(Test) Log Debug: User entered hands-on section");
        Abxr.Log("User entered hands-on section", Abxr.LogLevel.Debug, new Abxr.Dict().With("objective", "hands_on_drill_" + tag));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: user_position position=(1,0,2) meta: section=workstation_a");
        Abxr.Event("user_position", new Vector3(1f, 0f, 2f), new Abxr.Dict().With("section", "workstation_a"));
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) LogWarn: Session ended without completing objective");
        Abxr.LogWarn("Session ended without completing objective", new Abxr.Dict().With("objective", "hands_on_drill_" + tag));

        Logcat.Info("(Test) EventAssessmentComplete: " + assessmentName + " score=0 Incomplete");
        Abxr.EventAssessmentComplete(assessmentName, 0, Abxr.EventStatus.Incomplete);
    }

    [UnityTest]
    public IEnumerator BasicEventWithMetadata_Scenario()
    {
        // Narrative: User navigates menu, collects items, moves to different areas; multiple events with metadata and positions.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);

        Logcat.Info("(Test) Event: button_pressed (meta: screen=main_menu, button=start)");
        Abxr.Event("button_pressed", new Abxr.Dict().With("screen", "main_menu").With("button", "start"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) Event: scene_entered (meta: scene_id=warehouse, entry_point=spawn_a)");
        Abxr.Event("scene_entered", new Abxr.Dict().With("scene_id", "warehouse").With("entry_point", "spawn_a"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: item_collected (meta: item_type=coin, item_value=100, location=shelf_1)");
        Abxr.Event("item_collected", new Abxr.Dict().With("item_type", "coin").With("item_value", "100").With("location", "shelf_1"), sendTelemetry: false);
        Logcat.Info("(Test) Event: item_collected (meta: item_type=tool, item_value=wrench, location=bench_2)");
        Abxr.Event("item_collected", new Abxr.Dict().With("item_type", "tool").With("item_value", "wrench").With("location", "bench_2"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: player_teleported position=(2,0,-1) meta: destination=checkpoint_b");
        Abxr.Event("player_teleported", new Vector3(2f, 0f, -1f), new Abxr.Dict().With("destination", "checkpoint_b"));
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) Event: button_pressed (meta: screen=inventory, button=use_item)");
        Abxr.Event("button_pressed", new Abxr.Dict().With("screen", "inventory").With("button", "use_item"), sendTelemetry: false);
        Logcat.Info("(Test) LogInfo: User completed inventory action");
        Abxr.LogInfo("User completed inventory action", new Abxr.Dict().With("action", "use_item"));

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator TimedAssessment_Scenario()
    {
        // Narrative: Timed exam – total time tracked, multiple questions with logs; completion with duration on assessment complete.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "timed_exam_" + tag;

        Logcat.Info("(Test) StartTimedEvent: " + assessmentName + " (duration will attach to EventAssessmentComplete)");
        Abxr.StartTimedEvent(assessmentName);
        Logcat.Info("(Test) EventAssessmentStart: " + assessmentName);
        Abxr.EventAssessmentStart(assessmentName);
        Logcat.Info("(Test) LogInfo: Timed exam started");
        Abxr.LogInfo("Timed exam started", new Abxr.Dict().With("assessment", assessmentName));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: question_1");
        Abxr.EventObjectiveStart("question_1_" + tag);
        Logcat.Info("(Test) EventInteractionComplete: answer_1 Select Correct \"a\"");
        Abxr.EventInteractionComplete("answer_1_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "a");
        Logcat.Info("(Test) EventObjectiveComplete: question_1 score=100 Pass");
        Abxr.EventObjectiveComplete("question_1_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.25f);
        Logcat.Info("(Test) EventObjectiveStart: question_2");
        Abxr.EventObjectiveStart("question_2_" + tag);
        Logcat.Info("(Test) EventInteractionComplete: answer_2 Select Correct \"b\"");
        Abxr.EventInteractionComplete("answer_2_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "b");
        Logcat.Info("(Test) EventObjectiveComplete: question_2 score=100 Pass");
        Abxr.EventObjectiveComplete("question_2_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.3f);
        Logcat.Info("(Test) Log Info: Timed exam completed (meta: score=88)");
        Abxr.Log("Timed exam completed", Abxr.LogLevel.Info, new Abxr.Dict().With("score", "88"));
        Logcat.Info("(Test) EventAssessmentComplete: " + assessmentName + " score=88 Pass (includes duration from StartTimedEvent)");
        Abxr.EventAssessmentComplete(assessmentName, 88, Abxr.EventStatus.Pass);
    }

    [UnityTest]
    public IEnumerator LevelTracking_Scenario()
    {
        // Narrative: Multi-level run – level start/complete, waypoint positions, logs, timed level, critical violation on level 2 then recovery and complete.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Logcat.Info("(Test) EventLevelStart: level_1_" + tag);
        Abxr.EventLevelStart("level_1_" + tag);
        Logcat.Info("(Test) LogInfo: Level 1 loaded");
        Abxr.LogInfo("Level 1 loaded", new Abxr.Dict().With("level", "level_1_" + tag));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: waypoint_reached position=(0,0,5) meta: waypoint=checkpoint_a");
        Abxr.Event("waypoint_reached", new Vector3(0f, 0f, 5f), new Abxr.Dict().With("waypoint", "checkpoint_a"));
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) Event: waypoint_reached position=(2,0,8) meta: waypoint=checkpoint_b");
        Abxr.Event("waypoint_reached", new Vector3(2f, 0f, 8f), new Abxr.Dict().With("waypoint", "checkpoint_b"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) EventLevelComplete: level_1_" + tag + " score=85");
        Abxr.EventLevelComplete("level_1_" + tag, 85);
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) EventLevelStart: level_2_" + tag);
        Abxr.EventLevelStart("level_2_" + tag);
        Logcat.Info("(Test) StartTimedEvent: level_2_duration_" + tag);
        Abxr.StartTimedEvent("level_2_duration_" + tag);
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: waypoint_reached position=(1,1,0) meta: waypoint=start");
        Abxr.Event("waypoint_reached", new Vector3(1f, 1f, 0f), new Abxr.Dict().With("waypoint", "start"));
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) Event: user_entered_zone position=(-3,0,0) meta: zone=restricted");
        Abxr.Event("user_entered_zone", new Vector3(-3f, 0f, 0f), new Abxr.Dict().With("zone", "restricted"));
        Logcat.Info("(Test) EventCritical: safety_violation (meta: zone=machine_room, level)");
        Abxr.EventCritical("safety_violation", new Abxr.Dict().With("zone", "machine_room").With("level", "level_2_" + tag));
        Logcat.Info("(Test) LogWarn: Restricted zone entered");
        Abxr.LogWarn("Restricted zone entered", new Abxr.Dict().With("zone", "restricted"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: level_2_duration (duration from StartTimedEvent)");
        Abxr.Event("level_2_duration_" + tag, new Abxr.Dict().With("level", "level_2_" + tag), sendTelemetry: false);
        Logcat.Info("(Test) EventLevelComplete: level_2_" + tag + " score=70");
        Abxr.EventLevelComplete("level_2_" + tag, 70);
        Logcat.Info("(Test) LogInfo: Level 2 completed with penalty");
        Abxr.LogInfo("Level 2 completed with penalty", new Abxr.Dict().With("score", "70"));

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator SuperMetadataRegister_Scenario()
    {
        // Narrative: Set super metadata (Register/RegisterOnce), then run a short flow – events, log, position – all get the super meta.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        Logcat.Info("(Test) Register: user_type=premium, app_version=1.2.3 (super metadata on all subsequent events/logs)");
        Abxr.Register("user_type", "premium");
        Abxr.Register("app_version", "1.2.3");
        Logcat.Info("(Test) RegisterOnce: user_tier=free (set only if not already set)");
        Abxr.RegisterOnce("user_tier", "free");
        yield return new WaitForSeconds(0.1f);

        Logcat.Info("(Test) Event: session_started (meta: source=main_menu)");
        Abxr.Event("session_started", new Abxr.Dict().With("source", "main_menu"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Logcat.Info("(Test) LogInfo: Premium session active");
        Abxr.LogInfo("Premium session active", new Abxr.Dict().With("feature", "extended_content"));
        Logcat.Info("(Test) Event: content_selected position=(0,1.5,-2) meta: content_id=premium_video_1");
        Abxr.Event("content_selected", new Vector3(0f, 1.5f, -2f), new Abxr.Dict().With("content_id", "premium_video_1"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Event: playback_started (meta: content_id=premium_video_1)");
        Abxr.Event("playback_started", new Abxr.Dict().With("content_id", "premium_video_1"), sendTelemetry: false);

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator StorageSetAndGet_User_Scenario()
    {
        // Narrative: Progress through checkpoints, save/load progress via user-scoped storage. Clear device-scoped data first so we don't get device leftovers when GET user.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        // Use a unique userId per test run so user-scope storage is isolated.
        string testUserId = "user-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        Abxr.SetUserData(testUserId);
        yield return new WaitForSeconds(0.1f);

        Logcat.Info("(Test) Clear device-scoped storage so user-scope GET does not see leftover device data.");
        Abxr.StorageRemoveMultipleEntries(Abxr.StorageScope.Device);
        yield return new WaitForSeconds(1.5f);

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Logcat.Info("(Test) LogInfo: Training session started");
        Abxr.LogInfo("Training session started", new Abxr.Dict().With("session", tag));
        Logcat.Info("(Test) Event: checkpoint_reached (meta: checkpoint=intro, progress=25%)");
        Abxr.Event("checkpoint_reached", new Abxr.Dict().With("checkpoint", "intro").With("progress", "25%"), sendTelemetry: false);
        Logcat.Info("(Test) StorageSetEntry: name=state scope=User (user_progress=25%, user_last_checkpoint=intro)");
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("user_progress", "25%").With("user_last_checkpoint", "intro"), Abxr.StorageScope.User);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: checkpoint_reached position=(1,0,2) meta: checkpoint=mid");
        Abxr.Event("checkpoint_reached", new Vector3(1f, 0f, 2f), new Abxr.Dict().With("checkpoint", "mid"));
        Logcat.Info("(Test) StorageSetEntry: name=state scope=User (user_progress=75%, user_last_checkpoint=mid)");
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("user_progress", "75%").With("user_last_checkpoint", "mid"), Abxr.StorageScope.User);
        Logcat.Info("(Test) Log Debug: Progress saved (meta: user_progress=75%)");
        Abxr.Log("Progress saved", Abxr.LogLevel.Debug, new Abxr.Dict().With("user_progress", "75%"));
        yield return new WaitForSeconds(0.15f);

        // Flush queued storage so the POST is sent before we GET (REST transport batches; ForceSend triggers send on next tick).
        Logcat.Info("(Test) ForceSend storage/events, then wait 1.5s for backend to persist. (If no Storage POST log appears, user-scope add was skipped due to no userId.)");
        AbxrSubsystem.Instance.GetTransportForTesting()?.ForceSend();
        yield return new WaitForSeconds(1.5f);

        // StorageGetEntry is async (network I/O); it takes a callback and returns an IEnumerator. We start it as a coroutine and wait for the callback.
        Logcat.Info("(Test) StorageGetEntry: name=state scope=User (async, wait for callback)");
        List<Dictionary<string, string>> results = null;
        bool getDone = false;
        AbxrSubsystem.Instance.StartCoroutine(Abxr.StorageGetEntry("state", Abxr.StorageScope.User, r =>
        {
            results = r;
            getDone = true;
            Logcat.Info("(Test) StorageGetEntry callback: results=" + (r == null ? "null" : "List.Count=" + r.Count.ToString()));
            if (r != null && r.Count > 0)
            {
                var first = r[0];
                var keys = first != null ? string.Join(", ", first.Keys) : "(null)";
                Logcat.Info("(Test) StorageGetEntry first entry keys: " + keys);
            }
        }));
        yield return new WaitUntil(() => getDone);
        Logcat.Info("(Test) StorageGetEntry completed: results " + (results == null ? "null" : "Count=" + results.Count.ToString()));
        Assert.IsNotNull(results, "StorageGetEntry callback should be invoked (null = request failed or error).");
        Assert.Greater(results.Count, 0, "Backend should return at least one entry for 'state' (scope=User); ensure backend is running and storage POSTs completed (check Storage POST logs).");
        Logcat.Info("(Test) StorageGetEntry returned " + results.Count + " entry/entries.");
        Logcat.Info("(Test) LogInfo: Progress loaded for resume");
        Abxr.LogInfo("Progress loaded for resume", new Abxr.Dict().With("entries", results.Count.ToString()));

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator StorageSetAndGet_Device_Scenario()
    {
        // Narrative: Device-scoped storage: save/load state keyed by device (no userId). Same flow as StorageSetAndGet_Scenario but scope=Device.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Logcat.Info("(Test) LogInfo: Device storage session started");
        Abxr.LogInfo("Device storage session started", new Abxr.Dict().With("session", tag));
        Logcat.Info("(Test) Event: device_checkpoint (meta: checkpoint=intro, device_progress=25%)");
        Abxr.Event("device_checkpoint", new Abxr.Dict().With("checkpoint", "intro").With("device_progress", "25%"), sendTelemetry: false);
        Logcat.Info("(Test) StorageSetEntry: name=state scope=Device (device_progress=25%, device_last_checkpoint=intro)");
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("device_progress", "25%").With("device_last_checkpoint", "intro"), Abxr.StorageScope.Device);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) Event: device_checkpoint position=(1,0,2) meta: checkpoint=mid");
        Abxr.Event("device_checkpoint", new Vector3(1f, 0f, 2f), new Abxr.Dict().With("checkpoint", "mid"));
        Logcat.Info("(Test) StorageSetEntry: name=state scope=Device (device_progress=75%, device_last_checkpoint=mid)");
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("device_progress", "75%").With("device_last_checkpoint", "mid"), Abxr.StorageScope.Device);
        Logcat.Info("(Test) Log Debug: Device progress saved (meta: device_progress=75%)");
        Abxr.Log("Device progress saved", Abxr.LogLevel.Debug, new Abxr.Dict().With("device_progress", "75%"));
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) ForceSend storage/events, then wait 1.5s for backend to persist.");
        AbxrSubsystem.Instance.GetTransportForTesting()?.ForceSend();
        yield return new WaitForSeconds(1.5f);

        Logcat.Info("(Test) StorageGetEntry: name=state scope=Device (async, wait for callback)");
        List<Dictionary<string, string>> results = null;
        bool getDone = false;
        AbxrSubsystem.Instance.StartCoroutine(Abxr.StorageGetEntry("state", Abxr.StorageScope.Device, r =>
        {
            results = r;
            getDone = true;
            Logcat.Info("(Test) StorageGetEntry callback: results=" + (r == null ? "null" : "List.Count=" + r.Count.ToString()));
            if (r != null && r.Count > 0)
            {
                var first = r[0];
                var keys = first != null ? string.Join(", ", first.Keys) : "(null)";
                Logcat.Info("(Test) StorageGetEntry first entry keys: " + keys);
            }
        }));
        yield return new WaitUntil(() => getDone);
        Logcat.Info("(Test) StorageGetEntry completed: results " + (results == null ? "null" : "Count=" + results.Count.ToString()));
        Assert.IsNotNull(results, "StorageGetEntry callback should be invoked (null = request failed or error).");
        Assert.Greater(results.Count, 0, "Backend should return at least one entry for 'state' (scope=Device); ensure backend is running and storage POSTs completed.");
        Logcat.Info("(Test) StorageGetEntry returned " + results.Count + " entry/entries (device scope).");
        Logcat.Info("(Test) LogInfo: Device progress loaded for resume");
        Abxr.LogInfo("Device progress loaded for resume", new Abxr.Dict().With("entries", results.Count.ToString()));

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator Telemetry_Scenario()
    {
        // Narrative: Simulated session with repeated telemetry (position, frame rate), events and logs.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Logcat.Info("(Test) Event: session_phase (meta: phase=calibration)");
        Abxr.Event("session_phase", new Abxr.Dict().With("phase", "calibration"), sendTelemetry: false);
        Logcat.Info("(Test) Telemetry: headset_position (x=0, y=1.6, z=0)");
        Abxr.Telemetry("headset_position", new Abxr.Dict().With("x", "0").With("y", "1.6").With("z", "0"));
        Logcat.Info("(Test) Telemetry: frame_rate (fps=72, target=72)");
        Abxr.Telemetry("frame_rate", new Abxr.Dict().With("fps", "72").With("target", "72"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) LogInfo: Calibration complete");
        Abxr.LogInfo("Calibration complete", new Abxr.Dict().With("session", tag));
        Logcat.Info("(Test) Telemetry: headset_position (x=1.23, y=4.56, z=7.89)");
        Abxr.Telemetry("headset_position", new Abxr.Dict().With("x", "1.23").With("y", "4.56").With("z", "7.89"));
        Logcat.Info("(Test) Event: user_moved position=(1.23,4.56,7.89) meta: event=telemetry_sync");
        Abxr.Event("user_moved", new Vector3(1.23f, 4.56f, 7.89f), new Abxr.Dict().With("event", "telemetry_sync"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) Telemetry: battery_level (percent=78, charging=false)");
        Abxr.Telemetry("battery_level", new Abxr.Dict().With("percent", "78").With("charging", "false"));
        Logcat.Info("(Test) Event: telemetry_batch_sent (meta: samples=3)");
        Abxr.Event("telemetry_batch_sent", new Abxr.Dict().With("samples", "3"), sendTelemetry: false);

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator MultipleObjectivesInAssessment_Scenario()
    {
        // Narrative: Course with multiple objectives – open/close valve, logs, position events between objectives, one objective timed, assessment complete with meta.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");
        Logcat.Info("(Test) Auth succeeded.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "course_" + tag;

        Logcat.Info("(Test) EventAssessmentStart: " + assessmentName + " (meta: module=safety)");
        Abxr.EventAssessmentStart(assessmentName, new Abxr.Dict().With("module", "safety"));
        Logcat.Info("(Test) LogInfo: Course started");
        Abxr.LogInfo("Course started", new Abxr.Dict().With("assessment", assessmentName));
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventObjectiveStart: open_valve_" + tag);
        Abxr.EventObjectiveStart("open_valve_" + tag);
        Logcat.Info("(Test) Event: valve_interaction position=(0.5,1,1.5) meta: action=approach");
        Abxr.Event("valve_interaction", new Vector3(0.5f, 1f, 1.5f), new Abxr.Dict().With("action", "approach"));
        yield return new WaitForSeconds(0.2f);
        Logcat.Info("(Test) EventInteractionComplete: valve_turn Performance Correct \"open\"");
        Abxr.EventInteractionComplete("valve_turn_" + tag, Abxr.InteractionType.Performance, Abxr.InteractionResult.Correct, "open");
        Logcat.Info("(Test) EventObjectiveComplete: open_valve score=100 Complete");
        Abxr.EventObjectiveComplete("open_valve_" + tag, 100, Abxr.EventStatus.Complete);
        Logcat.Info("(Test) Log Debug: Objective open_valve completed");
        Abxr.Log("Objective open_valve completed", Abxr.LogLevel.Debug, new Abxr.Dict().With("objective", "open_valve_" + tag));
        yield return new WaitForSeconds(0.15f);

        Logcat.Info("(Test) EventObjectiveStart: close_valve_" + tag);
        Abxr.EventObjectiveStart("close_valve_" + tag);
        Logcat.Info("(Test) StartTimedEvent: close_valve_time_" + tag);
        Abxr.StartTimedEvent("close_valve_time_" + tag);
        yield return new WaitForSeconds(0.25f);
        Logcat.Info("(Test) Event: close_valve_time (duration from StartTimedEvent)");
        Abxr.Event("close_valve_time_" + tag, new Abxr.Dict().With("objective", "close_valve_" + tag), sendTelemetry: false);
        Logcat.Info("(Test) Event: valve_interaction position=(0.5,1,1.5) meta: action=close");
        Abxr.Event("valve_interaction", new Vector3(0.5f, 1f, 1.5f), new Abxr.Dict().With("action", "close"));
        Logcat.Info("(Test) EventObjectiveComplete: close_valve score=100 Complete");
        Abxr.EventObjectiveComplete("close_valve_" + tag, 100, Abxr.EventStatus.Complete);
        yield return new WaitForSeconds(0.2f);

        Logcat.Info("(Test) EventAssessmentComplete: " + assessmentName + " score=95 Pass (meta: max_score=100)");
        Abxr.EventAssessmentComplete(assessmentName, 95, Abxr.EventStatus.Pass, new Abxr.Dict().With("max_score", "100"));
    }
}

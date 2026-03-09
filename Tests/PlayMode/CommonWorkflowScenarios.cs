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

        yield return new WaitForSeconds(1f);
        string randomName = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string objectiveName = "Objective_" + randomName + "_0";

        Abxr.EventAssessmentStart("Assessment_" + randomName);
        Abxr.LogInfo("Assessment started", new Abxr.Dict().With("assessment", "Assessment_" + randomName));
        yield return new WaitForSeconds(0.2f);

        Abxr.Event("item_grabbed", new Abxr.Dict().With("item_type", "tool").With("item_value", "hammer"), sendTelemetry: false);
        Abxr.Event("workstation_approached", new Vector3(1.2f, 0f, -0.5f), new Abxr.Dict().With("station", "bench_1"));
        yield return new WaitForSeconds(0.2f);

        Abxr.EventObjectiveStart(objectiveName);
        yield return new WaitForSeconds(0.15f);
        Abxr.EventInteractionStart("select_option_a");
        yield return new WaitForSeconds(0.3f);
        Abxr.EventInteractionComplete("select_option_a", Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "true");
        Abxr.StartTimedEvent("objective_task_" + randomName);
        yield return new WaitForSeconds(0.25f);
        Abxr.Event("objective_task_" + randomName, new Abxr.Dict().With("objective", objectiveName), sendTelemetry: false);
        int objectiveScore = Random.Range(80, 101);
        Abxr.EventObjectiveComplete(objectiveName, objectiveScore, Abxr.EventStatus.Pass);
        Abxr.Log("Objective completed", Abxr.LogLevel.Debug, new Abxr.Dict().With("score", objectiveScore.ToString()));
        yield return new WaitForSeconds(0.2f);

        int assessmentScore = Random.Range(80, 101);
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string objectiveName = "explore_zone_" + tag;

        Abxr.Event("menu_opened", new Abxr.Dict().With("menu", "content_picker"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Abxr.Event("option_selected", new Abxr.Dict().With("option", "zone_tour").With("item_value", "2"), sendTelemetry: false);
        Abxr.LogInfo("User selected zone tour", new Abxr.Dict().With("option", "zone_tour"));
        yield return new WaitForSeconds(0.2f);
        Abxr.EventObjectiveStart(objectiveName);
        Abxr.Event("zone_entered", new Vector3(0f, 0f, 3f), new Abxr.Dict().With("zone", "warehouse_a"));
        yield return new WaitForSeconds(0.25f);
        Abxr.Event("item_inspected", new Abxr.Dict().With("item_type", "door").With("item_value", "safety_exit"), sendTelemetry: false);
        Abxr.LogDebug("Exploration in progress", new Abxr.Dict().With("objective", objectiveName));
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("viewer_position", new Vector3(1.5f, 1.6f, 2f), new Abxr.Dict().With("event", "mid_tour"));
        // Objective not completed – user exits or app closes; quit handler sends pending data.
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string experienceName = "video_gallery_" + tag;

        Abxr.EventExperienceStart(experienceName);
        yield return new WaitForSeconds(0.2f);

        // User selects which scene/video to experience
        Abxr.Event("content_selected", new Abxr.Dict().With("content_type", "video").With("content_id", "safety_intro_01").With("source", "gallery"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);

        // Logs: loading and load success
        Abxr.LogInfo("Loading experience content", new Abxr.Dict().With("content_id", "safety_intro_01"));
        yield return new WaitForSeconds(0.3f);
        Abxr.Log("Experience loaded successfully", Abxr.LogLevel.Info, new Abxr.Dict().With("duration_ms", "420").With("format", "360"));
        yield return new WaitForSeconds(0.2f);

        // Events with positions: user moves / gazes during playback
        Abxr.Event("viewer_position", new Vector3(0f, 1.6f, -2f), new Abxr.Dict().With("event", "playback_started"));
        yield return new WaitForSeconds(0.25f);
        Abxr.Event("viewer_position", new Vector3(0.2f, 1.55f, -1.9f), new Abxr.Dict().With("event", "mid_playback"));
        yield return new WaitForSeconds(0.25f);

        // Timed event: how long they watch this scene/video
        Abxr.StartTimedEvent("video_watch_safety_intro_01");
        yield return new WaitForSeconds(0.5f);
        Abxr.Event("video_watch_safety_intro_01", new Abxr.Dict().With("content_id", "safety_intro_01").With("completed", "true"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);

        // User tries to play a second video
        Abxr.Event("video_play_requested", new Abxr.Dict().With("content_id", "safety_advanced_02").With("source", "gallery"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);

        // Second video fails to load -> critical event, then complete experience (early exit)
        Abxr.EventCritical("video_load_failed", new Abxr.Dict().With("content_id", "safety_advanced_02").With("error_code", "STREAM_TIMEOUT"));
        Abxr.LogError("Second video failed to load", new Abxr.Dict().With("content_id", "safety_advanced_02").With("reason", "stream_timeout"));
        yield return new WaitForSeconds(0.1f);
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "safety_quiz_" + tag;

        Abxr.LogInfo("Quiz started", new Abxr.Dict().With("assessment", assessmentName));
        Abxr.EventAssessmentStart(assessmentName);
        yield return new WaitForSeconds(0.2f);

        // Question 1
        Abxr.EventObjectiveStart("q1_ppe_required_" + tag);
        Abxr.EventInteractionStart("select_ppe_answer_" + tag);
        yield return new WaitForSeconds(0.2f);
        Abxr.EventInteractionComplete("select_ppe_answer_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "gloves_and_goggles");
        Abxr.EventObjectiveComplete("q1_ppe_required_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.15f);

        // Question 2 – wrong answer
        Abxr.EventObjectiveStart("q2_emergency_exit_" + tag);
        Abxr.EventInteractionStart("select_exit_answer_" + tag);
        yield return new WaitForSeconds(0.2f);
        Abxr.EventInteractionComplete("select_exit_answer_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Incorrect, "nearest_door");
        Abxr.EventObjectiveComplete("q2_emergency_exit_" + tag, 0, Abxr.EventStatus.Fail);
        Abxr.LogWarn("Incorrect answer recorded", new Abxr.Dict().With("objective", "q2_emergency_exit_" + tag));
        yield return new WaitForSeconds(0.2f);

        // Question 3 – timed
        Abxr.EventObjectiveStart("q3_fire_extinguisher_" + tag);
        Abxr.StartTimedEvent("q3_time_" + tag);
        yield return new WaitForSeconds(0.35f);
        Abxr.Event("q3_time_" + tag, new Abxr.Dict().With("objective", "q3_fire_extinguisher_" + tag), sendTelemetry: false);
        Abxr.EventInteractionComplete("select_extinguisher_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Incorrect, "class_a_only");
        Abxr.EventObjectiveComplete("q3_fire_extinguisher_" + tag, 0, Abxr.EventStatus.Fail);
        yield return new WaitForSeconds(0.15f);

        Abxr.Log("Assessment complete – below passing threshold", Abxr.LogLevel.Info, new Abxr.Dict().With("score", "25"));
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "equipment_training_" + tag;

        Abxr.EventAssessmentStart(assessmentName);
        Abxr.LogInfo("Training module loaded", new Abxr.Dict().With("module", assessmentName));
        yield return new WaitForSeconds(0.25f);

        Abxr.EventObjectiveStart("intro_video_" + tag);
        Abxr.Event("video_started", new Abxr.Dict().With("video_id", "intro").With("objective", "intro_video_" + tag), sendTelemetry: false);
        yield return new WaitForSeconds(0.3f);
        Abxr.EventObjectiveComplete("intro_video_" + tag, 100, Abxr.EventStatus.Complete);
        yield return new WaitForSeconds(0.2f);

        // User starts second objective but does not complete (e.g. headset off or exit)
        Abxr.EventObjectiveStart("hands_on_drill_" + tag);
        Abxr.Log("User entered hands-on section", Abxr.LogLevel.Debug, new Abxr.Dict().With("objective", "hands_on_drill_" + tag));
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("user_position", new Vector3(1f, 0f, 2f), new Abxr.Dict().With("section", "workstation_a"));
        yield return new WaitForSeconds(0.15f);
        Abxr.LogWarn("Session ended without completing objective", new Abxr.Dict().With("objective", "hands_on_drill_" + tag));

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

        yield return new WaitForSeconds(1f);

        Abxr.Event("button_pressed", new Abxr.Dict().With("screen", "main_menu").With("button", "start"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Abxr.Event("scene_entered", new Abxr.Dict().With("scene_id", "warehouse").With("entry_point", "spawn_a"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("item_collected", new Abxr.Dict().With("item_type", "coin").With("item_value", "100").With("location", "shelf_1"), sendTelemetry: false);
        Abxr.Event("item_collected", new Abxr.Dict().With("item_type", "tool").With("item_value", "wrench").With("location", "bench_2"), sendTelemetry: false);
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("player_teleported", new Vector3(2f, 0f, -1f), new Abxr.Dict().With("destination", "checkpoint_b"));
        yield return new WaitForSeconds(0.15f);
        Abxr.Event("button_pressed", new Abxr.Dict().With("screen", "inventory").With("button", "use_item"), sendTelemetry: false);
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "timed_exam_" + tag;

        Abxr.StartTimedEvent(assessmentName);
        Abxr.EventAssessmentStart(assessmentName);
        Abxr.LogInfo("Timed exam started", new Abxr.Dict().With("assessment", assessmentName));
        yield return new WaitForSeconds(0.2f);

        Abxr.EventObjectiveStart("question_1_" + tag);
        Abxr.EventInteractionComplete("answer_1_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "a");
        Abxr.EventObjectiveComplete("question_1_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.25f);
        Abxr.EventObjectiveStart("question_2_" + tag);
        Abxr.EventInteractionComplete("answer_2_" + tag, Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "b");
        Abxr.EventObjectiveComplete("question_2_" + tag, 100, Abxr.EventStatus.Pass);
        yield return new WaitForSeconds(0.3f);
        Abxr.Log("Timed exam completed", Abxr.LogLevel.Info, new Abxr.Dict().With("score", "88"));
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Abxr.EventLevelStart("level_1_" + tag);
        Abxr.LogInfo("Level 1 loaded", new Abxr.Dict().With("level", "level_1_" + tag));
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("waypoint_reached", new Vector3(0f, 0f, 5f), new Abxr.Dict().With("waypoint", "checkpoint_a"));
        yield return new WaitForSeconds(0.15f);
        Abxr.Event("waypoint_reached", new Vector3(2f, 0f, 8f), new Abxr.Dict().With("waypoint", "checkpoint_b"));
        yield return new WaitForSeconds(0.2f);
        Abxr.EventLevelComplete("level_1_" + tag, 85);
        yield return new WaitForSeconds(0.15f);

        Abxr.EventLevelStart("level_2_" + tag);
        Abxr.StartTimedEvent("level_2_duration_" + tag);
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("waypoint_reached", new Vector3(1f, 1f, 0f), new Abxr.Dict().With("waypoint", "start"));
        yield return new WaitForSeconds(0.15f);
        Abxr.Event("user_entered_zone", new Vector3(-3f, 0f, 0f), new Abxr.Dict().With("zone", "restricted"));
        Abxr.EventCritical("safety_violation", new Abxr.Dict().With("zone", "machine_room").With("level", "level_2_" + tag));
        Abxr.LogWarn("Restricted zone entered", new Abxr.Dict().With("zone", "restricted"));
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("level_2_duration_" + tag, new Abxr.Dict().With("level", "level_2_" + tag), sendTelemetry: false);
        Abxr.EventLevelComplete("level_2_" + tag, 70);
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

        yield return new WaitForSeconds(1f);
        Abxr.Register("user_type", "premium");
        Abxr.Register("app_version", "1.2.3");
        Abxr.RegisterOnce("user_tier", "free");
        yield return new WaitForSeconds(0.1f);

        Abxr.Event("session_started", new Abxr.Dict().With("source", "main_menu"), sendTelemetry: false);
        yield return new WaitForSeconds(0.15f);
        Abxr.LogInfo("Premium session active", new Abxr.Dict().With("feature", "extended_content"));
        Abxr.Event("content_selected", new Vector3(0f, 1.5f, -2f), new Abxr.Dict().With("content_id", "premium_video_1"));
        yield return new WaitForSeconds(0.2f);
        Abxr.Event("playback_started", new Abxr.Dict().With("content_id", "premium_video_1"), sendTelemetry: false);

        RunQuitHandlerInTearDown = true;
        RunEndSessionInTearDown = false;
    }

    [UnityTest]
    public IEnumerator StorageSetAndGet_Scenario()
    {
        // Narrative: Progress through checkpoints, save/load progress via storage, events and logs around each step.
        SetProductionCustomAuth();
        bool authSucceeded = false;
        yield return PerformAuth(result => authSucceeded = result);
        Assert.IsTrue(authSucceeded, "Authentication should succeed.");

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Abxr.LogInfo("Training session started", new Abxr.Dict().With("session", tag));
        Abxr.Event("checkpoint_reached", new Abxr.Dict().With("checkpoint", "intro").With("progress", "25%"), sendTelemetry: false);
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("progress", "25%").With("last_checkpoint", "intro"), Abxr.StorageScope.User);
        yield return new WaitForSeconds(0.2f);

        Abxr.Event("checkpoint_reached", new Vector3(1f, 0f, 2f), new Abxr.Dict().With("checkpoint", "mid"));
        Abxr.StorageSetEntry("state", new Abxr.Dict().With("progress", "75%").With("last_checkpoint", "mid"), Abxr.StorageScope.User);
        Abxr.Log("Progress saved", Abxr.LogLevel.Debug, new Abxr.Dict().With("progress", "75%"));
        yield return new WaitForSeconds(0.15f);

        List<Dictionary<string, string>> results = null;
        bool getDone = false;
        AbxrSubsystem.Instance.StartCoroutine(Abxr.StorageGetEntry("state", Abxr.StorageScope.User, r => { results = r; getDone = true; }));
        yield return new WaitUntil(() => getDone);
        Assert.IsNotNull(results, "StorageGetEntry should return.");
        Assert.Greater(results.Count, 0, "Should have at least one entry.");
        Abxr.LogInfo("Progress loaded for resume", new Abxr.Dict().With("entries", results.Count.ToString()));

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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);

        Abxr.Event("session_phase", new Abxr.Dict().With("phase", "calibration"), sendTelemetry: false);
        Abxr.Telemetry("headset_position", new Abxr.Dict().With("x", "0").With("y", "1.6").With("z", "0"));
        Abxr.Telemetry("frame_rate", new Abxr.Dict().With("fps", "72").With("target", "72"));
        yield return new WaitForSeconds(0.2f);
        Abxr.LogInfo("Calibration complete", new Abxr.Dict().With("session", tag));
        Abxr.Telemetry("headset_position", new Abxr.Dict().With("x", "1.23").With("y", "4.56").With("z", "7.89"));
        Abxr.Event("user_moved", new Vector3(1.23f, 4.56f, 7.89f), new Abxr.Dict().With("event", "telemetry_sync"));
        yield return new WaitForSeconds(0.2f);
        Abxr.Telemetry("battery_level", new Abxr.Dict().With("percent", "78").With("charging", "false"));
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

        yield return new WaitForSeconds(1f);
        string tag = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string assessmentName = "course_" + tag;

        Abxr.EventAssessmentStart(assessmentName, new Abxr.Dict().With("module", "safety"));
        Abxr.LogInfo("Course started", new Abxr.Dict().With("assessment", assessmentName));
        yield return new WaitForSeconds(0.2f);

        Abxr.EventObjectiveStart("open_valve_" + tag);
        Abxr.Event("valve_interaction", new Vector3(0.5f, 1f, 1.5f), new Abxr.Dict().With("action", "approach"));
        yield return new WaitForSeconds(0.2f);
        Abxr.EventInteractionComplete("valve_turn_" + tag, Abxr.InteractionType.Performance, Abxr.InteractionResult.Correct, "open");
        Abxr.EventObjectiveComplete("open_valve_" + tag, 100, Abxr.EventStatus.Complete);
        Abxr.Log("Objective open_valve completed", Abxr.LogLevel.Debug, new Abxr.Dict().With("objective", "open_valve_" + tag));
        yield return new WaitForSeconds(0.15f);

        Abxr.EventObjectiveStart("close_valve_" + tag);
        Abxr.StartTimedEvent("close_valve_time_" + tag);
        yield return new WaitForSeconds(0.25f);
        Abxr.Event("close_valve_time_" + tag, new Abxr.Dict().With("objective", "close_valve_" + tag), sendTelemetry: false);
        Abxr.Event("valve_interaction", new Vector3(0.5f, 1f, 1.5f), new Abxr.Dict().With("action", "close"));
        Abxr.EventObjectiveComplete("close_valve_" + tag, 100, Abxr.EventStatus.Complete);
        yield return new WaitForSeconds(0.2f);

        Abxr.EventAssessmentComplete(assessmentName, 95, Abxr.EventStatus.Pass, new Abxr.Dict().With("max_score", "100"));
    }
}

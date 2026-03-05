// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for event tracking, logging, telemetry, and super-metadata propagation.
// Uses AbxrSubsystem GetPending*ForTesting (works with any transport; service transport returns empty lists).
// Tests that assert on queue contents are skipped when using ArborInsightsClient (device) transport.
using System.Collections.Generic;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class EventTrackingTests : AbxrPlayModeTestBase
{
    private static bool IsServiceTransport => AbxrSubsystem.Instance?.GetTransportForTesting()?.IsServiceTransport == true;

    private static List<EventPayload> PendingEvents => AbxrSubsystem.Instance?.GetPendingEventsForTesting() ?? new List<EventPayload>();
    private static List<LogPayload> PendingLogs => AbxrSubsystem.Instance?.GetPendingLogsForTesting() ?? new List<LogPayload>();
    private static List<TelemetryPayload> PendingTelemetry => AbxrSubsystem.Instance?.GetPendingTelemetryForTesting() ?? new List<TelemetryPayload>();

    [SetUp]
    public void SkipWhenServiceTransport()
    {
        if (IsServiceTransport)
            Assert.Ignore("Event tracking tests that inspect the queue require REST transport. On device with ArborInsightsClient, run in Editor or use REST transport.");
    }

    // ── Generic Event ─────────────────────────────────────────────────────

    [Test]
    public void Event_AddsEventToQueue()
    {
        Abxr.Event("button_pressed", null, sendTelemetry: false);
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("button_pressed", events[0].name);
    }

    [Test]
    public void Event_WithMetadata_MetadataStoredInPayload()
    {
        Abxr.Event("item_selected", new Abxr.Dict().With("item_id", "sword_01"), sendTelemetry: false);
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("sword_01", events[0].meta["item_id"]);
    }

    [Test]
    public void Event_MultipleCalls_AllQueued()
    {
        Abxr.Event("event_one",   null, sendTelemetry: false);
        Abxr.Event("event_two",   null, sendTelemetry: false);
        Abxr.Event("event_three", null, sendTelemetry: false);
        Assert.AreEqual(3, PendingEvents.Count);
    }

    [Test]
    public void Event_TimestampIsSet()
    {
        Abxr.Event("ts_event", null, sendTelemetry: false);
        var e = PendingEvents[0];
        Assert.IsFalse(string.IsNullOrEmpty(e.timestamp));
        Assert.Greater(e.preciseTimestamp, 0L);
    }

    // ── StartTimedEvent ───────────────────────────────────────────────────

    [Test]
    public void StartTimedEvent_ThenEvent_IncludesDurationInMeta()
    {
        Abxr.StartTimedEvent("timed_activity");
        Abxr.Event("timed_activity", null, sendTelemetry: false);
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.IsTrue(events[0].meta.ContainsKey("duration"),
            "Timed event should include 'duration' in metadata");
    }

    [Test]
    public void Event_WithoutPriorStartTimedEvent_HasNoDurationKey()
    {
        Abxr.Event("untimed", null, sendTelemetry: false);
        var events = PendingEvents;
        Assert.IsFalse(events[0].meta.ContainsKey("duration"),
            "Non-timed events should not have a 'duration' key");
    }

    // ── Event with Vector3 position ───────────────────────────────────────

    [Test]
    public void Event_WithPosition_IncludesPositionMetadata()
    {
        Abxr.Event("object_placed", new Vector3(1.5f, 2.0f, 3.5f));
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.IsTrue(events[0].meta.ContainsKey("position_x"));
        Assert.IsTrue(events[0].meta.ContainsKey("position_y"));
        Assert.IsTrue(events[0].meta.ContainsKey("position_z"));
    }

    // ── EventCritical ─────────────────────────────────────────────────────

    [Test]
    public void EventCritical_PrefixesEventName()
    {
        Abxr.EventCritical("safety_check_skipped");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("CRITICAL_ABXR_safety_check_skipped", events[0].name);
    }

    // ── Assessment events ─────────────────────────────────────────────────

    [Test]
    public void EventAssessmentStart_AddsEventWithAssessmentMetadata()
    {
        Abxr.EventAssessmentStart("Fire Safety Training");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("Fire Safety Training", events[0].name);
        Assert.AreEqual("assessment", events[0].meta["type"]);
        Assert.AreEqual("started", events[0].meta["verb"]);
    }

    [Test]
    public void EventAssessmentComplete_AddsEventWithScoreAndStatus()
    {
        Abxr.EventAssessmentStart("Module A");
        Abxr.EventAssessmentComplete("Module A", 85, Abxr.EventStatus.Pass);
        var events = PendingEvents;
        var completed = events.Find(e => e.meta.GetValueOrDefault("verb") == "completed");
        Assert.IsNotNull(completed);
        Assert.AreEqual("85", completed.meta["score"]);
        Assert.AreEqual("pass", completed.meta["status"]);
        Assert.IsTrue(completed.meta.ContainsKey("duration"), "Completed assessment must include duration");
    }

    [Test]
    public void EventAssessmentComplete_WithoutPriorStart_DurationIsZero()
    {
        Abxr.EventAssessmentComplete("Unknown Assessment", 50, Abxr.EventStatus.Fail);
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("0", events[0].meta["duration"]);
    }

    [Test]
    public void EventAssessmentStart_DuplicateName_OnlyQueuesOnce()
    {
        Abxr.EventAssessmentStart("Unique Assessment");
        Abxr.EventAssessmentStart("Unique Assessment"); // second call ignored
        Assert.AreEqual(1, PendingEvents.Count,
            "Duplicate assessment start should be silently ignored");
    }

    [Test]
    public void EventAssessmentComplete_StatusLowercasedInPayload()
    {
        Abxr.EventAssessmentComplete("Test", 75, Abxr.EventStatus.Complete);
        var events = PendingEvents;
        Assert.AreEqual("complete", events[0].meta["status"]);
    }

    // ── Experience events (assessment wrappers) ───────────────────────────

    [Test]
    public void EventExperienceStart_AddsAssessmentTypeEvent()
    {
        Abxr.EventExperienceStart("VR Onboarding");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("assessment", events[0].meta["type"]);
        Assert.AreEqual("started", events[0].meta["verb"]);
    }

    [Test]
    public void EventExperienceComplete_Uses100ScoreAndCompleteStatus()
    {
        Abxr.EventExperienceStart("Orientation Tour");
        Abxr.EventExperienceComplete("Orientation Tour");
        var completed = PendingEvents
            .Find(e => e.meta.GetValueOrDefault("verb") == "completed");
        Assert.IsNotNull(completed);
        Assert.AreEqual("100", completed.meta["score"]);
        Assert.AreEqual("complete", completed.meta["status"]);
    }

    // ── Objective events ──────────────────────────────────────────────────

    [Test]
    public void EventObjectiveStart_AddsEventWithObjectiveMetadata()
    {
        Abxr.EventObjectiveStart("Identify Safety Hazards");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("objective", events[0].meta["type"]);
        Assert.AreEqual("started", events[0].meta["verb"]);
    }

    [Test]
    public void EventObjectiveComplete_AddsScoreStatusAndDuration()
    {
        Abxr.EventObjectiveStart("Wear PPE Correctly");
        Abxr.EventObjectiveComplete("Wear PPE Correctly", 90, Abxr.EventStatus.Pass);
        var completed = PendingEvents
            .Find(e => e.meta.GetValueOrDefault("verb") == "completed");
        Assert.IsNotNull(completed);
        Assert.AreEqual("90", completed.meta["score"]);
        Assert.AreEqual("pass", completed.meta["status"]);
        Assert.IsTrue(completed.meta.ContainsKey("duration"));
    }

    // ── Interaction events ────────────────────────────────────────────────

    [Test]
    public void EventInteractionStart_AddsEventWithInteractionMetadata()
    {
        Abxr.EventInteractionStart("select_fire_extinguisher");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("interaction", events[0].meta["type"]);
        Assert.AreEqual("started", events[0].meta["verb"]);
    }

    [Test]
    public void EventInteractionComplete_AddsTypeResultAndResponse()
    {
        Abxr.EventInteractionStart("choose_option");
        Abxr.EventInteractionComplete("choose_option",
            Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, "option_a");
        var completed = PendingEvents
            .Find(e => e.meta.GetValueOrDefault("verb") == "completed");
        Assert.IsNotNull(completed);
        Assert.AreEqual("select", completed.meta["interaction"]);
        Assert.AreEqual("correct", completed.meta["result"]);
        Assert.AreEqual("option_a", completed.meta["response"]);
    }

    [Test]
    public void EventInteractionComplete_NullResponse_OmitsResponseKey()
    {
        Abxr.EventInteractionComplete("boolean_q",
            Abxr.InteractionType.Bool, Abxr.InteractionResult.Correct, null);
        var events = PendingEvents;
        Assert.IsFalse(events[0].meta.ContainsKey("response"));
    }

    [Test]
    public void EventInteractionComplete_InteractionResultLowercased()
    {
        Abxr.EventInteractionComplete("q1",
            Abxr.InteractionType.Select, Abxr.InteractionResult.Incorrect);
        var events = PendingEvents;
        Assert.AreEqual("incorrect", events[0].meta["result"]);
    }

    // ── Level events ──────────────────────────────────────────────────────

    [Test]
    public void EventLevelStart_AddsLevelStartEventWithId()
    {
        Abxr.EventLevelStart("Chapter 1");
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("level_start", events[0].name);
        Assert.AreEqual("Chapter 1", events[0].meta["id"]);
        Assert.AreEqual("started", events[0].meta["verb"]);
    }

    [Test]
    public void EventLevelComplete_AddsScoreAndDuration()
    {
        Abxr.EventLevelStart("Chapter 1");
        Abxr.EventLevelComplete("Chapter 1", 75);
        var completed = PendingEvents
            .Find(e => e.name == "level_complete");
        Assert.IsNotNull(completed);
        Assert.AreEqual("75", completed.meta["score"]);
        Assert.IsTrue(completed.meta.ContainsKey("duration"));
    }

    // ── Logging ───────────────────────────────────────────────────────────

    [Test]
    public void Log_AddsLogToQueue()
    {
        Abxr.Log("Test log message");
        var logs = PendingLogs;
        Assert.AreEqual(1, logs.Count);
        Assert.AreEqual("Test log message", logs[0].text);
        Assert.AreEqual("info", logs[0].logLevel);
    }

    [Test]
    public void LogDebug_UsesDebugLevel()
    {
        Abxr.LogDebug("debug msg");
        Assert.AreEqual("debug", PendingLogs[0].logLevel);
    }

    [Test]
    public void LogInfo_UsesInfoLevel()
    {
        Abxr.LogInfo("info msg");
        Assert.AreEqual("info", PendingLogs[0].logLevel);
    }

    [Test]
    public void LogWarn_UsesWarnLevel()
    {
        Abxr.LogWarn("warn msg");
        Assert.AreEqual("warn", PendingLogs[0].logLevel);
    }

    [Test]
    public void LogError_UsesErrorLevel()
    {
        Abxr.LogError("error msg");
        Assert.AreEqual("error", PendingLogs[0].logLevel);
    }

    [Test]
    public void LogCritical_UsesCriticalLevel()
    {
        Abxr.LogCritical("critical msg");
        Assert.AreEqual("critical", PendingLogs[0].logLevel);
    }

    [Test]
    public void Log_WithMetadata_MetadataIncludedInPayload()
    {
        Abxr.Log("event with meta", Abxr.LogLevel.Info, new Abxr.Dict().With("context", "unit_test"));
        Assert.AreEqual("unit_test", PendingLogs[0].meta["context"]);
    }

    // ── Telemetry ─────────────────────────────────────────────────────────

    [Test]
    public void Telemetry_AddsEntryToTelemetryQueue()
    {
        Abxr.Telemetry("frame_rate", new Abxr.Dict().With("fps", "90"));
        var telemetry = PendingTelemetry;
        var entry = telemetry.Find(t => t.name == "frame_rate");
        Assert.IsNotNull(entry);
        Assert.AreEqual("90", entry.meta["fps"]);
    }

    [Test]
    public void Telemetry_MultipleCalls_AllQueued()
    {
        Abxr.Telemetry("battery", new Abxr.Dict().With("level", "0.8"));
        Abxr.Telemetry("cpu",     new Abxr.Dict().With("usage",  "45"));
        var telemetry = PendingTelemetry;
        Assert.GreaterOrEqual(telemetry.Count, 2);
    }

    // ── Super-metadata propagation ────────────────────────────────────────

    [Test]
    public void Event_IncludesSuperMetaDataInPayload()
    {
        Abxr.Register("user_role", "instructor");
        Abxr.Event("training_started", null, sendTelemetry: false);
        var events = PendingEvents;
        Assert.IsTrue(events[0].meta.ContainsKey("user_role"));
        Assert.AreEqual("instructor", events[0].meta["user_role"]);
    }

    [Test]
    public void Event_EventSpecificMetaTakesPrecedenceOverSuperMeta()
    {
        Abxr.Register("context", "super_meta_value");
        Abxr.Event("test", new Abxr.Dict().With("context", "event_value"), sendTelemetry: false);
        Assert.AreEqual("event_value",
            PendingEvents[0].meta["context"]);
    }

    [Test]
    public void Log_IncludesSuperMetaDataInPayload()
    {
        Abxr.Register("build", "v1.2.3");
        Abxr.Log("log with super meta");
        var log = PendingLogs[0];
        Assert.IsTrue(log.meta.ContainsKey("build"));
        Assert.AreEqual("v1.2.3", log.meta["build"]);
    }

    // ── Backwards-compatible overloads ────────────────────────────────────

    [Test]
    public void EventAssessmentComplete_StringScore_ParsedCorrectly()
    {
        Abxr.EventAssessmentStart("Compat Test");
        Abxr.EventAssessmentComplete("Compat Test", "72"); // string overload
        var completed = PendingEvents
            .Find(e => e.meta.GetValueOrDefault("verb") == "completed");
        Assert.IsNotNull(completed);
        Assert.AreEqual("72", completed.meta["score"]);
    }

    [Test]
    public void EventInteractionComplete_StringResultStringResponse_Overload()
    {
        // Old 4-arg overload: (name, result, response, interactionType)
        Abxr.EventInteractionComplete("q", "correct", "option_b", Abxr.InteractionType.Select);
        var events = PendingEvents;
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("correct", events[0].meta["result"]);
        Assert.AreEqual("option_b", events[0].meta["response"]);
    }
}

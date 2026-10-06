// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for AbxrObserver (SDK-64): what the data service reports for each record, with a fake transport.
// The REST batch results, storage drops, and the public Abxr methods are covered in PlayMode (AbxrObserverRestTests).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Services.Data;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class AbxrObserverTests
{
    private GameObject _runner;
    private FakeTransport _transport;
    private bool _recording;
    private AbxrDataService _data;
    private List<AbxrObserver.Record> _created;
    private List<AbxrObserver.SendResult> _sent;

    [SetUp]
    public void SetUp()
    {
        AbxrObserver.ResetForTesting();
        _runner = new GameObject("[Test] Runner");
        _transport = new FakeTransport();
        _recording = true;
        _data = new AbxrDataService(_runner.AddComponent<Runner>(), () => _transport, () => _recording);
        _created = new List<AbxrObserver.Record>();
        _sent = new List<AbxrObserver.SendResult>();
    }

    [TearDown]
    public void TearDown()
    {
        AbxrObserver.ResetForTesting();
        UnityEngine.Object.DestroyImmediate(_runner);
    }

    private void Observe()
    {
        AbxrObserver.OnRecordCreated += _created.Add;
        AbxrObserver.OnRecordsSent += _sent.Add;
    }

    [Test]
    public void NoSubscribers_NoIdsAndNoScope()
    {
        var meta = new Dictionary<string, string> { ["k"] = "v" };
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, meta, "Event"))
            _data.AddEvent("e", meta);

        Assert.AreEqual(1, _transport.Added.Count);
        Assert.AreEqual(0, _transport.Added[0].recordId, "With no handler the transport gets no id.");

        // No id was used up, and the unobserved scope left nothing open for the next record.
        Observe();
        _data.AddEvent("next", null);
        Assert.AreEqual(1, _created[0].Id);
        Assert.IsTrue(_created[0].Automatic, "A scope opened while nobody observed must not attach to a later record.");
    }

    [Test]
    public void AppScope_ReportsMethodAndCallerDataFromBeforeTheSdkChangedIt()
    {
        Observe();
        var meta = new Dictionary<string, string> { ["score"] = "90" };
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, meta, "EventAssessmentComplete"))
        {
            // The subsystem writes into the caller's own dictionary before the data service sees it.
            meta["sceneName"] = "Lobby";
            meta["score_max"] = "100";
            _data.AddEvent("Quiz", meta);
        }

        var record = _created[0];
        Assert.AreEqual("EventAssessmentComplete", record.Method);
        Assert.IsFalse(record.Automatic);
        CollectionAssert.AreEquivalent(new Dictionary<string, string> { ["score"] = "90" }, record.CallerData);
        CollectionAssert.AreEquivalent(new Dictionary<string, string> { ["score"] = "90", ["sceneName"] = "Lobby", ["score_max"] = "100" }, record.Data);
        Assert.AreEqual(record.Id, _transport.Added[0].recordId, "The transport carries the same id to the send result.");
    }

    [Test]
    public void AppScope_NullMeta_ReportsEmptyCallerData()
    {
        Observe();
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Log, null, "LogInfo"))
            _data.AddLog("info", "hello", null);

        Assert.IsNotNull(_created[0].CallerData);
        Assert.AreEqual(0, _created[0].CallerData.Count);
    }

    [Test]
    public void AppScope_FirstRecordOfItsKindTakesIt_OthersAreAutomatic()
    {
        Observe();
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, null, "Event"))
        {
            _data.AddTelemetry("Target Gaze", null);   // gaze telemetry, sent before the event
            _data.AddEvent("Clicked", null);
            _data.AddTelemetry("Head Position", null); // location telemetry, sent after it
            _data.AddEvent("Second", null);
        }

        CollectionAssert.AreEqual(new[] { true, false, true, true }, _created.ConvertAll(r => r.Automatic));
        Assert.AreEqual("Event", _created[1].Method);
        Assert.IsNull(_created[0].Method);
        Assert.IsNull(_created[0].CallerData, "Automatic records have no caller data.");
    }

    [Test]
    public void AppScope_OutermostWins()
    {
        Observe();
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Log, new Dictionary<string, string> { ["outer"] = "1" }, "LogDebug"))
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Log, new Dictionary<string, string> { ["inner"] = "1" }, "Log"))
            _data.AddLog("debug", "x", null);

        Assert.AreEqual("LogDebug", _created[0].Method);
        Assert.IsTrue(_created[0].CallerData.ContainsKey("outer"));
    }

    [Test]
    public void AutomaticScope_InsideAppScope_IsAutomaticAndLeavesTheScopeForTheAppsRecord()
    {
        Observe();
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, null, "EventAssessmentStart"))
        {
            using (AbxrObserver.AutomaticScope())
            using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, null, "Event"))
                _data.AddEvent("DEFAULT", null);
            _data.AddEvent("Quiz", null);
        }

        Assert.IsTrue(_created[0].Automatic);
        Assert.IsFalse(_created[1].Automatic);
        Assert.AreEqual("EventAssessmentStart", _created[1].Method);
    }

    [Test]
    public void AppCodeScope_AppCallsFromAnSdkCallback_GetTheirOwnScope()
    {
        // EventAssessmentComplete advances the module sequence and calls the app's OnModuleTarget, whose handler starts the next one.
        Observe();
        using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, null, "EventAssessmentComplete"))
        {
            _data.AddEvent("Module 1", null);
            using (AbxrObserver.AppCodeScope())
            using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Event, new Dictionary<string, string> { ["next"] = "1" }, "EventAssessmentStart"))
                _data.AddEvent("Module 2", null);
            _data.AddEvent("after", null);
        }

        Assert.AreEqual("EventAssessmentComplete", _created[0].Method);
        Assert.IsFalse(_created[1].Automatic, "The handler's call is the app's.");
        Assert.AreEqual("EventAssessmentStart", _created[1].Method);
        Assert.AreEqual("1", _created[1].CallerData["next"]);
        Assert.IsTrue(_created[2].Automatic, "The outer scope comes back, already taken.");
    }

    [Test]
    public void AppCodeScope_InsideAutomaticScope_LetsTheAppsCallsReportAsTheApps()
    {
        // EndSession closes an assessment automatically, which can advance the module sequence into app code.
        Observe();
        using (AbxrObserver.AutomaticScope())
        {
            using (AbxrObserver.AppCodeScope())
            using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Log, null, "LogInfo"))
                _data.AddLog("info", "from the app", null);
            _data.AddEvent("closing", null);
        }

        Assert.IsFalse(_created[0].Automatic);
        Assert.IsTrue(_created[1].Automatic);
    }

    [Test]
    public void HandlerThatRecords_IsNotReported_AndDoesNotLoop()
    {
        AbxrObserver.OnRecordCreated += r =>
        {
            _created.Add(r);
            using (AbxrObserver.AppScope(AbxrObserver.RecordKind.Log, null, "LogInfo"))
                _data.AddLog("info", "saw " + r.Name, null);
        };

        _data.AddEvent("e", null);

        Assert.AreEqual(1, _created.Count, "A record made inside a handler must not raise the handler again.");
        Assert.AreEqual(2, _transport.Added.Count, "It is still recorded.");
        Assert.AreEqual(0, _transport.Added[1].recordId);
    }

    [Test]
    public void SendHandlerThatRecords_OnTheServiceTransport_DoesNotLoop()
    {
        _transport.IsServiceTransport = true;
        AbxrObserver.OnRecordsSent += s => { _sent.Add(s); _data.AddLog("info", "sent", null); };

        _data.AddEvent("e", null);

        Assert.AreEqual(1, _sent.Count);
        Assert.AreEqual(2, _transport.Added.Count);
    }

    [Test]
    public void NoScope_IsAutomatic()
    {
        Observe();
        _data.AddTelemetry("Battery", new Dictionary<string, string> { ["Percentage"] = "80%" });

        Assert.IsTrue(_created[0].Automatic);
        Assert.AreEqual(AbxrObserver.RecordKind.Telemetry, _created[0].Kind);
        Assert.AreEqual("80%", _created[0].Data["Percentage"]);
    }

    [Test]
    public void Data_IsACopy()
    {
        Observe();
        var meta = new Dictionary<string, string> { ["k"] = "v" };
        _data.AddEvent("e", meta);
        meta["k"] = "changed";

        Assert.AreEqual("v", _created[0].Data["k"], "A handler must see the record as queued, not later changes to the app's dictionary.");
    }

    [Test]
    public void Log_NameIsTheMessage_LevelIsSet()
    {
        Observe();
        _data.AddLog("warn", "Low battery", null);

        Assert.AreEqual(AbxrObserver.RecordKind.Log, _created[0].Kind);
        Assert.AreEqual("Low battery", _created[0].Name);
        Assert.AreEqual("warn", _created[0].Level);
    }

    [Test]
    public void NotRecording_ReportsTheDrop_AndDoesNotReachTheTransport()
    {
        Observe();
        _recording = false;
        _data.AddEvent("e", null);

        Assert.AreEqual(0, _transport.Added.Count);
        Assert.AreEqual(AbxrObserver.DropReason.NotRecording, _created[0].DropReason);
        Assert.IsTrue(_created[0].Dropped);
        Assert.AreEqual(0, _sent.Count);
    }

    [Test]
    public void QueueFull_ReportsTheDrop()
    {
        Observe();
        _transport.Accept = false;
        _data.AddTelemetry("t", null);

        Assert.AreEqual(AbxrObserver.DropReason.QueueFull, _created[0].DropReason);
        Assert.AreEqual(0, _sent.Count, "A dropped record is never sent.");
    }

    [Test]
    public void RestTransport_NoSendResultAtCreation()
    {
        Observe();
        _data.AddEvent("e", null);

        Assert.AreEqual(AbxrObserver.DropReason.None, _created[0].DropReason);
        Assert.AreEqual(0, _sent.Count, "REST reports when its batch is sent.");
    }

    [Test]
    public void ServiceTransport_ReportsHandedToService()
    {
        Observe();
        _transport.IsServiceTransport = true;
        _data.AddLog("info", "hello", null);

        Assert.AreEqual(1, _sent.Count);
        Assert.AreEqual(AbxrObserver.SendStatus.HandedToService, _sent[0].Status);
        CollectionAssert.AreEqual(new[] { _created[0].Id }, _sent[0].RecordIds);
        Assert.AreEqual(0, _sent[0].HttpStatus);
    }

    [Test]
    public void ServiceTransport_DroppedRecord_IsNotHandedToService()
    {
        Observe();
        _transport.IsServiceTransport = true;
        _recording = false;
        _data.AddEvent("e", null);

        Assert.AreEqual(0, _sent.Count);
    }

    [Test]
    public void OnlySendHandler_StillGetsIds()
    {
        AbxrObserver.OnRecordsSent += _sent.Add;
        _transport.IsServiceTransport = true;
        _data.AddEvent("e", null);

        Assert.AreNotEqual(0, _transport.Added[0].recordId);
        CollectionAssert.AreEqual(new[] { _transport.Added[0].recordId }, _sent[0].RecordIds);
    }

    [Test]
    public void Ids_AreUniqueAndIncreasing()
    {
        Observe();
        _data.AddEvent("a", null);
        _data.AddTelemetry("b", null);
        _data.AddLog("info", "c", null);

        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, _created.ConvertAll(r => r.Id));
    }

    [Test]
    public void HandlerThatThrows_DoesNotStopRecordingOrTheOtherHandlers()
    {
        AbxrObserver.OnRecordCreated += _ => throw new InvalidOperationException("app handler failed");
        Observe();
        LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("An AbxrObserver.OnRecordCreated handler threw: app handler failed")));

        _data.AddEvent("e", null);

        Assert.AreEqual(1, _transport.Added.Count, "The record must still be queued.");
        Assert.AreEqual(1, _created.Count, "The handler after the one that threw must still run.");
    }

    [Test]
    public void Unsubscribed_StopsReporting()
    {
        AbxrObserver.OnRecordCreated += _created.Add;
        _data.AddEvent("a", null);
        AbxrObserver.OnRecordCreated -= _created.Add;
        _data.AddEvent("b", null);

        Assert.AreEqual(1, _created.Count);
        Assert.AreEqual(0, _transport.Added[1].recordId, "With every handler removed the SDK goes back to doing no work.");
    }

    private class Runner : MonoBehaviour { }

    private sealed class FakeTransport : IAbxrTransport
    {
        public readonly List<(string name, long recordId)> Added = new List<(string, long)>();
        public bool Accept { get; set; } = true;
        public bool IsServiceTransport { get; set; }

        private bool Add(string name, long recordId)
        {
            if (!Accept) return false;
            Added.Add((name, recordId));
            return true;
        }

        public bool AddEvent(string name, Dictionary<string, string> meta, long recordId = 0) => Add(name, recordId);
        public bool AddTelemetry(string name, Dictionary<string, string> meta, long recordId = 0) => Add(name, recordId);
        public bool AddLog(string logLevel, string text, Dictionary<string, string> meta, long recordId = 0) => Add(text, recordId);
        public bool StorageAdd(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy, long recordId = 0) => Add(name, recordId);

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<AuthTransportResult> onComplete) { yield break; }
        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete) { yield break; }
        public void ForceSend() { }
        public IEnumerator StorageGetCoroutine(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete) { yield break; }
        public IEnumerator StorageDeleteCoroutine(Abxr.StorageScope scope, string name, Action<bool> onComplete) { yield break; }
        public void OnQuit() { }
        public void ClearAllPending() { }
        public List<EventPayload> GetPendingEventsForTesting() => new List<EventPayload>();
        public List<LogPayload> GetPendingLogsForTesting() => new List<LogPayload>();
        public List<TelemetryPayload> GetPendingTelemetryForTesting() => new List<TelemetryPayload>();
    }
}

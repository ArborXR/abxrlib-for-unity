// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for SessionReplayer (SDK-72, offline RFC D1, D5, D6): which sessions are signed in again, in what
// order, with what, and what each backend answer does. A fake host stands in for the transport.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Services.Transport;
using AbxrLib.Runtime.Services.Transport.Offline;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

[TestFixture]
public class SessionReplayerTests
{
    private string _root;
    private DurableRecordQueue _queue;
    private FakeHost _host;
    private SessionReplayer _replayer;
    private DateTime _now;
    private List<(string session, AbxrObserver.DropReason reason)> _dropped;

    private static string Jwt(string claims) =>
        "eyJhbGciOiJIUzI1NiJ9." + Convert.ToBase64String(Encoding.UTF8.GetBytes(claims)).TrimEnd('=') + ".sig";

    private static readonly string AppToken = Jwt("{\"appId\":\"app-1\"}");
    private static readonly string OrgToken = Jwt("{\"orgId\":\"org-1\"}");

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "abxrlib-replay-tests", Guid.NewGuid().ToString("N"));
        _now = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        _dropped = new List<(string, AbxrObserver.DropReason)>();
        _queue = new DurableRecordQueue(_root, () => _now, () => 30, () => 50L * 1024 * 1024);
        _queue.OnDropped = (s, n, r, _) => _dropped.Add((s, r));
        _queue.Load();
        _host = new FakeHost(_queue);
        _replayer = new SessionReplayer(_queue, _host);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch (DirectoryNotFoundException) { }
    }

    /// <summary>Records a sealed session, as an earlier launch would have.</summary>
    private void Sealed(string sessionId, bool signedInOnline = true, string loginType = null, string loginValue = null,
        string appKey = "app-1", string orgKey = "org-1", params string[] events)
    {
        _queue.SaveSignIn(new SavedSignIn
        {
            payload = new AuthPayload { sessionId = sessionId, deviceId = "device-1", deviceTitle = "Headset " + sessionId, buildType = "production" },
            signedInOnline = signedInOnline,
            loginType = loginType,
            loginValue = loginValue,
            appKey = appKey,
            orgKey = orgKey,
            instanceKey = ""
        });
        foreach (var e in events.Length > 0 ? events : new[] { sessionId + "-event" })
            _queue.Add(sessionId, new QueuedRecord(QueuedKind.Event, new EventPayload { name = e, meta = new Dictionary<string, string>(), recordId = Guid.NewGuid().ToString() }));
        _now = _now.AddMinutes(1);
        _queue.SealLive();
    }

    private void Run() => Drive(_replayer.Run());

    /// <summary>Steps a coroutine and the ones it yields to the end, as Unity would without waiting.</summary>
    private static void Drive(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        for (int guard = 0; stack.Count > 0; guard++)
        {
            Assert.Less(guard, 100000, "The replay never finished.");
            var top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator inner) stack.Push(inner);
        }
    }

    private static AuthTransportResult Token(string token) =>
        new AuthTransportResult(true, "{\"token\":\"" + token + "\",\"secret\":\"secret-" + token + "\",\"appId\":\"a\"}", false, 200);

    private static AuthTransportResult Status(long status, string body = "{\"detail\":\"no\"}", int retryAfter = 0) =>
        new AuthTransportResult(false, body, status == 401 || status == 403, status, false, retryAfter);

    // ── Order and scope ──────────────────────────────────────

    [Test]
    public void ReplaysOldestFirst_EachUnderItsOwnToken_AndDeletesThemWhenSent()
    {
        Sealed("first");
        Sealed("second");
        _host.SignInResults.Enqueue(Token("t1"));
        _host.SignInResults.Enqueue(Token("t2"));

        Run();

        CollectionAssert.AreEqual(new[] { "first", "second" }, _host.SignIns.Select(p => p.sessionId));
        CollectionAssert.AreEqual(new[] { ("first-event", "t1"), ("second-event", "t2") },
            _host.Uploads.Select(u => (((EventPayload)u.chunk.Records[0].Payload).name, u.token)));
        Assert.AreEqual(0, _queue.PendingSessions);
    }

    [Test]
    public void TheLiveSession_IsNeverReplayed()
    {
        Sealed("ended");
        _queue.SaveSignIn(new SavedSignIn { payload = new AuthPayload { sessionId = "live" }, appKey = "app-1", orgKey = "org-1", instanceKey = "" });
        _queue.Add("live", new QueuedRecord(QueuedKind.Event, new EventPayload { name = "now", recordId = "r" }));
        _host.SignInResults.Enqueue(Token("t1"));

        Run();

        CollectionAssert.AreEqual(new[] { "ended" }, _host.SignIns.Select(p => p.sessionId));
        Assert.AreEqual(1, _queue.PendingLive(RecordStream.Data), "The live session sends with the live token, not by replay.");
    }

    [Test]
    public void ASessionWithALiveSendOut_WaitsForIt()
    {
        Sealed("busy");
        _host.Busy = "busy";

        Run();

        Assert.IsEmpty(_host.SignIns);
    }

    // ── What the sign-in carries ─────────────────────────────

    [Test]
    public void SignIn_UsesTheSavedSessionWithTodaysCredentials()
    {
        Sealed("s1");
        _host.SignInResults.Enqueue(Token("t1"));

        Run();

        var sent = _host.SignIns.Single();
        Assert.AreEqual("s1", sent.sessionId);
        Assert.AreEqual("Headset s1", sent.deviceTitle, "The title the session started with (D8).");
        Assert.AreEqual(AppToken, sent.appToken);
        Assert.AreEqual(OrgToken, sent.orgToken);
        Assert.IsNull(sent.SSOAccessToken);
        Assert.IsNull(sent.authMechanism, "A session that signed in online replays device-only.");
    }

    [Test]
    public void AnOfflineSession_SignsInWithItsSavedAnswer()
    {
        Sealed("offline", signedInOnline: false, loginType: "email", loginValue: "sam@example.com");
        _host.SignInResults.Enqueue(Token("t1"));

        Run();

        var mechanism = _host.SignIns.Single().authMechanism;
        Assert.AreEqual("email", mechanism["type"]);
        Assert.AreEqual("sam@example.com", mechanism["prompt"]);
    }

    [Test]
    public void ADeviceOnlyAnswerWithoutAToken_TriesAgainWithTheSavedAnswer()
    {
        // Today's backend asks for the user again on a session it already knows; INS-527 won't.
        Sealed("online", signedInOnline: true, loginType: "text", loginValue: "Sam");
        _host.SignInResults.Enqueue(new AuthTransportResult(true, "{\"appId\":\"a\"}", false, 200));
        _host.SignInResults.Enqueue(Token("t1"));

        Run();

        Assert.AreEqual(2, _host.SignIns.Count);
        Assert.IsNull(_host.SignIns[0].authMechanism);
        Assert.AreEqual("Sam", _host.SignIns[1].authMechanism["prompt"]);
        Assert.AreEqual(0, _queue.PendingSessions);
    }

    // ── Answers ──────────────────────────────────────────────

    [Test]
    public void RateLimited_WaitsForRetryAfter_ThenCarriesOn()
    {
        Sealed("s1");
        _host.SignInResults.Enqueue(Status(429, retryAfter: 17));
        _host.SignInResults.Enqueue(Token("t1"));

        Run();

        CollectionAssert.AreEqual(new[] { 17f }, _host.Waits);
        Assert.AreEqual(0, _queue.PendingSessions);
    }

    [Test]
    public void RefusedSignIn_SkipsTheSessionThisLaunch_AndDropsItAfterThree()
    {
        Sealed("refused");
        Sealed("fine");
        _host.SignInResults.Enqueue(Status(400));
        _host.SignInResults.Enqueue(Token("t2"));

        Run();

        Assert.AreEqual(1, _queue.SealedSessions().Single().Summary.failedReplays);
        Assert.AreEqual(0, _host.Uploads.Count(u => u.chunk.SessionId == "refused"));
        Assert.AreEqual(1, _host.Uploads.Count(u => u.chunk.SessionId == "fine"), "One refused session doesn't hold up the others.");
        Assert.IsFalse(_replayer.ShouldRun(float.MaxValue), "Skipped until the next launch.");

        for (int launch = 2; launch <= 3; launch++)
        {
            _replayer = new SessionReplayer(_queue, _host);
            _host.SignInResults.Enqueue(Status(400));
            Run();
        }

        Assert.AreEqual(0, _queue.PendingSessions);
        CollectionAssert.Contains(_dropped, ("refused", AbxrObserver.DropReason.Rejected));
    }

    [Test]
    public void Offline_StopsAndWaits()
    {
        Sealed("s1");
        Sealed("s2");
        _host.SignInResults.Enqueue(new AuthTransportResult(false, null, false, 0, networkError: true));

        Run();

        Assert.AreEqual(1, _host.SignIns.Count, "No point trying the next session.");
        Assert.AreEqual(2, _queue.PendingSessions);
        Assert.IsFalse(_replayer.ShouldRun(_host.Now + 1));
        Assert.IsTrue(_replayer.ShouldRun(_host.Now + SessionReplayer.RetryAfterOfflineSeconds));
        _replayer.ResumeNow();
        Assert.IsTrue(_replayer.ShouldRun(_host.Now), "A live upload that gets through proves the connection.");
    }

    [Test]
    public void ASessionTheBackendKeepsFailing_IsPassedAfterThreeTries_SoTheNextOneUploads()
    {
        Sealed("s1", events: "first");
        Sealed("s2", events: "second");

        for (int run = 1; run < SessionReplayer.MaxFailedTriesPerLaunch; run++)
        {
            _host.SignInResults.Enqueue(Status(500));
            Run();
            _replayer.ResumeNow();
        }
        Assert.AreEqual(SessionReplayer.MaxFailedTriesPerLaunch - 1, _host.SignIns.Count, "Each try stops at the failing session.");

        _host.SignInResults.Enqueue(Status(500));
        _host.SignInResults.Enqueue(Token("t2"));
        Run();

        Assert.AreEqual("s2", _host.SignIns.Last().sessionId, "The last failed try moves on to the next session.");
        Assert.AreEqual(1, _queue.PendingSessions, "s2 uploaded; s1 waits for the next launch.");
        Assert.IsFalse(_replayer.ShouldRun(_host.Now + SessionReplayer.RetryAfterOfflineSeconds), "s1 isn't tried again this launch.");
    }

    [Test]
    public void NoConnection_IsNotAFailedTry_SoTheSessionUploadsOnceOnline()
    {
        Sealed("s1");
        for (int run = 0; run < SessionReplayer.MaxFailedTriesPerLaunch + 2; run++)
        {
            _host.SignInResults.Enqueue(new AuthTransportResult(false, null, false, 0, networkError: true));
            Run();
            _replayer.ResumeNow();
        }

        _host.SignInResults.Enqueue(Token("t1"));
        Run();

        Assert.AreEqual(0, _queue.PendingSessions, "A wifi drop doesn't park the session until the next launch.");
    }

    [Test]
    public void RevokedInstance_RevokesThePairing()
    {
        Sealed("s1");
        _host.SignInResults.Enqueue(Status(401, "{\"message\":\"Invalid app instance token\"}"));
        _host.Revoked = r => r.StatusCode == 401;

        Run();

        Assert.IsTrue(_host.RevokeCalled);
    }

    [Test]
    public void ADifferentIdentity_IsDroppedWithoutSigningIn()
    {
        Sealed("other-org", orgKey: "org-2");

        Run();

        Assert.IsEmpty(_host.SignIns);
        CollectionAssert.Contains(_dropped, ("other-org", AbxrObserver.DropReason.IdentityChanged));
    }

    [Test]
    public void AnExpiredReplayToken_SignsInOnceMore()
    {
        Sealed("s1");
        _host.SignInResults.Enqueue(Token("t1"));
        _host.SignInResults.Enqueue(Token("t2"));
        _host.UploadResults.Enqueue(new ChunkResult(ChunkOutcome.Unauthorized));

        Run();

        Assert.AreEqual(2, _host.SignIns.Count);
        Assert.AreEqual("t2", _host.Uploads.Last().token);
        Assert.AreEqual(0, _queue.PendingSessions);
    }

    [Test]
    public void BuildPayload_MapsProductionCustomToProduction()
    {
        var signIn = new SavedSignIn { payload = new AuthPayload { sessionId = "s" } };
        var payload = SessionReplayer.BuildPayload(signIn, new AuthPayload { appToken = "a", buildType = "production_custom" }, null);
        Assert.AreEqual("production", payload.buildType);
    }

    private sealed class FakeHost : IReplayHost
    {
        private readonly DurableRecordQueue _queue;
        public readonly Queue<AuthTransportResult> SignInResults = new Queue<AuthTransportResult>();
        public readonly Queue<ChunkResult> UploadResults = new Queue<ChunkResult>();
        public readonly List<AuthPayload> SignIns = new List<AuthPayload>();
        public readonly List<(RecordChunk chunk, string token)> Uploads = new List<(RecordChunk, string)>();
        public readonly List<float> Waits = new List<float>();
        public string Busy;
        public bool RevokeCalled;
        public Func<AuthTransportResult, bool> Revoked = _ => false;

        public FakeHost(DurableRecordQueue queue) => _queue = queue;

        public IEnumerator SignIn(AuthPayload payload, Action<AuthTransportResult> done)
        {
            SignIns.Add(payload);
            done(SignInResults.Count > 0 ? SignInResults.Dequeue() : Status(500));
            yield break;
        }

        public IEnumerator Upload(RecordChunk chunk, string token, string secret, Action<ChunkResult> done)
        {
            Uploads.Add((chunk, token));
            done(UploadResults.Count > 0 ? UploadResults.Dequeue() : new ChunkResult(ChunkOutcome.Done));
            yield break;
        }

        public AuthPayload CurrentCredentials() => new AuthPayload { appToken = AppToken, orgToken = OrgToken, buildType = "production", deviceId = "device-1" };
        public bool IsBusy(string sessionId) => sessionId == Busy;
        public bool IsRevokedInstance(AuthTransportResult result) => Revoked(result);
        public void RevokePairing() => RevokeCalled = true;
        public int ChunkSize(RecordStream stream) => 10;
        public float Now => 1000f;

        public IEnumerator Wait(float seconds)
        {
            Waits.Add(seconds);
            yield break;
        }

        public void Acknowledge(RecordChunk chunk) => _queue.Ack(chunk);
    }
}

// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins the clock that events, telemetry, logs and storage entries are stamped with (SDK-97). It was Unity's game
// clock, which stops while the app is paused or Time.timeScale is 0, so records made after a headset slept were
// stamped in the past. A synchronous EditMode test runs inside one frame, so the game clock stands still for it;
// an app at timeScale 0 is covered in PlayMode (EventTrackingTests).
using System;
using System.Threading;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class RecordTimestampTests
{
    [Test]
    public void Timestamp_IsTheDeviceUtcClock()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long stamp = Utils.GetUnityTime();
        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.That(stamp, Is.InRange(before, after));
    }

    [Test]
    public void Timestamp_KeepsUpWithRealTime_WhileTheGameClockStandsStill()
    {
        const int waitMs = 200;
        float gameClock = Time.time;

        long first = Utils.GetUnityTime();
        Thread.Sleep(waitMs);
        long second = Utils.GetUnityTime();

        Assert.AreEqual(gameClock, Time.time, "The game clock moved, so this no longer shows the stamp is independent of it.");
        Assert.GreaterOrEqual(second - first, waitMs - 20, "A record made after a real-time wait must be stamped that much later.");
    }
}

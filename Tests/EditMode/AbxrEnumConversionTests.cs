// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode unit tests for enum values and ResultOptions → EventStatus conversion
using NUnit.Framework;

[TestFixture]
public class AbxrEnumConversionTests
{
    // ── ResultOptions → EventStatus (backwards-compatibility shim) ────────

    [Test]
    public void ToEventStatus_Null_MapsToComplete()
        => Assert.AreEqual(Abxr.EventStatus.Complete, Abxr.ResultOptions.Null.ToEventStatus());

    [Test]
    public void ToEventStatus_Pass_MapsToPass()
        => Assert.AreEqual(Abxr.EventStatus.Pass, Abxr.ResultOptions.Pass.ToEventStatus());

    [Test]
    public void ToEventStatus_Fail_MapsToFail()
        => Assert.AreEqual(Abxr.EventStatus.Fail, Abxr.ResultOptions.Fail.ToEventStatus());

    [Test]
    public void ToEventStatus_Complete_MapsToComplete()
        => Assert.AreEqual(Abxr.EventStatus.Complete, Abxr.ResultOptions.Complete.ToEventStatus());

    [Test]
    public void ToEventStatus_Incomplete_MapsToIncomplete()
        => Assert.AreEqual(Abxr.EventStatus.Incomplete, Abxr.ResultOptions.Incomplete.ToEventStatus());

    [Test]
    public void ToEventStatus_Browsed_MapsToBrowsed()
        => Assert.AreEqual(Abxr.EventStatus.Browsed, Abxr.ResultOptions.Browsed.ToEventStatus());

    // ── LogLevel ──────────────────────────────────────────────────────────

    [Test]
    public void LogLevel_HasAllExpectedValues()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.LogLevel), Abxr.LogLevel.Debug));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.LogLevel), Abxr.LogLevel.Info));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.LogLevel), Abxr.LogLevel.Warn));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.LogLevel), Abxr.LogLevel.Error));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.LogLevel), Abxr.LogLevel.Critical));
    }

    // ── EventStatus ───────────────────────────────────────────────────────

    [Test]
    public void EventStatus_HasAllExpectedValues()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.Pass));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.Fail));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.Complete));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.Incomplete));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.Browsed));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.EventStatus), Abxr.EventStatus.NotAttempted));
    }

    // ── InteractionType ───────────────────────────────────────────────────

    [Test]
    public void InteractionType_HasAllExpectedValues()
    {
        var expected = new[]
        {
            Abxr.InteractionType.Null,
            Abxr.InteractionType.Bool,
            Abxr.InteractionType.Select,
            Abxr.InteractionType.Text,
            Abxr.InteractionType.Rating,
            Abxr.InteractionType.Number,
            Abxr.InteractionType.Matching,
            Abxr.InteractionType.Performance,
            Abxr.InteractionType.Sequencing
        };
        foreach (var type in expected)
            Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.InteractionType), type));
    }

    // ── InteractionResult ────────────────────────────────────────────────

    [Test]
    public void InteractionResult_HasCorrectIncorrectNeutral()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.InteractionResult), Abxr.InteractionResult.Correct));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.InteractionResult), Abxr.InteractionResult.Incorrect));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.InteractionResult), Abxr.InteractionResult.Neutral));
    }

    // ── StoragePolicy / StorageScope ──────────────────────────────────────

    [Test]
    public void StoragePolicy_HasKeepLatestAndAppendHistory()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.StoragePolicy), Abxr.StoragePolicy.KeepLatest));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.StoragePolicy), Abxr.StoragePolicy.AppendHistory));
    }

    [Test]
    public void StorageScope_HasDeviceAndUser()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.StorageScope), Abxr.StorageScope.Device));
        Assert.IsTrue(System.Enum.IsDefined(typeof(Abxr.StorageScope), Abxr.StorageScope.User));
    }
}

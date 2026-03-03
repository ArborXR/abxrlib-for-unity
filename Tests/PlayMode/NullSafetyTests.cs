// Copyright (c) 2026 ArborXR. All rights reserved.
// Verifies that all public Abxr static methods return safe defaults and never
// throw when AbxrSubsystem has not been initialised in the scene.
// These tests deliberately do NOT create a subsystem.
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class NullSafetyTests
{
    // No [SetUp] — we intentionally leave AbxrSubsystem.Instance as null.
    // Each test expects warnings from Abxr when not initialized; warnings do not fail tests.

    // ── Boolean defaults ──────────────────────────────────────────────────

    [Test]
    public void GetIsAuthenticated_ReturnsFalse()
        => Assert.IsFalse(Abxr.GetIsAuthenticated());

    [Test]
    public void IsQRScanForAuthAvailable_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsQRScanForAuthAvailable());

    [Test]
    public void IsAuthInputRequestPending_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsAuthInputRequestPending());

    [Test]
    public void IsQRScanCameraTexturePlaceable_ReturnsFalse()
        => Assert.IsFalse(Abxr.IsQRScanCameraTexturePlaceable());

    [Test]
    public void StartModuleAtIndex_ReturnsFalse()
        => Assert.IsFalse(Abxr.StartModuleAtIndex(0));

    // ── Null returns ──────────────────────────────────────────────────────

    [Test]
    public void GetUserData_ReturnsNull()
        => Assert.IsNull(Abxr.GetUserData());

    [Test]
    public void GetAuthResponse_ReturnsNull()
        => Assert.IsNull(Abxr.GetAuthResponse());

    [Test]
    public void GetModuleList_ReturnsNull()
        => Assert.IsNull(Abxr.GetModuleList());

    [Test]
    public void GetDeviceId_ReturnsNull()
        => Assert.IsNull(Abxr.GetDeviceId());

    [Test]
    public void GetDeviceSerial_ReturnsNull()
        => Assert.IsNull(Abxr.GetDeviceSerial());

    [Test]
    public void GetDeviceTitle_ReturnsNull()
        => Assert.IsNull(Abxr.GetDeviceTitle());

    [Test]
    public void GetDeviceTags_ReturnsNull()
        => Assert.IsNull(Abxr.GetDeviceTags());

    [Test]
    public void GetOrgId_ReturnsNull()
        => Assert.IsNull(Abxr.GetOrgId());

    [Test]
    public void GetOrgTitle_ReturnsNull()
        => Assert.IsNull(Abxr.GetOrgTitle());

    [Test]
    public void GetOrgSlug_ReturnsNull()
        => Assert.IsNull(Abxr.GetOrgSlug());

    [Test]
    public void GetMacAddressFixed_ReturnsNull()
        => Assert.IsNull(Abxr.GetMacAddressFixed());

    [Test]
    public void GetMacAddressRandom_ReturnsNull()
        => Assert.IsNull(Abxr.GetMacAddressRandom());

    [Test]
    public void GetAccessToken_ReturnsNull()
        => Assert.IsNull(Abxr.GetAccessToken());

    [Test]
    public void GetRefreshToken_ReturnsNull()
        => Assert.IsNull(Abxr.GetRefreshToken());

    [Test]
    public void GetExpiresDateUtc_ReturnsNull()
        => Assert.IsNull(Abxr.GetExpiresDateUtc());

    [Test]
    public void GetFingerprint_ReturnsNull()
        => Assert.IsNull(Abxr.GetFingerprint());

    [Test]
    public void GetSuperMetaData_ReturnsNull()
        => Assert.IsNull(Abxr.GetSuperMetaData());

    [Test]
    public void GetQRScanCameraTexture_ReturnsNull()
        => Assert.IsNull(Abxr.GetQRScanCameraTexture());

    // ── Void methods do not throw ────────────────────────────────────────

    [Test]
    public void StartAuthentication_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.StartAuthentication());

    [Test]
    public void ReAuthenticate_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.ReAuthenticate());

    [Test]
    public void Event_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Event("test_event"));

    [Test]
    public void Log_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Log("test log message"));

    [Test]
    public void LogDebug_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.LogDebug("debug"));

    [Test]
    public void LogInfo_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.LogInfo("info"));

    [Test]
    public void LogWarn_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.LogWarn("warn"));

    [Test]
    public void LogError_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.LogError("error"));

    [Test]
    public void LogCritical_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.LogCritical("critical"));

    [Test]
    public void Telemetry_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Telemetry("frame_rate", new Abxr.Dict().With("fps", "90")));

    [Test]
    public void TrackAutoTelemetry_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.TrackAutoTelemetry());

    [Test]
    public void Register_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Register("key", "value"));

    [Test]
    public void RegisterOnce_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.RegisterOnce("key", "value"));

    [Test]
    public void Unregister_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Unregister("key"));

    [Test]
    public void Reset_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Reset());

    [Test]
    public void SetDeviceId_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.SetDeviceId("device-id"));

    [Test]
    public void SetOrgId_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.SetOrgId("org-id"));

    [Test]
    public void SetAuthSecret_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.SetAuthSecret("secret"));

    [Test]
    public void StorageSetDefaultEntry_DoesNotThrow()
        => Assert.DoesNotThrow(() =>
            Abxr.StorageSetDefaultEntry(new Dictionary<string, string>(), Abxr.StorageScope.Device));

    [Test]
    public void StorageRemoveDefaultEntry_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.StorageRemoveDefaultEntry());

    [Test]
    public void StorageRemoveMultipleEntries_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.StorageRemoveMultipleEntries());

    [Test]
    public void EventAssessmentStart_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventAssessmentStart("test"));

    [Test]
    public void EventAssessmentComplete_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventAssessmentComplete("test", 100));

    [Test]
    public void EventObjectiveStart_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventObjectiveStart("objective"));

    [Test]
    public void EventObjectiveComplete_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventObjectiveComplete("objective", 80));

    [Test]
    public void EventInteractionStart_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventInteractionStart("interaction"));

    [Test]
    public void EventInteractionComplete_DoesNotThrow()
        => Assert.DoesNotThrow(() =>
            Abxr.EventInteractionComplete("interaction", Abxr.InteractionType.Select, Abxr.InteractionResult.Correct));

    [Test]
    public void EventLevelStart_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventLevelStart("level_1"));

    [Test]
    public void EventLevelComplete_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventLevelComplete("level_1", 90));

    [Test]
    public void EventCritical_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventCritical("safety_check"));

    [Test]
    public void EventExperienceStart_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventExperienceStart("orientation"));

    [Test]
    public void EventExperienceComplete_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.EventExperienceComplete("orientation"));

    [Test]
    public void StartNewSession_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.StartNewSession());

    [Test]
    public void StartTimedEvent_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.StartTimedEvent("event"));

    [Test]
    public void OnInputSubmitted_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.OnInputSubmitted("1234"));

    [Test]
    public void CancelQRScanForAuthInput_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.CancelQRScanForAuthInput());

    // ── AuthUIFollowCamera reads Configuration (creates default if needed) ─

    [Test]
    public void AuthUIFollowCamera_DoesNotThrow()
        => Assert.DoesNotThrow(() => { var _ = Abxr.AuthUIFollowCamera; });
}

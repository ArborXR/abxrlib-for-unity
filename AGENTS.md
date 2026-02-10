# abxrlib-for-unity – AI instructions

Team-wide instructions are duplicated in sibling ArborXR repos (abxrlib-for-unreal, abxrlib-for-webxr); keep shared guidelines in sync when updating.

---

## Overview

This repo is the **ABXRLib SDK for Unity**: a C# Unity package for analytics, authentication, and data collection in XR applications. It can operate in two modes:

1. **Standalone** – The app sends data and auth requests directly to ArborXR Insights (or another backend) over the network. Works on all platforms (Editor, Android, WebGL, etc.).

2. **With ArborInsightService (Android only)** – On Android VR devices, the app can bind to the **ArborInsightService** APK. Auth and analytics are then offloaded to that device-side service; the Unity SDK talks to it via a Java bridge in the **client AAR** (e.g. `insights-client-service.aar`). This repo is a **consumer** of that AAR and of the service APK—they are supplied separately and are not built in this repo.

Related:

- **ArborInsightService** – Android service APK + client AAR. When the APK is installed and the matching AAR is in `Plugins/Android/`, this SDK’s `ArborInsightServiceClient` binds to the service.
- **abxrlib-for-unity-demo-app** – Example Unity project that uses this package and optionally the device service on Android.

## Architecture Flow

### Standalone (no device service)

```
Unity App
    ↓
AbxrLib (this package): Authentication.cs, DataBatcher, StorageBatcher, Telemetry
    ↓
HTTP/REST → ArborXR Insights (or custom backend)
```

Config and credentials come from `Configuration.Instance` (appID, orgID, authSecret) or from ArborXR SDK when connected (`GetConfigData()` / `GetArborData()`).

### With ArborInsightService (Android)

```
Unity App
    ↓
AbxrLib: Authentication.cs, ArborInsightServiceClient (C#)
    ↓
ArborInsightServiceBridge → UnityArborInsightServiceClient.java (in AAR)
    ↓
AIDL → ArborInsightService.kt (separate APK)
```

- **Initialize:** `Initialize.OnBeforeSceneLoad()` attaches `ArborServiceClient` and `ArborInsightServiceClient` only when `UNITY_ANDROID && !UNITY_EDITOR`. The bridge calls the Java client’s `bind()`; `Authentication.cs` polls `ServiceIsFullyInitialized()` (up to 40 × 250 ms) before proceeding with auth.
- **Auth/config:** When the service is used, auth runs through the service (same flow as standalone: first auth, then config, then optional second auth with user input). Data sources: `GetConfigData()` from Unity `Configuration.Instance`, `GetArborData()` can override with ArborXR SDK values when connected.

### Initialization (this package)

- **Runtime:** `Initialize.cs` uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to attach core components: `CoroutineRunner`, `DeviceModel`, `KeyboardHandler`, `Authentication`, `ExitPollHandler`, `SceneChangeDetector`, `DataBatcher`, `StorageBatcher`, `TrackSystemInfo`, `ApplicationQuitHandler`; on Android build, also `ArborServiceClient`, `ArborInsightServiceClient`, `HeadsetDetector`, and optionally `TrackInputDevices` and platform QR readers (Pico/Meta).
- **Service readiness:** On Android, auth waits for `ArborInsightServiceClient.ServiceIsFullyInitialized()` before sending auth requests to the service. Init and readiness are handled by the client AAR and the service APK; this package only polls until ready.

## Key Files

- **Entry / config:** `Runtime/Core/Initialize.cs`, `Runtime/Core/Configuration.cs`, `Runtime/Core/Abxr.cs`
- **Auth:** `Runtime/Authentication/Authentication.cs` (device/user auth, session, GetConfigData/GetArborData, optional keyboard UI)
- **Service client (Android):** `Runtime/ServiceClient/ArborInsightServiceClient.cs`, `Runtime/ServiceClient/ArborInsightService/ArborInsightServiceBridge` (JNI to Java), `Runtime/ServiceClient/ArborServiceClient.cs`
- **Data / storage / telemetry:** `Runtime/Data/DataBatcher.cs`, `Runtime/Storage/StorageBatcher.cs`, `Runtime/Telemetry/` (TrackInputDevices, TrackObject, TrackSystemInfo)
- **UI:** `Runtime/UI/` (ExitPoll, Keyboard, DebugWindow, HandTrackingButtonSystem, etc.)
- **Plugins:** `Plugins/Android/` (client AAR, e.g. `insights-client-service.aar`, containing the UnityArborInsightServiceClient Java bridge; supplied separately, not built in this repo)

## Configuration and Data Sources

- **Configuration.Instance** – ScriptableObject from `Resources/AbxrLib.asset`. Holds appID, orgID, authSecret, buildType (production/development), REST URLs, telemetry/batching options, UI prefab references.
- **GetConfigData()** (in Authentication) – Reads appID, orgID, authSecret from Configuration.
- **GetArborData()** – Can override with ArborXR SDK values when connected (`ArborServiceClient.IsConnected()`, `Abxr.GetOrgId()`, `Abxr.GetFingerprint()`, deviceId, deviceTags). If Configuration has values, they take precedence; deviceId/deviceTags still come from ArborXR SDK when available.

## How This Repo Works With Other Projects

- **ArborInsightService:** This repo consumes the **client AAR** and (on device) the **service APK**. Both are supplied externally. On Android, `ArborInsightServiceClient` binds to the service via the AAR’s Java bridge. Do not assume access to the service’s source repo; guidance in this file is from the consumer’s perspective only.
- **abxrlib-for-unity-demo-app:** Consumes this package (git URL or local); demonstrates integration and optional use of the device service on Android when the AAR and APK are available.

## Build and Usage (for app developers)

- **Install:** Add package from git URL `https://github.com/ArborXR/abxrlib-for-unity.git` (Package Manager → Add package from git URL). See README for configuration and quick start.
- **Android + device service:** Obtain and install the ArborInsightService APK from your distribution channel. Ensure `Plugins/Android/` includes the **matching client AAR** (e.g. `insights-client-service.aar`) from the same source.

## Troubleshooting

- **Auth fails / wrong orgId or authSecret:** Confirm Unity Configuration (AbxrLib.asset) and buildType; remember GetArborData() can override with ArborXR SDK when connected.
- **Empty app_id / org_id / auth_secret or HTTP 422:** Check GetConfigData()/GetArborData() and that Configuration or ArborXR SDK is providing values; inspect auth payload in logs.
- **Service “not ready” or bind fails on Android:** Ensure the ArborInsightService APK is installed and the client AAR in `Plugins/Android/` matches that service version. Check logcat for `ArborInsightServiceClient` and `AbxrLib:`.
- **Missing AAR:** If you see “bridge not initialized” or “AAR may be missing”, add the client AAR (e.g. `insights-client-service.aar`) to `Plugins/Android/`. Obtain it from your distribution channel; it is not built in this repo.

## Technical Notes

- Default execution order: `Authentication` uses `[DefaultExecutionOrder(1)]` so it runs early.
- Android-only code is behind `#if UNITY_ANDROID && !UNITY_EDITOR` (service client, HeadsetDetector, etc.); Editor and WebGL use standalone path only.
- Authentication flow with the service mirrors the standalone flow: first auth, then config, then optional second auth with user input (same session, same endpoint).

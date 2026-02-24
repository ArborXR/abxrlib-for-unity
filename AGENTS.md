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
AbxrLib (this package): AbxrManager, AbxrAuthService, AbxrDataService, AbxrStorageService, AbxrTelemetryService
    ↓
HTTP/REST → ArborXR Insights (or custom backend)
```

Config and credentials come from `Configuration.Instance` (appID, orgID, authSecret) or from ArborXR SDK when connected (`GetConfigData()` / `GetArborData()`).

### With ArborInsightService (Android)

```
Unity App
    ↓
AbxrLib: AbxrAuthService, ArborInsightServiceClient (C#)
    ↓
ArborInsightServiceBridge → client AAR (e.g. insights-client-service.aar)
    ↓
AIDL → ArborInsightService (separate APK)
```

- **Initialize:** `Initialize.OnBeforeSceneLoad()` attaches `ArborServiceClient` and `ArborInsightServiceClient` only when `UNITY_ANDROID && !UNITY_EDITOR`. In `AbxrSubsystem.Awake()`, the bridge’s Init and Bind are started **before** creating `AbxrAuthService`, so the scene can load without blocking. Auth runs in a coroutine that waits for `ServiceIsFullyInitialized()` (up to 40 × 0.25 s) only when the ArborInsightService APK is installed; when ready, auth payload (including appToken/orgToken when useAppTokens) is pushed to the service and `AuthRequest()` is called. If the service is not installed, `IsServicePackageInstalled()` is false and the app uses the built-in REST path immediately.
- **Auth/config:** When the service is used, auth runs through the service (same flow as standalone: first auth, then config, then optional second auth with user input). Data sources: `GetConfigData()` from Unity `Configuration.Instance`, `GetArborData()` can override with ArborXR SDK values when connected.

### Initialization (this package)

- **Runtime:** `Initialize.cs` uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to attach core components including `AbxrManager`, `AbxrAuthService`, data/telemetry services, `DeviceModel`, UI (ExitPoll, Keyboard, etc.); on Android build, also `ArborServiceClient`, `ArborInsightServiceClient`, `HeadsetDetector`, and optionally platform QR readers (Pico/Meta).
- **Service readiness:** On Android, bind is started early in Awake (before creating the auth service). Auth runs in a coroutine and waits for `ServiceIsFullyInitialized()` (non-blocking, up to 40 × 0.25 s) only when `IsServicePackageInstalled()` is true, so the scene keeps loading. Init and readiness are handled by the client AAR and the service APK.

## Key Files

- **Entry / config:** `Runtime/Core/Initialize.cs`, `Runtime/Core/Configuration.cs`, `Runtime/Abxr.cs`, `Runtime/AbxrManager.cs`
- **Auth:** `Runtime/Services/Auth/AbxrAuthService.cs` (device/user auth, session, GetConfigData/GetArborData, appToken/orgToken, optional keyboard UI; on Android pushes payload to service and calls AuthRequest when service is ready)
- **Service client (Android):** `Runtime/Services/Platform/ArborInsightServiceClient.cs` (ArborInsightServiceBridge in same file), `Runtime/Services/Platform/ArborServiceClient.cs`
- **Data / storage / telemetry:** `Runtime/Services/Data/AbxrDataService.cs`, `Runtime/Services/Data/AbxrStorageService.cs`, `Runtime/Services/Telemetry/AbxrTelemetryService.cs`, `Runtime/Services/Telemetry/TrackObject.cs`
- **UI:** `Runtime/UI/` (ExitPoll, Keyboard, DebugWindow, HandTrackingButtonSystem, etc.)
- **Plugins:** `Plugins/Android/` (client AAR, e.g. `insights-client-service.aar`, containing the client bridge used by ArborInsightServiceClient; supplied separately, not built in this repo)

## Configuration and Data Sources

- **Configuration.Instance** – ScriptableObject from `Resources/AbxrLib.asset`. Holds appID, orgID, authSecret, buildType (production/development/production_custom), useAppTokens, appToken, orgToken, REST URLs, telemetry/batching options, UI prefab references.
- **Auth modes:** When **Use App Tokens** is on: single appToken (required) and optional orgToken; buildType production_custom requires orgToken for single-customer builds; API receives appToken + orgToken. When off (legacy): appID, orgID, authSecret; GetArborData() can override with ArborXR SDK when connected.
- **GetConfigData()** (in AbxrAuthService) – Uses `Utils.ExtractConfigData()` to populate payload (appToken/orgToken/buildType or appID/orgID/authSecret).
- **GetArborData()** – When useAppTokens and not production_custom: builds dynamic org token via `Utils.BuildOrgTokenDynamic(GetOrgId(), GetFingerprint())`. Legacy: overrides orgID, authSecret from ArborXR SDK. deviceId/deviceTags from ArborXR when available.
- **Production (Custom APK):** buildType production_custom; requires org token (or legacy orgID+authSecret); API receives buildType "production"; Android manifest injects build_type "production".

## How This Repo Works With Other Projects

- **ArborInsightService:** This repo consumes the **client AAR** and (on device) the **service APK**. Both are supplied externally. On Android, `ArborInsightServiceClient` binds to the service via the AAR’s Java bridge. Do not assume access to the service’s source repo; guidance in this file is from the consumer’s perspective only.
- **abxrlib-for-unity-demo-app:** Consumes this package (git URL or local); demonstrates integration and optional use of the device service on Android when the AAR and APK are available.

## Build and Usage (for app developers)

- **Install:** Add package from git URL `https://github.com/ArborXR/abxrlib-for-unity.git` (Package Manager → Add package from git URL). See README for configuration and quick start.
- **Android + device service:** Obtain and install the ArborInsightService APK from your distribution channel. Ensure `Plugins/Android/` includes the **matching client AAR** (e.g. `insights-client-service.aar`) from the same source.

## Input request (auth keyboard/PIN pad)

- When auth needs user input, the SDK invokes **OnInputRequested** (AuthMechanism + submitValue callback). The SDK assigns its **internal keyboard handler** (PresentKeyboard) first, so the native keyboard—including when the user replaces prefabs in Configuration—uses the exact same flow. Only one handler is allowed at a time; use **assignment** (`=`), not subscribe (`+=`). If the app assigns its own handler, it replaces the default; if no handler is assigned, the default shows the keyboard/PIN pad from config or built-in prefabs.
- **PresentKeyboard / PresentPinPad** are not public; there is no public API to show the keyboard directly. For custom UI: (1) set keyboard/PIN pad prefabs in Configuration and do not assign OnInputRequested, or (2) **assign** **OnInputRequested** and call the provided **submitValue** callback with the user’s input. See developer portal docs on custom prefabs for details.

## Troubleshooting

- **Auth fails / wrong orgId or authSecret:** Confirm Unity Configuration (AbxrLib.asset) and buildType; remember GetArborData() can override with ArborXR SDK when connected.
- **Empty app_id / org_id / auth_secret or HTTP 422:** Check GetConfigData()/GetArborData() and that Configuration or ArborXR SDK is providing values; inspect auth payload in logs.
- **App/org token validation (useAppTokens):** Ensure appToken and orgToken are valid JWTs (three dot-separated segments). For production_custom, orgToken is required. Development can use app token as org token if org token is empty.
- **Service “not ready” or bind fails on Android:** Ensure the ArborInsightService APK is installed and the client AAR in `Plugins/Android/` matches that service version (AAR must support set_OrgToken when using app tokens). Check logcat for `ArborInsightServiceClient` and `AbxrLib:`.
- **Missing AAR:** If you see “bridge not initialized” or “AAR may be missing”, add the client AAR (e.g. `insights-client-service.aar`) to `Plugins/Android/`. Obtain it from your distribution channel; it is not built in this repo.

## Technical Notes

- Default execution order: `Authentication` uses `[DefaultExecutionOrder(1)]` so it runs early.
- Android-only code is behind `#if UNITY_ANDROID && !UNITY_EDITOR` (service client, HeadsetDetector, etc.); Editor and WebGL use standalone path only.
- Authentication flow with the service mirrors the standalone flow: first auth, then config, then optional second auth with user input (same session, same endpoint).

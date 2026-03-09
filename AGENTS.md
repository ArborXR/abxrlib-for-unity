# abxrlib-for-unity – AI instructions

Team-wide instructions are duplicated in sibling ArborXR repos (abxrlib-for-unreal, abxrlib-for-webxr); keep shared guidelines in sync when updating.

---

## Overview

This repo is the **ABXRLib SDK for Unity**: a C# Unity package for analytics, authentication, and data collection in XR applications. It can operate in two modes:

1. **Standalone** – The app sends data and auth requests directly to ArborXR Insights (or another backend) over the network. Works on all platforms (Editor, Android, WebGL, etc.).

2. **With ArborInsightsClient (Android only)** – On Android VR devices, the app can bind to the **ArborInsightsClient** APK. Auth and analytics are then offloaded to that device-side service; the Unity SDK talks to it via a Java bridge in the **client AAR** (e.g. `insights-client-service.aar`). This repo is a **consumer** of that AAR and of the service APK—they are supplied separately and are not built in this repo.

Related:

- **ArborInsightsClient** – Android service APK + client AAR. When the APK is installed and the matching AAR is in `Plugins/Android/`, this SDK’s `ArborInsightsClient` binds to the service.
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

### With ArborInsightsClient (Android)

```
Unity App
    ↓
[AbxrLib] AbxrAuthService, ArborInsightsClient (C#)
    ↓
ArborInsightsServiceBridge → client AAR (e.g. insights-client-service.aar)
    ↓
AIDL → ArborInsightsClient (separate APK)
```

- **Initialize:** `Initialize.OnBeforeSceneLoad()` attaches `ArborMdmClient` and `ArborInsightsClient` only when `UNITY_ANDROID && !UNITY_EDITOR`. In `AbxrSubsystem.Awake()`, the bridge’s Init and Bind are started **before** creating `AbxrAuthService`, so the scene can load without blocking. Auth runs in a coroutine that waits for `ServiceIsFullyInitialized()` (up to 40 × 0.25 s) only when the ArborInsightsClient APK is installed; when ready, auth payload (including appToken/orgToken when useAppTokens) is pushed to the service and `AuthRequest()` is called. If the service is not installed, `IsServicePackageInstalled()` is false and the app uses the built-in REST path immediately.
- **Auth/config:** When the service is used, auth runs through the service (same flow as standalone: first auth, then config, then optional second auth with user input). Data sources: `GetConfigData()` from Unity `Configuration.Instance`, `GetArborData()` can override with ArborXR SDK values when connected.

### Transport abstraction

- **IAbxrTransport** – Single abstraction for sending auth, config, data (events/telemetry/logs), and storage. Two implementations: **AbxrTransportRest** (UnityWebRequest, queues and batch POST) and **AbxrTransportArborInsights** (wraps ArborInsightsClient; Android only).
- **Subsystem** holds `_transport` (volatile for cross-thread visibility when callers use the getter from another thread). At Awake it is set to `AbxrTransportRest`. On Android when `enableArborInsightsClient`, a coroutine waits for `ArborInsightsClient.ServiceIsFullyInitialized()` (e.g. 40 × 0.25 s); when ready, **SwitchToArborInsightsTransport()** replaces `_transport` with `AbxrTransportArborInsights`. Auth, data, and storage services receive a getter so they always use the current transport.
- **First auth gated on transport selection:** If "Enable auto start authentication" is on, the first auth does not run until the transport-selection coroutine has finished (swap to service or leave REST). So the first auth always uses the correct backend.
- **Credentials:** Token/Secret and `Abxr.GetAuthResponse()` remain in `AbxrAuthService`; `AbxrTransportRest` gets auth headers via `AbxrAuthService.SetAuthHeaders()`.

### Initialization (this package)

- **Runtime:** `Initialize.cs` uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to attach core components including `AbxrManager`, `AbxrAuthService`, data/telemetry services, `DeviceModel`, UI (ExitPoll, Keyboard, etc.); on Android build, also `ArborMdmClient`, `ArborInsightsClient`, `HeadsetDetector`, and optionally platform QR readers (Pico/Meta).
- **Transport selection:** In `AbxrSubsystem.Awake`, `_transport` is set to `AbxrTransportRest`. On Android when `enableArborInsightsClient`, a coroutine waits for `ServiceIsFullyInitialized()` (up to 40 × 0.25 s); when ready, the subsystem switches to `AbxrTransportArborInsights`. Auto-start auth is gated on this selection so the first auth always uses the chosen transport.
- **Service readiness:** On Android, bind is started early in Awake (before creating the auth service). Init and readiness are handled by the client AAR and the service APK.

## Key Files

- **Entry / config:** `Runtime/Core/Initialize.cs`, `Runtime/Core/Configuration.cs`, `Runtime/Abxr.cs`, `Runtime/AbxrManager.cs`
- **Auth:** `Runtime/Services/Auth/AbxrAuthService.cs` (device/user auth, session, GetConfigData/GetArborData, appToken/orgToken, optional keyboard UI; uses current transport for auth/config)
- **Transport:** `Runtime/Services/Transport/IAbxrTransport.cs`, `AbxrTransportRest.cs`, `AbxrTransportArborInsights.cs` (abstraction for REST vs ArborInsightsClient)
- **Service client (Android):** `Runtime/Services/Platform/ArborInsightsClient.cs` (ArborInsightsServiceBridge in same file), `Runtime/Services/Platform/ArborMdmClient.cs`
- **Data / storage / telemetry:** `Runtime/Services/Data/AbxrDataService.cs`, `Runtime/Services/Data/AbxrStorageService.cs` (forward to current transport), `Runtime/Services/Telemetry/AbxrTelemetryService.cs`, `Runtime/Services/Telemetry/TrackObject.cs`
- **UI:** `Runtime/UI/` (ExitPoll, Keyboard, DebugWindow, HandTrackingButtonSystem, etc.)
- **Plugins:** `Plugins/Android/` (client AAR, e.g. `insights-client-service.aar`, containing the client bridge used by ArborInsightsClient; supplied separately, not built in this repo)

## Configuration and Data Sources

- **Configuration.Instance** – ScriptableObject from `Resources/AbxrLib.asset`. Holds appID, orgID, authSecret, buildType (production/development/production_custom), useAppTokens, appToken, orgToken, REST URLs, telemetry/batching options, UI prefab references.
- **Auth modes:** When **Use App Tokens** is on: single appToken (required) and optional orgToken; buildType production_custom requires orgToken for single-customer builds; API receives appToken + orgToken. When off (legacy): appID, orgID, authSecret; GetArborData() can override with ArborXR SDK when connected.
- **GetConfigData()** (in AbxrAuthService) – Uses `Utils.ExtractConfigData()` to populate payload (appToken/orgToken/buildType or appID/orgID/authSecret).
- **GetArborData()** – When useAppTokens and not production_custom: builds dynamic org token via `Utils.BuildOrgTokenDynamic(GetOrgId(), GetFingerprint())`. Legacy: overrides orgID, authSecret from ArborXR SDK. deviceId/deviceTags from ArborXR when available.
- **Production (Custom APK):** buildType production_custom; requires org token (or legacy orgID+authSecret); API receives buildType "production"; Android manifest injects build_type "production".

## How This Repo Works With Other Projects

- **ArborInsightsClient:** This repo consumes the **client AAR** and (on device) the **service APK**. Both are supplied externally. On Android, `ArborInsightsClient` binds to the service via the AAR’s Java bridge. The device service runs as package **`app.xrdi.client`** with component **`InsightsService`**. Do not assume access to the service’s source repo; guidance in this file is from the consumer’s perspective only.
- **abxrlib-for-unity-demo-app:** Consumes this package (git URL or local); demonstrates integration and optional use of the device service on Android when the AAR and APK are available.

## Build and Usage (for app developers)

- **Install:** Add package from git URL `https://github.com/ArborXR/abxrlib-for-unity.git` (Package Manager → Add package from git URL). See README for configuration and quick start.
- **Android + device service:** Obtain and install the ArborInsightsClient APK from your distribution channel. Ensure `Plugins/Android/` includes the **matching client AAR** (e.g. `insights-client-service.aar`) from the same source.

## Input request (auth keyboard/PIN pad)

- When auth needs user input, the SDK invokes **OnInputRequested** with four strings: **type** (`"text"` | `"assessmentPin"` | `"email"`), **prompt**, **domain**, **error**. The SDK assigns its **internal keyboard handler** (PresentKeyboard) first, so the native keyboard—including when the user replaces prefabs in Configuration—uses the exact same flow. Only one handler is allowed at a time; use **assignment** (`=`), not subscribe (`+=`). If the app assigns its own handler, it replaces the default; if no handler is assigned, the default shows the keyboard/PIN pad from config or built-in prefabs.
- **Custom UI:** (1) Set keyboard/PIN pad prefabs in Configuration and do not assign OnInputRequested, or (2) assign **`Abxr.OnInputRequested`** with a handler `(string type, string prompt, string domain, string error)`. Show your UI using type/prompt/domain/error; when the user submits, call **`Abxr.OnInputSubmitted(enteredValue)`**. The **error** parameter is empty on first request and may contain a previous failure message on retry (e.g. invalid PIN). Set the handler to `null` in `OnDestroy()` when your component is no longer responsible. See developer portal docs on custom prefabs for details.
- **QR scanner (custom auth UI):** Use **`Abxr.IsQRScanForAuthAvailable()`** to show/hide a "Scan QR" option. Use **`Abxr.StartQRScanForAuthInput(Action<string> onResult)`** to start a scan; when the user scans or cancels, the callback runs with the extracted PIN (e.g. from `ABXR:123456`) or `null`. Close your auth UI and, if `value != null`, call **`Abxr.OnInputSubmitted(value)`**. Call **`Abxr.CancelQRScanForAuthInput()`** to stop a scan and invoke the callback with `null`. On Meta/Quest, **`Abxr.GetQRScanCameraTexture()`** returns the camera texture so you can assign it to a RawImage in your own parent and control layout.
- **Auth Handoff Launcher:** In **Advanced ArborXR Settings**, **Enable Auth Handoff Launcher** (`enableLearnerLauncherMode`, default false). When enabled, after config is received the SDK overrides the auth mechanism: if the API’s authMechanism type is not already **assessmentPin**, it is set to **assessmentPin** with prompt **"Enter your 6-digit PIN"**. Use when a launcher collects PIN before **`Abxr.StartAuthentication()`**, registers an **OnInputRequested** handler that does nothing (to suppress the built-in keyboard), then calls **StartAuthentication()** and **`Abxr.OnInputSubmitted(enteredValue)`** when ready. The normal flow then runs (OnInputRequested is invoked with type pin; app submits via OnInputSubmitted).

## Troubleshooting

- **Auth fails / wrong orgId or authSecret:** Confirm Unity Configuration (AbxrLib.asset) and buildType; remember GetArborData() can override with ArborXR SDK when connected.
- **Empty app_id / org_id / auth_secret or HTTP 422:** Check GetConfigData()/GetArborData() and that Configuration or ArborXR SDK is providing values; inspect auth payload in logs.
- **App/org token validation (useAppTokens):** Ensure appToken and orgToken are valid JWTs (three dot-separated segments). For production_custom, orgToken is required. Development can use app token as org token if org token is empty.
- **Service “not ready” or bind fails on Android:** Ensure the ArborInsightsClient APK is installed and the client AAR in `Plugins/Android/` matches that service version (AAR must support set_OrgToken when using app tokens). Check logcat for `ArborInsightsClient`, `[AbxrLib]`, and the service package `app.xrdi.client`.
- **Missing AAR:** If you see “bridge not initialized” or “AAR may be missing”, add the client AAR (e.g. `insights-client-service.aar`) to `Plugins/Android/`. Obtain it from your distribution channel; it is not built in this repo.

## StartNewSession and EndSession

- **`Abxr.StartNewSession()`** starts an entirely fresh session, equivalent to the user closing the app and reopening it from a session perspective. It: (1) clears all pending events, telemetry, logs, and storage from the in-memory batchers; (2) on Android when using ArborInsightsClient, unbinds then rebinds the service so the connection and service-side session are fresh; (3) clears all auth state (tokens, ResponseData, user data, session id, _usedArborInsightsClientForSession); (4) assigns a new session ID and runs authentication again. The ArborInsightsClient removes the client session from its map in `onUnbind` (after flushing unsent data), so a new bind gets a new `ClientSession` and clean batcher on the service side.
- **`Abxr.EndSession()`** ends the current session without starting a new one. It calls the same internal handler as application quit: closes running events, flushes data (SendAll), calls transport OnQuit (REST: synchronous flush of pending data and storage; service: Unbind), clears pending batches/storage/super metadata and auth state. If the app later calls **`Abxr.StartAuthentication()`** while still using the ArborInsightsClient transport, the service transport checks `IsServiceBound()` and, if unbound (e.g. after EndSession), establishes a new bind and waits for the service to be ready before running auth. We still recommend **`Abxr.StartNewSession()`** when re-authenticating for a clean session; the rebind-on-auth path is for cases where the app does not call StartNewSession.

## Technical Notes

- Default execution order: `Authentication` uses `[DefaultExecutionOrder(1)]` so it runs early.
- Android-only code is behind `#if UNITY_ANDROID && !UNITY_EDITOR` (service client, HeadsetDetector, etc.); Editor and WebGL use standalone path only.
- Authentication flow with the service mirrors the standalone flow: first auth, then config, then optional second auth with user input (same session, same endpoint).

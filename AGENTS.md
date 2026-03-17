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
- **Auth/config:** When the service is used, auth runs through the service (same flow as standalone: device authentication, then config, then optional user authentication with user input). Data sources: `GetConfigData()` from Unity `Configuration.Instance`, `GetArborData()` can override with ArborXR SDK values when connected. **Device authentication** establishes the connection for the device; **user authentication** is when the API requests user identification (optional—the app can decline and continue as unidentified).

### Transport abstraction

- **IAbxrTransport** – Single abstraction for sending auth, config, data (events/telemetry/logs), and storage. Two implementations: **AbxrTransportRest** (UnityWebRequest, queues and batch POST) and **AbxrTransportArborInsights** (wraps ArborInsightsClient; Android only).
- **Subsystem** holds `_transport` (volatile for cross-thread visibility when callers use the getter from another thread). At Awake it is set to `AbxrTransportRest`. On Android when `enableArborInsightsClient`, a coroutine waits for `ArborInsightsClient.ServiceIsFullyInitialized()` (e.g. 40 × 0.25 s); when ready, **SwitchToArborInsightsTransport()** replaces `_transport` with `AbxrTransportArborInsights`. Auth, data, and storage services receive a getter so they always use the current transport.
- **Device auth gated on transport selection:** If "Enable auto start authentication" is on, device authentication does not run until the transport-selection coroutine has finished (swap to service or leave REST). So device auth always uses the correct backend.
- **Credentials:** Token/Secret and `Abxr.GetAuthResponse()` remain in `AbxrAuthService`; `AbxrTransportRest` gets auth headers via `AbxrAuthService.SetAuthHeaders()`.

### Initialization (this package)

- **Runtime:** `Initialize.cs` uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to attach core components including `AbxrManager`, `AbxrAuthService`, data/telemetry services, `DeviceModel`, UI (ExitPoll, Keyboard, etc.); on Android build, also `ArborMdmClient`, `ArborInsightsClient`, `HeadsetDetector`, and optionally platform QR readers (Pico/Meta).
- **Transport selection:** In `AbxrSubsystem.Awake`, `_transport` is set to `AbxrTransportRest`. On Android when `enableArborInsightsClient`, a coroutine waits for `ServiceIsFullyInitialized()` (up to 40 × 0.25 s); when ready, the subsystem switches to `AbxrTransportArborInsights`. Auto-start auth is gated on this selection so device authentication always uses the chosen transport.
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

## Session userId and userData

- **Session userId** is **read-only** on the client; only lib-backend sets or creates it (e.g. as an anonymized one-way hash of `userData.id`).
- **GetAnonymizedUserId()** returns the session userId from the last auth response. It is **not** always set: only when the backend includes `userId` in the response (e.g. after device auth or user auth). If the app never completes auth or the backend omits userId, it can be null.
- **GetUserId()** returns a single user identifier: `userData.id` when the backend returned it, otherwise **GetAnonymizedUserId()**, or **null** when neither is set. Use this when you need one display/reference id regardless of PII or whether the backend echoes userData.
- **SetUserId(id)** updates the primary user id (userData.id) and syncs to the API via SetUserData(id, null).
- **userData.id** is the **primary user identification** value; clients write via **SetUserId** (id only) or **SetUserData** (id plus additional fields).
- **SetUserData(id, additionalUserData)** updates only userData (sets `userData.id` when `id` is provided); it does not set session userId. Re-auth sends userData to the backend, which may return updated userId and userData; the SDK adopts both. **Protection:** If the app passes `id` that equals the current **GetAnonymizedUserId()**, the SDK does **not** send that `id` in the re-auth payload (to avoid the server hashing an already-hashed value).
- **GetUserData()** returns a copy of the auth response’s userData only (no session userId key). Use **GetAnonymizedUserId()** for the session userId (read-only; not for public documentation yet).
- Auth handoff includes **UserData** in the payload so the receiving app gets both userId and userData.

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

## Auth rejected by API (no retry)

When the **transport** reports that the API rejected authentication (e.g. **401**/**403** or explicit error in the response body), the SDK treats this as **credentials rejected** and does **not** retry. The auth service has **no knowledge of HTTP codes**; only the transport layer (REST or ArborInsightsClient) decides “auth rejected” and passes a boolean `isAuthRejectedByApi`. REST uses 401/403; the device client (AAR/service) uses the same rule and exposes `getLastAuthRejected()`. The auth service sets an internal flag so that for the rest of the session: (1) no further auth requests are sent to the API, and (2) any later call to `Abxr.StartAuthentication()` or auto-start auth will immediately report failure and no-op. The app continues to run without data collection (same as when credentials were missing locally). Transient failures (e.g. network unreachable, 5xx) are still retried. The flag is cleared only on **StartNewSession** or app restart.

## Troubleshooting

- **Auth fails / wrong orgId or authSecret:** Confirm Unity Configuration (AbxrLib.asset) and buildType; remember GetArborData() can override with ArborXR SDK when connected.
- **Empty app_id / org_id / auth_secret or HTTP 422:** Check GetConfigData()/GetArborData() and that Configuration or ArborXR SDK is providing values; inspect auth payload in logs.
- **App/org token validation (useAppTokens):** Ensure appToken and orgToken are valid JWTs (three dot-separated segments). For production_custom, orgToken is required. Development can use app token as org token if org token is empty.
- **Service “not ready” or bind fails on Android:** Ensure the ArborInsightsClient APK is installed and the client AAR in `Plugins/Android/` matches that service version (AAR must support set_OrgToken when using app tokens). Check logcat for `ArborInsightsClient`, `[AbxrLib]`, and the service package `app.xrdi.client`.
- **Missing AAR:** If you see “bridge not initialized” or “AAR may be missing”, add the client AAR (e.g. `insights-client-service.aar`) to `Plugins/Android/`. Obtain it from your distribution channel; it is not built in this repo.
- **Test Runner Player on Android: "No activity in the manifest with action MAIN and category LAUNCHER":** When running Play Mode tests on device (Run on device), Unity builds a "Player with Tests" APK. Sometimes the merged manifest for that build does not include a launcher activity, so auto-launch after install fails. Ensure the consuming project has a custom `AndroidManifest.xml` in `Assets/Plugins/Android/` that declares `UnityPlayerActivity` with `<action android:name="android.intent.action.MAIN" />` and `<category android:name="android.intent.category.LAUNCHER" />`. If the error persists, launch the app manually on the device (e.g. open the app from the device app list, or `adb shell am start -n <applicationId>/com.unity3d.player.UnityPlayerActivity`) so the test run can proceed; the Test Runner may still connect once the app is running.
- **Test Runner build uses a different package name:** When you use Test Runner → Run on device, the built APK has application ID **`com.UnityTestRunner.UnityTestRunner`** (or similar). That is the test-runner build only; it does not use your project’s Player Settings package name. For **production** builds always use **File → Build Settings → Build** (not Run on device). Before each production build, confirm **Edit → Project Settings → Player → Android → Other Settings → Package Name** is your real application ID; running tests on device can sometimes affect or overwrite it.
- **Connection to Android device failed: "Unable to reverse network traffic" / "Address already in use" (port 55504):** Unity uses host port 55504 for the Android connection and runs `adb reverse tcp:55504 tcp:55504`. Unity (or UnityShaderCompiler) can bind ports in the 5555–5585 range, conflicting with adb (see Unity issue UUM-1385). **Fixes to try:** (1) Quit Unity completely, then run `adb reverse --remove tcp:55504` (with `-s <device_id>` if needed) and `lsof -i :55504` to see what holds the port; quit that process and reopen Unity. (2) If it persists after a full reboot: **kill adb server** then start Unity so adb and port use start clean: `"<Unity install>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" kill-server` (then open Unity). (3) **Clear project Temp**: in the consuming project (e.g. demo-app), delete the `Temp` folder (and optionally `Library` for a full reimport), then reopen the project. (4) Ensure only one Unity Editor window is open and no other Unity processes (e.g. UnityShaderCompiler) are holding 55504; check with `lsof -i :55504` when the error appears.
- **Test Runner Play Mode on device (requires reverse):** To run Play Mode tests on a physical Android headset, the Editor needs port 55504 reversed so the player can report results. If you get "Unable to reverse network traffic" when the device is connected, try **(A) Pre-establish reverse with Unity closed:** Quit Unity, connect the device (reboot the device first so its port 55504 is free), then run `"<Unity>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" -s <device_id> reverse tcp:55504 tcp:55504`. If that succeeds, open Unity and use Run on device—the reverse may already be active so the Editor can receive results even if Unity still shows the warning. **(B)** If the reverse command fails with Unity closed, the conflict may be on the device; reboot the headset and run the reverse again before opening any app. **(C)** Ensure the consuming project has a custom `AndroidManifest.xml` with a launcher activity (see "Test Runner Player on Android" above) so the test player can launch.

## Return-to-launcher (returnToPackage handoff)

When **Enable returnTo Launcher** (`enableReturnTo`) is on and the session came from auth handoff, the app exits or returns the session to the app that launched it only when assessment is **fully complete**. That means: if there is no module sequence, after **EventAssessmentComplete()**; if there is a module sequence with auto-advance, only after the **last** module’s **EventAssessmentComplete()** (same rule as the original “exit after assessment” behavior). Exit and return-handoff are normalized to this single decision.

- **App 1 (launcher):** Call **`Abxr.LaunchAppWithAuthHandoff(packageName, includeReturnToPackage: true)`** so the handoff includes **ReturnToPackage** = this app’s package. The receiving app (App 2) can then “return” the session when assessment completes.
- **App 2 (assessment):** Receives handoff; **ReturnToPackage** is stored from the handoff JSON. When assessment is fully complete (see above) and `enableReturnTo` is true, **ExitAfterAssessmentComplete** runs: if **returnToPackage** is set, the SDK calls **`Abxr.LaunchAppWithAuthHandoff(returnToPackage, includeReturnToPackage: false)`** (sending the session back to App 1), then clears returnToPackage, then SendAll(), delay, quit. The handoff back to App 1 does **not** include ReturnToPackage, so App 1 does not get a return target and no loop occurs.
- **Clearing:** **returnToPackage** is cleared after use (**GetAndClearReturnToPackage()**); the handoff sent back to App 1 is built with `includeReturnToPackage: false`, so the chain stops at App 1.

| Step | App 1 (launcher) | App 2 (assessment) |
|------|------------------|--------------------|
| Build handoff | `LaunchAppWithAuthHandoff(app2Package, includeReturnToPackage: true)` → handoff includes `ReturnToPackage = Application.identifier` | — |
| Receive handoff | — | ParseAuthResponse sets `_returnToPackage` from JSON |
| Assessment complete + enableReturnTo | — | GetAndClearReturnToPackage(); LaunchAppWithAuthHandoff(returnTo, false); then SendAll(), delay, quit |
| Receive handoff back | ParseAuthResponse; no ReturnToPackage in JSON → no return target | — |

Config: **Configuration.enableReturnTo** (default true). When enabled, the app will exit or return the session to the launcher when assessment is fully complete (no module sequence, or after the last module’s EventAssessmentComplete()). Can be overridden at runtime via **RuntimeAuthConfig.enableReturnTo** (e.g. in tests via SetRuntimeAuthForTesting or NextRuntimeAuthConfigForTesting); the subsystem uses **GetEffectiveEnableReturnTo()** (auth service), which falls back to Configuration when the override is null.

## StartNewSession and EndSession

- **`Abxr.StartNewSession()`** starts an entirely fresh session, equivalent to the user closing the app and reopening it from a session perspective. It: (1) clears all pending events, telemetry, logs, and storage from the in-memory batchers; (2) on Android when using ArborInsightsClient, unbinds then rebinds the service so the connection and service-side session are fresh; (3) clears all auth state (tokens, ResponseData, user data, session id, _usedArborInsightsClientForSession); (4) assigns a new session ID and runs authentication again. The ArborInsightsClient removes the client session from its map in `onUnbind` (after flushing unsent data), so a new bind gets a new `ClientSession` and clean batcher on the service side.
- **`Abxr.EndSession()`** ends the current session without starting a new one. It calls the same internal handler as application quit: closes running events, flushes data (SendAll), calls transport OnQuit (REST: synchronous flush of pending data and storage; service: Unbind), clears pending batches/storage/super metadata and auth state. If the app later calls **`Abxr.StartAuthentication()`** while still using the ArborInsightsClient transport, the service transport checks `IsServiceBound()` and, if unbound (e.g. after EndSession), establishes a new bind and waits for the service to be ready before running auth. We still recommend **`Abxr.StartNewSession()`** when re-authenticating for a clean session; the rebind-on-auth path is for cases where the app does not call StartNewSession.

## Testing (Unity Test Runner)

Test assemblies are **AbxrLib.Tests.PlayMode** and **AbxrLib.Tests.EditMode** (each has its own asmdef under `Tests/PlayMode` and `Tests/EditMode`). There is no root `Tests.asmdef`; all test scripts live in those two assemblies.

Test-only APIs and hooks are **internal** and only visible to the Editor and Test assemblies via `Runtime/Core/InternalsVisibleTo.cs` (`AbxrLib.Editor`, `AbxrLib.Tests.EditMode`, `AbxrLib.Tests.PlayMode`). Consumer projects do not have `InternalsVisibleTo`, so production/app code cannot call them.

- **No test-only branching in production:** Test-only code runs only when tests invoke internal APIs (e.g. `SetRuntimeAuthForTesting`, `SimulateAuthSuccess`, `Configuration.ResetForTesting`, `AbxrSubsystem.ResetStaticStateForTesting`). In production those APIs are never called; flags like `_useInjectedRuntimeAuthForTesting` stay false and static test config stays null.
- **Same production code paths:** Validation, auth payload building, transport selection, and auth flow are identical; tests only inject config (or simulate auth success) so the same logic can be asserted without the network. The same `_runtimeAuth` and request-building code is used in both cases.
- **Test-only surface:** Auth: `SetRuntimeAuthForTesting`, `ApplyRuntimeAuthOverridesForTesting`, `ClearRuntimeAuthInjectionForTesting`, `SimulateAuthSuccess`. Subsystem: `NextRuntimeAuthConfigForTesting`, `ResetStaticStateForTesting`, `SimulateQuitInExitAfterAssessmentComplete`, `AuthServiceForTesting`, `DataServiceForTesting`, `RestTransportForTesting`, `GetTransportForTesting`, `GetPendingEventsForTesting`, `LaunchAppWithAuthHandoffForTest`, `GetHandoffJsonForTesting` (and logs/telemetry). Config: `ResetForTesting`. Unit Test Credentials (`unitTestConfigEnabled`, `unitTestAuth*`) are `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and excluded from production builds; they are available in Editor and in Development Builds (e.g. TestRunner on device). When `SimulateQuitInExitAfterAssessmentComplete` is true, `ExitAfterAssessmentComplete()` does not quit the app; it logs and ends the coroutine so PlayMode tests can finish.
- **Runtime auth overrides (no asset change):** `RuntimeAuthConfig` and the auth service support overriding these from Configuration for the current session: `enableAutoStartAuthentication`, `enableReturnTo`, `enableAutoStartModules`, `enableAutoAdvanceModules`. Set them on the config passed to `SetRuntimeAuth()` or `NextRuntimeAuthConfigForTesting`; the subsystem uses `GetEffective*` from the auth service so tests can force e.g. no auto-start modules or no return-to-launcher without editing the asset.
- **AuthMechanism (user authentication tests):** `RuntimeAuthConfig.authMechanism` is the single auth mechanism (type, prompt, domain). When null or empty type, it is filled from GET config when received; when set by tests before config is fetched, that value is used and not overwritten. Tests set `authMechanism` so each test gets the user authentication flow it needs (assessmentPin, email, text, none). Default prompts when type is set: email → "Enter your email address", text → "Enter your Employee ID"; assessmentPin uses "Enter your 6-digit PIN".

When adding or changing tests, avoid production code paths that branch on “is this a test?”; prefer injection/simulation so behavior matches production.

## Technical Notes

- Default execution order: `Authentication` uses `[DefaultExecutionOrder(1)]` so it runs early.
- Android-only code is behind `#if UNITY_ANDROID && !UNITY_EDITOR` (service client, HeadsetDetector, etc.); Editor and WebGL use standalone path only.
- Authentication flow with the service mirrors the standalone flow: first auth, then config, then optional second auth with user input (same session, same endpoint).

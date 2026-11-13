# ABXRLib SDK for Unity

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Quick Start](#quick-start)
5. [Full Documentation](#full-documentation)
6. [Support](#support)

---

## Introduction

### Overview

The **ABXRLib SDK for Unity** is an open-source analytics and data collection library that provides developers with the tools to collect and send XR data to any service of their choice. This library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

> **Quick Start:** Most developers can integrate ABXRLib SDK and log their first event in under **15 minutes**.

**Why Use ABXRLib SDK?**

- **Open-Source** & portable to any backend—no vendor lock-in  
- **Quick integration**—track user interactions in minutes  
- **Secure & scalable**—ready for enterprise use cases  
- **Pluggable with ArborXR Insights**—seamless access to LMS/BI integrations, session replays, AI diagnostics, and more

### Core Features

The ABXRLib SDK provides:
- **Event Tracking:** Monitor user behaviors, interactions, and system events.
- **Spatial & Hardware Telemetry:** Capture headset/controller movement and hardware metrics.
- **Object & System Info:** Track XR objects and environmental state.
- **Storage & Session Management:** Support resumable training and long-form experiences.
- **Logs:** Developer and system-level logs available across sessions.

### Backend Services

The ABXRLib SDK is designed to work with any backend service that implements the ABXR protocol. Currently supported services include:

#### ArborXR Insights
When paired with [**ArborXR Insights**](https://arborxr.com/insights), ABXR becomes a full-service platform offering:
- Seamless data pipeline from headset to dashboard
- End-to-end session tracking, analysis, and replay
- AI-driven insights for content quality, learner performance, and device usage
- One-click LMS and BI integrations for scalable deployments

#### Custom Implementations
Developers can implement their own backend services by following the ABXR protocol specification. This allows for complete control over data storage, processing, and visualization.

---

## Installation

### Unity Package Installation

1. Open Unity and go to `Window > Package Manager`.
2. Select the '+' dropdown and choose **'Add package from git URL'**.
3. Use the GitHub repo URL:
   ```
   https://github.com/ArborXR/abxrlib-for-unity.git
   ```
4. Once imported, you will see `Analytics for XR` in your Unity toolbar.
5. If Unity asks whether to enable the native platform backends for the new input system, either answer works. AbxrLib depends on the Input System package, which is why Unity asks, but AbxrLib itself reads no input through it, and its optional sign-in UI works with the Input System, the Input Manager (old), or both. **Yes** enables the backends and restarts the Editor; **No** leaves your input settings as they are. The Setup Wizard stays blank behind the prompt until you answer.

### Setup Wizard

When the package finishes installing, the **AbxrLib Setup** wizard opens on its own and walks through everything below:

- **Credentials** — the App Token, with validation as you paste. A project already on `Production (Custom APK)` or the legacy App ID scheme also gets the fields it uses.
- **Project setup** — Editor version, required packages, the optional sign-in UI, and the Android player settings a standalone headset build needs (each with a one-click fix).
- **First events** — the assessment calls that turn on grading dashboards and LMS reporting.

Reopen it any time from `Analytics for XR > Setup Wizard`. It edits the same `Assets/Resources/AbxrLib.asset` as `Analytics for XR > Configuration`, so either route works. The wizard saves the asset as you move between its pages. Values typed into Configuration stay in memory until you save the project (`File > Save Project`), like any Unity asset, so save before you commit the asset or start a command-line build. Uncheck **Open automatically after install or update** in the wizard footer to stop it appearing on its own, and it never opens in batch mode or CI.

### Diagnostics

When something is not working, `Analytics for XR > Diagnostics > Copy report` (or **Copy diagnostics** on the wizard's last page) puts a plain-text report on the clipboard to paste into a support request: AbxrLib and Unity versions, install source, Android player settings, detected headset SDKs, the configuration, the state of the sign-in UI, and the result of every wizard check. Tokens and the auth secret are never included; the report only says whether they are set and, for tokens, whether they look like one. By default only settings changed from their defaults are listed; tick **Include all config values** in the same menu (or the toggle next to the wizard button) to list every setting.

Building also repeats any blocking wizard finding as a Console warning, and warns when a `Development` build uses the App Token as the org token, since a copy that leaves your team reports to your organization. These warnings never fail a build. (Android builds still stop on an App or Organization Token that is set but malformed, as they did before.)

### Sign-in UI (optional)

The package itself contains no user interface. AbxrLib's built-in sign-in UI — world-space keyboard, PIN pad, exit polls, and QR-code scanning — is a **separate import**, so a project that does not use it never pulls in TextMeshPro, uGUI, or XR Interaction Toolkit.

**Import it** if you want AbxrLib to ask the user for a PIN or email itself:

- From the wizard: `Analytics for XR > Setup Wizard` → **Import world-space UI**, or
- From `Window > Package Manager` → **AbxrLib for Unity** → **Samples** → **World-Space UI** → **Import**

It lands in `Assets/Samples/AbxrLib for Unity/<version>/World-Space UI/`. The wizard's import also adds TextMeshPro if the project does not have it; a Package Manager import does not, and the wizard then offers to add it. Because it is imported into your project rather than resolved as a package, **updating AbxrLib does not update it** — the wizard flags the mismatch and offers a re-import (which overwrites, so keep your own changes elsewhere).

The prompts take taps and clicks only through an `EventSystem`, and a new scene has none. On a flat screen, if none is enabled when a prompt opens, AbxrLib adds one to the scene. Its input module is XR Interaction Toolkit's when version 2.1.1 or newer is installed, and otherwise the one your Active Input Handling supports. It unloads with the scene, and steps aside as soon as your own `EventSystem` is enabled. On a headset, your rig supplies it: XR Interaction Toolkit's ray interactors add one. The wizard notes when your launch scene has none and can add one.

**Skip it** if your app collects input itself. Handle `Abxr.OnInputRequested` and pass the value to `Abxr.OnInputSubmitted`; AbxrLib will not try to draw anything. Events, telemetry, logs, storage, and authentication all work without the UI. Two things do not: `Abxr.PollUser` drops its poll with a warning unless you register your own UI via `AbxrUi.RegisterPollUi`, and the "same user?" prompt shown when the headset is put back on (`Abxr.OnHeadsetPutOnNewSession`) is asked through that same poll UI — so without one, that callback never fires.

> **Upgrading from 2.0.10 or earlier?** Five changes to know about:
>
> 1. The sign-in UI used to ship inside the package. After updating, import it as above — otherwise an authentication request that needs a PIN or email has nothing to show unless your app handles `Abxr.OnInputRequested`. The wizard says so on its first page. AbxrLib also no longer depends on TextMeshPro, so if your project only had it through AbxrLib, add it yourself or import through the wizard.
> 2. `Abxr.PollUser()` now takes `PollType` from `AbxrLib.Runtime.Types` instead of `ExitPollHandler.PollType`. If you call it, add `using AbxrLib.Runtime.Types;` and drop the `ExitPollHandler.` prefix — the enum values are unchanged.
> 3. `Abxr.AIProxy` has been removed, along with `AIProxyApi` and `AIPromptPayload`. The backend endpoint it called is no longer served, so every call already returned `null`. Call your LLM provider directly with your own keys.
> 4. If your app handles `Abxr.OnInputRequested`, its `type` is now the value the backend sends: `assessmentPin` for a PIN (it was `pin`), `email`, or `text`. After a wrong entry, `error` is "Authentication Failed". Empty input is no longer rejected by the SDK; it goes to the backend like any other value.
> 5. If your code uses the QR readers directly, switch to the `Abxr` QR methods (`IsQRScanForAuthAvailable`, `StartQRScanForAuthInput`, `CancelQRScanForAuthInput`, `GetQRScanCameraTexture`), which kept their signatures. The readers moved into the sample's `AbxrLib.WorldSpace` assembly as `QRCodeReaderMeta` (formerly `QRCodeReader`) and `QRCodeReaderPico`, in `AbxrLib.Runtime.Core.QRScanner`, with a smaller API. QR sign-in on PICO now needs PICO Unity SDK 3.4 or newer.
>
> 3.0 also adds passcode pairing, `AbxrObserver`, and the `AbxrUi` registry and its interfaces. The [3.0.0 release notes](https://github.com/ArborXR/abxrlib-for-unity/releases/tag/v3.0.0) list the rest, including behavior changes such as device authentication retrying instead of giving up.
>
> One deprecation: `enableArborInsightsClient` is now `[Obsolete]`. The Insights device service it turns on is being removed, and from 3.1 the setting does nothing. Leave it as it is; nothing changes in 3.0.

---

## Configuration

### Using with ArborXR Insights

To use the ABXRLib SDK with ArborXR Insights, configure **app token** and **org token** (recommended). The same model applies to the [Unreal](https://github.com/ArborXR/abxrlib-for-unreal#configuration) and [WebXR](https://github.com/ArborXR/abxrlib-for-webxr#configuration) SDKs.

#### App token and org token (recommended)

#### Configure Unity Project
1. Open `Analytics for XR > Configuration` in the Unity Editor.
2. New projects authenticate with an App Token. A **Use App Tokens** checkbox shows only in a project that is on the [legacy scheme](#legacy-app-id--org-id--auth-secret) or still has legacy values saved: ticking it switches the project to App Tokens, and unticking it switches back.
3. Set **App Token** (required): JWT for your app, from your distribution channel or ArborXR portal (**Content Library** → **Managed** app → **Insights Hub**).
4. **Org Token:** New projects don't set one. `Production` uses the **dynamic org token** (derived from device/org context when available) or passcode pairing, and `Development` uses your **App Token** as the org token. The field shows only when the project already has an org token saved, or is already on `Production (Custom APK)`, where it is required.

**Development / testing:** Set **Build Type** to `Development` and set App Token. The app signs in as the organization that owns the app, in the Editor or on any device, so an App Token alone is enough to test with. Something else that supplies an organization is used first: an ArborXR-managed device (dynamic org token), then an org token supplied at runtime (`Abxr.SetOrgId` with `Abxr.SetAuthSecret`, the `org_token` intent extra or URL parameter). A project that already has an Org Token set for Development keeps using it: the field stays visible while it holds a value, and clearing it switches to the App Token. A Development build never shows [passcode pairing](#headsets-without-arborxr-mdm-passcode-pairing); test pairing with Build Type `Production`.

**Production builds:** Set App Token; the device or runtime provides org context (the dynamic org token on an ArborXR-managed device, or passcode pairing).

**Production (Custom APK):** A build type for single-customer APKs with one organization baked in. A project saved on it keeps the option, its Org Token field, and its builds; other projects are offered `Production` and `Development` only. The option follows the saved Build Type: switch such a project to another Build Type and the option is gone once Configuration or the wizard is reopened. To restore it, set `buildType: production_custom` in `Assets/Resources/AbxrLib.asset`. A customer's Org Token is not self-serve: contact ArborXR.

> **⚠️ Security Note:** Avoid compiling org tokens or long-lived secrets into builds distributed to third parties. For general distribution, use ArborXR-managed devices or dynamic org token. For single-customer deployments, follow your security guidelines.

#### When ArborXR is the MDM

On ArborXR-managed devices the organization is **not** configured — it is resolved at runtime from the device, and AbxrLib builds a dynamic org token from the device's org ID and fingerprint. Three settings matter:

| Setting | Value | Why |
|---|---|---|
| **Build Type** | `Production` | Each device reports to its own organization. |
| **Org Token** | not set | The device supplies it. Production shows the Org Token field only when the project has one saved, and ignores it either way; clear a saved one, since it still ships inside the build. In Development the device's value is used instead of the App Token or a saved org token. |
| **Enable ArborMdmClient** (Advanced) | on (the default) | This is what reads the org ID and fingerprint. With it off there is no organization to authenticate against, and auth fails with *"Organization identification unavailable"* — the wizard warns if it has been turned off. |

A project that is still on `Production (Custom APK)` should **not** use it for a managed fleet. It takes the organization from configuration by design and never consults the MDM, so every device in the build would report to the one org baked into it.

On the legacy scheme, the MDM supplies Org ID and Auth Secret at runtime, so only **App ID** needs setting.

#### Headsets without ArborXR MDM (passcode pairing)

On an Android headset or in a WebGL build with no ArborXR client and no org token, the app has no organization to report to. It runs as usual and stays quiet: no data is recorded or sent, and `OnAuthCompleted` fires once with `false` and *"No organization identity"*. The app can offer pairing instead. An org admin creates a pairing passcode in the ArborXR Portal (the app's **Insights Hub** → **Pairing Passcodes**), and someone types it into the app once. The pairing is stored on the device, so later launches authenticate on their own.

The SDK never prompts by itself. Call `Abxr.StartPairing()` when your app wants to offer pairing, either at launch or from your own settings menu:

```csharp
void Start()
{
    // Subscribe first, then check the state: the startup decision can come before this script exists.
    Abxr.OnPairingStateChanged += OnPairingStateChanged;
    if (Abxr.GetPairingState() == Abxr.PairingState.Unpaired) Abxr.StartPairing();
}

void OnPairingStateChanged(Abxr.PairingState state, Abxr.PairingChangeReason reason)
{
    if (state == Abxr.PairingState.Unpaired && reason == Abxr.PairingChangeReason.Startup) Abxr.StartPairing();
    // Paired: authentication starts on its own, and OnAuthCompleted fires true.
}
```

- **The prompt** uses the [Sign-in UI](#sign-in-ui-optional) when it's imported: a short screen that says what pairing does, with **Not now** to close it, then the PIN pad for the passcode, then the keyboard if the passcode lets the person name the device. If another app already used the name, it asks whether to add this app to that device. To draw your own, assign a handler: `Abxr.OnInputRequested = (type, prompt, domain, error) => { ... };` (four strings, and one handler at a time). `type` is `pairingPasscode`, `pairingDeviceName`, `pairingDeviceNameRequired`, or `pairingDeviceJoin`; `prompt` is the text to show; `domain` is the existing device's name for `pairingDeviceJoin`; `error` is empty on the first request and says what went wrong when the SDK asks again. Answer with `Abxr.OnInputSubmitted(value)`. Call `Abxr.CancelPairing()` to close it. Or skip the prompt entirely with `Abxr.RedeemPairingPasscode(passcode, result => ...)`, which reports a typed `PairingRedeemResult`.
- **Too many attempts:** after repeated wrong passcodes, the pairing service makes the app wait (its Retry-After, from 1 second to an hour, or 60 seconds when it sends none). Until the wait is over, the SDK refuses redeems itself without contacting the service. The Sign-in UI counts down *"Too many attempts. Try again in N seconds."* on its own, but a custom prompt gets that text once, with `OnInputRequested`, so show the wait yourself: when the request arrives and `Abxr.GetLastPairingRedeemResult().Error` is `Abxr.PairingRedeemError.RateLimited`, start counting down from its `RetryAfterSeconds` right then. It's the wait at the moment of the result, not a live value. With `RedeemPairingPasscode`, do the same with the result passed to your callback, when the callback runs.
- **Device names** are set per passcode in the Portal. When a passcode allows naming, the prompt asks for a name after the passcode is accepted, and every app paired with that name joins the same device. The name is optional when the request's `type` is `pairingDeviceName`: a custom prompt skips it by submitting an empty string, `Abxr.OnInputSubmitted("")`, and the app pairs without a name. `pairingDeviceNameRequired` needs one, and an empty answer is asked again. `Abxr.GetPairedDeviceName()` returns the name.
- **Settings:** **Pairing URL** is the Portal API that redeems passcodes (`https://api.xrdm.app/`). **Enable Pairing Dismiss** shows **Not now** on the default prompt; leave it on, or someone without a passcode can't close a prompt your app opens at launch.
- **Removing a pairing:** revoking the app instance in the Portal removes the pairing on the device at its next sign-in (`OnPairingStateChanged` reports `Revoked`). `Abxr.ClearPairing()` removes it from the app.
- Each launch's identity is decided once, at the first authentication. An org credential set after that (a late `SetOrgId`, for example) is ignored for the launch, and the SDK logs a warning. If your app only gets the credential after an async step, turn off **Enable Auto Start Authentication** and call `Abxr.StartAuthentication()` once it's set. A `Development` build is the exception: it starts on the App Token, so an org credential set later takes over at the next authentication, with no warning.
- With **Enable Auto Start Authentication** off, the SDK decides the app's identity when you call `Abxr.StartAuthentication()`. Until then the state is `Resolving` and `StartPairing()` returns false.
- Pairing runs only in Android and WebGL builds. When the ArborXR client or an org token identifies the organization, it wins, and pairing doesn't apply.
- **To test pairing, use Build Type `Production`.** With Build Type `Development`, the SDK uses the App Token as the org token when nothing else supplies one, so a Development build always has an organization and never pairs. `Production` is the default, and it is separate from Unity's **Development Build** checkbox, so a pairing build can still be debugged. Use `Production` to test a launcher's auth handoff too: a Development build adopts the handed-over session, but any re-authentication after that signs in with the App Token.

#### Legacy (App ID / Org ID / Auth Secret)

A project that already uses the legacy scheme keeps working: Configuration and the Setup Wizard still show its App ID, Org ID, and Auth Secret (from the app’s credential or details views in the portal where your organization still exposes them), a note that the scheme is legacy, and a **Use App Tokens** checkbox for switching. On ArborXR-managed devices, only App ID may be required; Org ID and Auth Secret can auto-fill.

New projects use an App Token. A project that isn't on the legacy scheme and has no legacy values saved is not offered it: the legacy fields and the checkbox don't show. The legacy scheme also can't use passcode pairing, which needs an App Token.

If a project on App Tokens still has an App ID, Org ID, or Auth Secret saved (after switching, for example), Configuration and the Setup Wizard warn that the values ship inside the build, and a **Clear legacy values** button empties them. The button shows only once the project has an App Token, so it can never remove a project's only credentials. It works the other way round too: a project on the legacy scheme that still has an App Token or Org Token saved gets the same warning and a **Clear saved tokens** button. To switch back to the legacy scheme, untick **Use App Tokens**: the checkbox stays for as long as those values are saved, and clearing them removes it. A project last saved before the checkbox existed opens the same way, with the checkbox ticked and no App Token: untick it to keep using the legacy credentials, or paste an App Token.

### Using with Other Backend Services
For information on implementing your own backend service or using other compatible services, please refer to the ABXR protocol specification.

---

## Quick Start

### Essential Event Tracking (Required)

**Assessment events are required** to activate grading dashboards and LMS integration. Send these events to track training completion, scores, and pass/fail status.

```csharp
// When training starts
Abxr.EventAssessmentStart("safety_training");

// When training completes
Abxr.EventAssessmentComplete("safety_training", 92, EventStatus.Pass);
// or
Abxr.EventAssessmentComplete("safety_training", 25, EventStatus.Fail);
```

**Assessment Complete Parameters:**
- `Score` (second parameter) takes a 0-100 value
- The `EventStatus` enum has `Pass`, `Fail`, `Complete`, `Incomplete`, `Browsed`, `NotAttempted` options

### Tracking Objectives (Recommended)

For more detailed tracking, you can also track specific objectives within your training:

```csharp
// To mark a specific objective start
Abxr.EventObjectiveStart("open_valve");

// When the objective is complete
Abxr.EventObjectiveComplete("open_valve", 100, EventStatus.Complete);
```

### Watching What the SDK Records (Diagnostic)

`AbxrObserver` shows every record the SDK creates and what happened when it sent it. Use it for debug overlays and sample apps, not app logic: it's a best-effort diagnostic API, and the details it reports can change between releases.

```csharp
void OnEnable()
{
    AbxrObserver.OnRecordCreated += OnRecordCreated;
    AbxrObserver.OnRecordsSent += OnRecordsSent;
}

void OnDisable()
{
    AbxrObserver.OnRecordCreated -= OnRecordCreated;
    AbxrObserver.OnRecordsSent -= OnRecordsSent;
}

void OnRecordCreated(AbxrObserver.Record record)
{
    // record.Data is the metadata as queued, with what the SDK added (scene, duration, super metadata, scores).
    // record.CallerData is what your code passed. record.Automatic is true for records the SDK made itself.
    Debug.Log($"#{record.Id} {record.Kind} {record.Method ?? "(automatic)"} '{record.Name}' {record.DropReason}");
}

void OnRecordsSent(AbxrObserver.SendResult result)
{
    // Sent or Failed (with the HTTP status), or Dropped.
    Debug.Log($"{result.Status} {string.Join(",", result.RecordIds)} {result.HttpStatus} {result.Error}");
}
```

- Both events are raised on the main thread, in release builds too. With no handler attached, the SDK does no extra work.
- A record that isn't kept says why in `DropReason`: `NotRecording` (unpaired, no pairing prompt open), `QueueFull`, or for storage `NotAuthenticated` or `NoUser`.
- REST reports one result per batch. A failed batch is usually queued again, so a record can report `Failed` and later `Sent`; treat the latest result as its status. In this version, a batch that fails as the session ends (`EndSession`, or the app quitting) isn't queued again, and records lost after they were queued report `Dropped` with `SessionEnded` or `QueueFull`. A later version may keep unsent records past the session end, so don't treat session end as the point every record has its final result. The exception: records sent through the Insights device service (`app.xrdi.client`, Android only, when it's installed) get no send result, because the service sends them itself. Records still queued when the SDK switches to it at startup are never sent and get no result either.
- Records never carry tokens or secrets.

---

## Full Documentation

- **[INTEGRATION_INSTRUCTIONS.md](INTEGRATION_INSTRUCTIONS.md)** – Step-by-step integration guide for adopting AbxrLib in your project; also written for AI assistants to implement or review an integration consistently.

For comprehensive documentation covering all features, advanced topics, and detailed examples, visit:

- **[ArborXR Insights Documentation](https://developers.arborxr.com/docs/insights)** - Main documentation hub
- **[Complete SDK Documentation](https://developers.arborxr.com/docs/insights/full-documentation/)** - Full API reference and feature documentation

The full documentation includes:
- Complete event tracking API (Events, Analytics Event Wrappers, Timed Events)
- Advanced features (Module Targets, Authentication, Session Management)
- Storage, Telemetry, and Logging
- Mixpanel and Cognitive3D compatibility guides
- Troubleshooting and best practices
- Platform-specific examples and code samples

---

## Support

### Resources

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)

### FAQ

#### How do I get my App Token and Org Token?
Use an **App Token** (recommended). Copy it from **Content Library** → **Managed** app → **Insights Hub** in the portal, or use the value from your distribution channel. The org token is the dynamic org token when the device or runtime provides org context; for Editor testing, set **Build Type** to `Development`, which uses the App Token as the org token. (Don't switch a project that is on `Production (Custom APK)`: it already signs in from the Editor with its saved Org Token, and switching away removes that option.) New projects don't set an **Org Token**. A project already on `Production (Custom APK)` needs one in Configuration; it is not self-serve, so contact ArborXR. A project that already has one saved for `Development` keeps using it. For legacy setups, Application ID and Authorization Secret are still available under application details and Settings > Organization Codes.

#### How do I enable object tracking?
Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.

For more troubleshooting help and detailed FAQs, see the [full documentation](https://developers.arborxr.com/docs/insights/full-documentation/).

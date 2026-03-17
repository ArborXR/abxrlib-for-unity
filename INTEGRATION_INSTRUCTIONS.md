# ABXRLib for Unity – Integration Instructions

This document helps developers (and AI assistants) integrate **abxrlib-for-unity** into a Unity XR project. Use it as a step-by-step guide and as a **skill-style reference** so any AI agent can implement or correct an integration consistently.

---

## 1. Purpose and audience

- **Developers:** Share this document with your AI assistant when asking it to integrate or fix AbxrLib in your project. The instructions describe the goals and rules; the AI will review *your* codebase to see how you structure scenes, modules, navigation, and user identity, then apply these rules.
- **AI agents:** Use this document as the source of truth. You have not seen the customer’s code before. Your task is to (1) understand the goals and requirements below, (2) review the customer’s project to find where training/modules start and end, where users are identified, and how navigation to modules or scenarios works, and (3) implement or correct the integration using only the APIs and patterns described here. Prefer existing SDK APIs; do not propose changes to the abxrlib-for-unity package unless the customer explicitly needs a new feature.

**Documentation (all on the ArborXR Developer Portal):**

- **Quickstart:** [https://developers.arborxr.com/docs/insights/quickstart/](https://developers.arborxr.com/docs/insights/quickstart/)
- **Full documentation:** [https://developers.arborxr.com/docs/insights/full-documentation/](https://developers.arborxr.com/docs/insights/full-documentation/)

Do not reference or rely on files outside this package or the Developer Portal. The customer typically only has this repo and the docs at [developers.arborxr.com](https://developers.arborxr.com). A minimal demo app exists at [https://github.com/ArborXR/abxrlib-for-unity-demo-app](https://github.com/ArborXR/abxrlib-for-unity-demo-app) for reference only—the customer does not need it downloaded, and the AI should not download it for them; this document and the Developer Portal are sufficient for integration.

---

## 2. Installation and configuration

### 2.1 Install the package

1. In Unity: **Window → Package Manager → Add package from git URL**
2. URL: `https://github.com/ArborXR/abxrlib-for-unity.git`
3. After import, **Analytics for XR** appears in the menu; configuration is under **Analytics for XR → Configuration**.

### 2.2 Configure credentials (app token / org token)

Use **app token** and **org token** for authentication. In **Analytics for XR → Configuration**:

- **Use App Tokens:** Enable this option.
- **App Token:** Required. JWT for your app (from your distribution channel or ArborXR portal).
- **Org Token:** Usually leave empty so the SDK uses the **dynamic org token** (derived from device/org context when available). For single-customer builds (e.g. production_custom), set the org token explicitly.

**Development / testing:** Set App Token (and Org Token if needed for your build type). On ArborXR-managed devices, org context can be supplied at runtime (dynamic org token).

**Production (customer builds):** Set App Token. Use dynamic org token (empty org token in config) where the device or runtime provides org context; for production_custom builds, configure the org token as required.

**Security:** Do not compile org tokens or long-lived secrets into builds distributed to third parties unless required for a single-customer deployment. Prefer dynamic org token where possible.

**Legacy (app ID / org ID / auth secret):** If your project still uses the legacy scheme, set App ID, Org ID, and Auth Secret in Configuration and leave **Use App Tokens** off. New integrations should use app token and org token.

### 2.3 Optional: Android + ArborInsightsClient

For Android VR builds that use the device-side ArborInsightsClient service:

- Install the **ArborInsightsClient** APK on the device.
- Add the **matching client AAR** (e.g. `insights-client-service.aar`) to the project’s `Plugins/Android/`. The AAR is supplied separately (e.g. from your distribution channel), not built in this repo.

---

## 3. Required: Assessment events (LMS compatibility)

**Assessment events are required** for grading dashboards and LMS integration. Every training “unit” or “module” that should be reported to the LMS must have a matching start and complete pair.

### 3.1 Minimum pattern

```csharp
// When the training/module starts (e.g. scene load, scenario start)
Abxr.EventAssessmentStart("your_assessment_name");

// When the training/module ends (with score and status)
Abxr.EventAssessmentComplete("your_assessment_name", score, EventStatus.Pass);  // or Fail, Incomplete, etc.
```

- **Score:** Integer, typically 0–100. Use metadata (e.g. `max_score`) if your scale differs.
- **EventStatus:** `Pass`, `Fail`, `Complete`, `Incomplete`, `Browsed`, `NotAttempted`.

### 3.2 Where to call them

- **Single-scenario app:** Call `EventAssessmentStart` in `Start()` (or when the experience begins) and `EventAssessmentComplete` when the experience ends (success, fail, or exit).
- **Multi-module app:** Call `EventAssessmentStart(moduleName)` when each module/scenario starts and `EventAssessmentComplete(moduleName, score, status)` when that module completes. The SDK uses the assessment name as the effective “module” for LMS and reporting; do **not** put `"module"` in event metadata (see reserved keys below).

### 3.3 Optional: objectives and interactions

For finer-grained analytics and LMS objectives:

- **Objectives:** Call `EventObjectiveStart("objective_id")` when the objective begins and `EventObjectiveComplete("objective_id", score, status)` when it ends. Start/complete pairs let the backend compute duration and align with LMS objective tracking.
- **Interactions:** `EventInteractionStart("interaction_id")` / `EventInteractionComplete("interaction_id", type, result, response, meta)`.

See the [full documentation](https://developers.arborxr.com/docs/insights/full-documentation/) for parameters and enums.

### 3.4 Score and status: avoid parsing pass/fail strings as numbers

When the app has a pass/fail or similar outcome as a **string** (e.g. `"Pass"`, `"Fail"`), do **not** pass that string into a numeric parameter (e.g. `int.Parse("Pass")` will throw). Derive both score and `EventStatus`:

- `"Pass"` → score 100, `EventStatus.Pass`
- `"Fail"` → score 0, `EventStatus.Fail`
- Other (e.g. `"N/A"`, skipped) → score 0, `EventStatus.Incomplete` (or `NotAttempted` as appropriate)

---

## 4. Reserved keys and metadata

### 4.1 Do not use these in event metadata

The SDK reserves these for module/deep-link support. Do **not** pass them in event `meta` or as custom keys:

- `"module"`
- `"moduleName"`
- `"moduleId"`
- `"moduleOrder"`

The library sets module context from the **assessment name** when you call `EventAssessmentStart(assessmentName)`. Putting `"module"` in metadata can break LMS and reporting.

### 4.2 Use `Abxr.Register()` for global context

For data that should be included on **all** subsequent events (e.g. scenario name, lesson id, language), use **super metadata** instead of repeating the same keys on every event:

```csharp
// When scenario/module starts – set once, then all events carry it
Abxr.Register("scenario_name", currentScenarioName);
Abxr.Register("lesson", lessonId);
Abxr.Register("language_code", languageCode);
```

- `Abxr.Register(key, value)` – set or overwrite.
- `Abxr.RegisterOnce(key, value)` – set only if not already set.
- `Abxr.Unregister(key)` – remove.
- `Abxr.Reset()` – clear all super metadata (e.g. when starting a new session or scenario).

Use `Register` at scenario/module start rather than stuffing the same keys into every `Event(...)` or `EventAssessmentComplete(...)` call.

---

## 5. User identity: userData vs metadata

### 5.1 Prefer userData for identity

Store **user/learner identity** in **userData**, not in event metadata:

- **Primary id:** `Abxr.SetUserId(id)` – sets the primary user id and syncs to the API.
- **Full user payload:** `Abxr.SetUserData(id, additionalFields)` – e.g. `Abxr.SetUserData(employeeId, new Abxr.Dict { {"employee_name", name}, {"site_id", siteId} })`.

Use the same logical id (e.g. employee id) as the primary id when it clearly identifies the user. Then use `Abxr.GetUserId()` or `Abxr.GetUserData()` in your app when you need to display or reference the user.

### 5.2 Login + StartNewSession order

**StartNewSession()** clears all auth state (including user data) and then runs fresh authentication. So:

- **Wrong:** `SetUserData(...)` then `StartNewSession()` – the user data is cleared before the new session uses it.
- **Right (new session on login):** Call `StartNewSession()` first; in your **OnAuthCompleted** handler, when auth succeeds, call `SetUserData(...)` and `Register(...)` so the new session is tied to the logged-in user.
- **Right (same session, only identify):** Call only `SetUserData(...)` and `Register(...)`; do **not** call `StartNewSession()`.

---

## 6. Authentication and flow control

### 6.1 Wait for auth when needed

If your app must know whether auth succeeded before continuing (e.g. loading user-specific content or navigating to a module), subscribe to **OnAuthCompleted** and gate that logic there:

```csharp
Abxr.OnAuthCompleted += (success, errorMessage) => {
    if (success) {
        // Safe to use GetUserId(), GetUserData(), GetModuleList(), etc.
        StartAppFlow();
    } else {
        // Show error or fallback flow
    }
};
```

Subscribe **before** auth runs (e.g. in `Start()` or earlier; with auto-start auth, that means as early as your scene allows).

### 6.2 Module target (LMS deep link)

When the LMS (or backend) assigns specific modules, the SDK invokes **OnModuleTarget** with a target string (e.g. module or scenario id). Your app must **subscribe** and **navigate** the user to that module.

- Subscribe: `Abxr.OnModuleTarget += YourHandler;`
- In the handler: map the target string to your app’s notion of “module” or “scenario” (e.g. scene load, scenario id lookup) and perform the navigation.
- Unsubscribe in `OnDestroy`: `Abxr.OnModuleTarget -= YourHandler;`

**Pattern (recommended):** Implement a **single navigation entry point** in the app (e.g. a method that takes a module or scenario id and loads the right scene or content). Call it from (1) the **OnModuleTarget** handler when the LMS sends a target, and (2) any other place the app already navigates to a module (e.g. menu choice, external message, deep link). That way all paths use one pipeline and stay consistent with LMS behavior.

---

## 7. When the last module completes: OnAllModulesCompleted

If your app has multiple modules and the LMS sends a **sequence** of modules, the SDK fires **OnAllModulesCompleted** when the user completes the **last** module in that sequence. Use it to:

- Navigate the user back to a home screen or main menu.
- Call **Abxr.StartNewSession()** so the next run is a fresh session (recommended).

Example:

```csharp
Abxr.OnAllModulesCompleted += () => {
    // Your app: e.g. show main menu, home screen, or launcher
    ShowHomeScreen();  // or whatever the app uses to return the user to a neutral state
    Abxr.StartNewSession();
};
```

Subscribe/unsubscribe in the same component that handles **OnModuleTarget** (e.g. your navigation or bootstrap component) and unsubscribe in `OnDisable`/`OnDestroy`.

**LMS configuration:** In the LMS (or backend), configure the module target for this app to be the identifier your app expects (e.g. scenario id, module id) so that when the LMS sends a target, your **OnModuleTarget** handler can resolve it and navigate correctly.

---

## 8. Deep link and module target handling (summary)

| Source              | Purpose                         | Recommended implementation                                      |
|---------------------|---------------------------------|------------------------------------------------------------------|
| **Abxr.OnModuleTarget** | LMS/module assignment           | Subscribe in a persistent component; call your app’s single navigation entry point (e.g. load scene or scenario by id). |
| **External deep link** (e.g. Android intent, WebGL URL) | Open app to a specific module  | Parse in your own deep-link handler; then call the **same** navigation entry point so one code path handles both. |
| **OnAllModulesCompleted** | After last module in sequence   | Show home/main menu (or equivalent) + `Abxr.StartNewSession()`.                       |

Keep all handler logic in the customer’s app (e.g. their navigation component, deep-link handler); the SDK only provides the events and APIs.

**Other analytics or graph systems:** If the app also uses another analytics SDK or a visual/graph scripting system that fires events, ensure every reportable action (e.g. lesson exit, restart, unit complete) is also sent to Abxr. Mirror events or add hooks where that system runs so Abxr receives the same semantics (e.g. assessment complete, objective complete) for LMS and reporting.

---

## 9. Integration checklist (for you and for AI agents)

Use this to implement or audit an integration.

### Installation and config

- [ ] Package added via git URL (or local path). Optionally pin to a tag for reproducible builds (e.g. `#v2.0.4` in the package URL).
- [ ] **Analytics for XR → Configuration** uses **Use App Tokens** with **App Token** set; **Org Token** left empty for dynamic org token unless production_custom (or legacy credentials if still using app ID/org ID/auth secret).
- [ ] Production builds do not embed org token or long-lived secrets unless required for a single-customer deployment.

### Required events

- [ ] Every reportable training unit has `EventAssessmentStart(name)` at start.
- [ ] Every such unit has `EventAssessmentComplete(name, score, status)` at end (and optionally `meta`).
- [ ] Score and status are correct (e.g. no `int.Parse("Pass")`; use numeric score and `EventStatus`).

### Metadata and identity

- [ ] No reserved keys (`module`, `moduleName`, `moduleId`, `moduleOrder`) in event metadata.
- [ ] Repeating context (scenario, lesson, language) uses `Abxr.Register()` at start of scenario/module, not duplicated in every event.
- [ ] User identity is in userData (`SetUserId` / `SetUserData`), not only in event metadata.
- [ ] If login calls `StartNewSession()`, user data is set **after** auth completes (e.g. in OnAuthCompleted), not before.

### Auth and navigation

- [ ] If the app needs auth before proceeding, it subscribes to **OnAuthCompleted** and continues only when `success` is true.
- [ ] **OnModuleTarget** is subscribed (and unsubscribed in OnDisable/OnDestroy); the handler navigates to the requested module/scenario.
- [ ] If the app has multi-module sequences, **OnAllModulesCompleted** is subscribed and used to e.g. go home and call **Abxr.StartNewSession()**.

### Code quality

- [ ] Public API used is the global **Abxr** class (e.g. `Abxr.Event`, `Abxr.EventAssessmentStart`); no references to non-existent types (e.g. `Abxr.Runtime.Core.Abxr`).
- [ ] No changes to abxrlib-for-unity package code unless there is a stated need for a new feature (prefer using existing APIs).

---

## 10. Common mistakes and fixes

| Mistake | Fix |
|--------|-----|
| Putting `"module"` (or reserved keys) in event metadata | Remove from metadata; rely on assessment name and `EventAssessmentStart` for module context. |
| Repeating the same keys (scenario, lesson, etc.) in every event | Use `Abxr.Register("scenario_name", value)` (and similar) when the scenario/module starts. |
| Storing employee/user id only in event metadata | Use `Abxr.SetUserData(employeeId, { "employee_name", "site_id", ... })` and use the same id as primary id when appropriate. |
| Calling `SetUserData` then `StartNewSession` on login | Call `StartNewSession()` first; in **OnAuthCompleted**, call `SetUserData` and `Register` for the new session. |
| Not subscribing to **OnModuleTarget** | Add a handler that maps the target string to your navigation (e.g. scenario id → load scenario); subscribe before auth. |
| Ignoring **OnAllModulesCompleted** in multi-module flows | Subscribe and call the app’s “go home” (or equivalent) and **Abxr.StartNewSession()** when the last module completes. |
| Using wrong API for completion (e.g. `int.Parse("Pass")` for score) | Use numeric score and `EventStatus.Pass` / `EventStatus.Fail` (or other status) in `EventObjectiveComplete` / `EventAssessmentComplete`. |
| Referencing non-existent API (e.g. `Abxr.Runtime.Core.Abxr`) | Use the global **Abxr** class only (e.g. `Abxr.Event`, `Abxr.Register`). |

---

## 11. References (this package and Developer Portal only)

- **This package** – [README](README.md), [AGENTS.md](AGENTS.md) (architecture and key files; both live in the abxrlib-for-unity folder).
- **ArborXR Developer Portal** – [Quickstart](https://developers.arborxr.com/docs/insights/quickstart/), [Full documentation](https://developers.arborxr.com/docs/insights/full-documentation/). All further docs (events, LMS, troubleshooting) are at [https://developers.arborxr.com](https://developers.arborxr.com).

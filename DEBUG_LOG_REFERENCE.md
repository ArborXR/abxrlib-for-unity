# AbxrLib Debug.Log Reference – Normal Workflow Sequence

Use this list to find and adjust/remove logs. Order reflects **typical sequence** under generally normal workflows (Editor load → Play → Auth → Config → optional keyboard). Logs that only appear on **errors or edge cases** are marked so you can demote or remove them if desired.

**Legend:** `[LOG]` = Debug.Log, `[WARN]` = Debug.LogWarning, `[ERR]` = Debug.LogError.

---

## 1. Editor (domain reload / script load)

When Unity loads or recompiles, **one** of these runs depending on XR SDK:

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 1 | LOG | `[AbxrLib] Meta / OpenXR SDK detected.` | Editor/MetaDefineManager.cs:37 |
| 2 | LOG | `[AbxrLib] OpenXR detected.` | Editor/MetaDefineManager.cs:43 |
| 3 | LOG | `[AbxrLib] Meta SDK not detected.` | Editor/MetaDefineManager.cs:48 |
| 4 | LOG | `[AbxrLib] To enable Meta QR scanning, ensure Meta SDK or OpenXR is installed.` | Editor/MetaDefineManager.cs:49 |
| 5 | LOG | `[AbxrLib] Added {define} to {target} scripting define symbols.` | Editor/MetaDefineManager.cs:73 |

*(Commented out: MetaDefineManager.cs:36 – "Meta/Quest SDK detected. Setting META_QR_AVAILABLE define symbol.")*

---

## 2. Runtime – Startup (BeforeSceneLoad / Awake)

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 6 | ERR | `[AbxrLib] Incompatible Newtonsoft.Json version loaded.` *(only if Json < 13)* | Runtime/AbxrSubsystem.cs:91 |
| 7 | WARN | `[AbxrLib] Auth failed: {error}` *(only if auth fails)* | Runtime/AbxrSubsystem.cs:122 |
| 8 | LOG | `[AbxrLib] Auto-start auth is disabled. Call Abxr.StartAuthentication() manually when ready.` *(only if auto-start off)* | Runtime/AbxrSubsystem.cs:162 |
| 9 | LOG | `[AbxrLib] Version {version} Initialized.` | Runtime/AbxrSubsystem.cs:171 |

---

## 3. Runtime – HeadsetDetector (Start, Editor / non-VR)

In Editor or when XR isn’t available, you typically see:

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 10 | WARN | `[AbxrLib] HeadsetDetector - XR not available, headset detection disabled` | Runtime/Core/HeadsetDetector.cs:35 |

*Other HeadsetDetector logs (XR init failure, PICO, proximity, re-auth error) – Runtime/Core/HeadsetDetector.cs:41, 88, 125, 130, 149, 164, 195, 200, 229 – are edge/failure only.*

---

## 4. Auth & GetConfiguration

**Path A – Using ArborInsightService (Android, config from service):**

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 11 | LOG | `[AbxrLib] GetConfiguration from ArborInsightService` | Runtime/Services/Auth/AbxrAuthService.cs:503 |
| 12a | LOG | `[AbxrLib] GetConfiguration successful. User Authentication Required. Type: {type} & Prompt: {prompt}` | Runtime/Services/Auth/AbxrAuthService.cs:525 |
| 12b | LOG | `[AbxrLib] GetConfiguration successful. User authentication not required. Using anonymous session.` | Runtime/Services/Auth/AbxrAuthService.cs:527 |

**Path B – REST (no service or empty config):** no “GetConfiguration from ArborInsightService”; config comes from REST. Same 12a/12b after successful config.

*Auth failure/retry/JWT/response handling – AbxrAuthService.cs:172, 217, 370, 403, 432, 448, 454, 459, 468, 486, 534 – are error path only.*

---

## 5. Keyboard / input (when auth requests user input)

If config says user auth and default handler shows keyboard:

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 13 | LOG | `[AbxrLib] KeyboardHandler - Using custom keyboard prefab from configuration` *(if config has keyboard prefab)* | Runtime/UI/Keyboard/KeyboardHandler.cs:54 |
| 14 | LOG | `[AbxrLib] KeyboardHandler - Using custom PIN pad prefab from configuration` *(if config has PIN prefab)* | Runtime/UI/Keyboard/KeyboardHandler.cs:63 |
| 15 | LOG | `[AbxrLib] KeyboardHandler - Prefabs refreshed from configuration` *(only if RefreshPrefabs() called)* | Runtime/UI/Keyboard/KeyboardHandler.cs:96 |

*Keyboard/PIN prefab missing – KeyboardHandler.cs:68, 119, 139 – error path only.*

---

## 6. LaserPointerManager (when keyboard/PIN pad is shown)

Can appear when opening keyboard/PIN pad or on scene/cleanup:

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 16 | LOG | `[AbxrLib] LaserPointerManager - Cleaned up {n} destroyed ray interactor references` | Runtime/UI/Keyboard/LaserPointerManager.cs:58 |
| 17 | LOG | `[AbxrLib] LaserPointerManager - Force cleanup completed` | Runtime/UI/Keyboard/LaserPointerManager.cs:71 |
| 18 | LOG | `[AbxrLib] LaserPointerManager - Scene changed while managing laser pointers, performing cleanup` | Runtime/UI/Keyboard/LaserPointerManager.cs:82 |
| 19 | LOG | `[AbxrLib] LaserPointerManager - Updated cache, removed {n} destroyed ray interactors` | Runtime/UI/Keyboard/LaserPointerManager.cs:125 |
| 20 | LOG | `[AbxrLib] LaserPointerManager - Enabled ray interactor on {name}` | Runtime/UI/Keyboard/LaserPointerManager.cs:175 |
| 21 | WARN | `[AbxrLib] LaserPointerManager - Maximum dictionary size ({n}) reached, skipping additional ray interactors` | Runtime/UI/Keyboard/LaserPointerManager.cs:180 |

---

## 7. Application quit

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 22 | LOG | `[AbxrLib] Application quitting, automatically closing running events` | Runtime/AbxrSubsystem.cs:208 |

---

## 8. Modules / assessment (if using modules)

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 23 | LOG | `[AbxrLib] Module '{name}' complete. ...` | Runtime/AbxrSubsystem.cs:479 |
| 24 | LOG | `[AbxrLib] All modules complete` | Runtime/AbxrSubsystem.cs:486 |
| 25 | LOG | `[AbxrLib] Assessment complete with auth handoff - returning to launcher in 2 seconds` | Runtime/AbxrSubsystem.cs:710 |

*Module/poll errors – AbxrSubsystem.cs:330, 448, 454, 460, 904, 912, 918, 927 – error path only.*

---

## 9. Editor – Update check (only when checking for updates)

| # | Level | Message / pattern | File:Line |
|---|--------|-------------------|-----------|
| 26 | LOG | `[AbxrLib] Latest release version: ...` | Editor/UpdateCheck.cs:136 |
| 27 | LOG | `[AbxrLib] Package updated successfully` | Editor/UpdateCheck.cs:84 |

*UpdateCheck errors/warnings – UpdateCheck.cs:35, 86, 112, 116, 140, 144 – error path only.*

---

## Logs that only run on errors or edge cases

Kept in one place so you can trim or downgrade them.

- **AbxrAuthService:** 172 (OnInputSubmitted ignored), 217 (auth headers), 370, 403, 432, 448, 454, 459, 468, 486, 534 (config handling failed).
- **AbxrSubsystem:** 91 (Newtonsoft), 122 (auth failed), 330, 448, 454, 460 (modules), 904, 912, 918, 927 (polls).
- **HeadsetDetector:** 41, 88, 125, 130, 149, 164, 195, 200, 229.
- **KeyboardHandler:** 68, 119, 139 (prefab missing).
- **Utils:** 278, 285, 292, 302, 309, 317, 322, 327, 494, 527, 653, 685, 733, 768 (JWT, org token, Android, module conversion).
- **ArborServiceClient:** 22, 28, 56.
- **ArborInsightServiceClient:** 59, 71, 105, 114, 365, 372.
- **AbxrDataService / AbxrStorageService:** 195, 207, 244, 263, 277, 286, 317, 329, 366, 382, 408, 453, 465.
- **AbxrTelemetryService / AbxrTarget:** 49, 134, 149, 159, 319, 578, 613.
- **UpdateCheck:** 35, 86, 112, 116, 140, 144.

---

## Minimal “happy path” sequence (example)

For a typical run in **Editor**, **auto-start auth on**, **anonymous session** (no user auth), you might see only:

1. `[AbxrLib] Meta / OpenXR SDK detected.` (or OpenXR / Meta not detected pair)  
2. `[AbxrLib] Added ... to ... scripting define symbols.`  
3. `[AbxrLib] Version X.Y.Z Initialized.`  
4. `[AbxrLib] HeadsetDetector - XR not available, headset detection disabled`  
5. `[AbxrLib] GetConfiguration successful. User authentication not required. Using anonymous session.`

If **user auth is required** (e.g. type=text), add after step 5:

6. `[AbxrLib] GetConfiguration successful. User Authentication Required. Type: text & Prompt: ...`  
7. Optionally KeyboardHandler/LaserPointerManager lines when the keyboard is shown.

Use the **file:line** column above to open and edit or remove any of these logs.

# AbxrLib Unit Tests (TakeTwo design)

Unit tests for the ABXRLib SDK, written for the **TakeTwo** branch design (AbxrSubsystem, AbxrAuthService, static `Abxr` API). They run in Unity Test Runner (Play Mode) and require a valid configuration and successful authentication.

## Requirements

- **Configuration:** `Resources/AbxrLib.asset` must exist and be valid (e.g. from the demo app). Tests use `Configuration.Instance` and do not mock the backend.
- **Authentication:** Tests wait for real authentication to complete (started by `Initialize` before scene load). There is no test-mode PIN injection; use credentials that succeed against your configured backend.
- **Unity Test Runner:** Open **Window > General > Test Runner**, switch to **PlayMode**, run or filter tests.

## Layout

- **Tests/Runtime/** – Play Mode tests (NUnit + UnityEngine.TestTools).
  - **Utilities/** – `TestHelpers`, `AuthenticationTestHelper`, `AuthHandoffTestHelper`, `TestInitializer`.
  - **_AuthenticationTests** – Auth state and `GetAuthResponse`; run first (establishes shared session).
  - **EventTrackingTests** – `Abxr.Event`, metadata, `Abxr.Dict`, Register/Reset.
  - **AnalyticsEventTests** – Assessment, objective, interaction, experience events.
  - **AuthenticationHandoffTests** – Launcher-side handoff (serialized `GetAuthResponse`), `Modules`, and Event after auth. Target-side “receive handoff” is not testable without injecting `auth_handoff` (intent/command line); those scenarios are integration/manual.

## Design notes (TakeTwo)

- No `Authentication` static class; auth is handled by `AbxrSubsystem` / `AbxrAuthService`. Tests use `Abxr.GetIsAuthenticated()` and `Abxr.GetAuthResponse()`.
- No `ConnectionActive()`; use `Abxr.GetIsAuthenticated()`.
- Handoff is read from `auth_handoff` (Android intent / command line / WebGL query). Tests can only simulate “launcher”: wait for auth and serialize `GetAuthResponse()`. They cannot inject handoff into the SDK from here.
- Module targets: use `GetAuthResponse().Modules` (and `OnModuleTarget` in app code). There is no `GetModuleTarget()` API in TakeTwo.

## Running tests

1. **Add the Test Framework package** (required for tests to appear):
   - **Window > Package Manager**
   - Click **+** > **Add package by name**
   - Name: `com.unity.test-framework`, Version: `1.4.5` (or latest) > **Add**
2. Open the project that uses this package (e.g. the demo app), or open the package repo as a Unity project.
3. Ensure `Resources/AbxrLib.asset` is configured and valid (in the project that uses the package, the asset may be in the project’s Assets, not inside the package).
4. **Window > General > Test Runner** > **PlayMode** (and **EditMode** if you add Editor tests).
5. Click **Run All** or run filtered categories (e.g. **Authentication**, **PostAuth**).

Tests that require network or specific server responses (e.g. modules, handoff shape) may be skipped or fail if the backend does not return the expected data.

## Tests not showing in Test Runner

- **No tests in Play Mode or Edit Mode:** The project must have the **Test Framework** package (`com.unity.test-framework`) installed. Without it, the test assembly does not compile and no tests are listed. Add it via Package Manager (see above).
- **Using this repo as a package:** If you consume AbxrLib via **Add package from git URL** or **Add package from disk**, the Tests folder is part of the package. The project that *references* the package (e.g. the demo app) must have `com.unity.test-framework` in its `Packages/manifest.json` so that `UnityEngine.TestRunner` is available and the test assembly builds.
- After adding the Test Framework, let Unity reimport and then open **Window > General > Test Runner** again.

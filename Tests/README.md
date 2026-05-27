# AbxrLib Test Framework

Two Unity test assemblies plus a stdlib-only fake Python backend for integration tests.

```text
Tests/
  Editor/               # Unit tests — pure logic, no scene, no play mode
    UtilsTests.cs
    ValidationTests.cs
    AbxrLib.Tests.Editor.asmdef
  Runtime/              # PlayMode/integration tests + fake-backend harness
    AbxrTestHooks.cs                  # Test-only helpers for config/subsystem lifecycle
    FakeBackend.cs                    # C# wrapper that owns the Python process
    AbxrIntegrationTestFixture.cs     # Base fixture: config, subsystem lifecycle, auth helper
    AuthenticationTests.cs            # Sample auth integration tests
    AbxrLib.Tests.Runtime.asmdef
  FakeBackend/          # Python stdlib server that mimics the AbxrLib backend
    server.py
    README.md
```

`Tests/` is intentionally visible to Unity. Do **not** rename it to `Tests~/`
unless you want Unity to ignore the tests. If this package is referenced from
outside your project's `Packages/` folder, add the package name from
`package.json` to the consuming project's `Packages/manifest.json` `testables`
array so Unity discovers package tests.

## Requirements

Python 3.8+ on PATH. No pip packages, no venv, and no Flask are required.

The C# harness looks for Python in this order:

1. `ABXR_TEST_PYTHON` environment variable
2. `python3` on PATH
3. `python` on PATH

Set `ABXR_TEST_PYTHON` only when Unity is picking the wrong interpreter:

```bash
# macOS/Linux
export ABXR_TEST_PYTHON=/path/to/python3

# Windows PowerShell
$env:ABXR_TEST_PYTHON = "C:\Path\To\python.exe"
```

## Running tests

Open `Window → General → Test Runner` in Unity. Unit tests appear in the
EditMode tab. The fake-backend integration tests appear in the PlayMode tab.

CLI examples:

```bash
Unity -batchmode -nographics -projectPath . -runTests \
      -testPlatform EditMode -testResults editmode-results.xml -quit

Unity -batchmode -nographics -projectPath . -runTests \
      -testPlatform PlayMode -testResults playmode-results.xml -quit
```

## How the integration tests work

The runtime assembly does not contain Test Runner detection logic. Automatic SDK
startup is controlled by the normal configuration field
`enableAutomaticInitialization`. Production projects can leave it enabled, or
disable it and call `Abxr.Initialize()` manually after applying runtime setup.

Each integration test follows this lifecycle:

```text
[OneTimeSetUp]    -> Start the Python fake backend on a free port.
[UnitySetUp]      -> Reset backend state, destroy any leftover AbxrLib subsystem,
                     install a transient in-memory runtime config, point Configuration at
                     the fake backend, disable automatic SDK initialization,
                     auto-start auth, telemetry, and scene events, then explicitly
                     call Abxr.Initialize() to create a fresh AbxrSubsystem. Tests never
                     reset a live subsystem as their normal isolation strategy.
test body         -> Optionally script the backend, call RunAuthAndWait(),
                     assert on LastAuthSuccess / LastAuthError /
                     FakeBackend.GetRequests(...).
[UnityTearDown]   -> Unsubscribe static handlers, destroy the subsystem, run its
                     pre-destroy cleanup, and clear the transient Configuration.
[OneTimeTearDown] -> Stop the Python process.
```

The tests do not ship or load a `Resources/AbxrLib.asset` fixture; they use
a transient in-memory `Configuration` instance so editor menus and runtime resource
lookups are not polluted by test assets.

That gives tests the desired shape:

```text
give it configuration
start the SDK
verify something happens
```

A typical test:

```csharp
[UnityTest]
public IEnumerator Auth_Fails_On_401()
{
    FakeBackend.QueueScenario(
        path: "/v1/auth/token",
        status: 401,
        body: new Dictionary<string, object> { { "detail", "Invalid app token" } });

    yield return RunAuthAndWait();

    Assert.IsFalse(LastAuthSuccess);
    Assert.That(LastAuthError, Does.Contain("Invalid app token"));
    Assert.AreEqual(1, FakeBackend.GetRequests("/v1/auth/token").Count);
}
```

## Scenario queue semantics

Each `QueueScenario(...)` call appends to a per-`(method, path)` queue on the
server. Each request consumes the head of the queue. The last queued response is
sticky once the queue empties, so two queue calls can mean “fail once, then
succeed forever.”

If you do not queue anything for an endpoint, the server returns a default
response: a valid auth response for `/v1/auth/token`, `{}` for
`/v1/storage/config`, and success bodies for data/storage endpoints.

Wrapper behavior:

```csharp
FakeBackend.QueueScenario(path: "/v1/auth/token");
// Uses the endpoint's default status and default body.

FakeBackend.QueueScenario(path: "/v1/auth/token", status: 401,
    body: new Dictionary<string, object> { { "detail", "Invalid token" } });
// Returns JSON body.

FakeBackend.QueueEmptyBodyScenario(path: "/v1/auth/token", status: 500);
// Returns status only, no response body.

FakeBackend.QueueRawScenario(path: "/v1/auth/token", status: 200, raw: "not json");
// Returns malformed/non-JSON content.
```

## Inspecting requests

`FakeBackend.GetRequests(pathFilter)` returns every request the server has
received since the last `ResetState()` call. Each record has `Method`, `Path`,
`Headers`, `Query`, `BodyText`, and parsed `BodyJson`.

## Adding tests

Add fast unit tests under `Tests/Editor/` with NUnit `[Test]` or `[TestCase]`.

Add integration tests under `Tests/Runtime/` by inheriting
`AbxrIntegrationTestFixture`, scripting the fake backend with
`FakeBackend.QueueScenario(...)`, and driving auth with `RunAuthAndWait()`.

## Troubleshooting

**No tests show up**

Check the Unity Console first; any compile error in the runtime assembly or test
asmdefs prevents discovery. If the package is outside the project's `Packages/`
folder, add the package name to the project's manifest `testables` array.

**Fake backend process exited during startup**

The exception includes captured stderr/stdout. Usually this means Unity could
not find a usable Python interpreter. Set `ABXR_TEST_PYTHON` to a Python 3.8+
executable and re-run.

**Tests hang waiting on `OnAuthCompleted`**

The failure message prints recorded fake-backend requests, recent Unity logs,
and backend stderr. Common causes are a scenario that keeps auth retrying, an
unexpected endpoint path, or malformed JSON that does not produce a terminal
auth error.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using AbxrLib.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// Base fixture for integration tests that talk to <see cref="FakeBackend"/>.
    ///
    /// Each test owns the full lifecycle:
    ///   1. Reset fake backend and install transient runtime Configuration.
    ///   2. Apply test Configuration values, including restUrl -> fake backend and manual SDK initialization.
    ///   3. Explicitly initialize the AbxrSubsystem.
    ///   4. Trigger auth or REST calls and assert on results/recorded requests.
    ///   5. Destroy the subsystem, which performs deterministic cleanup and clears runtime state.
    ///
    /// That keeps the integration tests flow: configure first, start the SDK second, verify behavior third.
    /// </summary>
    public abstract class AbxrIntegrationTestFixture
    {
        // Structurally valid JWTs (header.payload.signature, base64url, no padding).
        protected const string FakeAppToken =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0LWFwcCJ9.c2ln";
        protected const string FakeOrgToken =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0LW9yZyJ9.c2ln";
        protected const string ValidJwtWithExpiration =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJleHAiOjQxMDI0NDQ4MDB9.c2ln";
        protected const string FakeAppId = "00000000-0000-0000-0000-000000000001";
        protected const string DefaultAssessmentPinPrompt = "Enter your test PIN";

        protected bool LastAuthDone { get; private set; }
        protected bool LastAuthSuccess { get; private set; }
        protected string LastAuthError { get; private set; }

        private Action<bool, string> _onAuthCompletedHandler;
        private readonly List<string> _capturedLogs = new();
        private Application.LogCallback _logCallback;

        // ── Lifecycle ───────────────────────────────────────────────

        [OneTimeSetUp]
        public void OneTimeFixtureSetUp()
        {
            FakeBackend.Start();
            Debug.Log($"[TestFixture] FakeBackend started at {FakeBackend.BaseUrl}");
        }

        [OneTimeTearDown]
        public void OneTimeFixtureTearDown()
        {
            FakeBackend.Stop();
        }

        [UnitySetUp]
        public IEnumerator PerTestSetUp()
        {
            _capturedLogs.Clear();
            _logCallback = (msg, stack, type) =>
            {
                lock (_capturedLogs) _capturedLogs.Add($"[{type}] {msg}");
            };
            Application.logMessageReceivedThreaded += _logCallback;

            FakeBackend.ResetState();

            // Be defensive in case a previous test failed before teardown completed.
            // DestroySubsystemForTest owns cleanup; tests should not reset a live subsystem.
            AbxrTestHooks.DestroySubsystemForTest(clearConfiguration: true);
            yield return null;
            AbxrTestHooks.ResetConfigurationForTest();

            ConfigureForFakeBackend();
            Debug.Log($"[TestFixture] Configured: restUrl={Configuration.Instance.restUrl}");

            AbxrTestHooks.CreateSubsystemForTest();
            yield return null;

            Assert.IsTrue(AbxrTestHooks.HasSubsystemInstance, "AbxrSubsystem was not created by the test fixture.");

            LastAuthDone = false;
            LastAuthSuccess = false;
            LastAuthError = null;
        }

        [UnityTearDown]
        public IEnumerator PerTestTearDown()
        {
            RemoveAuthCompletedHandler();
            Abxr.OnUserDataSyncCompleted = null;

            if (_logCallback != null)
            {
                Application.logMessageReceivedThreaded -= _logCallback;
                _logCallback = null;
            }

            AbxrTestHooks.DestroySubsystemForTest(clearConfiguration: true);
            yield return null;
        }

        // ── Configuration ───────────────────────────────────────────

        protected virtual void ConfigureForFakeBackend()
        {
            var c = Configuration.Instance;

            // Endpoint + credentials.
            c.restUrl = FakeBackend.BaseUrl;
            c.useAppTokens = true;
            c.buildType = "production_custom";
            c.appID = null;
            c.orgID = null;
            c.authSecret = null;
            c.appToken = FakeAppToken;
            c.orgToken = FakeOrgToken;

            // Explicit test lifecycle: no SDK work until the test calls it.
            c.enableAutomaticInitialization = false;
            c.enableAutoStartAuthentication = false;
            c.authenticationStartDelay = 0f;
            c.enableAutoStartModules = false;
            c.enableAutoAdvanceModules = false;
            c.enableReturnTo = false;
            c.enableAutomaticTelemetry = false;
            c.enableSceneEvents = false;
            c.headsetTracking = false;
            c.recordIpAddress = false;
            c.enableArborMdmClient = false;

            // Reset fields that can be changed by /v1/storage/config so tests do not leak.
            c.sendRetriesOnFailure = 0;
            c.sendRetryIntervalSeconds = 1;
            c.sendNextBatchWaitSeconds = 30;
            c.requestTimeoutSeconds = 5;
            c.stragglerTimeoutSeconds = 15;
            c.maxCallFrequencySeconds = 1f;
            c.dataEntriesPerSendAttempt = 32;
            c.storageEntriesPerSendAttempt = 16;
            c.pruneSentItemsOlderThanHours = 12;
            c.maximumCachedItems = 1024;
            c.retainLocalAfterSent = false;
            c.maxDictionarySize = 50;

            Assert.IsTrue(c.IsValid(), Configuration.LastValidationErrorMessage ?? "Test configuration should be valid.");
        }

        // ── Fake backend response builders ───────────────────────────

        protected static Dictionary<string, object> AuthBody(
            string token = ValidJwtWithExpiration,
            string secret = "test-secret",
            object userData = null,
            string userId = "test-user-id",
            object modules = null,
            string appId = FakeAppId,
            string packageName = "com.example.testapp")
        {
            var body = new Dictionary<string, object>();
            if (token != null) body["token"] = token;
            if (secret != null) body["secret"] = secret;
            if (userId != null) body["userId"] = userId;
            if (userData != null) body["userData"] = userData;
            if (appId != null) body["appId"] = appId;
            if (packageName != null) body["packageName"] = packageName;
            body["modules"] = modules ?? Array.Empty<object>();
            return body;
        }

        protected static string HandoffJson(
            string token = ValidJwtWithExpiration,
            string secret = "handoff-secret",
            object userData = null,
            string userId = "handoff-user-id",
            object modules = null,
            string appId = FakeAppId,
            string packageName = "com.example.handoffapp",
            string returnToPackage = null)
        {
            var body = AuthBody(token, secret, userData, userId, modules, appId, packageName);
            if (returnToPackage != null) body["ReturnToPackage"] = returnToPackage;
            return JsonConvert.SerializeObject(body);
        }

        protected static void QueueAssessmentPinConfig(string prompt = DefaultAssessmentPinPrompt) =>
            QueueAuthMechanismConfig("assessmentPin", prompt);

        protected static void QueueAuthMechanismConfig(string type, string prompt, string inputSource = "user", string domain = null)
        {
            var mechanism = new Dictionary<string, object>
            {
                { "type", type },
                { "prompt", prompt },
                { "inputSource", inputSource }
            };
            if (domain != null) mechanism["domain"] = domain;

            QueueStorageConfig(new Dictionary<string, object>
            {
                { "authMechanism", mechanism }
            });
        }

        protected static void QueueStorageConfig(object body, int status = 200)
        {
            FakeBackend.QueueScenario(
                path: "/v1/storage/config",
                method: "GET",
                status: status,
                body: body);
        }

        protected static string GetHeader(RecordedRequest request, string name)
        {
            if (request?.Headers == null) return null;
            foreach (var kvp in request.Headers)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        protected static string JwtWithClaims(Dictionary<string, object> claims)
        {
            var header = new Dictionary<string, object>
            {
                { "typ", "JWT" },
                { "alg", "HS256" }
            };

            return $"{Base64UrlEncodeJson(header)}.{Base64UrlEncodeJson(claims)}.c2ln";
        }

        protected static string Base64Utf8(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        private static string Base64UrlEncodeJson(object value)
        {
            var json = JsonConvert.SerializeObject(value);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ── Test helpers ────────────────────────────────────────────

        protected IEnumerator RunAuthAndWait(float timeoutSeconds = 10f)
        {
            RemoveAuthCompletedHandler();

            LastAuthDone = false;
            LastAuthSuccess = false;
            LastAuthError = null;

            _onAuthCompletedHandler = (success, error) =>
            {
                Debug.Log($"[TestFixture] OnAuthCompleted: success={success}, error={error ?? "(null)"}");
                if (LastAuthDone) return;
                LastAuthDone = true;
                LastAuthSuccess = success;
                LastAuthError = error;
            };
            Abxr.OnAuthCompleted += _onAuthCompletedHandler;

            Debug.Log("[TestFixture] Calling Abxr.StartAuthentication()");
            Abxr.StartAuthentication();

            float elapsed = 0f;
            while (!LastAuthDone && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            RemoveAuthCompletedHandler();

            if (!LastAuthDone)
            {
                var allReqs = FakeBackend.GetRequests();
                string requestSummary = allReqs.Count == 0
                    ? "  (NO requests reached the fake backend)"
                    : string.Join("\n", allReqs.Select(r =>
                        $"  {r.Method} {r.Path}  body={(r.BodyText ?? "").Substring(0, Math.Min(200, (r.BodyText ?? "").Length))}"));
                string logSummary;
                lock (_capturedLogs)
                {
                    logSummary = _capturedLogs.Count == 0
                        ? "  (no logs)"
                        : string.Join("\n", _capturedLogs.TakeLast(40).Select(l => "  " + l));
                }
                Assert.Fail(
                    $"OnAuthCompleted did not fire within {timeoutSeconds}s.\n" +
                    $"---\nFakeBackend requests received ({allReqs.Count}):\n{requestSummary}\n" +
                    $"---\nLast Unity logs:\n{logSummary}\n" +
                    $"---\nBackend stderr:\n{FakeBackend.GetStderr()}");
            }
        }

        protected IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string description)
        {
            float elapsed = 0f;
            while (!predicate() && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Assert.IsTrue(predicate(), $"WaitUntil timeout after {timeoutSeconds}s: {description}");
        }

        private void RemoveAuthCompletedHandler()
        {
            if (_onAuthCompletedHandler == null) return;
            Abxr.OnAuthCompleted -= _onAuthCompletedHandler;
            _onAuthCompletedHandler = null;
        }
    }
}

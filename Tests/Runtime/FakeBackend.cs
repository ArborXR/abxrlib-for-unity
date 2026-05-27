using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;

namespace AbxrLib.Tests.Runtime
{
    /// <summary>
    /// C# wrapper around the Python fake backend.
    /// Owns the server process lifetime for the test run and exposes helpers to script responses and inspect recorded requests.
    ///
    /// Started once per test session by <see cref="AbxrIntegrationTestFixture"/>'s OneTimeSetUp and stopped in OneTimeTearDown.
    /// Tests interact with it via <see cref="ResetState"/>, <see cref="QueueScenario"/>, and <see cref="GetRequests"/>.
    /// </summary>
    public static class FakeBackend
    {
        // ── Public surface ───────────────────────────────────────────

        public static bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>Base URL the AbxrLib runtime should be pointed at (includes trailing slash).</summary>
        public static string BaseUrl
        {
            get
            {
                if (_baseUrl == null) throw new InvalidOperationException("FakeBackend not started. Call Start() first.");
                return _baseUrl;
            }
        }

        /// <summary>Start the Python server. Idempotent — re-uses the existing process if already running.</summary>
        public static void Start(int startupTimeoutSeconds = 15)
        {
            if (IsRunning) return;

            int port = PickFreeTcpPort();
            string scriptPath = ResolveServerScriptPath();
            string python = ResolvePythonInterpreter();

            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"\"{scriptPath}\" --port {port}",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _stdoutBuffer = new StringBuilder();
            _stderrBuffer = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_stdoutBuffer) _stdoutBuffer.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_stderrBuffer) _stderrBuffer.AppendLine(e.Data); };

            if (!p.Start())
                throw new Exception($"Failed to start Python process: {python} {psi.Arguments}");

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _process = p;
            _baseUrl = $"http://127.0.0.1:{port}/";
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // Wait for /__control/health to respond before returning. If the process dies
            // during startup (syntax error, port collision), report the
            // captured stderr so the failure is actionable.
            var deadline = DateTime.UtcNow.AddSeconds(startupTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (p.HasExited)
                    throw new Exception($"Fake backend process exited during startup (exit code {p.ExitCode}).\nSTDERR:\n{GetStderr()}\nSTDOUT:\n{GetStdout()}");
                try
                {
                    using var resp = _http.GetAsync(_baseUrl + "__control/health").GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode) return;
                }
                catch { /* not up yet */ }
                Thread.Sleep(200);
            }
            string stderr = GetStderr();
            Stop();
            throw new TimeoutException($"Fake backend did not become healthy within {startupTimeoutSeconds}s.\nSTDERR:\n{stderr}");
        }

        /// <summary>Stop the Python server.</summary>
        public static void Stop()
        {
            try { _http?.Dispose(); } catch { }
            _http = null;

            var p = _process;
            _process = null;
            _baseUrl = null;
            if (p == null) return;
            try
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"FakeBackend.Stop: {ex.Message}"); }
            finally { p.Dispose(); }
        }

        /// <summary>Clear all queued scenarios and recorded requests on the server.</summary>
        public static void ResetState()
        {
            EnsureRunning();
            var resp = _http.PostAsync(_baseUrl + "__control/reset", new StringContent("{}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ResetState failed: {(int)resp.StatusCode}");
        }

        /// <summary>
        /// Queue a response for the given AbxrLib endpoint.
        /// Multiple calls for the same <paramref name="path"/> queue in order; the last one is sticky once the queue empties.
        /// </summary>
        /// <param name="path">e.g. "/v1/auth/token", "/v1/storage/config".</param>
        /// <param name="status">HTTP status to return. When null, the fake backend uses the endpoint's default status.</param>
        /// <param name="body">JSON-serializable body to return. Leave null to use the endpoint's default response body.</param>
        /// <param name="method">HTTP method this scenario applies to (default POST).</param>
        /// <param name="delayMs">Artificial delay before responding (useful for testing timeouts).</param>
        public static void QueueScenario(string path, int? status = null, object body = null, string method = "POST", int delayMs = 0)
        {
            EnsureRunning();
            var payload = new Dictionary<string, object>
            {
                { "path", path },
                { "method", method },
                { "delay_ms", delayMs },
            };
            if (status.HasValue) payload["status"] = status.Value;
            if (body != null) payload["body"] = body;
            SendScenarioPayload(payload, "QueueScenario");
        }

        /// <summary>
        /// Queue a status-only response with no response body.
        /// This sends "body": null to the fake backend, which is different from omitting body and using the
        /// endpoint default response body.
        /// </summary>
        public static void QueueEmptyBodyScenario(string path, int? status = null, string method = "POST", int delayMs = 0)
        {
            EnsureRunning();
            var payload = new Dictionary<string, object>
            {
                { "path", path },
                { "method", method },
                { "delay_ms", delayMs },
                { "body", null },
            };
            if (status.HasValue) payload["status"] = status.Value;
            SendScenarioPayload(payload, "QueueEmptyBodyScenario");
        }

        /// <summary>
        /// Queue a raw (non-JSON) response body. Useful for exercising parse-failure paths.
        /// </summary>
        public static void QueueRawScenario(string path, int status, string raw, string contentType = "application/json", string method = "POST")
        {
            EnsureRunning();
            var payload = new Dictionary<string, object>
            {
                { "path", path }, { "method", method }, { "status", status },
                { "raw", raw }, { "content_type", contentType },
            };
            SendScenarioPayload(payload, "QueueRawScenario");
        }

        private static void SendScenarioPayload(Dictionary<string, object> payload, string operationName)
        {
            string json = JsonConvert.SerializeObject(payload);
            using var resp = _http.PostAsync(_baseUrl + "__control/scenario", new StringContent(json, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"{operationName} failed: {(int)resp.StatusCode} body={json}");
        }

        /// <summary>Return everything the server has received. Pass a path to filter (e.g. "/v1/auth/token").</summary>
        public static List<RecordedRequest> GetRequests(string pathFilter = null)
        {
            EnsureRunning();
            string url = _baseUrl + "__control/requests";
            if (!string.IsNullOrEmpty(pathFilter)) url += "?path=" + Uri.EscapeDataString(pathFilter);
            using var resp = _http.GetAsync(url).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
            string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var parsed = JObject.Parse(body);
            var arr = (JArray)parsed["requests"] ?? new JArray();
            return arr.Select(token => token.ToObject<RecordedRequest>()).ToList();
        }

        // ── Internal plumbing ────────────────────────────────────────

        private static Process _process;
        private static string _baseUrl;
        private static HttpClient _http;
        private static StringBuilder _stdoutBuffer;
        private static StringBuilder _stderrBuffer;

        private static void EnsureRunning()
        {
            if (!IsRunning) throw new InvalidOperationException("FakeBackend is not running. Did OneTimeSetUp run?");
        }

        public static string GetStdout() { lock (_stdoutBuffer ?? new StringBuilder()) return _stdoutBuffer?.ToString() ?? ""; }
        public static string GetStderr() { lock (_stderrBuffer ?? new StringBuilder()) return _stderrBuffer?.ToString() ?? ""; }

        private static int PickFreeTcpPort()
        {
            // Tiny race window between Stop() and the Python server binding, but in
            // practice this is fine for tests on a single machine.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string ResolvePythonInterpreter()
        {
            string fromEnv = Environment.GetEnvironmentVariable("ABXR_TEST_PYTHON");
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv)) return fromEnv;

            // Best-effort PATH lookup. On Windows "python" is the typical entrypoint;
            // on macOS/Linux "python3" is more reliable.
            foreach (var candidate in new[] { "python3", "python" })
            {
                if (IsOnPath(candidate)) return candidate;
            }
            throw new Exception(
                "Could not find a Python interpreter. Set the ABXR_TEST_PYTHON environment variable to the python executable from your venv " +
                "(see Tests/FakeBackend/README.md), or install Python 3 on PATH.");
        }

        private static bool IsOnPath(string name)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(2000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Locate <c>server.py</c> via the compiler-injected path of this source file. This works
        /// regardless of where the package lives on disk (embedded, local, or UPM cache) because
        /// <c>CallerFilePath</c> is resolved at compile time, not runtime.
        /// </summary>
        private static string ResolveServerScriptPath()
        {
            string thisFile = GetThisFilePath();
            string runtimeDir = Path.GetDirectoryName(thisFile);  // .../Tests/Runtime
            string testsDir = Path.GetDirectoryName(runtimeDir);  // .../Tests
            string serverPath = Path.Combine(testsDir, "FakeBackend", "server.py");
            if (!File.Exists(serverPath))
                throw new FileNotFoundException($"Fake backend server script not found at {serverPath}");
            return serverPath;
        }

        private static string GetThisFilePath([CallerFilePath] string path = null) => path;
    }

    /// <summary>One request the fake server received.</summary>
    [Serializable]
    public class RecordedRequest
    {
        [JsonProperty("method")] public string Method;
        [JsonProperty("path")] public string Path;
        [JsonProperty("headers")] public Dictionary<string, string> Headers;
        [JsonProperty("query")] public Dictionary<string, string> Query;
        [JsonProperty("body_text")] public string BodyText;
        [JsonProperty("body_json")] public JObject BodyJson;
        [JsonProperty("received_at")] public double ReceivedAt;
    }
}

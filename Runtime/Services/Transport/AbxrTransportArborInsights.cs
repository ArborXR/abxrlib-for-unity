using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Transport
{
#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>ArborInsightsClient (device service) implementation of IAbxrTransport. Forwards all calls to the service.</summary>
    internal class AbxrTransportArborInsights : IAbxrTransport
    {
        public bool IsServiceTransport => true;

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<bool, string, bool> onComplete)
        {
            // If unbound (e.g. after EndSession), re-establish bind so StartAuthentication() works without StartNewSession.
            // Yields must be outside any try-catch block in C# iterators.
            if (!ArborInsightsClient.IsServiceBound())
            {
                if (!ArborInsightsClient.Bind(null))
                {
                    onComplete?.Invoke(false, "ArborInsightsClient.Bind failed", false);
                    yield break;
                }
                const int maxAttempts = 40;
                const float intervalSeconds = 0.25f;
                for (int i = 0; i < maxAttempts && !ArborInsightsClient.ServiceIsFullyInitialized(); i++)
                    yield return new WaitForSecondsRealtime(intervalSeconds);
                if (!ArborInsightsClient.ServiceIsFullyInitialized())
                {
                    onComplete?.Invoke(false, "ArborInsightsClient service not ready after bind", false);
                    yield break;
                }
            }

            try
            {
                string restUrl = Configuration.Instance.restUrl ?? "https://lib-backend.xrdm.app/";
                ArborInsightsClient.SetAuthPayloadForRequest(restUrl, payload);
                string responseJson = ArborInsightsClient.AuthRequest(payload.userId ?? "", Utils.DictToString(payload.authMechanism));
                // Device client (AAR/service) decides if the API rejected auth (e.g. 401/403); we only pass the flag.
                bool isAuthRejectedByApi = ArborInsightsClient.GetLastAuthRejected();
                // Use same success rule as auth service (AuthResponse.IsValidSuccess): full success or second-stage required.
                bool success = !string.IsNullOrEmpty(responseJson) && ParseAndCheckValidSuccess(responseJson);
                // Normalize empty failure body so auth service logs the same message for both transports.
                string body = responseJson ?? "";
                if (string.IsNullOrEmpty(body) && !success)
                    body = "No response body.";
                if (!success)
                    Logcat.Warning($"AuthRequest failed: {body}");
                onComplete?.Invoke(success, body, isAuthRejectedByApi);
            }
            catch (Exception ex)
            {
                Logcat.Error($"ArborInsights auth failed: {ex.Message}");
                onComplete?.Invoke(false, ex.Message, false);
            }
            yield return null;
        }

        public IEnumerator GetConfigCoroutine(Action<bool, string> onComplete)
        {
            try
            {
                string configJson = ArborInsightsClient.GetAppConfig();
                onComplete?.Invoke(!string.IsNullOrEmpty(configJson), configJson);
            }
            catch (Exception ex)
            {
                Logcat.Error($"ArborInsights GetAppConfig failed: {ex.Message}");
                onComplete?.Invoke(false, ex.Message);
            }
            yield return null;
        }

        public void AddEvent(string name, Dictionary<string, string> meta)
        {
            ArborInsightsClient.Event(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddTelemetry(string name, Dictionary<string, string> meta)
        {
            ArborInsightsClient.AddTelemetryEntry(name ?? "", meta ?? new Dictionary<string, string>());
        }

        public void AddLog(string logLevel, string text, Dictionary<string, string> meta)
        {
            var dict = meta ?? new Dictionary<string, string>();
            string level = (logLevel ?? "").ToUpperInvariant();
            if (level == "DEBUG") ArborInsightsClient.LogDebug(text ?? "", dict);
            else if (level == "INFO") ArborInsightsClient.LogInfo(text ?? "", dict);
            else if (level == "WARN") ArborInsightsClient.LogWarn(text ?? "", dict);
            else if (level == "ERROR") ArborInsightsClient.LogError(text ?? "", dict);
            else if (level == "CRITICAL") ArborInsightsClient.LogCritical(text ?? "", dict);
            else ArborInsightsClient.LogInfo(text ?? "", dict);
        }

        public void ForceSend()
        {
            try { ArborInsightsClient.ForceSendUnsent(); } catch { /* ignore */ }
        }

        public void StorageAdd(string name, Dictionary<string, string> entry, global::Abxr.StorageScope scope, global::Abxr.StoragePolicy policy)
        {
            long t = Utils.GetUnityTime();
            string iso = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var p = new StoragePayload
            {
                timestamp = iso,
                keepPolicy = Utils.PascalToCamelCase(policy.ToString()),
                name = name,
                data = new List<Dictionary<string, string>> { entry ?? new Dictionary<string, string>() },
                scope = Utils.PascalToCamelCase(scope.ToString())
            };
            string json = JsonConvert.SerializeObject(p);
            bool keepLatest = policy == global::Abxr.StoragePolicy.KeepLatest;
            bool sessionData = scope == global::Abxr.StorageScope.User;
            if (name == "state")
                ArborInsightsClient.StorageSetDefaultEntryFromString(json, keepLatest, "unity", sessionData);
            else
                ArborInsightsClient.StorageSetEntryFromString(name, json, keepLatest, "unity", sessionData);
        }

        public IEnumerator StorageGetCoroutine(string name, global::Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> onComplete)
        {
            string scopeParam = Utils.PascalToCamelCase(scope.ToString());
            string raw = ArborInsightsClient.StorageGetEntryAsString(name ?? "state", scopeParam);
            List<Dictionary<string, string>> result = null;
            if (!string.IsNullOrEmpty(raw))
            {
                string json = raw;
                // Device service can return a JSON-encoded string (leading "); unwrap one level so we parse the inner content.
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                {
                    try { json = JsonConvert.DeserializeObject<string>(raw); } catch { /* use raw as-is */ }
                }
                try
                {
                    var payload = JsonConvert.DeserializeObject<StoragePayload>(json);
                    if (payload?.data != null && payload.data.Count > 0)
                        result = payload.data;
                    else
                    {
                        var list = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                        if (list != null && list.Count > 0)
                            result = list;
                        else
                        {
                            var single = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                            if (single != null && single.Count > 0)
                                result = new List<Dictionary<string, string>> { single };
                        }
                    }
                }
                catch
                {
                    // Device service returns AbxrDictStrings.toString(): comma-separated key=value (e.g. "device_progress=75%,device_last_checkpoint=mid").
                    result = ParseKeyValueCommaSeparated(json);
                }
                if (result == null || result.Count == 0)
                    Logcat.Warning("Storage GET parse failed: could not parse response as JSON or key=value format.");
            }
            onComplete?.Invoke(result);
            yield return null;
        }

        /// <summary>Parses device service format: key=value,key=value (AbxrDictStrings.toString()). Escapes: \\ and \".</summary>
        private static List<Dictionary<string, string>> ParseKeyValueCommaSeparated(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var dict = new Dictionary<string, string>();
            int i = 0;
            while (i < text.Length)
            {
                int eq = text.IndexOf('=', i);
                if (eq < 0) break;
                string key = UnescapeKeyValue(text.Substring(i, eq - i));
                i = eq + 1;
                int nextComma = text.IndexOf(',', i);
                string value = nextComma < 0 ? UnescapeKeyValue(text.Substring(i)) : UnescapeKeyValue(text.Substring(i, nextComma - i));
                i = nextComma < 0 ? text.Length : nextComma + 1;
                dict[key ?? ""] = value ?? "";
            }
            if (dict.Count == 0) return null;
            return new List<Dictionary<string, string>> { dict };
        }

        private static string UnescapeKeyValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == '\\') { sb.Append('\\'); i++; }
                    else if (n == '"') { sb.Append('"'); i++; }
                    else sb.Append(s[i]);
                }
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public IEnumerator StorageDeleteCoroutine(global::Abxr.StorageScope scope, string name, Action<bool> onComplete)
        {
            if (string.IsNullOrEmpty(name))
                ArborInsightsClient.StorageRemoveMultipleEntries(scope == global::Abxr.StorageScope.User);
            else if (name == "state")
                ArborInsightsClient.StorageRemoveDefaultEntry();
            else
                ArborInsightsClient.StorageRemoveEntry(name);
            onComplete?.Invoke(true);
            yield return null;
        }

        public void OnQuit()
        {
            ArborInsightsClient.Unbind();
        }

        public void ClearAllPending()
        {
            // No-op; service session is cleared on Unbind.
        }

        public List<EventPayload> GetPendingEventsForTesting() => new List<EventPayload>();
        public List<LogPayload> GetPendingLogsForTesting() => new List<LogPayload>();
        public List<TelemetryPayload> GetPendingTelemetryForTesting() => new List<TelemetryPayload>();

        /// <summary>Parses auth response and returns true if valid success (same rule as auth service: token/modules or appId-only for second-stage).</summary>
        private static bool ParseAndCheckValidSuccess(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson)) return false;
            try
            {
                var parsed = JsonConvert.DeserializeObject<AuthResponse>(responseJson);
                return AuthResponse.IsValidSuccess(parsed);
            }
            catch
            {
                return false;
            }
        }
    }
#endif
}

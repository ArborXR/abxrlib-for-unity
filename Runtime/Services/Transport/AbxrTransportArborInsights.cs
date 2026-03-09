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

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<bool, string, long> onComplete)
        {
            // If unbound (e.g. after EndSession), re-establish bind so StartAuthentication() works without StartNewSession.
            // Yields must be outside any try-catch block in C# iterators.
            if (!ArborInsightsClient.IsServiceBound())
            {
                if (!ArborInsightsClient.Bind(null))
                {
                    onComplete?.Invoke(false, "ArborInsightsClient.Bind failed", -1);
                    yield break;
                }
                const int maxAttempts = 40;
                const float intervalSeconds = 0.25f;
                for (int i = 0; i < maxAttempts && !ArborInsightsClient.ServiceIsFullyInitialized(); i++)
                    yield return new WaitForSecondsRealtime(intervalSeconds);
                if (!ArborInsightsClient.ServiceIsFullyInitialized())
                {
                    onComplete?.Invoke(false, "ArborInsightsClient service not ready after bind", -1);
                    yield break;
                }
            }

            try
            {
                string restUrl = Configuration.Instance.restUrl ?? "https://lib-backend.xrdm.app/";
                ArborInsightsClient.SetAuthPayloadForRequest(restUrl, payload);
                string responseJson = ArborInsightsClient.AuthRequest(payload.userId ?? "", Utils.DictToString(payload.authMechanism));
                // Service returns success JSON (token/secret stripped) or a response indicating more steps (e.g. first-step before second-stage PIN) or failure. Only treat as success when it looks like AuthResponse with token or modules.
                bool success = !string.IsNullOrEmpty(responseJson) && LooksLikeSuccessAuthResponse(responseJson);
                onComplete?.Invoke(success, responseJson ?? "", -1);
                if (!success && !string.IsNullOrEmpty(responseJson))
                    Debug.LogWarning($"[AbxrLib] ArborInsights auth returned non-success response (may require second-stage or indicate failure): {responseJson}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbxrLib] ArborInsights auth failed: {ex.Message}");
                onComplete?.Invoke(false, ex.Message, -1);
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
                Debug.LogError($"[AbxrLib] ArborInsights GetAppConfig failed: {ex.Message}");
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
                keepPolicy = policy.ToString(),
                name = name,
                data = new List<Dictionary<string, string>> { entry ?? new Dictionary<string, string>() },
                scope = scope.ToString()
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
            string json = name == "state" ? ArborInsightsClient.StorageGetDefaultEntryAsString() : ArborInsightsClient.StorageGetEntryAsString(name);
            List<Dictionary<string, string>> result = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<StoragePayload>(json);
                    if (payload?.data != null && payload.data.Count > 0)
                        result = payload.data;
                    else
                    {
                        var list = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                        if (list != null) result = list;
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[AbxrLib] Storage GET parse failed: {ex.Message}"); }
            }
            onComplete?.Invoke(result);
            yield return null;
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

        /// <summary>True if the response is valid auth success (has token or modules). Service returns success JSON with token/secret stripped, or a non-success response when more steps (e.g. second-stage) are needed or auth failed.</summary>
        private static bool LooksLikeSuccessAuthResponse(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson)) return false;
            try
            {
                var parsed = JsonConvert.DeserializeObject<AuthResponse>(responseJson);
                if (parsed == null) return false;
                return !string.IsNullOrEmpty(parsed.Token) || (parsed.Modules != null && parsed.Modules.Count > 0);
            }
            catch
            {
                return false;
            }
        }
    }
#endif
}

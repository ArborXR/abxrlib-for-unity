/*
 * Copyright (c) 2024-2026 ArborXR. All rights reserved.
 *
 * Auth Handoff Test Helper for ABXRLib Tests (TakeTwo design)
 *
 * TakeTwo: Handoff is processed internally from auth_handoff (intent/command line).
 * This helper provides "launcher" side: wait for auth and return serialized AuthResponse for handoff.
 * Target-side injection is not available without a test hook in the SDK; handoff receipt tests
 * require running with auth_handoff set by the environment.
 */

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NUnit.Framework;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Helper for authentication handoff tests. Launcher side: authenticate and return handoff JSON.
    /// </summary>
    public static class AuthHandoffTestHelper
    {
        /// <summary>
        /// Serializes AuthResponse to JSON without Newtonsoft (tests avoid extra asmdef reference).
        /// </summary>
        public static string SerializeAuthResponseToJson(AuthResponse authResponse)
        {
            if (authResponse == null) return "{}";
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJsonString(sb, "Token", authResponse.Token);
            sb.Append(',');
            AppendJsonString(sb, "Secret", authResponse.Secret);
            sb.Append(',');
            AppendJsonString(sb, "AppId", authResponse.AppId);
            sb.Append(',');
            AppendJsonString(sb, "PackageName", authResponse.PackageName);
            sb.Append(",\"UserData\":");
            if (authResponse.UserData != null && authResponse.UserData.Count > 0)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in authResponse.UserData)
                {
                    if (!first) sb.Append(',');
                    AppendJsonString(sb, kv.Key, kv.Value);
                    first = false;
                }
                sb.Append('}');
            }
            else
                sb.Append("{}");
            sb.Append(",\"Modules\":[");
            if (authResponse.Modules != null && authResponse.Modules.Count > 0)
            {
                for (int i = 0; i < authResponse.Modules.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var m = authResponse.Modules[i];
                    sb.Append("{\"Id\":");
                    AppendJsonString(sb, null, m.Id);
                    sb.Append(",\"Name\":");
                    AppendJsonString(sb, null, m.Name);
                    sb.Append(",\"Target\":");
                    AppendJsonString(sb, null, m.Target);
                    sb.Append(",\"Order\":").Append(m.Order).Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendJsonString(StringBuilder sb, string key, string value)
        {
            if (key != null) { sb.Append('"'); sb.Append(EscapeJson(key)); sb.Append("\":"); }
            sb.Append('"');
            sb.Append(EscapeJson(value ?? ""));
            sb.Append('"');
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// Simulates launcher app: waits for authentication then returns serialized AuthResponse
        /// for use as handoff data. Yields the JSON string (access via coroutine current after completion).
        /// </summary>
        public static IEnumerator SimulateLauncherAppHandoff()
        {
            Debug.Log("AuthHandoffTestHelper: Waiting for launcher authentication...");
            yield return AuthenticationTestHelper.EnsureAuthenticated();

            if (!Abxr.GetIsAuthenticated())
            {
                Debug.LogError("AuthHandoffTestHelper: Launcher authentication failed");
                yield return null;
                yield break;
            }

            var authResponse = Abxr.GetAuthResponse();
            if (authResponse == null)
            {
                Debug.LogError("AuthHandoffTestHelper: No auth response after authentication");
                yield return null;
                yield break;
            }

            if (string.IsNullOrEmpty(authResponse.PackageName))
            {
                Debug.LogError("AuthHandoffTestHelper: No PackageName in auth response");
                yield return null;
                yield break;
            }

            string handoffJson = SerializeAuthResponseToJson(authResponse);
            Debug.Log($"AuthHandoffTestHelper: Handoff JSON ready, PackageName={authResponse.PackageName}");
            yield return handoffJson;
        }

        /// <summary>
        /// Validates that received auth data has required handoff fields
        /// </summary>
        public static bool ValidateHandoffProcessing(AuthResponse received, string expectedPackageName)
        {
            if (received == null)
            {
                Debug.LogError("AuthHandoffTestHelper: Received auth data is null");
                return false;
            }
            if (string.IsNullOrEmpty(received.Token))
            {
                Debug.LogError("AuthHandoffTestHelper: Missing token");
                return false;
            }
            if (string.IsNullOrEmpty(received.Secret))
            {
                Debug.LogError("AuthHandoffTestHelper: Missing secret");
                return false;
            }
            if (received.PackageName != expectedPackageName)
            {
                Debug.LogError($"AuthHandoffTestHelper: PackageName mismatch. Expected: {expectedPackageName}, Got: {received.PackageName}");
                return false;
            }
            Debug.Log("AuthHandoffTestHelper: Handoff validation successful");
            return true;
        }

        public static void AssertTargetAppAuthenticated()
        {
            Assert.IsTrue(Abxr.GetIsAuthenticated(), "Target app should be authenticated after handoff");
            Debug.Log("AuthHandoffTestHelper: Target app authentication verified");
        }
    }
}

// Copyright (c) 2026 ArborXR. All rights reserved.
// Clears the unitTest* credential values on AppConfig assets while a non-development player builds,
// then restores them. The fields themselves are always compiled and serialized — wrapping them in
// #if changes the asset's serialization layout between build flavors and corrupts the baked asset
// (see Configuration.cs) — so this scrubs only the VALUES, keeping test PINs/emails/JWTs out of
// release binaries without touching the layout.
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbxrLib.Editor
{
    internal class UnitTestFieldScrubber : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string SessionKeyPrefix = "AbxrLib.UnitTestFieldScrubber.";
        private const string SessionKeyGuids = SessionKeyPrefix + "guids";

        public int callbackOrder => 0;

        [System.Serializable]
        private class SavedFields
        {
            public bool unitTestConfigEnabled;
            public string unitTestAuthPin;
            public string unitTestAuthBadPin;
            public string unitTestAuthText;
            public string unitTestAuthEmail;
            public string unitTestAuthEmailDomain;
            public string unitTestDeviceId;
            public string unitTestFingerprint;
            public string unitTestSsoAccessToken;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            // A failed build never reaches OnPostprocessBuild; recover any values stashed by a
            // previous build before deciding whether to scrub for this one.
            RestoreAll();

            if ((report.summary.options & BuildOptions.Development) != 0)
                return;

            var scrubbedGuids = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AppConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<AppConfig>(path);
                if (config == null || !HasAnyValues(config)) continue;

                SessionState.SetString(SessionKeyPrefix + guid, JsonUtility.ToJson(Capture(config)));
                Clear(config);
                EditorUtility.SetDirty(config);
                scrubbedGuids.Add(guid);
                Debug.Log($"[AbxrLib] Cleared unit-test credential fields on '{path}' for this release build; they will be restored after the build.");
            }

            if (scrubbedGuids.Count == 0) return;
            SessionState.SetString(SessionKeyGuids, string.Join(";", scrubbedGuids));
            AssetDatabase.SaveAssets();
        }

        public void OnPostprocessBuild(BuildReport report) => RestoreAll();

        // SessionState survives domain reloads, so leftovers from a build that failed mid-way are
        // restored on the next script reload even if no new build is started.
        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload() => RestoreAll();

        private static void RestoreAll()
        {
            var guids = SessionState.GetString(SessionKeyGuids, "");
            if (string.IsNullOrEmpty(guids)) return;
            SessionState.EraseString(SessionKeyGuids);

            var restoredAny = false;
            foreach (var guid in guids.Split(';'))
            {
                var key = SessionKeyPrefix + guid;
                var json = SessionState.GetString(key, "");
                SessionState.EraseString(key);
                if (string.IsNullOrEmpty(json)) continue;

                var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null) continue;

                Apply(config, JsonUtility.FromJson<SavedFields>(json));
                EditorUtility.SetDirty(config);
                restoredAny = true;
            }

            if (restoredAny)
                AssetDatabase.SaveAssets();
        }

        private static bool HasAnyValues(AppConfig c) =>
            c.unitTestConfigEnabled
            || !string.IsNullOrEmpty(c.unitTestAuthPin)
            || !string.IsNullOrEmpty(c.unitTestAuthBadPin)
            || !string.IsNullOrEmpty(c.unitTestAuthText)
            || !string.IsNullOrEmpty(c.unitTestAuthEmail)
            || !string.IsNullOrEmpty(c.unitTestAuthEmailDomain)
            || !string.IsNullOrEmpty(c.unitTestDeviceId)
            || !string.IsNullOrEmpty(c.unitTestFingerprint)
            || !string.IsNullOrEmpty(c.unitTestSsoAccessToken);

        private static SavedFields Capture(AppConfig c) => new SavedFields
        {
            unitTestConfigEnabled = c.unitTestConfigEnabled,
            unitTestAuthPin = c.unitTestAuthPin,
            unitTestAuthBadPin = c.unitTestAuthBadPin,
            unitTestAuthText = c.unitTestAuthText,
            unitTestAuthEmail = c.unitTestAuthEmail,
            unitTestAuthEmailDomain = c.unitTestAuthEmailDomain,
            unitTestDeviceId = c.unitTestDeviceId,
            unitTestFingerprint = c.unitTestFingerprint,
            unitTestSsoAccessToken = c.unitTestSsoAccessToken
        };

        private static void Clear(AppConfig c)
        {
            c.unitTestConfigEnabled = false;
            c.unitTestAuthPin = "";
            c.unitTestAuthBadPin = "";
            c.unitTestAuthText = "";
            c.unitTestAuthEmail = "";
            c.unitTestAuthEmailDomain = "";
            c.unitTestDeviceId = "";
            c.unitTestFingerprint = "";
            c.unitTestSsoAccessToken = "";
        }

        private static void Apply(AppConfig c, SavedFields s)
        {
            c.unitTestConfigEnabled = s.unitTestConfigEnabled;
            c.unitTestAuthPin = s.unitTestAuthPin;
            c.unitTestAuthBadPin = s.unitTestAuthBadPin;
            c.unitTestAuthText = s.unitTestAuthText;
            c.unitTestAuthEmail = s.unitTestAuthEmail;
            c.unitTestAuthEmailDomain = s.unitTestAuthEmailDomain;
            c.unitTestDeviceId = s.unitTestDeviceId;
            c.unitTestFingerprint = s.unitTestFingerprint;
            c.unitTestSsoAccessToken = s.unitTestSsoAccessToken;
        }
    }
}

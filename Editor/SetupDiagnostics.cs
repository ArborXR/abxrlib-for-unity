// Copyright (c) 2026 ArborXR. All rights reserved.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
// UnityEditor also has a legacy PackageInfo, so name the Package Manager one explicitly.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Object = UnityEngine.Object;

namespace AbxrLib.Editor
{
    /// <summary>
    /// Builds the plain-text diagnostics report a developer pastes into a support request: package and Editor
    /// versions, Android player settings, headset defines, the configuration (with every secret redacted), the
    /// state of the optional sign-in UI, and the result of each setup wizard check. Nothing here validates anything
    /// on its own; the checks come from <see cref="SetupWizardChecks"/> so the report and the wizard always agree.
    ///
    /// Configuration fields reach the report through an explicit allowlist, never reflection, so a field added to
    /// <see cref="AppConfig"/> later is left out until someone decides how it should print. Tokens and the auth
    /// secret only ever pass through <see cref="DescribeSecret"/>.
    /// </summary>
    internal static class SetupDiagnostics
    {
        /// <summary>EditorPrefs key for the config mode: every allowlisted value, or only the ones changed from default.</summary>
        internal const string IncludeAllConfigPref = "Abxr_diagnosticsIncludeAllConfig";

        internal static bool IncludeAllConfig
        {
            get => EditorPrefs.GetBool(IncludeAllConfigPref, false);
            set => EditorPrefs.SetBool(IncludeAllConfigPref, value);
        }

        /// <summary>
        /// Builds the report in the current config mode, puts it on the clipboard, and echoes it to the Console so
        /// it survives a clipboard that did not take (headless Editors, some remote desktops).
        /// </summary>
        internal static void CopyToClipboard()
        {
            string report = Build(IncludeAllConfig);
            EditorGUIUtility.systemCopyBuffer = report;
            Logcat.Info("AbxrLib diagnostics\n" + report);
        }

        internal static string Build(bool includeAllConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AbxrLib diagnostics");

            PackageSection(sb);
            EditorSection(sb);
            AndroidSection(sb);
            HeadsetSection(sb);
            ConfigSection(sb, Core.GetConfig(), includeAllConfig);
            SignInUiSection(sb);
            ChecksSection(sb);

            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Sections
        // ---------------------------------------------------------------------------------------------------------

        private static void PackageSection(StringBuilder sb)
        {
            Header(sb, "Package");
            PackageInfo self = SetupWizardChecks.SelfPackage();
            string installed = SetupWizardChecks.InstalledPackageVersion();
            Line(sb, "version", installed);

            // The constant and package.json are synced by hand; showing both only when they differ makes drift
            // visible without cluttering every report with the same number twice.
            if (installed != AbxrLibVersion.Version) Line(sb, "version (AbxrLibVersion const)", AbxrLibVersion.Version);

            if (self == null)
            {
                Line(sb, "source", "not installed as a package (source copy under Assets/)");
                return;
            }

            string source = self.source.ToString();
            if (self.source == PackageSource.Git && self.git != null)
            {
                string hash = self.git.hash ?? "";
                if (hash.Length > 7) hash = hash.Substring(0, 7);
                string revision = string.IsNullOrEmpty(self.git.revision) ? "" : $" {self.git.revision}";
                source += $" ({hash}{revision})".Replace("( ", "(");
            }
            Line(sb, "source", source);
        }

        private static void EditorSection(StringBuilder sb)
        {
            Header(sb, "Editor");
            Line(sb, "unity", Application.unityVersion);
            Line(sb, "os", SystemInfo.operatingSystem);
            Line(sb, "build target", EditorUserBuildSettings.activeBuildTarget.ToString());
        }

        private static void AndroidSection(StringBuilder sb)
        {
            // Printed for every project, not only when Android is active: a desktop-target project that is about
            // to build for a headset is exactly the one whose Android settings support wants to see.
            Header(sb, "Android player settings");
            Line(sb, "min sdk", ((int)PlayerSettings.Android.minSdkVersion).ToString(CultureInfo.InvariantCulture));
            int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            Line(sb, "target sdk", targetSdk == 0 ? "auto (highest installed)" : targetSdk.ToString(CultureInfo.InvariantCulture));
            Line(sb, "scripting backend", PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android).ToString());
            Line(sb, "architectures", PlayerSettings.Android.targetArchitectures.ToString());
        }

        private static void HeadsetSection(StringBuilder sb)
        {
            Header(sb, "Headset support");

            // The same assembly probes CheckHeadsetSdk uses, repeated here because that check only speaks up when
            // the world-space UI is installed and support wants this line for every project.
            var names = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();
            var sdks = new List<string>();
            if (names.Any(n => n == "Unity.XR.PICO")) sdks.Add("PICO");
            if (names.Any(n => n.Contains("Oculus") || n.Contains("OVR"))) sdks.Add("Meta");
            if (names.Any(n => n.Contains("OpenXR"))) sdks.Add("OpenXR");
            Line(sb, "sdks in project", sdks.Count == 0 ? "none detected" : string.Join(", ", sdks));

            BuildTargetGroup selected = EditorUserBuildSettings.selectedBuildTargetGroup;
            foreach (string define in new[] { "META_QR_AVAILABLE", "PICO_SDK_3_4_OR_NEWER" })
            {
                var groups = new List<string>();
                if (BuildDefines.Has(define, BuildTargetGroup.Android)) groups.Add("Android");
                if (selected != BuildTargetGroup.Android && BuildDefines.Has(define, selected)) groups.Add(selected.ToString());
                Line(sb, define, groups.Count == 0 ? "not set" : "set (" + string.Join(", ", groups) + ")");
            }
        }

        private static void ConfigSection(StringBuilder sb, AppConfig config, bool includeAll)
        {
            Header(sb, includeAll ? "Config (all values)" : "Config (changed from default)");

            if (config == null)
            {
                Line(sb, "config", "not loaded (Unity is still compiling or importing, or the asset cannot be loaded)");
                return;
            }

            // Identity and credentials print in both modes. Secrets are described, never printed.
            Line(sb, "buildType", config.buildType);
            Line(sb, "useAppTokens", Format(config.useAppTokens));
            Line(sb, "appID", string.IsNullOrEmpty(config.appID) ? "not set" : config.appID);
            Line(sb, "orgID", string.IsNullOrEmpty(config.orgID) ? "not set" : config.orgID);
            if (!string.IsNullOrEmpty(config.launcherAppID)) Line(sb, "launcherAppID", config.launcherAppID);
            Line(sb, "appToken", DescribeSecret(config.appToken, expectJwt: true));
            Line(sb, "orgToken", DescribeSecret(config.orgToken, expectJwt: true));
            Line(sb, "authSecret", DescribeSecret(config.authSecret, expectJwt: false));
            Line(sb, "restUrl", string.IsNullOrEmpty(config.restUrl) ? "not set" : config.restUrl);
            Line(sb, "credentials", SetupWizardChecks.CredentialsAreValid(config)
                ? "valid"
                : SetupWizardChecks.DescribeCredentialProblem(config));

            // Tuning fields compare against a fresh instance so "default" means whatever this version ships with,
            // not a second hand-maintained list of numbers.
            var defaults = ScriptableObject.CreateInstance<AppConfig>();
            try
            {
                int before = sb.Length;

                Field(sb, "authUIFollowCamera", config, defaults, c => c.authUIFollowCamera, includeAll);
                Field(sb, "enableDirectTouchInteraction", config, defaults, c => c.enableDirectTouchInteraction, includeAll);
                Field(sb, "authUIDistanceFromCamera", config, defaults, c => c.authUIDistanceFromCamera, includeAll);
                Field(sb, "headsetTracking", config, defaults, c => c.headsetTracking, includeAll);
                Field(sb, "positionTrackingPeriodSeconds", config, defaults, c => c.positionTrackingPeriodSeconds, includeAll);
                Field(sb, "defaultMaxDistanceLimit", config, defaults, c => c.defaultMaxDistanceLimit, includeAll);
                Field(sb, "defaultAutoCreateTriggerCollider", config, defaults, c => c.defaultAutoCreateTriggerCollider, includeAll);
                Field(sb, "enableAutoStartAuthentication", config, defaults, c => c.enableAutoStartAuthentication, includeAll);
                Field(sb, "authenticationStartDelay", config, defaults, c => c.authenticationStartDelay, includeAll);
                Field(sb, "enableAutoStartModules", config, defaults, c => c.enableAutoStartModules, includeAll);
                Field(sb, "enableAutoAdvanceModules", config, defaults, c => c.enableAutoAdvanceModules, includeAll);
                Field(sb, "enableReturnTo", config, defaults, c => c.enableReturnTo, includeAll);
                Field(sb, "enablePinPadGuestAccess", config, defaults, c => c.enablePinPadGuestAccess, includeAll);
                Field(sb, "recordIpAddress", config, defaults, c => c.recordIpAddress, includeAll);
                Field(sb, "telemetryTrackingPeriodSeconds", config, defaults, c => c.telemetryTrackingPeriodSeconds, includeAll);
                Field(sb, "frameRateTrackingPeriodSeconds", config, defaults, c => c.frameRateTrackingPeriodSeconds, includeAll);
                Field(sb, "sendRetriesOnFailure", config, defaults, c => c.sendRetriesOnFailure, includeAll);
                Field(sb, "sendRetryIntervalSeconds", config, defaults, c => c.sendRetryIntervalSeconds, includeAll);
                Field(sb, "sendNextBatchWaitSeconds", config, defaults, c => c.sendNextBatchWaitSeconds, includeAll);
                Field(sb, "requestTimeoutSeconds", config, defaults, c => c.requestTimeoutSeconds, includeAll);
                Field(sb, "stragglerTimeoutSeconds", config, defaults, c => c.stragglerTimeoutSeconds, includeAll);
                Field(sb, "maxCallFrequencySeconds", config, defaults, c => c.maxCallFrequencySeconds, includeAll);
                Field(sb, "dataEntriesPerSendAttempt", config, defaults, c => c.dataEntriesPerSendAttempt, includeAll);
                Field(sb, "storageEntriesPerSendAttempt", config, defaults, c => c.storageEntriesPerSendAttempt, includeAll);
                Field(sb, "pruneSentItemsOlderThanHours", config, defaults, c => c.pruneSentItemsOlderThanHours, includeAll);
                Field(sb, "maximumCachedItems", config, defaults, c => c.maximumCachedItems, includeAll);
                Field(sb, "retainLocalAfterSent", config, defaults, c => c.retainLocalAfterSent, includeAll);
                Field(sb, "enableArborInsightsClient", config, defaults, c => c.enableArborInsightsClient, includeAll);
                Field(sb, "enableArborMdmClient", config, defaults, c => c.enableArborMdmClient, includeAll);
                Field(sb, "enableLearnerLauncherMode", config, defaults, c => c.enableLearnerLauncherMode, includeAll);
                Field(sb, "enableAutomaticTelemetry", config, defaults, c => c.enableAutomaticTelemetry, includeAll);
                Field(sb, "enableSceneEvents", config, defaults, c => c.enableSceneEvents, includeAll);
                Field(sb, "maxDictionarySize", config, defaults, c => c.maxDictionarySize, includeAll);
                // The switch only; the unit-test PIN, email, and token fields it guards never print.
                Field(sb, "unitTestConfigEnabled", config, defaults, c => c.unitTestConfigEnabled, includeAll);

                if (!includeAll && sb.Length == before) Line(sb, "other settings", "all defaults");
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        private static void SignInUiSection(StringBuilder sb)
        {
            Header(sb, "Sign-in UI");
            bool installed = SetupWizardChecks.WorldSpaceUiIsInstalled();
            bool imported = SetupWizardChecks.WorldSpaceUiFilesImported();
            Line(sb, "world-space ui", installed ? "installed" : imported ? "imported, not compiling" : "not installed (optional)");

            List<string> copies = SetupWizardChecks.ImportedWorldSpaceCopies();
            if (copies.Count > 0) Line(sb, "imported copies", string.Join(", ", copies));
            if (installed) Line(sb, "tmp essentials", SetupWizardChecks.TmpEssentialsImported() ? "imported" : "missing");
        }

        private static void ChecksSection(StringBuilder sb)
        {
            Header(sb, "Setup checks");
            foreach (SetupWizardChecks.Check check in SetupWizardChecks.Run())
                sb.Append("  [").Append(check.Severity).Append("] ").AppendLine(check.Title);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Formatting
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// How a secret is reported: whether it is there and, for a token, whether it has a token's shape. The value
        /// itself never leaves this method.
        /// </summary>
        internal static string DescribeSecret(string value, bool expectJwt)
        {
            if (string.IsNullOrEmpty(value)) return "not set";
            if (!expectJwt) return "set";
            return SetupWizardChecks.LooksLikeJwt(value) ? "set (JWT)" : "set (not a JWT)";
        }

        private static void Header(StringBuilder sb, string title)
        {
            sb.AppendLine();
            sb.AppendLine(title);
        }

        private static void Line(StringBuilder sb, string name, string value) =>
            sb.Append("  ").Append(name).Append(": ").AppendLine(value);

        private static void Field<T>(StringBuilder sb, string name, AppConfig config, AppConfig defaults,
            Func<AppConfig, T> read, bool includeAll)
        {
            T value = read(config);
            if (includeAll || !EqualityComparer<T>.Default.Equals(value, read(defaults)))
                Line(sb, name, Format(value));
        }

        private static string Format<T>(T value)
        {
            if (value is bool b) return b ? "true" : "false";
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return value?.ToString() ?? "null";
        }
    }
}

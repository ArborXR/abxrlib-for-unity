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

        /// <summary>One tuning field the report can print, with the reader that fetches it from a configuration.</summary>
        internal sealed class ReportedField
        {
            public readonly string Name;
            public readonly Func<AppConfig, object> Read;

            public ReportedField(string name, Func<AppConfig, object> read)
            {
                Name = name;
                Read = read;
            }
        }

        /// <summary>
        /// Identity and credential fields, printed in both modes. Each reader returns the text to print: identifiers as
        /// they are, secrets through <see cref="DescribeSecret"/> so the value itself never reaches the report.
        /// </summary>
        internal static readonly ReportedField[] IdentityFields =
        {
            new ReportedField("buildType", c => c.buildType),
            new ReportedField("useAppTokens", c => c.useAppTokens),
            new ReportedField("appID", c => OrNotSet(c.appID)),
            new ReportedField("orgID", c => OrNotSet(c.orgID)),
            new ReportedField("launcherAppID", c => OrNotSet(c.launcherAppID)),
            new ReportedField("appToken", c => DescribeSecret(c.appToken, expectJwt: true)),
            new ReportedField("orgToken", c => DescribeSecret(c.orgToken, expectJwt: true)),
            new ReportedField("authSecret", c => DescribeSecret(c.authSecret, expectJwt: false)),
            new ReportedField("restUrl", c => OrNotSet(c.restUrl))
        };

        /// <summary>
        /// Fields deliberately kept out of the report. Prefab references have nothing to say in text, and the unit-test
        /// credentials are secrets with no support value. Every public AppConfig field must appear here or in one of
        /// the two lists above, or SetupDiagnosticsTests fails - so adding a field forces a decision about how it prints.
        /// </summary>
        internal static readonly string[] ExcludedFields =
        {
            "KeyboardPrefab", "PinPrefab",
            "unitTestAuthPin", "unitTestAuthBadPin", "unitTestAuthText", "unitTestAuthEmail", "unitTestAuthEmailDomain",
            "unitTestDeviceId", "unitTestFingerprint", "unitTestSsoAccessToken"
        };

        /// <summary>
        /// Tuning fields, in report order. Printed when changed from default, or always in all-values mode. Defaults are
        /// read from a fresh AppConfig at report time, so this list carries no numbers to keep in sync.
        /// </summary>
        internal static readonly ReportedField[] TuningFields =
        {
            new ReportedField("authUIFollowCamera", c => c.authUIFollowCamera),
            new ReportedField("enableDirectTouchInteraction", c => c.enableDirectTouchInteraction),
            new ReportedField("authUIDistanceFromCamera", c => c.authUIDistanceFromCamera),
            new ReportedField("headsetTracking", c => c.headsetTracking),
            new ReportedField("positionTrackingPeriodSeconds", c => c.positionTrackingPeriodSeconds),
            new ReportedField("defaultMaxDistanceLimit", c => c.defaultMaxDistanceLimit),
            new ReportedField("defaultAutoCreateTriggerCollider", c => c.defaultAutoCreateTriggerCollider),
            new ReportedField("enableAutoStartAuthentication", c => c.enableAutoStartAuthentication),
            new ReportedField("authenticationStartDelay", c => c.authenticationStartDelay),
            new ReportedField("enableAutoStartModules", c => c.enableAutoStartModules),
            new ReportedField("enableAutoAdvanceModules", c => c.enableAutoAdvanceModules),
            new ReportedField("enableReturnTo", c => c.enableReturnTo),
            new ReportedField("enablePinPadGuestAccess", c => c.enablePinPadGuestAccess),
            new ReportedField("recordIpAddress", c => c.recordIpAddress),
            new ReportedField("telemetryTrackingPeriodSeconds", c => c.telemetryTrackingPeriodSeconds),
            new ReportedField("frameRateTrackingPeriodSeconds", c => c.frameRateTrackingPeriodSeconds),
            new ReportedField("sendRetriesOnFailure", c => c.sendRetriesOnFailure),
            new ReportedField("sendRetryIntervalSeconds", c => c.sendRetryIntervalSeconds),
            new ReportedField("sendNextBatchWaitSeconds", c => c.sendNextBatchWaitSeconds),
            new ReportedField("requestTimeoutSeconds", c => c.requestTimeoutSeconds),
            new ReportedField("stragglerTimeoutSeconds", c => c.stragglerTimeoutSeconds),
            new ReportedField("maxCallFrequencySeconds", c => c.maxCallFrequencySeconds),
            new ReportedField("dataEntriesPerSendAttempt", c => c.dataEntriesPerSendAttempt),
            new ReportedField("storageEntriesPerSendAttempt", c => c.storageEntriesPerSendAttempt),
            new ReportedField("pruneSentItemsOlderThanHours", c => c.pruneSentItemsOlderThanHours),
            new ReportedField("maximumCachedItems", c => c.maximumCachedItems),
            new ReportedField("retainLocalAfterSent", c => c.retainLocalAfterSent),
            new ReportedField("enableArborInsightsClient", c => c.enableArborInsightsClient),
            new ReportedField("enableArborMdmClient", c => c.enableArborMdmClient),
            new ReportedField("enableLearnerLauncherMode", c => c.enableLearnerLauncherMode),
            new ReportedField("enableAutomaticTelemetry", c => c.enableAutomaticTelemetry),
            new ReportedField("enableSceneEvents", c => c.enableSceneEvents),
            new ReportedField("maxDictionarySize", c => c.maxDictionarySize),
            // The switch only; the unit-test PIN, email, and token fields it guards are in ExcludedFields.
            new ReportedField("unitTestConfigEnabled", c => c.unitTestConfigEnabled)
        };

        /// <summary>
        /// Builds the report in the current config mode, echoes it to the Console, then puts it on the clipboard. The
        /// Console copy comes first so the report is already somewhere readable if the clipboard write is what fails
        /// (headless Editors, some remote desktops).
        /// </summary>
        internal static void CopyToClipboard()
        {
            string report = Build(IncludeAllConfig);
            Logcat.Info("AbxrLib diagnostics\n" + report);
            EditorGUIUtility.systemCopyBuffer = report;
        }

        internal static string Build(bool includeAllConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AbxrLib diagnostics");

            Section(sb, "Package", PackageSection);
            Section(sb, "Editor", EditorSection);
            Section(sb, "Android player settings", AndroidSection);
            Section(sb, "Headset support", HeadsetSection);
            Section(sb, includeAllConfig ? "Config (all values)" : "Config (changed from default)",
                s => ConfigSection(s, includeAllConfig));
            Section(sb, "Sign-in UI", SignInUiSection);
            Section(sb, "Setup checks", ChecksSection);

            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Sections
        // ---------------------------------------------------------------------------------------------------------

        private static void PackageSection(StringBuilder sb)
        {
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
            Line(sb, "unity", Application.unityVersion);
            Line(sb, "os", SystemInfo.operatingSystem);
            Line(sb, "build target", EditorUserBuildSettings.activeBuildTarget.ToString());
        }

        private static void AndroidSection(StringBuilder sb)
        {
            // Printed for every project, not only when Android is active: a desktop-target project that is about
            // to build for a headset is exactly the one whose Android settings support wants to see.
            Line(sb, "min sdk", ((int)PlayerSettings.Android.minSdkVersion).ToString(CultureInfo.InvariantCulture));
            int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            Line(sb, "target sdk", targetSdk == 0 ? "auto (highest installed)" : targetSdk.ToString(CultureInfo.InvariantCulture));
            Line(sb, "scripting backend", PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android).ToString());
            Line(sb, "architectures", PlayerSettings.Android.targetArchitectures.ToString());
        }

        private static void HeadsetSection(StringBuilder sb)
        {
            // Always printed, even though CheckHeadsetSdk only speaks up when the world-space UI is installed: support
            // wants this line for every project. Same detection as the check, so the two cannot disagree.
            List<string> sdks = SetupWizardChecks.DetectedHeadsetSdks();
            Line(sb, "sdks in project", sdks.Count == 0 ? "none detected" : string.Join(", ", sdks));

            foreach (string define in new[] { "META_QR_AVAILABLE", "PICO_SDK_3_4_OR_NEWER" })
            {
                List<BuildTargetGroup> groups = SetupWizardChecks.GroupsWithDefine(define);
                Line(sb, define, groups.Count == 0 ? "not set" : "set (" + string.Join(", ", groups) + ")");
            }
        }

        private static void ConfigSection(StringBuilder sb, bool includeAll)
        {
            // Read-only on purpose: a support report must describe the project as it is, and the creating accessor
            // would quarantine an unloadable asset and create a default before the report could mention it. The state
            // is worded by Core, shared with the build hook, so the two never describe the same state differently.
            Core.ConfigState state = Core.TryGetLoadedConfig(out AppConfig config);
            // Loaded prints the path the asset was actually found at, which is not always the canonical one: Resources.Load
            // answers for any Resources folder. An in-memory instance (tests) has no path and is named as such.
            string assetPath = state == Core.ConfigState.Loaded ? AssetDatabase.GetAssetPath(config) : null;
            Line(sb, "config", state != Core.ConfigState.Loaded ? Core.Describe(state)
                : string.IsNullOrEmpty(assetPath) ? "loaded (not an asset on disk)"
                : assetPath);
            if (config == null) return;

            // Identity and credentials print in both modes. Secrets are described, never printed.
            foreach (ReportedField field in IdentityFields) Line(sb, field.Name, Format(field.Read(config)));
            Line(sb, "credentials", SetupWizardChecks.CredentialsAreValid(config)
                ? "valid"
                : SetupWizardChecks.DescribeCredentialProblem(config));

            // Tuning fields compare against a fresh instance so "default" means whatever this version ships with,
            // not a second hand-maintained list of numbers.
            var defaults = ScriptableObject.CreateInstance<AppConfig>();
            try
            {
                int before = sb.Length;
                foreach (ReportedField field in TuningFields)
                {
                    object value = field.Read(config);
                    if (includeAll || !Equals(value, field.Read(defaults))) Line(sb, field.Name, Format(value));
                }

                if (!includeAll && sb.Length == before) Line(sb, "other settings", "all defaults");
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        private static void SignInUiSection(StringBuilder sb)
        {
            bool installed = SetupWizardChecks.WorldSpaceUiIsInstalled();
            bool imported = SetupWizardChecks.WorldSpaceUiFilesImported();
            Line(sb, "world-space ui", installed ? "installed" : imported ? "imported, not compiling" : "not installed (optional)");

            List<string> copies = SetupWizardChecks.ImportedWorldSpaceCopies();
            if (copies.Count > 0) Line(sb, "imported copies", string.Join(", ", copies));
            if (installed) Line(sb, "tmp essentials", SetupWizardChecks.TmpEssentialsImported() ? "imported" : "missing");
        }

        private static void ChecksSection(StringBuilder sb)
        {
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

        /// <summary>
        /// Writes one section, keeping a probe that throws from taking the rest of the report with it. The report is
        /// for environments where something is already wrong, which is exactly where a probe is likeliest to fail.
        /// </summary>
        private static void Section(StringBuilder sb, string title, Action<StringBuilder> write)
        {
            Header(sb, title);
            try
            {
                write(sb);
            }
            catch (Exception e)
            {
                Line(sb, "unavailable", e.GetType().Name + ": " + e.Message);
            }
        }

        private static void Header(StringBuilder sb, string title)
        {
            sb.AppendLine();
            sb.AppendLine(title);
        }

        private static void Line(StringBuilder sb, string name, string value) =>
            sb.Append("  ").Append(name).Append(": ").AppendLine(value);

        private static string OrNotSet(string value) => string.IsNullOrEmpty(value) ? "not set" : value;

        private static string Format(object value)
        {
            if (value is bool b) return b ? "true" : "false";
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return value?.ToString() ?? "null";
        }
    }
}

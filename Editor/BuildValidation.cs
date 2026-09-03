// Copyright (c) 2026 ArborXR. All rights reserved.
using System;
using AbxrLib.Runtime.Core;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AbxrLib.Editor
{
    /// <summary>
    /// Repeats the setup wizard's blocking findings as Console warnings when a build starts, for the developer who
    /// closed the wizard and moved on. Warnings only: nothing here fails or cancels a build. A project that builds
    /// without AbxrLib working is a support conversation; a project that cannot build at all is a blocked release.
    /// (The Android post-processor keeps its own hard stop for a token that is set but malformed - a typo guard on
    /// the Custom APK build itself, unchanged by this hook.)
    ///
    /// Only Problem-severity checks are repeated. Warning-severity items (Android settings, ArborMdmClient off) can
    /// be deliberate, and a deliberately configured project should build quietly.
    /// </summary>
    internal sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                // Read-only on purpose: GetConfig can create, migrate, or quarantine an asset, none of which belongs in
                // a build callback. Without a configuration there is nothing to check, and the checks themselves
                // would reach GetConfig, so stop here with the one warning that matters.
                AppConfig config = Core.TryGetLoadedConfig();
                if (config == null)
                {
                    Logcat.Warning("AbxrLib setup: no configuration asset was found (Assets/Resources/AbxrLib.asset), so " +
                                   "AbxrLib cannot authenticate in this build. Open Analytics for XR > Setup Wizard to create one.");
                    return;
                }

                if (!SetupWizardChecks.CredentialsAreValid(config))
                    Logcat.Warning("AbxrLib setup: " + SetupWizardChecks.DescribeCredentialProblem(config));

                foreach (SetupWizardChecks.Check check in SetupWizardChecks.Run())
                {
                    if (check.Severity != SetupWizardChecks.Severity.Problem) continue;
                    Logcat.Warning($"AbxrLib setup: {check.Title}. {check.Detail}");
                }
            }
            catch (Exception e)
            {
                // A diagnostic must never be the reason a build fails.
                Logcat.Debug("AbxrLib build validation skipped: " + e.Message);
            }
        }
    }
}

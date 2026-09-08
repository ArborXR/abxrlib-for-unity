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
                // a build callback. Each way of not having a usable configuration gets its own warning, because the
                // fix differs, and without one there is nothing further to check.
                Core.ConfigState state = Core.TryGetLoadedConfig(out AppConfig config);
                switch (state)
                {
                    case Core.ConfigState.Absent:
                        Logcat.Warning("AbxrLib setup: no configuration asset was found (Assets/Resources/AbxrLib.asset), so " +
                                       "AbxrLib cannot authenticate in this build. Open Analytics for XR > Setup Wizard to create one.");
                        return;
                    case Core.ConfigState.PresentButUnloadable:
                        Logcat.Warning("AbxrLib setup: a configuration asset exists but could not be loaded as AppConfig (a broken " +
                                       "script reference, or two copies of AbxrLib in the project), so AbxrLib cannot authenticate " +
                                       "in this build. Open Analytics for XR > Setup Wizard to repair it.");
                        return;
                    case Core.ConfigState.LegacyUnmigrated:
                        Logcat.Warning("AbxrLib setup: the configuration is still the legacy Assets/Resources/ArborXR.asset, which " +
                                       "the runtime does not load, so AbxrLib cannot authenticate in this build. Open Analytics for " +
                                       "XR > Configuration once to migrate it.");
                        break;
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
                // A diagnostic must never be the reason a build fails. Warning, not Debug: Debug compiles out unless
                // ENABLE_LOGS or a development build is defined, which would make this catch silent in a normal Editor.
                Logcat.Warning("AbxrLib setup checks were skipped for this build: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}

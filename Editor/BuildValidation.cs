// Copyright (c) 2026 ArborXR. All rights reserved.
using System;
using AbxrLib.Runtime.Core;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AbxrLib.Editor
{
    /// <summary>
    /// Repeats the setup wizard's blocking findings as Console warnings when a build starts, for the developer who
    /// closed the wizard and moved on. Warnings only: the build is never failed or cancelled from here. A project
    /// that builds without AbxrLib working is a support conversation; a project that cannot build at all is a
    /// blocked release, and the QoL project already ruled that kind of block out.
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
                AppConfig config = Core.GetConfig();
                if (config != null && !SetupWizardChecks.CredentialsAreValid(config))
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

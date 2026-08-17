using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    internal static class SetupWizardLauncher
    {
        private const string PackageName = "com.arborxr.unity";

        /// <summary>Frames to wait for the Editor to settle before opening anything. ~10 seconds at 30 fps.</summary>
        private const int MaxFramesToWaitForEditor = 300;

        private static int _framesWaited;

        static SetupWizardLauncher()
        {
            // Catches an install/upgrade that happens without a domain reload afterwards.
            Events.registeredPackages += OnRegisteredPackages;

            // The normal path: this runs on the reload that follows the install.
            EditorApplication.delayCall += BeginAutoOpen;

            // Finishes a world-space UI import that was waiting on TextMeshPro; installing that package reloaded the
            // domain, so the import has to be picked back up here.
            EditorApplication.delayCall += SetupWizardChecks.ResumePendingWorldSpaceImport;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Preferences (per project - EditorPrefs itself is shared across all of a user's projects)
        // -------------------------------------------------------------------------------------------------------------

        private static string PrefKey(string name) => $"AbxrLib.SetupWizard.{name}.{PlayerSettings.productGUID}";

        /// <summary>Whether the wizard may open on its own. Toggled from the wizard's footer.</summary>
        internal static bool AutoOpenEnabled
        {
            get => EditorPrefs.GetBool(PrefKey("AutoOpen"), true);
            set => EditorPrefs.SetBool(PrefKey("AutoOpen"), value);
        }

        /// <summary>The AbxrLib version this project last auto-opened the wizard for; empty on a first install.</summary>
        private static string ShownForVersion
        {
            get => EditorPrefs.GetString(PrefKey("ShownForVersion"), "");
            set => EditorPrefs.SetString(PrefKey("ShownForVersion"), value);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Auto-open
        // -------------------------------------------------------------------------------------------------------------

        private static void OnRegisteredPackages(PackageRegistrationEventArgs args)
        {
            bool involvesAbxrLib = false;
            foreach (var package in args.added)
                if (package.name == PackageName) involvesAbxrLib = true;
            foreach (var package in args.changedTo)
                if (package.name == PackageName) involvesAbxrLib = true;

            if (involvesAbxrLib) BeginAutoOpen();
        }

        /// <summary>
        /// Starts waiting for a moment when a window can be opened. Opening one while Unity is compiling, importing,
        /// or entering Play Mode either does nothing or gets thrown away by the reload that follows.
        /// </summary>
        private static void BeginAutoOpen()
        {
            if (!ShouldAutoOpen()) return;

            _framesWaited = 0;
            EditorApplication.update -= WaitForEditorThenOpen;
            EditorApplication.update += WaitForEditorThenOpen;
        }

        private static void WaitForEditorThenOpen()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Give up rather than wait forever: a developer who spends the whole timeout compiling or in Play Mode
                // is mid-task, and a window that appears then is an interruption. The menu item is still there.
                if (++_framesWaited <= MaxFramesToWaitForEditor) return;

                EditorApplication.update -= WaitForEditorThenOpen;
                return;
            }

            EditorApplication.update -= WaitForEditorThenOpen;

            // Re-check: the project may have finished importing into a state that no longer needs the wizard, and the
            // configuration can only be read now that the Editor is idle.
            if (!ShouldAutoOpen()) return;

            ShownForVersion = AbxrLibVersion.Version;
            SetupWizard.Open(true);
        }

        /// <summary>
        /// Whether the wizard should open by itself right now. Kept in one place so every entry point agrees, and so
        /// the reasons it declines are all visible together.
        /// </summary>
        private static bool ShouldAutoOpen()
        {
            // Command-line builds and CI must never open a window or block on one.
            if (Application.isBatchMode) return false;
            if (!AutoOpenEnabled) return false;

            string installedVersion = AbxrLibVersion.Version;
            string shownVersion = ShownForVersion;

            // Already opened for this exact version in this project.
            if (shownVersion == installedVersion) return false;

            // Upgrade of a project that is already set up: record the new version and stay out of the way. A first
            // install has no stamp at all, so it always gets the wizard.
            if (!string.IsNullOrEmpty(shownVersion) && ProjectIsAlreadySetUp())
            {
                ShownForVersion = installedVersion;
                Logcat.Info($"AbxrLib updated to {installedVersion}. This project is already configured, so the setup " +
                            "wizard was not opened - it is under Analytics for XR > Setup Wizard if you want it.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// True when this project has everything the wizard would have walked through: valid credentials and no
        /// blocking project problem. Reading the configuration here is safe because callers only ask once the Editor
        /// is idle; a configuration that cannot be read yet counts as not set up, which errs toward showing the wizard.
        /// </summary>
        private static bool ProjectIsAlreadySetUp()
        {
            if (!SetupWizardChecks.CredentialsAreValid(Core.GetConfig())) return false;

            // A configured project with no world-space UI is almost always one that just upgraded across the release
            // where the keyboard and PIN pad stopped shipping inside the package. Left alone it would keep working
            // until the first sign-in prompt failed to appear, so let the wizard open and say so. This costs one
            // window per version, not per load, and a project that deliberately runs core-only can switch off
            // "Open automatically" in the wizard footer.
            if (!SetupWizardChecks.WorldSpaceUiIsInstalled()) return false;

            // Only relevant once there is UI to draw with those fonts.
            if (!SetupWizardChecks.TmpEssentialsImported()) return false;

            return true;
        }
    }
}

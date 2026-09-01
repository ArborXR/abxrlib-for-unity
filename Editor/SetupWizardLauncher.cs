using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using AbxrLib.Runtime.Core;
// UnityEditor also has a legacy PackageInfo, so name the Package Manager one explicitly.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    internal static class SetupWizardLauncher
    {
        private const string PackageName = "com.arborxr.unity";

        /// <summary>
        /// How long to keep waiting for the Editor to go idle before giving up. A wall-clock deadline, not a tick
        /// count: EditorApplication.update runs an order of magnitude slower in an unfocused Editor - the common
        /// case, since people start an install and switch apps - so a tick budget meant anywhere from ninety
        /// seconds to a quarter hour. Installing a package is followed by an import and a full compile, which
        /// routinely takes minutes; a short budget here meant the wizard quietly never opened on the installs that
        /// needed it most.
        /// </summary>
        private const double MaxSecondsToWaitForEditor = 300.0;

        private static double _waitDeadline;

        static SetupWizardLauncher()
        {
            // Install and upgrade: raised after the reload that applies the change, when the new version of this
            // assembly is loaded and able to receive it.
            Events.registeredPackages += OnRegisteredPackages;

            // Removal: raised before the Package Manager applies changes, while AbxrLib is still present and this
            // assembly is still loaded. registeredPackages cannot serve here - it fires after the domain reload,
            // and an uninstalled package has no assembly left to receive it, so a handler there never observes
            // its own package's removal.
            Events.registeringPackages += OnRegisteringPackages;

            // The normal path: this runs on the reload that follows the install.
            EditorApplication.delayCall += BeginAutoOpen;

            // Finishes a world-space UI import that was waiting on TextMeshPro; installing that package reloaded the
            // domain, so the import has to be picked back up here.
            EditorApplication.delayCall += SetupWizardChecks.ResumePendingWorldSpaceImport;

            EditorApplication.delayCall += WarnAboutDuplicateImports;
        }

        /// <summary>
        /// Names the duplicate imported copies in one line, because Unity's own report of this state is sixteen
        /// "Assembly with name 'X' already exists" errors that never mention the folders involved. Logged at load
        /// rather than only in the wizard: with duplicate assembly names Unity stops compiling, so the wizard may not
        /// be reachable at all.
        /// </summary>
        private static void WarnAboutDuplicateImports()
        {
            var copies = SetupWizardChecks.ImportedWorldSpaceCopies();
            if (copies.Count < 2) return;

            Logcat.Error("AbxrLib's world-space UI is imported " + copies.Count + " times: " +
                         string.Join(", ", copies) + ".\n" +
                         "Each copy declares the same assembly names, which is what the \"Assembly with name " +
                         "'AbxrLib.WorldSpace' already exists\" errors mean, and Unity will not compile until one is " +
                         "left.\nWHAT TO DO: delete all but one of those folders, then let Unity recompile. " +
                         "Analytics for XR > Setup Wizard can do it if the Editor still has the menu.");
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

        /// <summary>
        /// Set once the wizard is owed to this project and cleared when it actually opens.
        ///
        /// Installing a package can reload the domain more than once, and each reload discards the delayCall and
        /// update handlers that were waiting to open the window. Persisting the intent means a reload - or quitting
        /// and reopening the Editor - resumes it rather than losing it, which is why an install could previously
        /// finish with no wizard and no explanation.
        /// </summary>
        private static bool WizardIsOwed
        {
            get => EditorPrefs.GetBool(PrefKey("WizardIsOwed"), false);
            set => EditorPrefs.SetBool(PrefKey("WizardIsOwed"), value);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Auto-open
        // -------------------------------------------------------------------------------------------------------------

        private static void OnRegisteredPackages(PackageRegistrationEventArgs args)
        {
            if (FindAbxrLib(args.added) != null || FindAbxrLib(args.changedTo) != null) BeginAutoOpen();
        }

        private static void OnRegisteringPackages(PackageRegistrationEventArgs args)
        {
            PackageInfo removed = FindAbxrLib(args.removed);
            if (removed == null) return;

            // Removed and re-added in the same operation is a reinstall, not an uninstall - leave it alone.
            if (FindAbxrLib(args.added) != null || FindAbxrLib(args.changedTo) != null) return;

            // The "already shown for this version" stamp lives in EditorPrefs, which outlives the package. Without
            // clearing it, uninstalling and installing the same version again left the stamp in place and the wizard
            // never opened for what the developer experiences as a fresh install.
            EditorPrefs.DeleteKey(PrefKey("ShownForVersion"));
            EditorPrefs.DeleteKey(PrefKey("WizardIsOwed"));

            OfferToRemoveImportedSamples(removed);
            OfferToRemoveConfiguration();

            // A "reopen the project?" dialog used to follow, for the stale state a removal leaves behind (compile
            // errors from anything that referenced the package, cached inspectors). It cannot work from this event:
            // the reopen has to happen after the removal completes, and a deferred callback waiting for that is
            // discarded by the domain reload the removal triggers. The wizard reopening on reinstall never needed
            // the restart anyway - clearing the prefs above is what does that - so a log line is what remains.
            Logcat.Info("AbxrLib is being removed. If the Editor is left showing stale errors from the removed " +
                        "package, reopening the project clears them.");
        }

        private static PackageInfo FindAbxrLib(IEnumerable<PackageInfo> packages)
        {
            foreach (var package in packages)
                if (package.name == PackageName) return package;

            return null;
        }

        /// <summary>
        /// Offers to delete the imported samples when AbxrLib is uninstalled.
        ///
        /// Package Manager copies samples into Assets/, so Unity leaves them behind - and without the package they no
        /// longer compile, because AbxrLib.WorldSpace references AbxrLib.Runtime. This event is the last moment this
        /// assembly is loaded during an uninstall, so it is the only chance to say anything.
        ///
        /// It asks rather than deletes: the imported copy is the developer's own project files and may have been
        /// edited, which is one of the reasons for shipping it as a sample. Nothing is deleted unattended.
        /// </summary>
        private static void OfferToRemoveImportedSamples(PackageInfo removed)
        {
            // Package Manager copies samples to Assets/Samples/<package display name>/<version>/<sample>.
            string importRoot = $"Assets/Samples/{removed.displayName}";
            if (!Directory.Exists(importRoot)) return;

            // Nothing left inside - typically the files were removed by hand and only the folders remain. There is
            // nothing to lose, so tidy up without asking - though still not in batch mode: "never delete
            // unattended" covers empty folders too, and CI has no business mutating assets mid-resolve.
            if (!Directory.EnumerateFileSystemEntries(importRoot).Any())
            {
                if (!Application.isBatchMode) DeleteFolderAndEmptyParents(importRoot);
                return;
            }

            // Never prompt or delete unattended: a command-line build removing the package must not block on a dialog
            // or quietly delete project files.
            if (Application.isBatchMode)
            {
                Logcat.Info($"AbxrLib was removed, but its imported samples are still in {importRoot}. They will not " +
                            "compile without the package.\nWHAT TO DO: delete that folder, or reinstall AbxrLib.");
                return;
            }

            bool delete = EditorUtility.DisplayDialog("AbxrLib removed",
                $"AbxrLib's imported samples are still in your project:\n\n{importRoot}\n\n" +
                "They will not compile without the package, because the world-space UI references AbxrLib's runtime " +
                "assembly.\n\nDelete them? Any changes you made to those files will be lost.",
                "Delete", "Keep");

            if (!delete)
            {
                Logcat.Info($"Kept {importRoot}. It will not compile until AbxrLib is reinstalled.");
                return;
            }

            DeleteFolderAndEmptyParents(importRoot);
        }

        /// <summary>
        /// Deletes a folder and then any parent it leaves empty. Unity's DeleteAsset only removes what it is given,
        /// so deleting the import folder used to leave an empty "Assets/Samples" (and its .meta) sitting in the
        /// project looking like something failed.
        /// </summary>
        private static void DeleteFolderAndEmptyParents(string folder)
        {
            if (!AssetDatabase.DeleteAsset(folder))
            {
                Logcat.Warning($"Could not delete {folder}.\nWHAT TO DO: delete the folder in the Project window.");
                return;
            }

            Logcat.Info($"Deleted {folder}.");

            // Walk up while each parent is empty, stopping before Assets itself.
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(parent) && parent != "Assets")
            {
                if (!Directory.Exists(parent) || Directory.EnumerateFileSystemEntries(parent).Any()) break;
                if (!AssetDatabase.DeleteAsset(parent)) break;

                Logcat.Info($"Removed the empty folder {parent}.");
                parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Points out the configuration asset, which is a project asset and so survives uninstalling.
        ///
        /// Keeping it is usually right - it holds the App Token - but it is also why installing AbxrLib again reports
        /// the project as already configured instead of starting fresh, which is confusing if the uninstall was meant
        /// to be a clean slate. Keep is the default: credentials are not something to delete on a guess.
        /// </summary>
        private static void OfferToRemoveConfiguration()
        {
            const string configPath = "Assets/Resources/AbxrLib.asset";
            if (!File.Exists(configPath)) return;

            if (Application.isBatchMode)
            {
                Logcat.Info($"AbxrLib was removed. Its configuration is still at {configPath}, so installing AbxrLib " +
                            "again will pick up the existing settings.");
                return;
            }

            // The destructive choice must be the ok button: DisplayDialog returns false for the cancel button AND
            // for Escape or closing the window, so whatever cancel means is also what a dismissal does. With the
            // buttons the other way around, dismissing this dialog deleted the App Token.
            bool delete = EditorUtility.DisplayDialog("AbxrLib removed",
                $"AbxrLib's configuration is still in your project:\n\n{configPath}\n\n" +
                "It holds your App Token and settings. Keeping it means installing AbxrLib again picks up this " +
                "configuration, and the setup wizard will report the project as already configured.\n\n" +
                "Delete it only if you want a clean slate - the App Token would have to be entered again.",
                "Delete it", "Keep configuration");

            if (!delete)
            {
                Logcat.Info($"Kept {configPath}. Installing AbxrLib again will use these settings.");
                return;
            }

            if (AssetDatabase.DeleteAsset(configPath))
            {
                AssetDatabase.Refresh();
                Logcat.Info($"Deleted {configPath}.");
            }
            else
            {
                Logcat.Warning($"Could not delete {configPath}.\nWHAT TO DO: delete it in the Project window.");
            }
        }

        /// <summary>
        /// Starts waiting for a moment when a window can be opened. Opening one while Unity is compiling, importing,
        /// or entering Play Mode either does nothing or gets thrown away by the reload that follows.
        /// </summary>
        private static void BeginAutoOpen()
        {
            // Command-line builds and CI must never open a window or block on one, and a developer who switched
            // auto-open off is not second-guessed. Checked here, outside WizardIsWanted, so an owed flag stranded
            // by an earlier load survives a batch-mode run untouched and reaches the next interactive one.
            if (Application.isBatchMode || !AutoOpenEnabled) return;

            if (!WizardIsOwed && !WizardIsWanted()) return;

            // Record the decision before waiting for the Editor, so a reload in the meantime does not lose it.
            WizardIsOwed = true;

            _waitDeadline = EditorApplication.timeSinceStartup + MaxSecondsToWaitForEditor;
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
                if (EditorApplication.timeSinceStartup <= _waitDeadline) return;

                // Give up waiting, but leave WizardIsOwed set: the next Editor load picks it up instead of the
                // install silently ending with nothing shown.
                EditorApplication.update -= WaitForEditorThenOpen;
                return;
            }

            EditorApplication.update -= WaitForEditorThenOpen;

            // Re-check on the project's merits, never the owed flag: the first check usually runs mid-import, when
            // the configuration reads as null and the sample assembly is not compiled yet, so "not set up" was the
            // only answer available then. The flag records that a look is owed - it must not pre-decide the answer,
            // or an already-configured project gets this window on every upgrade.
            if (!AutoOpenEnabled || !WizardIsWanted())
            {
                WizardIsOwed = false;
                return;
            }

            WizardIsOwed = false;
            ShownForVersion = AbxrLibVersion.Version;
            SetupWizard.Open(true);
        }

        /// <summary>
        /// Whether this project's state calls for the wizard: not shown for this version yet, and not already fully
        /// set up. Deliberately silent on batch mode, the auto-open preference, and the owed flag - those belong to
        /// the callers, because this is also the post-wait re-check, and a flag consulted here would answer it
        /// before the project state got a say.
        /// </summary>
        private static bool WizardIsWanted()
        {
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

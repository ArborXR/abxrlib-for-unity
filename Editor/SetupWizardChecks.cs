using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;
// UnityEditor also has a legacy PackageInfo, so name the Package Manager one explicitly.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AbxrLib.Editor
{
    internal static class SetupWizardChecks
    {
        /// <summary>How a check is drawn: Problem blocks a working integration, Warning is worth fixing, Info is context only.</summary>
        internal enum Severity { Ok, Info, Warning, Problem }

        internal sealed class Check
        {
            public string Title;
            public string Detail;
            public Severity Severity;
            /// <summary>Button text for the one-click fix, or null when there is nothing to click.</summary>
            public string FixLabel;
            public Action Fix;
        }

        /// <summary>
        /// Inspects the project and returns the checks in the order the wizard shows them. Cheap enough to call on
        /// step entry, window focus, or a "Re-check" click, but not per-frame: callers cache the result.
        /// </summary>
        internal static List<Check> Run()
        {
            var checks = new List<Check>
            {
                CheckUnityVersion(),
                CheckDuplicateWorldSpaceImports(),
                CheckWorldSpaceUi(),
                CheckTextMeshPro(),
                CheckRequiredPackages(),
                CheckArborMdmClient(),
                CheckHeadsetSdk(),
                CheckAndroidPlayerSettings()
            };

            // A check returns null when it has nothing to say about this project, rather than padding the list with
            // rows confirming that a default was left alone.
            checks.RemoveAll(check => check == null);
            return checks;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Editor version and packages
        //
        // Nothing here hardcodes a package version. What a given dependency resolves to depends on the Editor: on
        // Unity 2021.3 com.unity.textmeshpro comes from the registry and Input System resolves to the pinned 1.8.1,
        // while on Unity 6 TextMeshPro is served from the built-in com.unity.ugui and Input System resolves forward
        // to a newer release. So the required versions are read from AbxrLib's own manifest and reported against
        // what this Editor actually registered, which is correct in every version by construction.
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Used only when AbxrLib's manifest cannot be read (for example a copy dropped into Assets/).</summary>
        private const string FallbackMinimumUnityVersion = "2021.3";

        /// <summary>
        /// First Unity version where TextMeshPro stopped shipping as its own package and moved into com.unity.ugui.
        /// From here on, the registry's com.unity.textmeshpro is a deprecated shim, so the wizard must not send
        /// anyone to install it.
        /// </summary>
        private const int TmpMergedMajor = 2023;
        private const int TmpMergedMinor = 2;

        /// <summary>AbxrLib's own package manifest, or null when it is not installed as a package.</summary>
        internal static PackageInfo SelfPackage()
        {
            try { return PackageInfo.FindForAssembly(typeof(SetupWizardChecks).Assembly); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// The version the Package Manager knows this install as - the same value it names sample import folders
        /// with. <see cref="AbxrLibVersion.Version"/> is only the fallback for a source copy with no manifest:
        /// the constant and package.json are synced by hand, and comparing a folder name against the constant
        /// turns any drift between them into a permanent "stale import" warning that re-importing cannot clear.
        /// </summary>
        internal static string InstalledPackageVersion() => SelfPackage()?.version ?? AbxrLibVersion.Version;

        /// <summary>Only the two fields needed out of package.json; JsonUtility ignores the rest of the manifest.</summary>
        [Serializable]
        private class UnityRequirement
        {
            public string unity;
            public string unityRelease;
        }

        /// <summary>
        /// AbxrLib's declared minimum Editor version. PackageInfo does not surface the manifest's "unity" field, so
        /// package.json is read from the resolved package folder; <see cref="FallbackMinimumUnityVersion"/> stands in
        /// when there is no manifest to read (a source copy under Assets/) or it cannot be parsed.
        /// </summary>
        private static void GetMinimumUnityVersion(out string version, out string release)
        {
            version = FallbackMinimumUnityVersion;
            release = "";

            var self = SelfPackage();
            if (self == null || string.IsNullOrEmpty(self.resolvedPath)) return;

            try
            {
                string manifestPath = Path.Combine(self.resolvedPath, "package.json");
                if (!File.Exists(manifestPath)) return;

                var parsed = JsonUtility.FromJson<UnityRequirement>(File.ReadAllText(manifestPath));
                if (parsed == null || string.IsNullOrEmpty(parsed.unity)) return;

                version = parsed.unity;
                release = parsed.unityRelease ?? "";
            }
            catch (Exception)
            {
                // Keep the fallback: an unreadable manifest is not worth reporting as a setup problem.
            }
        }

        private static bool TmpIsPartOfUgui() =>
            TryParseUnityVersion(Application.unityVersion, out int major, out int minor, out _) &&
            (major > TmpMergedMajor || (major == TmpMergedMajor && minor >= TmpMergedMinor));

        /// <summary>
        /// Reads a Unity version string ("2021.3", "2021.3.57f2", "6000.0.36f1"). Unity's major numbers stay
        /// comparable as plain integers across the 6000 renumbering, so a numeric compare is enough.
        /// </summary>
        internal static bool TryParseUnityVersion(string version, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (string.IsNullOrEmpty(version)) return false;

            string[] parts = version.Split('.');
            if (parts.Length < 2) return false;
            if (!int.TryParse(parts[0], out major)) return false;
            if (!int.TryParse(parts[1], out minor)) return false;

            // The third part carries the release suffix ("57f2"); only the leading digits are the patch number.
            if (parts.Length > 2)
            {
                string digits = new string(parts[2].TakeWhile(char.IsDigit).ToArray());
                int.TryParse(digits, out patch);
            }

            return true;
        }

        /// <summary>
        /// Compares the running Editor against the minimum in AbxrLib's manifest. A project below the minimum is
        /// reported rather than fixed - the only fix is opening the project in a newer Editor.
        /// </summary>
        private static Check CheckUnityVersion()
        {
            GetMinimumUnityVersion(out string required, out string requiredRelease);
            string requiredDisplay = string.IsNullOrEmpty(requiredRelease) ? required : $"{required}.{requiredRelease}";
            string running = Application.unityVersion;

            if (!TryParseUnityVersion(running, out int major, out int minor, out int patch) ||
                !TryParseUnityVersion(required + ".0", out int reqMajor, out int reqMinor, out _))
            {
                return new Check
                {
                    Title = $"Unity {running}",
                    Detail = $"AbxrLib declares Unity {requiredDisplay} or newer as its minimum.",
                    Severity = Severity.Info
                };
            }

            // Only compare the release when the minimum names one and major.minor already match.
            int requiredPatch = 0;
            if (!string.IsNullOrEmpty(requiredRelease))
                int.TryParse(new string(requiredRelease.TakeWhile(char.IsDigit).ToArray()), out requiredPatch);

            bool tooOld = major < reqMajor
                          || (major == reqMajor && minor < reqMinor)
                          || (major == reqMajor && minor == reqMinor && patch < requiredPatch);

            if (tooOld)
            {
                return new Check
                {
                    Title = $"Unity {running} is below AbxrLib's minimum",
                    Detail = $"AbxrLib supports Unity {requiredDisplay} and newer. On an older Editor its packages may " +
                             "not resolve and APIs it relies on can be missing.\nWHAT TO DO: open this project in " +
                             $"Unity {requiredDisplay} or newer.",
                    Severity = Severity.Problem
                };
            }

            return new Check
            {
                Title = $"Unity {running} is supported",
                Detail = $"AbxrLib's minimum is Unity {requiredDisplay}.",
                Severity = Severity.Ok
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        // Credentials
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// True when the configuration's credentials would pass the same validation the runtime applies on launch.
        /// <see cref="RuntimeAuthConfig.ValidateAuthFields"/> is the single authority so the wizard can never call a
        /// configuration good that the runtime then rejects.
        /// </summary>
        internal static bool CredentialsAreValid(AppConfig config)
        {
            if (config == null) return false;
            return RuntimeAuthConfig.ValidateAuthFields(config.useAppTokens, config.buildType, config.appID,
                config.orgID, config.authSecret, config.appToken, config.orgToken) == null;
        }

        /// <summary>
        /// Describes what is missing from the credentials in terms of what to do about it, or null when they are
        /// valid. The runtime's own messages are deliberately vague (they reach end users); these do not, so they say
        /// which field is wrong and where the value comes from.
        /// </summary>
        internal static string DescribeCredentialProblem(AppConfig config)
        {
            if (config == null) return "No AbxrLib configuration could be loaded yet.";
            if (CredentialsAreValid(config)) return null;

            bool isCustomApk = config.buildType == "production_custom";

            if (config.useAppTokens)
            {
                if (string.IsNullOrWhiteSpace(config.appToken))
                    return "App Token is required. In the ArborXR portal: Content Library > your app under Managed > Insights Hub tab.";
                if (!LooksLikeJwt(config.appToken))
                    return "App Token does not look like a token (a token has three parts separated by periods). Re-copy the whole value - it is easy to miss the end of it.";
                if (isCustomApk && string.IsNullOrWhiteSpace(config.orgToken))
                    return "Production (Custom APK) builds need that customer's Organization Token as well as the App Token. It is not self-serve in the portal - contact ArborXR for it.";
                if (!string.IsNullOrWhiteSpace(config.orgToken) && !LooksLikeJwt(config.orgToken))
                    return "Organization Token does not look like a token (three parts separated by periods). Clear the field to use the org the device reports, or re-copy the whole value.";
                return "Credentials are incomplete.";
            }

            if (string.IsNullOrWhiteSpace(config.appID))
                return "Application ID is required when App Tokens are off. It looks like xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.";
            if (!LooksLikeUuid(config.appID))
                return "Application ID must be a UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).";
            if (isCustomApk && (string.IsNullOrWhiteSpace(config.orgID) || string.IsNullOrWhiteSpace(config.authSecret)))
                return "Production (Custom APK) builds need Organization ID and Authorization Secret as well as the Application ID.";
            if (!string.IsNullOrWhiteSpace(config.orgID) && !LooksLikeUuid(config.orgID))
                return "Organization ID must be a UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).";
            return "Credentials are incomplete.";
        }

        /// <summary>Shape check only - matches how the runtime recognizes a token, so the wizard agrees with it.</summary>
        internal static bool LooksLikeJwt(string value) =>
            !string.IsNullOrEmpty(value) && value.Split('.').Length == 3;

        private static bool LooksLikeUuid(string value) =>
            !string.IsNullOrEmpty(value) &&
            System.Text.RegularExpressions.Regex.IsMatch(value,
                "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$");

        // ---------------------------------------------------------------------------------------------------------
        // Project checks
        // ---------------------------------------------------------------------------------------------------------

        // ---------------------------------------------------------------------------------------------------------
        // World-space UI (optional sample)
        // ---------------------------------------------------------------------------------------------------------

        private const string WorldSpaceSampleName = "World-Space UI";

        /// <summary>
        /// Type the world-space objects register themselves through. Found by name rather than by reference: core's
        /// Editor assembly must not depend on an assembly that only exists once the sample has been imported.
        /// </summary>
        private const string WorldSpaceBootstrapType = "AbxrLib.Runtime.UI.AbxrWorldSpaceBootstrap, AbxrLib.WorldSpace";

        /// <summary>
        /// True when the world-space objects are present in this project. Detected by looking for their assembly, so
        /// it answers correctly whether they arrived through the Package Manager's Import button or by hand.
        /// </summary>
        internal static bool WorldSpaceUiIsInstalled() => Type.GetType(WorldSpaceBootstrapType) != null;

        /// <summary>
        /// True when the sample's files are in the project, whether or not they compiled. Package Manager's own
        /// Samples list imports without installing anything, so a project can end up with the scripts present and the
        /// assembly missing - a different problem from never having imported them. Scanned broadly on purpose: a
        /// copy that was moved or vendored outside Assets/Samples has the same symptom, and answering "not imported"
        /// for it offers an import that would create a duplicate.
        /// </summary>
        internal static bool WorldSpaceUiFilesImported() => AllBootstrapPathsInProject().Count > 0;

        /// <summary>Asset path of the imported bootstrap script, or null when the sample is not in the project.</summary>
        private static string ImportedBootstrapPath() => ImportedBootstrapPaths().FirstOrDefault();

        /// <summary>
        /// Every imported copy of the sample. Normally one - but Package Manager's Samples list imports into a folder
        /// named after the package version, so importing again after an upgrade leaves the old copy in place and the
        /// project ends up with two. Only the canonical import root counts: what this list finds feeds the
        /// duplicate-copy delete and the stale-version re-import, and a copy someone moved or vendored elsewhere is
        /// theirs - it must never be classified as stale or deleted by a path coincidence.
        /// </summary>
        private const string SampleImportRoot = "Assets/Samples/";

        /// <summary>
        /// FindAssets matches asset names by substring, so a duplicated "AbxrWorldSpaceBootstrap 1", a
        /// "AbxrWorldSpaceBootstrap.cs.bak", or anything else carrying the token comes back too. Only the script
        /// itself marks a copy of the sample - anything looser turns stray files into phantom duplicates.
        /// </summary>
        private static bool IsBootstrapScript(string path) =>
            Path.GetFileName(path) == "AbxrWorldSpaceBootstrap.cs";

        private static List<string> ImportedBootstrapPaths()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("AbxrWorldSpaceBootstrap"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(SampleImportRoot) && IsBootstrapScript(path)) paths.Add(path);
            }

            return paths;
        }

        /// <summary>
        /// Every copy of the sample's files anywhere under Assets/, for detection only. Broader than
        /// <see cref="ImportedBootstrapPaths"/> on purpose: a copy that was moved or vendored outside Assets/Samples
        /// still means "the files are in this project" when diagnosing a missing assembly, and still collides on
        /// assembly names when counting duplicates. Nothing this scan finds is ever deleted or version-classified.
        /// </summary>
        private static List<string> AllBootstrapPathsInProject()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("AbxrWorldSpaceBootstrap"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/") && IsBootstrapScript(path)) paths.Add(path);
            }

            return paths;
        }

        /// <summary>One folder per copy of the sample found anywhere under Assets/, for duplicate reporting.</summary>
        internal static List<string> SampleCopyFolders()
        {
            var folders = new List<string>();
            foreach (string path in AllBootstrapPathsInProject())
            {
                string folder = path.Substring(0, path.LastIndexOf('/'));
                if (!folders.Contains(folder)) folders.Add(folder);
            }

            return folders;
        }

        /// <summary>
        /// The version folders holding imported copies, e.g. "Assets/Samples/AbxrLib for Unity/2.0.11". Deleting one
        /// of these removes that entire copy.
        /// </summary>
        internal static List<string> ImportedWorldSpaceCopies()
        {
            var copies = new List<string>();
            foreach (string path in ImportedBootstrapPaths())
            {
                // .../Samples/<display name>/<version>/<sample name>/AbxrWorldSpaceBootstrap.cs
                // A real copy has at least one path segment below the version folder - without requiring that,
                // a bootstrap sitting two levels under Samples makes Take(i + 3) return the script file itself,
                // and "delete the stale copy" deletes one file while the duplicate assembly error stays.
                string[] parts = path.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != "Samples" || i + 3 >= parts.Length) continue;

                    string copy = string.Join("/", parts.Take(i + 3));
                    if (!copies.Contains(copy)) copies.Add(copy);
                    break;
                }
            }

            return copies;
        }

        /// <summary>
        /// Two imported copies means two asmdefs declaring the same assembly names, which Unity reports as
        /// "Assembly with name 'AbxrLib.WorldSpace' already exists" - once per assembly, for both copies - and then
        /// refuses to compile anything. Deleting the stale copy is the whole fix, so name it plainly.
        /// </summary>
        /// <summary>
        /// The copy the duplicate fix preserves: the one matching the running version when there is one, otherwise
        /// the newest by its version folder. FindAssets order says nothing about age, so "last one found" could
        /// keep the stale copy and delete the current one.
        /// </summary>
        internal static string CopyToKeep(List<string> copies)
        {
            string current = copies.FirstOrDefault(c => c.EndsWith("/" + InstalledPackageVersion()));
            if (current != null) return current;

            string newest = copies[0];
            foreach (string copy in copies.Skip(1))
                if (CompareVersions(VersionFolder(copy), VersionFolder(newest)) > 0) newest = copy;

            return newest;
        }

        /// <summary>The version segment of a copy path ("Assets/Samples/(display name)/2.0.11" -> "2.0.11").</summary>
        private static string VersionFolder(string copy) => copy.Substring(copy.LastIndexOf('/') + 1);

        private static Check CheckDuplicateWorldSpaceImports()
        {
            List<string> folders = SampleCopyFolders();
            if (folders.Count < 2) return null;

            List<string> canonical = ImportedWorldSpaceCopies();
            int deletable = canonical.Count > 0 ? canonical.Count - 1 : 0;

            string problem =
                "Each copy declares the same assembly names, so Unity reports \"Assembly with name " +
                "'AbxrLib.WorldSpace' already exists\" and stops compiling until only one is left.\n" +
                "Copies found: " + string.Join(", ", folders) + ".\n";

            // The one-click fix only ever deletes canonical imports under Assets/Samples. A copy living anywhere
            // else is the developer's own file - possibly vendored on purpose, possibly edited - so when deleting
            // the stale canonical copies would not get the project down to one, this row can only explain.
            if (folders.Count - deletable > 1)
            {
                return new Check
                {
                    Title = "The world-space UI is in this project more than once",
                    Detail = problem +
                             "WHAT TO DO: delete all but one copy, then let Unity recompile. Copies outside " +
                             "Assets/Samples are yours to choose between - the wizard does not delete those.",
                    Severity = Severity.Problem
                };
            }

            return new Check
            {
                Title = "The world-space UI is imported more than once",
                Detail = problem +
                         $"Keeping {CopyToKeep(canonical)} and deleting the rest fixes it. Package Manager's Samples " +
                         "list imports into a folder named after the package version, so importing again after an " +
                         "upgrade adds a copy instead of replacing one - the wizard's own import replaces in place.",
                Severity = Severity.Problem,
                FixLabel = "Delete the older copies",
                Fix = DeleteStaleWorldSpaceCopies
            };
        }

        private static void DeleteStaleWorldSpaceCopies()
        {
            // Everything is recomputed at click time, not captured when the check ran: the copy set can change in
            // between (a re-import, a manual delete), and a keep chosen from the old set could put every current
            // copy on the delete list.
            List<string> copies = ImportedWorldSpaceCopies();
            if (copies.Count < 2) return;

            string keep = CopyToKeep(copies);
            List<string> stale = copies.Where(copy => copy != keep).ToList();
            if (stale.Count == 0) return;

            // DeleteAsset skips the OS trash, and the stale copy may hold edits the developer made to the sample.
            // Same rule as the uninstall cleanup: show exactly what goes away, delete nothing unattended.
            bool confirmed = EditorUtility.DisplayDialog("AbxrLib Setup",
                "Delete the older world-space UI copies?\n\n" +
                string.Join("\n", stale) + "\n\n" +
                $"Keeping {keep}.\n\n" +
                "Deleted folders do not go to the system trash - any edits made inside them are lost.",
                "Delete", "Cancel");
            if (!confirmed) return;

            foreach (string copy in stale)
            {
                if (AssetDatabase.DeleteAsset(copy)) Logcat.Info($"Deleted the duplicate world-space UI copy {copy}.");
                else Logcat.Warning($"Could not delete {copy}.\nWHAT TO DO: delete that folder in the Project window.");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The version of the imported world-space objects, read from the import folder's name - which Package
        /// Manager takes from the manifest version of the package that was installed when the import happened.
        /// Because samples are imported into Assets/ rather than resolved as a package, updating AbxrLib leaves the
        /// old copy in place - so this is compared against the installed version to catch a stale import.
        /// </summary>
        private static string ImportedWorldSpaceVersion()
        {
            string path = ImportedBootstrapPath();
            if (path == null) return null;

            // .../Samples/<display name>/<version>/<sample name>/AbxrWorldSpaceBootstrap.cs
            // Same depth rule as ImportedWorldSpaceCopies: the segment two below "Samples" is only a version
            // when something sits beneath it. In a shallower layout it would be the script's own file name,
            // which then reads as a bogus "older AbxrLib" and arms a re-import nobody needs.
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] == "Samples" && i + 3 < parts.Length) return parts[i + 2];

            return null;
        }

        /// <summary>
        /// Reports whether AbxrLib's own sign-in UI is available, and offers to import it. This is the one check that
        /// a project can legitimately fail on purpose: an app that collects user input itself does not want this UI.
        /// </summary>
        private static Check CheckWorldSpaceUi()
        {
            if (!WorldSpaceUiIsInstalled())
            {
                // Files there but no assembly: the import happened without its dependencies, which is what Package
                // Manager's own Samples > Import does - it copies files and installs nothing.
                if (WorldSpaceUiFilesImported()) return CheckImportedButNotCompiling();

                return new Check
                {
                    Title = "World-space UI not installed",
                    Detail = "AbxrLib's built-in sign-in UI - world-space keyboard, PIN pad, exit poll, and QR-code " +
                             "scanning - is an optional import, so a project that does not need it stays free of " +
                             "TextMeshPro, uGUI, and XR Interaction Toolkit.\n" +
                             "Import it to have AbxrLib ask the user for PIN or email itself. Skip it if your app " +
                             "collects input and submits it through Abxr.OnInputRequested / Abxr.OnInputSubmitted.",
                    Severity = Severity.Warning,
                    FixLabel = "Import world-space UI",
                    Fix = ImportWorldSpaceUi
                };
            }

            string imported = ImportedWorldSpaceVersion();
            if (!string.IsNullOrEmpty(imported) && imported != InstalledPackageVersion())
            {
                return new Check
                {
                    Title = "World-space UI is from an older AbxrLib",
                    Detail = $"AbxrLib is {InstalledPackageVersion()} but the imported world-space UI came from " +
                             $"{imported}. Because it is imported into Assets/ rather than resolved as a package, " +
                             "updating AbxrLib leaves the old copy behind.\nRe-importing overwrites it - any edits you " +
                             "made to those files will be replaced.",
                    Severity = Severity.Warning,
                    FixLabel = "Re-import world-space UI",
                    Fix = ImportWorldSpaceUi
                };
            }

            return new Check
            {
                Title = "World-space UI installed",
                Detail = "AbxrLib can show its own sign-in UI (keyboard, PIN pad, QR) and exit polls.",
                Severity = Severity.Ok
            };
        }

        /// <summary>Set while a TextMeshPro install is in flight so the import can finish after the domain reload.</summary>
        private const string PendingWorldSpaceImportKey = "AbxrLib.SetupWizard.PendingWorldSpaceImport";

        /// <summary>
        /// Whether TextMeshPro is actually usable in this project, which is not the same question as which package
        /// provides it. A core-only Unity 6 project has neither com.unity.textmeshpro nor uGUI 2.0, so the assembly is
        /// genuinely absent even though newer editors can supply it.
        /// </summary>
        private static bool TmpAssemblyAvailable() => Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro") != null;

        /// <summary>Which package supplies TextMeshPro on this Editor: uGUI absorbed it in 2023.2.</summary>
        private static string TmpPackageForThisEditor() =>
            TmpIsPartOfUgui() ? "com.unity.ugui" : "com.unity.textmeshpro";

        /// <summary>
        /// The sample's files are in the project but its assembly is not, so something stopped it compiling. The
        /// usual cause is importing from Package Manager's own Samples list, which copies files and installs nothing:
        /// without TextMeshPro (and the uGUI that comes with it) the UI cannot build, and the Console fills with
        /// errors naming TMPro and UnityEngine.UI.
        /// </summary>
        private static Check CheckImportedButNotCompiling()
        {
            if (!TmpAssemblyAvailable())
            {
                string package = TmpPackageForThisEditor();
                return new Check
                {
                    Title = "World-space UI is imported but cannot compile",
                    Detail = "Its scripts are in this project, but TextMeshPro is not - so the UI assembly is not " +
                             $"built and the Console shows missing TMPro and UnityEngine.UI types. Adding {package} " +
                             "fixes it.\nImporting from Package Manager > Samples copies the files without installing " +
                             "what they need; the wizard's own import adds it for you.",
                    Severity = Severity.Problem,
                    FixLabel = "Add TextMeshPro",
                    Fix = AddTmpPackage
                };
            }

            // Re-import only helps a canonical import: Sample.Import overrides previous imports under
            // Assets/Samples and nothing else, so for a copy living elsewhere it would ADD a second copy - and
            // with it the duplicate assembly names that stop the whole compile.
            if (ImportedWorldSpaceCopies().Count == 0)
            {
                return new Check
                {
                    Title = "World-space UI is in this project but not compiling",
                    Detail = "Its scripts are in this project (outside the Assets/Samples import root) and " +
                             "TextMeshPro is present, so something else is stopping the AbxrLib.WorldSpace assembly " +
                             "from building.\nWHAT TO DO: check the Console for the first compile error. If that " +
                             "copy is beyond repair, delete it first, then import the sample fresh from here or " +
                             "from Package Manager.",
                    Severity = Severity.Problem
                };
            }

            return new Check
            {
                Title = "World-space UI is imported but not compiling",
                Detail = "Its scripts are in this project and TextMeshPro is present, so something else is stopping " +
                         "the AbxrLib.WorldSpace assembly from building.\nWHAT TO DO: check the Console for the first " +
                         "compile error. Re-importing the sample replaces the files with a clean copy.",
                Severity = Severity.Problem,
                FixLabel = "Re-import world-space UI",
                Fix = ImportWorldSpaceUi
            };
        }

        private static void AddTmpPackage()
        {
            string package = TmpPackageForThisEditor();
            Logcat.Info($"Adding {package} so AbxrLib's imported world-space UI can compile.");
            Client.Add(package);
        }

        /// <summary>
        /// Imports the world-space sample. The UI does not compile without TextMeshPro, so that is installed first
        /// when missing and the import resumes once it lands - installing a package reloads the domain, which is why
        /// the intent is parked in SessionState rather than held in a callback.
        /// </summary>
        internal static void ImportWorldSpaceUi()
        {
            if (SelfPackage() == null)
            {
                EditorUtility.DisplayDialog("AbxrLib Setup",
                    "AbxrLib is not installed as a package here, so its samples cannot be imported.\n\n" +
                    "WHAT TO DO: install AbxrLib through Window > Package Manager, then run this again.", "OK");
                return;
            }

            if (TmpAssemblyAvailable())
            {
                ImportWorldSpaceSample();
                return;
            }

            string tmpPackage = TmpPackageForThisEditor();
            SessionState.SetBool(PendingWorldSpaceImportKey, true);
            Logcat.Info($"AbxrLib's world-space UI needs TextMeshPro, which this project does not have. Adding " +
                        $"{tmpPackage}; the UI will be imported as soon as that finishes.");
            Client.Add(tmpPackage);
        }

        /// <summary>
        /// Finishes an import that was waiting on TextMeshPro. Called after each Editor load, because installing the
        /// package reloads the domain and discards whatever was mid-flight.
        /// </summary>
        internal static void ResumePendingWorldSpaceImport()
        {
            if (!SessionState.GetBool(PendingWorldSpaceImportKey, false)) return;

            // Still resolving: try again on the next load rather than importing UI that cannot compile yet.
            if (!TmpAssemblyAvailable()) return;

            SessionState.EraseBool(PendingWorldSpaceImportKey);
            ImportWorldSpaceSample();
        }

        private static void ImportWorldSpaceSample()
        {
            var self = SelfPackage();
            if (self == null) return;

            var sample = Sample.FindByPackage(self.name, self.version)
                .FirstOrDefault(s => s.displayName == WorldSpaceSampleName);

            if (sample.importPath == null)
            {
                EditorUtility.DisplayDialog("AbxrLib Setup",
                    $"Could not find the \"{WorldSpaceSampleName}\" sample in AbxrLib {self.version}.\n\n" +
                    "WHAT TO DO: open Window > Package Manager, select AbxrLib for Unity, and import it from the " +
                    "Samples section.", "OK");
                return;
            }

            // Overwrite a previous import so an upgrade lands in place; two copies would define the same types twice.
            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
            {
                EditorUtility.DisplayDialog("AbxrLib Setup",
                    "Unity could not import the world-space UI.\n\n" +
                    "WHAT TO DO: import it from Window > Package Manager > AbxrLib for Unity > Samples.", "OK");
                return;
            }

            AssetDatabase.Refresh();
            Logcat.Info($"Imported AbxrLib's world-space UI to {sample.importPath}. AbxrLib will now show its own " +
                        "keyboard and PIN pad when the backend asks the user to sign in.");
        }

        private static bool PackageIsRegistered(string packageName)
        {
            try { return PackageInfo.GetAllRegisteredPackages().Any(p => p.name == packageName); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// TextMeshPro ships with Unity but its fonts and shaders ("Essential Resources") are imported per project.
        /// Without them the PIN pad and keyboard render as blank quads at runtime, which is the most common
        /// "AbxrLib UI is invisible" report.
        /// </summary>
        private static Check CheckTextMeshPro()
        {
            // Nothing to say unless there is UI that needs the fonts and the fonts are missing: a core-only project
            // never draws with TextMeshPro, and a project whose resources are already imported does not need a row
            // confirming it.
            if (!WorldSpaceUiIsInstalled()) return null;
            if (TmpEssentialsImported()) return null;

            // Which package provides TextMeshPro depends on the Editor, so name the one this version actually uses -
            // otherwise the developer goes looking in Package Manager for something that is not there. The importer
            // menu path is the same either way (verified on 2021.3 and Unity 6).
            string provider = TmpIsPartOfUgui()
                ? "TextMeshPro is part of the com.unity.ugui package on this Editor version"
                : "TextMeshPro comes from the com.unity.textmeshpro package on this Editor version";

            return new Check
            {
                Title = "TextMeshPro essential resources missing",
                Detail = "AbxrLib's keyboard and PIN pad use TextMeshPro. Until its essential resources (fonts and " +
                         $"shaders) are imported into this project, that UI renders without text. {provider}; the " +
                         "resources themselves are imported per project either way.",
                Severity = Severity.Problem,
                FixLabel = "Import TMP Essentials",
                Fix = ImportTmpEssentials
            };
        }

        /// <summary>
        /// True when this project has the TMP essentials. Checked through the AssetDatabase rather than
        /// TMP_Settings.instance because reading that property is what triggers TMP's own import prompt, and two
        /// prompts for the same thing is worse than one.
        /// </summary>
        internal static bool TmpEssentialsImported()
        {
            // Type names as strings, not typeof: core no longer references the TextMeshPro assembly, because a
            // core-only project may not have TextMeshPro installed at all. An unknown type filter simply finds
            // nothing, which is the same answer as "not imported".
            bool hasSettings = AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;
            bool hasFont = AssetDatabase.FindAssets("t:TMP_FontAsset").Length > 0;
            return hasSettings && hasFont;
        }

        private static void ImportTmpEssentials()
        {
            // TMP's importer is a window, so this opens it rather than importing silently - the developer still
            // confirms. If the menu path is not there (TMP not installed at all), say so instead of failing quietly.
            if (EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources")) return;

            string install = TmpIsPartOfUgui()
                ? "add com.unity.ugui (TextMeshPro is part of it on this Editor version)"
                : "install TextMeshPro";
            EditorUtility.DisplayDialog("AbxrLib Setup",
                "Unity could not open the TextMeshPro importer.\n\n" +
                $"WHAT TO DO: {install} in Window > Package Manager, then use " +
                "Window > TextMeshPro > Import TMP Essential Resources.", "OK");
        }

        /// <summary>
        /// Reports each package AbxrLib's manifest depends on against the version this Editor resolved for it, so
        /// what the wizard shows is always what the project actually has rather than the numbers written in the
        /// manifest. A dependency that is not registered at all means resolution has not finished or has failed,
        /// which is a resolve problem rather than something to install by hand.
        /// </summary>
        private static Check CheckRequiredPackages()
        {
            var self = SelfPackage();
            if (self?.dependencies == null || self.dependencies.Length == 0) return CheckRequiredAssemblies();

            Dictionary<string, PackageInfo> registered;
            try
            {
                registered = PackageInfo.GetAllRegisteredPackages()
                    .GroupBy(p => p.name)
                    .ToDictionary(g => g.Key, g => g.First());
            }
            catch (Exception)
            {
                // No package registry to read (rare, and not worth guessing about) - fall back to what loaded.
                return CheckRequiredAssemblies();
            }

            if (registered.Count == 0) return CheckRequiredAssemblies();

            var missing = new List<string>();
            var older = new List<string>();
            var present = new List<string>();

            foreach (var dependency in self.dependencies)
            {
                if (!registered.TryGetValue(dependency.name, out var info))
                {
                    missing.Add($"{dependency.name} (needs {dependency.version})");
                    continue;
                }

                string name = string.IsNullOrEmpty(info.displayName) ? info.name : info.displayName;
                // Built-in packages ship with the Editor, so saying where it came from explains why the version can
                // differ from the manifest without anything being wrong.
                string source = info.source == PackageSource.BuiltIn ? " (built in)" : "";
                present.Add($"{name} {info.version}{source}");

                if (CompareVersions(info.version, dependency.version) < 0)
                    older.Add($"{name} {info.version} (AbxrLib needs {dependency.version})");
            }

            if (missing.Count > 0)
            {
                return new Check
                {
                    Title = "Some required packages are not registered",
                    Detail = "AbxrLib's manifest depends on: " + string.Join(", ", missing) + ". These install " +
                             "automatically with the package, so this usually means resolution has not finished or " +
                             "failed.",
                    Severity = Severity.Problem,
                    FixLabel = "Resolve packages",
                    Fix = () => Client.Resolve()
                };
            }

            if (older.Count > 0)
            {
                return new Check
                {
                    Title = "A required package is older than AbxrLib needs",
                    Detail = string.Join("; ", older) + ".\nWHAT TO DO: update it in Window > Package Manager. A " +
                             "version pinned in this project's manifest overrides what AbxrLib asks for.",
                    Severity = Severity.Warning,
                    FixLabel = "Resolve packages",
                    Fix = () => Client.Resolve()
                };
            }

            return new Check
            {
                Title = "Required packages installed",
                Detail = string.Join(", ", present),
                Severity = Severity.Ok
            };
        }

        /// <summary>
        /// Fallback when AbxrLib's manifest is unavailable - for instance a source copy under Assets/ rather than an
        /// installed package. Checks the assemblies its code needs instead of package versions it cannot know.
        /// </summary>
        private static Check CheckRequiredAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();
            var missing = new List<string>();
            // TextMeshPro is deliberately not checked: only the world-space UI needs it, and that carries its own
            // requirement.
            if (!loaded.Contains("Newtonsoft.Json")) missing.Add("Newtonsoft Json (com.unity.nuget.newtonsoft-json)");
            if (!loaded.Contains("Unity.InputSystem")) missing.Add("Input System (com.unity.inputsystem)");

            if (missing.Count == 0)
            {
                return new Check
                {
                    Title = "Required assemblies present",
                    Detail = "Newtonsoft Json and Input System are loaded.",
                    Severity = Severity.Ok
                };
            }

            return new Check
            {
                Title = "Required packages not loaded",
                Detail = "AbxrLib needs " + string.Join(" and ", missing) + ".\nWHAT TO DO: install the missing " +
                         "package in Window > Package Manager.",
                Severity = Severity.Problem,
                FixLabel = "Resolve packages",
                Fix = () => Client.Resolve()
            };
        }

        /// <summary>
        /// Numeric comparison of two package versions ("1.8.1" against "1.12.0" - 1.12 is newer). Pre-release
        /// suffixes are compared only as far as their leading digits, which is enough to tell "older than required"
        /// from "at or above".
        /// </summary>
        internal static int CompareVersions(string left, string right)
        {
            string[] a = (left ?? "").Split('.');
            string[] b = (right ?? "").Split('.');

            for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++)
            {
                int result = PartValue(a, i).CompareTo(PartValue(b, i));
                if (result != 0) return result;
            }

            return 0;

            int PartValue(string[] parts, int index)
            {
                if (index >= parts.Length) return 0;
                string digits = new string(parts[index].TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(digits, out int value) ? value : 0;
            }
        }

        /// <summary>
        /// Catches the one Advanced setting that quietly breaks authentication on an ArborXR-managed fleet.
        ///
        /// On a managed device the organization is not configured at all - it is resolved at runtime from the MDM,
        /// which AbxrLib does by building a dynamic org token out of the device's org id and fingerprint. That whole
        /// path starts with ArborMdmClient, so with it switched off there is no organization to authenticate against
        /// and the failure reads "Organization identification unavailable", pointing nowhere near this checkbox.
        ///
        /// Returns null - nothing to report - in the two cases where it is off for a good reason: a project not using
        /// ArborXR as its MDM at all, and a Production (Custom APK) build, which takes its organization from
        /// configuration by design and never consults the MDM for it.
        /// </summary>
        private static Check CheckArborMdmClient()
        {
            var config = Core.GetConfig();

            // Null while Unity is still settling; the wizard reports that separately.
            if (config == null) return null;
            if (config.enableArborMdmClient) return null;
            if (config.buildType == "production_custom") return null;

            return new Check
            {
                Title = "ArborMdmClient is turned off",
                Detail = "On ArborXR-managed devices the organization is supplied at runtime by the MDM, and that " +
                         "needs ArborMdmClient. With it off, builds on managed devices have no organization to " +
                         "authenticate against and fail with \"Organization identification unavailable\".\n" +
                         "Leave it off only if this project does not run on ArborXR-managed devices.",
                Severity = Severity.Warning,
                FixLabel = "Turn ArborMdmClient on",
                Fix = EnableArborMdmClient
            };
        }

        private static void EnableArborMdmClient()
        {
            var config = Core.GetConfig();
            if (config == null) return;

            config.enableArborMdmClient = true;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Logcat.Info("Turned ArborMdmClient on, so builds on ArborXR-managed devices can get their organization " +
                        "from the MDM at runtime.");
        }

        /// <summary>
        /// Reports when QR-code sign-in cannot work. QR is part of the world-space UI, so this says nothing at all in
        /// a core-only project, and nothing when QR is already available - only when the UI is installed and the
        /// headset support it needs is missing.
        ///
        /// Events, telemetry, logs, and PIN or email sign-in never depend on any of this.
        /// </summary>
        private static Check CheckHeadsetSdk()
        {
            if (!WorldSpaceUiIsInstalled()) return null;

            var names = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();
            bool hasMeta = names.Any(n => n.Contains("Oculus") || n.Contains("OVR"));
            bool hasPico = names.Any(n => n == "Unity.XR.PICO");
            bool hasOpenXr = names.Any(n => n.Contains("OpenXR"));

            BuildTargetGroup selected = EditorUserBuildSettings.selectedBuildTargetGroup;
            bool metaQr = BuildDefines.Has("META_QR_AVAILABLE", BuildTargetGroup.Android) ||
                          BuildDefines.Has("META_QR_AVAILABLE", selected);
            bool picoQr = BuildDefines.Has("PICO_SDK_3_4_OR_NEWER", BuildTargetGroup.Android) ||
                          BuildDefines.Has("PICO_SDK_3_4_OR_NEWER", selected);

            // QR is compiled in, so there is nothing to report.
            if (picoQr || metaQr) return null;

            if (hasPico || hasMeta || hasOpenXr)
            {
                string detected = hasPico ? "PICO" : hasMeta ? "Meta" : "OpenXR";
                return new Check
                {
                    Title = "QR-code sign-in is not enabled for this build target",
                    Detail = $"{detected} support is in the project, but the define that marks QR support ready is " +
                             "not set for the active build target. (On PICO it also compiles the scanner in; on " +
                             "Meta the scanner reaches the SDK by reflection and the define is only the readiness " +
                             "marker.) It is set automatically once the headset SDK is detected for that target - " +
                             "switching to Android usually does it. PIN and email sign-in work either way.",
                    Severity = Severity.Info
                };
            }

            return new Check
            {
                Title = "QR-code sign-in unavailable - no headset SDK",
                Detail = "The world-space UI is installed, but QR scanning needs the Meta or PICO SDK (or OpenXR) in " +
                         "the project. PIN and email sign-in work without it.",
                Severity = Severity.Info
            };
        }

        /// <summary>
        /// Android player settings a standalone headset build needs. Only reported when Android is the active build
        /// target, so desktop and WebGL projects are not told to change settings they do not use.
        /// </summary>
        private static Check CheckAndroidPlayerSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return new Check
                {
                    Title = "Build target is " + EditorUserBuildSettings.activeBuildTarget,
                    Detail = "Switch to Android in File > Build Settings when you build for a standalone headset " +
                             "(Quest, PICO). AbxrLib also runs in the Editor and in desktop builds.",
                    Severity = Severity.Info
                };
            }

            bool il2cpp = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP;
            bool arm64 = (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0;
            bool minSdk = PlayerSettings.Android.minSdkVersion >= AndroidSdkVersions.AndroidApiLevel29;

            var todo = new List<string>();
            if (!il2cpp) todo.Add("scripting backend is Mono (headsets need IL2CPP)");
            if (!arm64) todo.Add("ARM64 is not a target architecture");
            if (!minSdk) todo.Add($"minimum API level is {(int)PlayerSettings.Android.minSdkVersion} (headsets need 29 or higher)");

            if (todo.Count == 0)
            {
                return new Check
                {
                    Title = "Android player settings ready",
                    Detail = "IL2CPP, ARM64, and minimum API level 29 or higher.",
                    Severity = Severity.Ok
                };
            }

            return new Check
            {
                Title = "Android player settings need attention",
                Detail = "Standalone headset builds require IL2CPP, ARM64, and API level 29 or higher. In this " +
                         "project: " + string.Join("; ", todo) + ".",
                Severity = Severity.Warning,
                FixLabel = "Apply recommended settings",
                Fix = ApplyRecommendedAndroidSettings
            };
        }

        /// <summary>
        /// Applies the Android settings a headset build needs. ARM64 is added to whatever is already selected rather
        /// than replacing it, so a project that also ships ARMv7 keeps it.
        /// </summary>
        private static void ApplyRecommendedAndroidSettings()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

            Logcat.Info("Applied recommended Android player settings for AbxrLib: IL2CPP, ARM64, minimum API level 29.");
        }
    }
}

using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEngine;
// UnityEditor also has a legacy PackageInfo, so name the Package Manager one explicitly.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AbxrLib.Editor
{
    public sealed class SetupWizard : EditorWindow
    {
        private const string DocsUrl = "https://developers.arborxr.com/docs/insights/full-documentation/";
        private const string CredentialsDocsUrl = "https://developers.arborxr.com/docs/insights/full-documentation/#get-your-credentials";
        private const string OrgTokenDocsUrl = "https://developers.arborxr.com/docs/insights/full-documentation/#org-token";
        private const string QuickStartDocsUrl = "https://developers.arborxr.com/docs/insights/quickstart/";

        private const string QuickStartSnippet =
            "// Start the Assessment, should trigger LMS to show an Assessment in progress:\n" +
            "Abxr.EventAssessmentStart(\"safety_training\");\n" +
            "\n" +
            "// Report events to be assessed by LMS:\n" +
            "Abxr.Event(\"valve_opened\", new Dictionary<string, string> { [\"attempt\"] = \"2\" });\n" +
            "\n" +
            "// Complete the Assessment, should trigger LMS to show final assessment report.\n" +
            "Abxr.EventAssessmentComplete(\"safety_training\", 92, EventStatus.Pass);";

        private enum Step { Welcome = 0, Credentials = 1, Project = 2, FirstEvents = 3 }

        private static readonly string[] StepTitles = { "Welcome", "Credentials", "Project Setup", "First Events" };

        private Step _step = Step.Welcome;
        private bool _openedAutomatically;
        private AppConfig _config;
        private List<SetupWizardChecks.Check> _checks;
        private bool _worldSpaceInstalled;
        private bool _worldSpaceFilesImported;
        private System.DateTime _checkedAt;
        // Keeps the Organization Token field up while this window is open, once it has held a value.
        private readonly SetupWizardChecks.OrgTokenFieldLatch _orgTokenField = new SetupWizardChecks.OrgTokenFieldLatch();
        // Keeps the Use App Tokens checkbox up while this window is open, so a legacy project that ticks it can untick it.
        private readonly SetupWizardChecks.ShownLatch _useAppTokensToggle = new SetupWizardChecks.ShownLatch();
        // Keeps Production (Custom APK) in the Build Type choice while this window is open, so switching away can be undone.
        private readonly SetupWizardChecks.ShownLatch _customApkOption = new SetupWizardChecks.ShownLatch();
        private Vector2 _scroll;
        private Styles _styles;

        [MenuItem("Analytics for XR/Setup Wizard", priority = 0)]
        public static void Open() => Open(false);

        /// <summary>
        /// Shows the wizard. <paramref name="openedAutomatically"/> is only used to pick the opening line, so a
        /// developer who just installed the package is told why a window appeared on its own.
        /// </summary>
        internal static void Open(bool openedAutomatically)
        {
            // Every open counts as "shown for this version", the menu-item ones included - otherwise a developer
            // who opens the wizard by hand while the post-install auto-open is still waiting for the Editor to go
            // idle gets the window they just finished with reopened on top of them.
            SetupWizardLauncher.MarkWizardShown();

            var window = GetWindow<SetupWizard>(false, "AbxrLib Setup", true);
            window.minSize = new Vector2(640f, 560f);
            window._step = Step.Welcome;
            window._openedAutomatically = openedAutomatically;
            window._orgTokenField.Reset();
            window._useAppTokensToggle.Reset();
            window._customApkOption.Reset();
            window.Refresh();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("AbxrLib Setup");
            _orgTokenField.Reset();
            _useAppTokensToggle.Reset();
            _customApkOption.Reset();
            Refresh();
        }

        private void OnFocus() => Refresh();

        /// <summary>Re-reads the configuration and the project checks. Cheap, but not per-frame cheap.</summary>
        private void Refresh()
        {
            // Returns null while Unity is still compiling or importing; the UI handles that rather than caching null
            // as "broken".
            _config = Core.GetConfig();
            _checks = SetupWizardChecks.Run();
            _worldSpaceInstalled = SetupWizardChecks.WorldSpaceUiIsInstalled();
            _worldSpaceFilesImported = SetupWizardChecks.WorldSpaceUiFilesImported();
            _checkedAt = System.DateTime.Now;
            Repaint();
        }

        private void OnGUI()
        {
            _styles ??= new Styles();

            DrawHeader();

            EditorGUILayout.BeginHorizontal();
            DrawStepList();
            DrawStepContent();
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Chrome
        // ---------------------------------------------------------------------------------------------------------

        private Texture2D _headerIcon;
        private bool _headerIconLookedUp;

        /// <summary>
        /// The header icon lives in the package's Editor folder rather than a Resources folder: Resources content
        /// ships in every consumer's build, and the sample's copy of this art only exists once the sample is
        /// imported - while the wizard's primary audience is the core-only project that never imports it.
        /// </summary>
        private Texture2D HeaderIcon()
        {
            if (_headerIconLookedUp) return _headerIcon;
            _headerIconLookedUp = true;

            // Wrapped like SetupWizardChecks.SelfPackage wraps the same query: a throw here would land mid-layout
            // inside OnGUI, and a missing icon is not worth that.
            try
            {
                var self = PackageInfo.FindForAssembly(typeof(SetupWizard).Assembly);
                if (self != null)
                    _headerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        $"{self.assetPath}/Editor/Images/ArborXR_Org_Icon.png");
            }
            catch (System.Exception)
            {
                _headerIcon = null;
            }

            return _headerIcon;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var icon = HeaderIcon();
            if (icon) GUILayout.Label(icon, GUILayout.Width(38f), GUILayout.Height(38f));

            EditorGUILayout.BeginVertical();
            GUILayout.Space(2f);
            EditorGUILayout.LabelField("AbxrLib for Unity", _styles.Title);
            EditorGUILayout.LabelField(
                $"AbxrLib {AbxrLibVersion.Version}  ·  Unity {Application.unityVersion}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStepList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150f));
            GUILayout.Space(6f);

            for (int i = 0; i < StepTitles.Length; i++)
            {
                var step = (Step)i;
                bool current = step == _step;
                string mark = IsStepComplete(step) ? "✓" : current ? "▸" : "•";
                var style = current ? _styles.StepSelected : _styles.Step;

                if (GUILayout.Button($"  {mark}  {StepTitles[i]}", style)) GoTo(step);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawStepContent()
        {
            EditorGUILayout.BeginVertical();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(4f);

            if (_config == null && _step != Step.FirstEvents)
            {
                DrawConfigUnavailable();
            }
            else
            {
                switch (_step)
                {
                    case Step.Welcome: DrawWelcome(); break;
                    case Step.Credentials: DrawCredentials(); break;
                    case Step.Project: DrawProject(); break;
                    case Step.FirstEvents: DrawFirstEvents(); break;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            GUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            bool autoOpen = EditorGUILayout.ToggleLeft(new GUIContent("Open automatically after install or update",
                    "When off, the wizard only opens from Analytics for XR > Setup Wizard."),
                SetupWizardLauncher.AutoOpenEnabled, GUILayout.Width(300f));
            if (autoOpen != SetupWizardLauncher.AutoOpenEnabled) SetupWizardLauncher.AutoOpenEnabled = autoOpen;

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_step == Step.Welcome))
            {
                if (GUILayout.Button("Back", GUILayout.Width(80f)))
                {
                    GoTo((Step)((int)_step - 1));
                    Refresh();
                }
            }

            if (_step == Step.FirstEvents)
            {
                if (GUILayout.Button("Done", GUILayout.Width(90f)))
                {
                    SaveConfig();
                    Close();
                }
            }
            else if (GUILayout.Button("Next", GUILayout.Width(90f)))
            {
                GoTo((Step)((int)_step + 1));
                Refresh();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Steps
        // ---------------------------------------------------------------------------------------------------------

        private void DrawWelcome()
        {
            EditorGUILayout.LabelField(_openedAutomatically
                    ? "AbxrLib is installed. This wizard covers everything needed before your first event arrives."
                    : "This wizard covers everything needed before your first event arrives.",
                _styles.Body);

            DrawWorldSpaceCallout();

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("What this sets up", EditorStyles.boldLabel);
            DrawBullet("Credentials: The App Token that identifies your app to ArborXR Insights.");
            DrawBullet("Project setup: Resources, packages, and Android settings a headset build needs.");
            DrawBullet("First events: The assessment calls that turn on grading dashboards and LMS reporting.");

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Already done for you", EditorStyles.boldLabel);
            DrawBullet("No scene setup: AbxrLib starts itself before the first scene loads.");
            DrawBullet("Android permissions and headset SDK defines are handled at build time.");

            GUILayout.Space(10f);
            DrawStatusSummary();

            GUILayout.Space(10f);
            if (GUILayout.Button("Open documentation", GUILayout.Width(180f))) Application.OpenURL(DocsUrl);
        }

        private void DrawCredentials()
        {
            EditorGUILayout.LabelField("Credentials", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Everything modified here is stored in Assets/Resources/AbxrLib.asset, the same asset " +
                "Analytics for XR > Configuration edits.", _styles.Body);

            GUILayout.Space(10f);
            string problem = SetupWizardChecks.DescribeCredentialProblem(_config);
            if (problem == null)
                EditorGUILayout.HelpBox("Credentials look valid. AbxrLib will authenticate on launch.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(problem, MessageType.Warning);

            GUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();

            // An unrecognized stored value shows as Production, matching how the runtime normalizes it, and the
            // value is only written back when the popup actually changes - drawing this page must never edit the
            // configuration on its own.
            int current = SetupWizardChecks.BuildTypeIndex(_config.buildType);
            // Production (Custom APK) is offered only to a project already saved on it (SDK-90).
            bool offersCustomApk = _customApkOption.Holds(SetupWizardChecks.UsesCustomApk(_config));
            string buildTypeTooltip =
                "Production: shared builds, including ArborXR-managed fleets; each device's org comes from the " +
                "device at runtime.\n" +
                "Development: for your own test builds.";
            // Only a project that is offered Production (Custom APK) is told about it (SDK-90).
            if (offersCustomApk)
                buildTypeTooltip += "\nProduction (Custom APK): one customer per build; the Organization Token is " +
                                    "required and the MDM is not consulted for it.";
            int selected = EditorGUILayout.Popup(new GUIContent("Build Type", buildTypeTooltip),
                current, SetupWizardChecks.BuildTypeLabels(offersCustomApk));
            if (selected != current) _config.buildType = SetupWizardChecks.BuildTypeAtIndex(selected);

            EditorGUILayout.LabelField(selected switch
            {
                0 => "Shared builds, and the right choice for an ArborXR-managed fleet: each device reports to its own " +
                     "organization at runtime.",
                1 => "Your own test builds. They report to the organization that owns the app; a managed device " +
                     "reports to its own organization. Passcode pairing never shows in Development: use Production " +
                     "to test it.",
                _ => "One customer per build: every device running this build reports to the organization set here, " +
                     "and the MDM is not consulted for it. Not for an ArborXR-managed fleet - use Production there."
            }, _styles.Hint);

            GUILayout.Space(8f);
            // Only a project already on the legacy scheme is offered the choice (SDK-89). New projects use App Tokens.
            if (_useAppTokensToggle.Holds(SetupWizardChecks.ShowsLegacyScheme(_config)))
            {
                _config.useAppTokens = EditorGUILayout.ToggleLeft(new GUIContent("Use App Tokens (recommended)",
                        "On: authenticate with App Token / Organization Token. Off: legacy App ID, Org ID, and Auth Secret."),
                    _config.useAppTokens);
                GUILayout.Space(4f);
            }

            if (_config.useAppTokens)
            {
                _config.appToken = DrawPasteField("App Token (required)",
                    "The JWT that identifies your app and publisher.", _config.appToken);

                // Only a Custom APK build needs an Organization Token (SDK-87): Production takes the organization from
                // the device, and Development uses the App Token. A token the project already has saved always shows.
                if (_orgTokenField.Shows(_config))
                {
                    bool isCustomApk = _config.buildType == "production_custom";
                    _config.orgToken = DrawPasteField(
                        isCustomApk ? "Organization Token (required)" : "Organization Token",
                        isCustomApk ? "The customer's organization JWT." : "An Organization Token saved in this project.",
                        _config.orgToken);

                    EditorGUILayout.LabelField(isCustomApk
                            ? "A specific customer's Organization Token is not self-serve in the portal. Contact ArborXR."
                            : SetupWizardChecks.OrgTokenFieldHint(_config, out _) ?? "",
                        _styles.Hint);
                }

                // Values left from the legacy scheme have no field here, and they still ship inside the build (SDK-89).
                string leftoverLegacy = SetupWizardChecks.LeftoverLegacyValuesWarning(_config);
                if (leftoverLegacy != null)
                {
                    EditorGUILayout.HelpBox(leftoverLegacy, MessageType.Warning);
                    if (GUILayout.Button("Clear legacy values", GUILayout.Width(160f)))
                    {
                        Undo.RecordObject(_config, "Clear legacy AbxrLib values");
                        SetupWizardChecks.ClearLeftoverLegacyValues(_config);
                        EditorUtility.SetDirty(_config);
                    }
                }
            }
            else
            {
                _config.appID = DrawPasteField("Application ID (required)",
                    "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", _config.appID);
                using (new EditorGUI.DisabledScope(_config.buildType == "production"))
                {
                    _config.orgID = DrawPasteField("Organization ID", "Only for custom APKs.", _config.orgID);
                    _config.authSecret = DrawPasteField("Authorization Secret", "Only for custom APKs.", _config.authSecret);
                }

                EditorGUILayout.HelpBox(SetupWizardChecks.LegacySchemeNote, MessageType.Warning);
            }

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);

            GUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Where to find your App Token", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "1. Log in to the ArborXR portal.\n2. Open Content Library.\n3. Choose your app from the Managed Apps list.\n" +
                "4. Open its Insights Hub tab. The App Token is managed on that page.", _styles.Body);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Never ship an Organization Token or Authorization Secret in a publicly distributed APK.\n\nA Development " +
                "build uses the App Token as the Organization Token, so it is for internal or single-org builds only.\n\nFor shared builds use " +
                "Production and let ArborXR-managed devices supply the organization.", MessageType.None);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get your credentials (docs)", GUILayout.Width(200f)))
                Application.OpenURL(CredentialsDocsUrl);
            if (GUILayout.Button("Org token rules (docs)", GUILayout.Width(180f)))
                Application.OpenURL(OrgTokenDocsUrl);
            

            if (GUILayout.Button("Open full Configuration", GUILayout.Width(200f)))
            {
                SaveConfig();
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }
            EditorGUILayout.EndHorizontal();            
        }

        private void DrawProject()
        {
            EditorGUILayout.LabelField("Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Requirements for AbxrLib to run, and for its default world space sign-in UI to be usable, if installed.", _styles.Body);
            GUILayout.Space(6f);

            _checks ??= SetupWizardChecks.Run();

            foreach (var check in _checks) DrawCheck(check);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-check", GUILayout.Width(120f))) Refresh();
            // The rows also refresh whenever the window gets focus, so without the time a click changes nothing visible.
            EditorGUILayout.LabelField($"Last checked {_checkedAt:T}. The checks also run when this window gets focus.", _styles.Hint);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFirstEvents()
        {
            EditorGUILayout.LabelField("First Events", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Assessment events are what activate and engage with grading dashboards and LMS reporting.\n\nCall them from your own " +
                "scripts - there is nothing to add to the scene.", _styles.Body);

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Add calls like these where your training starts and ends:", _styles.Body);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.SelectableLabel(QuickStartSnippet, _styles.Code, GUILayout.Height(140f));
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy snippet", GUILayout.Width(140f)))
            {
                EditorGUIUtility.systemCopyBuffer = QuickStartSnippet;
                ShowNotification(new GUIContent("Copied"));
            }
            if (GUILayout.Button("Quick start (docs)", GUILayout.Width(150f))) Application.OpenURL(QuickStartDocsUrl);
            if (GUILayout.Button("Full documentation", GUILayout.Width(160f))) Application.OpenURL(DocsUrl);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Validation Checklist:", EditorStyles.boldLabel);
            DrawBullet("Enter Play Mode: AbxrLib authenticates and logs its progress to the Console.");
            DrawBullet("Sign-in UI (PIN, email, QR) appears on its own when the backend asks for it.");
            DrawBullet("Events queue locally and send in batches, so a moment can pass before they show up in Insights.");
            DrawBullet("The pairing prompt appears only when your code calls Abxr.StartPairing().");

            GUILayout.Space(10f);
            DrawStatusSummary();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Pieces
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Shows where the wizard stands, so the last step is not just a wall of prose.</summary>
        private void DrawStatusSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            bool credentialsOk = SetupWizardChecks.CredentialsAreValid(_config);
            DrawStatusLine("Credentials", credentialsOk,
                credentialsOk ? "Valid" : "Not set up yet", Step.Credentials);

            _checks ??= SetupWizardChecks.Run();
            int problems = 0;
            foreach (var check in _checks)
                if (check.Severity == SetupWizardChecks.Severity.Problem) problems++;

            DrawStatusLine("Project setup", problems == 0,
                problems == 0 ? "Nothing blocking" : $"{problems} item{(problems == 1 ? "" : "s")} to fix", Step.Project);

            DrawStatusLine("Sign-in UI", _worldSpaceInstalled,
                _worldSpaceInstalled ? "Installed"
                    : _worldSpaceFilesImported ? "Imported, not compiling"
                    : "Not installed (optional)", Step.Project);

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy diagnostics", GUILayout.Width(140f)))
            {
                SetupDiagnostics.CopyToClipboard();
                ShowNotification(new GUIContent("Copied"));
            }
            // Assign only on change: the setter writes EditorPrefs, and OnGUI runs this several times per frame.
            bool includeAll = SetupDiagnostics.IncludeAllConfig;
            bool toggled = EditorGUILayout.ToggleLeft("All config values", includeAll, GUILayout.Width(150f));
            if (toggled != includeAll) SetupDiagnostics.IncludeAllConfig = toggled;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Paste into a support request. Tokens and secrets are never included.", _styles.Body);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Says on the first page that AbxrLib's sign-in UI is a separate import, because the case that needs telling
        /// is an upgrade: the project already works, the package quietly stopped carrying the keyboard and PIN pad,
        /// and nothing else would surface that until a user is standing in front of a sign-in prompt that never
        /// appears. A configured project is warned, since it can sign in and would reach that prompt; an empty one
        /// is just informed. Valid credentials don't prove an upgrade (a new project has them as soon as its App
        /// Token is pasted), so the text never says the project came from an earlier AbxrLib.
        /// </summary>
        private void DrawWorldSpaceCallout()
        {
            if (_worldSpaceInstalled) return;

            // Imported but not compiling is a different problem with a different fix; Project Setup covers it.
            if (_worldSpaceFilesImported) return;

            bool configured = SetupWizardChecks.CredentialsAreValid(_config);

            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var previous = GUI.color;
            GUI.color = configured ? _styles.WarnColor : _styles.InfoColor;
            EditorGUILayout.LabelField(configured
                ? "! Sign-in UI is not installed in this project"
                : "i Sign-in UI is a separate import", EditorStyles.boldLabel);
            GUI.color = previous;

            EditorGUILayout.LabelField(configured
                    ? "The world-space keyboard, PIN pad, and QR scanning are a separate import. Before 3.0 they " +
                      "were part of the package, so a project updated from an earlier AbxrLib has to import them " +
                      "again. Keeping them separate keeps TextMeshPro, uGUI, and XR Interaction Toolkit out of " +
                      "projects that do not draw AbxrLib's UI.\nWithout it, an authentication request that needs a PIN or email has " +
                      "nothing to show unless your app handles Abxr.OnInputRequested itself."
                    : "AbxrLib can draw its own sign-in UI - world-space keyboard, PIN pad, exit polls, and QR " +
                      "scanning - or your app can collect input itself through Abxr.OnInputRequested. Import it if " +
                      "you want the built-in UI.",
                _styles.Body);

            GUILayout.Space(4f);
            if (GUILayout.Button("Import world-space UI", GUILayout.Width(190f)))
            {
                SetupWizardChecks.ImportWorldSpaceUi();
                Refresh();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusLine(string label, bool ok, string value, Step goTo)
        {
            EditorGUILayout.BeginHorizontal();
            var color = GUI.color;
            GUI.color = ok ? _styles.OkColor : _styles.WarnColor;
            GUILayout.Label(ok ? "✓" : "!", GUILayout.Width(14f));
            GUI.color = color;
            EditorGUILayout.LabelField(label, GUILayout.Width(110f));
            EditorGUILayout.LabelField(value);
            if (!ok && GUILayout.Button("Fix", EditorStyles.miniButton, GUILayout.Width(40f))) GoTo(goTo);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCheck(SetupWizardChecks.Check check)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var previous = GUI.color;
            GUI.color = check.Severity switch
            {
                SetupWizardChecks.Severity.Ok => _styles.OkColor,
                SetupWizardChecks.Severity.Warning => _styles.WarnColor,
                SetupWizardChecks.Severity.Problem => _styles.ProblemColor,
                _ => _styles.InfoColor
            };
            GUILayout.Label(check.Severity switch
            {
                SetupWizardChecks.Severity.Ok => "✓",
                SetupWizardChecks.Severity.Warning => "!",
                SetupWizardChecks.Severity.Problem => "✕",
                _ => "i"
            }, GUILayout.Width(16f));
            GUI.color = previous;

            EditorGUILayout.LabelField(check.Title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(check.Detail, _styles.Body);

            if (check.Fix != null && !string.IsNullOrEmpty(check.FixLabel))
            {
                GUILayout.Space(2f);
                if (GUILayout.Button(check.FixLabel, GUILayout.Width(200f)))
                {
                    check.Fix();
                    // The fix changes what the checks see, so drop the cached list rather than showing a stale row.
                    Refresh();
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2f);
        }

        /// <summary>
        /// A text field with a Paste button. Tokens are long enough that pasting is the only realistic way to enter
        /// them, and a copied token often carries a trailing newline, so whatever arrives is trimmed.
        /// </summary>
        private string DrawPasteField(string label, string tooltip, string value)
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip));
            EditorGUILayout.BeginHorizontal();
            string result = EditorGUILayout.TextField(value ?? "");
            if (GUILayout.Button(new GUIContent("Paste", "Paste from the clipboard"), GUILayout.Width(52f)))
            {
                result = EditorGUIUtility.systemCopyBuffer ?? "";
                // Without dropping focus the field keeps drawing the string the user was editing, not the pasted one.
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            return result.Trim();
        }

        private void DrawBullet(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("•", GUILayout.Width(12f));
            EditorGUILayout.LabelField(text, _styles.Body);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigUnavailable()
        {
            EditorGUILayout.HelpBox(
                "AbxrLib cannot read its configuration right now. This is normal while Unity is compiling or " +
                "importing.\n\nWHAT TO DO: wait for Unity to finish, then choose Retry. If it keeps failing, check the " +
                "Console - a configuration that exists but cannot be loaded is reported there.", MessageType.Warning);

            if (GUILayout.Button("Retry", GUILayout.Width(100f))) Refresh();
        }

        private bool IsStepComplete(Step step) => step switch
        {
            Step.Welcome => true,
            Step.Credentials => SetupWizardChecks.CredentialsAreValid(_config),
            Step.Project => _checks != null && !_checks.Exists(c => c.Severity == SetupWizardChecks.Severity.Problem),
            _ => false
        };

        /// <summary>
        /// Every page change saves, however the page was left (Next, Back, the step list, a Fix jump), so the asset on
        /// disk never trails what the wizard shows.
        /// </summary>
        private void GoTo(Step step)
        {
            SaveConfig();
            _step = step;
        }

        /// <summary>Closing saves too: a token pasted on the Credentials page is otherwise only in memory.</summary>
        private void OnDestroy()
        {
            if (_config != null && EditorUtility.IsDirty(_config)) SaveConfig();
        }

        private void SaveConfig()
        {
            if (_config == null) return;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Styles are created lazily because a GUIStyle can only be built inside a GUI call.</summary>
        private sealed class Styles
        {
            public readonly GUIStyle Title;
            public readonly GUIStyle Body;
            public readonly GUIStyle Hint;
            public readonly GUIStyle Code;
            public readonly GUIStyle Step;
            public readonly GUIStyle StepSelected;

            public readonly Color OkColor = new Color(0.30f, 0.72f, 0.40f);
            public readonly Color WarnColor = new Color(0.90f, 0.68f, 0.20f);
            public readonly Color ProblemColor = new Color(0.87f, 0.35f, 0.32f);
            public readonly Color InfoColor = new Color(0.55f, 0.62f, 0.75f);

            public Styles()
            {
                Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
                Body = new GUIStyle(EditorStyles.wordWrappedLabel);
                Hint = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
                Code = new GUIStyle(EditorStyles.textArea) { font = EditorStyles.miniFont, wordWrap = false };
                Step = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fixedHeight = 24f };
                StepSelected = new GUIStyle(Step) { fontStyle = FontStyle.Bold };
            }
        }
    }
}

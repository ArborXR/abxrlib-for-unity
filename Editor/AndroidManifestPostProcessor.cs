using System.IO;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Callbacks;
using UnityEngine;

namespace AbxrLib.Editor
{
    /// <summary>
    /// Post-processes the Android manifest to add AbxrLib version metadata.
    /// This allows the manifest to indicate that AbxrLib is being used and which version.
    /// </summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string MetaDataVersionName = "com.arborxr.abxrlib.version";
        private const string FallbackVersion = "1.0.0";

        /// <summary>
        /// Gets the AbxrLib version, or falls back to 1.0.0 if not found.
        /// </summary>
        private static string GetVersion()
        {
            try
            {
                if (!string.IsNullOrEmpty(AbxrLibVersion.Version))
                {
                    return AbxrLibVersion.Version;
                }
            }
            catch
            {
                // If AbxrLibVersion is not accessible, use fallback
            }
            
            return FallbackVersion;
        }

        /// <summary>
        /// Called after Unity generates the Gradle Android project.
        /// This is the correct place to modify the AndroidManifest.xml for Gradle builds.
        /// </summary>
        /// <param name="path">Path to the generated Gradle project</param>
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            ProcessManifest(path);
        }

        /// <summary>
        /// Fallback PostProcessBuild for direct APK builds (non-Gradle).
        /// </summary>
        /// <param name="target">The build target platform</param>
        /// <param name="pathToBuiltProject">Path to the built project</param>
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.Android)
            {
                return;
            }

            // Only process if not already handled by IPostGenerateGradleAndroidProject
            ProcessManifest(pathToBuiltProject);
        }

        /// <summary>
        /// Processes the AndroidManifest.xml to inject AbxrLib metadata.
        /// </summary>
        private static void ProcessManifest(string projectPath)
        {
            string manifestPath = GetManifestPath(projectPath);
            if (string.IsNullOrEmpty(manifestPath))
            {
                Debug.LogWarning($"AbxrLib: AndroidManifest.xml not found. Searched in: {projectPath}");
                return;
            }

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"AbxrLib: AndroidManifest.xml not found at: {manifestPath}");
                return;
            }

            try
            {
                string manifestContent = File.ReadAllText(manifestPath);
                string modifiedContent = InjectMetadata(manifestContent);
                
                if (modifiedContent != manifestContent)
                {
                    File.WriteAllText(manifestPath, modifiedContent);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"AbxrLib: Failed to inject metadata into AndroidManifest.xml: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Gets the path to the AndroidManifest.xml file.
        /// Unity places the merged manifest in different locations depending on build type.
        /// </summary>
        private static string GetManifestPath(string projectPath)
        {
            // For Gradle builds (most common), the manifest is in src/main/AndroidManifest.xml
            string gradleManifestPath = Path.Combine(projectPath, "src", "main", "AndroidManifest.xml");
            if (File.Exists(gradleManifestPath))
            {
                return gradleManifestPath;
            }

            // Get the project root directory (parent of Assets folder)
            string unityProjectPath = Application.dataPath;
            if (unityProjectPath.EndsWith("/Assets") || unityProjectPath.EndsWith("\\Assets"))
            {
                unityProjectPath = Directory.GetParent(unityProjectPath).FullName;
            }
            else
            {
                unityProjectPath = Directory.GetParent(unityProjectPath)?.FullName ?? "";
            }

            // Unity stores the merged manifest in Temp/StagingArea during builds
            // Try multiple possible locations
            string[] possiblePaths = {
                // Standard Unity temp location for merged manifest (most common for direct APK builds)
                Path.Combine(unityProjectPath, "Temp", "StagingArea", "src", "main", "AndroidManifest.xml"),
                // Alternative temp location
                Path.Combine(unityProjectPath, "Temp", "StagingArea", "AndroidManifest.xml"),
                // Direct in project path
                Path.Combine(projectPath, "AndroidManifest.xml"),
                // For some Unity versions/build types
                Path.Combine(Path.GetDirectoryName(projectPath), "AndroidManifest.xml"),
                // System temp cache (less common)
                Path.Combine(Application.temporaryCachePath, "StagingArea", "src", "main", "AndroidManifest.xml"),
                Path.Combine(Application.temporaryCachePath, "StagingArea", "AndroidManifest.xml")
            };

            foreach (string path in possiblePaths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }

            // Log all paths we tried for debugging
            Debug.LogWarning($"AbxrLib: Manifest not found. Searched paths:\n{string.Join("\n", possiblePaths)}");
            return null;
        }

        /// <summary>
        /// Injects AbxrLib version metadata into the manifest content.
        /// </summary>
        private static string InjectMetadata(string manifestContent)
        {
            // Remove old enabled flag if it exists (from previous versions)
            string enabledFlagPattern = @"<meta-data\s+android:name=""com\.arborxr\.abxrlib\.enabled""\s+android:value=""[^""]*""\s*/>\s*";
            manifestContent = Regex.Replace(manifestContent, enabledFlagPattern, "", RegexOptions.IgnoreCase);

            // Check if version metadata already exists
            if (manifestContent.Contains(MetaDataVersionName))
            {
                // Version exists, check if it needs updating
                string versionPattern = $@"<meta-data\s+android:name=""{Regex.Escape(MetaDataVersionName)}""\s+android:value=""([^""]*)""\s*/>";
                Match versionMatch = Regex.Match(manifestContent, versionPattern, RegexOptions.IgnoreCase);
                
                if (versionMatch.Success && versionMatch.Groups.Count > 1)
                {
                    string currentVersion = versionMatch.Groups[1].Value;
                    string targetVersion = GetVersion();
                    if (currentVersion != targetVersion)
                    {
                        // Update version
                        manifestContent = Regex.Replace(manifestContent, versionPattern, 
                            $@"<meta-data android:name=""{MetaDataVersionName}"" android:value=""{targetVersion}"" />", 
                            RegexOptions.IgnoreCase);
                    }
                }
                
                return manifestContent;
            }

            // Version doesn't exist, add it
            return AddVersionMetadata(manifestContent);
        }

        /// <summary>
        /// Adds the version metadata if it doesn't exist.
        /// </summary>
        private static string AddVersionMetadata(string manifestContent)
        {
            string version = GetVersion();
            string versionMetadata = $@"        <meta-data android:name=""{MetaDataVersionName}"" android:value=""{version}"" />";
            return InsertMetadataAfterApplicationTag(manifestContent, versionMetadata);
        }

        /// <summary>
        /// Inserts metadata after the <application> tag.
        /// </summary>
        private static string InsertMetadataAfterApplicationTag(string manifestContent, string metadata)
        {
            string applicationPattern = @"(<application[\s\S]*?>)";
            Match match = Regex.Match(manifestContent, applicationPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                int insertPosition = match.Index + match.Length;
                int newlineAfter = manifestContent.IndexOf('\n', insertPosition);
                if (newlineAfter > 0)
                {
                    insertPosition = newlineAfter + 1;
                }
                manifestContent = manifestContent.Insert(insertPosition, metadata + "\n");
                return manifestContent;
            }

            // Fallback: Try to find the closing </application> tag
            string applicationClosePattern = @"(</application>)";
            match = Regex.Match(manifestContent, applicationClosePattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                manifestContent = manifestContent.Insert(match.Index, metadata + "\n        ");
                return manifestContent;
            }

            Debug.LogWarning("AbxrLib: Could not find <application> tag. Metadata not added.");
            return manifestContent;
        }

    }
}


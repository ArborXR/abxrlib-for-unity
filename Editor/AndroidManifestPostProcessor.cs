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
    /// Post-processes the Android manifest to add AbxrLib version and app_id metadata.
    /// This allows the manifest to indicate that AbxrLib is being used, which version, and which app_id.
    /// </summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string MetaDataVersionName = "com.arborxr.abxrlib.version";
        private const string MetaDataAppIdName = "com.arborxr.abxrlib.insights_id";
        private const string MetaDataBuildTypeName = "com.arborxr.abxrlib.build_type";

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
        /// Gets the app_id from Configuration, or returns null if not found.
        /// </summary>
        private static string GetAppId()
        {
            try
            {
                var config = Configuration.Instance;
                if (config != null && !string.IsNullOrEmpty(config.appID))
                {
                    return config.appID;
                }
            }
            catch
            {
                // If Configuration is not accessible, return null
            }
            
            return null;
        }

        /// <summary>
        /// Gets the build type based on Configuration values.
        /// Returns "production" if orgID and authSecret are both empty, "custom" otherwise.
        /// </summary>
        private static string GetBuildType()
        {
            try
            {
                var config = Configuration.Instance;
                if (config != null)
                {
                    bool orgIdEmpty = string.IsNullOrEmpty(config.orgID);
                    bool authSecretEmpty = string.IsNullOrEmpty(config.authSecret);
                    
                    // Production when both are empty, custom otherwise
                    if (orgIdEmpty && authSecretEmpty)
                    {
                        return "production";
                    }
                    else
                    {
                        return "custom";
                    }
                }
            }
            catch
            {
                // If Configuration is not accessible, default to production
            }
            
            return "production";
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
            // For direct APK builds, the manifest is already packaged and not accessible as a file.
            // This is expected behavior, so we skip silently.
            if (!string.IsNullOrEmpty(projectPath) && projectPath.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string manifestPath = GetManifestPath(projectPath);
            if (string.IsNullOrEmpty(manifestPath))
            {
                // Only log warning for Gradle project builds where we expect to find the manifest
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

            // Only log warning if this is a Gradle project build (not an APK build)
            // For APK builds, the manifest is already packaged and not accessible as a file
            if (!projectPath.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"AbxrLib: Manifest not found. Searched paths:\n{string.Join("\n", possiblePaths)}");
            }
            return null;
        }

        /// <summary>
        /// Injects AbxrLib version, app_id, and build_type metadata into the manifest content.
        /// </summary>
        private static string InjectMetadata(string manifestContent)
        {
            // Remove old enabled flag if it exists (from previous versions)
            string enabledFlagPattern = @"<meta-data\s+android:name=""com\.arborxr\.abxrlib\.enabled""\s+android:value=""[^""]*""\s*/>\s*";
            manifestContent = Regex.Replace(manifestContent, enabledFlagPattern, "", RegexOptions.IgnoreCase);

            // Handle version metadata
            manifestContent = InjectOrUpdateMetadata(manifestContent, MetaDataVersionName, GetVersion());

            // Handle app_id metadata (only if app_id is available)
            string appId = GetAppId();
            if (!string.IsNullOrEmpty(appId))
            {
                manifestContent = InjectOrUpdateMetadata(manifestContent, MetaDataAppIdName, appId);
            }

            // Handle build_type metadata
            manifestContent = InjectOrUpdateMetadata(manifestContent, MetaDataBuildTypeName, GetBuildType());

            return manifestContent;
        }

        /// <summary>
        /// Injects or updates a metadata entry in the manifest.
        /// </summary>
        /// <param name="manifestContent">The manifest content</param>
        /// <param name="metadataName">The metadata name</param>
        /// <param name="metadataValue">The metadata value</param>
        /// <returns>The modified manifest content</returns>
        private static string InjectOrUpdateMetadata(string manifestContent, string metadataName, string metadataValue)
        {
            if (string.IsNullOrEmpty(metadataValue))
            {
                return manifestContent;
            }

            // Check if metadata already exists
            if (manifestContent.Contains(metadataName))
            {
                // Metadata exists, check if it needs updating
                string metadataPattern = $@"<meta-data\s+android:name=""{Regex.Escape(metadataName)}""\s+android:value=""([^""]*)""\s*/>";
                Match metadataMatch = Regex.Match(manifestContent, metadataPattern, RegexOptions.IgnoreCase);
                
                if (metadataMatch.Success && metadataMatch.Groups.Count > 1)
                {
                    string currentValue = metadataMatch.Groups[1].Value;
                    if (currentValue != metadataValue)
                    {
                        // Update metadata value
                        manifestContent = Regex.Replace(manifestContent, metadataPattern, 
                            $@"<meta-data android:name=""{metadataName}"" android:value=""{metadataValue}"" />", 
                            RegexOptions.IgnoreCase);
                    }
                }
            }
            else
            {
                // Metadata doesn't exist, add it
                string metadata = $@"        <meta-data android:name=""{metadataName}"" android:value=""{metadataValue}"" />";
                manifestContent = InsertMetadataAfterApplicationTag(manifestContent, metadata);
            }

            return manifestContent;
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


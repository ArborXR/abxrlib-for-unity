using System.IO;
using System.Text.RegularExpressions;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
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
        private const string MetaDataBuildFingerprintName = "com.arborxr.abxrlib.build_fingerprint";

        private const string FallbackVersion = "1.0.0";

        // Cached config data to avoid multiple extractions
        private static Utils.AuthConfigData? _cachedConfigData = null;

        /// <summary>
        /// Gets the cached config data, extracting it if not already cached.
        /// </summary>
        private static Utils.AuthConfigData GetCachedConfigData()
        {
            if (_cachedConfigData == null)
            {
                try
                {
                    var config = Configuration.Instance;
                    _cachedConfigData = Utils.ExtractConfigData(config);
                }
                catch
                {
                    // If Configuration is not accessible, return invalid result
                    _cachedConfigData = new Utils.AuthConfigData { isValid = false };
                }
            }
            return _cachedConfigData.Value;
        }

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
        
        public int callbackOrder => 1;

        /// <summary>
        /// Gets the app_id from Configuration, or extracts it from App Token if using app tokens.
        /// Returns null if not found.
        /// </summary>
        private static string GetAppId()
        {
            var configData = GetCachedConfigData();
            return configData.isValid ? configData.appId : null;
        }

        /// <summary>
        /// Gets the build type from Configuration, or extracts it from App Token if using app tokens.
        /// Returns the buildType field value, or "production" as default. Production (Custom APK) returns "production" so manifest matches API.
        /// </summary>
        private static string GetBuildType()
        {
            var configData = GetCachedConfigData();
            if (!configData.isValid) return "production";
            return configData.buildType == "production_custom" ? "production" : configData.buildType;
        }

        /// <summary>
        /// Validates app/org tokens or legacy orgID/authSecret for Production (Custom APK). Fails the build when set but invalid or incomplete.
        /// </summary>
        private static void ValidateAppTokensForBuild()
        {
            var configData = GetCachedConfigData();
            if (configData.useAppTokens)
            {
                if (!string.IsNullOrEmpty(configData.appToken) && !LooksLikeJwt(configData.appToken))
                    throw new BuildFailedException("AbxrLib: App Token is set but does not look like a JWT (expected three dot-separated segments). Fix or clear the App Token in Analytics for XR configuration.");
                if (configData.buildType == "production_custom")
                {
                    if (!string.IsNullOrEmpty(configData.appToken))
                    {
                        if (string.IsNullOrEmpty(configData.orgToken))
                            throw new BuildFailedException("AbxrLib: Production (Custom APK) requires Organization Token to be set for Custom APK builds. Set the customer's org token in Analytics for XR configuration.");
                        if (!LooksLikeJwt(configData.orgToken))
                            throw new BuildFailedException("AbxrLib: Organization Token is set but does not look like a JWT (expected three dot-separated segments). Fix the Organization Token in Analytics for XR configuration.");
                    }
                }
                return;
            }
            if (configData.buildType == "production_custom" && !string.IsNullOrEmpty(configData.appId))
            {
                if (string.IsNullOrEmpty(configData.orgId))
                    throw new BuildFailedException("AbxrLib: Production (Custom APK) requires Organization ID to be set for Custom APK builds. Set the customer's org ID in Analytics for XR configuration.");
                if (string.IsNullOrEmpty(configData.authSecret) || string.IsNullOrWhiteSpace(configData.authSecret))
                    throw new BuildFailedException("AbxrLib: Production (Custom APK) requires Authorization Secret to be set for Custom APK builds. Set it in Analytics for XR configuration.");
                if (!LooksLikeUuid(configData.orgId))
                    throw new BuildFailedException("AbxrLib: Organization ID does not look like a valid UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx). Fix the Organization ID in Analytics for XR configuration.");
            }
        }

        private static bool LooksLikeUuid(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return Regex.IsMatch(value, "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$");
        }

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        /// <summary>
        /// Generates a unique GUID for each build.
        /// This build_fingerprint can be used to track and compare results across different builds.
        /// </summary>
        private static string GenerateBuildFingerprint()
        {
            return System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Called after Unity generates the Gradle Android project.
        /// This is the correct place to modify the AndroidManifest.xml for Gradle builds.
        /// </summary>
        /// <param name="path">Path to the generated Gradle project</param>
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
            // Reset cached config data to ensure fresh extraction for each manifest processing
            _cachedConfigData = null;
            
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
                Configuration.PreferValidationWarnings = true;
                try
                {
                    ValidateAppTokensForBuild();
                    string manifestContent = File.ReadAllText(manifestPath);
                    string modifiedContent = InjectMetadata(manifestContent);
                    
                    if (modifiedContent != manifestContent)
                    {
                        File.WriteAllText(manifestPath, modifiedContent);
                    }
                }
                finally
                {
                    Configuration.PreferValidationWarnings = false;
                }
            }
            catch (BuildFailedException)
            {
                throw;
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
            
            // Add camera permission for Meta Quest QR code reading
            manifestContent = AddCameraPermission(manifestContent);
            
            // Add headset camera permission for Meta Quest Passthrough Camera API
            manifestContent = AddHeadsetCameraPermission(manifestContent);
            
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

            // Handle build_fingerprint metadata (generated GUID for each build)
            manifestContent = InjectOrUpdateMetadata(manifestContent, MetaDataBuildFingerprintName, GenerateBuildFingerprint());

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

        /// <summary>
        /// Adds camera permission to the manifest if it doesn't already exist.
        /// Required for Meta Quest QR code reading functionality.
        /// </summary>
        private static string AddCameraPermission(string manifestContent)
        {
            // Check if camera permission already exists
            if (manifestContent.Contains("android.permission.CAMERA"))
            {
                return manifestContent;
            }

            // Find the <manifest> tag and add permission after it
            string manifestPattern = @"(<manifest[\s\S]*?>)";
            Match match = Regex.Match(manifestContent, manifestPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                int insertPosition = match.Index + match.Length;
                int newlineAfter = manifestContent.IndexOf('\n', insertPosition);
                if (newlineAfter > 0)
                {
                    insertPosition = newlineAfter + 1;
                }
                
                // Add camera permission
                string cameraPermission = "    <uses-permission android:name=\"android.permission.CAMERA\" />\n";
                manifestContent = manifestContent.Insert(insertPosition, cameraPermission);
                return manifestContent;
            }

            Debug.LogWarning("AbxrLib: Could not find <manifest> tag. Camera permission not added.");
            return manifestContent;
        }

        /// <summary>
        /// Adds the headset camera permission for Meta Quest Passthrough Camera API.
        /// </summary>
        private static string AddHeadsetCameraPermission(string manifestContent)
        {
            // Check if headset camera permission already exists
            if (manifestContent.Contains("horizonos.permission.HEADSET_CAMERA"))
            {
                return manifestContent;
            }

            // Find the <manifest> tag and add permission after it
            string manifestPattern = @"(<manifest[\s\S]*?>)";
            Match match = Regex.Match(manifestContent, manifestPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                int insertPosition = match.Index + match.Length;
                int newlineAfter = manifestContent.IndexOf('\n', insertPosition);
                if (newlineAfter > 0)
                {
                    insertPosition = newlineAfter + 1;
                }
                
                // Add headset camera permission
                string headsetCameraPermission = "    <uses-permission android:name=\"horizonos.permission.HEADSET_CAMERA\" />\n";
                manifestContent = manifestContent.Insert(insertPosition, headsetCameraPermission);
                Debug.Log("AbxrLib: Added horizonos.permission.HEADSET_CAMERA permission to AndroidManifest.xml");
                return manifestContent;
            }

            Debug.LogWarning("AbxrLib: Could not find <manifest> tag. Headset camera permission not added.");
            return manifestContent;
        }
    }
}


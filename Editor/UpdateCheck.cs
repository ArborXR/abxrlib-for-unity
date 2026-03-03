using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AbxrLib.Editor
{
    public class UpdateCheck
    {
        private const string PackageUrl = "https://github.com/ArborXR/abxrlib-for-unity.git";
        private const string VersionUrl = "https://api.github.com/repos/ArborXR/abxrlib-for-unity/releases/latest";
        private const string PackageName = "com.arborxr.unity";
        private const string SkippedVersionKey = "SkippedPackageVersion";
        private const int UpdateCheckFrequencyDays = 1;
        public const string UpdateCheckPref = "Abxr_updateCheck";

        [MenuItem("Analytics for XR/Check For Updates")]
        public static async void CheckForUpdatesMenu()
        {
            EditorPrefs.DeleteKey(SkippedVersionKey);
            EditorPrefs.DeleteKey(UpdateCheckPref);
            await CheckForUpdates(true);
        }

        public static async Task CheckForUpdates(bool forced = false)
        {
            string currentVersion = await CheckPackageVersion();
        
            if (string.IsNullOrEmpty(currentVersion))
            {
                Debug.LogError("[AbxrLib] Failed to get current package version.");
                return;
            }

            string latestVersion = CheckLatestVersion();
            if (string.IsNullOrEmpty(latestVersion)) return;
            bool updateAvailable = currentVersion != latestVersion;
        
            if (!updateAvailable)
            {
                if (forced)
                {
                    EditorUtility.DisplayDialog("Up-to-date", "You have the latest version of AbxrLib.", "OK");
                }
            }
            else
            {
                ShowUpdateDialog(currentVersion, latestVersion);
            }
        
            EditorPrefs.SetString(UpdateCheckPref, DateTime.UtcNow.AddDays(UpdateCheckFrequencyDays).ToString("G"));
        }

        private static void ShowUpdateDialog(string currentVersion, string latestVersion)
        {
            int option = EditorUtility.DisplayDialogComplex("AbxrLib Update Available",
                $"Version {latestVersion} is available. Your current version is {currentVersion}. Would you like to update?",
                "Update", "Cancel", "Skip This Version");

            if (option == 0) // Update
            {
                UpdatePackage();
            }
            else if (option == 2) // Skip this version
            {
                EditorPrefs.SetString(SkippedVersionKey, currentVersion);
            }
        }

        private static void UpdatePackage()
        {
            AddRequest addRequest = Client.Add(PackageUrl);
            EditorApplication.update += ProgressUpdate;

            void ProgressUpdate()
            {
                if (addRequest.IsCompleted)
                {
                    if (addRequest.Status == StatusCode.Success)
                        Debug.Log("[AbxrLib] Package updated successfully");
                    else if (addRequest.Status >= StatusCode.Failure)
                        Debug.LogError($"[AbxrLib] Package update failed: {addRequest.Error.message}");

                    EditorApplication.update -= ProgressUpdate;
                }
            }
        }

        private static async Task<string> CheckPackageVersion()
        {
            ListRequest listRequest = Client.List(); // List all packages installed in the project

            while (!listRequest.IsCompleted)
            {
                await Task.Delay(100); // Wait for the request to complete
            }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == PackageName)
                    {
                        return package.version;
                    }
                }
            
                Debug.LogWarning($"[AbxrLib] Package {PackageName} not found.");
            }
            else if (listRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[AbxrLib] {listRequest.Error.message}");
            }

            return "";
        }

        private static string CheckLatestVersion()
        {
            try
            {
                using var client = new HttpClient();
            
                // Set User-Agent header required by GitHub API
                client.DefaultRequestHeaders.Add("User-Agent", "Unity App");
            
                HttpResponseMessage response = client.GetAsync(VersionUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    var releaseInfo = JsonUtility.FromJson<GitHubRelease>(jsonResponse);
                    Debug.Log("[AbxrLib] Latest release version: " + releaseInfo.tag_name);
                    return releaseInfo.tag_name.Replace("v", "");
                }

                Debug.LogError("[AbxrLib] Error: " + response.ReasonPhrase);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AbxrLib] Exception occurred: " + ex.Message);
            }

            return "";
        }

        [Serializable]
        public class GitHubRelease
        {
            public string tag_name; // Represents the version tag, e.g., "v1.0.0"
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbxrLib.Editor
{
    /// <summary>
    /// Adds this package to the project's Test Runner "testables" so AbxrLib unit tests appear in the Test Runner window.
    /// Updates the consuming project's Packages/manifest.json.
    /// </summary>
    public static class TestRunnerTestablesHelper
    {
        public const string PackageName = "com.arborxr.unity";

        /// <summary>
        /// Adds <see cref="PackageName"/> to the project manifest's testables array if not already present.
        /// </summary>
        /// <param name="message">Result message for the user.</param>
        /// <returns>True if the manifest was updated or already contained the package; false on error.</returns>
        public static bool EnsurePackageInTestables(out string message)
        {
            message = null;
            string manifestPath = GetManifestPath();
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                message = "Could not find project Packages/manifest.json.";
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception ex)
            {
                message = $"Could not read manifest: {ex.Message}";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                message = $"Invalid manifest JSON: {ex.Message}";
                return false;
            }

            var testables = root["testables"] as JArray;
            if (testables == null)
            {
                testables = new JArray();
                root["testables"] = testables;
            }

            foreach (var token in testables)
            {
                if (token.Type == JTokenType.String && string.Equals(token.Value<string>(), PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    message = $"\"{PackageName}\" is already in Test Runner testables. Open Window > General > Test Runner to run AbxrLib tests.";
                    return true;
                }
            }

            testables.Add(PackageName);

            try
            {
                File.WriteAllText(manifestPath, root.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                message = $"Could not write manifest: {ex.Message}";
                return false;
            }

            message = $"Added \"{PackageName}\" to Test Runner testables. Open Window > General > Test Runner to run AbxrLib tests. The Package Manager may refresh.";
            return true;
        }

        /// <summary>
        /// Returns whether the project manifest already lists this package in testables.
        /// </summary>
        public static bool IsPackageInTestables()
        {
            string manifestPath = GetManifestPath();
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                return false;

            try
            {
                var root = JObject.Parse(File.ReadAllText(manifestPath));
                var testables = root["testables"] as JArray;
                if (testables == null) return false;
                foreach (var token in testables)
                {
                    if (token.Type == JTokenType.String && string.Equals(token.Value<string>(), PackageName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // ignore parse errors
            }

            return false;
        }

        private static string GetManifestPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return projectRoot == null ? null : Path.Combine(projectRoot, "Packages", "manifest.json");
        }
    }
}

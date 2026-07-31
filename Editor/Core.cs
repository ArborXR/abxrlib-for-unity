using System;
using System.IO;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    internal class Core
    {
        private static AppConfig _config;
        private const string NEW_CONFIG_NAME = "AbxrLib";
        private const string OLD_CONFIG_NAME = "ArborXR";
    
        static Core()
        {
            // Stub function nicase we need it at some point.
        }
    
        /// <summary>
        /// Gets the configuration, creating a new default configuration only when none exists yet.
        /// Returns null when a configuration file exists but cannot be loaded as <see cref="AppConfig"/>, so a
        /// broken asset is reported instead of silently overwritten.
        /// </summary>
        public static AppConfig GetConfig()
        {
            if (_config) return _config;
        
            // First try to load the new config name using Resources.Load
            _config = Resources.Load<AppConfig>(NEW_CONFIG_NAME);
            if (_config) return _config;
        
            // If Resources.Load failed, try direct AssetDatabase load as fallback
            // This prevents false negatives during Unity startup/compilation
            const string newConfigPath = "Assets/Resources/" + NEW_CONFIG_NAME + ".asset";
            _config = AssetDatabase.LoadAssetAtPath<AppConfig>(newConfigPath);
            if (_config) 
            {
                Logcat.Debug($"Loaded existing config via AssetDatabase fallback - {newConfigPath}");
                return _config;
            }
        
            // If new config doesn't exist, try the old config name
            _config = Resources.Load<AppConfig>(OLD_CONFIG_NAME);
            if (_config)
            {
                // If old config exists but new one doesn't, migrate it
                MigrateConfigToNewName();
                return _config;
            }
        
            // Try old config via AssetDatabase as well
            const string oldConfigPath = "Assets/Resources/" + OLD_CONFIG_NAME + ".asset";
            _config = AssetDatabase.LoadAssetAtPath<AppConfig>(oldConfigPath);
            if (_config)
            {
                // If old config exists but new one doesn't, migrate it
                MigrateConfigToNewName();
                return _config;
            }
        
            // The loads above are all typed, and a typed load returns null while the file is still on disk
            // whenever the AppConfig type cannot be resolved: a compile error in AbxrLib.Runtime, a domain
            // reload before assemblies are ready, an unresolvable m_Script reference, or two copies of AbxrLib
            // in the project. AssetDatabase.CreateAsset replaces whatever already occupies the path, so creating
            // over an unloadable asset would silently destroy the configured identity. Move it aside instead:
            // the values stay recoverable from the quarantined copy and a usable default can still be created.
            bool newPathClear = QuarantineUnloadableConfig(newConfigPath);
            QuarantineUnloadableConfig(oldConfigPath);

            // If the unloadable asset could not be moved, do not create over it - that is the destructive case.
            if (!newPathClear) return null;

            // Only create a new config when no configuration file occupies the path.
            Logcat.Debug("No existing configuration found, creating new default configuration");
            _config = ScriptableObject.CreateInstance<AppConfig>();
            const string filepath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(filepath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateAsset(_config, filepath + "/" + NEW_CONFIG_NAME + ".asset");
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _config;
        }

        /// <summary>
        /// True when a file exists at the given project-relative path, independent of whether its type resolves.
        /// Used to tell "no configuration yet" apart from "configuration present but unloadable".
        /// </summary>
        private static bool AssetFileExists(string projectRelativePath) =>
            !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(projectRelativePath));

        /// <summary>
        /// Moves a configuration asset that exists but cannot be loaded as <see cref="AppConfig"/> out of the way so
        /// a fresh default can be created without overwriting it. The original file is kept alongside with a
        /// ".broken" suffix: it still holds the configured app and organization identity, which is not recoverable
        /// from anywhere else, and those values can be copied out of it by hand.
        /// Returns true when the path is clear afterwards (nothing was there, or the file was moved successfully),
        /// and false when a file is still in the way - in which case callers must not write over it.
        /// </summary>
        private static bool QuarantineUnloadableConfig(string projectRelativePath)
        {
            if (!AssetFileExists(projectRelativePath)) return true;

            try
            {
                // Never clobber an earlier quarantined copy - each failure keeps its own.
                string quarantinePath = projectRelativePath + ".broken";
                for (int i = 1; File.Exists(quarantinePath); i++)
                    quarantinePath = $"{projectRelativePath}.broken{i}";

                File.Move(projectRelativePath, quarantinePath);

                // Drop the stale .meta so Unity does not keep the old (unresolvable) import settings.
                string metaPath = projectRelativePath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);

                AssetDatabase.Refresh();

                Logcat.Warning($"{projectRelativePath} exists but could not be loaded as {nameof(AppConfig)}, so it was " +
                               $"moved to {quarantinePath} and a new default configuration was created. The original " +
                               "still contains the configured app and organization identity - open it in a text editor " +
                               "to copy those values across. This usually means the asset's m_Script reference does not " +
                               "resolve (see Runtime/Core/AppConfig.cs), the AbxrLib.Runtime assembly failed to compile, " +
                               "or the project contains more than one copy of AbxrLib.");
                return true;
            }
            catch (Exception e)
            {
                // Leave the file untouched on failure: losing the identity values is worse than not creating a default.
                Logcat.Error($"{projectRelativePath} could not be loaded as {nameof(AppConfig)} and moving it aside " +
                             $"failed ({e.GetType().Name}: {e.Message}). It has been left in place - resolve the load " +
                             "failure before continuing, and do not delete the asset.");
                return false;
            }
        }

        private static void MigrateConfigToNewName()
        {
            const string filepath = "Assets/Resources";
            string oldPath = filepath + "/" + OLD_CONFIG_NAME + ".asset";
            string newPath = filepath + "/" + NEW_CONFIG_NAME + ".asset";
        
            // Check if old config exists AND new config doesn't exist
            if (AssetDatabase.LoadAssetAtPath<AppConfig>(oldPath) && 
                !AssetDatabase.LoadAssetAtPath<AppConfig>(newPath))
            {
                // Rename the asset
                AssetDatabase.RenameAsset(oldPath, NEW_CONFIG_NAME);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            
                Logcat.Debug($"ArborXR configuration has been migrated to {NEW_CONFIG_NAME}");
            }
            else if (AssetDatabase.LoadAssetAtPath<AppConfig>(oldPath))
            {
                Logcat.Warning($"Migration skipped - {NEW_CONFIG_NAME} already exists alongside {OLD_CONFIG_NAME}");
            }
        }
    }
}
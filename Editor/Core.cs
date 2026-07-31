using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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
        
            // A typed load also returns null for a perfectly healthy asset while the Editor is still settling -
            // mid-compile or mid-import, the type or the imported asset simply is not available yet. Treating that
            // as "broken" would quarantine a working configuration, so do nothing and let the next call decide once
            // the Editor is idle. _config is still null here, so nothing is cached and the retry re-runs the loads.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Logcat.Debug("AbxrLib skipped loading its configuration because Unity is still compiling or importing. " +
                             "WHAT TO DO: wait for Unity to finish, then try again.");
                return null;
            }

            // The loads above are all typed, and a typed load returns null while the file is still on disk whenever
            // the AppConfig type cannot be resolved: an unresolvable m_Script reference, or two copies of AbxrLib in
            // the project. AssetDatabase.CreateAsset replaces whatever already occupies the path, so creating over an
            // unloadable asset would silently destroy the configured identity. Move it aside instead: the values stay
            // recoverable from the quarantined copy and a usable default can still be created.
            bool newPathClear = QuarantineUnloadableConfig(newConfigPath, out string quarantinedPath);
            QuarantineUnloadableConfig(oldConfigPath, out string quarantinedOldPath);
            quarantinedPath ??= quarantinedOldPath;

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

            // The quarantined file is still readable text even though Unity could not bind it to a type, so the
            // settings can be carried over automatically instead of asking the developer to retype them.
            if (quarantinedPath != null) RestoreSettingsFromQuarantinedConfig(quarantinedPath, _config);

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
        /// from anywhere else. <paramref name="quarantinePath"/> receives that location so the caller can copy the
        /// settings into the replacement asset.
        /// Returns true when the path is clear afterwards (nothing was there, or the file was moved successfully),
        /// and false when a file is still in the way - in which case callers must not write over it.
        /// </summary>
        private static bool QuarantineUnloadableConfig(string projectRelativePath, out string quarantinePath)
        {
            quarantinePath = null;
            if (!AssetFileExists(projectRelativePath)) return true;

            try
            {
                // Never clobber an earlier quarantined copy - each failure keeps its own.
                string target = projectRelativePath + ".broken";
                for (int i = 1; File.Exists(target); i++)
                    target = $"{projectRelativePath}.broken{i}";

                File.Move(projectRelativePath, target);

                // Drop the stale .meta so Unity does not keep the old (unresolvable) import settings.
                string metaPath = projectRelativePath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);

                AssetDatabase.Refresh();
                quarantinePath = target;
                return true;
            }
            catch (Exception e)
            {
                // Leave the file untouched on failure: losing the identity values is worse than not creating a default.
                Logcat.Error($"AbxrLib could not read your configuration and could not rename it either " +
                             $"({e.GetType().Name}: {e.Message}). Nothing was changed.\n" +
                             $"WHAT TO DO: quit Unity, move {projectRelativePath} out of the project (keep it - your " +
                             "settings are in it), then reopen Unity and use Analytics for XR > Configuration to make " +
                             "a new one.");
                return false;
            }
        }

        /// <summary>
        /// Copies the settings out of a quarantined configuration file into <paramref name="target"/>.
        /// Unity could not bind the file to a type, but the file itself is still readable, so the values can be
        /// recovered rather than retyped. Only simple values (text, numbers, checkboxes) can be read back this way -
        /// prefab references point at other assets and are left at their defaults.
        /// </summary>
        private static void RestoreSettingsFromQuarantinedConfig(string quarantinePath, AppConfig target)
        {
            if (target == null) return;

            Dictionary<string, string> saved;
            try
            {
                saved = ReadTopLevelValues(quarantinePath);
            }
            catch (Exception e)
            {
                Logcat.Warning($"AbxrLib created a new configuration but could not read the old one to copy your " +
                               $"settings across ({e.GetType().Name}: {e.Message}).\n" +
                               $"WHAT TO DO: open Analytics for XR > Configuration and enter your appID / appToken / " +
                               $"orgToken again. The old values are still in {quarantinePath}.");
                return;
            }

            int restored = 0;
            foreach (FieldInfo field in typeof(AppConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!saved.TryGetValue(field.Name, out string raw)) continue;
                if (!TryParseSerializedValue(raw, field.FieldType, out object value)) continue;

                field.SetValue(target, value);
                restored++;
            }

            if (restored == 0)
            {
                // Nothing parsed: the project stores assets in binary, or the file is not a configuration at all.
                Logcat.Warning($"AbxrLib created a new configuration but could not copy your old settings across " +
                               $"automatically.\n" +
                               $"WHAT TO DO: open {quarantinePath} in a text editor, copy your appID / appToken / " +
                               "orgToken values, then paste them into Analytics for XR > Configuration.\n" +
                               "Your settings are not lost - they are still in that file. Do not delete it until you " +
                               "have copied them across.");
                return;
            }

            Logcat.Warning($"AbxrLib could not read your configuration, so it created a new one and copied your " +
                           $"{restored} saved settings into it. The old file is kept as {quarantinePath}.\n" +
                           "WHAT TO DO: open Analytics for XR > Configuration and check your appID / appToken / " +
                           "orgToken are correct. Keyboard and PIN prefab slots are not copied, so re-assign those if " +
                           "you had set them.");
        }

        /// <summary>
        /// Reads the "name: value" pairs at the top level of a serialized asset (two spaces of indentation, which is
        /// where Unity writes a ScriptableObject's own fields). Nested lines are skipped so only the object's own
        /// values are returned. An asset saved in binary yields nothing, which callers treat as "cannot recover".
        /// </summary>
        private static Dictionary<string, string> ReadTopLevelValues(string path)
        {
            var values = new Dictionary<string, string>();

            foreach (string line in File.ReadAllLines(path))
            {
                // Exactly two spaces of indentation: deeper lines belong to nested values, not the object's own fields.
                if (!line.StartsWith("  ") || line.StartsWith("   ")) continue;

                int separator = line.IndexOf(':');
                if (separator <= 2) continue;

                string name = line.Substring(2, separator - 2).Trim();
                if (name.Length == 0 || values.ContainsKey(name)) continue;

                values[name] = line.Substring(separator + 1).Trim();
            }

            return values;
        }

        /// <summary>
        /// Converts one serialized value into <paramref name="type"/>. Returns false for anything that cannot be
        /// recovered from text - notably references to other assets, which are written as ids rather than values.
        /// </summary>
        private static bool TryParseSerializedValue(string raw, Type type, out object value)
        {
            value = null;
            raw = raw.Trim();

            // Unity only quotes strings when it has to; strip the quotes back off if present.
            if (raw.Length >= 2)
            {
                char first = raw[0], last = raw[raw.Length - 1];
                if ((first == '\'' && last == '\'') || (first == '"' && last == '"'))
                    raw = raw.Substring(1, raw.Length - 2);
            }

            if (type == typeof(string))
            {
                value = raw;
                return true;
            }

            if (raw.Length == 0) return false;

            if (type == typeof(bool))
            {
                // Unity writes checkboxes as 1 and 0.
                value = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            if (type == typeof(int) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = i;
                return true;
            }

            if (type == typeof(long) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                value = l;
                return true;
            }

            if (type == typeof(float) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                value = f;
                return true;
            }

            return false;
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
                Logcat.Warning($"You have two AbxrLib configuration files: {NEW_CONFIG_NAME}.asset (the one AbxrLib " +
                               $"uses) and the older {OLD_CONFIG_NAME}.asset (ignored).\n" +
                               $"WHAT TO DO: check {NEW_CONFIG_NAME}.asset has your appID / appToken / orgToken, then " +
                               $"delete {OLD_CONFIG_NAME}.asset.");
            }
        }
    }
}
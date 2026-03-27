using System;
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
        /// Gets the configuration or a new default configuration
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
        
            // Only create new config if file genuinely doesn't exist
            Logcat.Debug("No existing configuration found, creating new default configuration");
            _config = ScriptableObject.CreateInstance<AppConfig>();
            const string filepath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(filepath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
        
            AssetDatabase.CreateAsset(_config, filepath + "/" + NEW_CONFIG_NAME + ".asset");
            EditorUtility.SetDirty(GetConfig());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _config;
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
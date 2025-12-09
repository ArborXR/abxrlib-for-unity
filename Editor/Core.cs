using System;
using AbxrLib.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace AbxrLib.Editor
{
    [InitializeOnLoad]
    internal class Core
    {
        private static Configuration _config;
        private const string NEW_CONFIG_NAME = "AbxrLib";
        private const string OLD_CONFIG_NAME = "ArborXR";
    
        static Core()
        {
            string nextUpdateCheck = EditorPrefs.GetString(UpdateCheck.UpdateCheckPref, DateTime.UtcNow.ToString("G"));
            var parsedDate = DateTime.ParseExact(nextUpdateCheck, "G", null);
            if (parsedDate < DateTime.UtcNow)
            {
                _ = UpdateCheck.CheckForUpdates();
            }
        }
    
        /// <summary>
        /// Gets the configuration or a new default configuration
        /// </summary>
        public static Configuration GetConfig()
        {
            if (_config) return _config;
        
            // First try to load the new config name using Resources.Load
            _config = Resources.Load<Configuration>(NEW_CONFIG_NAME);
            if (_config) return _config;
        
            // If Resources.Load failed, try direct AssetDatabase load as fallback
            // This prevents false negatives during Unity startup/compilation
            const string newConfigPath = "Assets/Resources/" + NEW_CONFIG_NAME + ".asset";
            _config = AssetDatabase.LoadAssetAtPath<Configuration>(newConfigPath);
            if (_config) 
            {
                Debug.Log($"AbxrLib: Loaded existing config via AssetDatabase fallback - {newConfigPath}");
                return _config;
            }
        
            // If new config doesn't exist, try the old config name
            _config = Resources.Load<Configuration>(OLD_CONFIG_NAME);
            if (_config)
            {
                // If old config exists but new one doesn't, migrate it
                MigrateConfigToNewName();
                return _config;
            }
        
            // Try old config via AssetDatabase as well
            const string oldConfigPath = "Assets/Resources/" + OLD_CONFIG_NAME + ".asset";
            _config = AssetDatabase.LoadAssetAtPath<Configuration>(oldConfigPath);
            if (_config)
            {
                // If old config exists but new one doesn't, migrate it
                MigrateConfigToNewName();
                return _config;
            }
        
            // Only create new config if file genuinely doesn't exist
            Debug.Log("AbxrLib: No existing configuration found, creating new default configuration");
            _config = ScriptableObject.CreateInstance<Configuration>();
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
            if (AssetDatabase.LoadAssetAtPath<Configuration>(oldPath) && 
                !AssetDatabase.LoadAssetAtPath<Configuration>(newPath))
            {
                // Rename the asset
                AssetDatabase.RenameAsset(oldPath, NEW_CONFIG_NAME);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            
                Debug.Log($"AbxrLib: ArborXR configuration has been migrated to {NEW_CONFIG_NAME}");
            }
            else if (AssetDatabase.LoadAssetAtPath<Configuration>(oldPath))
            {
                Debug.LogWarning($"AbxrLib: Migration skipped - {NEW_CONFIG_NAME} already exists alongside {OLD_CONFIG_NAME}");
            }
        }
    }
}
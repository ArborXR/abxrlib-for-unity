using System;
using Abxr.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace Abxr.Editor
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
        
            // First try to load the new config name
            _config = Resources.Load<Configuration>(NEW_CONFIG_NAME);
            if (_config) return _config;
        
            // If new config doesn't exist, try the old config name
            _config = Resources.Load<Configuration>(OLD_CONFIG_NAME);
            if (_config)
            {
                // If old config exists but new one doesn't, migrate it
                MigrateConfigToNewName();
                return _config;
            }
        
            // If neither exists, create new config with new name
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
        
            if (AssetDatabase.LoadAssetAtPath<Configuration>(oldPath))
            {
                // Rename the asset
                AssetDatabase.RenameAsset(oldPath, NEW_CONFIG_NAME);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            
                Debug.Log($"ArborXR configuration has been migrated to {NEW_CONFIG_NAME}");
            }
        }
    }
}
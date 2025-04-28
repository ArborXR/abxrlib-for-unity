using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal class Core
{
    private static Configuration _config;
    
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
        if (_config != null) return _config;
        
        _config = Resources.Load<Configuration>("ArborXR");
        if (_config != null) return _config;
        
        _config = ScriptableObject.CreateInstance<Configuration>();
        const string filepath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(filepath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        
        AssetDatabase.CreateAsset(_config, filepath + "/ArborXR.asset");
        EditorUtility.SetDirty(GetConfig());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return _config;
    }
}
#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Android;

public class EnableDesugaring : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // Unity 6 module
        string module = Path.Combine(path, "app", "build.gradle");
        // Older Unity fallback
        if (!File.Exists(module))
            module = Path.Combine(path, "launcher", "build.gradle");

        if (!File.Exists(module)) return;

        string text = File.ReadAllText(module, Encoding.UTF8);
        bool changed = false;

        // 1) Ensure compileOptions block has coreLibraryDesugaringEnabled true
        if (!text.Contains("coreLibraryDesugaringEnabled true"))
        {
            text = Regex.Replace(
                text,
                @"android\s*\{",
                m => m.Value + @"
    compileOptions {
        // Unity 6 (AGP 8) is Java 17 by default. Use VERSION_1_8 if you prefer.
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
        coreLibraryDesugaringEnabled true
    }",
                RegexOptions.Multiline
            );
            changed = true;
        }

        // 2) Add desugaring dependency ONLY in this module
        if (!Regex.IsMatch(text, @"coreLibraryDesugaring\s+'com\.android\.tools:desugar_jdk_libs"))
        {
            text = Regex.Replace(
                text,
                @"dependencies\s*\{",
                "dependencies {\n    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.0.4'",
                RegexOptions.Multiline
            );
            changed = true;
        }

        if (changed)
            File.WriteAllText(module, text, Encoding.UTF8);

        // 3) Ensure google() is present in AGP 8 repo config (Unity 6 keeps it in settings.gradle)
        string settingsGradle = Path.Combine(path, "settings.gradle");
        if (File.Exists(settingsGradle))
        {
            string s = File.ReadAllText(settingsGradle, Encoding.UTF8);
            // Add google() inside dependencyResolutionManagement { repositories { ... } }
            if (!Regex.IsMatch(s, @"repositories\s*\{[^}]*google\(\)", RegexOptions.Singleline))
            {
                s = Regex.Replace(
                    s,
                    @"dependencyResolutionManagement\s*\{\s*repositories\s*\{",
                    m => m.Value + "\n        google()",
                    RegexOptions.Multiline
                );
                File.WriteAllText(settingsGradle, s, Encoding.UTF8);
            }
        }
    }
}
#endif

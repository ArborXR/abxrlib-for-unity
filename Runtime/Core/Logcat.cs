using UnityEngine;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AbxrLib.Runtime.Core
{
    public static class Logcat
    {
        /// <summary>Formats a single log line for both Unity and (on Android) logcat.</summary>
        private static string Format(string message, int lineNumber, string memberName, string filePath)
        {
            char separatorChar = (char)typeof(Path).GetTypeInfo().GetDeclaredField("DirectorySeparatorChar").GetValue(null);
            var className = Path.GetFileName(filePath.Replace('\\', separatorChar));
            return $"[AbxrLib] {message} (Line: {lineNumber}, {className}.{memberName})";
        }

        /// <summary>Sends to Android logcat when on Android; never throws. Falls back to Unity log if JNI fails (e.g. Test Runner Player).</summary>
        private static void LogToAndroid(string logLevel, string formatted, string fallbackMessage)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var androidLog = new AndroidJavaClass("android.util.Log");
                androidLog.CallStatic<int>(logLevel, "AbxrLib", formatted);
            }
            catch (System.Exception)
            {
                UnityEngine.Debug.Log($"[AbxrLib] (logcat unavailable) {fallbackMessage}");
            }
#endif
        }

        private static void Log(string logLevel, string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
        {
            string formatted = Format(message, lineNumber, memberName, filePath);
            LogToAndroid(logLevel, formatted, message);
#if !UNITY_EDITOR
            // In Player (including Test Runner Player), always emit to Unity log so Editor's Player log window shows output
            switch (logLevel)
            {
                case "e":
                    UnityEngine.Debug.LogError(formatted);
                    break;
                case "w":
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
                default:
                    UnityEngine.Debug.Log(formatted);
                    break;
            }
#endif
        }

        public static void Info(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
        {
#if ENABLE_LOGS || DEVELOPMENT_BUILD
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[AbxrLib] {message} {lineNumber} {memberName} {filePath} ");
#endif
            Log("i", message, lineNumber, memberName, filePath);
#else
            UnityEngine.Debug.Log($"[AbxrLib] {message}");
#endif
        }

        [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
        public static void Debug(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[AbxrLib] {message} {lineNumber} {memberName} {filePath} ");
#endif
            Log("d", message, lineNumber, memberName, filePath);
        }

        /// <summary>Emitted in Editor (including Test Runner), development builds, or when ENABLE_LOGS/DEVELOPMENT_BUILD is defined; no-op in release.</summary>
        public static void Warning(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (!UnityEngine.Debug.isDebugBuild)
                return;
#endif
#if UNITY_EDITOR
#if ENABLE_LOGS || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[AbxrLib] {message} {lineNumber} {memberName} {filePath} ");
#else
            UnityEngine.Debug.LogWarning($"[AbxrLib] {message}");
#endif
#endif
            Log("w", message, lineNumber, memberName, filePath);
        }

        /// <summary>Alias for <see cref="Warning"/>.</summary>
        public static void Warn(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
            => Warning(message, lineNumber, memberName, filePath);

        public static void Error(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
        {
#if ENABLE_LOGS || DEVELOPMENT_BUILD
#if UNITY_EDITOR
            UnityEngine.Debug.LogError($"[AbxrLib] {message} {lineNumber} {memberName} {filePath} ");
#endif
            Log("e", message, lineNumber, memberName, filePath);
#else
            UnityEngine.Debug.LogError($"[AbxrLib] {message}");
#endif
        }
    }
}

using UnityEngine;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AbxrLib.Runtime.Core
{
    public static class Logcat
{
    private static void Log(string logLevel, string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
        char separatorChar = (char)typeof(Path).GetTypeInfo().GetDeclaredField("DirectorySeparatorChar").GetValue(null);
        var className = Path.GetFileName(filePath.Replace('\\', separatorChar));
    
        var androidLog = new AndroidJavaClass("android.util.Log");
        androidLog.CallStatic<int>(logLevel, "AbxrLib", "(Line: " + lineNumber + "), Class: " + className + ", Method: " + memberName + "- Message: " + message);
    }
    
    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"AbxrLib: {message} {lineNumber} {memberName} {filePath} ");
#endif
        Logcat.Log ("i", message, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Debug(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"AbxrLib: {message} {lineNumber} {memberName} {filePath} ");
#endif
        Logcat.Log ("d", message, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning($"AbxrLib: {message} {lineNumber} {memberName} {filePath} ");
#endif
        Logcat.Log ("w", message, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Error(string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogError($"AbxrLib: {message} {lineNumber} {memberName} {filePath} ");
#endif
        Logcat.Log ("e", message, lineNumber, memberName, filePath);
    }
}
}

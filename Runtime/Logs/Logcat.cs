using UnityEngine;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Reflection;

public static class Logcat
{
    private static void Log(string logLevel, string msg, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
        char seperatorChar = (char)typeof(Path).GetTypeInfo().GetDeclaredField("DirectorySeparatorChar").GetValue(null);
        var classType = Path.GetFileName(filePath.Replace('\\', seperatorChar));
    
        var log = new AndroidJavaClass("android.util.Log");
        log.CallStatic<int>(logLevel, "AbxrLib", "(Line: " + lineNumber + "), Class: " + classType + ", Method: " + memberName + "- Message: " + msg);
    }
    
    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string msg, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"{msg} {lineNumber} {memberName} {memberName} {filePath} ");
#endif
        Logcat.Log ("i", msg, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Debug(string msg, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"{msg} {lineNumber} {memberName} {memberName} {filePath} ");
#endif
        Logcat.Log ("d", msg, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(string msg, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning($"{msg} {lineNumber} {memberName} {memberName} {filePath} ");
#endif
        Logcat.Log ("w", msg, lineNumber, memberName, filePath);
    }

    [Conditional("ENABLE_LOGS"), Conditional("DEVELOPMENT_BUILD")]
    public static void Error(string msg, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = null, [CallerFilePath] string filePath = null)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogError($"{msg} {lineNumber} {memberName} {memberName} {filePath} ");
#endif
        Logcat.Log ("e", msg, lineNumber, memberName, filePath);
    }
}

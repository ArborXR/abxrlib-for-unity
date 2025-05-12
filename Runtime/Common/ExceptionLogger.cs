using UnityEngine;
using System;
using System.IO;

public class ExceptionLogger : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Set up global exception handling
        Application.logMessageReceived += HandleUnityLog;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        
        // Create Android Java bridge
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var crashHandler = new AndroidJavaClass("com.example.crashhandlerlib.CrashHandlerInitializer");
        crashHandler.CallStatic("initializeCrashHandler", activity);
#endif
    }

    private static void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        if (type is not (LogType.Exception or LogType.Error)) return;
        
        string fullLog = $"Unity Exception:\n{logString}\nStack Trace:\n{stackTrace}";
        LogException(fullLog);
    }

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Exception e = (Exception)args.ExceptionObject;
        LogException($"Unhandled Exception:\n{e.Message}\nStack Trace:\n{e.StackTrace}");
    }

    private static void LogException(string message)
    {
        string logFilePath = Path.Combine(Application.persistentDataPath, "CrashLogs", $"crash_log_{Environment.TickCount}.txt");
        try
        {
            File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}\n");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to log to Android: {e.Message}");
        }
    }
}
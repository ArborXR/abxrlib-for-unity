using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbxrLib.Runtime.UI.DebugWindow
{
    public class DebugWindow : MonoBehaviour
    {
        public TMP_Text logText;
        public ScrollRect scrollRect;
        private readonly List<string> _logs = new();
        private const int MaxLogs = 100;
    
        private void OnEnable()
        {
            // Subscribe to Unity's log callback
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }
    
        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            float roundedTime = Mathf.Round(Time.time * 100f) / 100f;
            string newLog = $"[{roundedTime}] {type}: {logString}";
            _logs.Add(newLog);
            if (_logs.Count > MaxLogs) _logs.RemoveAt(0);
            logText.text = string.Join("\n", _logs);

            // Scroll to the bottom to show the latest text
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for accessing Unity console logs.
    /// </summary>
    [McpToolProvider]
    public class ConsoleTools
    {
        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private static bool _isCapturing;
        private const int MaxLogEntries = 100;

        static ConsoleTools()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            _isCapturing = true;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!_isCapturing) return;

            lock (_logBuffer)
            {
                _logBuffer.Add(new LogEntry
                {
                    message = condition,
                    stackTrace = stackTrace,
                    type = type.ToString(),
                    timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                });

                // Keep buffer size limited
                while (_logBuffer.Count > MaxLogEntries)
                {
                    _logBuffer.RemoveAt(0);
                }
            }
        }

        [McpTool("unity_get_console_logs", "Get recent Unity console logs")]
        public static object GetConsoleLogs(string argsJson)
        {
            var args = JsonUtility.FromJson<GetLogsArgs>(argsJson);
            // count defaults to 0 in JsonUtility, so we need to check for <= 0
            var count = (args?.count ?? 0) <= 0 ? 50 : args.count;
            var typeFilter = args?.type;

            List<LogEntry> results;

            lock (_logBuffer)
            {
                results = new List<LogEntry>();
                
                for (int i = _logBuffer.Count - 1; i >= 0 && results.Count < count; i--)
                {
                    var entry = _logBuffer[i];
                    
                    if (string.IsNullOrEmpty(typeFilter) || 
                        entry.type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(entry);
                    }
                }
            }

            results.Reverse(); // Oldest first

            return new ConsoleLogsResult
            {
                totalBuffered = _logBuffer.Count,
                returnedCount = results.Count,
                logs = results.ToArray()
            };
        }

        [McpTool("unity_clear_console", "Clear the Unity console")]
        public static object ClearConsole(string argsJson)
        {
            // Use reflection to access the internal Console clear method
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntries != null)
            {
                var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                clearMethod?.Invoke(null, null);
            }

            lock (_logBuffer)
            {
                _logBuffer.Clear();
            }

            return new { success = true, message = "Console cleared" };
        }

        #region Data Types

        [Serializable]
        public class GetLogsArgs
        {
            public int count;
            public string type; // "Log", "Warning", "Error"
        }

        [Serializable]
        public class ConsoleLogsResult
        {
            public int totalBuffered;
            public int returnedCount;
            public LogEntry[] logs;
        }

        [Serializable]
        public class LogEntry
        {
            public string message;
            public string stackTrace;
            public string type;
            public string timestamp;
        }

        #endregion
    }
}

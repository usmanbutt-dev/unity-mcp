using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// Editor window for controlling the MCP server.
    /// </summary>
    public class McpServerWindow : EditorWindow
    {
        private int _port = 3000;
        private Vector2 _scrollPosition;

        [MenuItem("Window/MCP Server")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpServerWindow>("MCP Server");
            window.minSize = new Vector2(300, 200);
        }

        private void OnEnable()
        {
            _port = EditorPrefs.GetInt("MCP_Port", 3000);
            McpServer.OnServerStateChanged += OnServerStateChanged;
        }

        private void OnDisable()
        {
            McpServer.OnServerStateChanged -= OnServerStateChanged;
        }

        private void OnServerStateChanged(bool isRunning)
        {
            Repaint();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField("Unity MCP Server", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Model Context Protocol server for AI agent integration. " +
                "Connect AI tools like Antigravity, Claude, or Cursor to control Unity.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Server Status
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            
            var isRunning = McpServer.Instance.IsRunning;
            var statusColor = isRunning ? Color.green : Color.gray;
            var statusText = isRunning ? "● Running" : "○ Stopped";

            using (new EditorGUILayout.HorizontalScope())
            {
                var originalColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField(statusText, EditorStyles.boldLabel, GUILayout.Width(100));
                GUI.color = originalColor;

                if (isRunning)
                {
                    EditorGUILayout.LabelField($"http://localhost:{McpServer.Instance.Port}/");
                }
            }

            EditorGUILayout.Space(10);

            // Port Configuration
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledGroupScope(isRunning))
            {
                var newPort = EditorGUILayout.IntField("Port", _port);
                if (newPort != _port && newPort > 0 && newPort < 65536)
                {
                    _port = newPort;
                    EditorPrefs.SetInt("MCP_Port", _port);
                }
            }

            EditorGUILayout.Space(10);

            // Control Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!isRunning)
                {
                    if (GUILayout.Button("Start Server", GUILayout.Height(30)))
                    {
                        ToolRegistry.Initialize();
                        McpServer.Instance.Start(_port);
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Server", GUILayout.Height(30)))
                    {
                        McpServer.Instance.Stop();
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Connection Info
            if (isRunning)
            {
                EditorGUILayout.LabelField("Connection Info", EditorStyles.boldLabel);
                
                EditorGUILayout.HelpBox(
                    "Add this to your MCP client configuration:\n\n" +
                    "{\n" +
                    "  \"mcpServers\": {\n" +
                    "    \"unity\": {\n" +
                    $"      \"url\": \"http://localhost:{_port}/\"\n" +
                    "    }\n" +
                    "  }\n" +
                    "}",
                    MessageType.None);

                if (GUILayout.Button("Copy URL to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = $"http://localhost:{_port}/";
                    Debug.Log("[MCP] URL copied to clipboard.");
                }
            }

            EditorGUILayout.Space(10);

            // Available Tools
            EditorGUILayout.LabelField("Available Tools", EditorStyles.boldLabel);
            
            var tools = ToolRegistry.GetToolDefinitions();
            foreach (var tool in tools)
            {
                EditorGUILayout.LabelField($"• {tool.name}", EditorStyles.miniLabel);
            }

            if (tools.Length == 0)
            {
                EditorGUILayout.LabelField("(Start server to load tools)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}

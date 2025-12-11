using System;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for general Unity Editor operations.
    /// </summary>
    [McpToolProvider]
    public class EditorTools
    {
        [McpTool("unity_execute_menu", "Execute a Unity menu item")]
        public static object ExecuteMenu(string argsJson)
        {
            var args = JsonUtility.FromJson<ExecuteMenuArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.menuPath))
            {
                return new { error = "menuPath parameter is required" };
            }

            var result = EditorApplication.ExecuteMenuItem(args.menuPath);
            
            return new ExecuteMenuResult
            {
                menuPath = args.menuPath,
                success = result
            };
        }

        [McpTool("unity_select_object", "Select a GameObject in the Editor")]
        public static object SelectObject(string argsJson)
        {
            var args = JsonUtility.FromJson<SelectObjectArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }

            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}", success = false };
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return new SelectObjectResult
            {
                path = args.path,
                success = true
            };
        }

        [McpTool("unity_get_selection", "Get the currently selected objects in the Editor")]
        public static object GetSelection(string argsJson)
        {
            var selection = Selection.gameObjects;
            var paths = new string[selection.Length];
            
            for (int i = 0; i < selection.Length; i++)
            {
                paths[i] = GetGameObjectPath(selection[i]);
            }

            return new SelectionResult
            {
                count = selection.Length,
                paths = paths
            };
        }

        [McpTool("unity_get_editor_state", "Get the current state of the Unity Editor")]
        public static object GetEditorState(string argsJson)
        {
            return new EditorStateResult
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                currentScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                platform = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        #region Data Types

        [Serializable]
        public class ExecuteMenuArgs
        {
            public string menuPath;
        }

        [Serializable]
        public class ExecuteMenuResult
        {
            public string menuPath;
            public bool success;
        }

        [Serializable]
        public class SelectObjectArgs
        {
            public string path;
        }

        [Serializable]
        public class SelectObjectResult
        {
            public string path;
            public bool success;
        }

        [Serializable]
        public class SelectionResult
        {
            public int count;
            public string[] paths;
        }

        [Serializable]
        public class EditorStateResult
        {
            public bool isPlaying;
            public bool isPaused;
            public bool isCompiling;
            public string currentScene;
            public string currentScenePath;
            public string platform;
        }

        #endregion
    }
}

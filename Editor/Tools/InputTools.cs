using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for simulating input during play mode.
    /// Note: Input simulation is limited and may not work with all input systems.
    /// </summary>
    [McpToolProvider]
    public class InputTools
    {
        [McpTool("unity_simulate_key", "Simulate a key press during play mode", typeof(SimulateKeyArgs))]
        public static object SimulateKey(string argsJson)
        {
            var args = JsonUtility.FromJson<SimulateKeyArgs>(argsJson);
            
            if (!EditorApplication.isPlaying)
            {
                return new { error = "Input simulation only works during play mode" };
            }
            
            if (string.IsNullOrEmpty(args?.key))
            {
                return new { error = "key parameter is required" };
            }
            
            // Try to parse the key
            if (!Enum.TryParse<KeyCode>(args.key, true, out var keyCode))
            {
                return new { error = $"Invalid key: {args.key}. Use Unity KeyCode names like 'W', 'Space', 'LeftArrow', etc." };
            }
            
            // Note: Unity's Input system doesn't allow direct key simulation from editor code.
            // We can use SendMessage or find UI elements and invoke them.
            // For basic testing, we can use reflection to access internal Unity systems.
            
            string action = string.IsNullOrEmpty(args.action) ? "press" : args.action.ToLower();
            
            // The most reliable approach is to use the Event system
            Event keyEvent = null;
            
            switch (action)
            {
                case "down":
                    keyEvent = new Event { type = EventType.KeyDown, keyCode = keyCode };
                    break;
                case "up":
                    keyEvent = new Event { type = EventType.KeyUp, keyCode = keyCode };
                    break;
                case "press":
                default:
                    // Simulate down then up
                    keyEvent = new Event { type = EventType.KeyDown, keyCode = keyCode };
                    break;
            }
            
            // Try to send to focused window
            var focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null)
            {
                focusedWindow.SendEvent(keyEvent);
            }
            
            return new SimulateKeyResult
            {
                success = true,
                key = keyCode.ToString(),
                action = action,
                note = "Key event sent. May not work with all input systems. Consider using unity_execute_menu for editor actions or direct component method calls for game logic."
            };
        }

        [McpTool("unity_simulate_mouse", "Simulate a mouse click during play mode", typeof(SimulateMouseArgs))]
        public static object SimulateMouse(string argsJson)
        {
            var args = JsonUtility.FromJson<SimulateMouseArgs>(argsJson);
            
            if (!EditorApplication.isPlaying)
            {
                return new { error = "Input simulation only works during play mode" };
            }
            
            int button = args?.button ?? 0;
            float x = args?.x ?? 0;
            float y = args?.y ?? 0;
            string action = string.IsNullOrEmpty(args?.action) ? "click" : args.action.ToLower();
            
            EventType eventType;
            switch (action)
            {
                case "down":
                    eventType = EventType.MouseDown;
                    break;
                case "up":
                    eventType = EventType.MouseUp;
                    break;
                case "move":
                    eventType = EventType.MouseMove;
                    break;
                case "click":
                default:
                    eventType = EventType.MouseDown;
                    break;
            }
            
            Event mouseEvent = new Event
            {
                type = eventType,
                button = button,
                mousePosition = new Vector2(x, y)
            };
            
            var focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null)
            {
                focusedWindow.SendEvent(mouseEvent);
                
                // For click, also send up event
                if (action == "click")
                {
                    mouseEvent = new Event
                    {
                        type = EventType.MouseUp,
                        button = button,
                        mousePosition = new Vector2(x, y)
                    };
                    focusedWindow.SendEvent(mouseEvent);
                }
            }
            
            return new SimulateMouseResult
            {
                success = true,
                button = button,
                action = action,
                x = x,
                y = y,
                note = "Mouse event sent. For UI testing, consider using unity_click_ui_element instead."
            };
        }

        [McpTool("unity_click_ui_element", "Click a UI element by name during play mode", typeof(ClickUIElementArgs))]
        public static object ClickUIElement(string argsJson)
        {
            var args = JsonUtility.FromJson<ClickUIElementArgs>(argsJson);
            
            if (!EditorApplication.isPlaying)
            {
                return new { error = "UI interaction only works during play mode" };
            }
            
            if (string.IsNullOrEmpty(args?.objectName))
            {
                return new { error = "objectName parameter is required" };
            }
            
            // Find the GameObject
            var go = GameObject.Find(args.objectName);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.objectName}" };
            }
            
            // Try to find and invoke Button component
            var button = go.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return new ClickUIElementResult
                {
                    success = true,
                    objectName = args.objectName,
                    componentType = "Button",
                    message = "Button click invoked"
                };
            }
            
            // Try Toggle
            var toggle = go.GetComponent<UnityEngine.UI.Toggle>();
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
                return new ClickUIElementResult
                {
                    success = true,
                    objectName = args.objectName,
                    componentType = "Toggle",
                    message = $"Toggle set to {toggle.isOn}"
                };
            }
            
            return new { error = $"No clickable UI component found on: {args.objectName}" };
        }

        #region Data Types

        [Serializable]
        public class SimulateKeyArgs
        {
            [McpParam("Key code (e.g., 'W', 'Space', 'Return', 'Escape')", Required = true)] public string key;
            [McpParam("Action type", EnumValues = new[] { "press", "down", "up" })] public string action;
        }

        [Serializable]
        public class SimulateKeyResult
        {
            public bool success;
            public string key;
            public string action;
            public string note;
        }

        [Serializable]
        public class SimulateMouseArgs
        {
            [McpParam("X position in screen coordinates")] public float x;
            [McpParam("Y position in screen coordinates")] public float y;
            [McpParam("Mouse button (0=left, 1=right, 2=middle)")] public int button;
            [McpParam("Action type", EnumValues = new[] { "click", "down", "up", "move" })] public string action;
        }

        [Serializable]
        public class SimulateMouseResult
        {
            public bool success;
            public int button;
            public string action;
            public float x;
            public float y;
            public string note;
        }

        [Serializable]
        public class ClickUIElementArgs
        {
            [McpParam("Name of the GameObject with UI component", Required = true)] public string objectName;
        }

        [Serializable]
        public class ClickUIElementResult
        {
            public bool success;
            public string objectName;
            public string componentType;
            public string message;
        }

        #endregion
    }
}

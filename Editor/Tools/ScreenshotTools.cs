using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for capturing screenshots and visual information.
    /// </summary>
    [McpToolProvider]
    public class ScreenshotTools
    {
        [McpTool("unity_take_screenshot", "Capture a screenshot of the Game View or Scene View", typeof(TakeScreenshotArgs))]
        public static object TakeScreenshot(string argsJson)
        {
            var args = JsonUtility.FromJson<TakeScreenshotArgs>(argsJson);
            
            string viewType = string.IsNullOrEmpty(args?.view) ? "game" : args.view.ToLower();
            int width = args?.width > 0 ? args.width : 1280;
            int height = args?.height > 0 ? args.height : 720;
            
            Texture2D screenshot = null;
            
            try
            {
                if (viewType == "scene")
                {
                    screenshot = CaptureSceneView(width, height);
                }
                else
                {
                    screenshot = CaptureGameView(width, height);
                }
                
                if (screenshot == null)
                {
                    return new { error = $"Failed to capture {viewType} view. Make sure the view is open." };
                }
                
                // Encode to PNG and convert to base64
                byte[] pngData = screenshot.EncodeToPNG();
                string base64 = Convert.ToBase64String(pngData);
                
                return new TakeScreenshotResult
                {
                    success = true,
                    view = viewType,
                    width = screenshot.width,
                    height = screenshot.height,
                    format = "png",
                    base64 = base64,
                    sizeBytes = pngData.Length
                };
            }
            finally
            {
                if (screenshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }
            }
        }

        private static Texture2D CaptureGameView(int width, int height)
        {
            // Find the Game View window
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return null;
            
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null) return null;
            
            // Use RenderTexture to capture at specific resolution
            var rt = new RenderTexture(width, height, 24);
            var prevRT = RenderTexture.active;
            
            try
            {
                // Get the main camera
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    // Try to find any camera
                    mainCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                }
                
                if (mainCamera == null)
                {
                    return null;
                }
                
                // Render camera to texture
                var prevTargetTexture = mainCamera.targetTexture;
                mainCamera.targetTexture = rt;
                mainCamera.Render();
                mainCamera.targetTexture = prevTargetTexture;
                
                // Read pixels from render texture
                RenderTexture.active = rt;
                var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                
                return screenshot;
            }
            finally
            {
                RenderTexture.active = prevRT;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static Texture2D CaptureSceneView(int width, int height)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return null;
            
            var camera = sceneView.camera;
            if (camera == null) return null;
            
            var rt = new RenderTexture(width, height, 24);
            var prevRT = RenderTexture.active;
            
            try
            {
                // Render scene view camera
                var prevTargetTexture = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = prevTargetTexture;
                
                // Read pixels
                RenderTexture.active = rt;
                var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                
                return screenshot;
            }
            finally
            {
                RenderTexture.active = prevRT;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        #region Data Types

        [Serializable]
        public class TakeScreenshotArgs
        {
            [McpParam("View to capture", EnumValues = new[] { "game", "scene" })] public string view;
            [McpParam("Screenshot width (default 1280)")] public int width;
            [McpParam("Screenshot height (default 720)")] public int height;
        }

        [Serializable]
        public class TakeScreenshotResult
        {
            public bool success;
            public string view;
            public int width;
            public int height;
            public string format;
            public string base64;
            public int sizeBytes;
        }

        #endregion
    }
}

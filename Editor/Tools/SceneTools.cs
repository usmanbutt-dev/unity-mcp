using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for working with Unity Scenes.
    /// </summary>
    [McpToolProvider]
    public class SceneTools
    {
        [McpTool("unity_get_scenes", "List all scenes in the project")]
        public static object GetScenes(string argsJson)
        {
            var scenes = new List<SceneInfo>();
            
            // Find all scene assets
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var isInBuild = false;
                var buildIndex = -1;
                
                // Check if scene is in build settings
                for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    if (EditorBuildSettings.scenes[i].path == path)
                    {
                        isInBuild = true;
                        buildIndex = i;
                        break;
                    }
                }
                
                scenes.Add(new SceneInfo
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    guid = guid,
                    isInBuildSettings = isInBuild,
                    buildIndex = buildIndex,
                    isLoaded = IsSceneLoaded(path),
                    isActive = SceneManager.GetActiveScene().path == path
                });
            }
            
            return new GetScenesResult
            {
                totalCount = scenes.Count,
                scenes = scenes.ToArray()
            };
        }

        [McpTool("unity_open_scene", "Open a scene in the editor", typeof(OpenSceneArgs))]
        public static object OpenScene(string argsJson)
        {
            var args = JsonUtility.FromJson<OpenSceneArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.scenePath))
            {
                return new { error = "scenePath parameter is required" };
            }
            
            // Check if scene exists
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(args.scenePath);
            if (sceneAsset == null)
            {
                return new { error = $"Scene not found: {args.scenePath}" };
            }
            
            // Prompt to save if there are unsaved changes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return new { error = "Operation cancelled by user" };
            }
            
            OpenSceneMode mode = args.additive 
                ? OpenSceneMode.Additive 
                : OpenSceneMode.Single;
            
            var scene = EditorSceneManager.OpenScene(args.scenePath, mode);
            
            return new OpenSceneResult
            {
                success = true,
                scenePath = args.scenePath,
                sceneName = scene.name,
                isLoaded = scene.isLoaded,
                mode = mode.ToString()
            };
        }

        [McpTool("unity_save_scene", "Save the current scene", typeof(SaveSceneArgs))]
        public static object SaveScene(string argsJson)
        {
            var args = JsonUtility.FromJson<SaveSceneArgs>(argsJson);
            
            Scene sceneToSave;
            
            if (!string.IsNullOrEmpty(args?.scenePath))
            {
                // Save specific scene
                sceneToSave = SceneManager.GetSceneByPath(args.scenePath);
                if (!sceneToSave.IsValid())
                {
                    return new { error = $"Scene not loaded: {args.scenePath}" };
                }
            }
            else
            {
                // Save active scene
                sceneToSave = SceneManager.GetActiveScene();
            }
            
            bool success;
            
            if (!string.IsNullOrEmpty(args?.saveAsPath))
            {
                // Save as new path
                success = EditorSceneManager.SaveScene(sceneToSave, args.saveAsPath);
            }
            else
            {
                // Save to current path
                success = EditorSceneManager.SaveScene(sceneToSave);
            }
            
            return new SaveSceneResult
            {
                success = success,
                scenePath = sceneToSave.path,
                sceneName = sceneToSave.name
            };
        }

        [McpTool("unity_new_scene", "Create a new empty scene", typeof(NewSceneArgs))]
        public static object NewScene(string argsJson)
        {
            var args = JsonUtility.FromJson<NewSceneArgs>(argsJson);
            
            // Prompt to save if there are unsaved changes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return new { error = "Operation cancelled by user" };
            }
            
            NewSceneSetup setup = args?.addDefaultGameObjects == true 
                ? NewSceneSetup.DefaultGameObjects 
                : NewSceneSetup.EmptyScene;
            
            NewSceneMode mode = args?.additive == true 
                ? NewSceneMode.Additive 
                : NewSceneMode.Single;
            
            var scene = EditorSceneManager.NewScene(setup, mode);
            
            // Save if path provided
            if (!string.IsNullOrEmpty(args?.savePath))
            {
                EditorSceneManager.SaveScene(scene, args.savePath);
            }
            
            return new NewSceneResult
            {
                success = true,
                sceneName = scene.name,
                scenePath = scene.path,
                hasDefaultObjects = setup == NewSceneSetup.DefaultGameObjects
            };
        }

        [McpTool("unity_close_scene", "Close a scene (when multiple scenes are loaded)", typeof(CloseSceneArgs))]
        public static object CloseScene(string argsJson)
        {
            var args = JsonUtility.FromJson<CloseSceneArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.scenePath))
            {
                return new { error = "scenePath parameter is required" };
            }
            
            var scene = SceneManager.GetSceneByPath(args.scenePath);
            if (!scene.IsValid())
            {
                return new { error = $"Scene not loaded: {args.scenePath}" };
            }
            
            if (SceneManager.sceneCount <= 1)
            {
                return new { error = "Cannot close the only loaded scene" };
            }
            
            bool removeScene = args.removeScene;
            bool success = EditorSceneManager.CloseScene(scene, removeScene);
            
            return new CloseSceneResult
            {
                success = success,
                scenePath = args.scenePath,
                removed = removeScene
            };
        }

        [McpTool("unity_set_active_scene", "Set the active scene (when multiple scenes are loaded)", typeof(SetActiveSceneArgs))]
        public static object SetActiveScene(string argsJson)
        {
            var args = JsonUtility.FromJson<SetActiveSceneArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.scenePath))
            {
                return new { error = "scenePath parameter is required" };
            }
            
            var scene = SceneManager.GetSceneByPath(args.scenePath);
            if (!scene.IsValid())
            {
                return new { error = $"Scene not loaded: {args.scenePath}" };
            }
            
            bool success = SceneManager.SetActiveScene(scene);
            
            return new SetActiveSceneResult
            {
                success = success,
                scenePath = args.scenePath,
                sceneName = scene.name
            };
        }

        #region Helper Methods

        private static bool IsSceneLoaded(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Data Types

        [Serializable]
        public class SceneInfo
        {
            public string name;
            public string path;
            public string guid;
            public bool isInBuildSettings;
            public int buildIndex;
            public bool isLoaded;
            public bool isActive;
        }

        [Serializable]
        public class GetScenesResult
        {
            public int totalCount;
            public SceneInfo[] scenes;
        }

        [Serializable]
        public class OpenSceneArgs
        {
            [McpParam("Path to the scene asset", Required = true)] public string scenePath;
            [McpParam("If true, opens scene additively")] public bool additive;
        }

        [Serializable]
        public class OpenSceneResult
        {
            public bool success;
            public string scenePath;
            public string sceneName;
            public bool isLoaded;
            public string mode;
        }

        [Serializable]
        public class SaveSceneArgs
        {
            [McpParam("Path to scene to save (optional, uses active scene if not provided)")] public string scenePath;
            [McpParam("Path to save as (optional, saves to current path if not provided)")] public string saveAsPath;
        }

        [Serializable]
        public class SaveSceneResult
        {
            public bool success;
            public string scenePath;
            public string sceneName;
        }

        [Serializable]
        public class NewSceneArgs
        {
            [McpParam("Path to immediately save the new scene")] public string savePath;
            [McpParam("If true, adds Camera and Light")] public bool addDefaultGameObjects;
            [McpParam("If true, adds scene without closing current scenes")] public bool additive;
        }

        [Serializable]
        public class NewSceneResult
        {
            public bool success;
            public string sceneName;
            public string scenePath;
            public bool hasDefaultObjects;
        }

        [Serializable]
        public class CloseSceneArgs
        {
            [McpParam("Path to the scene to close", Required = true)] public string scenePath;
            [McpParam("If true, removes scene from hierarchy completely")] public bool removeScene;
        }

        [Serializable]
        public class CloseSceneResult
        {
            public bool success;
            public string scenePath;
            public bool removed;
        }

        [Serializable]
        public class SetActiveSceneArgs
        {
            [McpParam("Path to the scene to set as active", Required = true)] public string scenePath;
        }

        [Serializable]
        public class SetActiveSceneResult
        {
            public bool success;
            public string scenePath;
            public string sceneName;
        }

        #endregion
    }
}

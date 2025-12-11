using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for working with Prefabs.
    /// </summary>
    [McpToolProvider]
    public class PrefabTools
    {
        [McpTool("unity_instantiate_prefab", "Instantiate a prefab in the scene", typeof(InstantiatePrefabArgs))]
        public static object InstantiatePrefab(string argsJson)
        {
            var args = JsonUtility.FromJson<InstantiatePrefabArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.prefabPath))
            {
                return new { error = "prefabPath parameter is required" };
            }
            
            // Load the prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(args.prefabPath);
            if (prefab == null)
            {
                return new { error = $"Prefab not found: {args.prefabPath}" };
            }
            
            // Instantiate
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            
            // Set name if specified
            if (!string.IsNullOrEmpty(args.name))
            {
                instance.name = args.name;
            }
            
            // Set parent if specified
            if (!string.IsNullOrEmpty(args.parentPath))
            {
                var parent = GameObject.Find(args.parentPath);
                if (parent != null)
                {
                    instance.transform.SetParent(parent.transform, false);
                }
            }
            
            // Set transform
            if (args.position != null)
            {
                instance.transform.position = new Vector3(args.position.x, args.position.y, args.position.z);
            }
            if (args.rotation != null)
            {
                instance.transform.eulerAngles = new Vector3(args.rotation.x, args.rotation.y, args.rotation.z);
            }
            // Only set scale if non-zero (JsonUtility defaults to 0,0,0 which would make object invisible)
            if (args.scale != null && !IsZeroVec3(args.scale))
            {
                instance.transform.localScale = new Vector3(args.scale.x, args.scale.y, args.scale.z);
            }
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");
            
            // Select the new instance
            Selection.activeGameObject = instance;
            
            return new InstantiatePrefabResult
            {
                success = true,
                prefabPath = args.prefabPath,
                instanceName = instance.name,
                instancePath = GetGameObjectPath(instance),
                instanceId = instance.GetInstanceID()
            };
        }

        [McpTool("unity_get_prefab_info", "Get detailed information about a prefab", typeof(GetPrefabInfoArgs))]
        public static object GetPrefabInfo(string argsJson)
        {
            var args = JsonUtility.FromJson<GetPrefabInfoArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.prefabPath))
            {
                return new { error = "prefabPath parameter is required" };
            }
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(args.prefabPath);
            if (prefab == null)
            {
                return new { error = $"Prefab not found: {args.prefabPath}" };
            }
            
            var info = BuildPrefabInfo(prefab, 0, 5);
            info.assetPath = args.prefabPath;
            info.guid = AssetDatabase.AssetPathToGUID(args.prefabPath);
            
            return info;
        }

        [McpTool("unity_create_prefab", "Create a prefab from a GameObject in the scene", typeof(CreatePrefabArgs))]
        public static object CreatePrefab(string argsJson)
        {
            var args = JsonUtility.FromJson<CreatePrefabArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.gameObjectPath))
            {
                return new { error = "gameObjectPath parameter is required" };
            }
            if (string.IsNullOrEmpty(args?.savePath))
            {
                return new { error = "savePath parameter is required (e.g., Assets/Prefabs/MyPrefab.prefab)" };
            }
            
            var go = GameObject.Find(args.gameObjectPath);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.gameObjectPath}" };
            }
            
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(args.savePath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                // Create folders recursively
                string[] folders = directory.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string nextPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = nextPath;
                }
            }
            
            // Create the prefab
            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, args.savePath, out success);
            
            if (!success || prefab == null)
            {
                return new { error = $"Failed to create prefab at: {args.savePath}" };
            }
            
            return new CreatePrefabResult
            {
                success = true,
                prefabPath = args.savePath,
                guid = AssetDatabase.AssetPathToGUID(args.savePath)
            };
        }

        [McpTool("unity_unpack_prefab", "Unpack a prefab instance in the scene", typeof(UnpackPrefabArgs))]
        public static object UnpackPrefab(string argsJson)
        {
            var args = JsonUtility.FromJson<UnpackPrefabArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }
            
            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }
            
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                return new { error = $"GameObject is not a prefab instance: {args.path}" };
            }
            
            var mode = args.completely 
                ? PrefabUnpackMode.Completely 
                : PrefabUnpackMode.OutermostRoot;
            
            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.UserAction);
            
            return new UnpackPrefabResult
            {
                success = true,
                path = args.path,
                unpackMode = mode.ToString()
            };
        }

        #region Helper Methods

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

        private static bool IsZeroVec3(Vec3 v)
        {
            if (v == null) return true;
            return v.x == 0 && v.y == 0 && v.z == 0;
        }

        private static PrefabInfoResult BuildPrefabInfo(GameObject go, int depth, int maxDepth)
        {
            var components = go.GetComponents<Component>();
            var componentInfos = new List<ComponentInfo>();
            foreach (var c in components)
            {
                if (c == null) continue;
                componentInfos.Add(new ComponentInfo
                {
                    typeName = c.GetType().Name,
                    fullTypeName = c.GetType().FullName
                });
            }
            
            var children = new List<PrefabInfoResult>();
            if (depth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildPrefabInfo(go.transform.GetChild(i).gameObject, depth + 1, maxDepth));
                }
            }
            
            return new PrefabInfoResult
            {
                name = go.name,
                activeSelf = go.activeSelf,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                position = go.transform.localPosition.ToString(),
                rotation = go.transform.localEulerAngles.ToString(),
                scale = go.transform.localScale.ToString(),
                components = componentInfos.ToArray(),
                children = children.ToArray(),
                childCount = go.transform.childCount
            };
        }

        #endregion

        #region Data Types

        [Serializable]
        public class Vec3
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        public class InstantiatePrefabArgs
        {
            [McpParam("Path to the prefab asset", Required = true)] public string prefabPath;
            [McpParam("Instance name override")] public string name;
            [McpParam("Path to parent GameObject")] public string parentPath;
            [McpParam("World position {x, y, z}")] public Vec3 position;
            [McpParam("Rotation in euler angles {x, y, z}")] public Vec3 rotation;
            [McpParam("Local scale {x, y, z}")] public Vec3 scale;
        }

        [Serializable]
        public class InstantiatePrefabResult
        {
            public bool success;
            public string prefabPath;
            public string instanceName;
            public string instancePath;
            public int instanceId;
        }

        [Serializable]
        public class GetPrefabInfoArgs
        {
            [McpParam("Path to the prefab asset", Required = true)] public string prefabPath;
        }

        [Serializable]
        public class PrefabInfoResult
        {
            public string assetPath;
            public string guid;
            public string name;
            public bool activeSelf;
            public string tag;
            public string layer;
            public string position;
            public string rotation;
            public string scale;
            public ComponentInfo[] components;
            public PrefabInfoResult[] children;
            public int childCount;
        }

        [Serializable]
        public class ComponentInfo
        {
            public string typeName;
            public string fullTypeName;
        }

        [Serializable]
        public class CreatePrefabArgs
        {
            [McpParam("Path to the GameObject in scene", Required = true)] public string gameObjectPath;
            [McpParam("Path to save the prefab (e.g., Assets/Prefabs/MyPrefab.prefab)", Required = true)] public string savePath;
        }

        [Serializable]
        public class CreatePrefabResult
        {
            public bool success;
            public string prefabPath;
            public string guid;
        }

        [Serializable]
        public class UnpackPrefabArgs
        {
            [McpParam("Path to the prefab instance in scene", Required = true)] public string path;
            [McpParam("If true, unpacks completely; otherwise unpacks outermost root")] public bool completely;
        }

        [Serializable]
        public class UnpackPrefabResult
        {
            public bool success;
            public string path;
            public string unpackMode;
        }

        #endregion
    }
}

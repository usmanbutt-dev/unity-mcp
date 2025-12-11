using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// Handles MCP resource operations (resources/list, resources/read).
    /// </summary>
    public static class ResourceHandler
    {
        /// <summary>
        /// Handle resources/list request.
        /// </summary>
        public static object HandleResourcesList(string paramsJson)
        {
            var resources = new List<McpResource>();
            
            // List all C# scripts
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            foreach (var guid in scriptGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                resources.Add(new McpResource
                {
                    uri = $"unity://script/{path}",
                    name = Path.GetFileName(path),
                    description = $"C# Script: {path}",
                    mimeType = "text/x-csharp"
                });
            }
            
            // List all scenes
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                resources.Add(new McpResource
                {
                    uri = $"unity://scene/{path}",
                    name = Path.GetFileName(path),
                    description = $"Unity Scene: {path}",
                    mimeType = "application/x-unity-scene"
                });
            }
            
            // List all prefabs
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                resources.Add(new McpResource
                {
                    uri = $"unity://prefab/{path}",
                    name = Path.GetFileName(path),
                    description = $"Prefab: {path}",
                    mimeType = "application/x-unity-prefab"
                });
            }
            
            // List all ScriptableObjects
            var soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            foreach (var guid in soGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                resources.Add(new McpResource
                {
                    uri = $"unity://scriptableobject/{path}",
                    name = Path.GetFileName(path),
                    description = $"ScriptableObject: {path}",
                    mimeType = "application/json"
                });
            }
            
            // Limit results
            if (resources.Count > 500)
            {
                resources = resources.GetRange(0, 500);
            }
            
            return new McpResourcesListResult
            {
                resources = resources.ToArray()
            };
        }

        /// <summary>
        /// Handle resources/read request.
        /// </summary>
        public static object HandleResourcesRead(string paramsJson)
        {
            // Extract URI from params
            string uri = ExtractUri(paramsJson);
            
            if (string.IsNullOrEmpty(uri))
            {
                throw new ArgumentException("uri parameter is required");
            }
            
            // Parse the URI: unity://type/path
            if (!uri.StartsWith("unity://"))
            {
                throw new ArgumentException($"Invalid URI scheme. Expected unity://, got: {uri}");
            }
            
            string remainder = uri.Substring(8); // Remove "unity://"
            int slashIndex = remainder.IndexOf('/');
            if (slashIndex < 0)
            {
                throw new ArgumentException($"Invalid URI format: {uri}");
            }
            
            string resourceType = remainder.Substring(0, slashIndex);
            string assetPath = remainder.Substring(slashIndex + 1);
            
            var contents = new List<McpResourceContent>();
            
            switch (resourceType)
            {
                case "script":
                    contents.Add(ReadScriptResource(assetPath));
                    break;
                case "scene":
                    contents.Add(ReadSceneResource(assetPath));
                    break;
                case "prefab":
                    contents.Add(ReadPrefabResource(assetPath));
                    break;
                case "scriptableobject":
                    contents.Add(ReadScriptableObjectResource(assetPath));
                    break;
                case "file":
                    contents.Add(ReadFileResource(assetPath));
                    break;
                default:
                    throw new ArgumentException($"Unknown resource type: {resourceType}");
            }
            
            return new McpResourcesReadResult
            {
                contents = contents.ToArray()
            };
        }

        private static McpResourceContent ReadScriptResource(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Script not found: {assetPath}");
            }
            
            string content = File.ReadAllText(fullPath);
            
            return new McpResourceContent
            {
                uri = $"unity://script/{assetPath}",
                mimeType = "text/x-csharp",
                text = content
            };
        }

        private static McpResourceContent ReadSceneResource(string assetPath)
        {
            // For scenes, return metadata about the scene
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath);
            if (sceneAsset == null)
            {
                throw new FileNotFoundException($"Scene not found: {assetPath}");
            }
            
            var info = new SceneInfo
            {
                name = sceneAsset.name,
                path = assetPath,
                guid = AssetDatabase.AssetPathToGUID(assetPath)
            };
            
            return new McpResourceContent
            {
                uri = $"unity://scene/{assetPath}",
                mimeType = "application/json",
                text = JsonUtility.ToJson(info, true)
            };
        }

        private static McpResourceContent ReadPrefabResource(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Prefab not found: {assetPath}");
            }
            
            // Build prefab structure
            var info = BuildGameObjectInfo(prefab);
            
            return new McpResourceContent
            {
                uri = $"unity://prefab/{assetPath}",
                mimeType = "application/json",
                text = JsonUtility.ToJson(info, true)
            };
        }

        private static McpResourceContent ReadScriptableObjectResource(string assetPath)
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (so == null)
            {
                throw new FileNotFoundException($"ScriptableObject not found: {assetPath}");
            }
            
            // Serialize to JSON
            string json = JsonUtility.ToJson(so, true);
            
            return new McpResourceContent
            {
                uri = $"unity://scriptableobject/{assetPath}",
                mimeType = "application/json",
                text = json
            };
        }

        private static McpResourceContent ReadFileResource(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {assetPath}");
            }
            
            string content = File.ReadAllText(fullPath);
            string mimeType = GetMimeType(assetPath);
            
            return new McpResourceContent
            {
                uri = $"unity://file/{assetPath}",
                mimeType = mimeType,
                text = content
            };
        }

        private static PrefabInfo BuildGameObjectInfo(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var componentNames = new List<string>();
            foreach (var c in components)
            {
                if (c != null) componentNames.Add(c.GetType().Name);
            }
            
            var children = new List<PrefabInfo>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                children.Add(BuildGameObjectInfo(go.transform.GetChild(i).gameObject));
            }
            
            return new PrefabInfo
            {
                name = go.name,
                activeSelf = go.activeSelf,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                components = componentNames.ToArray(),
                children = children.ToArray()
            };
        }

        private static string GetMimeType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            switch (ext)
            {
                case ".cs": return "text/x-csharp";
                case ".js": return "text/javascript";
                case ".json": return "application/json";
                case ".xml": return "application/xml";
                case ".txt": return "text/plain";
                case ".md": return "text/markdown";
                case ".shader": return "text/x-shader";
                case ".hlsl": return "text/x-hlsl";
                case ".cginc": return "text/x-cginc";
                case ".asmdef": return "application/json";
                default: return "text/plain";
            }
        }

        private static string ExtractUri(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            
            // Simple extraction for "uri":"value"
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"uri\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        #region Data Types

        [Serializable]
        public class SceneInfo
        {
            public string name;
            public string path;
            public string guid;
        }

        [Serializable]
        public class PrefabInfo
        {
            public string name;
            public bool activeSelf;
            public string tag;
            public string layer;
            public string[] components;
            public PrefabInfo[] children;
        }

        #endregion
    }

    #region MCP Resource Types

    [Serializable]
    public class McpResource
    {
        public string uri;
        public string name;
        public string description;
        public string mimeType;
    }

    [Serializable]
    public class McpResourcesListResult
    {
        public McpResource[] resources;
    }

    [Serializable]
    public class McpResourceContent
    {
        public string uri;
        public string mimeType;
        public string text;
    }

    [Serializable]
    public class McpResourcesReadResult
    {
        public McpResourceContent[] contents;
    }

    #endregion
}

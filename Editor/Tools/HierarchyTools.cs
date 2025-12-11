using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for querying Unity scene hierarchy.
    /// </summary>
    [McpToolProvider]
    public class HierarchyTools
    {
        [McpTool("unity_get_hierarchy", "Get the hierarchy of GameObjects in the current scene")]
        public static object GetHierarchy(string argsJson)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var hierarchy = new List<GameObjectInfo>();

            foreach (var go in rootObjects)
            {
                hierarchy.Add(BuildHierarchy(go, 0, 3)); // Max depth of 3 for performance
            }

            return new HierarchyResult
            {
                sceneName = scene.name,
                scenePath = scene.path,
                rootObjects = hierarchy.ToArray()
            };
        }

        [McpTool("unity_get_gameobject", "Get details of a specific GameObject by path", typeof(GetGameObjectArgs))]
        public static object GetGameObject(string argsJson)
        {
            var args = JsonUtility.FromJson<GetGameObjectArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }

            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }

            return BuildDetailedGameObjectInfo(go);
        }

        [McpTool("unity_get_components", "Get all components on a GameObject", typeof(GetGameObjectArgs))]
        public static object GetComponents(string argsJson)
        {
            var args = JsonUtility.FromJson<GetGameObjectArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }

            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }

            var components = go.GetComponents<Component>();
            var componentInfos = new List<ComponentInfo>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                
                componentInfos.Add(new ComponentInfo
                {
                    typeName = comp.GetType().Name,
                    fullTypeName = comp.GetType().FullName,
                    enabled = comp is Behaviour b ? b.enabled : true
                });
            }

            return new ComponentsResult
            {
                gameObjectPath = args.path,
                components = componentInfos.ToArray()
            };
        }

        private static GameObjectInfo BuildHierarchy(GameObject go, int depth, int maxDepth)
        {
            var info = new GameObjectInfo
            {
                name = go.name,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                childCount = go.transform.childCount,
                componentCount = go.GetComponents<Component>().Length
            };

            if (depth < maxDepth && go.transform.childCount > 0)
            {
                var children = new List<GameObjectInfo>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildHierarchy(go.transform.GetChild(i).gameObject, depth + 1, maxDepth));
                }
                info.children = children.ToArray();
            }

            return info;
        }

        private static object BuildDetailedGameObjectInfo(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var componentNames = new List<string>();
            foreach (var c in components)
            {
                if (c != null) componentNames.Add(c.GetType().Name);
            }

            return new DetailedGameObjectInfo
            {
                name = go.name,
                path = GetGameObjectPath(go),
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                isStatic = go.isStatic,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                position = go.transform.position.ToString(),
                rotation = go.transform.rotation.eulerAngles.ToString(),
                scale = go.transform.localScale.ToString(),
                childCount = go.transform.childCount,
                components = componentNames.ToArray()
            };
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var path = new StringBuilder(go.name);
            var parent = go.transform.parent;
            while (parent != null)
            {
                path.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return path.ToString();
        }

        #region Data Types

        [Serializable]
        public class GetGameObjectArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
        }

        [Serializable]
        public class HierarchyResult
        {
            public string sceneName;
            public string scenePath;
            public GameObjectInfo[] rootObjects;
        }

        [Serializable]
        public class GameObjectInfo
        {
            public string name;
            public bool activeSelf;
            public bool activeInHierarchy;
            public string tag;
            public string layer;
            public int childCount;
            public int componentCount;
            public GameObjectInfo[] children;
        }

        [Serializable]
        public class DetailedGameObjectInfo
        {
            public string name;
            public string path;
            public bool activeSelf;
            public bool activeInHierarchy;
            public bool isStatic;
            public string tag;
            public string layer;
            public string position;
            public string rotation;
            public string scale;
            public int childCount;
            public string[] components;
        }

        [Serializable]
        public class ComponentsResult
        {
            public string gameObjectPath;
            public ComponentInfo[] components;
        }

        [Serializable]
        public class ComponentInfo
        {
            public string typeName;
            public string fullTypeName;
            public bool enabled;
        }

        #endregion
    }
}

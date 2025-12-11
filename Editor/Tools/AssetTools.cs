using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for querying Unity project assets.
    /// </summary>
    [McpToolProvider]
    public class AssetTools
    {
        [McpTool("unity_get_assets", "List assets in a folder")]
        public static object GetAssets(string argsJson)
        {
            var args = JsonUtility.FromJson<GetAssetsArgs>(argsJson);
            var folderPath = string.IsNullOrEmpty(args?.folderPath) ? "Assets" : args.folderPath;
            var filter = args?.filter ?? "";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return new { error = $"Invalid folder path: {folderPath}" };
            }

            var guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
            var assets = new List<AssetInfo>();

            // Limit results to prevent huge responses
            var maxResults = 100;
            var count = 0;

            foreach (var guid in guids)
            {
                if (count >= maxResults) break;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                assets.Add(new AssetInfo
                {
                    path = path,
                    name = Path.GetFileName(path),
                    type = type?.Name ?? "Unknown",
                    guid = guid
                });

                count++;
            }

            return new AssetsResult
            {
                folderPath = folderPath,
                filter = filter,
                totalCount = guids.Length,
                returnedCount = assets.Count,
                assets = assets.ToArray()
            };
        }

        [McpTool("unity_get_project_settings", "Get Unity project settings")]
        public static object GetProjectSettings(string argsJson)
        {
            string scriptingBackend = "Unknown";
            string apiCompatibility = "Unknown";

            try
            {
                // Use NamedBuildTarget for Unity 2022+ / Unity 6
                var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                scriptingBackend = PlayerSettings.GetScriptingBackend(buildTarget).ToString();
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(buildTarget).ToString();
            }
            catch
            {
                // Fallback for older Unity versions
                scriptingBackend = "N/A";
                apiCompatibility = "N/A";
            }

            return new ProjectSettingsResult
            {
                productName = Application.productName,
                companyName = Application.companyName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scripting = scriptingBackend,
                apiCompatibility = apiCompatibility
            };
        }

        #region Data Types

        [Serializable]
        public class GetAssetsArgs
        {
            public string folderPath;
            public string filter;
        }

        [Serializable]
        public class AssetsResult
        {
            public string folderPath;
            public string filter;
            public int totalCount;
            public int returnedCount;
            public AssetInfo[] assets;
        }

        [Serializable]
        public class AssetInfo
        {
            public string path;
            public string name;
            public string type;
            public string guid;
        }

        [Serializable]
        public class ProjectSettingsResult
        {
            public string productName;
            public string companyName;
            public string version;
            public string unityVersion;
            public string platform;
            public string scripting;
            public string apiCompatibility;
        }

        #endregion
    }
}

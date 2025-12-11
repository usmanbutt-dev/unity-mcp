using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for searching the Unity project.
    /// </summary>
    [McpToolProvider]
    public class SearchTools
    {
        [McpTool("unity_search_project", "Search for assets, scripts, or content in the project", typeof(SearchProjectArgs))]
        public static object SearchProject(string argsJson)
        {
            var args = JsonUtility.FromJson<SearchProjectArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.query))
            {
                return new { error = "query parameter is required" };
            }
            
            var results = new List<SearchResult>();
            int maxResults = args.maxResults > 0 ? args.maxResults : 50;
            string searchPath = string.IsNullOrEmpty(args.folder) ? "Assets" : args.folder;
            
            // Determine search type
            string searchType = string.IsNullOrEmpty(args.type) ? "name" : args.type.ToLower();
            
            switch (searchType)
            {
                case "name":
                    SearchByName(args.query, searchPath, args.assetType, results, maxResults);
                    break;
                case "content":
                    SearchByContent(args.query, searchPath, results, maxResults, args.caseSensitive);
                    break;
                case "reference":
                    SearchByReference(args.query, results, maxResults);
                    break;
                default:
                    return new { error = $"Unknown search type: {searchType}. Valid types: name, content, reference" };
            }
            
            return new SearchProjectResult
            {
                query = args.query,
                searchType = searchType,
                folder = searchPath,
                resultCount = results.Count,
                results = results.ToArray()
            };
        }

        private static void SearchByName(string query, string folder, string assetType, List<SearchResult> results, int maxResults)
        {
            // Build filter string
            string filter = query;
            if (!string.IsNullOrEmpty(assetType))
            {
                filter = $"t:{assetType} {query}";
            }
            
            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            
            foreach (var guid in guids)
            {
                if (results.Count >= maxResults) break;
                
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                
                results.Add(new SearchResult
                {
                    path = path,
                    name = Path.GetFileName(path),
                    type = type?.Name ?? "Unknown",
                    guid = guid,
                    matchType = "name"
                });
            }
        }

        private static void SearchByContent(string query, string folder, List<SearchResult> results, int maxResults, bool caseSensitive)
        {
            // Get project root path
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string fullFolderPath = Path.Combine(projectPath, folder);
            
            if (!Directory.Exists(fullFolderPath))
            {
                return;
            }
            
            // Search in text-based files
            string[] extensions = { "*.cs", "*.shader", "*.cginc", "*.hlsl", "*.json", "*.txt", "*.xml", "*.asmdef" };
            
            foreach (var ext in extensions)
            {
                if (results.Count >= maxResults) break;
                
                var files = Directory.GetFiles(fullFolderPath, ext, SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    if (results.Count >= maxResults) break;
                    
                    try
                    {
                        string content = File.ReadAllText(file);
                        
                        bool matches = caseSensitive 
                            ? content.Contains(query) 
                            : content.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                        
                        if (matches)
                        {
                            // Convert to Unity path
                            string relativePath = file.Replace(projectPath + Path.DirectorySeparatorChar, "").Replace('\\', '/');
                            string guid = AssetDatabase.AssetPathToGUID(relativePath);
                            
                            // Find matching lines
                            var matchingLines = FindMatchingLines(content, query, caseSensitive, 3);
                            
                            results.Add(new SearchResult
                            {
                                path = relativePath,
                                name = Path.GetFileName(file),
                                type = Path.GetExtension(file).TrimStart('.').ToUpper(),
                                guid = guid,
                                matchType = "content",
                                matchContext = matchingLines.Count > 0 ? matchingLines[0] : null,
                                lineNumber = matchingLines.Count > 0 ? GetLineNumber(content, query, caseSensitive) : 0
                            });
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }
        }

        private static void SearchByReference(string query, List<SearchResult> results, int maxResults)
        {
            // Find the asset being referenced
            string[] guids = AssetDatabase.FindAssets(query);
            if (guids.Length == 0)
            {
                return;
            }
            
            string targetGuid = guids[0];
            string targetPath = AssetDatabase.GUIDToAssetPath(targetGuid);
            
            // Search for references to this asset
            string[] allAssets = AssetDatabase.GetAllAssetPaths();
            
            foreach (var assetPath in allAssets)
            {
                if (results.Count >= maxResults) break;
                
                // Skip the target itself
                if (assetPath == targetPath) continue;
                
                // Check dependencies
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                
                foreach (var dep in dependencies)
                {
                    if (dep == targetPath)
                    {
                        string guid = AssetDatabase.AssetPathToGUID(assetPath);
                        var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                        
                        results.Add(new SearchResult
                        {
                            path = assetPath,
                            name = Path.GetFileName(assetPath),
                            type = type?.Name ?? "Unknown",
                            guid = guid,
                            matchType = "reference",
                            matchContext = $"References: {targetPath}"
                        });
                        break;
                    }
                }
            }
        }

        private static List<string> FindMatchingLines(string content, string query, bool caseSensitive, int maxLines)
        {
            var lines = new List<string>();
            var allLines = content.Split('\n');
            
            for (int i = 0; i < allLines.Length && lines.Count < maxLines; i++)
            {
                bool matches = caseSensitive 
                    ? allLines[i].Contains(query) 
                    : allLines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                
                if (matches)
                {
                    lines.Add($"L{i + 1}: {allLines[i].Trim()}");
                }
            }
            
            return lines;
        }

        private static int GetLineNumber(string content, string query, bool caseSensitive)
        {
            var lines = content.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                bool matches = caseSensitive 
                    ? lines[i].Contains(query) 
                    : lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                
                if (matches)
                {
                    return i + 1;
                }
            }
            
            return 0;
        }

        #region Data Types

        [Serializable]
        public class SearchProjectArgs
        {
            [McpParam("Search query", Required = true)] public string query;
            [McpParam("Search type", EnumValues = new[] { "name", "content", "reference" })] public string type;
            [McpParam("Folder to search (default 'Assets')")] public string folder;
            [McpParam("Filter by asset type (e.g., 'Script', 'Prefab', 'Scene')")] public string assetType;
            [McpParam("Case sensitive search (for content search)")] public bool caseSensitive;
            [McpParam("Maximum results (default 50)")] public int maxResults;
        }

        [Serializable]
        public class SearchResult
        {
            public string path;
            public string name;
            public string type;
            public string guid;
            public string matchType;
            public string matchContext;
            public int lineNumber;
        }

        [Serializable]
        public class SearchProjectResult
        {
            public string query;
            public string searchType;
            public string folder;
            public int resultCount;
            public SearchResult[] results;
        }

        #endregion
    }
}

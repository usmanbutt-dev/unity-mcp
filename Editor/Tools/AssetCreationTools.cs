using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for creating and managing assets.
    /// </summary>
    [McpToolProvider]
    public class AssetCreationTools
    {
        [McpTool("unity_create_folder", "Create a new folder in the project", typeof(CreateFolderArgs))]
        public static object CreateFolder(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateFolderArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required (e.g., 'Assets/NewFolder')" };
            
            // Ensure path starts with Assets
            string path = args.path;
            if (!path.StartsWith("Assets"))
                path = "Assets/" + path;
            
            // Check if already exists
            if (AssetDatabase.IsValidFolder(path))
                return new { error = $"Folder already exists: {path}", exists = true };
            
            // Create folder hierarchy
            string[] parts = path.Split('/');
            string currentPath = parts[0]; // "Assets"
            
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
            
            AssetDatabase.Refresh();
            
            return new CreateFolderResult
            {
                success = true,
                path = path,
                guid = AssetDatabase.AssetPathToGUID(path)
            };
        }

        [McpTool("unity_create_material", "Create a new material asset", typeof(CreateMaterialArgs))]
        public static object CreateMaterial(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateMaterialArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required (e.g., 'Assets/Materials/NewMaterial.mat')" };
            
            string path = args.path;
            if (!path.StartsWith("Assets"))
                path = "Assets/" + path;
            if (!path.EndsWith(".mat"))
                path += ".mat";
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
            {
                // Create folder
                CreateFolder(JsonUtility.ToJson(new CreateFolderArgs { path = directory }));
            }
            
            // Find shader
            Shader shader = Shader.Find(string.IsNullOrEmpty(args.shaderName) ? "Standard" : args.shaderName);
            if (shader == null)
                return new { error = $"Shader not found: {args.shaderName}" };
            
            Material material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            
            // Set color if provided
            if (args.color != null)
            {
                material.color = new Color(args.color.r, args.color.g, args.color.b, args.color.a);
            }
            
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            return new CreateMaterialResult
            {
                success = true,
                path = path,
                guid = AssetDatabase.AssetPathToGUID(path),
                shaderName = shader.name
            };
        }

        [McpTool("unity_create_script", "Create a new C# script", typeof(CreateScriptArgs))]
        public static object CreateScript(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateScriptArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required (e.g., 'Assets/Scripts/NewScript.cs')" };
            
            string path = args.path;
            if (!path.StartsWith("Assets"))
                path = "Assets/" + path;
            if (!path.EndsWith(".cs"))
                path += ".cs";
            
            string className = Path.GetFileNameWithoutExtension(path);
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
            {
                CreateFolder(JsonUtility.ToJson(new CreateFolderArgs { path = directory }));
            }
            
            // Determine script type and generate content
            string scriptType = string.IsNullOrEmpty(args.scriptType) ? "monobehaviour" : args.scriptType.ToLower();
            string namespaceName = string.IsNullOrEmpty(args.namespaceName) ? null : args.namespaceName;
            
            string content = GenerateScript(className, scriptType, namespaceName);
            
            // Write file
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            File.WriteAllText(fullPath, content);
            
            AssetDatabase.Refresh();
            
            return new CreateScriptResult
            {
                success = true,
                path = path,
                guid = AssetDatabase.AssetPathToGUID(path),
                className = className,
                scriptType = scriptType
            };
        }

        [McpTool("unity_move_asset", "Move or rename an asset", typeof(MoveAssetArgs))]
        public static object MoveAsset(string argsJson)
        {
            var args = JsonUtility.FromJson<MoveAssetArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.sourcePath))
                return new { error = "sourcePath parameter is required" };
            if (string.IsNullOrEmpty(args?.destinationPath))
                return new { error = "destinationPath parameter is required" };
            
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("/Assets", ""), args.sourcePath)) &&
                !AssetDatabase.IsValidFolder(args.sourcePath))
            {
                return new { error = $"Source not found: {args.sourcePath}" };
            }
            
            // Ensure destination directory exists
            string destDir = Path.GetDirectoryName(args.destinationPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(destDir))
            {
                CreateFolder(JsonUtility.ToJson(new CreateFolderArgs { path = destDir }));
            }
            
            string result = AssetDatabase.MoveAsset(args.sourcePath, args.destinationPath);
            
            if (!string.IsNullOrEmpty(result))
            {
                return new { error = result };
            }
            
            AssetDatabase.Refresh();
            
            return new MoveAssetResult
            {
                success = true,
                sourcePath = args.sourcePath,
                destinationPath = args.destinationPath,
                newGuid = AssetDatabase.AssetPathToGUID(args.destinationPath)
            };
        }

        [McpTool("unity_duplicate_asset", "Duplicate an asset", typeof(DuplicateAssetArgs))]
        public static object DuplicateAsset(string argsJson)
        {
            var args = JsonUtility.FromJson<DuplicateAssetArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.sourcePath))
                return new { error = "sourcePath parameter is required" };
            
            string destPath = args.destinationPath;
            if (string.IsNullOrEmpty(destPath))
            {
                // Generate automatic name
                string dir = Path.GetDirectoryName(args.sourcePath).Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(args.sourcePath);
                string ext = Path.GetExtension(args.sourcePath);
                destPath = $"{dir}/{name}_Copy{ext}";
            }
            
            bool success = AssetDatabase.CopyAsset(args.sourcePath, destPath);
            
            if (!success)
            {
                return new { error = $"Failed to duplicate: {args.sourcePath}" };
            }
            
            AssetDatabase.Refresh();
            
            return new DuplicateAssetResult
            {
                success = true,
                sourcePath = args.sourcePath,
                destinationPath = destPath,
                newGuid = AssetDatabase.AssetPathToGUID(destPath)
            };
        }

        private static string GenerateScript(string className, string scriptType, string namespaceName)
        {
            string template;
            
            switch (scriptType)
            {
                case "monobehaviour":
                    template = $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start()
    {{
        
    }}

    void Update()
    {{
        
    }}
}}";
                    break;
                    
                case "scriptableobject":
                    template = $@"using UnityEngine;

[CreateAssetMenu(fileName = ""{className}"", menuName = ""ScriptableObjects/{className}"")]
public class {className} : ScriptableObject
{{
    
}}";
                    break;
                    
                case "editor":
                    template = $@"using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(REPLACE_WITH_TARGET_TYPE))]
public class {className} : Editor
{{
    public override void OnInspectorGUI()
    {{
        base.OnInspectorGUI();
    }}
}}";
                    break;
                    
                case "static":
                    template = $@"using UnityEngine;

public static class {className}
{{
    
}}";
                    break;
                    
                default:
                    template = $@"using UnityEngine;

public class {className}
{{
    
}}";
                    break;
            }
            
            if (!string.IsNullOrEmpty(namespaceName))
            {
                // Wrap in namespace
                var lines = template.Split('\n');
                var indented = new System.Text.StringBuilder();
                indented.AppendLine($"namespace {namespaceName}");
                indented.AppendLine("{");
                foreach (var line in lines)
                {
                    indented.AppendLine("    " + line);
                }
                indented.AppendLine("}");
                template = indented.ToString();
            }
            
            return template;
        }

        #region Data Types

        [Serializable]
        public class CreateFolderArgs
        {
            [McpParam("Folder path (e.g., 'Assets/NewFolder')", Required = true)] public string path;
        }

        [Serializable]
        public class CreateFolderResult
        {
            public bool success;
            public string path;
            public string guid;
        }

        [Serializable]
        public class ColorValue
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        [Serializable]
        public class CreateMaterialArgs
        {
            [McpParam("Material path (e.g., 'Assets/Materials/New.mat')", Required = true)] public string path;
            [McpParam("Shader name (default 'Standard')")] public string shaderName;
            [McpParam("Initial color {r, g, b, a}")] public ColorValue color;
        }

        [Serializable]
        public class CreateMaterialResult
        {
            public bool success;
            public string path;
            public string guid;
            public string shaderName;
        }

        [Serializable]
        public class CreateScriptArgs
        {
            [McpParam("Script path (e.g., 'Assets/Scripts/New.cs')", Required = true)] public string path;
            [McpParam("Script type", EnumValues = new[] { "monobehaviour", "scriptableobject", "editor", "static", "class" })] public string scriptType;
            [McpParam("Namespace name (optional)")] public string namespaceName;
        }

        [Serializable]
        public class CreateScriptResult
        {
            public bool success;
            public string path;
            public string guid;
            public string className;
            public string scriptType;
        }

        [Serializable]
        public class MoveAssetArgs
        {
            [McpParam("Source asset path", Required = true)] public string sourcePath;
            [McpParam("Destination path", Required = true)] public string destinationPath;
        }

        [Serializable]
        public class MoveAssetResult
        {
            public bool success;
            public string sourcePath;
            public string destinationPath;
            public string newGuid;
        }

        [Serializable]
        public class DuplicateAssetArgs
        {
            [McpParam("Source asset path", Required = true)] public string sourcePath;
            [McpParam("Destination path (auto-generated if empty)")] public string destinationPath;
        }

        [Serializable]
        public class DuplicateAssetResult
        {
            public bool success;
            public string sourcePath;
            public string destinationPath;
            public string newGuid;
        }

        #endregion
    }
}

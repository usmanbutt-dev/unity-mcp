using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for working with materials and shaders.
    /// </summary>
    [McpToolProvider]
    public class MaterialTools
    {
        [McpTool("unity_get_material_info", "Get information about a material on a GameObject", typeof(GetMaterialInfoArgs))]
        public static object GetMaterialInfo(string argsJson)
        {
            var args = JsonUtility.FromJson<GetMaterialInfoArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return new { error = $"No Renderer component on: {args.path}" };
            
            int materialIndex = args.materialIndex >= 0 ? args.materialIndex : 0;
            if (materialIndex >= renderer.sharedMaterials.Length)
                return new { error = $"Material index {materialIndex} out of range. Object has {renderer.sharedMaterials.Length} materials." };
            
            var material = renderer.sharedMaterials[materialIndex];
            if (material == null)
                return new { error = "Material is null" };
            
            // Get shader properties
            var properties = new List<MaterialPropertyInfo>();
            var shader = material.shader;
            int propertyCount = shader.GetPropertyCount();
            
            for (int i = 0; i < propertyCount; i++)
            {
                var propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);
                
                var propInfo = new MaterialPropertyInfo
                {
                    name = propName,
                    type = propType.ToString()
                };
                
                // Get current value
                switch (propType)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        var color = material.GetColor(propName);
                        propInfo.value = $"({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        propInfo.value = material.GetFloat(propName).ToString("F3");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        var vec = material.GetVector(propName);
                        propInfo.value = $"({vec.x:F2}, {vec.y:F2}, {vec.z:F2}, {vec.w:F2})";
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var tex = material.GetTexture(propName);
                        propInfo.value = tex != null ? tex.name : "None";
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:
                        propInfo.value = material.GetInt(propName).ToString();
                        break;
                }
                
                properties.Add(propInfo);
            }
            
            return new GetMaterialInfoResult
            {
                path = args.path,
                materialIndex = materialIndex,
                materialName = material.name,
                shaderName = shader.name,
                renderQueue = material.renderQueue,
                propertyCount = properties.Count,
                properties = properties.ToArray()
            };
        }

        [McpTool("unity_set_material_property", "Set a property on a material", typeof(SetMaterialPropertyArgs))]
        public static object SetMaterialProperty(string argsJson)
        {
            var args = JsonUtility.FromJson<SetMaterialPropertyArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (string.IsNullOrEmpty(args?.propertyName))
                return new { error = "propertyName parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return new { error = $"No Renderer component on: {args.path}" };
            
            int materialIndex = args.materialIndex >= 0 ? args.materialIndex : 0;
            if (materialIndex >= renderer.sharedMaterials.Length)
                return new { error = $"Material index {materialIndex} out of range" };
            
            // Get material (use instance to allow runtime changes)
            Material material = Application.isPlaying ? renderer.materials[materialIndex] : renderer.sharedMaterials[materialIndex];
            
            if (material == null)
                return new { error = "Material is null" };
            
            Undo.RecordObject(material, $"Set {args.propertyName}");
            
            string propType = string.IsNullOrEmpty(args.propertyType) ? "color" : args.propertyType.ToLower();
            
            try
            {
                switch (propType)
                {
                    case "color":
                        if (args.color != null)
                        {
                            material.SetColor(args.propertyName, new Color(args.color.r, args.color.g, args.color.b, args.color.a));
                        }
                        break;
                    case "float":
                        material.SetFloat(args.propertyName, args.floatValue);
                        break;
                    case "int":
                        material.SetInt(args.propertyName, args.intValue);
                        break;
                    case "vector":
                        if (args.vector != null)
                        {
                            material.SetVector(args.propertyName, new Vector4(args.vector.x, args.vector.y, args.vector.z, args.vector.w));
                        }
                        break;
                    default:
                        return new { error = $"Unknown property type: {propType}. Use: color, float, int, vector" };
                }
                
                EditorUtility.SetDirty(material);
                
                return new SetMaterialPropertyResult
                {
                    success = true,
                    path = args.path,
                    materialIndex = materialIndex,
                    propertyName = args.propertyName,
                    propertyType = propType
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to set property: {ex.Message}" };
            }
        }

        [McpTool("unity_set_material", "Assign a material to a renderer", typeof(SetMaterialArgs))]
        public static object SetMaterial(string argsJson)
        {
            var args = JsonUtility.FromJson<SetMaterialArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (string.IsNullOrEmpty(args?.materialPath))
                return new { error = "materialPath parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return new { error = $"No Renderer component on: {args.path}" };
            
            var material = AssetDatabase.LoadAssetAtPath<Material>(args.materialPath);
            if (material == null)
                return new { error = $"Material not found: {args.materialPath}" };
            
            int materialIndex = args.materialIndex >= 0 ? args.materialIndex : 0;
            
            Undo.RecordObject(renderer, "Set Material");
            
            var materials = renderer.sharedMaterials;
            if (materialIndex >= materials.Length)
            {
                // Expand materials array
                var newMaterials = new Material[materialIndex + 1];
                for (int i = 0; i < materials.Length; i++)
                    newMaterials[i] = materials[i];
                newMaterials[materialIndex] = material;
                renderer.sharedMaterials = newMaterials;
            }
            else
            {
                materials[materialIndex] = material;
                renderer.sharedMaterials = materials;
            }
            
            return new SetMaterialResult
            {
                success = true,
                path = args.path,
                materialPath = args.materialPath,
                materialIndex = materialIndex
            };
        }

        #region Data Types

        [Serializable]
        public class GetMaterialInfoArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Material index (default 0)")] public int materialIndex;
        }

        [Serializable]
        public class MaterialPropertyInfo
        {
            public string name;
            public string type;
            public string value;
        }

        [Serializable]
        public class GetMaterialInfoResult
        {
            public string path;
            public int materialIndex;
            public string materialName;
            public string shaderName;
            public int renderQueue;
            public int propertyCount;
            public MaterialPropertyInfo[] properties;
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
        public class Vec4
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        public class SetMaterialPropertyArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Property name (e.g., '_Color', '_Metallic')", Required = true)] public string propertyName;
            [McpParam("Property type", EnumValues = new[] { "color", "float", "int", "vector" })] public string propertyType;
            [McpParam("Material index (default 0)")] public int materialIndex;
            [McpParam("Color value {r, g, b, a}")] public ColorValue color;
            [McpParam("Float value")] public float floatValue;
            [McpParam("Int value")] public int intValue;
            [McpParam("Vector value {x, y, z, w}")] public Vec4 vector;
        }

        [Serializable]
        public class SetMaterialPropertyResult
        {
            public bool success;
            public string path;
            public int materialIndex;
            public string propertyName;
            public string propertyType;
        }

        [Serializable]
        public class SetMaterialArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Path to the material asset", Required = true)] public string materialPath;
            [McpParam("Material index to replace (default 0)")] public int materialIndex;
        }

        [Serializable]
        public class SetMaterialResult
        {
            public bool success;
            public string path;
            public string materialPath;
            public int materialIndex;
        }

        #endregion
    }
}

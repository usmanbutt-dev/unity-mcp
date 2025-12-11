using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// Attribute to mark a class as containing MCP tools.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class McpToolProviderAttribute : Attribute { }

    /// <summary>
    /// Attribute to mark a method as an MCP tool.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public Type ArgsType { get; }

        public McpToolAttribute(string name, string description, Type argsType = null)
        {
            Name = name;
            Description = description;
            ArgsType = argsType;
        }
    }

    /// <summary>
    /// Attribute to provide additional metadata for tool parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class McpParamAttribute : Attribute
    {
        public string Description { get; set; }
        public bool Required { get; set; }
        public string[] EnumValues { get; set; }

        public McpParamAttribute(string description = null)
        {
            Description = description;
        }
    }

    /// <summary>
    /// Registry for MCP tools. Discovers and manages tool methods.
    /// </summary>
    public static class ToolRegistry
    {
        private static readonly Dictionary<string, ToolInfo> _tools = new Dictionary<string, ToolInfo>();
        private static bool _initialized;

        private class ToolInfo
        {
            public string Name;
            public string Description;
            public MethodInfo Method;
            public object Instance;
            public Type ArgsType;
        }

        /// <summary>
        /// Initialize the tool registry by scanning for McpTool attributes.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _tools.Clear();

            // Find all types with McpToolProvider attribute
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.GetCustomAttribute<McpToolProviderAttribute>() == null)
                            continue;

                        // Create instance for non-static methods
                        object instance = null;
                        if (!type.IsAbstract && !type.IsStatic())
                        {
                            try
                            {
                                instance = Activator.CreateInstance(type);
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        // Find all methods with McpTool attribute
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                        {
                            var attr = method.GetCustomAttribute<McpToolAttribute>();
                            if (attr == null) continue;

                            _tools[attr.Name] = new ToolInfo
                            {
                                Name = attr.Name,
                                Description = attr.Description,
                                Method = method,
                                Instance = method.IsStatic ? null : instance,
                                ArgsType = attr.ArgsType
                            };

                            Debug.Log($"[MCP] Registered tool: {attr.Name}");
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that can't be scanned
                }
            }

            _initialized = true;
            Debug.Log($"[MCP] Tool registry initialized with {_tools.Count} tools.");
        }

        /// <summary>
        /// Get all registered tool definitions for MCP.
        /// </summary>
        public static McpToolDefinition[] GetToolDefinitions()
        {
            if (!_initialized) Initialize();

            var definitions = new List<McpToolDefinition>();

            foreach (var tool in _tools.Values)
            {
                definitions.Add(new McpToolDefinition
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = SchemaGenerator.GenerateSchema(tool.ArgsType)
                });
            }

            return definitions.ToArray();
        }

        /// <summary>
        /// Execute a tool by name with the given arguments.
        /// </summary>
        public static object ExecuteTool(string name, string argumentsJson)
        {
            if (!_initialized) Initialize();

            if (!_tools.TryGetValue(name, out var tool))
            {
                throw new ArgumentException($"Unknown tool: {name}");
            }

            try
            {
                var parameters = tool.Method.GetParameters();
                object[] args;

                if (parameters.Length == 0)
                {
                    args = new object[0];
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    args = new object[] { argumentsJson };
                }
                else
                {
                    args = new object[] { argumentsJson };
                }

                var result = tool.Method.Invoke(tool.Instance, args);
                return result ?? new { success = true };
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
    }

    /// <summary>
    /// Generates JSON Schema from C# types for MCP tool definitions.
    /// </summary>
    public static class SchemaGenerator
    {
        /// <summary>
        /// Generate an MCP input schema from a C# type.
        /// </summary>
        public static McpInputSchema GenerateSchema(Type argsType)
        {
            if (argsType == null)
            {
                return new McpInputSchema
                {
                    type = "object",
                    properties = "{}",
                    required = new string[0]
                };
            }

            var properties = new StringBuilder();
            var required = new List<string>();
            bool first = true;

            properties.Append("{");

            foreach (var field in argsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!first) properties.Append(",");
                first = false;

                var paramAttr = field.GetCustomAttribute<McpParamAttribute>();
                var fieldSchema = GenerateFieldSchema(field.FieldType, field.Name, paramAttr);
                
                properties.Append($"\"{field.Name}\":{fieldSchema}");

                if (paramAttr?.Required == true)
                {
                    required.Add(field.Name);
                }
            }

            properties.Append("}");

            return new McpInputSchema
            {
                type = "object",
                properties = properties.ToString(),
                required = required.ToArray()
            };
        }

        private static string GenerateFieldSchema(Type fieldType, string fieldName, McpParamAttribute paramAttr)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

            // Determine JSON Schema type
            string jsonType = GetJsonType(underlyingType);
            sb.Append($"\"type\":\"{jsonType}\"");

            // Add description if available
            if (!string.IsNullOrEmpty(paramAttr?.Description))
            {
                sb.Append($",\"description\":\"{EscapeJson(paramAttr.Description)}\"");
            }

            // Add enum values if specified
            if (paramAttr?.EnumValues != null && paramAttr.EnumValues.Length > 0)
            {
                sb.Append(",\"enum\":[");
                for (int i = 0; i < paramAttr.EnumValues.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{paramAttr.EnumValues[i]}\"");
                }
                sb.Append("]");
            }

            // Handle nested objects (Vec3, etc.)
            if (jsonType == "object" && !IsSimpleType(underlyingType))
            {
                var nestedProps = GenerateNestedProperties(underlyingType);
                if (!string.IsNullOrEmpty(nestedProps))
                {
                    sb.Append($",\"properties\":{nestedProps}");
                }
            }

            // Handle arrays
            if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                sb.Append($",\"items\":{{\"type\":\"{GetJsonType(elementType)}\"}}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string GenerateNestedProperties(Type type)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!first) sb.Append(",");
                first = false;

                string jsonType = GetJsonType(field.FieldType);
                sb.Append($"\"{field.Name}\":{{\"type\":\"{jsonType}\"}}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string GetJsonType(Type type)
        {
            if (type == typeof(string))
                return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                return "integer";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            if (type.IsArray)
                return "array";
            if (type.IsClass || type.IsValueType && !type.IsPrimitive)
                return "object";
            
            return "string"; // Default fallback
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        private static string EscapeJson(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    // Extension method to check if type is static
    internal static class TypeExtensions
    {
        public static bool IsStatic(this Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }
    }
}

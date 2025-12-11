using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// Handles JSON-RPC 2.0 protocol for MCP.
    /// </summary>
    public static class JsonRpcHandler
    {
        /// <summary>
        /// Process a JSON-RPC request and return a response.
        /// </summary>
        public static string ProcessRequest(string requestJson)
        {
            try
            {
                // Parse manually since JsonUtility can't handle nested objects well
                string method = ExtractStringValue(requestJson, "method");
                string id = ExtractStringValue(requestJson, "id");
                string paramsJson = ExtractObjectValue(requestJson, "params");

                if (string.IsNullOrEmpty(method))
                {
                    return CreateErrorResponse(id, -32600, "Invalid Request: method is required");
                }

                // Route to appropriate handler
                object result;
                switch (method)
                {
                    case "initialize":
                        result = HandleInitialize(paramsJson);
                        break;
                    case "tools/list":
                        result = HandleToolsList();
                        break;
                    case "tools/call":
                        result = HandleToolsCall(paramsJson);
                        break;
                    case "resources/list":
                        result = ResourceHandler.HandleResourcesList(paramsJson);
                        break;
                    case "resources/read":
                        result = ResourceHandler.HandleResourcesRead(paramsJson);
                        break;
                    case "ping":
                        result = new { pong = true };
                        break;
                    default:
                        return CreateErrorResponse(id, -32601, $"Method not found: {method}");
                }

                return CreateSuccessResponse(id, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] JSON-RPC Error: {ex.Message}");
                return CreateErrorResponse(null, -32700, "Parse error: " + ex.Message);
            }
        }

        private static object HandleInitialize(string paramsJson)
        {
            return new McpInitializeResult
            {
                protocolVersion = "2024-11-05",
                capabilities = new McpCapabilities
                {
                    tools = new McpToolsCapability { listChanged = false },
                    resources = new McpResourcesCapability { subscribe = false, listChanged = false }
                },
                serverInfo = new McpServerInfo
                {
                    name = "unity-mcp",
                    version = "1.1.0"
                }
            };
        }

        private static object HandleToolsList()
        {
            return new McpToolsListResult
            {
                tools = ToolRegistry.GetToolDefinitions()
            };
        }

        private static object HandleToolsCall(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson))
            {
                throw new ArgumentException("Tool call requires parameters");
            }

            // Extract tool name and arguments from params object
            string toolName = ExtractStringValue(paramsJson, "name");
            string argumentsJson = ExtractObjectValue(paramsJson, "arguments");

            if (string.IsNullOrEmpty(toolName))
            {
                throw new ArgumentException("Tool name is required");
            }

            // Pass empty JSON object if no arguments provided
            if (string.IsNullOrEmpty(argumentsJson))
            {
                argumentsJson = "{}";
            }

            var result = ToolRegistry.ExecuteTool(toolName, argumentsJson);

            return new McpToolResult
            {
                content = new[]
                {
                    new McpContent
                    {
                        type = "text",
                        text = JsonUtility.ToJson(result, true)
                    }
                }
            };
        }

        public static string CreateSuccessResponse(string id, object result)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",");
            if (!string.IsNullOrEmpty(id))
            {
                sb.Append($"\"id\":\"{id}\",");
            }
            else
            {
                sb.Append("\"id\":null,");
            }
            sb.Append("\"result\":");
            sb.Append(JsonUtility.ToJson(result));
            sb.Append("}");
            return sb.ToString();
        }

        public static string CreateErrorResponse(string id, int code, string message)
        {
            var sb = new StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\",");
            if (!string.IsNullOrEmpty(id))
            {
                sb.Append($"\"id\":\"{id}\",");
            }
            else
            {
                sb.Append("\"id\":null,");
            }
            sb.Append($"\"error\":{{\"code\":{code},\"message\":\"{EscapeJson(message)}\"}}}}");
            return sb.ToString();
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// Extract a string value from a JSON object.
        /// </summary>
        private static string ExtractStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // Pattern: "key":"value" or "key": "value"
            var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(json, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Try numeric/null value: "key":123 or "key":null
            pattern = $"\"{key}\"\\s*:\\s*([^,}}\\]]+)";
            match = Regex.Match(json, pattern);
            if (match.Success)
            {
                var value = match.Groups[1].Value.Trim();
                if (value == "null") return null;
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    return value.Substring(1, value.Length - 2);
                }
                return value;
            }

            return null;
        }

        /// <summary>
        /// Extract a nested object value from a JSON object.
        /// </summary>
        private static string ExtractObjectValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // Find the key
            var keyPattern = $"\"{key}\"\\s*:";
            var keyMatch = Regex.Match(json, keyPattern);
            if (!keyMatch.Success) return null;

            int startIndex = keyMatch.Index + keyMatch.Length;

            // Skip whitespace
            while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= json.Length) return null;

            char startChar = json[startIndex];

            // If it's an object or array, find the matching closing bracket
            if (startChar == '{' || startChar == '[')
            {
                char endChar = startChar == '{' ? '}' : ']';
                int depth = 1;
                int endIndex = startIndex + 1;
                bool inString = false;

                while (endIndex < json.Length && depth > 0)
                {
                    char c = json[endIndex];

                    if (inString)
                    {
                        if (c == '"' && json[endIndex - 1] != '\\')
                        {
                            inString = false;
                        }
                    }
                    else
                    {
                        if (c == '"')
                        {
                            inString = true;
                        }
                        else if (c == startChar)
                        {
                            depth++;
                        }
                        else if (c == endChar)
                        {
                            depth--;
                        }
                    }
                    endIndex++;
                }

                return json.Substring(startIndex, endIndex - startIndex);
            }

            // If it's a string, extract it
            if (startChar == '"')
            {
                int endIndex = startIndex + 1;
                while (endIndex < json.Length)
                {
                    if (json[endIndex] == '"' && json[endIndex - 1] != '\\')
                    {
                        break;
                    }
                    endIndex++;
                }
                return json.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            // Otherwise it's a primitive, extract until comma or closing bracket
            int end = startIndex;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
            {
                end++;
            }

            return json.Substring(startIndex, end - startIndex).Trim();
        }
    }

    #region MCP Protocol Types

    [Serializable]
    public class McpInitializeResult
    {
        public string protocolVersion;
        public McpCapabilities capabilities;
        public McpServerInfo serverInfo;
    }

    [Serializable]
    public class McpCapabilities
    {
        public McpToolsCapability tools;
        public McpResourcesCapability resources;
    }

    [Serializable]
    public class McpToolsCapability
    {
        public bool listChanged;
    }

    [Serializable]
    public class McpResourcesCapability
    {
        public bool subscribe;
        public bool listChanged;
    }

    [Serializable]
    public class McpServerInfo
    {
        public string name;
        public string version;
    }

    [Serializable]
    public class McpToolsListResult
    {
        public McpToolDefinition[] tools;
    }

    [Serializable]
    public class McpToolDefinition
    {
        public string name;
        public string description;
        public McpInputSchema inputSchema;
    }

    [Serializable]
    public class McpInputSchema
    {
        public string type = "object";
        public string properties; // JSON string for flexibility
        public string[] required;
    }

    [Serializable]
    public class McpToolResult
    {
        public McpContent[] content;
        public bool isError;
    }

    [Serializable]
    public class McpContent
    {
        public string type;
        public string text;
    }

    #endregion
}

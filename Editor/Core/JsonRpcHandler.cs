using System;
using System.Collections.Generic;
using System.Text;
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
                var request = JsonUtility.FromJson<JsonRpcRequest>(requestJson);

                if (string.IsNullOrEmpty(request.method))
                {
                    return CreateErrorResponse(request.id, -32600, "Invalid Request: method is required");
                }

                // Route to appropriate handler
                object result;
                switch (request.method)
                {
                    case "initialize":
                        result = HandleInitialize(request.@params);
                        break;
                    case "tools/list":
                        result = HandleToolsList();
                        break;
                    case "tools/call":
                        result = HandleToolsCall(request.@params);
                        break;
                    case "ping":
                        result = new { pong = true };
                        break;
                    default:
                        return CreateErrorResponse(request.id, -32601, $"Method not found: {request.method}");
                }

                return CreateSuccessResponse(request.id, result);
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
                    tools = new McpToolsCapability { listChanged = false }
                },
                serverInfo = new McpServerInfo
                {
                    name = "unity-mcp",
                    version = "1.0.0"
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

            var callParams = JsonUtility.FromJson<ToolCallParams>(paramsJson);
            
            if (string.IsNullOrEmpty(callParams.name))
            {
                throw new ArgumentException("Tool name is required");
            }

            var result = ToolRegistry.ExecuteTool(callParams.name, callParams.arguments);
            
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
            var response = new JsonRpcResponse
            {
                jsonrpc = "2.0",
                id = id,
                result = JsonUtility.ToJson(result)
            };
            return JsonUtility.ToJson(response);
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

        #region JSON Data Structures

        [Serializable]
        private class JsonRpcRequest
        {
            public string jsonrpc;
            public string method;
            public string @params;
            public string id;
        }

        [Serializable]
        private class JsonRpcResponse
        {
            public string jsonrpc;
            public string id;
            public string result;
        }

        [Serializable]
        private class ToolCallParams
        {
            public string name;
            public string arguments;
        }

        #endregion
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
    }

    [Serializable]
    public class McpToolsCapability
    {
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

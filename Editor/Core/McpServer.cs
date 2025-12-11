using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// HTTP server that handles MCP JSON-RPC requests.
    /// Runs on a background thread and dispatches to the main thread for Unity API calls.
    /// </summary>
    [InitializeOnLoad]
    public class McpServer
    {
        private static McpServer _instance;
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        public static McpServer Instance => _instance ??= new McpServer();

        public bool IsRunning => _isRunning;
        public int Port { get; private set; } = 3000;

        public static event Action<bool> OnServerStateChanged;

        static McpServer()
        {
            EditorApplication.update += ProcessMainThreadQueue;
        }

        /// <summary>
        /// Starts the MCP server on the specified port.
        /// </summary>
        public void Start(int port = 3000)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[MCP] Server is already running.");
                return;
            }

            Port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();

                _isRunning = true;
                _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                _listenerThread.Start();

                Debug.Log($"[MCP] Server started on http://localhost:{port}/");
                OnServerStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Failed to start server: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// Stops the MCP server.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;

            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listenerThread?.Join(1000);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Error stopping server: {ex.Message}");
            }

            Debug.Log("[MCP] Server stopped.");
            OnServerStateChanged?.Invoke(false);
        }

        private void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Expected when stopping the listener
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogError($"[MCP] Listener error: {ex.Message}");
                    }
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Add CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                // Handle preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                // Only accept POST for JSON-RPC
                if (request.HttpMethod != "POST")
                {
                    SendError(response, 405, "Method Not Allowed");
                    return;
                }

                // Read request body
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                // Process JSON-RPC on main thread and wait for result
                string responseBody = null;
                var waitHandle = new ManualResetEvent(false);

                EnqueueMainThread(() =>
                {
                    try
                    {
                        responseBody = JsonRpcHandler.ProcessRequest(requestBody);
                    }
                    catch (Exception ex)
                    {
                        responseBody = JsonRpcHandler.CreateErrorResponse(null, -32603, ex.Message);
                    }
                    finally
                    {
                        waitHandle.Set();
                    }
                });

                // Wait for main thread processing (timeout after 30 seconds)
                if (!waitHandle.WaitOne(30000))
                {
                    responseBody = JsonRpcHandler.CreateErrorResponse(null, -32603, "Request timeout");
                }

                // Send response
                response.ContentType = "application/json";
                response.StatusCode = 200;
                var buffer = Encoding.UTF8.GetBytes(responseBody);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Request handling error: {ex.Message}");
                SendError(response, 500, "Internal Server Error");
            }
            finally
            {
                response.Close();
            }
        }

        private void SendError(HttpListenerResponse response, int statusCode, string message)
        {
            response.StatusCode = statusCode;
            var buffer = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_mainThreadQueue)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        private static void ProcessMainThreadQueue()
        {
            if (_instance == null) return;

            lock (_instance._mainThreadQueue)
            {
                while (_instance._mainThreadQueue.Count > 0)
                {
                    var action = _instance._mainThreadQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MCP] Main thread action error: {ex.Message}");
                    }
                }
            }
        }
    }
}

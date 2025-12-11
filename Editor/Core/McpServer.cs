using System;
using System.Collections.Concurrent;
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
    /// HTTP server that handles MCP JSON-RPC requests via HTTP POST and pushes events via SSE.
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
        private readonly ConcurrentDictionary<Guid, HttpListenerResponse> _sseClients = new ConcurrentDictionary<Guid, HttpListenerResponse>();

        public static McpServer Instance => _instance ??= new McpServer();

        public bool IsRunning => _isRunning;
        public int Port { get; private set; } = 3000;

        public static event Action<bool> OnServerStateChanged;

        static McpServer()
        {
            EditorApplication.update += ProcessMainThreadQueue;
        }

        [InitializeOnLoadMethod]
        private static void AutoStart()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Instance.IsRunning)
                {
                    int port = EditorPrefs.GetInt("MCP_Port", 3000);
                    Instance.Start(port);
                }
            };
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
        /// Stops the MCP server and closes all connections.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;

            // Close all SSE connections
            foreach (var client in _sseClients)
            {
                try { client.Value.Close(); } catch { }
            }
            _sseClients.Clear();

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

        /// <summary>
        /// Sends a JSON-RPC notification/response to all connected SSE clients.
        /// </summary>
        public void SendNotification(string jsonMessage)
        {
            if (!_isRunning || _sseClients.IsEmpty) return;

            // Format as SSE data
            string sseData = $"data: {jsonMessage}\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseData);

            foreach (var kvp in _sseClients)
            {
                try
                {
                    kvp.Value.OutputStream.Write(bytes, 0, bytes.Length);
                    kvp.Value.OutputStream.Flush();
                }
                catch
                {
                    // If write fails, client usually disconnected
                    _sseClients.TryRemove(kvp.Key, out _);
                }
            }
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

            // CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                // Route based on path
                if (request.Url.AbsolutePath == "/sse" && request.HttpMethod == "GET")
                {
                    HandleSseConnection(context);
                }
                else if ((request.Url.AbsolutePath == "/message" || request.Url.AbsolutePath == "/") && request.HttpMethod == "POST")
                {
                    HandleMessage(context);
                }
                else
                {
                    SendError(response, 404, "Not Found");
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Request handling error: {ex.Message}");
                try { SendError(response, 500, "Internal Server Error"); response.Close(); } catch { }
            }
        }

        private void HandleSseConnection(HttpListenerContext context)
        {
            var response = context.Response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.StatusCode = 200;

            var result = Guid.NewGuid();
            _sseClients.TryAdd(result, response);

            Debug.Log($"[MCP] Client connected via SSE: {result}");

            // Send initial connection message to keep it alive or handshake?
            // Optional, but good practice to flush headers
            try
            {
                string init = ": connected\n\n";
                byte[] bytes = Encoding.UTF8.GetBytes(init);
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Flush();
            }
            catch
            {
                _sseClients.TryRemove(result, out _);
                response.Close();
                return;
            }
            
            // Keep the connection open indefinitely until client disconnects or server stops
            // The ListenLoop thread actually handed this off to ThreadPool, so blocking here blocks one pool thread.
            // For a simple server this is okay. Ideally we'd use async IO but HttpListener synchronous API is simpler.
            while (_isRunning && _sseClients.ContainsKey(result))
            {
                Thread.Sleep(1000); // Check every second
            }

            try { response.Close(); } catch { }
        }

        private void HandleMessage(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string requestBody;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                requestBody = reader.ReadToEnd();
            }

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

            if (!waitHandle.WaitOne(30000))
            {
                responseBody = JsonRpcHandler.CreateErrorResponse(null, -32603, "Request timeout");
            }

            // Send response direct in POST reply
            response.ContentType = "application/json";
            response.StatusCode = 200;
            var buffer = Encoding.UTF8.GetBytes(responseBody ?? "{}");
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
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

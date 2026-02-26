using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace GxMcp.Gateway
{
    class Program
    {
        private static WorkerProcess? _worker;
        private static ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
        private static ConcurrentDictionary<string, JObject> _semanticCache = new ConcurrentDictionary<string, JObject>();
        private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gateway_debug.log");

        private static void InitializeLogging()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(_logPath))
                    {
                        string prevLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gateway_debug.prev.log");
                        if (File.Exists(prevLog)) File.Delete(prevLog);
                        File.Move(_logPath, prevLog);
                        break;
                    }
                }
                catch 
                { 
                    if (i == 2) break;
                    System.Threading.Thread.Sleep(100); 
                }
            }
            
            Log("=== Gateway starting (Stdio Mode) ===");
        }

        public static void Log(string msg)
        {
            try { 
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_logPath, $"[{timestamp}] {msg}\n"); 
            }
            catch { }
        }

        static async Task Main(string[] args)
        {
            // Register encoding provider for Windows-1252 support in .NET 8
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            InitializeLogging();

            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Log("FATAL UNHANDLED EXCEPTION: " + (e.ExceptionObject as Exception)?.ToString());
            };

            TaskScheduler.UnobservedTaskException += (s, e) => {
                Log("UNOBSERVED TASK EXCEPTION: " + e.Exception?.ToString());
                e.SetObserved();
            };

            Console.Error.WriteLine("=== Gateway starting (Stdio Mode) ===");
            
            var config = Configuration.Load();
            _worker = new WorkerProcess(config);
            _worker.OnRpcResponse += HandleWorkerResponse;
            _worker.Start();

            // HTTP Server in background
            if (config.Server?.HttpPort > 0)
            {
                _ = Task.Run(async () => {
                    try { await StartHttpServer(config); }
                    catch { }
                });
            }

            // MCP Stdio Loop
            using (var reader = new StreamReader(Console.OpenStandardInput()))
            {
                while (true)
                {
                    string? line = null;
                    try { line = await reader.ReadLineAsync(); } catch { }
                    
                    if (line == null) {
                        // If Stdio is closed but HTTP is enabled, wait forever
                        if (config.Server?.HttpPort > 0) {
                            Log("Stdio closed, keeping alive for HTTP...");
                            await Task.Delay(-1);
                        }
                        break; 
                    }
                    
                    try {
                        var request = JObject.Parse(line);
                        var response = await ProcessMcpRequest(request);
                        if (response != null)
                        {
                            Console.WriteLine(response.ToString(Formatting.None));
                            Console.Out.Flush();
                        }
                    } catch (Exception ex) { Log("MCP Error: " + ex.Message); }
                }
            }
        }

        private static void HandleWorkerResponse(string json)
        {
            try {
                var val = JObject.Parse(json);
                string id = val["id"]?.ToString();
                if (!string.IsNullOrEmpty(id) && _pendingRequests.TryRemove(id, out var tcs))
                    tcs.SetResult(json);
            } catch { }
        }

        private static async Task<JObject?> ProcessMcpRequest(JObject request)
        {
            string method = request["method"]?.ToString();
            var idToken = request["id"];

            // Protocol level
            var mcpResponse = McpRouter.Handle(request);
            if (mcpResponse != null)
            {
                return new JObject { ["jsonrpc"] = "2.0", ["id"] = idToken?.DeepClone(), ["result"] = JToken.FromObject(mcpResponse) };
            }

            // Tool Calls
            if (method == "tools/call")
            {
                // ... (logic handled below) ...
            }

            // Resource Calls
            if (method == "resources/read")
            {
                var workerCmd = McpRouter.ConvertResourceCall(request) as JObject;
                if (workerCmd != null)
                {
                    workerCmd["client"] = "mcp";
                    string idStr = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();
                    _pendingRequests[idStr] = tcs;

                    var rpcWrapper = new { jsonrpc = "2.0", id = idStr, method = "execute_command", @params = workerCmd };
                    await _worker!.SendCommandAsync(JsonConvert.SerializeObject(rpcWrapper));

                    if (await Task.WhenAny(tcs.Task, Task.Delay(30000)) == tcs.Task)
                    {
                        var resultObj = JObject.Parse(await tcs.Task);
                        var content = resultObj["result"]?.ToString() ?? "";
                        return new JObject { 
                            ["jsonrpc"] = "2.0", 
                            ["id"] = idToken?.DeepClone(), 
                            ["result"] = JToken.FromObject(new { 
                                contents = new[] { 
                                    new { 
                                        uri = request["params"]?["uri"]?.ToString(), 
                                        mimeType = "text/plain", 
                                        text = content 
                                    } 
                                } 
                            }) 
                        };
                    }
                }
            }

            // Tool Calls (Actual logic)
            if (method == "tools/call")
            {
                var paramsObj = request["params"] as JObject;
                string toolName = paramsObj?["name"]?.ToString() ?? "";
                var args = paramsObj?["arguments"] as JObject;
                
                // 1. CACHE INVALIDATION: If it's a write operation, clear the cache
                if (toolName.Contains("write") || toolName.Contains("patch"))
                {
                    Log($"[Cache] Invalidation triggered by {toolName}");
                    _semanticCache.Clear();
                }

                // 2. SEMANTIC CACHE: Try to get from cache for read-only tools
                string cacheKey = $"{toolName}:{args?.ToString(Formatting.None)}";
                if (_semanticCache.TryGetValue(cacheKey, out var cachedResponse))
                {
                    Log($"[Cache] HIT for {toolName}");
                    var cloned = cachedResponse.DeepClone() as JObject;
                    if (cloned != null) {
                        cloned["id"] = idToken?.DeepClone();
                        return cloned;
                    }
                }

                var workerCmd = McpRouter.ConvertToolCall(request) as JObject;
                if (workerCmd != null)
                {
                    workerCmd["client"] = "mcp";
                    string idStr = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();
                    _pendingRequests[idStr] = tcs;

                    var rpcWrapper = new { jsonrpc = "2.0", id = idStr, method = "execute_command", @params = workerCmd };
                    await _worker!.SendCommandAsync(JsonConvert.SerializeObject(rpcWrapper));

                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(60000));
                    if (completedTask == tcs.Task)
                    {
                        string resultJson = await tcs.Task;
                        var resultObj = JObject.Parse(resultJson);
                        var finalResult = resultObj["result"] ?? resultObj["error"];
                        
                        var response = new JObject { 
                            ["jsonrpc"] = "2.0", 
                            ["id"] = idToken?.DeepClone(), 
                            ["result"] = JToken.FromObject(new { content = new[] { new { type = "text", text = finalResult.ToString() } }, isError = resultObj["error"] != null }) 
                        };

                        // Store in cache if not an error and not a write tool
                        if (resultObj["error"] == null && !toolName.Contains("write") && !toolName.Contains("patch"))
                        {
                            _semanticCache[cacheKey] = response;
                        }

                        return response;
                    }
                }
            }
            return null;
        }

        static Task StartHttpServer(Configuration config)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://*:{config.Server.HttpPort}");
            builder.Logging.ClearProviders();
            builder.Services.AddResponseCompression(options => { options.EnableForHttps = true; });
            builder.Services.AddCors(options => options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            var app = builder.Build();
            app.UseResponseCompression();
            app.UseCors("AllowAll");

            app.MapPost("/api/command", async (HttpRequest request) => {
                using (var reader = new StreamReader(request.Body)) {
                    string body = await reader.ReadToEndAsync();
                    Log($"[HTTP] Received command body: {(body.Length > 100 ? body.Substring(0, 100) + "..." : body)}");
                    
                    var requestObj = JsonConvert.DeserializeObject<JObject>(body);
                    var workerParams = requestObj?["params"] as JObject ?? requestObj;
                    if (workerParams != null) workerParams["client"] = "ide";

                    string requestId = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();
                    _pendingRequests[requestId] = tcs;

                    var rpcWrapper = new { jsonrpc = "2.0", id = requestId, method = "execute_command", @params = workerParams };
                    string rpcStr = JsonConvert.SerializeObject(rpcWrapper);
                    Log($"[HTTP] Sending to worker: {(rpcStr.Length > 100 ? rpcStr.Substring(0, 100) + "..." : rpcStr)}");
                    
                    await _worker!.SendCommandAsync(rpcStr);
                    Log($"[HTTP] Command {requestId} sent to worker. Waiting for response...");

                    if (await Task.WhenAny(tcs.Task, Task.Delay(30000)) == tcs.Task) {
                        var res = JObject.Parse(await tcs.Task);
                        Log($"[HTTP] Worker responded for {requestId}: {(res.ToString(Formatting.None).Length > 100 ? res.ToString(Formatting.None).Substring(0, 100) + "..." : res.ToString(Formatting.None))}");
                        return Results.Content(res["result"]?.ToString() ?? await tcs.Task, "application/json");
                    }
                    Log($"[HTTP] Timeout for {requestId}");
                    return Results.BadRequest(new { error = "Timeout", requestId = requestId });
                }
            });

            return app.RunAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text;
using System.Text.RegularExpressions;
using Functions;
using Client;
using Main;
using RMemory;

namespace Bridge
{
    public class Websocket
    {
        private readonly string _host;
        private readonly int _port;
        private readonly WebSocketServer _server;

        private readonly object _clientLock = new();
        private readonly object _connectionLock = new();
        private readonly object _requestLock = new();
        private readonly object _listenerLock = new();

        private readonly Dictionary<int, WebsocketBehavior> _connections = new();
        private readonly List<Client> _clients = new();
        private readonly Dictionary<string, TaskCompletionSource<JObject>> _onGoingRequests = new();
        private readonly Dictionary<string, Action<WebsocketBehavior, JObject>> _listeners = new();
        public event Action<int> OnInitialized;

        public Websocket(string host, int port)
        {
            _host = host;
            _port = port;
            _server = new WebSocketServer(port);
            _server.AddWebSocketService<WebsocketBehavior>("/", () => new WebsocketBehavior(this));
            SetupListeners();
        }

        public bool Run()
        {
            try
            {
                _server.Start();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to start server: {e.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _server.Stop();
            Console.WriteLine("Server stopped.");
            lock (_connectionLock) _connections.Clear();
        }

        public void Send(string data, int pid)
        {
            lock (_connectionLock)
            {
                if (_connections.TryGetValue(pid, out var wsBehavior))
                    wsBehavior.SendString(data);
            }
        }

        public void Send(JObject data, int pid)
        {
            lock (_connectionLock)
            {
                if (_connections.TryGetValue(pid, out var wsBehavior))
                    wsBehavior.SendJson(data);
            }
        }

        public async Task<JObject> SendAndReceive(JObject data, int pid, int timeoutSeconds = 5)
        {
            string id = Guid.NewGuid().ToString();
            data["id"] = id;
            var tcs = new TaskCompletionSource<JObject>();
            lock (_requestLock) _onGoingRequests[id] = tcs;

            Send(data, pid);

            if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutSeconds * 1000)) == tcs.Task)
            {
                lock (_requestLock) _onGoingRequests.Remove(id);
                return tcs.Task.Result;
            }

            lock (_requestLock) _onGoingRequests.Remove(id);
            throw new TimeoutException($"timeout waiting for response {id}");
        }

        public void AddClient(Client client)
        {
            lock (_clientLock)
            {
                _clients.Add(client);
            }
        }

        public void RemoveClient(int pid)
        {
            lock (_clientLock)
            {
                lock (_connectionLock)
                {
                    if (_connections.TryGetValue(pid, out var wsBehavior))
                    {
                        wsBehavior.Context.WebSocket.Close();
                        _connections.Remove(pid);
                    }
                }

                _clients.RemoveAll(c => c.GetProcess().Id == pid);
            }
        }

        internal void AddConnection(int pid, WebsocketBehavior behavior)
        {
            lock (_connectionLock)
            {
                _connections[pid] = behavior;
                Console.WriteLine($"[*] WebSocket connection established for PID {pid}");
            }
        }

        internal void OnMessage(WebsocketBehavior behavior, JObject data)
        {
            string id = data.ContainsKey("id") ? data["id"]?.ToString() : "";
            string type = data.ContainsKey("type") ? data["type"]?.ToString() : "";
            string action = data.ContainsKey("action") ? data["action"]?.ToString() : "";
            int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

            if (!string.IsNullOrEmpty(id) && type == "response")
            {
                lock (_requestLock)
                {
                    if (_onGoingRequests.TryGetValue(id, out var tcs))
                    {
                        tcs.SetResult(data);
                        _onGoingRequests.Remove(id);
                        return;
                    }
                }
            }

            if (string.IsNullOrEmpty(action) || pid == 0) return;

            lock (_listenerLock)
            {
                if (_listeners.TryGetValue(action, out var listener))
                    listener(behavior, data);
            }
        }

        private void SetupListeners()
        {
            AddListener("initialize", (behavior, data) =>
            {
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                
                lock (_connectionLock)
                {
                    _connections[pid] = behavior;
                }
                
                OnInitialized?.Invoke(pid);
            });

            AddListener("is_compilable", (behavior, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

                JObject response = new JObject
                {
                    ["type"] = "response",
                    ["success"] = false,
                    ["pid"] = pid
                };

                if (!string.IsNullOrEmpty(id))
                    response["id"] = id;

                try
                {
                    if (!data.ContainsKey("source"))
                        throw new Exception("Missing required key: source");

                    string source = data["source"].ToString();
                    byte[] sourceBytes = Convert.FromBase64String(source);
                    string decodedSource = Encoding.UTF8.GetString(sourceBytes);

                    string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lua");
                    File.WriteAllText(tempFile, decodedSource);
                    
                    try
                    {
                        byte[] bytecode = Executor.Compile(tempFile);  // Changed this line - assign to variable
                        response["success"] = true;
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                behavior.SendJson(response);
            });

            AddListener("request", (behavior, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

                JObject response = new JObject
                {
                    ["type"] = "response",
                    ["success"] = false,
                    ["pid"] = pid
                };

                if (!string.IsNullOrEmpty(id))
                    response["id"] = id;

                try
                {
                    if (!data.ContainsKey("url") || !data.ContainsKey("method"))
                        throw new Exception("Missing required keys: url, method");

                    string url = data["url"].ToString();
                    string method = data["method"].ToString().ToUpper();
                    JObject headers = data.ContainsKey("headers") ? data["headers"] as JObject : new JObject();
                    string body = data.ContainsKey("body") ? data["body"].ToString() : "";

                    // Validate URL
                    string urlPattern = @"^(https?:\/\/(?:[a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}|https?:\/\/(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)){3})(:\d{1,5})?(\/[^\s]*)?$";
                    if (!Regex.IsMatch(url, urlPattern, RegexOptions.IgnoreCase))
                        throw new Exception("Invalid URL");

                    // Make HTTP request
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);

                        var requestMessage = new HttpRequestMessage();
                        requestMessage.RequestUri = new Uri(url);

                        // Add headers
                        foreach (var header in headers)
                        {
                            try
                            {
                                client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
                            }
                            catch { }
                        }

                        // Set method and body
                        switch (method)
                        {
                            case "GET":
                                requestMessage.Method = HttpMethod.Get;
                                break;
                            case "POST":
                                requestMessage.Method = HttpMethod.Post;
                                requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                                break;
                            case "PUT":
                                requestMessage.Method = HttpMethod.Put;
                                requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                                break;
                            case "DELETE":
                                requestMessage.Method = HttpMethod.Delete;
                                if (!string.IsNullOrEmpty(body))
                                    requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                                break;
                            case "PATCH":
                                requestMessage.Method = new HttpMethod("PATCH");
                                requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                                break;
                            default:
                                throw new Exception("Unsupported HTTP method");
                        }

                        var httpResponse = client.SendAsync(requestMessage).Result;
                        string responseBody = httpResponse.Content.ReadAsStringAsync().Result;

                        JObject responseHeaders = new JObject();
                        foreach (var header in httpResponse.Headers)
                        {
                            responseHeaders[header.Key] = string.Join(", ", header.Value);
                        }

                        response["response"] = new JObject
                        {
                            ["success"] = httpResponse.IsSuccessStatusCode,
                            ["status_code"] = (int)httpResponse.StatusCode,
                            ["status_message"] = httpResponse.ReasonPhrase,
                            ["headers"] = responseHeaders,
                            ["body"] = responseBody
                        };

                        response["success"] = true;
                    }
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                behavior.SendJson(response);
            });

            AddListener("loadstring", (behavior, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

                JObject response = new JObject
                {
                    ["type"] = "response",
                    ["success"] = false,
                    ["pid"] = pid
                };

                if (!string.IsNullOrEmpty(id))
                    response["id"] = id;

                try
                {
                    if (!data.ContainsKey("chunk") || !data.ContainsKey("chunk_name") || !data.ContainsKey("script_name"))
                        throw new Exception("Missing required keys: chunk, chunk_name, script_name");

                    string chunk = data["chunk"].ToString();
                    byte[] chunkBytes = Convert.FromBase64String(chunk);
                    string decodedChunk = Encoding.UTF8.GetString(chunkBytes);

                    string chunkName = data["chunk_name"].ToString();
                    string scriptName = data["script_name"].ToString();

                    Client client = FindClientByProcessId(pid);
                    if (client == null)
                        throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var script = robloxReplicatedStorage.FindFirstChildFromPath($"Executor.Scripts.{scriptName}");
                    if (script == null || script.Self == IntPtr.Zero)
                        throw new Exception($"Script '{scriptName}' not found");

                    // Build the loadstring wrapper
                    string wrappedCode = $"local function {chunkName}(...)do for i,v in pairs(getfenv(debug.info(1,'f')))do getfenv(0)[i]=v;end;setmetatable(getgenv and getgenv()or{{}},{{__newindex=function(t,i,v)rawset(t,i,v)getfenv()[i]=v;getfenv(0)[i]=v;end}})end;{decodedChunk}\nend;return {chunkName}";

                    // Write to temp file and compile
                    string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lua");
                    File.WriteAllText(tempFile, wrappedCode);

                    try
                    {
                        byte[] bytecode = Executor.Compile(tempFile);
                        script.UnlockModule();
                        Bytecodes.SetBytecode(script, bytecode);
                        response["success"] = true;
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                    Console.WriteLine($"[ERROR] Loadstring failed: {ex.Message}");
                }

                behavior.SendJson(response);
            });

            AddListener("getscriptbytecode", (behavior, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

                JObject response = new JObject
                {
                    ["type"] = "response",
                    ["success"] = false,
                    ["pid"] = pid
                };

                if (!string.IsNullOrEmpty(id))
                    response["id"] = id;

                try
                {
                    if (!data.ContainsKey("pointer_name"))
                        throw new Exception("Missing required key: pointer_name");

                    string pointerName = data["pointer_name"].ToString();

                    Client client = FindClientByProcessId(pid);
                    if (client == null)
                        throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Executor.Objects.{pointerName}");
                    if (pointer == null || pointer.Self == IntPtr.Zero)
                        throw new Exception($"Pointer '{pointerName}' not found");

                    // Read the script from ObjectValue.Value
                    IntPtr scriptPtr = Memory.Read<IntPtr>((ulong)pointer.Self + 0x48); // Offsets::Misc::Value
                    if (scriptPtr == IntPtr.Zero)
                        throw new Exception("Script pointer is null");

                    var script = new RobloxInstance(scriptPtr);
                    byte[] bytecode = Bytecodes.GetBytecode(script);

                    response["bytecode"] = Convert.ToBase64String(bytecode);
                    response["success"] = true;
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                behavior.SendJson(response);
            });

            AddListener("unlock_module", (behavior, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;

                JObject response = new JObject
                {
                    ["type"] = "response",
                    ["success"] = false,
                    ["pid"] = pid
                };

                if (!string.IsNullOrEmpty(id))
                    response["id"] = id;

                try
                {
                    if (!data.ContainsKey("pointer_name"))
                        throw new Exception("Missing required key: pointer_name");

                    string pointerName = data["pointer_name"].ToString();

                    Client client = FindClientByProcessId(pid);
                    if (client == null)
                        throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Executor.Objects.{pointerName}");
                    if (pointer == null || pointer.Self == IntPtr.Zero)
                        throw new Exception($"Pointer '{pointerName}' not found");

                    // Read the module script from ObjectValue.Value
                    IntPtr moduleScriptPtr = Memory.Read<IntPtr>((ulong)pointer.Self + 0x48); // Offsets::Misc::Value
                    if (moduleScriptPtr == IntPtr.Zero)
                        throw new Exception("ModuleScript pointer is null");

                    var moduleScript = new RobloxInstance(moduleScriptPtr);
                    moduleScript.UnlockModule();

                    response["success"] = true;
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                behavior.SendJson(response);
            });
        }

        public Client FindClientByProcessId(int pid) => _clients.FirstOrDefault(c => c.GetProcess().Id == pid);

        public int GetPort() => _port;
        public string GetHost() => _host;
        public IReadOnlyList<Client> GetClients() => _clients.AsReadOnly();

        public void AddListener(string id, Action<WebsocketBehavior, JObject> listener)
        {
            lock (_listenerLock)
            {
                _listeners[id] = listener;
            }
        }

        public class WebsocketBehavior : WebSocketBehavior
        {
            private readonly Websocket _parent;

            public WebsocketBehavior(Websocket parent)
            {
                _parent = parent;
            }

            protected override void OnOpen() { }
            protected override void OnClose(CloseEventArgs e) { }

            protected override void OnMessage(MessageEventArgs e)
            {
                try
                {
                    var data = JObject.Parse(e.Data);
                    _parent.OnMessage(this, data);
                }
                catch
                {
                    // ignore malformed JSON
                }
            }

            public void SendString(string message)
            {
                Send(message);
            }

            public void SendJson(JObject data)
            {
                Send(data.ToString());
            }
        }
    }
}

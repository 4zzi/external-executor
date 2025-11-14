using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Fleck;
//
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
        private IWebSocketServer _server;

        private readonly object _clientLock = new();
        private readonly object _connectionLock = new();
        private readonly object _requestLock = new();
        private readonly object _listenerLock = new();

        private readonly Dictionary<int, IWebSocketConnection> _connections = new();
        private readonly List<Client> _clients = new();
        private readonly Dictionary<string, TaskCompletionSource<JObject>> _onGoingRequests = new();
        private readonly Dictionary<string, Action<IWebSocketConnection, JObject>> _listeners = new();
        public event Action<int> OnInitialized;

        public Websocket(string host, int port)
        {
            _host = host;
            _port = port;
            SetupListeners();
        }

        public bool Run()
        {
            try
            {
                _server = new WebSocketServer($"ws://{_host}:{_port}");
                _server.Start(socket =>
                {
                    socket.OnOpen = () => { };
                    socket.OnClose = () => RemoveConnection(socket);
                    socket.OnMessage = msg =>
                    {
                        try
                        {
                            var data = JObject.Parse(msg);
                            OnMessage(socket, data);
                        }
                        catch { }
                    };
                });
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
            _server?.Dispose();
            lock (_connectionLock) _connections.Clear();
        }

        public void Send(JObject data, int pid)
        {
            lock (_connectionLock)
            {
                if (_connections.TryGetValue(pid, out var socket))
                {
                    socket.Send(data.ToString());
                }
                else
                {
                }
            }
        }

        public Task<JObject> SendAndReceive(JObject data, int pid, int timeoutMs = 5000)
        {
            var tcs = new TaskCompletionSource<JObject>();
            string id = data["id"]?.ToString() ?? Guid.NewGuid().ToString();
            data["id"] = id;

            lock (_requestLock)
                _onGoingRequests[id] = tcs;

            Send(data, pid);

            Task.Delay(timeoutMs).ContinueWith(_ =>
            {
                lock (_requestLock)
                {
                    if (_onGoingRequests.TryGetValue(id, out var existingTcs))
                    {
                        existingTcs.TrySetException(new TimeoutException());
                        _onGoingRequests.Remove(id);
                    }
                }
            });

            return tcs.Task;
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
                    if (_connections.TryGetValue(pid, out var socket))
                    {
                        socket.Close();
                        _connections.Remove(pid);
                    }
                }

                _clients.RemoveAll(c => c.GetProcess().Id == pid);
            }
        }

        private void AddConnection(IWebSocketConnection socket, int pid)
        {
            lock (_connectionLock)
            {
                _connections[pid] = socket;
            }
        }

        private void RemoveConnection(IWebSocketConnection socket)
        {
            lock (_connectionLock)
            {
                var pair = _connections.FirstOrDefault(kv => kv.Value == socket);
                if (pair.Key != 0) _connections.Remove(pair.Key);
            }
        }

        private void OnMessage(IWebSocketConnection socket, JObject data)
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
                    listener(socket, data);
            }
        }

        private void SetupListeners()
        {
            AddListener("initialize", (socket, data) =>
            {
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                AddConnection(socket, pid);
                OnInitialized?.Invoke(pid);
            });

            AddListener("is_compilable", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("source")) throw new Exception("Missing required key: source");
                    string decodedSource = Encoding.UTF8.GetString(Convert.FromBase64String(data["source"].ToString()));
                    string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lua");
                    File.WriteAllText(tempFile, decodedSource);
                    try
                    {
                        byte[] bytecode = Executor.Compile(tempFile);
                        response["success"] = true;
                    }
                    finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("request", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("url") || !data.ContainsKey("method"))
                        throw new Exception("Missing required keys: url, method");

                    string url = data["url"].ToString();
                    string method = data["method"].ToString().ToUpper();
                    JObject headers = data.ContainsKey("headers") ? data["headers"] as JObject : new JObject();
                    string body = data.ContainsKey("body") ? data["body"].ToString() : "";

                    string urlPattern = @"^(https?:\/\/(?:[a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}|https?:\/\/(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)){3})(:\d{1,5})?(\/[^\s]*)?$";
                    if (!Regex.IsMatch(url, urlPattern, RegexOptions.IgnoreCase))
                        throw new Exception("Invalid URL");

                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    var requestMessage = new HttpRequestMessage(new HttpMethod(method), new Uri(url));

                    foreach (var header in headers)
                    {
                        try { client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString()); }
                        catch { }
                    }

                    if (method == "POST" || method == "PUT" || method == "PATCH")
                        requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    else if (method == "DELETE" && !string.IsNullOrEmpty(body))
                        requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    var httpResponse = client.SendAsync(requestMessage).Result;
                    string responseBody = httpResponse.Content.ReadAsStringAsync().Result;

                    JObject responseHeaders = new JObject();
                    foreach (var header in httpResponse.Headers)
                        responseHeaders[header.Key] = string.Join(", ", header.Value);

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
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("loadstring", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("chunk") || !data.ContainsKey("chunk_name") || !data.ContainsKey("script_name"))
                        throw new Exception("Missing keys: chunk, chunk_name, script_name");

                    string chunk = data["chunk"].ToString();
                    string chunkName = data["chunk_name"].ToString();
                    string scriptName = data["script_name"].ToString();

                    Client client = FindClientByProcessId(pid);
                    if (client == null) throw new Exception($"Client {pid} not connected");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var moduleScript = robloxReplicatedStorage.FindFirstChildFromPath($"Oracle.Scripts.{scriptName}");
                    if (moduleScript == null || moduleScript.Self == IntPtr.Zero)
                        throw new Exception($"Script {scriptName} not found");

                    string decodedChunk = Encoding.UTF8.GetString(Convert.FromBase64String(chunk));
                    string wrappedCode = $"local function {chunkName}(...)\n{decodedChunk}\nend\nreturn {chunkName}";
                    string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lua");
                    File.WriteAllText(tempFile, wrappedCode);

                    try
                    {
                        byte[] bytecode = Executor.Compile(tempFile);
                        if (bytecode == null || bytecode.Length == 0)
                            throw new Exception("Compilation failed");

                        moduleScript.UnlockModule();
                        Bytecodes.SetBytecode(moduleScript, bytecode);
                        response["success"] = true;
                    }
                    finally { }
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("getscriptbytecode", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("pointer_name")) throw new Exception("Missing required key: pointer_name");

                    string pointerName = data["pointer_name"].ToString();
                    Client client = FindClientByProcessId(pid);
                    if (client == null) throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Executor.Objects.{pointerName}");
                    if (pointer == null || pointer.Self == IntPtr.Zero)
                        throw new Exception($"Pointer '{pointerName}' not found");

                    IntPtr scriptPtr = Memory.Read<IntPtr>((nint)pointer.Self + 0x48);
                    if (scriptPtr == IntPtr.Zero) throw new Exception("Script pointer is null");

                    var script = new RobloxInstance(scriptPtr);
                    byte[] bytecode = Bytecodes.GetBytecode(script);

                    response["bytecode"] = Convert.ToBase64String(bytecode);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("unlock_module", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? data["id"].ToString() : "";
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("pointer_name")) throw new Exception("Missing required key: pointer_name");

                    string pointerName = data["pointer_name"].ToString();
                    Client client = FindClientByProcessId(pid);
                    if (client == null) throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Executor.Objects.{pointerName}");
                    if (pointer == null || pointer.Self == IntPtr.Zero)
                        throw new Exception($"Pointer '{pointerName}' not found");

                    IntPtr moduleScriptPtr = Memory.Read<IntPtr>((nint)pointer.Self + 0x48);
                    if (moduleScriptPtr == IntPtr.Zero) throw new Exception("ModuleScript pointer is null");

                    var moduleScript = new RobloxInstance(moduleScriptPtr);
                    moduleScript.UnlockModule();
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });
        }

        public Client FindClientByProcessId(int pid) => _clients.FirstOrDefault(c => c.GetProcess().Id == pid);

        public int GetPort() => _port;
        public string GetHost() => _host;
        public IReadOnlyList<Client> GetClients() => _clients.AsReadOnly();

        public void AddListener(string id, Action<IWebSocketConnection, JObject> listener)
        {
            lock (_listenerLock)
            {
                _listeners[id] = listener;
            }
        }
    }
}
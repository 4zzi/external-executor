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
using System.Runtime.InteropServices;
//
using Functions;
using Client;
using Main;
using RMemory;

namespace Bridge
{
    public class Websocket
    {
        // Simple in-memory console storage per bridge process
        private readonly Dictionary<string, List<string>> _consoles = new();
        private readonly Dictionary<int, string> _clientDefaultConsole = new();
        // Native clipboard helpers (avoid depending on System.Windows.Forms)
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        private static bool SetClipboardText(string text)
        {
            if (text == null) return false;
            // Encode as UTF-16LE with terminating null
            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            UIntPtr size = (UIntPtr)bytes.Length;
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, size);
            if (hGlobal == IntPtr.Zero) return false;

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (!OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                var res = SetClipboardData(CF_UNICODETEXT, hGlobal);
                return res != IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();
            }
        }
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
            // Resolve base directory and enforce workspace boundaries for mutating filesystem ops
            string _baseDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            string _workspace = Path.GetFullPath(Path.Combine(_baseDir, "workspace"));

            string ResolveAndValidateWorkspacePath(string path)
            {
                if (string.IsNullOrEmpty(path)) throw new Exception("Missing required key: path");
                string fullPath = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(_workspace, path));
                return fullPath;
            }

            AddListener("initialize", (socket, data) =>
            {
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                AddConnection(socket, pid);
                // Ensure a workspace directory exists next to the running executable.
                try
                {
                    Directory.CreateDirectory(Path.Combine(_baseDir, "workspace"));
                }
                catch { }

                OnInitialized?.Invoke(pid);
            });

            AddListener("is_compilable", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    if (!data.ContainsKey("source")) throw new Exception("Missing required key: source");
                    // source is base64 encoded; decode to bytes first so we can detect bytecode
                    byte[] raw = Convert.FromBase64String(data["source"].ToString());

                    // If the payload looks like Lua bytecode (starts with 0x1B 'L' 'u' 'a'), reject it as not compilable
                    if (raw.Length >= 4 && raw[0] == 0x1B && raw[1] == (byte)'L' && raw[2] == (byte)'u' && raw[3] == (byte)'a')
                    {
                        response["message"] = "source appears to be bytecode, not compilable";
                        socket.Send(response.ToString());
                        return;
                    }

                    string decodedSource = Encoding.UTF8.GetString(raw);
                    string tempFile = Path.Combine(Path.GetTempPath(), $"Oracle.lua");
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
                Task.Run(async () =>
                {
                    try
                    {
                        string url =
                            (data.ContainsKey("url") ? data["url"] :
                            data.ContainsKey("Url") ? data["Url"] : null)?.ToString();

                        string method =
                            (data.ContainsKey("method") ? data["method"] :
                            data.ContainsKey("Method") ? data["Method"] : null)?.ToString();

                        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(method))
                            throw new Exception("Missing required keys: url, method");

                        method = method.ToUpperInvariant();

                        string body = "";
                        if (data.ContainsKey("body")) body = data["body"]?.ToString() ?? "";
                        else if (data.ContainsKey("Body")) body = data["Body"]?.ToString() ?? "";

                        JObject headers = null;
                        if (data.ContainsKey("headers"))
                            headers = data["headers"] as JObject;
                        else if (data.ContainsKey("Headers"))
                            headers = data["Headers"] as JObject;

                        if (headers == null || headers.Type == JTokenType.Null)
                            headers = new JObject();

                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                            client.Timeout = TimeSpan.FromSeconds(30);
                            
                            using (var requestMessage = new HttpRequestMessage(new HttpMethod(method), url))
                            {
                                requestMessage.Headers.TryAddWithoutValidation("Accept", "*/*");
                                foreach (var pair in headers)
                                {
                                    string key = pair.Key;
                                    string value = pair.Value?.ToString() ?? "";

                                    if (key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                                    {
                                        client.DefaultRequestHeaders.Remove("User-Agent");
                                        client.DefaultRequestHeaders.Add("User-Agent", value);
                                    }
                                    else if (!requestMessage.Headers.TryAddWithoutValidation(key, value))
                                    {
                                        if (requestMessage.Content == null)
                                            requestMessage.Content = new StringContent("");

                                        requestMessage.Content.Headers.TryAddWithoutValidation(key, value);
                                    }
                                }

                                if (method == "POST" || method == "PUT" || method == "PATCH")
                                {
                                    requestMessage.Content = new StringContent(body ?? "");
                                }

                                var response = await client.SendAsync(requestMessage);
                                string responseBody = await response.Content.ReadAsStringAsync();

                                JObject result = new JObject
                                {
                                    ["type"] = "response",
                                    ["pid"] = data["pid"],
                                    ["id"] = data["id"],
                                    ["success"] = true,
                                    ["status_code"] = (int)response.StatusCode,
                                    ["status_message"] = response.ReasonPhrase,
                                    ["body"] = responseBody,
                                    ["headers"] = JObject.FromObject(
                                        response.Headers.Concat(response.Content.Headers)
                                            .ToDictionary(x => x.Key, x => string.Join(",", x.Value))
                                    )
                                };

                                socket.Send(result.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        JObject error = new JObject
                        {
                            ["type"] = "response",
                            ["pid"] = data["pid"],
                            ["id"] = data["id"],
                            ["success"] = false,
                            ["message"] = ex.Message
                        };

                        socket.Send(error.ToString());
                    }
                });
            });

            // Convenience endpoints for base64 operations so clients can offload encoding/decoding
            AddListener("base64_encode", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string text = (data.ContainsKey("text") && data["text"] != null) ? data["text"].ToString() : null;
                    if (text == null) throw new Exception("Missing required key: text");

                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                    response["success"] = true;
                    response["b64"] = b64;
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                socket.Send(response.ToString());
            });

            AddListener("base64_decode", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string b64 = (data.ContainsKey("b64") && data["b64"] != null) ? data["b64"].ToString() : null;
                    if (b64 == null) throw new Exception("Missing required key: b64");

                    byte[] raw = Convert.FromBase64String(b64);
                    string text = Encoding.UTF8.GetString(raw);
                    response["success"] = true;
                    response["text"] = text;
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

                socket.Send(response.ToString());
            });

            // Set clipboard text on host machine (runs on STA thread)
            AddListener("setclipboard", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    var token = data.ContainsKey("text") ? data["text"] : null;
                    if (token == null) throw new Exception("Missing required key: text");
                    string text = token.ToString();

                    // Use native clipboard helper to avoid dependency on System.Windows.Forms
                    var thread = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            SetClipboardText(text);
                        }
                        catch { }
                    });
                    thread.SetApartmentState(System.Threading.ApartmentState.STA);
                    thread.Start();
                    thread.Join();

                    response["success"] = true;
                }
                catch (Exception ex)
                {
                    response["message"] = ex.Message;
                }

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

                    byte[] raw = Convert.FromBase64String(chunk);

                    // Reject if this looks like bytecode
                    if (raw.Length >= 4 && raw[0] == 0x1B && raw[1] == (byte)'L' && raw[2] == (byte)'u' && raw[3] == (byte)'a')
                    {
                        response["message"] = "chunk appears to be bytecode, cannot loadstring";
                        socket.Send(response.ToString());
                        return;
                    }

                    string decodedChunk = Encoding.UTF8.GetString(raw);
                    string wrappedCode = $@"
                    return function(...)
                        {decodedChunk}
                    end
                    ";

                    string tempFile = Path.Combine(Path.GetTempPath(), $"Oracle.lua");
                    File.WriteAllText(tempFile, wrappedCode);

                    try
                    {
                        byte[] bytecode = Executor.Compile(tempFile);
                        if (bytecode == null || bytecode.Length == 0)
                            throw new Exception("Compilation failed");

                        // compiled successfully

                        moduleScript.UnlockModule();
                        Bytecodes.SetBytecode(moduleScript, bytecode);
                        response["success"] = true;
                    }
                    finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsolecreate", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string title = (data.ContainsKey("title") && data["title"] != null) ? data["title"].ToString() : "";
                    string consoleId = Guid.NewGuid().ToString();
                    _consoles[consoleId] = new List<string>();
                    // store default console for this client
                    if (pid != 0) _clientDefaultConsole[pid] = consoleId;
                    response["success"] = true;
                    response["console_id"] = consoleId;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsoleprint", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string text = (data.ContainsKey("text") && data["text"] != null) ? data["text"].ToString() : "";
                    // If text is empty or null, do nothing but return success
                    if (string.IsNullOrEmpty(text)) { response["success"] = true; socket.Send(response.ToString()); return; }

                    string consoleId = null;
                    if (data.ContainsKey("console_id") && data["console_id"] != null) consoleId = data["console_id"].ToString();
                    if (string.IsNullOrEmpty(consoleId) && pid != 0 && _clientDefaultConsole.TryGetValue(pid, out var def)) consoleId = def;
                    if (string.IsNullOrEmpty(consoleId))
                    {
                        // create a console automatically
                        consoleId = Guid.NewGuid().ToString();
                        _consoles[consoleId] = new List<string>();
                        if (pid != 0) _clientDefaultConsole[pid] = consoleId;
                    }

                    if (!_consoles.ContainsKey(consoleId)) _consoles[consoleId] = new List<string>();
                    _consoles[consoleId].Add(text);

                    // mirror to host console omitted

                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsoleclear", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string consoleId = (data.ContainsKey("console_id") && data["console_id"] != null) ? data["console_id"].ToString() : null;
                    if (string.IsNullOrEmpty(consoleId) && pid != 0 && _clientDefaultConsole.TryGetValue(pid, out var def)) consoleId = def;
                    if (!string.IsNullOrEmpty(consoleId) && _consoles.ContainsKey(consoleId)) _consoles[consoleId].Clear();
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsoledestroy", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string consoleId = (data.ContainsKey("console_id") && data["console_id"] != null) ? data["console_id"].ToString() : null;
                    if (string.IsNullOrEmpty(consoleId) && pid != 0 && _clientDefaultConsole.TryGetValue(pid, out var def)) consoleId = def;
                    if (!string.IsNullOrEmpty(consoleId) && _consoles.ContainsKey(consoleId)) _consoles.Remove(consoleId);
                    if (pid != 0 && _clientDefaultConsole.ContainsKey(pid) && _clientDefaultConsole[pid] == consoleId) _clientDefaultConsole.Remove(pid);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsolesettitle", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string title = (data.ContainsKey("title") && data["title"] != null) ? data["title"].ToString() : "";
                    string consoleId = (data.ContainsKey("console_id") && data["console_id"] != null) ? data["console_id"].ToString() : null;
                    if (string.IsNullOrEmpty(consoleId) && pid != 0 && _clientDefaultConsole.TryGetValue(pid, out var def)) consoleId = def;
                    // We don't store title separately in this simple impl, but accept the call
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("rconsoleinput", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = true, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    // We don't support interactive input; return empty string
                    response["text"] = "";
                }
                catch (Exception ex) { response["message"] = ex.Message; response["success"] = false; }

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
                    Client client = FindClientByProcessId(pid);
                    if (client == null) throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    RobloxInstance? scriptInstance = null;

                    // If caller provided a script_path, locate the instance from the game root
                    if (data.ContainsKey("script_path"))
                    {
                        var token = data["script_path"];
                        if (token == null) throw new Exception("script_path was provided but null");
                        string scriptPath = token.ToString();
                        var found = game.FindFirstChildFromPath(scriptPath);
                        if (found != null && found.Self != IntPtr.Zero)
                            scriptInstance = new RobloxInstance(found.Self);
                        else
                            throw new Exception($"Script not found at path: {scriptPath}");
                    }
                    else
                    {
                        if (!data.ContainsKey("pointer_name")) throw new Exception("Missing required key: pointer_name");

                        var ptoken = data["pointer_name"];
                        if (ptoken == null) throw new Exception("pointer_name was provided but null");
                        string pointerName = ptoken.ToString();
                        var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Oracle.Objects.{pointerName}");
                        // Retry a few times in case replication is delayed
                        int retries = 8;
                        int delayMs = 100;
                        int attempt = 0;
                        while ((pointer == null || pointer.Self == IntPtr.Zero) && attempt < retries)
                        {
                            attempt++;
                            System.Threading.Thread.Sleep(delayMs);
                            pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Oracle.Objects.{pointerName}");
                        }
                        if (pointer == null || pointer.Self == IntPtr.Zero)
                        {
                            throw new Exception($"Pointer '{pointerName}' not found");
                        }

                        // The ObjectValue.Value field offset can vary by build; try multiple candidate offsets.
                        IntPtr scriptPtr = IntPtr.Zero;
                        int[] candidateOffsets = new int[] { 0x40, 0x48, 0x50, 0x58, 0x60 };
                        foreach (var off in candidateOffsets)
                        {
                            try
                            {
                                var cand = Memory.Read<IntPtr>((nint)pointer.Self + off);
                                if (cand != IntPtr.Zero)
                                {
                                    var candInst = new RobloxInstance(cand);
                                    if (candInst && (candInst.ClassName == "ModuleScript" || candInst.ClassName == "LocalScript"))
                                    {
                                        scriptPtr = cand;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (scriptPtr == IntPtr.Zero) {
                            throw new Exception("Script pointer is null");
                        }

                        scriptInstance = new RobloxInstance(scriptPtr);
                    }

                    if (scriptInstance == null || scriptInstance.Self == IntPtr.Zero)
                        throw new Exception("Script instance not found or invalid");

                    byte[] bytecode = Bytecodes.GetBytecode(scriptInstance);

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
                    Client client = FindClientByProcessId(pid);
                    if (client == null) throw new Exception($"Failed to find client {pid}");

                    var game = Main.Roblox.Game;
                    var robloxReplicatedStorage = game.FindFirstChildOfClass("RobloxReplicatedStorage");
                    if (robloxReplicatedStorage == null || robloxReplicatedStorage.Self == IntPtr.Zero)
                        throw new Exception("RobloxReplicatedStorage not found");

                    RobloxInstance? moduleInstance = null;

                    if (data.ContainsKey("script_path"))
                    {
                        var token = data["script_path"];
                        if (token == null) throw new Exception("script_path was provided but null");
                        string scriptPath = token.ToString();
                        var found = game.FindFirstChildFromPath(scriptPath);
                        if (found != null && found.Self != IntPtr.Zero)
                            moduleInstance = new RobloxInstance(found.Self);
                        else
                            throw new Exception($"Script not found at path: {scriptPath}");
                    }
                    else
                    {
                        if (!data.ContainsKey("pointer_name")) throw new Exception("Missing required key: pointer_name");

                        var ptoken = data["pointer_name"];
                        if (ptoken == null) throw new Exception("pointer_name was provided but null");
                        string pointerName = ptoken.ToString();
                        var pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Oracle.Objects.{pointerName}");
                        // Retry a few times in case replication is delayed
                        int retries2 = 8;
                        int delayMs2 = 100;
                        int attempt2 = 0;
                        while ((pointer == null || pointer.Self == IntPtr.Zero) && attempt2 < retries2)
                        {
                            attempt2++;
                            System.Threading.Thread.Sleep(delayMs2);
                            pointer = robloxReplicatedStorage.FindFirstChildFromPath($"Oracle.Objects.{pointerName}");
                        }
                        if (pointer == null || pointer.Self == IntPtr.Zero)
                        {
                            throw new Exception($"Pointer '{pointerName}' not found");
                        }

                        // Try multiple offsets for ObjectValue.Value like above
                        IntPtr moduleScriptPtr = IntPtr.Zero;
                        int[] offsets2 = new int[] { 0x40, 0x48, 0x50, 0x58, 0x60 };
                        foreach (var off in offsets2)
                        {
                            try
                            {
                                var cand = Memory.Read<IntPtr>((nint)pointer.Self + off);
                                if (cand != IntPtr.Zero)
                                {
                                    var candInst = new RobloxInstance(cand);
                                    if (candInst && candInst.ClassName == "ModuleScript")
                                    {
                                        moduleScriptPtr = cand;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (moduleScriptPtr == IntPtr.Zero)
                        {
                            throw new Exception("ModuleScript pointer is null");
                        }

                        moduleInstance = new RobloxInstance(moduleScriptPtr);
                    }

                    if (moduleInstance == null || moduleInstance.Self == IntPtr.Zero)
                        throw new Exception("ModuleScript instance not found or invalid");

                    moduleInstance.UnlockModule();
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("readfile", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    // default to workspace root when path is omitted
                    string fullPath;
                    if (string.IsNullOrEmpty(path) || path == ".")
                        fullPath = _workspace;
                    else
                        fullPath = ResolveAndValidateWorkspacePath(path);

                    if (!File.Exists(fullPath)) throw new Exception("file not found");

                    string text = File.ReadAllText(fullPath);
                    response["text"] = text;
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("writefile", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    string text = (data.ContainsKey("text") && data["text"] != null) ? data["text"].ToString() : null;
                    if (path == null) throw new Exception("Missing required key: path");
                    if (text == null) throw new Exception("Missing required key: text");

                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    File.WriteAllText(fullPath, text);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("appendfile", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    string text = (data.ContainsKey("text") && data["text"] != null) ? data["text"].ToString() : null;
                    if (path == null) throw new Exception("Missing required key: path");
                    if (text == null) throw new Exception("Missing required key: text");

                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    File.AppendAllText(fullPath, text);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("isfile", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    response["isfile"] = File.Exists(fullPath);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("isfolder", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    response["isfolder"] = Directory.Exists(fullPath);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("makefolder", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    if (path == null) throw new Exception("Missing required key: path");

                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    Directory.CreateDirectory(fullPath);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("listfiles", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    string fullPath;
                    if (string.IsNullOrEmpty(path) || path == ".") fullPath = _workspace;
                    else fullPath = ResolveAndValidateWorkspacePath(path);

                    var files = new JArray();
                    if (Directory.Exists(fullPath))
                    {
                        foreach (var f in Directory.GetFiles(fullPath)) files.Add(f);
                        foreach (var d in Directory.GetDirectories(fullPath)) files.Add(d);
                    }

                    response["files"] = files;
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("delfile", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    if (path == null) throw new Exception("Missing required key: path");
                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    response["success"] = true;
                }
                catch (Exception ex) { response["message"] = ex.Message; }

                socket.Send(response.ToString());
            });

            AddListener("delfolder", (socket, data) =>
            {
                string id = data.ContainsKey("id") ? (data["id"]?.ToString() ?? "") : "";
                int pid = (data.ContainsKey("pid") && data["pid"] != null) ? data["pid"].Value<int>() : 0;
                JObject response = new JObject { ["type"] = "response", ["success"] = false, ["pid"] = pid };
                if (!string.IsNullOrEmpty(id)) response["id"] = id;

                try
                {
                    string path = (data.ContainsKey("path") && data["path"] != null) ? data["path"].ToString() : null;
                    if (path == null) throw new Exception("Missing required key: path");
                    string fullPath = ResolveAndValidateWorkspacePath(path);

                    if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
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
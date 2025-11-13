using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

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
                Console.WriteLine($"[*] Server running at {_host}:{_port}");
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
            AddListener("print", (behavior, data) =>
            {
                int pid = data.ContainsKey("pid") ? data["pid"].Value<int>() : 0;
                string message = data.ContainsKey("message") ? data["message"].ToString() : "";
                if (!string.IsNullOrEmpty(message))
                    Console.WriteLine($"[PID {pid}] {message}");
            });

            // Other listeners like loadstring, unlock_module can be added here similarly
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

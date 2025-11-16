
	local HttpService = game:GetService("HttpService")
	local WebSocketService = game:GetService("WebSocketService")
	local RobloxReplicatedStorage = game:GetService("RobloxReplicatedStorage")
	local CorePackages = game:GetService("CorePackages")
	local Players = game:GetService("Players")

	local PROCESS_ID = %PROCESS_ID%
	local VERSION = "0.1"
	local USER_AGENT = "Oracle/0.1"

	local client = WebSocketService:CreateClient("ws://127.0.0.1:6969")

	local is_server_ready = false

	client.Opened:Connect(function()
		is_server_ready = true
	end)

	while not is_server_ready do
		task.wait(0.1)
	end

	local initMessage = {
		action = "initialize",
		pid = PROCESS_ID
	}

	client:Send(HttpService:JSONEncode(initMessage))

	local Container, Scripts, Objects = Instance.new("Folder"), Instance.new("Folder"), Instance.new("Folder")
	Container.Name = "Oracle"
	Container.Parent = RobloxReplicatedStorage

	Scripts.Name = "Scripts"
	Scripts.Parent = Container

	Objects.Name = "Objects"
	Objects.Parent = Container

	local Enviroment, Bridge, Utils = {}, {}, {}
	-- ensure on_going_requests table exists to avoid nil indexing when messages arrive
	Bridge.on_going_requests = Bridge.on_going_requests or {}

	-- utils

	function Utils:GetRandomModule()
		local children = CorePackages.Packages:GetChildren()
		local module

		while not module or module.ClassName ~= "ModuleScript" do
			module = children[math.random(#children)]
		end

		function Utils:MergeTable(a, b)
			a = a or {}
			b = b or {}
			for k, v in pairs(b) do
				a[k] = v
			end
			return a
		end

		function Utils:HttpGet(url)
			return HttpService:GetAsync(url)
		end

		local clone = module:Clone()
		clone.Name = HttpService:GenerateGUID(false)
		clone.Parent = Scripts

		return clone
	end

	-- rconsole functions forward to the bridge so host does the console work
	function rconsolecreate(title)
		local resp = Bridge:SendAndReceive({
			["action"] = "rconsolecreate",
			["title"] = title
		})
		if resp and resp["success"] and resp["console_id"] then
			return resp["console_id"]
		end
		return nil
	end

	function rconsoledestroy(id)
		local resp = Bridge:SendAndReceive({
			["action"] = "rconsoledestroy",
			["console_id"] = id
		})
		return resp and resp["success"] == true
	end

	function rconsoleclear(id)
		local resp = Bridge:SendAndReceive({
			["action"] = "rconsoleclear",
			["console_id"] = id
		})
		return resp and resp["success"] == true
	end

	function rconsoleprint(...)
		local args = { ... }
		if #args == 0 then
			-- forward an empty print (bridge will accept and no-op)
			local resp = Bridge:SendAndReceive({ ["action"] = "rconsoleprint" })
			return resp and resp["success"] == true
		end

		local outputParts = {}
		for i = 1, #args do
			table.insert(outputParts, tostring(args[i]))
		end
		local out = table.concat(outputParts, "\t")

		local resp = Bridge:SendAndReceive({
			["action"] = "rconsoleprint",
			["text"] = out
		})
		return resp and resp["success"] == true
	end

	function rconsoleinput(prompt)
		local resp = Bridge:SendAndReceive({
			["action"] = "rconsoleinput",
			["prompt"] = prompt
		})
		if resp and resp["success"] then
			return resp["text"] or ""
		end
		return ""
	end

	function rconsolesettitle(title, id)
		local resp = Bridge:SendAndReceive({
			["action"] = "rconsolesettitle",
			["title"] = title,
			["console_id"] = id
		})
		return resp and resp["success"] == true
	end

	-- Aliases
	consoleclear = rconsoleclear
	consolecreate = rconsolecreate
	consoledestroy = rconsoledestroy
	consoleinput = rconsoleinput
	consoleprint = rconsoleprint
	consolesettitle = rconsolesettitle

	function Bridge:Send(data)
		if type(data) == "string" then
			data = HttpService:JSONDecode(data)
		end
		if not data["pid"] then
			data["pid"] = PROCESS_ID
		end
		client:Send(HttpService:JSONEncode(data))
	end

	function Bridge:SendAndReceive(data, timeout)
		timeout = timeout or 15

		self.on_going_requests = self.on_going_requests or {}

		local id = HttpService:GenerateGUID(false)
		data.id = id

		local bindable_event = Instance.new("BindableEvent")
		local response_data

		local connection
		connection = bindable_event.Event:Connect(function(response)
			response_data = response
			connection:Disconnect()
		end)

		self.on_going_requests[id] = bindable_event
		self:Send(data)

		local start_time = tick()
		while not response_data do
			if tick() - start_time > timeout then
				-- on timeout, return a structured response instead of throwing
				response_data = { success = false, message = "Timeout" }
				break
			end
			task.wait(0.1)
		end

		self.on_going_requests[id] = nil
		connection:Disconnect()
		bindable_event:Destroy()

		return response_data
	end

	function Bridge:IsCompilable(source)
		local response = self:SendAndReceive({
			["action"] = "is_compilable",
			["source"] = source
		})

		if not response["success"] then
			return false, response["message"]
		end

		return true
	end

	function Bridge:UnlockModule(modulescript)
		-- Avoid ObjectValue pointer replication races by sending the module's full path
		local path = modulescript:GetFullName()
		local response = self:SendAndReceive({
			["action"] = "unlock_module",
			["script_path"] = path
		})

		if not response["success"] then
			return false, response["message"]
		end

		return true
	end

	function Bridge:Loadstring(chunk, chunk_name)
		local module = Utils:GetRandomModule()

		local response = self:SendAndReceive({
			["action"] = "loadstring",
			["chunk"] = chunk,
			["chunk_name"] = chunk_name,
			["script_name"] = module.Name
		})

		if not response["success"] then
			return nil, response["message"]
		end

		local ok, func = pcall(require, module)
		if not ok or type(func) ~= "function" then
			module:Destroy()
			return nil, "module did not return a function"
		end

		module.Parent = nil
		return func
	end

	function Bridge:Request(options)
		local response = self:SendAndReceive({
			["action"] = "request",
			["url"] = options.Url,
			["method"] = options.Method,
			["headers"] = options.Headers,
			["body"] = options.Body
		})

		if not response["success"] then
			error(response["message"], 3)
		end

		return {
			Success = response.success,
			StatusCode = response.status_code,
			StatusMessage = response.status_message,
			Headers = response.headers,
			Body = response.body
		}
	end

	-- Executing
	client.MessageReceived:Connect(function(rawData)
		local success, data = pcall(HttpService.JSONDecode, HttpService, rawData)
		if not success then 
			warn("Failed to decode JSON:", data)
			return 
		end
		
		local id, action = data.id, data.action

		-- Handle ongoing request responses first
		if id and Bridge.on_going_requests[id] then
			Bridge.on_going_requests[id]:Fire(data)
			return
		end

		local function sendResponse(resp)
			resp.type = "response"
			resp.pid = PROCESS_ID

			if id then resp.id = id end
			Bridge:Send(resp)
		end

		if action == "Execute" then
			local resp = { success = false }
			
			if not data.source then
				resp.message = "Missing source"
				sendResponse(resp)
				return
			end

			local decoded = Enviroment.base64.decode(data.source)
			local encoded = Enviroment.base64.encode(decoded)
			local func, err = Bridge:Loadstring(encoded, "Oracle")
			
			if not func then
				resp.message = tostring(err)
				return sendResponse(resp)
			end

			setfenv(func, Utils:MergeTable(getfenv(func), Enviroment))
			
			task.spawn(function()
				local execSuccess, execErr = pcall(func)
				if not execSuccess then
					warn(execErr)
				end
			end)
			
			resp.success = true
			sendResponse(resp)
		end
	end)

	-- Enviroment

	Enviroment.base64 = {}
	Enviroment.crypt = {}

	function Enviroment.base64.encode(data)
		local b = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
		if data == nil then 
			error("base64.decode expected string, got nil", 2)
		end
		return ((data:gsub('.', function(x) 
			local r,b='',x:byte()
			for i=8,1,-1 do r=r..(b%2^i-b%2^(i-1)>0 and '1' or '0') end
			return r;
		end)..'0000'):gsub('%d%d%d?%d?%d?%d?', function(x)
			if (#x < 6) then return '' end
			local c=0
			for i=1,6 do c=c+(x:sub(i,i)=='1' and 2^(6-i) or 0) end
			return b:sub(c+1,c+1)
		end)..({ '', '==', '=' })[#data%3+1])
	end
	
	function Enviroment.base64.decode(data)
		local b = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
		if data == nil then
			error("base64.decode expected string, got nil", 2)
		end
		data = string.gsub(data, '[^'..b..'=]', '')
		return (data:gsub('.', function(x)
			if (x == '=') then return '' end
			local r,f='',(b:find(x)-1)
			for i=6,1,-1 do r=r..(f%2^i-f%2^(i-1)>0 and '1' or '0') end
			return r;
		end):gsub('%d%d%d?%d?%d?%d?%d?%d?', function(x)
			if (#x ~= 8) then return '' end
			local c=0
			for i=1,8 do c=c+(x:sub(i,i)=='1' and 2^(8-i) or 0) end
			return string.char(c)
		end))
	end

	Enviroment.base64_encode = Enviroment.base64.encode
	Enviroment.base64_decode = Enviroment.base64.decode
	Enviroment.base64encode = Enviroment.base64.encode
	Enviroment.base64decode = Enviroment.base64.decode

	Enviroment.crypt = {
		base64 = Enviroment.base64,
		base64_encode = Enviroment.base64.encode,
		base64_decode = Enviroment.base64.decode,
		base64encode = Enviroment.base64.encode,
		base64decode = Enviroment.base64.decode
	}

	function Enviroment.getgenv()
		return Enviroment
	end

	local _require = require
	local unlocked_modules = setmetatable({}, { __mode = "k" })

	function Enviroment.require(modulescript)
		assert(typeof(modulescript) == "Instance", "invalid argument #1 to 'require' (ModuleScript expected, got " .. typeof(modulescript) .. ") ", 2)
		assert(modulescript.ClassName == "ModuleScript", "invalid argument #1 to 'require' (ModuleScript expected, got " .. modulescript.ClassName .. ") ", 2)

		if not unlocked_modules[modulescript] then
			local success, err = Bridge:UnlockModule(modulescript)
			if success then
				unlocked_modules[modulescript] = true
			end
		end

		for i, v in pairs(modulescript:GetDescendants()) do
			if v.ClassName == "ModuleScript" and not unlocked_modules[v] then
				local success, err = Bridge:UnlockModule(v)
				
				if success then unlocked_modules[v] = true end
			end
		end

		local parent = modulescript.Parent
		if parent then
			if parent.ClassName == "ModuleScript" and not unlocked_modules[parent] then
				local success, err = Bridge:UnlockModule(parent)
				if success then
					unlocked_modules[parent] = true
				end
			end
		end

		return _require(modulescript)
	end

	function Enviroment.request(options)
		assert(type(options) == "table", "invalid argument #1 to 'request' (table expected, got " .. type(options) .. ") ", 2)
		assert(type(options.Url) == "string", "invalid option 'Url' for argument #1 to 'request' (string expected, got " .. type(options.Url) .. ") ", 2)
		options.Method = options.Method or "GET"
		options.Method = options.Method:upper()
		assert(table.find({"GET", "POST", "PUT", "PATCH", "DELETE"}, options.Method), "invalid option 'Method' for argument #1 to 'request' (a valid http method expected, got '" .. options.Method .. "') ", 2)
		assert(not (options.Method == "GET" and options.Body), "invalid option 'Body' for argument #1 to 'request' (current method is GET but option 'Body' was used)", 2)
		if table.find({"POST", "PUT", "PATCH"}, options.Method) then
			assert(options.Body, "invalid option 'Body' for argument #1 to 'request' (current method is " .. options.Method .. " but option 'Body' was not provided)", 2)
		end
		if options.Body then
			assert(type(options.Body) == "string", "invalid option 'Body' for argument #1 to 'request' (string expected, got " .. type(options.Body) .. ") ", 2)
		end
		options.Headers = options.Headers or {}
		if options.Headers then assert(type(options.Headers) == "table", "invalid option 'Headers' for argument #1 to 'request' (table expected, got " .. type(options.Url) .. ") ", 2) end
		options.Headers["User-Agent"] = options.Headers["User-Agent"] or USER_AGENT

		return Bridge:Request(options)
	end

	local _game = game
	Enviroment.game = {}
	setmetatable(Enviroment.game, {
		__index = function(self, index)
			if index == "HttpGet" or index == "HttpGetAsync" then
				return function(self, ...)
					return Utils:HttpGet(...)
				end
			end

			if type(_game[index]) == "function" then
				return function(self, ...)
					return _game[index](_game, ...)
				end
			end

			return _game[index]
		end,

		__tostring = function(self)
			return _game.Name
		end,

		__metatable = getmetatable(_game)
	})

	Enviroment.http = {
		request = Enviroment.request
	}
	Enviroment.http_request = Enviroment.request

	function Enviroment.loadstring(chunk, chunk_name_or_use_env)
		local use_custom_env = false
		local chunk_name = "=(loadstring)"

		if type(chunk_name_or_use_env) == "string" then
			chunk_name = chunk_name_or_use_env
		elseif chunk_name_or_use_env == true then
			use_custom_env = true
		end

		local encoded_chunk = Enviroment.base64.encode(chunk)
		local compile_success, compile_error = Bridge:IsCompilable(encoded_chunk)
		if not compile_success then
			return nil, chunk_name .. tostring(compile_error)
		end

		local func, loadstring_error = Bridge:Loadstring(encoded_chunk, chunk_name)
		if not func then return nil, loadstring_error end

		if use_custom_env then
			setfenv(func, getfenv(debug.info(2, 'f')))
		end

		return func
	end

	function Enviroment.getexecutorname()
		return "Oracle"
	end

	function Enviroment.getexecutorversion()
		return "V"..VERSION
	end

	function Enviroment.identifyexecutor()
		return Enviroment.getexecutorname(), Enviroment.getexecutorversion()
	end

	-- Export convenience globals so scripts that call these directly work as expected
	identifyexecutor = Enviroment.identifyexecutor
	getexecutorname = Enviroment.getexecutorname
	getexecutorversion = Enviroment.getexecutorversion

	crypt = Enviroment.crypt
	base64 = Enviroment.base64
	base64encode = Enviroment.base64.encode
	base64decode = Enviroment.base64.decode

	-- Provide underscore-style globals expected by some scripts
	base64_encode = Enviroment.base64.encode
	base64_decode = Enviroment.base64.decode

	-- lowercase websocket alias for compatibility (some scripts use `websocket.connect`)
	websocket = WebSocket

	getscriptbytecode = function(s) return Enviroment.getscriptbytecode(s) end
	loadstring = function(chunk, name_or_env) return Enviroment.loadstring(chunk, name_or_env) end
	request = function(opts) return Enviroment.request(opts) end
	getgenv = function() return Enviroment.getgenv() end

	http = http or {}
	http.request = request
	http_request = request

	-- Clipboard helpers: forward clipboard set requests to the bridge
	function setclipboard(text)
		assert(type(text) == "string", "setclipboard expects a string")
		local resp = Bridge:SendAndReceive({
			["action"] = "setclipboard",
			["text"] = text
		})

		return resp and resp["success"] == true
	end

	-- alias
	function toclipboard(text)
		return setclipboard(text)
	end

	-- expose clipboard helpers into environment
	Enviroment.setclipboard = setclipboard
	Enviroment.toclipboard = toclipboard

	-- Filesystem functions backed by the bridge
	function readfile(path)
		assert(type(path) == "string", "readfile expects a string path")
		local resp = Bridge:SendAndReceive({ action = "readfile", path = path })
		if not resp or resp.success ~= true then error(resp and resp.message or "readfile failed") end
		return resp.text
	end

	function writefile(path, text)
		assert(type(path) == "string" and type(text) == "string", "writefile expects (path, text)")
		local resp = Bridge:SendAndReceive({ action = "writefile", path = path, text = text })
		if not resp or resp.success ~= true then error(resp and resp.message or "writefile failed") end
		return true
	end

	function appendfile(path, text)
		assert(type(path) == "string" and type(text) == "string", "appendfile expects (path, text)")
		local resp = Bridge:SendAndReceive({ action = "appendfile", path = path, text = text })
		if not resp or resp.success ~= true then error(resp and resp.message or "appendfile failed") end
		return true
	end

	function isfile(path)
		assert(type(path) == "string", "isfile expects a string path")
		local resp = Bridge:SendAndReceive({ action = "isfile", path = path })
		if not resp then return false end
		return resp.isfile == true
	end

	function isfolder(path)
		assert(type(path) == "string", "isfolder expects a string path")
		local resp = Bridge:SendAndReceive({ action = "isfolder", path = path })
		if not resp then return false end
		return resp.isfolder == true
	end

	function makefolder(path)
		assert(type(path) == "string", "makefolder expects a string path")
		local resp = Bridge:SendAndReceive({ action = "makefolder", path = path })
		if not resp or resp.success ~= true then error(resp and resp.message or "makefolder failed") end
		return true
	end

	function listfiles(path)
		assert(type(path) == "string", "listfiles expects a string path")
		-- listfiles will return list of file paths; if path is nil/empty, list current folder
		local resp = Bridge:SendAndReceive({ action = "listfiles", path = path })
		if not resp or resp.success ~= true then return {} end
		return resp.files or {}
	end

	function delfile(path)
		assert(type(path) == "string", "delfile expects a string path")
		local resp = Bridge:SendAndReceive({ action = "delfile", path = path })
		if not resp or resp.success ~= true then error(resp and resp.message or "delfile failed") end
		return true
	end

	function delfolder(path)
		assert(type(path) == "string", "delfolder expects a string path")
		local resp = Bridge:SendAndReceive({ action = "delfolder", path = path })
		if not resp or resp.success ~= true then error(resp and resp.message or "delfolder failed") end
		return true
	end

	-- expose filesystem functions into the injected environment
	Enviroment.readfile = readfile
	Enviroment.writefile = writefile
	Enviroment.appendfile = appendfile
	Enviroment.isfile = isfile
	Enviroment.isfolder = isfolder
	Enviroment.makefolder = makefolder
	Enviroment.listfiles = listfiles
	Enviroment.delfile = delfile
	Enviroment.delfolder = delfolder

-- WebSocket.connect wrapper for external websockets (returns table with Send/Close and event lists)
WebSocket = WebSocket or {}
function WebSocket.connect(url)
	assert(type(url) == "string", "WebSocket.connect expects a url string")
	local raw = WebSocketService:CreateClient(url)
	local conn = { _raw = raw, OnMessage = {}, OnClose = {}, OnOpen = {} }

	function conn:Send(msg)
		if type(msg) ~= "string" then msg = tostring(msg) end
		pcall(function() raw:Send(msg) end)
	end

	function conn:Close()
		pcall(function() raw:Close() end)
	end

	-- helper to allow table or function assignment like OnMessage = {} or OnMessage = someFunc
	local function callHandlers(list, ...)
		if type(list) == "function" then pcall(list, ...) return end
		if type(list) == "table" then
			for _, cb in ipairs(list) do if cb then pcall(cb, ...) end end
		end
	end

	if raw.MessageReceived then
		raw.MessageReceived:Connect(function(data)
			callHandlers(conn.OnMessage, data)
		end)
	end
	if raw.Opened then
		raw.Opened:Connect(function()
			callHandlers(conn.OnOpen)
		end)
	end
	if raw.Closed then
		raw.Closed:Connect(function()
			callHandlers(conn.OnClose)
		end)
	end

	return conn
end

	function Enviroment.getscriptbytecode(script)
		assert(typeof(script) == "Instance", "invalid argument #1 to 'getscriptbytecode' (LocalScript Or ModuleScript expected, got " .. typeof(script) .. ") ", 2)
		assert(script.ClassName == "ModuleScript" or script.ClassName == "LocalScript", "invalid argument #1 to 'getscriptbytecode' (LocalScript Or ModuleScript expected, got " .. script.ClassName .. ") ", 2)
		
		-- Send the script's full path to the bridge so it can locate the instance directly
		local path = script:GetFullName()
		local response = Bridge:SendAndReceive({
			["action"] = "getscriptbytecode",
			["script_path"] = path
		})

		if not response["success"] then
			error(response["message"], 2)
		end

		return Enviroment.base64.decode(response["bytecode"])
	end

-- expose websocket into environment
Enviroment.WebSocket = WebSocket
Enviroment.websocket = WebSocket

-- rconsole functions: expose to environment and provide rconsolename alias
Enviroment.rconsolecreate = rconsolecreate
Enviroment.rconsoledestroy = rconsoledestroy
Enviroment.rconsoleclear = rconsoleclear
Enviroment.rconsoleprint = rconsoleprint
Enviroment.rconsoleinput = rconsoleinput
Enviroment.rconsolesettitle = rconsolesettitle
Enviroment.rconsolename = rconsolesettitle

-- globals aliases too
rconsolename = rconsolesettitle

	game:GetService("StarterGui"):SetCore("SendNotification",{
		Title = "Oracle", -- Required
		Text = "Injected", -- Required
	})
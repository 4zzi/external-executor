
	local HttpService = game:GetService("HttpService")
	local WebSocketService = game:GetService("WebSocketService")
	local RobloxReplicatedStorage = game:GetService("RobloxReplicatedStorage")
	local CorePackages = game:GetService("CorePackages")
	local Players = game:GetService("Players")

	local PROCESS_ID = %PROCESS_ID%
	local VERSION = "0.1"

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

	-- utils

	function Utils:GetRandomModule()
		local children = CorePackages.Packages:GetChildren()
		local module

		while not module or module.ClassName ~= "ModuleScript" do
			module = children[math.random(#children)]
		end

		local clone = module:Clone()
		clone.Name = HttpService:GenerateGUID(false)
		clone.Parent = Scripts

		return clone
	end

	function Utils:MergeTable(t1, t2)
		for i, v in pairs(t2) do
			t1[i] = v
		end
		return t1
	end

	function Utils:CreatePointer()
		local pointer = Instance.new("ObjectValue")
		pointer.Name = HttpService:GenerateGUID(false)
		pointer.Parent = Objects

		return pointer
	end

	function Utils:HttpGet(url, return_raw)
		assert(type(url) == "string", "invalid argument #1 to 'HttpGet' (string expected, got " .. type(url) .. ") ", 2)

		if return_raw == nil then
			return_raw = true
		end

		local response = Enviroment.request({
			Url = url,
			Method = "GET",
		})

		if not response then
			error("HttpGet failed: Enviroment.request returned nil")
		end

		if return_raw then
			return response.Body
		end

		return HttpService:JSONDecode(response.Body)
	end

	-- bridge
	Bridge.on_going_requests = {}

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
		timeout = timeout or 5

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
				break
			end
			task.wait(0.1)
		end

		self.on_going_requests[id] = nil
		connection:Disconnect()
		bindable_event:Destroy()

		if not response_data then  
			error("Timeout")
		end

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
		local pointer = Utils:CreatePointer()
		pointer.Value = modulescript

		local response = self:SendAndReceive({
			["action"] = "unlock_module",
			["pointer_name"] = pointer.Name
		})

		pointer:Destroy()

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

	function Enviroment.getEnviromentname()
		return "Oracle"
	end

	function Enviroment.getEnviromentversion()
		return "V"..VERSION
	end

	function Enviroment.identifyEnviroment()
		return Enviroment.getEnviromentname(), Enviroment.getEnviromentversion()
	end

	function Enviroment.getscriptbytecode(script)
		assert(typeof(script) == "Instance", "invalid argument #1 to 'getscriptbytecode' (LocalScript Or ModuleScript expected, got " .. typeof(script) .. ") ", 2)
		assert(script.ClassName == "ModuleScript" or script.ClassName == "LocalScript", "invalid argument #1 to 'getscriptbytecode' (LocalScript Or ModuleScript expected, got " .. script.ClassName .. ") ", 2)
		
		local pointer = Utils:CreatePointer()
		pointer.Value = script

		local response = Bridge:SendAndReceive({
			["action"] = "getscriptbytecode",
			["pointer_name"] = pointer.Name
		})

		pointer:Destroy()

		if not response["success"] then
			error(response["message"], 2)
		end

		return Enviroment.base64.decode(response["bytecode"])
	end

	game:GetService("StarterGui"):SetCore("SendNotification",{
		Title = "Oracle", -- Required
		Text = "Injected", -- Required
	})
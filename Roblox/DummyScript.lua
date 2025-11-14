-- 93 147 181
script.Parent = nil

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

-- bridge

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

	if not response_data then --[[error("[Executor] timeout waiting for response " .. id, 2)]] end

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

	local response =self:SendAndReceive({
		["action"] = "loadstring",
		["chunk"] = chunk,
		["chunk_name"] = chunk_name,
		["script_name"] = module.Name
	})

	if not response["success"] then
		return nil, response["message"]
	end

	local func = require(module)

	if debug.info(func, "n") ~= chunk_name then
		module:Destroy()
		return nil, "function does not match"
	end

	module.Parent = nil
	return func
end

-- executing
client.MessageReceived:Connect(function(data)
	local success, decoded = pcall(function()
		return HttpService:JSONDecode(data)
	end)
	
	if not success then return end
	
	local action = decoded.action
	
	if action == "Execute" then
		if decoded.source then
			local func, err = Bridge:Loadstring(data["source"], "Oracle")
			setfenv(func, Utils:MergeTable(getfenv(func), Enviroment))
			task.spawn(func)
		end
	end
end)

-- Enviroment

Enviroment.base64 = {}
Enviroment.crypt = {}

function Enviroment.base64.encode(data)
    local b = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
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

function Enviroment.loadstring(chunk, chunk_name)
	assert(type(chunk) == "string", "invalid argument #1 to 'loadstring' (string expected, got " .. type(chunk) .. ") ", 2)
	chunk_name = chunk_name or "loadstring"
	assert(type(chunk_name) == "string", "invalid argument #2 to 'loadstring' (string expected, got " .. type(chunk_name) .. ") ", 2)
	chunk_name = chunk_name:gsub("[^%a_]", "")

	local encoded_chunk = Enviroment.base64.encode(chunk)

	local compile_success, compile_error = Bridge:IsCompilable(encoded_chunk)
	if not compile_success then
		return nil, chunk_name .. tostring(compile_error)
	end

	local func, loadstring_error = Bridge:Loadstring(encoded_chunk, chunk_name)

	if not func then
		return nil, loadstring_error
	end

	setfenv(func, getfenv(debug.info(2, 'f')))

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

while true do
	task.wait(9e9)
end
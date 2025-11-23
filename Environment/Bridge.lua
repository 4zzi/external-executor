    -- 11/18/2025 11:10 PM 4zzi

    local HttpService = game:GetService("HttpService")
    local WebSocketService = game:GetService("WebSocketService")
    local UGCValidationService = game:GetService("UGCValidationService")
    local RobloxReplicatedStorage = game:GetService("RobloxReplicatedStorage")
    local CorePackages = game:GetService("CorePackages")
    local common = game:GetService("CoreGui").RobloxGui.Modules.Common
    local commonutil = common.CommonUtil
    local Players = game:GetService("Players")
    local RunService = game:GetService("RunService")
    local InsertService = game:GetService("InsertService")
    local UserInputService = game:GetService("UserInputService")

    local PROCESS_ID = %PROCESS_ID%
    local VERSION = "1.0"
    local USER_AGENT = "Oracle/" .. VERSION

    local CoreGui = game:GetService("CoreGui")

    local Oracle = Instance.new("Folder", CoreGui)
    Oracle.Name = "Oracle"

    local Pointer = Instance.new("Folder", Oracle)
    Pointer.Name = "Pointer"

    local HUI = Instance.new("ScreenGui", CoreGui)
    HUI.Name = "hidden_ui_container"

    local Container = Instance.new("Folder")
    Container.Name = "Oracle"
    Container.Parent = RobloxReplicatedStorage

    local Scripts = Instance.new("Folder")
    Scripts.Name = "Scripts"
    Scripts.Parent = Container

    local proxyobjects = Instance.new("Folder")
    proxyobjects.Name = "proxyobjects"
    proxyobjects.Parent = Container

    local constants = commonutil:Clone()
    constants.Name = "Constants"
    constants.Parent = common -- getscriptclosure fix

    --=============================================================================--
    -- ## WebSocket Client Initialization
    --=============================================================================--

    local client = WebSocketService:CreateClient("ws://127.0.0.1:6969")
    local is_server_ready = false

    -- Wait for the WebSocket connection to open
    client.Opened:Connect(function()
        is_server_ready = true
    end)

    while not is_server_ready do
        task.wait(0.1)
    end

    --=============================================================================--
    -- ## Hash Lib
    --=============================================================================--

    local Alphabet = {}
    local Indexes = {}

    -- A-Z
    for Index = 65, 90 do
        table.insert(Alphabet, Index)
    end

    -- a-z
    for Index = 97, 122 do
        table.insert(Alphabet, Index)
    end

    -- 0-9
    for Index = 48, 57 do
        table.insert(Alphabet, Index)
    end

    table.insert(Alphabet, 43) -- +
    table.insert(Alphabet, 47) -- /

    for Index, Character in ipairs(Alphabet) do
        Indexes[Character] = Index
    end

    local Base64 = {}

    local bit32_rshift = bit32.rshift
    local bit32_lshift = bit32.lshift
    local bit32_band = bit32.band

    --[[**
        Encodes a string in Base64.
        @param [t:string] Input The input string to encode.
        @returns [t:string] The string encoded in Base64.
    **--]]

    function Base64.Encode(Input)
        local Output = {}
        local Length = 0

        for Index = 1, #Input, 3 do
            local C1, C2, C3 = string.byte(Input, Index, Index + 2)

            local A = bit32_rshift(C1, 2)
            local B = bit32_lshift(bit32_band(C1, 3), 4) + bit32_rshift(C2 or 0, 4)
            local C = bit32_lshift(bit32_band(C2 or 0, 15), 2) + bit32_rshift(C3 or 0, 6)
            local D = bit32_band(C3 or 0, 63)

            Length = Length + 1
            Output[Length] = Alphabet[A + 1]

            Length = Length + 1
            Output[Length] = Alphabet[B + 1]

            Length = Length + 1
            Output[Length] = C2 and Alphabet[C + 1] or 61

            Length = Length + 1
            Output[Length] = C3 and Alphabet[D + 1] or 61
        end

        local NewOutput = {}
        local NewLength = 0
        local IndexAdd4096Sub1

        for Index = 1, Length, 4096 do
            NewLength = NewLength + 1
            IndexAdd4096Sub1 = Index + 4096 - 1

            NewOutput[NewLength] = string.char(table.unpack(
                Output,
                Index,
                IndexAdd4096Sub1 > Length and Length or IndexAdd4096Sub1
                ))
        end

        return table.concat(NewOutput)
    end

    --[[**
        Decodes a string from Base64.
        @param [t:string] Input The input string to decode.
        @returns [t:string] The newly decoded string.
    **--]]
    function Base64.Decode(Input)
        local Output = {}
        local Length = 0

        for Index = 1, #Input, 4 do
            local C1, C2, C3, C4 = string.byte(Input, Index, Index + 3)

            local I1 = Indexes[C1] - 1
            local I2 = Indexes[C2] - 1
            local I3 = (Indexes[C3] or 1) - 1
            local I4 = (Indexes[C4] or 1) - 1

            local A = bit32_lshift(I1, 2) + bit32_rshift(I2, 4)
            local B = bit32_lshift(bit32_band(I2, 15), 4) + bit32_rshift(I3, 2)
            local C = bit32_lshift(bit32_band(I3, 3), 6) + I4

            Length = Length + 1
            Output[Length] = A

            if C3 ~= 61 then
                Length = Length + 1
                Output[Length] = B
            end

            if C4 ~= 61 then
                Length = Length + 1
                Output[Length] = C
            end
        end

        local NewOutput = {}
        local NewLength = 0
        local IndexAdd4096Sub1

        for Index = 1, Length, 4096 do
            NewLength = NewLength + 1
            IndexAdd4096Sub1 = Index + 4096 - 1

            NewOutput[NewLength] = string.char(table.unpack(
                Output,
                Index,
                IndexAdd4096Sub1 > Length and Length or IndexAdd4096Sub1
                ))
        end

        return table.concat(NewOutput)
    end

    --=============================================================================---
    -- LOCALIZATION FOR VM OPTIMIZATIONS
    --=============================================================================---

    local ipairs = ipairs

    --=============================================================================---
    -- 32-BIT BITWISE FUNCTIONS
    --=============================================================================---
    -- Only low 32 bits of function arguments matter, high bits are ignored
    -- The result of all functions (except HEX) is an integer inside "correct range":
    -- for "bit" library:    (-TWO_POW_31)..(TWO_POW_31-1)
    -- for "bit32" library:        0..(TWO_POW_32-1)
    local bit32_band = bit32.band -- 2 arguments
    local bit32_bor = bit32.bor -- 2 arguments
    local bit32_bxor = bit32.bxor -- 2..5 arguments
    local bit32_lshift = bit32.lshift -- second argument is integer 0..31
    local bit32_rshift = bit32.rshift -- second argument is integer 0..31
    local bit32_lrotate = bit32.lrotate -- second argument is integer 0..31
    local bit32_rrotate = bit32.rrotate -- second argument is integer 0..31

    --=============================================================================---
    -- CREATING OPTIMIZED INNER LOOP
    --=============================================================================---
    -- Arrays of SHA2 "magic numbers" (in "INT64" and "FFI" branches "*_lo" arrays contain 64-bit values)
    local sha2_K_lo, sha2_K_hi, sha2_H_lo, sha2_H_hi, sha3_RC_lo, sha3_RC_hi = {}, {}, {}, {}, {}, {}
    local sha2_H_ext256 = {
        [224] = {};
        [256] = sha2_H_hi;
    }

    local sha2_H_ext512_lo, sha2_H_ext512_hi = {
        [384] = {};
        [512] = sha2_H_lo;
    }, {
        [384] = {};
        [512] = sha2_H_hi;
    }

    local md5_K, md5_sha1_H = {}, {0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0}
    local md5_next_shift = {0, 0, 0, 0, 0, 0, 0, 0, 28, 25, 26, 27, 0, 0, 10, 9, 11, 12, 0, 15, 16, 17, 18, 0, 20, 22, 23, 21}
    local HEX64, XOR64A5, lanes_index_base -- defined only for branches that internally use 64-bit integers: "INT64" and "FFI"
    local common_W = {} -- temporary table shared between all calculations (to avoid creating new temporary table every time)
    local K_lo_modulo, hi_factor, hi_factor_keccak = 4294967296, 0, 0

    local TWO_POW_NEG_56 = 2 ^ -56
    local TWO_POW_NEG_17 = 2 ^ -17

    local TWO_POW_2 = 2 ^ 2
    local TWO_POW_3 = 2 ^ 3
    local TWO_POW_4 = 2 ^ 4
    local TWO_POW_5 = 2 ^ 5
    local TWO_POW_6 = 2 ^ 6
    local TWO_POW_7 = 2 ^ 7
    local TWO_POW_8 = 2 ^ 8
    local TWO_POW_9 = 2 ^ 9
    local TWO_POW_10 = 2 ^ 10
    local TWO_POW_11 = 2 ^ 11
    local TWO_POW_12 = 2 ^ 12
    local TWO_POW_13 = 2 ^ 13
    local TWO_POW_14 = 2 ^ 14
    local TWO_POW_15 = 2 ^ 15
    local TWO_POW_16 = 2 ^ 16
    local TWO_POW_17 = 2 ^ 17
    local TWO_POW_18 = 2 ^ 18
    local TWO_POW_19 = 2 ^ 19
    local TWO_POW_20 = 2 ^ 20
    local TWO_POW_21 = 2 ^ 21
    local TWO_POW_22 = 2 ^ 22
    local TWO_POW_23 = 2 ^ 23
    local TWO_POW_24 = 2 ^ 24
    local TWO_POW_25 = 2 ^ 25
    local TWO_POW_26 = 2 ^ 26
    local TWO_POW_27 = 2 ^ 27
    local TWO_POW_28 = 2 ^ 28
    local TWO_POW_29 = 2 ^ 29
    local TWO_POW_30 = 2 ^ 30
    local TWO_POW_31 = 2 ^ 31
    local TWO_POW_32 = 2 ^ 32
    local TWO_POW_40 = 2 ^ 40

    local TWO56_POW_7 = 256 ^ 7

    -- Implementation for Lua 5.1/5.2 (with or without bitwise library available)
    local function sha256_feed_64(H, str, offs, size)
        -- offs >= 0, size >= 0, size is multiple of 64
        local W, K = common_W, sha2_K_hi
        local h1, h2, h3, h4, h5, h6, h7, h8 = H[1], H[2], H[3], H[4], H[5], H[6], H[7], H[8]
        for pos = offs, offs + size - 1, 64 do
            for j = 1, 16 do
                pos = pos + 4
                local a, b, c, d = string.byte(str, pos - 3, pos)
                W[j] = ((a * 256 + b) * 256 + c) * 256 + d
            end

            for j = 17, 64 do
                local a, b = W[j - 15], W[j - 2]
                W[j] = bit32_bxor(bit32_rrotate(a, 7), bit32_lrotate(a, 14), bit32_rshift(a, 3)) + bit32_bxor(bit32_lrotate(b, 15), bit32_lrotate(b, 13), bit32_rshift(b, 10)) + W[j - 7] + W[j - 16]
            end

            local a, b, c, d, e, f, g, h = h1, h2, h3, h4, h5, h6, h7, h8
            for j = 1, 64 do
                local z = bit32_bxor(bit32_rrotate(e, 6), bit32_rrotate(e, 11), bit32_lrotate(e, 7)) + bit32_band(e, f) + bit32_band(-1 - e, g) + h + K[j] + W[j]
                h = g
                g = f
                f = e
                e = z + d
                d = c
                c = b
                b = a
                a = z + bit32_band(d, c) + bit32_band(a, bit32_bxor(d, c)) + bit32_bxor(bit32_rrotate(a, 2), bit32_rrotate(a, 13), bit32_lrotate(a, 10))
            end

            h1, h2, h3, h4 = (a + h1) % 4294967296, (b + h2) % 4294967296, (c + h3) % 4294967296, (d + h4) % 4294967296
            h5, h6, h7, h8 = (e + h5) % 4294967296, (f + h6) % 4294967296, (g + h7) % 4294967296, (h + h8) % 4294967296
        end

        H[1], H[2], H[3], H[4], H[5], H[6], H[7], H[8] = h1, h2, h3, h4, h5, h6, h7, h8
    end

    local function sha512_feed_128(H_lo, H_hi, str, offs, size)
        -- offs >= 0, size >= 0, size is multiple of 128
        -- W1_hi, W1_lo, W2_hi, W2_lo, ...   Wk_hi = W[2*k-1], Wk_lo = W[2*k]
        local W, K_lo, K_hi = common_W, sha2_K_lo, sha2_K_hi
        local h1_lo, h2_lo, h3_lo, h4_lo, h5_lo, h6_lo, h7_lo, h8_lo = H_lo[1], H_lo[2], H_lo[3], H_lo[4], H_lo[5], H_lo[6], H_lo[7], H_lo[8]
        local h1_hi, h2_hi, h3_hi, h4_hi, h5_hi, h6_hi, h7_hi, h8_hi = H_hi[1], H_hi[2], H_hi[3], H_hi[4], H_hi[5], H_hi[6], H_hi[7], H_hi[8]
        for pos = offs, offs + size - 1, 128 do
            for j = 1, 16 * 2 do
                pos = pos + 4
                local a, b, c, d = string.byte(str, pos - 3, pos)
                W[j] = ((a * 256 + b) * 256 + c) * 256 + d
            end

            for jj = 34, 160, 2 do
                local a_lo, a_hi, b_lo, b_hi = W[jj - 30], W[jj - 31], W[jj - 4], W[jj - 5]
                local tmp1 = bit32_bxor(bit32_rshift(a_lo, 1) + bit32_lshift(a_hi, 31), bit32_rshift(a_lo, 8) + bit32_lshift(a_hi, 24), bit32_rshift(a_lo, 7) + bit32_lshift(a_hi, 25)) % 4294967296 +
                    bit32_bxor(bit32_rshift(b_lo, 19) + bit32_lshift(b_hi, 13), bit32_lshift(b_lo, 3) + bit32_rshift(b_hi, 29), bit32_rshift(b_lo, 6) + bit32_lshift(b_hi, 26)) % 4294967296 +
                    W[jj - 14] + W[jj - 32]

                local tmp2 = tmp1 % 4294967296
                W[jj - 1] = bit32_bxor(bit32_rshift(a_hi, 1) + bit32_lshift(a_lo, 31), bit32_rshift(a_hi, 8) + bit32_lshift(a_lo, 24), bit32_rshift(a_hi, 7)) +
                    bit32_bxor(bit32_rshift(b_hi, 19) + bit32_lshift(b_lo, 13), bit32_lshift(b_hi, 3) + bit32_rshift(b_lo, 29), bit32_rshift(b_hi, 6)) +
                    W[jj - 15] + W[jj - 33] + (tmp1 - tmp2) / 4294967296

                W[jj] = tmp2
            end

            local a_lo, b_lo, c_lo, d_lo, e_lo, f_lo, g_lo, h_lo = h1_lo, h2_lo, h3_lo, h4_lo, h5_lo, h6_lo, h7_lo, h8_lo
            local a_hi, b_hi, c_hi, d_hi, e_hi, f_hi, g_hi, h_hi = h1_hi, h2_hi, h3_hi, h4_hi, h5_hi, h6_hi, h7_hi, h8_hi
            for j = 1, 80 do
                local jj = 2 * j
                local tmp1 = bit32_bxor(bit32_rshift(e_lo, 14) + bit32_lshift(e_hi, 18), bit32_rshift(e_lo, 18) + bit32_lshift(e_hi, 14), bit32_lshift(e_lo, 23) + bit32_rshift(e_hi, 9)) % 4294967296 +
                    (bit32_band(e_lo, f_lo) + bit32_band(-1 - e_lo, g_lo)) % 4294967296 +
                    h_lo + K_lo[j] + W[jj]

                local z_lo = tmp1 % 4294967296
                local z_hi = bit32_bxor(bit32_rshift(e_hi, 14) + bit32_lshift(e_lo, 18), bit32_rshift(e_hi, 18) + bit32_lshift(e_lo, 14), bit32_lshift(e_hi, 23) + bit32_rshift(e_lo, 9)) +
                    bit32_band(e_hi, f_hi) + bit32_band(-1 - e_hi, g_hi) +
                    h_hi + K_hi[j] + W[jj - 1] +
                    (tmp1 - z_lo) / 4294967296

                h_lo = g_lo
                h_hi = g_hi
                g_lo = f_lo
                g_hi = f_hi
                f_lo = e_lo
                f_hi = e_hi
                tmp1 = z_lo + d_lo
                e_lo = tmp1 % 4294967296
                e_hi = z_hi + d_hi + (tmp1 - e_lo) / 4294967296
                d_lo = c_lo
                d_hi = c_hi
                c_lo = b_lo
                c_hi = b_hi
                b_lo = a_lo
                b_hi = a_hi
                tmp1 = z_lo + (bit32_band(d_lo, c_lo) + bit32_band(b_lo, bit32_bxor(d_lo, c_lo))) % 4294967296 + bit32_bxor(bit32_rshift(b_lo, 28) + bit32_lshift(b_hi, 4), bit32_lshift(b_lo, 30) + bit32_rshift(b_hi, 2), bit32_lshift(b_lo, 25) + bit32_rshift(b_hi, 7)) % 4294967296
                a_lo = tmp1 % 4294967296
                a_hi = z_hi + (bit32_band(d_hi, c_hi) + bit32_band(b_hi, bit32_bxor(d_hi, c_hi))) + bit32_bxor(bit32_rshift(b_hi, 28) + bit32_lshift(b_lo, 4), bit32_lshift(b_hi, 30) + bit32_rshift(b_lo, 2), bit32_lshift(b_hi, 25) + bit32_rshift(b_lo, 7)) + (tmp1 - a_lo) / 4294967296
            end

            a_lo = h1_lo + a_lo
            h1_lo = a_lo % 4294967296
            h1_hi = (h1_hi + a_hi + (a_lo - h1_lo) / 4294967296) % 4294967296
            a_lo = h2_lo + b_lo
            h2_lo = a_lo % 4294967296
            h2_hi = (h2_hi + b_hi + (a_lo - h2_lo) / 4294967296) % 4294967296
            a_lo = h3_lo + c_lo
            h3_lo = a_lo % 4294967296
            h3_hi = (h3_hi + c_hi + (a_lo - h3_lo) / 4294967296) % 4294967296
            a_lo = h4_lo + d_lo
            h4_lo = a_lo % 4294967296
            h4_hi = (h4_hi + d_hi + (a_lo - h4_lo) / 4294967296) % 4294967296
            a_lo = h5_lo + e_lo
            h5_lo = a_lo % 4294967296
            h5_hi = (h5_hi + e_hi + (a_lo - h5_lo) / 4294967296) % 4294967296
            a_lo = h6_lo + f_lo
            h6_lo = a_lo % 4294967296
            h6_hi = (h6_hi + f_hi + (a_lo - h6_lo) / 4294967296) % 4294967296
            a_lo = h7_lo + g_lo
            h7_lo = a_lo % 4294967296
            h7_hi = (h7_hi + g_hi + (a_lo - h7_lo) / 4294967296) % 4294967296
            a_lo = h8_lo + h_lo
            h8_lo = a_lo % 4294967296
            h8_hi = (h8_hi + h_hi + (a_lo - h8_lo) / 4294967296) % 4294967296
        end

        H_lo[1], H_lo[2], H_lo[3], H_lo[4], H_lo[5], H_lo[6], H_lo[7], H_lo[8] = h1_lo, h2_lo, h3_lo, h4_lo, h5_lo, h6_lo, h7_lo, h8_lo
        H_hi[1], H_hi[2], H_hi[3], H_hi[4], H_hi[5], H_hi[6], H_hi[7], H_hi[8] = h1_hi, h2_hi, h3_hi, h4_hi, h5_hi, h6_hi, h7_hi, h8_hi
    end

    local function md5_feed_64(H, str, offs, size)
        -- offs >= 0, size >= 0, size is multiple of 64
        local W, K, md5_next_shift = common_W, md5_K, md5_next_shift
        local h1, h2, h3, h4 = H[1], H[2], H[3], H[4]
        for pos = offs, offs + size - 1, 64 do
            for j = 1, 16 do
                pos = pos + 4
                local a, b, c, d = string.byte(str, pos - 3, pos)
                W[j] = ((d * 256 + c) * 256 + b) * 256 + a
            end

            local a, b, c, d = h1, h2, h3, h4
            local s = 25
            for j = 1, 16 do
                local F = bit32_rrotate(bit32_band(b, c) + bit32_band(-1 - b, d) + a + K[j] + W[j], s) + b
                s = md5_next_shift[s]
                a = d
                d = c
                c = b
                b = F
            end

            s = 27
            for j = 17, 32 do
                local F = bit32_rrotate(bit32_band(d, b) + bit32_band(-1 - d, c) + a + K[j] + W[(5 * j - 4) % 16 + 1], s) + b
                s = md5_next_shift[s]
                a = d
                d = c
                c = b
                b = F
            end

            s = 28
            for j = 33, 48 do
                local F = bit32_rrotate(bit32_bxor(bit32_bxor(b, c), d) + a + K[j] + W[(3 * j + 2) % 16 + 1], s) + b
                s = md5_next_shift[s]
                a = d
                d = c
                c = b
                b = F
            end

            s = 26
            for j = 49, 64 do
                local F = bit32_rrotate(bit32_bxor(c, bit32_bor(b, -1 - d)) + a + K[j] + W[(j * 7 - 7) % 16 + 1], s) + b
                s = md5_next_shift[s]
                a = d
                d = c
                c = b
                b = F
            end

            h1 = (a + h1) % 4294967296
            h2 = (b + h2) % 4294967296
            h3 = (c + h3) % 4294967296
            h4 = (d + h4) % 4294967296
        end

        H[1], H[2], H[3], H[4] = h1, h2, h3, h4
    end

    local function sha1_feed_64(H, str, offs, size)
        -- offs >= 0, size >= 0, size is multiple of 64
        local W = common_W
        local h1, h2, h3, h4, h5 = H[1], H[2], H[3], H[4], H[5]
        for pos = offs, offs + size - 1, 64 do
            for j = 1, 16 do
                pos = pos + 4
                local a, b, c, d = string.byte(str, pos - 3, pos)
                W[j] = ((a * 256 + b) * 256 + c) * 256 + d
            end

            for j = 17, 80 do
                W[j] = bit32_lrotate(bit32_bxor(W[j - 3], W[j - 8], W[j - 14], W[j - 16]), 1)
            end

            local a, b, c, d, e = h1, h2, h3, h4, h5
            for j = 1, 20 do
                local z = bit32_lrotate(a, 5) + bit32_band(b, c) + bit32_band(-1 - b, d) + 0x5A827999 + W[j] + e -- constant = math.floor(TWO_POW_30 * sqrt(2))
                e = d
                d = c
                c = bit32_rrotate(b, 2)
                b = a
                a = z
            end

            for j = 21, 40 do
                local z = bit32_lrotate(a, 5) + bit32_bxor(b, c, d) + 0x6ED9EBA1 + W[j] + e -- TWO_POW_30 * sqrt(3)
                e = d
                d = c
                c = bit32_rrotate(b, 2)
                b = a
                a = z
            end

            for j = 41, 60 do
                local z = bit32_lrotate(a, 5) + bit32_band(d, c) + bit32_band(b, bit32_bxor(d, c)) + 0x8F1BBCDC + W[j] + e -- TWO_POW_30 * sqrt(5)
                e = d
                d = c
                c = bit32_rrotate(b, 2)
                b = a
                a = z
            end

            for j = 61, 80 do
                local z = bit32_lrotate(a, 5) + bit32_bxor(b, c, d) + 0xCA62C1D6 + W[j] + e -- TWO_POW_30 * sqrt(10)
                e = d
                d = c
                c = bit32_rrotate(b, 2)
                b = a
                a = z
            end

            h1 = (a + h1) % 4294967296
            h2 = (b + h2) % 4294967296
            h3 = (c + h3) % 4294967296
            h4 = (d + h4) % 4294967296
            h5 = (e + h5) % 4294967296
        end

        H[1], H[2], H[3], H[4], H[5] = h1, h2, h3, h4, h5
    end

    local function keccak_feed(lanes_lo, lanes_hi, str, offs, size, block_size_in_bytes)
        -- This is an example of a Lua function having 79 local variables :-)
        -- offs >= 0, size >= 0, size is multiple of block_size_in_bytes, block_size_in_bytes is positive multiple of 8
        local RC_lo, RC_hi = sha3_RC_lo, sha3_RC_hi
        local qwords_qty = block_size_in_bytes / 8
        for pos = offs, offs + size - 1, block_size_in_bytes do
            for j = 1, qwords_qty do
                local a, b, c, d = string.byte(str, pos + 1, pos + 4)
                lanes_lo[j] = bit32_bxor(lanes_lo[j], ((d * 256 + c) * 256 + b) * 256 + a)
                pos = pos + 8
                a, b, c, d = string.byte(str, pos - 3, pos)
                lanes_hi[j] = bit32_bxor(lanes_hi[j], ((d * 256 + c) * 256 + b) * 256 + a)
            end

            local L01_lo, L01_hi, L02_lo, L02_hi, L03_lo, L03_hi, L04_lo, L04_hi, L05_lo, L05_hi, L06_lo, L06_hi, L07_lo, L07_hi, L08_lo, L08_hi, L09_lo, L09_hi, L10_lo, L10_hi, L11_lo, L11_hi, L12_lo, L12_hi, L13_lo, L13_hi, L14_lo, L14_hi, L15_lo, L15_hi, L16_lo, L16_hi, L17_lo, L17_hi, L18_lo, L18_hi, L19_lo, L19_hi, L20_lo, L20_hi, L21_lo, L21_hi, L22_lo, L22_hi, L23_lo, L23_hi, L24_lo, L24_hi, L25_lo, L25_hi = lanes_lo[1], lanes_hi[1], lanes_lo[2], lanes_hi[2], lanes_lo[3], lanes_hi[3], lanes_lo[4], lanes_hi[4], lanes_lo[5], lanes_hi[5], lanes_lo[6], lanes_hi[6], lanes_lo[7], lanes_hi[7], lanes_lo[8], lanes_hi[8], lanes_lo[9], lanes_hi[9], lanes_lo[10], lanes_hi[10], lanes_lo[11], lanes_hi[11], lanes_lo[12], lanes_hi[12], lanes_lo[13], lanes_hi[13], lanes_lo[14], lanes_hi[14], lanes_lo[15], lanes_hi[15], lanes_lo[16], lanes_hi[16], lanes_lo[17], lanes_hi[17], lanes_lo[18], lanes_hi[18], lanes_lo[19], lanes_hi[19], lanes_lo[20], lanes_hi[20], lanes_lo[21], lanes_hi[21], lanes_lo[22], lanes_hi[22], lanes_lo[23], lanes_hi[23], lanes_lo[24], lanes_hi[24], lanes_lo[25], lanes_hi[25]

            for round_idx = 1, 24 do
                local C1_lo = bit32_bxor(L01_lo, L06_lo, L11_lo, L16_lo, L21_lo)
                local C1_hi = bit32_bxor(L01_hi, L06_hi, L11_hi, L16_hi, L21_hi)
                local C2_lo = bit32_bxor(L02_lo, L07_lo, L12_lo, L17_lo, L22_lo)
                local C2_hi = bit32_bxor(L02_hi, L07_hi, L12_hi, L17_hi, L22_hi)
                local C3_lo = bit32_bxor(L03_lo, L08_lo, L13_lo, L18_lo, L23_lo)
                local C3_hi = bit32_bxor(L03_hi, L08_hi, L13_hi, L18_hi, L23_hi)
                local C4_lo = bit32_bxor(L04_lo, L09_lo, L14_lo, L19_lo, L24_lo)
                local C4_hi = bit32_bxor(L04_hi, L09_hi, L14_hi, L19_hi, L24_hi)
                local C5_lo = bit32_bxor(L05_lo, L10_lo, L15_lo, L20_lo, L25_lo)
                local C5_hi = bit32_bxor(L05_hi, L10_hi, L15_hi, L20_hi, L25_hi)

                local D_lo = bit32_bxor(C1_lo, C3_lo * 2 + (C3_hi % TWO_POW_32 - C3_hi % TWO_POW_31) / TWO_POW_31)
                local D_hi = bit32_bxor(C1_hi, C3_hi * 2 + (C3_lo % TWO_POW_32 - C3_lo % TWO_POW_31) / TWO_POW_31)

                local T0_lo = bit32_bxor(D_lo, L02_lo)
                local T0_hi = bit32_bxor(D_hi, L02_hi)
                local T1_lo = bit32_bxor(D_lo, L07_lo)
                local T1_hi = bit32_bxor(D_hi, L07_hi)
                local T2_lo = bit32_bxor(D_lo, L12_lo)
                local T2_hi = bit32_bxor(D_hi, L12_hi)
                local T3_lo = bit32_bxor(D_lo, L17_lo)
                local T3_hi = bit32_bxor(D_hi, L17_hi)
                local T4_lo = bit32_bxor(D_lo, L22_lo)
                local T4_hi = bit32_bxor(D_hi, L22_hi)

                L02_lo = (T1_lo % TWO_POW_32 - T1_lo % TWO_POW_20) / TWO_POW_20 + T1_hi * TWO_POW_12
                L02_hi = (T1_hi % TWO_POW_32 - T1_hi % TWO_POW_20) / TWO_POW_20 + T1_lo * TWO_POW_12
                L07_lo = (T3_lo % TWO_POW_32 - T3_lo % TWO_POW_19) / TWO_POW_19 + T3_hi * TWO_POW_13
                L07_hi = (T3_hi % TWO_POW_32 - T3_hi % TWO_POW_19) / TWO_POW_19 + T3_lo * TWO_POW_13
                L12_lo = T0_lo * 2 + (T0_hi % TWO_POW_32 - T0_hi % TWO_POW_31) / TWO_POW_31
                L12_hi = T0_hi * 2 + (T0_lo % TWO_POW_32 - T0_lo % TWO_POW_31) / TWO_POW_31
                L17_lo = T2_lo * TWO_POW_10 + (T2_hi % TWO_POW_32 - T2_hi % TWO_POW_22) / TWO_POW_22
                L17_hi = T2_hi * TWO_POW_10 + (T2_lo % TWO_POW_32 - T2_lo % TWO_POW_22) / TWO_POW_22
                L22_lo = T4_lo * TWO_POW_2 + (T4_hi % TWO_POW_32 - T4_hi % TWO_POW_30) / TWO_POW_30
                L22_hi = T4_hi * TWO_POW_2 + (T4_lo % TWO_POW_32 - T4_lo % TWO_POW_30) / TWO_POW_30

                D_lo = bit32_bxor(C2_lo, C4_lo * 2 + (C4_hi % TWO_POW_32 - C4_hi % TWO_POW_31) / TWO_POW_31)
                D_hi = bit32_bxor(C2_hi, C4_hi * 2 + (C4_lo % TWO_POW_32 - C4_lo % TWO_POW_31) / TWO_POW_31)

                T0_lo = bit32_bxor(D_lo, L03_lo)
                T0_hi = bit32_bxor(D_hi, L03_hi)
                T1_lo = bit32_bxor(D_lo, L08_lo)
                T1_hi = bit32_bxor(D_hi, L08_hi)
                T2_lo = bit32_bxor(D_lo, L13_lo)
                T2_hi = bit32_bxor(D_hi, L13_hi)
                T3_lo = bit32_bxor(D_lo, L18_lo)
                T3_hi = bit32_bxor(D_hi, L18_hi)
                T4_lo = bit32_bxor(D_lo, L23_lo)
                T4_hi = bit32_bxor(D_hi, L23_hi)

                L03_lo = (T2_lo % TWO_POW_32 - T2_lo % TWO_POW_21) / TWO_POW_21 + T2_hi * TWO_POW_11
                L03_hi = (T2_hi % TWO_POW_32 - T2_hi % TWO_POW_21) / TWO_POW_21 + T2_lo * TWO_POW_11
                L08_lo = (T4_lo % TWO_POW_32 - T4_lo % TWO_POW_3) / TWO_POW_3 + T4_hi * TWO_POW_29 % TWO_POW_32
                L08_hi = (T4_hi % TWO_POW_32 - T4_hi % TWO_POW_3) / TWO_POW_3 + T4_lo * TWO_POW_29 % TWO_POW_32
                L13_lo = T1_lo * TWO_POW_6 + (T1_hi % TWO_POW_32 - T1_hi % TWO_POW_26) / TWO_POW_26
                L13_hi = T1_hi * TWO_POW_6 + (T1_lo % TWO_POW_32 - T1_lo % TWO_POW_26) / TWO_POW_26
                L18_lo = T3_lo * TWO_POW_15 + (T3_hi % TWO_POW_32 - T3_hi % TWO_POW_17) / TWO_POW_17
                L18_hi = T3_hi * TWO_POW_15 + (T3_lo % TWO_POW_32 - T3_lo % TWO_POW_17) / TWO_POW_17
                L23_lo = (T0_lo % TWO_POW_32 - T0_lo % TWO_POW_2) / TWO_POW_2 + T0_hi * TWO_POW_30 % TWO_POW_32
                L23_hi = (T0_hi % TWO_POW_32 - T0_hi % TWO_POW_2) / TWO_POW_2 + T0_lo * TWO_POW_30 % TWO_POW_32

                D_lo = bit32_bxor(C3_lo, C5_lo * 2 + (C5_hi % TWO_POW_32 - C5_hi % TWO_POW_31) / TWO_POW_31)
                D_hi = bit32_bxor(C3_hi, C5_hi * 2 + (C5_lo % TWO_POW_32 - C5_lo % TWO_POW_31) / TWO_POW_31)

                T0_lo = bit32_bxor(D_lo, L04_lo)
                T0_hi = bit32_bxor(D_hi, L04_hi)
                T1_lo = bit32_bxor(D_lo, L09_lo)
                T1_hi = bit32_bxor(D_hi, L09_hi)
                T2_lo = bit32_bxor(D_lo, L14_lo)
                T2_hi = bit32_bxor(D_hi, L14_hi)
                T3_lo = bit32_bxor(D_lo, L19_lo)
                T3_hi = bit32_bxor(D_hi, L19_hi)
                T4_lo = bit32_bxor(D_lo, L24_lo)
                T4_hi = bit32_bxor(D_hi, L24_hi)

                L04_lo = T3_lo * TWO_POW_21 % TWO_POW_32 + (T3_hi % TWO_POW_32 - T3_hi % TWO_POW_11) / TWO_POW_11
                L04_hi = T3_hi * TWO_POW_21 % TWO_POW_32 + (T3_lo % TWO_POW_32 - T3_lo % TWO_POW_11) / TWO_POW_11
                L09_lo = T0_lo * TWO_POW_28 % TWO_POW_32 + (T0_hi % TWO_POW_32 - T0_hi % TWO_POW_4) / TWO_POW_4
                L09_hi = T0_hi * TWO_POW_28 % TWO_POW_32 + (T0_lo % TWO_POW_32 - T0_lo % TWO_POW_4) / TWO_POW_4
                L14_lo = T2_lo * TWO_POW_25 % TWO_POW_32 + (T2_hi % TWO_POW_32 - T2_hi % TWO_POW_7) / TWO_POW_7
                L14_hi = T2_hi * TWO_POW_25 % TWO_POW_32 + (T2_lo % TWO_POW_32 - T2_lo % TWO_POW_7) / TWO_POW_7
                L19_lo = (T4_lo % TWO_POW_32 - T4_lo % TWO_POW_8) / TWO_POW_8 + T4_hi * TWO_POW_24 % TWO_POW_32
                L19_hi = (T4_hi % TWO_POW_32 - T4_hi % TWO_POW_8) / TWO_POW_8 + T4_lo * TWO_POW_24 % TWO_POW_32
                L24_lo = (T1_lo % TWO_POW_32 - T1_lo % TWO_POW_9) / TWO_POW_9 + T1_hi * TWO_POW_23 % TWO_POW_32
                L24_hi = (T1_hi % TWO_POW_32 - T1_hi % TWO_POW_9) / TWO_POW_9 + T1_lo * TWO_POW_23 % TWO_POW_32

                D_lo = bit32_bxor(C4_lo, C1_lo * 2 + (C1_hi % TWO_POW_32 - C1_hi % TWO_POW_31) / TWO_POW_31)
                D_hi = bit32_bxor(C4_hi, C1_hi * 2 + (C1_lo % TWO_POW_32 - C1_lo % TWO_POW_31) / TWO_POW_31)

                T0_lo = bit32_bxor(D_lo, L05_lo)
                T0_hi = bit32_bxor(D_hi, L05_hi)
                T1_lo = bit32_bxor(D_lo, L10_lo)
                T1_hi = bit32_bxor(D_hi, L10_hi)
                T2_lo = bit32_bxor(D_lo, L15_lo)
                T2_hi = bit32_bxor(D_hi, L15_hi)
                T3_lo = bit32_bxor(D_lo, L20_lo)
                T3_hi = bit32_bxor(D_hi, L20_hi)
                T4_lo = bit32_bxor(D_lo, L25_lo)
                T4_hi = bit32_bxor(D_hi, L25_hi)

                L05_lo = T4_lo * TWO_POW_14 + (T4_hi % TWO_POW_32 - T4_hi % TWO_POW_18) / TWO_POW_18
                L05_hi = T4_hi * TWO_POW_14 + (T4_lo % TWO_POW_32 - T4_lo % TWO_POW_18) / TWO_POW_18
                L10_lo = T1_lo * TWO_POW_20 % TWO_POW_32 + (T1_hi % TWO_POW_32 - T1_hi % TWO_POW_12) / TWO_POW_12
                L10_hi = T1_hi * TWO_POW_20 % TWO_POW_32 + (T1_lo % TWO_POW_32 - T1_lo % TWO_POW_12) / TWO_POW_12
                L15_lo = T3_lo * TWO_POW_8 + (T3_hi % TWO_POW_32 - T3_hi % TWO_POW_24) / TWO_POW_24
                L15_hi = T3_hi * TWO_POW_8 + (T3_lo % TWO_POW_32 - T3_lo % TWO_POW_24) / TWO_POW_24
                L20_lo = T0_lo * TWO_POW_27 % TWO_POW_32 + (T0_hi % TWO_POW_32 - T0_hi % TWO_POW_5) / TWO_POW_5
                L20_hi = T0_hi * TWO_POW_27 % TWO_POW_32 + (T0_lo % TWO_POW_32 - T0_lo % TWO_POW_5) / TWO_POW_5
                L25_lo = (T2_lo % TWO_POW_32 - T2_lo % TWO_POW_25) / TWO_POW_25 + T2_hi * TWO_POW_7
                L25_hi = (T2_hi % TWO_POW_32 - T2_hi % TWO_POW_25) / TWO_POW_25 + T2_lo * TWO_POW_7

                D_lo = bit32_bxor(C5_lo, C2_lo * 2 + (C2_hi % TWO_POW_32 - C2_hi % TWO_POW_31) / TWO_POW_31)
                D_hi = bit32_bxor(C5_hi, C2_hi * 2 + (C2_lo % TWO_POW_32 - C2_lo % TWO_POW_31) / TWO_POW_31)

                T1_lo = bit32_bxor(D_lo, L06_lo)
                T1_hi = bit32_bxor(D_hi, L06_hi)
                T2_lo = bit32_bxor(D_lo, L11_lo)
                T2_hi = bit32_bxor(D_hi, L11_hi)
                T3_lo = bit32_bxor(D_lo, L16_lo)
                T3_hi = bit32_bxor(D_hi, L16_hi)
                T4_lo = bit32_bxor(D_lo, L21_lo)
                T4_hi = bit32_bxor(D_hi, L21_hi)

                L06_lo = T2_lo * TWO_POW_3 + (T2_hi % TWO_POW_32 - T2_hi % TWO_POW_29) / TWO_POW_29
                L06_hi = T2_hi * TWO_POW_3 + (T2_lo % TWO_POW_32 - T2_lo % TWO_POW_29) / TWO_POW_29
                L11_lo = T4_lo * TWO_POW_18 + (T4_hi % TWO_POW_32 - T4_hi % TWO_POW_14) / TWO_POW_14
                L11_hi = T4_hi * TWO_POW_18 + (T4_lo % TWO_POW_32 - T4_lo % TWO_POW_14) / TWO_POW_14
                L16_lo = (T1_lo % TWO_POW_32 - T1_lo % TWO_POW_28) / TWO_POW_28 + T1_hi * TWO_POW_4
                L16_hi = (T1_hi % TWO_POW_32 - T1_hi % TWO_POW_28) / TWO_POW_28 + T1_lo * TWO_POW_4
                L21_lo = (T3_lo % TWO_POW_32 - T3_lo % TWO_POW_23) / TWO_POW_23 + T3_hi * TWO_POW_9
                L21_hi = (T3_hi % TWO_POW_32 - T3_hi % TWO_POW_23) / TWO_POW_23 + T3_lo * TWO_POW_9

                L01_lo = bit32_bxor(D_lo, L01_lo)
                L01_hi = bit32_bxor(D_hi, L01_hi)
                L01_lo, L02_lo, L03_lo, L04_lo, L05_lo = bit32_bxor(L01_lo, bit32_band(-1 - L02_lo, L03_lo)), bit32_bxor(L02_lo, bit32_band(-1 - L03_lo, L04_lo)), bit32_bxor(L03_lo, bit32_band(-1 - L04_lo, L05_lo)), bit32_bxor(L04_lo, bit32_band(-1 - L05_lo, L01_lo)), bit32_bxor(L05_lo, bit32_band(-1 - L01_lo, L02_lo))
                L01_hi, L02_hi, L03_hi, L04_hi, L05_hi = bit32_bxor(L01_hi, bit32_band(-1 - L02_hi, L03_hi)), bit32_bxor(L02_hi, bit32_band(-1 - L03_hi, L04_hi)), bit32_bxor(L03_hi, bit32_band(-1 - L04_hi, L05_hi)), bit32_bxor(L04_hi, bit32_band(-1 - L05_hi, L01_hi)), bit32_bxor(L05_hi, bit32_band(-1 - L01_hi, L02_hi))
                L06_lo, L07_lo, L08_lo, L09_lo, L10_lo = bit32_bxor(L09_lo, bit32_band(-1 - L10_lo, L06_lo)), bit32_bxor(L10_lo, bit32_band(-1 - L06_lo, L07_lo)), bit32_bxor(L06_lo, bit32_band(-1 - L07_lo, L08_lo)), bit32_bxor(L07_lo, bit32_band(-1 - L08_lo, L09_lo)), bit32_bxor(L08_lo, bit32_band(-1 - L09_lo, L10_lo))
                L06_hi, L07_hi, L08_hi, L09_hi, L10_hi = bit32_bxor(L09_hi, bit32_band(-1 - L10_hi, L06_hi)), bit32_bxor(L10_hi, bit32_band(-1 - L06_hi, L07_hi)), bit32_bxor(L06_hi, bit32_band(-1 - L07_hi, L08_hi)), bit32_bxor(L07_hi, bit32_band(-1 - L08_hi, L09_hi)), bit32_bxor(L08_hi, bit32_band(-1 - L09_hi, L10_hi))
                L11_lo, L12_lo, L13_lo, L14_lo, L15_lo = bit32_bxor(L12_lo, bit32_band(-1 - L13_lo, L14_lo)), bit32_bxor(L13_lo, bit32_band(-1 - L14_lo, L15_lo)), bit32_bxor(L14_lo, bit32_band(-1 - L15_lo, L11_lo)), bit32_bxor(L15_lo, bit32_band(-1 - L11_lo, L12_lo)), bit32_bxor(L11_lo, bit32_band(-1 - L12_lo, L13_lo))
                L11_hi, L12_hi, L13_hi, L14_hi, L15_hi = bit32_bxor(L12_hi, bit32_band(-1 - L13_hi, L14_hi)), bit32_bxor(L13_hi, bit32_band(-1 - L14_hi, L15_hi)), bit32_bxor(L14_hi, bit32_band(-1 - L15_hi, L11_hi)), bit32_bxor(L15_hi, bit32_band(-1 - L11_hi, L12_hi)), bit32_bxor(L11_hi, bit32_band(-1 - L12_hi, L13_hi))
                L16_lo, L17_lo, L18_lo, L19_lo, L20_lo = bit32_bxor(L20_lo, bit32_band(-1 - L16_lo, L17_lo)), bit32_bxor(L16_lo, bit32_band(-1 - L17_lo, L18_lo)), bit32_bxor(L17_lo, bit32_band(-1 - L18_lo, L19_lo)), bit32_bxor(L18_lo, bit32_band(-1 - L19_lo, L20_lo)), bit32_bxor(L19_lo, bit32_band(-1 - L20_lo, L16_lo))
                L16_hi, L17_hi, L18_hi, L19_hi, L20_hi = bit32_bxor(L20_hi, bit32_band(-1 - L16_hi, L17_hi)), bit32_bxor(L16_hi, bit32_band(-1 - L17_hi, L18_hi)), bit32_bxor(L17_hi, bit32_band(-1 - L18_hi, L19_hi)), bit32_bxor(L18_hi, bit32_band(-1 - L19_hi, L20_hi)), bit32_bxor(L19_hi, bit32_band(-1 - L20_hi, L16_hi))
                L21_lo, L22_lo, L23_lo, L24_lo, L25_lo = bit32_bxor(L23_lo, bit32_band(-1 - L24_lo, L25_lo)), bit32_bxor(L24_lo, bit32_band(-1 - L25_lo, L21_lo)), bit32_bxor(L25_lo, bit32_band(-1 - L21_lo, L22_lo)), bit32_bxor(L21_lo, bit32_band(-1 - L22_lo, L23_lo)), bit32_bxor(L22_lo, bit32_band(-1 - L23_lo, L24_lo))
                L21_hi, L22_hi, L23_hi, L24_hi, L25_hi = bit32_bxor(L23_hi, bit32_band(-1 - L24_hi, L25_hi)), bit32_bxor(L24_hi, bit32_band(-1 - L25_hi, L21_hi)), bit32_bxor(L25_hi, bit32_band(-1 - L21_hi, L22_hi)), bit32_bxor(L21_hi, bit32_band(-1 - L22_hi, L23_hi)), bit32_bxor(L22_hi, bit32_band(-1 - L23_hi, L24_hi))
                L01_lo = bit32_bxor(L01_lo, RC_lo[round_idx])
                L01_hi = L01_hi + RC_hi[round_idx] -- RC_hi[] is either 0 or 0x80000000, so we could use fast addition instead of slow XOR
            end

            lanes_lo[1] = L01_lo
            lanes_hi[1] = L01_hi
            lanes_lo[2] = L02_lo
            lanes_hi[2] = L02_hi
            lanes_lo[3] = L03_lo
            lanes_hi[3] = L03_hi
            lanes_lo[4] = L04_lo
            lanes_hi[4] = L04_hi
            lanes_lo[5] = L05_lo
            lanes_hi[5] = L05_hi
            lanes_lo[6] = L06_lo
            lanes_hi[6] = L06_hi
            lanes_lo[7] = L07_lo
            lanes_hi[7] = L07_hi
            lanes_lo[8] = L08_lo
            lanes_hi[8] = L08_hi
            lanes_lo[9] = L09_lo
            lanes_hi[9] = L09_hi
            lanes_lo[10] = L10_lo
            lanes_hi[10] = L10_hi
            lanes_lo[11] = L11_lo
            lanes_hi[11] = L11_hi
            lanes_lo[12] = L12_lo
            lanes_hi[12] = L12_hi
            lanes_lo[13] = L13_lo
            lanes_hi[13] = L13_hi
            lanes_lo[14] = L14_lo
            lanes_hi[14] = L14_hi
            lanes_lo[15] = L15_lo
            lanes_hi[15] = L15_hi
            lanes_lo[16] = L16_lo
            lanes_hi[16] = L16_hi
            lanes_lo[17] = L17_lo
            lanes_hi[17] = L17_hi
            lanes_lo[18] = L18_lo
            lanes_hi[18] = L18_hi
            lanes_lo[19] = L19_lo
            lanes_hi[19] = L19_hi
            lanes_lo[20] = L20_lo
            lanes_hi[20] = L20_hi
            lanes_lo[21] = L21_lo
            lanes_hi[21] = L21_hi
            lanes_lo[22] = L22_lo
            lanes_hi[22] = L22_hi
            lanes_lo[23] = L23_lo
            lanes_hi[23] = L23_hi
            lanes_lo[24] = L24_lo
            lanes_hi[24] = L24_hi
            lanes_lo[25] = L25_lo
            lanes_hi[25] = L25_hi
        end
    end

    --=============================================================================---
    -- MAGIC NUMBERS CALCULATOR
    --=============================================================================---
    -- Q:
    --    Is 53-bit "double" math enough to calculate square roots and cube roots of primes with 64 correct bits after decimal point?
    -- A:
    --    Yes, 53-bit "double" arithmetic is enough.
    --    We could obtain first 40 bits by direct calculation of p^(1/3) and next 40 bits by one step of Newton's method.
    do
        local function mul(src1, src2, factor, result_length)
            -- src1, src2 - long integers (arrays of digits in base TWO_POW_24)
            -- factor - small integer
            -- returns long integer result (src1 * src2 * factor) and its floating point approximation
            local result, carry, value, weight = table.create(result_length), 0, 0, 1
            for j = 1, result_length do
                for k = math.max(1, j + 1 - #src2), math.min(j, #src1) do
                    carry = carry + factor * src1[k] * src2[j + 1 - k] -- "int32" is not enough for multiplication result, that's why "factor" must be of type "double"
                end

                local digit = carry % TWO_POW_24
                result[j] = math.floor(digit)
                carry = (carry - digit) / TWO_POW_24
                value = value + digit * weight
                weight = weight * TWO_POW_24
            end

            return result, value
        end

        local idx, step, p, one, sqrt_hi, sqrt_lo = 0, {4, 1, 2, -2, 2}, 4, {1}, sha2_H_hi, sha2_H_lo
        repeat
            p = p + step[p % 6]
            local d = 1
            repeat
                d = d + step[d % 6]
                if d * d > p then
                    -- next prime number is found
                    local root = p ^ (1 / 3)
                    local R = root * TWO_POW_40
                    R = mul(table.create(1, math.floor(R)), one, 1, 2)
                    local _, delta = mul(R, mul(R, R, 1, 4), -1, 4)
                    local hi = R[2] % 65536 * 65536 + math.floor(R[1] / 256)
                    local lo = R[1] % 256 * 16777216 + math.floor(delta * (TWO_POW_NEG_56 / 3) * root / p)

                    if idx < 16 then
                        root = math.sqrt(p)
                        R = root * TWO_POW_40
                        R = mul(table.create(1, math.floor(R)), one, 1, 2)
                        _, delta = mul(R, R, -1, 2)
                        local hi = R[2] % 65536 * 65536 + math.floor(R[1] / 256)
                        local lo = R[1] % 256 * 16777216 + math.floor(delta * TWO_POW_NEG_17 / root)
                        local idx = idx % 8 + 1
                        sha2_H_ext256[224][idx] = lo
                        sqrt_hi[idx], sqrt_lo[idx] = hi, lo + hi * hi_factor
                        if idx > 7 then
                            sqrt_hi, sqrt_lo = sha2_H_ext512_hi[384], sha2_H_ext512_lo[384]
                        end
                    end

                    idx = idx + 1
                    sha2_K_hi[idx], sha2_K_lo[idx] = hi, lo % K_lo_modulo + hi * hi_factor
                    break
                end
            until p % d == 0
        until idx > 79
    end

    -- Calculating IVs for SHA512/224 and SHA512/256
    for width = 224, 256, 32 do
        local H_lo, H_hi = {}, nil
        if XOR64A5 then
            for j = 1, 8 do
                H_lo[j] = XOR64A5(sha2_H_lo[j])
            end
        else
            H_hi = {}
            for j = 1, 8 do
                H_lo[j] = bit32_bxor(sha2_H_lo[j], 0xA5A5A5A5) % 4294967296
                H_hi[j] = bit32_bxor(sha2_H_hi[j], 0xA5A5A5A5) % 4294967296
            end
        end

        sha512_feed_128(H_lo, H_hi, "SHA-512/" .. tostring(width) .. "\128" .. string.rep("\0", 115) .. "\88", 0, 128)
        sha2_H_ext512_lo[width] = H_lo
        sha2_H_ext512_hi[width] = H_hi
    end

    -- Constants for MD5
    do
        for idx = 1, 64 do
            -- we can't use formula math.floor(abs(sin(idx))*TWO_POW_32) because its result may be beyond integer range on Lua built with 32-bit integers
            local hi, lo = math.modf(math.abs(math.sin(idx)) * TWO_POW_16)
            md5_K[idx] = hi * 65536 + math.floor(lo * TWO_POW_16)
        end
    end

    -- Constants for SHA3
    do
        local sh_reg = 29
        local function next_bit()
            local r = sh_reg % 2
            sh_reg = bit32_bxor((sh_reg - r) / 2, 142 * r)
            return r
        end

        for idx = 1, 24 do
            local lo, m = 0, nil
            for _ = 1, 6 do
                m = m and m * m * 2 or 1
                lo = lo + next_bit() * m
            end

            local hi = next_bit() * m
            sha3_RC_hi[idx], sha3_RC_lo[idx] = hi, lo + hi * hi_factor_keccak
        end
    end

    --=============================================================================---
    -- MAIN FUNCTIONS
    --=============================================================================---
    local function sha256ext(width, message)
        -- Create an instance (private proxyobjects for current calculation)
        local Array256 = sha2_H_ext256[width] -- # == 8
        local length, tail = 0, ""
        local H = table.create(8)
        H[1], H[2], H[3], H[4], H[5], H[6], H[7], H[8] = Array256[1], Array256[2], Array256[3], Array256[4], Array256[5], Array256[6], Array256[7], Array256[8]

        local function partial(message_part)
            if message_part then
                local partLength = #message_part
                if tail then
                    length = length + partLength
                    local offs = 0
                    local tailLength = #tail
                    if tail ~= "" and tailLength + partLength >= 64 then
                        offs = 64 - tailLength
                        sha256_feed_64(H, tail .. string.sub(message_part, 1, offs), 0, 64)
                        tail = ""
                    end

                    local size = partLength - offs
                    local size_tail = size % 64
                    sha256_feed_64(H, message_part, offs, size - size_tail)
                    tail = tail .. string.sub(message_part, partLength + 1 - size_tail)
                    return partial
                else
                    error("Adding more chunks is not allowed after receiving the result", 2)
                end
            else
                if tail then
                    local final_blocks = table.create(10) --{tail, "\128", string.rep("\0", (-9 - length) % 64 + 1)}
                    final_blocks[1] = tail
                    final_blocks[2] = "\128"
                    final_blocks[3] = string.rep("\0", (-9 - length) % 64 + 1)

                    tail = nil
                    -- Assuming user data length is shorter than (TWO_POW_53)-9 bytes
                    -- Anyway, it looks very unrealistic that someone would spend more than a year of calculations to process TWO_POW_53 bytes of data by using this Lua script :-)
                    -- TWO_POW_53 bytes = TWO_POW_56 bits, so "bit-counter" fits in 7 bytes
                    length = length * (8 / TWO56_POW_7) -- convert "byte-counter" to "bit-counter" and move decimal point to the left
                    for j = 4, 10 do
                        length = length % 1 * 256
                        final_blocks[j] = string.char(math.floor(length))
                    end

                    final_blocks = table.concat(final_blocks)
                    sha256_feed_64(H, final_blocks, 0, #final_blocks)
                    local max_reg = width / 32
                    for j = 1, max_reg do
                        H[j] = string.format("%08x", H[j] % 4294967296)
                    end

                    H = table.concat(H, "", 1, max_reg)
                end

                return H
            end
        end

        if message then
            -- Actually perform calculations and return the SHA256 digest of a message
            return partial(message)()
        else
            -- Return function for chunk-by-chunk loading
            -- User should feed every chunk of input data as single argument to this function and finally get SHA256 digest by invoking this function without an argument
            return partial
        end
    end

    local function sha512ext(width, message)

        -- Create an instance (private proxyobjects for current calculation)
        local length, tail, H_lo, H_hi = 0, "", table.pack(table.unpack(sha2_H_ext512_lo[width])), not HEX64 and table.pack(table.unpack(sha2_H_ext512_hi[width]))

        local function partial(message_part)
            if message_part then
                local partLength = #message_part
                if tail then
                    length = length + partLength
                    local offs = 0
                    if tail ~= "" and #tail + partLength >= 128 then
                        offs = 128 - #tail
                        sha512_feed_128(H_lo, H_hi, tail .. string.sub(message_part, 1, offs), 0, 128)
                        tail = ""
                    end

                    local size = partLength - offs
                    local size_tail = size % 128
                    sha512_feed_128(H_lo, H_hi, message_part, offs, size - size_tail)
                    tail = tail .. string.sub(message_part, partLength + 1 - size_tail)
                    return partial
                else
                    error("Adding more chunks is not allowed after receiving the result", 2)
                end
            else
                if tail then
                    local final_blocks = table.create(3) --{tail, "\128", string.rep("\0", (-17-length) % 128 + 9)}
                    final_blocks[1] = tail
                    final_blocks[2] = "\128"
                    final_blocks[3] = string.rep("\0", (-17 - length) % 128 + 9)

                    tail = nil
                    -- Assuming user data length is shorter than (TWO_POW_53)-17 bytes
                    -- TWO_POW_53 bytes = TWO_POW_56 bits, so "bit-counter" fits in 7 bytes
                    length = length * (8 / TWO56_POW_7) -- convert "byte-counter" to "bit-counter" and move floating point to the left
                    for j = 4, 10 do
                        length = length % 1 * 256
                        final_blocks[j] = string.char(math.floor(length))
                    end

                    final_blocks = table.concat(final_blocks)
                    sha512_feed_128(H_lo, H_hi, final_blocks, 0, #final_blocks)
                    local max_reg = math.ceil(width / 64)

                    if HEX64 then
                        for j = 1, max_reg do
                            H_lo[j] = HEX64(H_lo[j])
                        end
                    else
                        for j = 1, max_reg do
                            H_lo[j] = string.format("%08x", H_hi[j] % 4294967296) .. string.format("%08x", H_lo[j] % 4294967296)
                        end

                        H_hi = nil
                    end

                    H_lo = string.sub(table.concat(H_lo, "", 1, max_reg), 1, width / 4)
                end

                return H_lo
            end
        end

        if message then
            -- Actually perform calculations and return the SHA512 digest of a message
            return partial(message)()
        else
            -- Return function for chunk-by-chunk loading
            -- User should feed every chunk of input data as single argument to this function and finally get SHA512 digest by invoking this function without an argument
            return partial
        end
    end

    local function md5(message)

        -- Create an instance (private proxyobjects for current calculation)
        local H, length, tail = table.create(4), 0, ""
        H[1], H[2], H[3], H[4] = md5_sha1_H[1], md5_sha1_H[2], md5_sha1_H[3], md5_sha1_H[4]

        local function partial(message_part)
            if message_part then
                local partLength = #message_part
                if tail then
                    length = length + partLength
                    local offs = 0
                    if tail ~= "" and #tail + partLength >= 64 then
                        offs = 64 - #tail
                        md5_feed_64(H, tail .. string.sub(message_part, 1, offs), 0, 64)
                        tail = ""
                    end

                    local size = partLength - offs
                    local size_tail = size % 64
                    md5_feed_64(H, message_part, offs, size - size_tail)
                    tail = tail .. string.sub(message_part, partLength + 1 - size_tail)
                    return partial
                else
                    error("Adding more chunks is not allowed after receiving the result", 2)
                end
            else
                if tail then
                    local final_blocks = table.create(3) --{tail, "\128", string.rep("\0", (-9 - length) % 64)}
                    final_blocks[1] = tail
                    final_blocks[2] = "\128"
                    final_blocks[3] = string.rep("\0", (-9 - length) % 64)
                    tail = nil
                    length = length * 8 -- convert "byte-counter" to "bit-counter"
                    for j = 4, 11 do
                        local low_byte = length % 256
                        final_blocks[j] = string.char(low_byte)
                        length = (length - low_byte) / 256
                    end

                    final_blocks = table.concat(final_blocks)
                    md5_feed_64(H, final_blocks, 0, #final_blocks)
                    for j = 1, 4 do
                        H[j] = string.format("%08x", H[j] % 4294967296)
                    end

                    H = string.gsub(table.concat(H), "(..)(..)(..)(..)", "%4%3%2%1")
                end

                return H
            end
        end

        if message then
            -- Actually perform calculations and return the MD5 digest of a message
            return partial(message)()
        else
            -- Return function for chunk-by-chunk loading
            -- User should feed every chunk of input data as single argument to this function and finally get MD5 digest by invoking this function without an argument
            return partial
        end
    end

    local function sha1(message)
        -- Create an instance (private proxyobjects for current calculation)
        local H, length, tail = table.pack(table.unpack(md5_sha1_H)), 0, ""

        local function partial(message_part)
            if message_part then
                local partLength = #message_part
                if tail then
                    length = length + partLength
                    local offs = 0
                    if tail ~= "" and #tail + partLength >= 64 then
                        offs = 64 - #tail
                        sha1_feed_64(H, tail .. string.sub(message_part, 1, offs), 0, 64)
                        tail = ""
                    end

                    local size = partLength - offs
                    local size_tail = size % 64
                    sha1_feed_64(H, message_part, offs, size - size_tail)
                    tail = tail .. string.sub(message_part, partLength + 1 - size_tail)
                    return partial
                else
                    error("Adding more chunks is not allowed after receiving the result", 2)
                end
            else
                if tail then
                    local final_blocks = table.create(10) --{tail, "\128", string.rep("\0", (-9 - length) % 64 + 1)}
                    final_blocks[1] = tail
                    final_blocks[2] = "\128"
                    final_blocks[3] = string.rep("\0", (-9 - length) % 64 + 1)
                    tail = nil

                    -- Assuming user data length is shorter than (TWO_POW_53)-9 bytes
                    -- TWO_POW_53 bytes = TWO_POW_56 bits, so "bit-counter" fits in 7 bytes
                    length = length * (8 / TWO56_POW_7) -- convert "byte-counter" to "bit-counter" and move decimal point to the left
                    for j = 4, 10 do
                        length = length % 1 * 256
                        final_blocks[j] = string.char(math.floor(length))
                    end

                    final_blocks = table.concat(final_blocks)
                    sha1_feed_64(H, final_blocks, 0, #final_blocks)
                    for j = 1, 5 do
                        H[j] = string.format("%08x", H[j] % 4294967296)
                    end

                    H = table.concat(H)
                end

                return H
            end
        end

        if message then
            -- Actually perform calculations and return the SHA-1 digest of a message
            return partial(message)()
        else
            -- Return function for chunk-by-chunk loading
            -- User should feed every chunk of input data as single argument to this function and finally get SHA-1 digest by invoking this function without an argument
            return partial
        end
    end

    local function keccak(block_size_in_bytes, digest_size_in_bytes, is_SHAKE, message)
        -- "block_size_in_bytes" is multiple of 8
        if type(digest_size_in_bytes) ~= "number" then
            -- arguments in SHAKE are swapped:
            --    NIST FIPS 202 defines SHAKE(message,num_bits)
            --    this module   defines SHAKE(num_bytes,message)
            -- it's easy to forget about this swap, hence the check
            error("Argument 'digest_size_in_bytes' must be a number", 2)
        end

        -- Create an instance (private proxyobjects for current calculation)
        local tail, lanes_lo, lanes_hi = "", table.create(25, 0), hi_factor_keccak == 0 and table.create(25, 0)
        local result

        --~     pad the input N using the pad function, yielding a padded bit string P with a length divisible by r (such that n = len(P)/r is integer),
        --~     break P into n consecutive r-bit pieces P0, ..., Pn-1 (last is zero-padded)
        --~     initialize the state S to a string of b 0 bits.
        --~     absorb the input into the state: For each block Pi,
        --~         extend Pi at the end by a string of c 0 bits, yielding one of length b,
        --~         XOR that with S and
        --~         apply the block permutation f to the result, yielding a new state S
        --~     initialize Z to be the empty string
        --~     while the length of Z is less than d:
        --~         append the first r bits of S to Z
        --~         if Z is still less than d bits long, apply f to S, yielding a new state S.
        --~     truncate Z to d bits
        local function partial(message_part)
            if message_part then
                local partLength = #message_part
                if tail then
                    local offs = 0
                    if tail ~= "" and #tail + partLength >= block_size_in_bytes then
                        offs = block_size_in_bytes - #tail
                        keccak_feed(lanes_lo, lanes_hi, tail .. string.sub(message_part, 1, offs), 0, block_size_in_bytes, block_size_in_bytes)
                        tail = ""
                    end

                    local size = partLength - offs
                    local size_tail = size % block_size_in_bytes
                    keccak_feed(lanes_lo, lanes_hi, message_part, offs, size - size_tail, block_size_in_bytes)
                    tail = tail .. string.sub(message_part, partLength + 1 - size_tail)
                    return partial
                else
                    error("Adding more chunks is not allowed after receiving the result", 2)
                end
            else
                if tail then
                    -- append the following bits to the message: for usual SHA3: 011(0*)1, for SHAKE: 11111(0*)1
                    local gap_start = is_SHAKE and 31 or 6
                    tail = tail .. (#tail + 1 == block_size_in_bytes and string.char(gap_start + 128) or string.char(gap_start) .. string.rep("\0", (-2 - #tail) % block_size_in_bytes) .. "\128")
                    keccak_feed(lanes_lo, lanes_hi, tail, 0, #tail, block_size_in_bytes)
                    tail = nil

                    local lanes_used = 0
                    local total_lanes = math.floor(block_size_in_bytes / 8)
                    local qwords = {}

                    local function get_next_qwords_of_digest(qwords_qty)
                        -- returns not more than 'qwords_qty' qwords ('qwords_qty' might be non-integer)
                        -- doesn't go across keccak-buffer boundary
                        -- block_size_in_bytes is a multiple of 8, so, keccak-buffer contains integer number of qwords
                        if lanes_used >= total_lanes then
                            keccak_feed(lanes_lo, lanes_hi, "\0\0\0\0\0\0\0\0", 0, 8, 8)
                            lanes_used = 0
                        end

                        qwords_qty = math.floor(math.min(qwords_qty, total_lanes - lanes_used))
                        if hi_factor_keccak ~= 0 then
                            for j = 1, qwords_qty do
                                qwords[j] = HEX64(lanes_lo[lanes_used + j - 1 + lanes_index_base])
                            end
                        else
                            for j = 1, qwords_qty do
                                qwords[j] = string.format("%08x", lanes_hi[lanes_used + j] % 4294967296) .. string.format("%08x", lanes_lo[lanes_used + j] % 4294967296)
                            end
                        end

                        lanes_used = lanes_used + qwords_qty
                        return string.gsub(table.concat(qwords, "", 1, qwords_qty), "(..)(..)(..)(..)(..)(..)(..)(..)", "%8%7%6%5%4%3%2%1"), qwords_qty * 8
                    end

                    local parts = {} -- digest parts
                    local last_part, last_part_size = "", 0

                    local function get_next_part_of_digest(bytes_needed)
                        -- returns 'bytes_needed' bytes, for arbitrary integer 'bytes_needed'
                        bytes_needed = bytes_needed or 1
                        if bytes_needed <= last_part_size then
                            last_part_size = last_part_size - bytes_needed
                            local part_size_in_nibbles = bytes_needed * 2
                            local result = string.sub(last_part, 1, part_size_in_nibbles)
                            last_part = string.sub(last_part, part_size_in_nibbles + 1)
                            return result
                        end

                        local parts_qty = 0
                        if last_part_size > 0 then
                            parts_qty = 1
                            parts[parts_qty] = last_part
                            bytes_needed = bytes_needed - last_part_size
                        end

                        -- repeats until the length is enough
                        while bytes_needed >= 8 do
                            local next_part, next_part_size = get_next_qwords_of_digest(bytes_needed / 8)
                            parts_qty = parts_qty + 1
                            parts[parts_qty] = next_part
                            bytes_needed = bytes_needed - next_part_size
                        end

                        if bytes_needed > 0 then
                            last_part, last_part_size = get_next_qwords_of_digest(1)
                            parts_qty = parts_qty + 1
                            parts[parts_qty] = get_next_part_of_digest(bytes_needed)
                        else
                            last_part, last_part_size = "", 0
                        end

                        return table.concat(parts, "", 1, parts_qty)
                    end

                    if digest_size_in_bytes < 0 then
                        result = get_next_part_of_digest
                    else
                        result = get_next_part_of_digest(digest_size_in_bytes)
                    end

                end

                return result
            end
        end

        if message then
            -- Actually perform calculations and return the SHA3 digest of a message
            return partial(message)()
        else
            -- Return function for chunk-by-chunk loading
            -- User should feed every chunk of input data as single argument to this function and finally get SHA3 digest by invoking this function without an argument
            return partial
        end
    end

    local function HexToBinFunction(hh)
        return string.char(tonumber(hh, 16))
    end

    local function hex2bin(hex_string)
        return (string.gsub(hex_string, "%x%x", HexToBinFunction))
    end

    local base64_symbols = {
        ["+"] = 62, ["-"] = 62, [62] = "+";
        ["/"] = 63, ["_"] = 63, [63] = "/";
        ["="] = -1, ["."] = -1, [-1] = "=";
    }

    local symbol_index = 0
    for j, pair in ipairs{"AZ", "az", "09"} do
        for ascii = string.byte(pair), string.byte(pair, 2) do
            local ch = string.char(ascii)
            base64_symbols[ch] = symbol_index
            base64_symbols[symbol_index] = ch
            symbol_index = symbol_index + 1
        end
    end

    local function bin2base64(binary_string)
        local stringLength = #binary_string
        local result = table.create(math.ceil(stringLength / 3))
        local length = 0

        for pos = 1, #binary_string, 3 do
            local c1, c2, c3, c4 = string.byte(string.sub(binary_string, pos, pos + 2) .. '\0', 1, -1)
            length = length + 1
            result[length] =
                base64_symbols[math.floor(c1 / 4)] ..
                base64_symbols[c1 % 4 * 16 + math.floor(c2 / 16)] ..
                base64_symbols[c3 and c2 % 16 * 4 + math.floor(c3 / 64) or -1] ..
                base64_symbols[c4 and c3 % 64 or -1]
        end

        return table.concat(result)
    end

    local function base642bin(base64_string)
        local result, chars_qty = {}, 3
        for pos, ch in string.gmatch(string.gsub(base64_string, "%s+", ""), "()(.)") do
            local code = base64_symbols[ch]
            if code < 0 then
                chars_qty = chars_qty - 1
                code = 0
            end

            local idx = pos % 4
            if idx > 0 then
                result[-idx] = code
            else
                local c1 = result[-1] * 4 + math.floor(result[-2] / 16)
                local c2 = (result[-2] % 16) * 16 + math.floor(result[-3] / 4)
                local c3 = (result[-3] % 4) * 64 + code
                result[#result + 1] = string.sub(string.char(c1, c2, c3), 1, chars_qty)
            end
        end

        return table.concat(result)
    end

    local block_size_for_HMAC -- this table will be initialized at the end of the module
    --local function pad_and_xor(str, result_length, byte_for_xor)
    --	return string.gsub(str, ".", function(c)
    --		return string.char(bit32_bxor(string.byte(c), byte_for_xor))
    --	end) .. string.rep(string.char(byte_for_xor), result_length - #str)
    --end

    -- For the sake of speed of converting hexes to strings, there's a map of the conversions here
    local BinaryStringMap = {}
    for Index = 0, 255 do
        BinaryStringMap[string.format("%02x", Index)] = string.char(Index)
    end

    -- Update 02.14.20 - added AsBinary for easy GameAnalytics replacement.
    local function hmac(hash_func, key, message, AsBinary)
        -- Create an instance (private proxyobjects for current calculation)
        local block_size = block_size_for_HMAC[hash_func]
        if not block_size then
            error("Unknown hash function", 2)
        end

        local KeyLength = #key
        if KeyLength > block_size then
            key = string.gsub(hash_func(key), "%x%x", HexToBinFunction)
            KeyLength = #key
        end

        local append = hash_func()(string.gsub(key, ".", function(c)
            return string.char(bit32_bxor(string.byte(c), 0x36))
        end) .. string.rep("6", block_size - KeyLength)) -- 6 = string.char(0x36)

        local result

        local function partial(message_part)
            if not message_part then
                result = result or hash_func(
                    string.gsub(key, ".", function(c)
                        return string.char(bit32_bxor(string.byte(c), 0x5c))
                    end) .. string.rep("\\", block_size - KeyLength) -- \ = string.char(0x5c)
                    .. (string.gsub(append(), "%x%x", HexToBinFunction))
                )

                return result
            elseif result then
                error("Adding more chunks is not allowed after receiving the result", 2)
            else
                append(message_part)
                return partial
            end
        end

        if message then
            -- Actually perform calculations and return the HMAC of a message
            local FinalMessage = partial(message)()
            return AsBinary and (string.gsub(FinalMessage, "%x%x", BinaryStringMap)) or FinalMessage
        else
            -- Return function for chunk-by-chunk loading of a message
            -- User should feed every chunk of the message as single argument to this function and finally get HMAC by invoking this function without an argument
            return partial
        end
    end

    local HashLib = {
        md5 = md5,
        sha1 = sha1,
        -- SHA2 hash functions:
        sha224 = function(message)
            return sha256ext(224, message)
        end;

        sha256 = function(message)
            return sha256ext(256, message)
        end;

        sha512_224 = function(message)
            return sha512ext(224, message)
        end;

        sha512_256 = function(message)
            return sha512ext(256, message)
        end;

        sha384 = function(message)
            return sha512ext(384, message)
        end;

        sha512 = function(message)
            return sha512ext(512, message)
        end;

        -- SHA3 hash functions:
        sha3_224 = function(message)
            return keccak((1600 - 2 * 224) / 8, 224 / 8, false, message)
        end;

        sha3_256 = function(message)
            return keccak((1600 - 2 * 256) / 8, 256 / 8, false, message)
        end;

        sha3_384 = function(message)
            return keccak((1600 - 2 * 384) / 8, 384 / 8, false, message)
        end;

        sha3_512 = function(message)
            return keccak((1600 - 2 * 512) / 8, 512 / 8, false, message)
        end;

        shake128 = function(message, digest_size_in_bytes)
            return keccak((1600 - 2 * 128) / 8, digest_size_in_bytes, true, message)
        end;

        shake256 = function(message, digest_size_in_bytes)
            return keccak((1600 - 2 * 256) / 8, digest_size_in_bytes, true, message)
        end;

        -- misc utilities:
        hmac = hmac; -- HMAC(hash_func, key, message) is applicable to any hash function from this module except SHAKE*
        hex_to_bin = hex2bin; -- converts hexadecimal representation to binary string
        base64_to_bin = base642bin; -- converts base64 representation to binary string
        bin_to_base64 = bin2base64; -- converts binary string to base64 representation
        base64_encode = Base64.Encode;
        base64_decode = Base64.Decode;
    }

    block_size_for_HMAC = {
        [HashLib.md5] = 64;
        [HashLib.sha1] = 64;
        [HashLib.sha224] = 64;
        [HashLib.sha256] = 64;
        [HashLib.sha512_224] = 128;
        [HashLib.sha512_256] = 128;
        [HashLib.sha384] = 128;
        [HashLib.sha512] = 128;
        [HashLib.sha3_224] = (1600 - 2 * 224) / 8;
        [HashLib.sha3_256] = (1600 - 2 * 256) / 8;
        [HashLib.sha3_384] = (1600 - 2 * 384) / 8;
        [HashLib.sha3_512] = (1600 - 2 * 512) / 8;
    }

    --=============================================================================--
    -- ## Bridging variables
    --=============================================================================--

    local Environment = {}
    local Bridge = {
        on_going_requests = {} -- Ensures table exists for use in Bridge:SendAndReceive
    }
    local Utils = {}
    local unlocked_modules = setmetatable({}, { __mode = "k" })

    local _require = require
    local _game = game

    local cachedmethods = {}
    local proxyobject
    local proxied = {}
    local proxyobjects = {}

    local lz4 = {}
    local touchers_reg = setmetatable({}, { __mode = "ks" })
    local nilinstances, cache = {Instance.new("Part")}, {cached = {}, invalidated = {}}   

    type Streamer = {
        Offset: number,
        Source: string,
        Length: number,
        IsFinished: boolean,
        LastUnreadBytes: number,

        read: (Streamer, len: number?, shiftOffset: boolean?) -> string,
        seek: (Streamer, len: number) -> (),
        append: (Streamer, newData: string) -> (),
        toEnd: (Streamer) -> ()
    }

    type BlockData = {
        [number]: {
            Literal: string,
            LiteralLength: number,
            MatchOffset: number?,
            MatchLength: number?
        }
    }

    --=============================================================================--
    -- ## Utilities
    --=============================================================================--

    function Utils:GetRandomModule()
        local children = CorePackages.Packages:GetChildren()
        local module

        -- Find a valid ModuleScript to clone
        while not module or module.ClassName ~= "ModuleScript" do
            module = children[math.random(#children)]
        end

        local clone = module:Clone()
        clone.Name = "Oracle"
        clone.Parent = Scripts

        return clone
    end

    function Utils:MergeTable(a, b)
        a = a or {}
        b = b or {}
        for k, v in pairs(b) do
            a[k] = v
        end
        return a
    end

    function Utils:HttpGet(url, return_raw)
        assert(type(url) == "string", "invalid argument #1 to 'HttpGet' (string expected, got " .. type(url) .. ") ", 2)
        if return_raw == nil then
            return_raw = true
        end
        local response = Environment.request({
            Url = url,
            Method = "GET",
        })

        if return_raw then
            return response.Body
        end

        return HttpService:JSONDecode(response.Body)
    end

    --=============================================================================--
    -- ## Bridging
    --=============================================================================--

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
                -- On timeout, return a structured response
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

    function Bridge:IsCompilable(encoded_source)
        local response = self:SendAndReceive({
            ["action"] = "is_compilable",
            ["source"] = encoded_source
        })

        if not response or response["success"] ~= true then
            return false, response and response["message"] or "Compilation check failed (no response)"
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

        if not response or response["success"] ~= true then
            return false, response and response["message"] or "Unlock failed (no response)"
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

        local func = require(module)
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

        if not response or response["success"] ~= true then
            error(response and response["message"] or "HTTP request failed (no response)", 3)
        end

        return {
            Success = response.success,
            StatusCode = response.status_code,
            StatusMessage = response.status_message,
            Headers = response.headers,
            Body = response.body
        }
    end

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

            local decoded = Environment.base64.decode(data.source)
            
            local func, err = Bridge:Loadstring(data.source, "loadstring")
            
            if not func then
                resp.message = tostring(err)
                return sendResponse(resp)
            end

            setfenv(func, Utils:MergeTable(getfenv(0), Environment))
            
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

    function proxyobject(obj, namecalls)
        if proxyobjects[obj] then
            return proxyobjects[obj].proxy
        end
        namecalls = namecalls or {}
        local proxy = newproxy(true)
        local meta = getmetatable(proxy)
        meta.__index = function(...)return index(...)end
        meta.__namecall = function(...)return namecall(...)end
        meta.__newindex = function(...)return newindex(...)end
        meta.__tostring = function(...)return ptostring(...)end
        meta.__metatable = getmetatable(obj)

        local data = {}
        data.object = obj
        data.proxy = proxy
        data.meta = meta
        data.namecalls = namecalls

        proxied[proxy] = data
        proxyobjects[obj] = data
        return proxy
    end

    --=============================================================================--
    -- ## Metatables
    --=============================================================================--

    type userdata = {}
    type _function = (...any) -> (...any)

    local metatable = {
        metamethods = {
            __index = function(self, key)
                return self[key]
            end,
            __newindex = function(self, key, value)
                self[key] = value
            end,
            __call = function(self, ...)
                return self(...)
            end,
            __concat = function(self, b)
                return self..b
            end,
            __add = function(self, b)
                return self + b
            end,
            __sub = function(self, b)
                return self - b
            end,
            __mul = function(self, b)
                return self * b
            end,
            __div = function(self, b)
                return self / b
            end,
            __idiv = function(self, b)
                return self // b
            end,
            __mod = function(self, b)
                return self % b
            end,
            __pow = function(self, b)
                return self ^ b
            end,
            __tostring = function(self)
                return tostring(self)
            end,
            __eq = function(self, b)
                return self == b
            end,
            __lt = function(self, b)
                return self < b
            end,
            __le = function(self, b)
                return self <= b
            end,
            __len = function(self)
                return #self
            end,
            __iter = function(self)
                return next, self
            end,
            __namecall = function(self, ...)
                return self:_(...)
            end,
            __metatable = function(self)
                return getmetatable(self)
            end
        }
    }

    -- methods
    function metatable.get_L_closure(metamethod: string, obj: {any} | userdata)
        local hooked
        local metamethod_emulator = metatable.metamethods[metamethod]
        
        xpcall(function()
            metamethod_emulator(obj)
        end, function()
            hooked = debug.info(2, "f")
        end)
        
        return hooked
    end

    function metatable.get_all_L_closures(obj: {any} | userdata)
        local metamethods = {}
        local innacurate = {}

        for method, _ in metatable.metamethods do
            local metamethod, accurate = metatable.get_L_closure(method, obj)
            metamethods[method] = metamethod
        end

        return metamethods
    end

    function metatable.metahook(t: any, f: _function)
        local metahook = {
            __metatable = getmetatable(t) or "The metatable is locked"
        }

        for metamethod, value in metatable.metamethods do
            metahook[metamethod] = function(self, ...)
                f(metamethod, ...)
                
                if metamethod == "__tostring" then
                    return ""
                elseif metamethod == "__len" then
                    return math.random(0, 1024)
                end
                
                return metatable.metahook({}, f) 
            end
        end

        return setmetatable({}, metahook)
    end

    --=============================================================================--
    -- ## Environment
    --=============================================================================--

    -- Base64 Implementation
    Environment.base64 = {}

    function Environment.base64.encode(data)
        local b = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
        if data == nil then 
            error("base64.encode expected string, got nil", 2)
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

    function Environment.base64.decode(data)
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

    function Environment.GenerateKey(len)
        local key = ''
        local x = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
        for i = 1, len or 32 do local n = math.random(1, #x) key = key .. x:sub(n, n) end
        return base64.encode(key)
    end

    function Environment.Encrypt(a, b)
        local result = {}
        a = tostring(a) b = tostring(b)
        for i = 1, #a do
            local byte = string.byte(a, i)
            local keyByte = string.byte(b, (i - 1) % #b + 1)
            table.insert(result, string.char(bit32.bxor(byte, keyByte)))
        end
        return table.concat(result), b
    end

    function Environment.Hash(txt, hashName)
        if type(txt) ~= "string" then
            error("invalid argument #1 (string expected, got " .. type(txt) .. ")")
        end
        
        if type(hashName) ~= "string" then
            error("invalid argument #2 (string expected, got " .. type(hashName) .. ")")
        end
        
        for name, func in pairs(HashLib) do
            if name == hashName or name:gsub("_", "-") == hashName then
                return func(txt)
            end
        end
        
        error("invalid hash algorithm: " .. tostring(hashName))
    end

    function Environment.GenerateBytes(len)
        return Environment.GenerateKey(len)
    end

    function Environment.Random(len)
        return Environment.GenerateKey(len)
    end

    Environment.base64_encode = Environment.base64.encode
    Environment.base64_decode = Environment.base64.decode
    Environment.base64encode = Environment.base64.encode
    Environment.base64decode = Environment.base64.decode
    -- endregion

    -- Executor Functions
    -- =============================================================================
    function Environment.getgenv()
        local genv = getfenv(0)
        
        return setmetatable({}, {
            __index = function(_, k)
                return genv[k]
            end,
            __newindex = function(_, k, v)
                genv[k] = v
            end,
            __pairs = function()
                return pairs(genv)
            end,
            __ipairs = function()
                return ipairs(genv)
            end
        })

        --[[
            test("getgenv", {}, function()
                getgenv().__TEST_GLOBAL = true
                assert(__TEST_GLOBAL, "Failed to set a global variable")
                getgenv().__TEST_GLOBAL = nil
            end)
        ]]
    end

    function Environment.require(modulescript)
        assert(typeof(modulescript) == "Instance", "invalid argument #1 to 'require' (ModuleScript expected, got " .. typeof(modulescript) .. ") ", 2)
        assert(modulescript.ClassName == "ModuleScript", "invalid argument #1 to 'require' (ModuleScript expected, got " .. modulescript.ClassName .. ") ", 2)

        if not unlocked_modules[modulescript] then
            local success, err = Bridge:UnlockModule(modulescript)
            if success then unlocked_modules[modulescript] = true end
        end

        for i, v in pairs(modulescript:GetDescendants()) do
            if v.ClassName == "ModuleScript" and not unlocked_modules[v] then
                local success, err = Bridge:UnlockModule(v)
                if success then unlocked_modules[v] = true end
            end
        end

        local parent = modulescript.Parent
        if parent and parent.ClassName == "ModuleScript" and not unlocked_modules[parent] then
            local success, err = Bridge:UnlockModule(parent)
            if success then unlocked_modules[parent] = true end
        end

        if not proxyobjects[modulescript] then
            proxyobject(modulescript)
        end
        
        return _require(modulescript)
    end

    function Environment.request(options)
        assert(typeof(options) == "table", "invalid argument #1 to 'request' (table expected, got " .. typeof(options) .. ") ", 2)
        assert(typeof(options.Url) == "string", "invalid option 'Url' for argument #1 to 'request' (string expected, got " .. typeof(options.Url) .. ") ", 2)
        
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

    function Environment.loadstring(chunk)
        assert(typeof(chunk) == "string", "invalid argument #1 to 'loadstring' (string expected, got " .. typeof(chunk) .. ") ", 2)
        
        local chunk_name = math.random(1000, 9999)
        local encoded_chunk = Environment.base64.encode(chunk)

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

    function Environment.getscriptbytecode(script)
        assert(typeof(script) == "Instance", "invalid argument #1 to 'getscriptbytecode' (Script expected)", 2)
        assert(script.ClassName == "ModuleScript" or script.ClassName == "LocalScript", "invalid argument #1 to 'getscriptbytecode' (ModuleScript/LocalScript expected)", 2)
        
        local path = script:GetFullName()
        local response = Bridge:SendAndReceive({
            ["action"] = "getscriptbytecode",
            ["script_path"] = path
        })

        if not response or response["success"] ~= true then
            error(response and response["message"] or "getscriptbytecode failed (no response)", 2)
        end

        return Environment.base64.decode(response["bytecode"])
    end

    function Environment.getexecutorname()
        return "Oracle"
    end

    function Environment.getexecutorversion()
        return "V"..VERSION
    end

    function Environment.identifyexecutor()
        return Environment.getexecutorname(), Environment.getexecutorversion()
    end
    -- endregion

    -- Proxy
    -- =============================================================================
    Environment.game = {}
    setmetatable(Environment.game, {
        __index = function(self, index)
            if index == "HttpGet" or index == "HttpGetAsync" then
                -- HttpGet aliased to utility function
                return function(_, ...)
                    return Utils:HttpGet(...)
                end
            end

            if type(_game[index]) == "function" then
                -- Proxy for methods on `game` (e.g., game:GetService)
                return function(_, ...)
                    return _game[index](_game, ...)
                end
            end

            -- Direct property access
            return _game[index]
        end,

        __tostring = function(self)
            return _game.Name
        end,

        __metatable = getmetatable(_game)
    })

    function rconsolecreate(title)
        local resp = Bridge:SendAndReceive({ ["action"] = "rconsolecreate", ["title"] = title })
        if resp and resp["success"] and resp["console_id"] then
            return resp["console_id"]
        end
        return nil
    end

    function rconsoledestroy(id)
        local resp = Bridge:SendAndReceive({ ["action"] = "rconsoledestroy", ["console_id"] = id })
        return resp and resp["success"] == true
    end

    function rconsoleclear(id)
        local resp = Bridge:SendAndReceive({ ["action"] = "rconsoleclear", ["console_id"] = id })
        return resp and resp["success"] == true
    end

    function rconsoleprint(...)
        local args = { ... }
        if #args == 0 then
            -- Handle empty print for logging purposes
            local resp = Bridge:SendAndReceive({ ["action"] = "rconsoleprint" })
            return resp and resp["success"] == true
        end

        local outputParts = {}
        for i = 1, #args do
            table.insert(outputParts, tostring(args[i]))
        end
        local out = table.concat(outputParts, "\t")

        local resp = Bridge:SendAndReceive({ ["action"] = "rconsoleprint", ["text"] = out })
        return resp and resp["success"] == true
    end

    function rconsoleinput(prompt)
        local resp = Bridge:SendAndReceive({ ["action"] = "rconsoleinput", ["prompt"] = prompt })
        if resp and resp["success"] then
            return resp["text"] or ""
        end
        return ""
    end

    function rconsolesettitle(title, id)
        local resp = Bridge:SendAndReceive({ ["action"] = "rconsolesettitle", ["title"] = title, ["console_id"] = id })
        return resp and resp["success"] == true
    end

    rconsolename = rconsolesettitle
    consoleclear = rconsoleclear
    consolecreate = rconsolecreate
    consoledestroy = rconsoledestroy
    consoleinput = rconsoleinput
    consoleprint = rconsoleprint
    consolesettitle = rconsolesettitle

    function setclipboard(text)
        assert(type(text) == "string", "setclipboard expects a string")
        local resp = Bridge:SendAndReceive({ ["action"] = "setclipboard", ["text"] = text })
        return resp and resp["success"] == true
    end

    function toclipboard(text)
        return setclipboard(text)
    end

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

    function ToProxy(...)
        local packed = table.pack(...)
        local function LookTable(t)
            for i, obj in ipairs(t) do
                if rtypeof(obj) == "Instance" then
                    if proxyobjects[obj] then
                        t[i] = proxyobjects[obj].proxy
                    else
                        t[i] = proxyobject(obj)
                    end
                elseif typeof(obj) == "table" then
                    LookTable(obj)
                else
                    t[i] = obj
                end
            end
        end
        LookTable(packed)
        return table.unpack(packed, 1, packed.n)
    end

    function ToObject(...)
        local packed = table.pack(...)
        local function LookTable(t)
            for i, obj in ipairs(t) do
                if rtypeof(obj) == "userdata" then
                    if proxied[obj] then
                        t[i] = proxied[obj].object
                    else
                        t[i] = obj
                    end
                elseif typeof(obj) == "table" then
                    LookTable(obj)
                else
                    t[i] = obj
                end
            end
        end
        LookTable(packed)
        return table.unpack(packed, 1, packed.n)
    end

    local function index(t, n)
        local data = proxied[t]
        local namecalls = data.namecalls
        local obj = data.object
        if namecalls[n] then
            return function(self, ...)
                return ToProxy(namecalls[n](...))
            end
        end
        local v = obj[n]
        if typeof(v) == "function" then
            return function(self, ...)
                return ToProxy(v(ToObject(self, ...)))
            end
        else
            return ToProxy(v)
        end
    end

    local function newindex(t, n, v)
        local data = proxied[t]
        local obj = data.object
        local val = table.pack(ToObject(v))
        obj[n] = table.unpack(val)
    end

    local function ptostring(t)
        return t.Name
    end

    local function plainFind(str, pat)
        return string.find(str, pat, 0, true)
    end

    local function streamer(str): Streamer
        local Stream = {}
        Stream.Offset = 0
        Stream.Source = str
        Stream.Length = string.len(str)
        Stream.IsFinished = false	
        Stream.LastUnreadBytes = 0

        function Stream.read(self: Streamer, len: number?, shift: boolean?): string
            local len = len or 1
            local shift = if shift ~= nil then shift else true
            local dat = string.sub(self.Source, self.Offset + 1, self.Offset + len)

            local dataLength = string.len(dat)
            local unreadBytes = len - dataLength

            if shift then
                self:seek(len)
            end

            self.LastUnreadBytes = unreadBytes
            return dat
        end

        function Stream.seek(self: Streamer, len: number)
            local len = len or 1

            self.Offset = math.clamp(self.Offset + len, 0, self.Length)
            self.IsFinished = self.Offset >= self.Length
        end

        function Stream.append(self: Streamer, newData: string)
            -- adds new data to the end of a stream
            self.Source ..= newData
            self.Length = string.len(self.Source)
            self:seek(0) --hacky but forces a recalculation of the isFinished flag
        end

        function Stream.toEnd(self: Streamer)
            self:seek(self.Length)
        end

        return Stream
    end

    function get_real_address(instance)
        --[[
        assert(typeof(instance) == "Instance", "invalid argument #1 to 'get_real_address' (Instance expected, got " .. typeof(instance) .. ") ", 2)
        local objectValue = Instance.new("ObjectValue", objectPointerContainer)
        objectValue.Name = HttpService:GenerateGUID(false)
        objectValue.Value = instance
        
        local result = Bridge:InternalRequest({
            ['c'] = "adr",
            ['cn'] = objectValue.Name,
            ['pid'] = tostring(ProcessID)
        })
        objectValue:Destroy()
        if tonumber(result) then
            return tonumber(result)
        end
        return 0
        
        -- todo
        ]]
    end

    -- WebSocket.connect Implementation
    -- =============================================================================
    Environment.WebSocket = Environment.WebSocket or {}
    local WebSocket = Environment.WebSocket

    function WebSocket.connect(url)
        assert(type(url) == "string", "WebSocket.connect expects a url string")
        local raw = WebSocketService:CreateClient(url)
        local conn = { _raw = raw, OnMessage = {}, OnClose = {}, OnOpen = {} }

        local function callHandlers(list, ...)
            if type(list) == "function" then pcall(list, ...) return end
            if type(list) == "table" then
                for _, cb in ipairs(list) do if cb then pcall(cb, ...) end end
            end
        end

        function conn:Send(msg)
            if type(msg) ~= "string" then msg = tostring(msg) end
            pcall(function() raw:Send(msg) end)
        end

        function conn:Close()
            pcall(function() raw:Close() end)
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

    local clonerefs = {}
    
    function Environment.cloneref(obj)
        local proxy = newproxy(true)
        local meta = getmetatable(proxy)

        meta.__index = function(t, n)
            local v = obj[n]
            if typeof(v) == "function" then
                return function(self, ...)
                    if self == t then
                        self = obj
                    end
                    return v(self, ...)
                end
            else
                return v
            end
        end

        meta.__newindex = function(t, n, v)
            obj[n] = v
        end

        meta.__tostring = function(t)
            return tostring(obj)
        end

        meta.__metatable = getmetatable(obj)
        clonerefs[proxy] = obj

        return proxy
    end

    function Environment.compareinstances(proxy1, proxy2)
        assert(type(proxy1) == "userdata", "Invalid argument #1 to 'compareinstances' (Instance expected, got " .. typeof(proxy1) .. ")")
        assert(type(proxy2) == "userdata", "Invalid argument #2 to 'compareinstances' (Instance expected, got " .. typeof(proxy2) .. ")")

        -- Check if they're clonerefs and get the originals
        if clonerefs[proxy1] then
            proxy1 = clonerefs[proxy1]
        end
        if clonerefs[proxy2] then
            proxy2 = clonerefs[proxy2]
        end
        
        -- Check if they're proxied objects and get the originals
        if proxied[proxy1] then
            proxy1 = proxied[proxy1].object
        end
        if proxied[proxy2] then
            proxy2 = proxied[proxy2].object
        end

        return proxy1 == proxy2
    end

    function Environment.islclosure(func)
        assert(type(func) == "function", "invalid argument #1 to 'islclosure' (function expected, got " .. type(func) .. ") ", 2)
        return debug.info(func, "s") ~= "[C]"
    end

    function Environment.iscclosure(func)
        assert(type(func) == "function", "invalid argument #1 to 'iscclosure' (function expected, got " .. type(func) .. ") ", 2)
        return debug.info(func, "s") == "[C]"
    end

    function Environment.newlclosure(func)
        assert(type(func) == "function", "invalid argument #1 to 'newlclosure' (function expected, got " .. type(func) .. ") ", 2)
        local cloned = function(...)
            return func(...)
        end
        return cloned
    end

    function Environment.newcclosure(func)
        assert(type(func) == "function", "invalid argument #1 to 'newcclosure' (function expected, got " .. type(func) .. ") ", 2)
        local cloned = coroutine.wrap(function(...)
            while true do
                coroutine.yield(func(...))
            end
        end)
        return cloned
    end

    function Environment.clonefunction(func)
        assert(type(func) == "function", "invalid argument #1 to 'clonefunction' (function expected, got " .. type(func) .. ") ", 2)
        if Environment.iscclosure(func) then
            return Environment.newcclosure(func)
        else
            return Environment.newlclosure(func)
        end
    end

    function Environment.Getproxyobjects(asset)
        return {
            game:GetService("InsertService"):LoadLocalAsset(asset)
        }
    end

    local function GenerateError(object)
        local _, err = xpcall(function()
            object:__namecall()
        end, function()
            return debug.info(2, "f")
        end)
        return err
    end

    local FirstTest = GenerateError(OverlapParams.new())
    local SecondTest = GenerateError(Color3.new())

    function Environment.getnamecallmethod()
        local _, err = pcall(FirstTest)
        local method = if type(err) == "string" then err:match("^(.+) is not a valid member of %w+$") else nil
        if not method then
            _, err = pcall(SecondTest)
            method = if type(err) == "string" then err:match("^(.+) is not a valid member of %w+$") else nil
        end

        if not method or method == "__namecall" then
            if cachedmethods[coroutine.running()] then
                return cachedmethods[coroutine.running()]
            end
            return nil
        end
        cachedmethods[coroutine.running()] = method
        return method
    end

    local function namecall(t, ...)
        local data = proxied[t]
        local namecalls = data.namecalls
        local obj = data.object
        local method = Environment.getnamecallmethod()
        if namecalls[method] then
            return ToProxy(namecalls[method](...))
        end
        return ToProxy(obj[method](ToObject(t, ...)))
    end

    function Environment.getinstances()
        return workspace.Parent:GetDescendants()
    end

    function Environment.getnilinstances()
        return nilinstances
    end

    function Environment.getloadedmodules()
        local scripts = {}
        for _, v in pairs(Environment.getinstances()) do
            if v:IsA("ModuleScript") and v.Parent ~= nil then table.insert(scripts, v) end
        end
        return scripts
    end

    function Environment.getrunningscripts()
        local scripts = {}
        for _, v in pairs(Environment.getinstances()) do
            if v:IsA("LocalScript") and v.Enabled then table.insert(scripts, v) end
        end
        return scripts
    end

    function Environment.getcallingscript()
        local s = debug.info(1, 's')
        for i, v in next, game:GetDescendants() do
        if v:GetFullName() == s then return v end
        end
        return nil
    end

    function Environment.gethui()
        return HUI
    end

    function lz4.compress(str: string): string
        local blocks: BlockData = {}
        local iostream = streamer(str)

        if iostream.Length > 12 then
            local firstFour = iostream:read(4)

            local processed = firstFour
            local lit = firstFour
            local match = ""
            local LiteralPushValue = ""
            local pushToLiteral = true

            repeat
                pushToLiteral = true
                local nextByte = iostream:read()

                if plainFind(processed, nextByte) then
                    local next3 = iostream:read(3, false)

                    if string.len(next3) < 3 then
                        --push bytes to literal block then break
                        LiteralPushValue = nextByte .. next3
                        iostream:seek(3)
                    else
                        match = nextByte .. next3

                        local matchPos = plainFind(processed, match)
                        if matchPos then
                            iostream:seek(3)
                            repeat
                                local nextMatchByte = iostream:read(1, false)
                                local newResult = match .. nextMatchByte

                                local repos = plainFind(processed, newResult) 
                                if repos then
                                    match = newResult
                                    matchPos = repos
                                    iostream:seek(1)
                                end
                            until not plainFind(processed, newResult) or iostream.IsFinished

                            local matchLen = string.len(match)
                            local pushMatch = true

                            if iostream.Length - iostream.Offset <= 5 then
                                LiteralPushValue = match
                                pushMatch = false
                                --better safe here, dont bother pushing to match ever
                            end

                            if pushMatch then
                                pushToLiteral = false

                                -- gets the position from the end of processed, then slaps it onto processed
                                local realPosition = string.len(processed) - matchPos
                                processed = processed .. match

                                table.insert(blocks, {
                                    Literal = lit,
                                    LiteralLength = string.len(lit),
                                    MatchOffset = realPosition + 1,
                                    MatchLength = matchLen,
                                })
                                lit = ""
                            end
                        else
                            LiteralPushValue = nextByte
                        end
                    end
                else
                    LiteralPushValue = nextByte
                end

                if pushToLiteral then
                    lit = lit .. LiteralPushValue
                    processed = processed .. nextByte
                end
            until iostream.IsFinished
            table.insert(blocks, {
                Literal = lit,
                LiteralLength = string.len(lit)
            })
        else
            local str = iostream.Source
            blocks[1] = {
                Literal = str,
                LiteralLength = string.len(str)
            }
        end

        -- generate the output chunk
        -- %s is for adding header
        local output = string.rep("\x00", 4)
        local function write(char)
            output = output .. char
        end
        -- begin working through chunks
        for chunkNum, chunk in blocks do
            local litLen = chunk.LiteralLength
            local matLen = (chunk.MatchLength or 4) - 4

            -- create token
            local tokenLit = math.clamp(litLen, 0, 15)
            local tokenMat = math.clamp(matLen, 0, 15)

            local token = bit32.lshift(tokenLit, 4) + tokenMat
            write(string.pack("<I1", token))

            if litLen >= 15 then
                litLen = litLen - 15
                --begin packing extra bytes
                repeat
                    local nextToken = math.clamp(litLen, 0, 0xFF)
                    write(string.pack("<I1", nextToken))
                    if nextToken == 0xFF then
                        litLen = litLen - 255
                    end
                until nextToken < 0xFF
            end

            -- push raw lit data
            write(chunk.Literal)

            if chunkNum ~= #blocks then
                -- push offset as u16
                write(string.pack("<I2", chunk.MatchOffset))

                -- pack extra match bytes
                if matLen >= 15 then
                    matLen = matLen - 15

                    repeat
                        local nextToken = math.clamp(matLen, 0, 0xFF)
                        write(string.pack("<I1", nextToken))
                        if nextToken == 0xFF then
                            matLen = matLen - 255
                        end
                    until nextToken < 0xFF
                end
            end
        end
        --append chunks
        local compLen = string.len(output) - 4
        local decompLen = iostream.Length

        return string.pack("<I4", compLen) .. string.pack("<I4", decompLen) .. output
    end

    function lz4.decompress(lz4data: string): string
        local inputStream = streamer(lz4data)

        local compressedLen = string.unpack("<I4", inputStream:read(4))
        local decompressedLen = string.unpack("<I4", inputStream:read(4))
        local reserved = string.unpack("<I4", inputStream:read(4))

        if compressedLen == 0 then
            return inputStream:read(decompressedLen)
        end

        local outputStream = streamer("")

        repeat
            local token = string.byte(inputStream:read())
            local litLen = bit32.rshift(token, 4)
            local matLen = bit32.band(token, 15) + 4

            if litLen >= 15 then
                repeat
                    local nextByte = string.byte(inputStream:read())
                    litLen += nextByte
                until nextByte ~= 0xFF
            end

            local literal = inputStream:read(litLen)
            outputStream:append(literal)
            outputStream:toEnd()
            if outputStream.Length < decompressedLen then
                --match
                local offset = string.unpack("<I2", inputStream:read(2))
                if matLen >= 19 then
                    repeat
                        local nextByte = string.byte(inputStream:read())
                        matLen += nextByte
                    until nextByte ~= 0xFF
                end

                outputStream:seek(-offset)
                local pos = outputStream.Offset
                local match = outputStream:read(matLen)
                local unreadBytes = outputStream.LastUnreadBytes
                local extra
                if unreadBytes then
                    repeat
                        outputStream.Offset = pos
                        extra = outputStream:read(unreadBytes)
                        unreadBytes = outputStream.LastUnreadBytes
                        match ..= extra
                    until unreadBytes <= 0
                end

                outputStream:append(match)
                outputStream:toEnd()
            end

        until outputStream.Length >= decompressedLen

        return outputStream.Source
    end

    function Environment.getscripthash(instance)
        assert(typeof(instance) == "Instance" and instance:IsA("LuaSourceContainer"), `arg #1 must be LuaSourceContainer`)

        return if instance:IsA("Script") then instance:GetHash() else instance:GetDebugId(0)
    end

    function Environment.getthreadidentity()
        local function try(fn, ...)
            local o = pcall(fn, ...)
            return o
        end

        local ourresults = {
            -- ! ITS IMPORTANT WE DONT USE ANY METHODS HERE THAT WE SPOOF
            -- ! HOPEFULLY Security Tags of these dont change
            -- PluginSecurity (1)
            try(function()
                return game:GetJobsInfo()
            end),
            -- LocalUserSecurity (3)
            try(function()
                return workspace:ExperimentalSolverIsEnabled()
            end),
            --WritePlayerSecurity (4)
            try(Instance.new, "Player"),
            --RobloxScriptSecurity (5)
            try(function()
                return game:GetPlaySessionId()
            end),
            --RobloxSecurity (6)
            try(function()
                return Instance.new("SurfaceAppearance").TexturePack
            end),
            --NotAccessibleSecurity (7)
            try(function()
                Instance.new("MeshPart").HasJointOffset = false
            end),
        }
        local permissionChart =
            { -- We go in reverse because LocalGui is equal to CommandBar in permissionChart it seems like (this way we can match properly, though we can't tell lvl 7 from 8 then but not like that's a big issue)
                { true, true, false, false, false, false }, -- LocalGui [1]
                { false, false, false, false, false, false }, -- GameScript [2]
                { true, true, false, true, false, false }, -- ElevatedGameScript [3]
                { true, true, false, false, false, false }, -- CommandBar [4]
                { true, false, false, false, false, false }, -- StudioPlugin [5]
                { true, true, false, true, false, false }, -- ElevatedStudioPlugin [6]
                { true, true, true, true, true, true }, -- COM [7] Level 7 WOOHOO
                { true, true, true, true, true, true }, -- WebService [8] WOW LVL 8
                { false, false, true, true, false, false }, -- Replicator [9]
            }

        for level = #permissionChart, 1, -1 do
            local securityInfo = permissionChart[level]

            local match = true
            for i, canAccess in securityInfo do
                if canAccess ~= ourresults[i] then
                    match = false
                    break
                end
            end
            if match then
                return level
            end
        end
        return 0 -- None
    end

    function Environment.getexecutioncontext()
        local RunService = game:GetService("RunService")

        return if RunService:IsClient()
            then "Client"
            elseif RunService:IsServer() then "Server"
            else if RunService:IsStudio() then "Studio" else "Unknown"
    end

    function deepclone(a)
        local Result = {}
        for i, v in pairs(a) do
            if type(v) == 'table' then
                Result[i] = funcs.deepclone(v)
            end
            Result[i] = v
        end
        return Result
    end

    function Environment.setreadonly(tbl, cond)
        if cond then
            table.freeze(tbl)
        else
            return deepclone(tbl)
        end
    end

    function Environment.isscriptable(instance, property_name)
        local ok, Result = xpcall(instance.GetPropertyChangedSignal, function(result)
            return result
        end, instance, property_name)

        return ok or not string.find(Result, "scriptable", nil, true)
    end

    function Environment.setscriptable(instance, property_name, scriptable)
        assert(typeof(instance) == "Instance", `arg #1 must be type Instance`)
        assert(type(property_name) == "string", `arg #2 must be type string`)
        assert(type(scriptable) == "boolean", `arg #3 must be type bolean`)
        if Environment.isscriptable(instance, property_name) then
            return false
        end

        -- return bridge:send("set_scriptable", instance, property_name, scriptable)
    end

    function Environment.gethiddenproperties(instance)
        assert(typeof(instance) == "Instance", `arg #1 must be type Instance`)

        local hidden_properties = {}

        -- For now, return empty table since getproperties is not implemented
        -- TODO: Implement getproperties or use reflection to get all properties
        
        return hidden_properties
    end

    function Environment.gethiddenproperty(instance, property_name)
        assert(typeof(instance) == "Instance", `arg #1 must be type Instance`)
        assert(type(property_name) == "string", `arg #2 must be type string`)
        if Environment.isscriptable(instance, property_name) then
            return instance[property_name] -- * This will error if it's an invalid property but that should intended
        end
        
        return Environment.gethiddenproperties(instance)[property_name]
    end

    function Environment.sethiddenproperty(instance, property_name, value)
        assert(typeof(instance) == "Instance", `arg #1 must be type Instance`)
        assert(type(property_name) == "string", `arg #2 must be type string`)

        -- TODO If we can't figure out how to setscriptable and access property in lua without crashing then just bridge this function entirely

        -- local was_scriptable = script_env.setscriptable(instance, property_name, true)
        -- local o, err = pcall(function()
        --     instance[property_name] = value
        -- end)
        -- if not was_scriptable then
        --     script_env.setscriptable(instance, property_name, was_scriptable)
        -- end
        -- if o then
        --     return was_scriptable
        -- else
        --     error(err, 2)
        -- end
    end

    function Environment.isrbxactive()
        return true
    end

    function Environment.getinfo(f, options)
        if type(options) == "string" then
            options = string.lower(options) -- if someone adds "L" for activelines and "l" for currentline then thats on them (it will just slow down this function because duplicate debug.infos)
        else
            options = "sflnu"
        end

        local result = {}

        for index = 1, #options do
            local option = string.sub(options, index, index)
            if "s" == option then
                local short_src = debug.info(f, "s")

                result.short_src = short_src
                result.source = "@" .. short_src
                result.what = if short_src == "[C]" then "C" else "Lua"
            elseif "f" == option then
                result.func = debug.info(f, "f")
            elseif "l" == option then
                result.currentline = debug.info(f, "l")
            elseif "n" == option then
                result.name = debug.info(f, "n")
            elseif "u" == option or option == "a" then
                local numparams, is_vararg = debug.info(f, "a")
                result.numparams = numparams
                result.is_vararg = if is_vararg then 1 else 0

                if "u" == option then
                    result.nups = -1 --#debug.getupvalues(f)
                end
            end
        end

        return result
    end

    function Environment.isexecutorclosure(func)
        if Environment.iscclosure(func) then
            return debug.info(func, "n") == "" -- * Hopefully there aren't any false positives
        end
        local f_env = getfenv(func)
        return f_env.script.Parent == nil or f_env == getfenv(0) -- TODO The second part can be fooled if isexecutorclosure(HijackedModule.Function)
    end

    function Environment.checkcaller()
        return 3 <= Environment.getthreadidentity()
    end

    function Environment.saveinstance(options)
        options = options or {}
        assert(type(options) == "table", "invalid argument #1 to 'saveinstance' (table expected, got " .. type(options) .. ") ", 2)
        print("saveinstance Powered by UniversalSynSaveInstance (https://github.com/luau/UniversalSynSaveInstance)")
        return Environment.loadstring(Environment.HttpGet("https://raw.githubusercontent.com/luau/SynSaveInstance/main/saveinstance.luau", true), "saveinstance")()(options)
    end

    function Environment.get_hwid()
        return "67" -- to do: bridge 
    end

    function Environment.fireclickdetector(part)
        assert(typeof(part) == "Instance", "invalid argument #1 to 'fireclickdetector' (Instance expected, got " .. type(part) .. ") ", 2)
        local clickDetector = part:FindFirstChild("ClickDetector") or part
        local previousParent = clickDetector.Parent

        local newPart = Instance.new("Part", workspace)
        do
            newPart.Transparency = 1
            newPart.Size = Vector3.new(30, 30, 30)
            newPart.Anchored = true
            newPart.CanCollide = false
            delay(15, function()
                if newPart:IsDescendantOf(game) then
                    newPart:Destroy()
                end
            end)
            clickDetector.Parent = newPart
            clickDetector.MaxActivationDistance = math.huge
        end

        -- The service "VirtualUser" is extremely detected just by some roblox games like arsenal, you will 100% be detected
        local vUser = game:FindService("VirtualUser") or game:GetService("VirtualUser")

        local connection = RunService.Heartbeat:Connect(function()
            local camera = workspace.CurrentCamera or workspace.Camera
            newPart.CFrame = camera.CFrame * CFrame.new(0, 0, -20) * CFrame.new(camera.CFrame.LookVector.X, camera.CFrame.LookVector.Y, camera.CFrame.LookVector.Z)
            vUser:ClickButton1(Vector2.new(20, 20), camera.CFrame)
        end)

        clickDetector.MouseClick:Once(function()
            connection:Disconnect()
            clickDetector.Parent = previousParent
            newPart:Destroy()
        end)
    end

    --[[function Environment.firetouchinterest(toucher, toTouch, touch_state)
        assert(typeof(toucher) == "Instance", "invalid argument #1 to 'firetouchinterest' (Instance expected, got " .. type(toucher) .. ") ")
        assert(typeof(toTouch) == "Instance", "invalid argument #2 to 'firetouchinterest' (Instance expected, got " .. type(toTouch) .. ") ")
        assert(type(touch_state) == "number", "invalid argument #3 to 'firetouchinterest' (number expected, got " .. type(touch_state) .. ") ")

        if not touchers_reg[toucher] then
            touchers_reg[toucher] = {}
        end

        local toTouchAddress = tostring(get_real_address(toTouch))

        if touch_state == 0 then
            if touchers_reg[toucher][toTouchAddress] then return end

            local newPart = Instance.new("Part", toTouch)
            newPart.CanCollide = false
            newPart.CanTouch = true
            newPart.Anchored = true
            newPart.Transparency = 1

            Xeno.Xeno.spoof_instance(newPart, toTouch)
            touchers_reg[toucher][toTouchAddress] = task.spawn(function()
                while task.wait() do
                    newPart.CFrame = toucher.CFrame
                end
            end)
        elseif touch_state == 1 then
            if not touchers_reg[toucher][toTouchAddress] then return end
            Xeno.Xeno.spoof_instance(toTouch, tonumber(toTouchAddress))
            local toucher_thread = touchers_reg[toucher][toTouchAddress]
            task.cancel(toucher_thread)
            touchers_reg[toucher][toTouchAddress] = nil
        end
    end
    
    -- todo
    ]]

    function Environment.setsimulationradius(newRadius, newMaxRadius)
        newRadius = tonumber(newRadius)
        newMaxRadius = tonumber(newMaxRadius) or newRadius
        assert(type(newRadius) == "number", "invalid argument #1 to 'setsimulationradius' (number expected, got " .. type(newRadius) .. ") ", 2)

        local lp = game:FindService("Players").LocalPlayer
        if lp then
            lp.SimulationRadius = newRadius
            lp.MaximumSimulationRadius = newMaxRadius or newRadius
        end
    end

    function Environment.isreadonly(t)
        assert(type(t) == "table", "invalid argument #1 to 'isreadonly' (table expected, got " .. type(t) .. ") ", 2)
        return table.isfrozen(t)
    end

    function cache.iscached(t)
        if typeof(t) ~= "Instance" then
            return false
        end

        -- temporarily parent it if it has no parent
        local previousParent = t.Parent
        if not previousParent then
            t.Parent = Oracle
        end

        -- respect manual invalidation first
        if cache.invalidated[t] then
            if not previousParent then t.Parent = nil end
            return false
        end

        -- already cached
        if cache.cached[t] then
            if not previousParent then t.Parent = nil end
            return true
        end

        -- cache it if not in game
        if not t:IsDescendantOf(game) then
            cache.cached[t] = true
            if not previousParent then t.Parent = nil end
            return true
        end

        if not previousParent then t.Parent = nil end
        return false
    end

    function cache.invalidate(t)
        cache.cached[t] = nil
        cache.invalidated[t] = true
        t.Parent = nil  
    end

    function cache.replace(x, y)
        if cache.cached[x] then
            cache.cached[x] = y
        end
        y.Parent = x.Parent
        y.Name = x.Name
        x.Parent = nil
    end

    function Environment.getgc()
        return table.clone(nilinstances)
    end

    function Environment.getsenv(script_instance)
        local env = getfenv(2)

        return setmetatable({
            script = script_instance,
        }, {
            __index = function(self, index)
                return env[index] or rawget(self, index)
            end,
            __newindex = function(self, index, value)
                xpcall(function()
                    env[index] = value
                end, function()
                    rawset(self, index, value)
                end)
            end,
        })
    end

    function Environment.getconnections(event)
        assert(event.Connect, "invalid argument #1 to 'getconnections' (event.Connect does not exist)", 2)
        local connections = {}
        for _, connection in ipairs(event:GetConnected()) do
            local connectinfo = {
                Enabled = connection.Enabled, 
                ForeignState = connection.ForeignState, 
                LuaConnection = connection.LuaConnection, 
                Function = connection.Function,
                Thread = connection.Thread,
                Fire = connection.Fire, 
                Defer = connection.Defer, 
                Disconnect = connection.Disconnect,
                Disable = connection.Disable, 
                Enable = connection.Enable,
            }

            table.insert(connections, connectinfo)
        end
        return connections
    end

    local OLD_ENVIRONMENT_RAW_LOAD = true
    type _function = (...any) -> (...any)

    local scheduled_tasks = {}

    function Environment.hookfunction(old: _function, new: _function, old_environment: {any}, run_on_seperate_thread: boolean?)
        if debug.info(old, "s") == "[C]" then
            print("c")
            local name = debug.info(old, "n")
            
            if old_environment[name] then
                old_environment[name] = new
            end
            
            return function(...)
                return old(...)
            end
        else
            local last_trace
            
            local function execute_hook()
                if new then
                    local current_trace = {
                        debug.info(3, "l"), debug.info(3, "s"), debug.info(3, "n"), debug.traceback()
                    }
                    
                    local equal = true
                    for i, v in last_trace or {} do
                        if current_trace[i] ~= v then
                            equal = false
                        end
                    end
                    
                    if not equal or not last_trace then
                        if run_on_seperate_thread then
                            table.insert(scheduled_tasks, coroutine.wrap(new))
                        else
                            new()
                        end
                    end
                    
                    return current_trace
                end
            end
            
            local function wrap()
                local hooks = {}
                
                for metamethod in Metatable.metamethods do
                    hooks[metamethod] = function(self, ...)
                        local f = debug.info(2, "f")

                        if f == old then
                            last_trace = execute_hook()
                        end
                        
                        if metamethod == "__len" then
                            return 3
                        elseif metamethod == "__tostring" then
                            return tostring(getfenv(0))
                        end
                        
                        return wrap()
                    end
                end
                
                return setmetatable({}, hooks)
            end
            
            local environment = wrap()
            setfenv(old, environment)
            
            if OLD_ENVIRONMENT_RAW_LOAD then
                for i, v in pairs(old_environment) do -- pairs bypasses __iter
                    environment[i] = v
                end
            end
            
            return function(...)
                setfenv(old, old_environment)
                
                local return_value
                if run_on_seperate_thread then
                    local vararg = {...}
                    local unpack = unpack
                    
                    table.insert(scheduled_tasks, coroutine.wrap(setfenv(function()
                        return_value = {old(unpack(vararg))}
                    end, old_environment)))
                else
                    return_value = {old(...)}
                end
                
                while not return_value do task.wait() end
                setfenv(old, wrap()) -- insert new hook once old gets executed
                
                return unpack(return_value)
            end
        end
	end

    task.spawn(function()
        while task.wait() do
            for i, new_task in scheduled_tasks do
                scheduled_tasks[i] = nil
                
                task.spawn(new_task)
            end
        end
    end)

    function type_check(argument_position: number, value: any, allowed_types: {any}, optional: boolean?)
        local formatted_arguments = table.concat(allowed_types, " or ")

        if value == nil and not optional and not table.find(allowed_types, "nil") then
            error(("missing argument #%d (expected %s)"):format(argument_position, formatted_arguments), 0)
        elseif value == nil and optional == true then
            return value
        end

        if not (table.find(allowed_types, typeof(value)) or table.find(allowed_types, type(value)) or table.find(allowed_types, value)) and not table.find(allowed_types, "any") then
            error(("invalid argument #%d (expected %s, got %s)"):format(argument_position, formatted_arguments, typeof(value)), 0)
        end

        return value
    end

    function Environment.getrawmetatable(obj: any): {any}
		type_check(1, obj, {"any"})

		local raw_mt = get_all_L_closures(obj)

		return setmetatable({
			__tostring = _cclosure(function(self)
				return tostring(self)
			end)
		}, {
			__index = raw_mt,
			__newindex = function(_, key, value)
				local success = pcall(function()
					getmetatable(obj)[key] = value
				end)

				if not success then error("attempt to write to a protected/read-only metatable", 0) end
			end
		})
    end

    function Environment.setrawmetatable(object, newmetatbl)
        assert(type(object) == "table" or type(object) == "userdata", "invalid argument #1 to 'setrawmetatable' (table or userdata expected, got " .. type(object) .. ") ", 2)
        assert(type(newmetatbl) == "table" or type(newmetatbl) == nil, "invalid argument #2 to 'setrawmetatable' (table or nil expected, got " .. type(object) .. ") ", 2)
       
        local raw_mt = Environment.debug.getmetatable(object)
        
        if raw_mt and raw_mt.__metatable then
            local old_metatable = raw_mt.__metatable
            raw_mt.__metatable = nil  
            local success, err = pcall(setmetatable, object, newmetatbl)
            raw_mt.__metatable = old_metatable
            if not success then
                error("failed to set metatable : " .. tostring(err), 2)
            end
            return true  
        end

        setmetatable(object, newmetatbl)
        return true
    end

    function Environment.hookmetamethod(obj, index, value)
        if typeof(index) ~= "string" then
            return nil
        end
        if typeof(value) ~= "function" then
            return nil
        end
        local meta = Environment.getrawmetatable(obj)
        if typeof(meta) ~= "table" then
            return nil
        end
        if meta[index] and typeof(meta[index]) == "function" then
            local old = Environment.clonefunction(meta[index])
            Environment.hookfunction(meta[index], value)
            print(old)
            return old
        end
        return nil
    end

    function Environment.GetScriptHash(scr)
        assert(typeof(scr) == 'Instance', 'Argument #1 to \'getscripthash\' must be an Instance, not ' .. typeof(scr))
        assert(scr.ClassName ~= 'LocalScript' or scr.ClassName ~= 'Script', 'Argument #1 to \'getscripthash\' must be a LocalScript or Script')
        return scr:GetHash()
    end

    function Environment.gethiddenproperty(a, b)
        local Success, Value = pcall(function()
            return UGCValidationService:GetPropertyValue(a, b)
        end)

        assert(Success, "Invalid argument #2 to 'gethiddenproperty', property is not a valid member of Instance")

        return Value
    end

    function Environment.getcustomasset(name)
        return "rbxasset://" .. name
    end

    function Environment.getrenv()
        return {
            warn,
            print,
            error
        }
    end

    --=============================================================================--
    -- ## Debug Library
    --=============================================================================--

    --========================--
    -- ## function decomp
    --========================--

    local FunctionDecomp = {}

    --// Settings

    -- Table
    local TABLE_MAX_RECURSION = 8
    local TABLE_MAX_ITERATIONS = 64
    local TABLE_TIMEOUT_PREVENTION_YIELD_CHANCE = 2

    -- Timeout
    local TIMEOUT_PREVENTION_YIELD_CHANCE = 1 
    local TIMEOUT_MAX_REPEATED_METAMETHODS = 5
    local TIMEOUT_MAX_TOTAL_FUNCTION_CALLS = 2048
    local TIMEOUT_MS = 5000

    --// Localization
    local setmetatable = setmetatable
    local pcall = pcall
    local table = table
    local debug = debug
    local string = string
    local coroutine = coroutine
    local setfenv = setfenv
    local getfenv = getfenv
    local require = require
    local task = task

    local function get_param_num(f)
        return debug.info(f, "a")
    end

    local function merge_t(a, b)
        local r = {}

        for i, v in a do r[i] = v end
        for i, v in b do r[i] = v end

        return r
    end

    local safe_letters = ("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_"):split("")
    local numbers = ("1234567890"):split("")

    local function round(n)
        return math.floor(n + 0.5)
    end

    local part1 = math.random(0, 2^30 - 1)  
    local part2 = math.random(0, 2^30 - 1)  
    local part3 = math.random(0, 2^30 - 1) 

    local rand64 = part1 * 2^30 + part2  --
    -- bigger randomness: rand64 = rand64 * 2^30 + part3

    local number_signature = round(tick() - rand64)

    local function is_string_safe(s: string)
        for i, char in s:split("") do
            if (i == 1 and table.find(numbers, char)) or not (table.find(safe_letters, char) or table.find(numbers, char)) then
                return false
            end
        end

        return true
    end

    local function concatenate_t(t: {any}, sep: string)
        local result = ""
        
        for _, v in t do
            result ..= tostring(v)..sep
        end
        
        return result
    end

    --// Sandbox
    function FunctionDecomp.sandbox(f: (...any) -> (...any), upvalues: {any}?, constants: {string}?, protos: {(...any) -> (...any)}?, i: number?, param_overrides: {any}?)
        upvalues = upvalues or {}
        constants = constants or {}
        protos = protos or {}
        i = i or 1

        local root = {
            root = true,
            children = {},
            stack = {},
            params = {},
            constants = constants,
            upvalues = upvalues,
            protos = protos,
            pc = 0,
            function_info = {debug.info(f, "na")},
            f = f,
            i = i,
            param_overrides = param_overrides,
            
            iteration_ends = {}
        }

        local id_i = 0
        local last_func

        local param_root = table.clone(root)
        param_root.is_param = true
        
        local wrapped = {}
        local wrap
        
        local function le(_self, b)
            root.pc += 1

            local parent = wrapped[_self]

            local pc = root.pc
            local stack = root.stack
            local self = {pc = pc, children = {}, parent = parent, arguments = {b}, metamethod = "__le", is_param = parent == param_root, self = _self}

            table.insert(parent.children, self)

            return true
        end
        
        local function lt(_self, b)
            root.pc += 1

            local parent = wrapped[_self]

            local pc = root.pc
            local stack = root.stack
            local self = {pc = pc, children = {}, parent = parent, arguments = {b}, metamethod = "__lt", is_param = parent == param_root, self = _self}

            table.insert(parent.children, self)

            return true
        end
        
        local last_metamethod_a, last_metamethod_b
        local last_arg_a, last_arg_b, last_arg_c
        local last_metamethod_count = 0
        
        local function_calls = 0
        
        function wrap(parent: {pc: number?, children: {any}, arguments: {any?}?})
            local hooks = {
                __le = le,
                __lt = lt
            }
            
            local t = {}

            for metamethod in Metatable.metamethods do
                if hooks[metamethod] then continue end
                
                hooks[metamethod] = function(_self, ...)
                    root.pc += 1
                    
                    local pc = root.pc
                    local stack = root.stack
                    local self = {pc = pc, children = {}, parent = parent, arguments = {...}, metamethod = metamethod, is_param = parent == param_root, self = _self, notes = ""}
                    
                    table.insert(parent.children, self)
                    
                    local a, b = ...
                    if (metamethod == last_metamethod_a or metamethod == last_metamethod_b or last_metamethod_a == last_metamethod_b) and (a == last_arg_c or a == last_arg_b or a == last_arg_a) then
                        last_metamethod_count += 1
                    else
                        last_metamethod_count = 0
                    end
                    
                    if last_metamethod_count >= TIMEOUT_MAX_REPEATED_METAMETHODS then
                        if metamethod ~= "__index" or last_metamethod_count > TIMEOUT_MAX_REPEATED_METAMETHODS + 2 then
                            self.notes = " -- repetition detected"
                            
                            return 
                        end
                    end
                    
                    last_metamethod_b = last_metamethod_a
                    last_metamethod_a = metamethod
                    
                    last_arg_c = last_arg_b
                    last_arg_b = last_arg_a
                    last_arg_a = a

                    if metamethod == "__len" then
                        return number_signature + pc
                    end
                    
                    local wrapped = wrap(self)
                    if metamethod == "__iter" then
                        local iter_i = wrap(self)
                        root.pc += 1
                        local iter_v = wrap(self)
                        
                        local iter_wrapper = {
                            [iter_i] = iter_v
                        }
                        
                        root.iteration_ends[pc] = 0
                        
                        local indexed = false
                        return function()
                            if indexed then
                                root.iteration_ends[pc] = root.pc
                                
                                return
                            end
                            
                            indexed = true
                            
                            return iter_i, iter_v, nil
                        end, iter_wrapper
                    end
                    
                    if metamethod == "__call" then
                        function_calls += 1
                        
                        if math.random(0, TIMEOUT_PREVENTION_YIELD_CHANCE * 100) == 0 then
                            task.wait()
                        end
                    elseif metamethod == "__tostring" then
                        return "[ leaked internal stack ]"
                    end
                    
                    if function_calls >= TIMEOUT_MAX_TOTAL_FUNCTION_CALLS then
                        coroutine.yield()
                    end
                    
                    return wrapped
                end
            end

            if root.pc ~= 0 then
                root.stack[t] = root.pc
            else
                root.stack[t] = id_i
                root.params[t] = id_i

                id_i += 1
            end
            
            local wrapper = setmetatable(t, hooks)
            wrapped[wrapper] = parent

            return wrapper
        end

        local env = wrap(root)
        local params = {}

        local param_num, vararg = get_param_num(f)

        for i = 1, param_num do
            local arg = wrap(param_root)

            table.insert(params, arg)
            root.params[arg] = root.i

            root.i += 1
        end

        if vararg then
            local vararg = wrap(param_root)

            table.insert(params, vararg)
            root.params[vararg] = "..."
        end
        
        if FunctionDecomp.vLuau then
            return root, params, env
        end
        
        local original_env = getfenv(f)
        local return_value

        task.spawn(function()
            return_value = {pcall(setfenv(f, env), unpack(params))}
        end)
        
        task.delay(TIMEOUT_MS / 1000, function()
            if root.return_value == nil then
                print("Decompilation timeout: Exhausted maximum time without return")
                
                return_value = {true, "__DECOMPILATION_TIMEOUT__"}
            end
        end)
        
        repeat
            task.wait()
        until return_value
        
        setfenv(f, original_env)

        root.return_value = table.move(return_value, 2, #return_value, 1, {})
        root.success = return_value[1]
        
        if not root.success then warn(unpack(root.return_value), root) end

        return root
    end

    --// Disassembler
    function FunctionDecomp.disassemble(tree: {any}, tabs: number?)
        tabs = tabs or 0
        task.wait()

        local tab_formatting = ("\t"):rep(tabs)

        local stack = tree.stack
        local params = tree.params
        local upvalues = tree.upvalues
        local final_pc = tree.pc
        local success = tree.success
        local function_info = tree.function_info
        local param_overrides = tree.param_overrides
        local i = tree.i

        local stack_offset do	
            stack_offset = (final_pc > 0 and 1) or 0
        end

        local disassembly = {}
        local constants = {}
        local protos = {}

        if final_pc > 0 then
            table.insert(disassembly, tab_formatting.."local _ = {};\n")
        end

        local pc = 0
        local in_for_loop = false

        local function find_in_t(value, t)
            for i, v in t do
                if v == value then
                    return i
                end
            end
        end

        local function format(value, recursion: number?, ignore_func_name: boolean?, global_func: boolean?, func_name_override: string?)
            recursion = (recursion and recursion + 1) or 0
            ignore_func_name = not not ignore_func_name
            global_func = not not global_func

            if recursion > 8 then
                return "{ --[[ recursion limit reached ]] }"
            end

            local type = type(value)

            local s_index = stack[value]
            local p_index = params[value]
            local uv_index = upvalues[value]

            if p_index then
                if p_index ~= "..." then
                    return ("_p%d"):format(p_index)
                end

                return p_index
            elseif s_index then
                return ("_[%d]"):format(s_index)
            elseif uv_index then
                if not param_overrides[value] then
                    disassembly[1] = ("%slocal _uv_%s = _[%s];\n"):format(tab_formatting, uv_index, uv_index)..(disassembly[1] or "")
                else
                    return ("_p%s"):format(uv_index)
                end

                return ("_uv_%d"):format(uv_index)
            end

            if type == "string" then
                local s = ""

                if #value > 10000 then
                    value = ("%s... (maximum length exceded)"):format(value:sub(1, 100))
                end

                for _, char in {value:byte(1, -1)} do
                    if char > 126 or char < 32 then
                        s ..= "\\"..char
                    else
                        s ..= string.char(char)
                    end
                end

                table.insert(constants, value)

                return ('"%s"'):format(s)
            elseif type == "table" then
                if stack[value] then return "{ --[[ leaked internal stack ]] }" end

                local passed = {}

                local iteration = 0
                local t = ""

                for i, v in pairs(value) do
                    if math.random(0, TABLE_TIMEOUT_PREVENTION_YIELD_CHANCE) == 0 then task.wait() end
                    
                    if v == value or i == value then
                        t ..= "--[[ self-reference... ]] "

                        continue
                    end

                    if table.find(passed, i) then continue end
                    table.insert(passed, i)

                    iteration += 1
                    if iteration > TABLE_MAX_ITERATIONS then continue end

                    if (typeof(i) == "string" and is_string_safe(i)) then
                        t ..= ("%s = %s; "):format(i, format(v, recursion))
                    else
                        t ..= ("[%s] = %s; "):format(format(i, recursion), format(v, recursion))
                    end
                end
                
                t = ("{ %s}"):format(t)
                
                local metatable = getmetatable(value)
                if metatable then
                    local mt = ""
                    
                    for i, v in pairs(metatable) do
                        if (typeof(i) == "string" and is_string_safe(i)) then
                            mt ..= ("%s = %s; "):format(i, format(v, recursion))
                        else
                            mt ..= ("[%s] = %s; "):format(format(i, recursion), format(v, recursion))
                        end
                    end
                    
                    local format = ("%ssetmetatable(%s, { %s})"):format(tab_formatting, t, mt)
                    
                    return format
                end

                return t
            elseif type == "function" then
                if not table.find(protos, value) and value ~= tree.f then
                    table.insert(protos, value)

                    local param_override = table.clone(params)

                    local sandbox = FunctionDecomp.sandbox(value, merge_t(merge_t(param_override, upvalues), stack), constants, protos, i, param_override)
                    local _disassembly = FunctionDecomp.disassemble(sandbox, tabs + 1)
                    local arguments = {}
                    local params = {}

                    for _, param in sandbox.params do
                        if param ~= 0 and tostring(param) ~= "..." then
                            table.insert(params, param)
                        end
                    end

                    table.sort(params, function(a, b)
                        return a < b
                    end)

                    for i, v in params do
                        table.insert(arguments, "_p"..v)
                    end

                    if sandbox.function_info[3] then
                        table.insert(arguments, "...")
                    end

                    if sandbox.function_info[1] == "" or ignore_func_name then
                        return ("function(%s) %s\n%send"):format(table.concat(arguments, ", "), _disassembly, tab_formatting)
                    else
                        disassembly[pc + 1] = ("%s%sfunction %s(%s) %s\n%send\n%s"):format(
                            tab_formatting,
                            (not global_func and "local ") or "",
                            func_name_override or sandbox.function_info[1],
                            table.concat(arguments, ", "),
                            _disassembly,
                            tab_formatting,
                            (not global_func and "\n") or ""
                        )..(disassembly[pc + 1] or "")

                        pc += 1

                        return sandbox.function_info[1]
                    end
                else
                    local func_name = debug.info(value, "n")

                    if func_name == "" then
                        return "debug.info(1, 'f') --[[ anonymous recursion ]]"
                    else
                        return (func_name_override or func_name).." --[[ recursion ]] " 
                    end
                end
            elseif type == "number" then
                if value > number_signature then
                    local potential_stack = value - number_signature
                    
                    if find_in_t(potential_stack - 1, stack) then
                        return ("_[%d]"):format(potential_stack)
                    end
                end
            end
            
            if typeof(value) == "Instance" or type == "userdata" and pcall(function() value:GetFullName(value.Parent) end) then
                local result = ""
                
                if value.Parent == nil then
                    local name = value.Name
                    if is_string_safe(name) and name ~= "" then
                        return "_nil." .. name
                    else
                        return "_nil[" .. format(name) .. "]"
                    end
                end
                
                while value do
                    local name = value.Name
                    if value:IsA("DataModel") then
                        name = "game"
                    end
                    
                    if is_string_safe(name) and name ~= "" then
                        name = "." .. name
                    else
                        name = "["..format(name).."]"
                    end
                    
                    result = name .. result
                    if not value:IsA("DataModel") and value.Parent == nil then
                        result = "_nil" .. result
                    end
                    
                    value = value.Parent
                    
                    task.wait()
                end
                
                if result:sub(-1) == "." then
                    result = result:sub(1, -2)
                end
                
                return result
            end

            if type == "userdata" or type == "vector" then
                local index = math.random(0, 0xFFF)
                stack[value] = index

                return "_unknown_"..index
            end

            return tostring(value)
        end

        local function format_tuple(...)
            local t = {}

            local last = 0
            for i, index in {...} do
                if i - last > 1 then
                    local void_size = i - last - 1
                    table.move(table.create(void_size, "nil"), i, void_size, 1, t)
                end

                table.insert(t, format(index))

                last = i
            end

            return table.concat(t, ", ")
        end
        
        local function parse(branch, parent)
            if math.random(0, TIMEOUT_PREVENTION_YIELD_CHANCE) == 0 then task.wait() end

            pc = branch.pc
            
            local metamethod = branch.metamethod
            local args = branch.arguments

            local parent_pc = (parent and parent.pc) or 0
            local a, b = args[1], args[2]
            local global = parent == nil
            
            local self = format(branch.self)
            
            local push = ""
            for i = 1, pc do
                local end_pc = tree.iteration_ends[i]
                
                if pc - 1 == end_pc then
                    tree.iteration_ends[i] = nil
                    in_for_loop = false
                    
                    push ..= "end;\n\n"
                end
            end
            
            if metamethod == "__index" then
                if global then
                    push ..= ("_[%d] = %s;"):format(pc, a or "(???)")

                    table.insert(constants, a)
                else
                    local parent_pc = parent_pc + ((in_for_loop and 1) or 0)
                    
                    if (type(a) == "string" and is_string_safe(a)) then
                        push ..= ("_[%d] = %s.%s;"):format(pc, self, a)

                        table.insert(constants, a)
                    else
                        push ..= ("_[%d] = %s[%s];"):format(pc, self, format(a))
                    end
                end
            elseif metamethod == "__newindex" then			
                if global then
                    if (type(b) == "function" and debug.info(b, "n") == "") then
                        table.insert(constants, a)

                        push ..= ("%s = %s;"):format(a or "(???)", format(b, nil, true))
                    else
                        format(b, nil, false, true, a)
                        push ..= ""
                    end
                else
                    if (type == "string" and is_string_safe(a)) then
                        table.insert(constants, a)

                        push ..= ("%s.%s = %s;"):format(self, a, format(b))
                    else
                        push ..= ("%s[%s] = %s;"):format(self, format(a), format(b))
                    end
                end
            elseif metamethod == "__call" then
                push ..= ("_[%s] = %s(%s);"):format(pc, self, format_tuple(unpack(args)))
            elseif metamethod == "__iter" then
                local i, v = pc, pc + 1
                
                push ..= ("\n%sfor __i%d, __v%d in %s do\n"):format(tab_formatting, i, v, self) ..
                    tab_formatting .. ("\t_[%d], _[%d] = __i%d, __v%d;\n"):format(i, v, i, v)
                
                pc += 1
                
                in_for_loop = true
            else
                local only_self = {
                    __len = "#",
                    __unm = "-"
                }

                local math = {
                    __add = "+",
                    __sub = "-",
                    __mul = "*",
                    __div = "/",
                    __idiv = "//",
                    __pow = "^",
                    __eq = "==",
                    __lt = "<",
                    __le = "<=",
                    __mod = "%",
                    __concat = ".."
                }

                local self_index, math_index = only_self[metamethod], math[metamethod]

                if self_index then
                    push ..= ("_[%d] = %s%s;"):format(pc, self_index, self)
                elseif math_index then
                    push ..= ("_[%d] = %s %s %s;"):format(pc, self, math_index, format(a))
                end
            end
            
            if in_for_loop then
                push = "\t"..push
            end
            
            disassembly[pc + stack_offset] = (push or "") .. branch.notes

            for _, child in branch.children do
                parse(child, branch)
            end
        end
        
        for _, child in tree.children do
            parse(child)
        end

        if success or FunctionDecomp.vLuau then
            local return_value = tree.return_value

            if final_pc > 0 then
                table.insert(disassembly, "")
            end
            
            if (return_value and #return_value > 0) then
                table.insert(disassembly, ((in_for_loop and "\t") or "") .. ("return %s;"):format(format_tuple(unpack(return_value))))
            else
                table.insert(disassembly, ((in_for_loop and "\t") or "") .. "return;")
            end
            
            if in_for_loop then
                table.insert(disassembly, "end;")
            end
        end
        
        local header = "-- %s, %d%s params, %d constants, %d protos\n"

        header = header:format(
            (function_info[1] == "" and "anonymous") or ("'%s'"):format(function_info[1]),
            function_info[2],
            (function_info[3] and "+") or "",
            #constants,
            #protos
        )

        disassembly = header..concatenate_t(disassembly, "\n"..("\t"):rep(tabs))

        return ((not success and not FunctionDecomp.vLuau and ("-- An error occured while decompiling (@pc %d)\n"):format(final_pc)) or "")..disassembly, constants, protos, success
    end

    sandbox = FunctionDecomp.sandbox
    disassemble = FunctionDecomp.disassemble

    local function decompile(f)
        local result = disassemble(sandbox(f))
        local disassembly = result[1]
        local constants = result[2]
        local protos = result[3]
        local success = result[4]

        return {disassembly, constants, protos, success}
    end

    local function call(self, f: (...any) -> (...any))
        return decompile(f)
    end

    local _debug = debug
    local debug_funcs = {}
    Environment.debug = {}

    debug_funcs.getinfo = function(f)
        type_check(1, f, {"number", "function"})

        if not pcall(getfenv, f) then
            error("invalid stack detected", 0)
        end

        if f == 0 then f = 1 end
        if type(f) == "number" then f += 1 end

        local s, n, a, v, l, fn = debug.info(f, "snalf")

        return {
            source = s,
            short_src = s,
            func = fn,
            what = (s == "[C]" and "C") or "Lua",
            currentline = l,
            name = n,
            nups = -1,
            numparams = a,
            is_vararg = (v and 1) or 0
        }
    end

    debug_funcs.getconstant = function(f, index)
        type_check(1, f, {"function", "number"})
        type_check(2, index, {"number"})

        if type(f) == "number" then
            f += 1
            if not pcall(getfenv, f + 1) then
                error("invalid stack level", 0)
            end
        end

        local decomp = FunctionDecomp(_debug.info(f, "f"))
        local constants = decomp[2]  -- constant table

        return constants[index]
    end

    debug_funcs.getconstants = function(f)
        type_check(1, f, {"function", "number"})

        if type(f) == "number" then
            f += 1
            if not pcall(getfenv, f + 1) then
                error("invalid stack level", 0)
            end
        end

        local decomp = decompile(_debug.info(f, "f"))
        return decomp[2]  -- the entire constant array
    end

    debug_funcs.getproto = function(f, index, active)
        type_check(1, f, {"function", "number"})
        type_check(2, index, {"number"})
        type_check(3, active, {"boolean"}, true)  -- active default = true

        if type(f) == "number" then
            f += 1
            if not pcall(getfenv, f + 1) then
                error("invalid stack level", 0)
            end
        end

        local decomp = decompile(_debug.info(f, "f"))
        local proto = decomp[3][index]

        if active then
            return { proto }
        else
            return proto
        end
    end

    debug_funcs.getprotos = function(f)
        type_check(1, f, {"function", "number"})

        if type(f) == "number" then
            f += 1
            if not pcall(getfenv, f + 1) then
                error("invalid stack level", 0)
            end
        end

        local decomp = decompile(_debug.info(f, "f"))
        return decomp[3]  -- array of protos
    end

    setmetatable(Environment.debug, {
        __index = function(_, key)
            if debug_funcs[key] then
                return debug_funcs[key] -- custom debug funcs
            end
            return _debug[key] -- still able to use built in funcs
        end,
        __metatable = getmetatable(_debug),
    })


    --=============================================================================--
    -- ## expose uncs
    --=============================================================================--

    Environment.rconsolecreate = rconsolecreate
    Environment.rconsoledestroy = rconsoledestroy
    Environment.rconsoleclear = rconsoleclear
    Environment.rconsoleprint = rconsoleprint
    Environment.rconsoleinput = rconsoleinput
    Environment.rconsolesettitle = rconsolesettitle
    Environment.rconsolename = rconsolesettitle

    Environment.setclipboard = setclipboard
    Environment.toclipboard = toclipboard

    Environment.readfile = readfile
    Environment.writefile = writefile
    Environment.appendfile = appendfile
    Environment.isfile = isfile
    Environment.isfolder = isfolder
    Environment.makefolder = makefolder
    Environment.listfiles = listfiles
    Environment.delfile = delfile
    Environment.delfolder = delfolder

    Environment.http = { request = Environment.request }
    Environment.http_request = Environment.request
    identifyexecutor = Environment.identifyexecutor
    getexecutorname = Environment.getexecutorname
    getexecutorversion = Environment.getexecutorversion

    Environment.crypt = {
        base64 = Environment.base64,
        base64_encode = Environment.base64.encode,
        base64_decode = Environment.base64.decode,
        base64encode = Environment.base64.encode,
        base64decode = Environment.base64.decode,
        generatekey = Environment.GenerateKey,

        encrypt = Environment.Encrypt,
        decrypt = Environment.Encrypt,
        generatebytes = Environment.GenerateBytes,
        random = Environment.Random,
        hash = Environment.Hash,
    }
    crypt = Environment.crypt
    base64 = Environment.base64
    base64encode = Environment.base64.encode
    base64decode = Environment.base64.decode
    base64_encode = Environment.base64.encode
    base64_decode = Environment.base64.decode

    websocket = Environment.websocket
    getscriptbytecode = Environment.getscriptbytecode
    loadstring = Environment.loadstring
    request = Environment.request
    getgenv = Environment.getgenv

    http = Environment.http
    http_request = Environment.http_request
    setclipboard = setclipboard
    toclipboard = toclipboard
    compareinstances = Environment.compareinstances

    cloneref = Environment.cloneref
    isluaclosure = Environment.islclosure
    islclosure = Environment.islclosure
    iscclosure = Environment.islclosure
    newlclosure = Environment.newlclosure

    newcclosure = Environment.newcclosure
    getproxyobjects = Environment.Getproxyobjects
    clonefunction = Environment.clonefunction
    getnamecallmethod = Environment.getnamecallmethod
    getinstances = Environment.getinstances

    getnilinstances = Environment.getnilinstances
    getloadedmodules = Environment.getloadedmodules
    getrunningscripts = Environment.getrunningscripts
    getscripts = getrunningscripts
    gethui = Environment.gethui

    lz4 = lz4
    lz4compress = lz4.compress
    lz4decompress = lz4.decompress
    getscripthash = Environment.getscripthash
    getthreadidentity = Environment.getthreadidentity

    getexecutioncontext = Environment.getexecutioncontext
    setreadonly = Environment.setreadonly
    isscriptable = Environment.isscriptable
    setscriptable = Environment.setscriptable
    gethiddenproperties = Environment.gethiddenproperties

    gethiddenproperty = Environment.gethiddenproperty
    sethiddenproperty = Environment.sethiddenproperty
    isrbxactive = Environment.isrbxactive
    getinfo = Environment.getinfo
    isexecutorclosure = Environment.isexecutorclosure

    checkclosure = isexecutorclosure
    isourclosure = isexecutorclosure
    checkcaller = Environment.checkcaller
    isgameactive = isrbxactive
    dumpstring = Environment.getscriptbytecode

    saveinstance = Environment.saveinstance
    savegame = saveinstance
    get_hwid = Environment.get_hwid
    fireclickdetector = Environment.fireclickdetector
    setsimulationradius = Environment.setsimulationradius

    iswindowactive = isrbxactive
    Environment.cache = cache
    getgc = Environment.getgc
    getsenv = Environment.getsenv
    getconnections = Environment.getconnections

    hookfunction = Environment.hookfunction
    replaceclosure = hookfunction
    getrawmetatable = Environment.getrawmetatable
    setrawmetatable = Environment.setrawmetatable
    getscripthash = Environment.getscirpthash

    getcustomasset = Environment.getcustomasset
    getidentity = getthreadidentity
    getthreadcontext = getthreadidentity
    getcallingscript = Environment.getcallingscript

    readfile = readfile
    writefile = writefile
    appendfile = appendfile
    isfile = isfile
    isfolder = isfolder
    makefolder = makefolder
    listfiles = listfiles
    delfile = delfile
    delfolder = delfolder

    --=============================================================================--
    -- ## final part
    --=============================================================================--

    for i, v in ipairs(game:GetDescendants()) do
        proxyobject(v)
    end

    game.DescendantAdded:Connect(proxyobject)

    workspace.Parent.DescendantRemoving:Connect(function(des)
        table.insert(nilinstances, des)
        delay(15, function()
            for i = #nilinstances, 1, -1 do
                if nilinstances[i] == des then
                    table.remove(nilinstances, i)
                    break
                end
            end
            cache.cached[des] = nil
        end)

        -- only mark it if not already invalidated
        if cache.cached[des] ~= nil then
            cache.cached[des] = true
        end
    end)

    workspace.Parent.DescendantAdded:Connect(function(des)
        cache.cached[des] = true
    end)

    client:Send(HttpService:JSONEncode({action = "initialize", pid = PROCESS_ID}))

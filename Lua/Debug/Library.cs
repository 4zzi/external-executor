using System;
using System.Text;
using RMemory; // Assumed existing in your project

namespace DebugLibrary
{
    public partial class Library
    {
        private IntPtr _L;

        // --- LUAU OFFSETS ---
        public const int LCLOSURE_PROTO_OFFSET = 0x20; 
        public const int PROTO_K_OFFSET = 0x18;        
        public const int PROTO_SIZEK_OFFSET = 0x44;    
        public const int TVALUE_SIZE = 16;             

        // --- LUAU TAGS ---
        public const int LUA_TNIL = 0;
        public const int LUA_TBOOLEAN = 1;
        public const int LUA_TNUMBER = 3;
        public const int LUA_TSTRING = 5;
        public const int LUA_TFUNCTION = 6;

        // Constructor
        public Library(IntPtr luaState)
        {
            _L = luaState;
        }

        // This allows usage like: DebugLibrary.Library.New(L)
        public static Library New(IntPtr luaState)
        {
            return new Library(luaState);
        }

        // Helper: Read TValue
        public object ReadTValue(IntPtr address)
        {
            int tag = Memory.Read<int>(address + 12); 
            switch (tag)
            {
                case LUA_TNUMBER: return Memory.Read<double>(address);
                case LUA_TBOOLEAN: return Memory.Read<bool>(address);
                case LUA_TSTRING: return ReadLuauString(Memory.Read<IntPtr>(address));
                case LUA_TNIL: return "nil";
                default: return $"Object(Tag:{tag})";
            }
        }

        // Helper: Read String
        public string ReadLuauString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "nil";
            try {
                int len = Memory.Read<int>(ptr + 0x14);
                if (len > 0 && len < 2048) {
                    byte[] b = Memory.ReadBytes(ptr + 0x18, len);
                    return Encoding.ASCII.GetString(b);
                }
                return "";
            } catch { return ""; }
        }
    }
}
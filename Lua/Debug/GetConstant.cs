using System;
using RMemory; // Assumed existing

namespace DebugLibrary
{
    public partial class Library
    {
        public object GetConstant(IntPtr functionAddress, int index)
        {
            // 1. Validation
            if (functionAddress == IntPtr.Zero) return null;

            // 2. Get Proto Pointer (LClosure->p)
            IntPtr protoPtr = Memory.Read<IntPtr>(functionAddress + LCLOSURE_PROTO_OFFSET);
            
            // If 0, likely a CClosure
            if (protoPtr == IntPtr.Zero) return null;

            // 3. Get Constants Array & Size
            IntPtr kArray = Memory.Read<IntPtr>(protoPtr + PROTO_K_OFFSET);
            int sizek = Memory.Read<int>(protoPtr + PROTO_SIZEK_OFFSET);

            // 4. Index Check (Lua is 1-based)
            if (index < 1 || index > sizek) return null;

            // 5. Calculate Address of Constant
            // kArray + ((index - 1) * 16)
            IntPtr constantAddress = kArray + ((index - 1) * TVALUE_SIZE);

            // 6. Read Value
            return ReadTValue(constantAddress);
        }
    }
}
//
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Xml.Serialization;
    using Blake3;
//  
    
//
    using RMemory;
    using Functions;
//

public static class Bytecodes
{
    public static void SetBytecode(RobloxInstance script, byte[] bytecode)
    {
        if (script.ClassName != "LocalScript" && script.ClassName != "ModuleScript")
            throw new Exception("[ERROR] Unsupported script");

        ulong embeddedOffset = script.ClassName == "LocalScript" ? (ulong)Offsets.LocalScript.ByteCode : (ulong)Offsets.ModuleScript.ByteCode;
        ulong embeddedPtr = Memory.ReadFrom<ulong>(script, embeddedOffset);

        ulong size = (ulong)bytecode.Length;

        ulong allocated = Memory.VirtualAlloc(size);
        if (allocated == 0)
            throw new Exception("[ERROR] Allocating failed");

        if (!Memory.Write(allocated, bytecode, size))
            throw new Exception("[ERROR] Writing failed");

        Memory.Write(embeddedPtr + 0x10, allocated);
        Memory.Write(embeddedPtr + 0x20, size);

        Console.WriteLine("[*] Set ByteCode");
    }
}
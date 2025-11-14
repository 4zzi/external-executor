//
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Xml.Serialization;
    using System.Buffers.Binary;
    using K4os.Hash.xxHash;
    using Blake3;
    using ZstdNet;
//  
    
//
    using RMemory;
    using Functions;
//

public static class Bytecodes
{
    public static readonly byte[] BYTECODE_SIGNATURE = new byte[]
    {
        (byte)'R',
        (byte)'S',
        (byte)'B',
        (byte)'1'
    };
    public const byte BYTECODE_HASH_MULTIPLIER = 41;
    public const uint BYTECODE_HASH_SEED = 42u;
    public const uint MAGIC_A = 0x4C464F52;
    public const uint MAGIC_B = 0x946AC432;
    public static readonly byte[] KEY_BYTES = new byte[]
    {
        0x52, 0x4F, 0x46, 0x4C
    };

    public static byte rotl8(byte value, int shift)
    {
        shift &= 7;
        return (byte)((value << shift) | (value >> (8 - shift)));
    }

    public static byte[] Decompress(byte[] compressed)
    {
        if (compressed.Length < 8)
            return Array.Empty<byte>();

        byte[] compressedData = compressed.ToArray();
        byte[] headerBuffer = new byte[4];

        for (int i = 0; i < 4; i++)
        {
            headerBuffer[i] = (byte)(compressedData[i] ^ BYTECODE_SIGNATURE[i]);
            headerBuffer[i] = (byte)(headerBuffer[i] - (i * BYTECODE_HASH_MULTIPLIER));
        }

        for (int i = 0; i < compressedData.Length; i++)
        {
            int xorValue = headerBuffer[i % 4] + (i * BYTECODE_HASH_MULTIPLIER);
            compressedData[i] ^= (byte)xorValue;
        }

        uint hashValue = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer);

        var hasher = new XXH32(BYTECODE_HASH_SEED);
        hasher.Update(compressedData, 0, compressedData.Length);
        uint rehash = hasher.Digest();

        if (rehash != hashValue)
            return Array.Empty<byte>();

        uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(compressedData.AsSpan(4, 4));
        byte[] decompressedBytes = new byte[decompressedSize];

        using (var decompressor = new ZstdNet.Decompressor())
        {
            byte[] result = decompressor.Unwrap(compressedData.AsSpan(8, compressedData.Length - 8).ToArray());

            if (result.Length != decompressedSize)
                return Array.Empty<byte>();

            Buffer.BlockCopy(result, 0, decompressedBytes, 0, result.Length);
        }

        return decompressedBytes; // Return byte[] instead of string
    }
   
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
    }

    public static byte[] GetBytecode(RobloxInstance script)
    {
        if (script.ClassName != "LocalScript" && script.ClassName != "ModuleScript")
            throw new InvalidOperationException($"Instance::GetBytecode(): {script.Name} is not a LocalScript or a ModuleScript");

        ulong embeddedOffset = (script.ClassName == "LocalScript")
            ? (ulong)Offsets.LocalScript.ByteCode
            : (ulong)Offsets.ModuleScript.ByteCode;

        ulong embeddedPtr = Memory.ReadFrom<ulong>(script, embeddedOffset);

        ulong bytecodePtr = Memory.Read<ulong>(embeddedPtr + 0x10);
        ulong bytecodeSize = Memory.Read<ulong>(embeddedPtr + 0x20);

        byte[] buffer = new byte[bytecodeSize];

        for (ulong i = 0; i < bytecodeSize; i++)
            buffer[i] = Memory.Read<byte>(bytecodePtr + i);

        // Pass raw bytes directly
        return Decompress(buffer);
    }
}
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
    //

    private static readonly byte[] BYTECODE_SIGNATURE = { (byte)'R', (byte)'S', (byte)'B', (byte)'1' };
    private const byte BYTECODE_HASH_MULTIPLIER = 41;
    private const uint BYTECODE_HASH_SEED = 42;
    private const uint MAGIC_A = 0x4C464F52;
    private const uint MAGIC_B = 0x946AC432;
    private static readonly byte[] KEY_BYTES = { 0x52, 0x4F, 0x46, 0x4C };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    [DllImport("zstd.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern UIntPtr ZSTD_compressBound(UIntPtr srcSize);

    [DllImport("zstd.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern UIntPtr ZSTD_maxCLevel();

    [DllImport("zstd.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern UIntPtr ZSTD_isError(UIntPtr code);

    [DllImport("zstd.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern UIntPtr ZSTD_compress(
        IntPtr dst, UIntPtr dstCapacity,
        IntPtr src, UIntPtr srcSize,
        int level);

    [DllImport("zstd.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern UIntPtr ZSTD_decompress(
        IntPtr dst, UIntPtr dstCapacity,
        IntPtr src, UIntPtr compressedSize);

    //

    static uint XXH32(byte[] data, int len, uint seed)
    {
        return xxHashSharp.xxHash.CalculateHash(data, len, seed);
    }

    private static byte Rotl8(byte value, int shift)
    {
        shift &= 7;
        return (byte)((value << shift) | (value >> (8 - shift)));
    }

    public static byte[] Compress(byte[] input)
    {
        var maxSize = (ulong)ZSTD_compressBound((UIntPtr)input.Length);
        var buffer = new byte[maxSize + 8];

        Buffer.BlockCopy(BYTECODE_SIGNATURE, 0, buffer, 0, 4);

        uint size = (uint)input.Length;
        Buffer.BlockCopy(BitConverter.GetBytes(size), 0, buffer, 4, 4);

        unsafe
        {
            fixed (byte* dstPtr = &buffer[8])
            fixed (byte* srcPtr = &input[0])
            {
                var compressedSize = ZSTD_compress(
                    (IntPtr)dstPtr, (UIntPtr)maxSize,
                    (IntPtr)srcPtr, (UIntPtr)input.Length,
                    (int)ZSTD_maxCLevel()
                );

                if (ZSTD_isError(compressedSize) != UIntPtr.Zero)
                    return Array.Empty<byte>();

                int finalSize = (int)compressedSize + 8;
                Array.Resize(ref buffer, finalSize);

                uint hashKey = XXH32(buffer, finalSize, BYTECODE_HASH_SEED);
                byte[] keyBytes = BitConverter.GetBytes(hashKey);

                for (uint i = 0; i < finalSize; i++)
                    buffer[i] ^= (byte)((keyBytes[i % 4] + i * BYTECODE_HASH_MULTIPLIER) & 0xFF);

                return buffer;
            }
        }
    }

    public static byte[] Decompress(byte[] input)
    {
        if (input.Length < 8)
            return Array.Empty<byte>();

        // Compute hash key used
        uint hashKey = XXH32(input, input.Length, BYTECODE_HASH_SEED);
        byte[] keyBytes = BitConverter.GetBytes(hashKey);

        byte[] data = (byte[])input.Clone();

        for (uint i = 0; i < data.Length; i++)
            data[i] ^= (byte)((keyBytes[i % 4] + i * BYTECODE_HASH_MULTIPLIER) & 0xFF);

        uint decompressedSize = BitConverter.ToUInt32(data, 4);
        var output = new byte[decompressedSize];

        unsafe
        {
            fixed (byte* dstPtr = &output[0])
            fixed (byte* srcPtr = &data[8])
            {
                var actual = ZSTD_decompress(
                    (IntPtr)dstPtr, (UIntPtr)decompressedSize,
                    (IntPtr)srcPtr, (UIntPtr)(data.Length - 8)
                );

                if (ZSTD_isError(actual) != UIntPtr.Zero || (ulong)actual != decompressedSize)
                    return Array.Empty<byte>();

                return output;
            }
        }
    }

    public static byte[] SignBytecode(byte[] bytecode)
    {
        if (bytecode == null || bytecode.Length == 0)
            return Array.Empty<byte>();

        const int FOOTER_SIZE = 40;

        using var hasher = Hasher.New();
        hasher.Update(bytecode);
        byte[] blake3Hash = hasher.Finalize().AsSpan().ToArray();

        byte[] transformedHash = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            byte b = KEY_BYTES[i & 3];
            byte hashByte = blake3Hash[i];
            byte combined = (byte)(b + i);
            int shift;

            switch (i & 3)
            {
                case 0: shift = (combined & 3) + 1; break;
                case 1: shift = (combined & 3) + 2; break;
                case 2: shift = (combined & 3) + 3; break;
                default: shift = (combined & 3) + 4; break;
            }

            transformedHash[i] = (i & 3) % 2 == 0
                ? Rotl8((byte)(hashByte ^ ~b), shift)
                : Rotl8((byte)(b ^ ~hashByte), shift);
        }

        byte[] footer = new byte[FOOTER_SIZE];
        uint firstHashDword = BitConverter.ToUInt32(transformedHash, 0);
        Buffer.BlockCopy(BitConverter.GetBytes(firstHashDword ^ MAGIC_B), 0, footer, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(firstHashDword ^ MAGIC_A), 0, footer, 4, 4);
        Buffer.BlockCopy(transformedHash, 0, footer, 8, 32);

        byte[] signedBytecode = new byte[bytecode.Length + FOOTER_SIZE];
        Buffer.BlockCopy(bytecode, 0, signedBytecode, 0, bytecode.Length);
        Buffer.BlockCopy(footer, 0, signedBytecode, bytecode.Length, FOOTER_SIZE);

        return Compress(Compress(signedBytecode)); // return as raw bytes
    }

    public static void SetBytecode(RobloxInstance script, byte[] bytecode)
    {
        if (script.ClassName != "LocalScript" && script.ClassName != "ModuleScript")
            throw new Exception("[ERROR] Unsupported script");

        ulong embeddedOffset = script.ClassName == "LocalScript" ? 0x1A8UL : 0x150UL;
        ulong embeddedPtr = Memory.ReadFrom<ulong>(script, embeddedOffset);

        ulong size = (ulong)bytecode.Length;

        ulong allocated = Memory.VirtualAlloc(size);
        if (allocated == 0)
            throw new Exception("[ERROR] Allocating failed");

        if (!Memory.Write(allocated, bytecode, size))
            throw new Exception("[ERROR] Writing failed");

        Memory.Write(embeddedPtr + 0x10, allocated);
        Memory.Write(embeddedPtr + 0x18, size);
    }
}
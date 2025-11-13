using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RMemory
{
    public class Memory
    {
        public static IntPtr Handle = IntPtr.Zero;
        public static int ProcessID = 0;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int access, bool inherit, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int nSize,
            out int lpNumberOfBytesWritten
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetModuleHandleEx(uint dwFlags, IntPtr lpAddress, out IntPtr phModule);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint flAllocationType,
            uint flProtect
        );

        public static bool Attached()
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            if (processes.Length == 0) return false;

            ProcessID = processes[0].Id;
            Handle = OpenProcess(0x1F0FFF, false, ProcessID);
            return Handle != IntPtr.Zero;
        }

        public static void Detach()
        {
            ProcessID = 0;
            Handle = IntPtr.Zero;
        }

        // ---------------- Read ----------------
        public static T Read<T>(ulong address, ulong size = 0, bool convert = true) where T : struct
        {
            int structSize = Marshal.SizeOf<T>();
            byte[] buffer = new byte[structSize];

            if (!ReadProcessMemory(Handle, (IntPtr)address, buffer, structSize, out _))
                throw new InvalidOperationException($"[ERROR] Failed to read memory at 0x{address:X}");

            if (!convert)
                return default;

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public static T ReadFrom<T>(Functions.RobloxInstance script, ulong offset) where T : struct
        {
            ulong address = (ulong)script.Self + offset;
            return Read<T>(address);
        }

        public static string ReadString(IntPtr addr, int max = 200)
        {
            if (addr == IntPtr.Zero) return "<null>";

            byte[] buffer = new byte[max];
            ReadProcessMemory(Handle, addr, buffer, max, out _);

            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = max;

            return Encoding.UTF8.GetString(buffer, 0, length).Replace("\0", "").Replace("\ufffd", "?");
        }

        public static byte[] ReadBytes(IntPtr address, int length)
        {
            byte[] buffer = new byte[length];
            ReadProcessMemory(Handle, address, buffer, length, out _);
            return buffer;
        }

        // ---------------- Write ----------------
        public static bool Write<T>(ulong address, T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, buffer, 0, size);
            Marshal.FreeHGlobal(ptr);

            return WriteProcessMemory(Handle, (IntPtr)address, buffer, size, out int written) && written == size;
        }

        public static bool WriteBytes(ulong address, byte[] bytes)
        {
            return WriteProcessMemory(Handle, (IntPtr)address, bytes, bytes.Length, out int written) && written == bytes.Length;
        }

        public static bool Write(ulong address, byte[] bytes, ulong length)
        {
            if (bytes == null) return false;
            if ((ulong)bytes.Length < length) return false;
            return WriteProcessMemory(Handle, (IntPtr)address, bytes, (int)length, out int written) && written == (int)length;
        }

        public static bool Write(IntPtr address, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return WriteProcessMemory(Handle, address, bytes, bytes.Length, out int written) && written == bytes.Length;
        }

        public static bool Write(IntPtr address, IntPtr value)
        {
            byte[] bytes = BitConverter.GetBytes(value.ToInt64());
            return WriteProcessMemory(Handle, address, bytes, bytes.Length, out int written) && written == bytes.Length;
        }

        public static bool WriteTo<T>(Functions.RobloxInstance script, ulong offset, T value) where T : struct
        {
            ulong address = (ulong)script.Self + offset;
            return Write(address, value);
        }

        public static string ReplaceString(string input, string placeholder, object value)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            return input.Replace(placeholder, value?.ToString() ?? "");
        }

        // ---------------- Other stuff ----------------
        public static IntPtr GetBaseAddress()
        {
            var proc = Process.GetProcessById(ProcessID);
            foreach (ProcessModule mod in proc.Modules)
            {
                if (mod.ModuleName.Equals("RobloxPlayerBeta.exe", StringComparison.OrdinalIgnoreCase))
                    return mod.BaseAddress;
            }
            return IntPtr.Zero;
        }

        public static IntPtr GetModule()
        {
            IntPtr hModule;
            IntPtr funcPtr = Marshal.GetFunctionPointerForDelegate((Action)(() => { }));

            if (!GetModuleHandleEx(0x00000004, funcPtr, out hModule) || hModule == IntPtr.Zero)
                throw new InvalidOperationException("[ERROR] Failed to get module handle.");

            return hModule;
        }

        public static ulong VirtualAlloc(ulong size)
        {
            var addr = VirtualAllocEx(Handle, IntPtr.Zero, (UIntPtr)size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            return (ulong)addr;
        }

        public static List<int> GetProcessIds(string name)
        {
            var pids = new List<int>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (string.Equals(proc.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase))
                        pids.Add(proc.Id);
                }
                catch { }
            }
            return pids;
        }
    }
}

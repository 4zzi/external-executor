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

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TH32CS_SNAPMODULE = 0x00000008;
        private const uint TH32CS_SNAPMODULE32 = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

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
        public static T Read<T>(IntPtr addy) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] buff = new byte[size];
            ReadProcessMemory(Handle, addy, buff, size, out nint readlen);
            GCHandle gch = GCHandle.Alloc(buff, GCHandleType.Pinned);
            T value = Marshal.PtrToStructure<T>(gch.AddrOfPinnedObject());
            return value;
        }

        public static T ReadFrom<T>(Functions.RobloxInstance script, ulong offset) where T : struct
        {
            ulong address = (ulong)script.Self + offset;
            return Read<T>((nint)address);
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
            if (Handle == IntPtr.Zero) return IntPtr.Zero;

            try
            {
                IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)ProcessID);
                if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                    return IntPtr.Zero;

                MODULEENTRY32 moduleEntry = new MODULEENTRY32();
                moduleEntry.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));

                if (Module32First(snapshot, ref moduleEntry))
                {
                    CloseHandle(snapshot);
                    return moduleEntry.modBaseAddr;
                }

                CloseHandle(snapshot);
            }
            catch { }

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

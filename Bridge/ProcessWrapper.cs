using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Bridge
{
    public class ProcessWrapper
    {
        public int Id { get; }
        public IntPtr Handle { get; }
        public IntPtr BaseAddress { get; }

        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        public ProcessWrapper(int pid)
        {
            Id = pid;
            Handle = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to open process {pid}");

            var proc = Process.GetProcessById(pid);
            BaseAddress = proc.MainModule.BaseAddress;
        }
    }
}
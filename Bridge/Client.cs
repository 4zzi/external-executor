//
    using System;
    using System.Text;
    using System.Threading;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Newtonsoft.Json.Linq;
//

namespace Bridge
{
    public class Client
    {
        private readonly ProcessWrapper _process;
        public readonly Websocket _server;
        private readonly TeleportHandler _teleportHandler;

        public Client(int pid, Websocket server)
        {
            _process = new ProcessWrapper(pid);
            _server = server;
            _teleportHandler = new TeleportHandler(_process);
        }

        public ProcessWrapper GetProcess() => _process;

        #region P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern ushort MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        static extern IntPtr FindResource(IntPtr hModule, int lpName, int lpType);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll")]
        static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        #endregion
    }
}

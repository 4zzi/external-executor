//
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics;
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.IO;
    using System.Reflection;
    using System.Reflection.Metadata;
    using Newtonsoft.Json.Linq;
//
//
    using WindowsInput;
    using RMemory;
    using Offsets;
    using Functions;
    using Client;
    using Luau;
    using Bridge;
//

namespace Bridge
{
    public static class BridgeHost
    {
        public static Websocket Server;
    }
}

namespace Main
{
    using Bridge;
    public class Roblox
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        public static IntPtr GamePointer = Memory.Read<IntPtr>(Memory.GetBaseAddress() + Offsets.FakeDataModel.Pointer);
        public static RobloxInstance Game = new RobloxInstance(Memory.Read<IntPtr>(GamePointer + 0x1C0));

        public Websocket _server;

        public void Inject()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (Game == null || Game.Self == IntPtr.Zero)
                        throw new Exception("Failed to get Base Address");
                       
                    // to do list: getservice instead of findfirstchild cus they change the services name

                    var VirtualInput = new InputSimulator();
                    var manager = Game.FindFirstChildFromPath("CoreGui.RobloxGui.Modules.PlayerList.PlayerListManager");

                    if (manager == null || manager.Self == IntPtr.Zero)
                        throw new Exception("Injection failed");

                    var spoof = Game.FindFirstChildFromPath("StarterPlayer.StarterPlayerScripts.PlayerModule.ControlModule.VRNavigation");
                    if (spoof == null || spoof.Self == IntPtr.Zero)
                        throw new Exception("Injection failed");

                    string initscript = Executor.GetInitScript();
                    byte[] bytecode = Executor.Compile(initscript);

                    spoof.UnlockModule();
                    Bytecodes.SetBytecode(spoof, bytecode);

                    manager.SpoofWith(spoof.Self);
                    Memory.Write((ulong)Memory.GetBaseAddress().ToInt64() + (ulong)Offsets.FFlags.WebSocketServiceEnableClientCreation, 1);

                    try
                    {
                        Process robloxProcess = Process.GetProcessById(Memory.ProcessID);
                        if (robloxProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            SetForegroundWindow(robloxProcess.MainWindowHandle);
                            Thread.Sleep(200);
                        }
                    }
                    catch { }

                    VirtualInput.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);
                    Thread.Sleep(10);
                    VirtualInput.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);

                    manager.SpoofWith(manager.Self);
                    File.Delete(initscript);
                }
                else
                {
                    throw new Exception("[*] Only supported on windows.");
                }
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[ERROR] Injection failed: {ex.Message}");
                REPL.REPLPrint($"[ERROR] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    REPL.REPLPrint($"[ERROR] Inner exception: {ex.InnerException.Message}");
                throw;
            }
        }
        public async Task Execute(string source)
        {
            try
            {
                int pid = Memory.ProcessID;
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(source));

                var execute = new JObject
                {
                    ["action"] = "Execute",
                    ["pid"] = pid,
                    ["source"] = base64
                };

                var response = await BridgeHost.Server.SendAndReceive(execute, pid, 5000);
                
                if (response["success"]?.Value<bool>() != true)
                {
                    throw new Exception(response["message"]?.ToString() ?? "Execution failed");
                }
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[ERROR] Execution failed: {ex.Message}");
                throw;
            }
        }
    }
    public class Program
    {
        public static async Task MonitorProcessAndExit()
        {
            Process robloxProcess = Process.GetProcessById(Memory.ProcessID);
            while (!robloxProcess.HasExited)
            {
                await Task.Delay(1000);
            }
            Environment.Exit(0);
        }
        public static async Task Main(string[] args) 
        {
            try
            {
                Console.Clear();

                BridgeHost.Server = new Websocket("127.0.0.1", 6969);
                if (!BridgeHost.Server.Run())
                {
                    REPL.REPLPrint("[FATAL] Failed to start websocket server.");
                    return;
                }

                Console.Clear();

                int attempts = 0;
                while (!Memory.Attached())
                {
                    Thread.Sleep(100);
                    attempts++;
                    if (attempts % 10 == 0) Console.Write(".");

                    if (attempts > 300)
                    {
                        REPL.REPLPrint("\n[ERROR] Could not find Roblox process after 30 seconds.");
                        REPL.REPLPrint("[*] Please start Roblox and restart the application.");
                        return;
                    }
                }

                Console.Title = "Oracle.exe";
                Console.Clear();

                var Roblox = new Roblox();
                Client injectedClient = null;

                try
                {
                    Roblox.Inject();

                    injectedClient = new Client(Memory.ProcessID, BridgeHost.Server);
                    BridgeHost.Server.AddClient(injectedClient);

                    REPL.REPLPrint("[*] Please keep this console open unless you closed the gui.\n");

                    IMGui Executor = new IMGui(Roblox);

                    BridgeHost.Server.OnInitialized += (six_seven) => // idk why it only works with a dummy argument
                    {
                        Executor.Start(); // only start im gui when bridge.lua actually works
                    };

                    await MonitorProcessAndExit();
                }
                catch (Exception ex)
                {
                    REPL.REPLPrint("[ERROR] Injection failed: " + ex.Message);
                    REPL.REPLPrint("[ERROR] Stack trace: " + ex.StackTrace);
                    return;
                }
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[FATAL ERROR] {ex.Message}");
                REPL.REPLPrint($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
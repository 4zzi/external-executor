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
using WindowsInput;
using RMemory;
using Offsets;
using Functions;
using Client;
using Luau;
using Bridge;
using Newtonsoft.Json.Linq;

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

        public static RobloxInstance Game = new RobloxInstance(
            Memory.Read<IntPtr>(
                (ulong)Memory.Read<IntPtr>(
                    (ulong)Memory.GetBaseAddress() + (ulong)Offsets.FakeDataModel.Pointer
                )
                + (ulong)0x1C0
            )
        );
        public Websocket _server;

        public void Execute(string source)
        {
            JObject request = new JObject
            {
                ["action"] = "execute",
                ["source"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(source))
            };

            if (BridgeHost.Server == null)
                throw new InvalidOperationException("[ERROR] Websocket server not initialized.");

            JObject response = BridgeHost.Server.SendAndReceive(request, Memory.ProcessID).Result;

            if (response["*"]?.Value<bool>() != true)
                throw new Exception(response["message"]?.ToString());
        }

        public void Inject()
        {
            try
            {
                var VirtualInput = new InputSimulator();
                var manager = Game.FindFirstChildFromPath("CoreGui.RobloxGui.Modules.PlayerList.PlayerListManager");

                if (manager == null || manager.Self == IntPtr.Zero)
                {
                    throw new Exception("Failed to find PlayerListManager");
                }
                
                var spoof = Game.FindFirstChildFromPath("StarterPlayer.StarterPlayerScripts.PlayerModule.ControlModule.VRNavigation");
                if (spoof == null || spoof.Self == IntPtr.Zero)
                {
                    throw new Exception("Failed to find VRNavigation module");
                }

                string initscript = Executor.GetInitScript();
                byte[] bytecode = Executor.Compile(initscript);
                
                try
                {
                    spoof.UnlockModule();
                    Bytecodes.SetBytecode(spoof, bytecode);

                    manager.SpoofWith(spoof.Self);
                    Memory.Write((ulong)Memory.GetBaseAddress().ToInt64() + (ulong)Offsets.FFlags.WebSocketServiceEnableClientCreation, 1);

                    REPL.REPLPrint("[*] Enabled WebSocket");

                    try
                    {
                        Process robloxProcess = Process.GetProcessById(Memory.ProcessID);
                        if (robloxProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            SetForegroundWindow(robloxProcess.MainWindowHandle);
                            Thread.Sleep(200);
                        }
                    }
                    catch (Exception focusEx)
                    {
                        REPL.REPLPrint($"[WARN] Could not focus window: {focusEx.Message}");
                    }

                    VirtualInput.Keyboard.KeyDown(VirtualKeyCode.ESCAPE);
                    Thread.Sleep(50);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.ESCAPE);

                    Thread.Sleep(100);

                    VirtualInput.Keyboard.KeyDown(VirtualKeyCode.ESCAPE);
                    Thread.Sleep(50);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.ESCAPE);

                    Thread.Sleep(100);

                    VirtualInput.Keyboard.KeyDown(VirtualKeyCode.F9);
                    Thread.Sleep(50);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.F9);

                    manager.SpoofWith(manager.Self);
                    File.Delete(initscript);
                }
                finally
                {
                }
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[ERROR] Injection failed: {ex.Message}");
                REPL.REPLPrint($"[ERROR] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    REPL.REPLPrint($"[ERROR] Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }
    };

    public class Program
    {
        public static void Main(string[] args)
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
                
                int attempts = 0;
                while (!Memory.Attached())
                {
                    Thread.Sleep(100);
                    attempts++;

                    if (attempts % 10 == 0)
                    {
                        Console.Write(".");
                    }

                    if (attempts > 300)
                    {
                        REPL.REPLPrint("\n[ERROR] Could not find Roblox process after 30 seconds.");
                        REPL.REPLPrint("[*] Please start Roblox and restart the application.");
                        REPL.REPLPrint("\nPress any key to exit...");
                        Console.ReadKey();
                        return;
                    }
                }

                Console.Title = "Labubu Executor.exe (64 bit)";

                REPL.REPLPrint("[*] Attached to process (PID: " + Memory.ProcessID + ")");
                Thread.Sleep(100);

                REPL.REPLPrint("[*] Type 'inject' to inject, or enter lua codes to execute.");
                REPL.REPLPrint("[*] Type 'exit' to quit.\n");

                var Roblox = new Roblox();
                Client injectedClient = null;

                while (true)
                {
                    Console.Write(">> ");

                    string input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input)) continue;
                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                    string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts.Length > 0 ? parts[0] : "";
                    string code = parts.Length > 1 ? parts[1] : input;

                    if (command.Equals("inject", StringComparison.OrdinalIgnoreCase))
                    {
                        var pid = Memory.ProcessID;

                        if (injectedClient == null)
                        {
                            try
                            {
                                REPL.REPLPrint("[*] Running Process " + pid + "...");
                                Roblox.Inject();
                                
                                Thread.Sleep(500);
                                
                                try
                                {
                                    injectedClient = new Client(pid, BridgeHost.Server);
                                    BridgeHost.Server.AddClient(injectedClient);
                                    
                                    Thread.Sleep(200);
                                    
                                    var clientCount = BridgeHost.Server.GetClients().Count();
                                    REPL.REPLPrint($"[*] Injected onto {clientCount} client\n");
                                }
                                catch (Exception addEx)
                                {
                                    REPL.REPLPrint($"[ERROR] Failed to add client: {addEx.Message}\n");
                                    injectedClient = null;
                                    throw;
                                }
                            }
                            catch (Exception ex)
                            {
                                REPL.REPLPrint("[ERROR] Failed to initialize client " + pid);
                                REPL.REPLPrint("[ERROR] " + ex.Message);
                                REPL.REPLPrint("[ERROR] Stack trace: " + ex.StackTrace);
                                REPL.REPLPrint("\nPress any key to continue...");
                                Console.ReadKey();
                                Console.WriteLine();
                            }
                        }
                        else
                        {
                            REPL.REPLPrint($"[*] Already attached to {pid}\n");
                        }
                    }
                    else
                    {
                        if (injectedClient == null)
                        {
                            REPL.REPLPrint("[ERROR] No client attached. Please run 'inject' first.\n");
                            continue;
                        }

                        try
                        {
                            Roblox.Execute(code);
                            REPL.REPLPrint("[*] Executed.\n");
                        }
                        catch (Exception ex)
                        {
                            REPL.REPLPrint("[ERROR] " + ex.Message + "\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[FATAL ERROR] {ex.Message}");
                REPL.REPLPrint($"Stack trace: {ex.StackTrace}");
                REPL.REPLPrint("\nPress any key to exit...");
                Console.ReadKey();
            }
            
            REPL.REPLPrint("[*] Exiting... \n");
        }
    }
}
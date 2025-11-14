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
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        public static RobloxInstance Game = new RobloxInstance(
            Memory.Read<IntPtr>(
                (ulong)Memory.Read<IntPtr>(
                    (ulong)Memory.GetBaseAddress() + (ulong)Offsets.FakeDataModel.Pointer
                )
                + (ulong)0x1C0
            )
        );
        public Websocket _server;

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
                    Thread.Sleep(1);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.ESCAPE);

                    Thread.Sleep(1);

                    VirtualInput.Keyboard.KeyDown(VirtualKeyCode.ESCAPE);
                    Thread.Sleep(1);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.ESCAPE);

                    Thread.Sleep(1);

                    VirtualInput.Keyboard.KeyDown(VirtualKeyCode.F9);
                    Thread.Sleep(1);
                    VirtualInput.Keyboard.KeyUp(VirtualKeyCode.F9);

                    manager.SpoofWith(manager.Self);
                    File.Delete(initscript);

                    IntPtr consoleWindow = GetConsoleWindow();
                    if (consoleWindow != IntPtr.Zero)
                    {
                        SetForegroundWindow(consoleWindow);
                    }
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
        public void Execute(string source)
        {
            try
            {
                string folderPath = Path.Combine(Path.GetTempPath(), "Oracle Executor");
                Directory.CreateDirectory(folderPath);

                string timestamp = DateTime.Now.ToString("hh-mmtt_MM-dd-yyyy").ToLower(); // 02-33pm_11-14-2025
                string raw = Path.Combine(folderPath, timestamp + ".lua");

                File.WriteAllText(raw, $"script.Parent = nil \n\nlocal PROCESS_ID = {Memory.ProcessID} \n local VERSION = '0.1'\n\n" + source + "\n\nwhile true do\n task.wait(9e9)\nend");
                
                var execute = new JObject
                {
                    ["action"] = "Execute",
                    ["pid"] = Memory.ProcessID,
                    ["source"] = source
                };

                BridgeHost.Server.Send(execute, Memory.ProcessID);
            }
            catch (Exception ex)
            {
                REPL.REPLPrint($"[ERROR] Execution failed: {ex.Message}");
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
        private static ManualResetEventSlim initializeReceived = new ManualResetEventSlim(false);
        private static ManualResetEventSlim executeReceived = new ManualResetEventSlim(false);

        public static void Main(string[] args)
        {
            try
            {
                Console.Clear();

                BridgeHost.Server = new Websocket("127.0.0.1", 6969);

                BridgeHost.Server.OnInitialized += (pid) =>
                {
                    initializeReceived.Set();
                };

                BridgeHost.Server.AddListener("execute", (behavior, data) =>
                {
                    string status = data.ContainsKey("status") ? data["status"].ToString() : "";
                    
                    if (status == "ready")
                    {
                        executeReceived.Set();
                    }
                });

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

                Console.Title = "Oracle.exe";
                Thread.Sleep(100);

                REPL.REPLPrint("[*] Type 'inject' to inject");
                REPL.REPLPrint("[*] Press enter on empty line to execute");
                REPL.REPLPrint("[*] Type 'exit' to quit.\n");

                var Roblox = new Roblox();
                Client injectedClient = null;

                while (true)
                {
                    Console.Write("> ");

                    string input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input)) continue;
                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                    string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts.Length > 0 ? parts[0] : "";

                    if (command.Equals("inject", StringComparison.OrdinalIgnoreCase))
                    {
                        var pid = Memory.ProcessID;

                        if (injectedClient == null)
                        {
                            try
                            {
                                REPL.REPLPrint("[*] Running Process " + pid + "...");
                                
                                initializeReceived.Reset();
                                Roblox.Inject();

                                if (initializeReceived.Wait(TimeSpan.FromSeconds(10)))
                                {
                                    injectedClient = new Client(pid, BridgeHost.Server);
                                    BridgeHost.Server.AddClient(injectedClient);

                                    var clientCount = BridgeHost.Server.GetClients().Count();
                                    REPL.REPLPrint($"[*] Injected\n");
                                }
                                else
                                {
                                    REPL.REPLPrint("[ERROR] Injection timeout\n");
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
                            REPL.REPLPrint($"[*] Already injected to {pid}\n");
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
                            StringBuilder codeBuilder = new StringBuilder();
                            codeBuilder.AppendLine(input);

                            int lineNumber = 2;
                            while (true)
                            {
                                Console.Write(new string('>', lineNumber) + " ");
                                string line = Console.ReadLine();
                                
                                if (string.IsNullOrEmpty(line))
                                    break;
                                
                                codeBuilder.AppendLine(line);
                                lineNumber++;
                            }

                            string source = codeBuilder.ToString().Trim();
                            Roblox.Execute(source);
                            REPL.REPLPrint("\n");
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
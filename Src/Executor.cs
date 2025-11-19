//
    using System;
    using System.Reflection;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Text;
    using System.Globalization;
    using System.Diagnostics;
//

//
    using Luau;
    using Functions;
    using RMemory;
//

namespace Client
{
    public class Executor
    {
        public static string Version = "0.1";

        public static string GetInitScript()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            string initscript = Path.Combine(root, "Lua", "Bridge.lua");

            if (!File.Exists(initscript))
                throw new FileNotFoundException($"Injector not found at: {initscript}");

            string scriptContent = File.ReadAllText(initscript);
            scriptContent = Memory.ReplaceString(scriptContent, "%PROCESS_ID%", Memory.ProcessID);

            // write to temporary file, not original
            string tempPath = Path.Combine(root, "Bridge", "Bridge.lua");
            File.WriteAllText(tempPath, "script.Parent = nil \ntask.spawn(function() \n" + scriptContent + "\nend)\n\nwhile true do \n   task.wait(9e9) \nend");

            return tempPath;
        }

        public static byte[] Compile(string scriptFullPath)
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            string compilerPath = Path.Combine(root, "Lua", "luaucompiler.exe");

            if (!File.Exists(compilerPath))
                throw new FileNotFoundException("luaucompiler.exe not found");

            if (!File.Exists(scriptFullPath))
                throw new FileNotFoundException("script missing");

            var psi = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"\"{scriptFullPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(scriptFullPath) ?? Path.GetDirectoryName(compilerPath) ?? root
            };

            using var p = Process.Start(psi);
            if (p == null)
                throw new Exception("compiler failed");

            string err = p.StandardError.ReadToEnd();
            string stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception(err);

            string compiledPath = null;
            string[] searchPaths = {
                Path.Combine(Path.GetDirectoryName(compilerPath) ?? root, "Compiled.txt"),
                Path.Combine(Path.GetDirectoryName(scriptFullPath) ?? root, "Compiled.txt"),
                Path.Combine(root, "Compiled.txt"),
                Path.Combine(AppContext.BaseDirectory, "Compiled.txt"),
                Path.Combine(Directory.GetCurrentDirectory(), "Compiled.txt")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    compiledPath = path;
                    break;
                }
            }

            if (compiledPath == null)
            {
                try
                {
                    var candidates = new List<string>();
                    if (Directory.Exists(root))
                        candidates.AddRange(Directory.GetFiles(root, "Compiled.txt", SearchOption.AllDirectories));

                    var compDir = Path.GetDirectoryName(compilerPath) ?? root;
                    if (Directory.Exists(compDir))
                        candidates.AddRange(Directory.GetFiles(compDir, "Compiled.txt", SearchOption.AllDirectories));

                    var baseDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
                    if (Directory.Exists(baseDir))
                        candidates.AddRange(Directory.GetFiles(baseDir, "Compiled.txt", SearchOption.AllDirectories));

                    compiledPath = candidates
                        .Where(File.Exists)
                        .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                        .FirstOrDefault();
                }
                catch { compiledPath = null; }
            }

            if (compiledPath == null)
            {
                var msg = new StringBuilder();
                msg.AppendLine("Compiled.txt not found (searched common locations)");
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    msg.AppendLine("--- compiler error ---");
                    msg.AppendLine(stdout);
                }
                if (!string.IsNullOrWhiteSpace(err))
                {
                    msg.AppendLine("--- compiler error ---");
                    msg.AppendLine(err);
                }

                throw new FileNotFoundException(msg.ToString());
            }

            byte[] raw = File.ReadAllBytes(compiledPath);
            try { File.Delete(compiledPath); } catch { }
            return raw;
        }
    }
}
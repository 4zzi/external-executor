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
            string initscript = Path.Combine(root, "Roblox", "DummyScript.lua");

            if (!File.Exists(initscript))
                throw new FileNotFoundException($"DummyScript.lua not found at: {initscript}");

            string scriptContent = File.ReadAllText(initscript);
            scriptContent = Memory.ReplaceString(scriptContent, "%PROCESS_ID%", Memory.ProcessID);

            // write to temporary file, not original
            string tempPath = Path.Combine(root, "Roblox", "InitScript.lua");
            File.WriteAllText(tempPath, scriptContent);

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
                WorkingDirectory = Path.GetDirectoryName(compilerPath) ?? root
            };

            using var p = Process.Start(psi);
            if (p == null)
                throw new Exception("compiler failed");

            string err = p.StandardError.ReadToEnd();
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
                throw new FileNotFoundException("Compiled.txt not found");

            byte[] raw = File.ReadAllBytes(compiledPath);
            File.Delete(compiledPath);

            // Console.WriteLine("[*] " + BitConverter.ToString(raw.Take(67).ToArray()).Replace("-", " "));
            return raw;
        }
    }
}
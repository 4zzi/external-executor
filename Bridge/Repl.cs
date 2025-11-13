using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Bridge
{
    public class REPL
    {
        private Websocket _server;

        public REPL(Websocket server)
        {
            _server = server;
        }

        public static void REPLPrint(string message)
        {
            Console.WriteLine(message);
        }

        private List<int> GetProcessIds(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            var pids = new List<int>();
            foreach (var p in processes)
                pids.Add(p.Id);
            return pids;
        }
    }
}

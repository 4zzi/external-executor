using System;
using System.Threading;
using System.Threading.Tasks;
using RMemory;
using Offsets;

namespace Bridge
{
    public class TeleportHandler
    {
        private readonly ProcessWrapper _process;
        private Action _listener;
        private int _epoch;
        private bool _isGameReady;
        private readonly object _mutex = new object();
        private bool _running;

        public TeleportHandler(ProcessWrapper process)
        {
            _process = process;
        }

        public void SetListener(Action listener)
        {
            lock (_mutex)
            {
                _listener = listener;
            }
        }

        public int GetEpoch()
        {
            return _epoch;
        }
        public void Stop()
        {
            _running = false;
        }
    }
}

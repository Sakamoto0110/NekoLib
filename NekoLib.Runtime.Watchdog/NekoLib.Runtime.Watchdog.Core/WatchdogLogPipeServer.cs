    using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace NekoLib.Runtime.Watchdog
{
    public sealed class WatchdogLogPipeServer : IDisposable
    {
        private sealed class Client
        {
            public NamedPipeServerStream Pipe;
            public StreamWriter Writer;
        }

        private readonly string _pipeName;
        private readonly object _lock = new object();
        private readonly List<Client> _clients = new List<Client>();

        private readonly BlockingCollection<string> _queue =
            new BlockingCollection<string>(2048);

        private volatile bool _exiting;
        private Thread _acceptThread;
        private Thread _dispatchThread;

        public WatchdogLogPipeServer(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException(nameof(pipeName));

            _pipeName = pipeName.Trim();
        }

        public void Start()
        {
            if (_acceptThread != null) return;

            _acceptThread = new Thread(AcceptLoop)
            { IsBackground = true, Name = "WDG-LogPipe-Accept" };

            _dispatchThread = new Thread(DispatchLoop)
            { IsBackground = true, Name = "WDG-LogPipe-Dispatch" };

            _acceptThread.Start();
            _dispatchThread.Start();
        }

        public void Enqueue(string line)
        {
            if (_exiting || string.IsNullOrEmpty(line))
                return;

            // never block watchdog: drop if full
            try { _queue.TryAdd(line); } catch { }
        }

        private void AcceptLoop()
        {
            while (!_exiting)
            {
                NamedPipeServerStream pipe = null;

                try
                {
                    pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    pipe.WaitForConnection();

                    var writer = new StreamWriter(pipe, new UTF8Encoding(false))
                    { AutoFlush = true };

                    lock (_lock)
                        _clients.Add(new Client { Pipe = pipe, Writer = writer });

                    pipe = null; // ownership transferred
                }
                catch
                {
                    try { pipe?.Dispose(); } catch { }
                    Thread.Sleep(100);
                }
            }
        }

        private void DispatchLoop()
        {
            while (!_exiting)
            {
                string line;

                try { line = _queue.Take(); }
                catch
                {
                    if (_exiting) break;
                    Thread.Sleep(25);
                    continue;
                }

                List<Client> snapshot;
                lock (_lock)
                    snapshot = new List<Client>(_clients);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    var c = snapshot[i];
                    try
                    {
                        if (c?.Pipe == null || !c.Pipe.IsConnected)
                            throw new IOException("disconnected");

                        c.Writer.WriteLine(line);
                    }
                    catch
                    {
                        lock (_lock)
                            _clients.Remove(c);

                        try { c.Writer?.Dispose(); } catch { }
                        try { c.Pipe?.Dispose(); } catch { }
                    }
                }
            }
        }

        public void Dispose()
        {
            _exiting = true;

            try { _queue.CompleteAdding(); } catch { }

            try { _acceptThread?.Join(800); } catch { }
            try { _dispatchThread?.Join(800); } catch { }

            lock (_lock)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    var c = _clients[i];
                    try { c.Writer?.Dispose(); } catch { }
                    try { c.Pipe?.Dispose(); } catch { }
                }
                _clients.Clear();
            }

            try { _queue.Dispose(); } catch { }
        }
    }
}

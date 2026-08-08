using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace NekoLib.Watchdog
{
    [Obsolete("Use WatchdogRuntime's PipeServer event stream and WatchdogController.SubscribeLogs instead.")]
    public sealed class WatchdogLogPipeServer : IDisposable
    {
        private sealed class Client
        {
            public NamedPipeServerStream? Pipe;
            public StreamWriter? Writer;
        }

        private readonly string _pipeName;
        private readonly object _lock = new object();
        private readonly List<Client> _clients = new List<Client>();

        private readonly BlockingCollection<string> _queue =
            new BlockingCollection<string>(2048);

        private volatile bool _exiting;
        private Thread? _acceptThread;
        private Thread? _dispatchThread;
        private NamedPipeServerStream? _pendingAccept;
        private int _disposeStarted;

        internal bool IsAcceptThreadAlive => _acceptThread?.IsAlive == true;
        internal bool IsDispatchThreadAlive => _dispatchThread?.IsAlive == true;
        internal int ConnectedClientCount
        {
            get
            {
                lock (_lock)
                    return _clients.Count;
            }
        }
        internal bool HasPendingAccept
        {
            get
            {
                lock (_lock)
                    return _pendingAccept != null;
            }
        }

        public WatchdogLogPipeServer(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException(nameof(pipeName));

            _pipeName = pipeName.Trim();
        }

        public void Start()
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                throw new ObjectDisposedException(nameof(WatchdogLogPipeServer));
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
                NamedPipeServerStream? pipe = null;

                try
                {
                    pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    lock (_lock)
                    {
                        if (_exiting)
                            throw new ObjectDisposedException(nameof(WatchdogLogPipeServer));
                        _pendingAccept = pipe;
                    }

                    pipe.WaitForConnection();

                    var writer = new StreamWriter(pipe, new UTF8Encoding(false))
                    { AutoFlush = true };

                    lock (_lock)
                    {
                        if (ReferenceEquals(_pendingAccept, pipe))
                            _pendingAccept = null;

                        if (_exiting)
                            throw new ObjectDisposedException(nameof(WatchdogLogPipeServer));

                        _clients.Add(new Client { Pipe = pipe, Writer = writer });
                    }

                    pipe = null; // ownership transferred
                }
                catch
                {
                    lock (_lock)
                    {
                        if (ReferenceEquals(_pendingAccept, pipe))
                            _pendingAccept = null;
                    }
                    try { pipe?.Dispose(); } catch { }
                    if (!_exiting)
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
                        if (c?.Pipe == null || c.Writer == null || !c.Pipe.IsConnected)
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
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return;

            _exiting = true;

            try { _queue.CompleteAdding(); } catch { }

            NamedPipeServerStream? pendingAccept;
            List<Client> clients;
            lock (_lock)
            {
                pendingAccept = _pendingAccept;
                _pendingAccept = null;
                clients = new List<Client>(_clients);
                _clients.Clear();
            }

            try { pendingAccept?.Dispose(); } catch { }
            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                try { client.Writer?.Dispose(); } catch { }
                try { client.Pipe?.Dispose(); } catch { }
            }

            try { _acceptThread?.Join(2000); } catch { }
            try { _dispatchThread?.Join(2000); } catch { }

            try { _queue.Dispose(); } catch { }
        }
    }
}

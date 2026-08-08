using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    internal sealed class PipeOperationRegistry
    {
        internal sealed class Operation
        {
            private readonly object _gate = new object();
            private NamedPipeServerStream? _pipe;
            private bool _stopRequested;

            public bool SetPipe(NamedPipeServerStream pipe)
            {
                lock (_gate)
                {
                    if (_stopRequested)
                    {
                        try { pipe.Dispose(); } catch { }
                        return false;
                    }

                    _pipe = pipe;
                    return true;
                }
            }

            public void StopTransport()
            {
                NamedPipeServerStream? pipe;
                lock (_gate)
                {
                    _stopRequested = true;
                    pipe = _pipe;
                    _pipe = null;
                }

                try { pipe?.Dispose(); } catch { }
            }
        }

        private sealed class Entry
        {
            public Operation Operation { get; } = new Operation();
        }

        private readonly object _gate = new object();
        private readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();
        private readonly TaskCompletionSource<bool> _drained
            = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _nextId;
        private bool _stopping;

        public int Count
        {
            get
            {
                lock (_gate)
                    return _entries.Count;
            }
        }

        public Task Completion => _drained.Task;

        public bool TryStart(Func<Operation, Task> body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            Entry entry;
            long id;
            lock (_gate)
            {
                if (_stopping)
                    return false;

                id = ++_nextId;
                entry = new Entry();
                _entries.Add(id, entry);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await body(entry.Operation).ConfigureAwait(false);
                }
                finally
                {
                    entry.Operation.StopTransport();
                    Complete(id);
                }
            });

            return true;
        }

        public void BeginStop()
        {
            Entry[] entries;
            lock (_gate)
            {
                if (_stopping)
                    return;

                _stopping = true;
                entries = _entries.Values.ToArray();
                if (entries.Length == 0)
                    _drained.TrySetResult(true);
            }

            foreach (var entry in entries)
                entry.Operation.StopTransport();
        }

        private void Complete(long id)
        {
            lock (_gate)
            {
                _entries.Remove(id);
                if (_stopping && _entries.Count == 0)
                    _drained.TrySetResult(true);
            }
        }
    }
}

using NekoLib.Diagnostics.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Runtime.Watchdog
{
    public sealed class WatchdogPipeLogSink : ILogSink, IDisposable
    {
        private readonly string _pipeName;
        private NamedPipeClientStream _client;
        private StreamWriter _writer;

        public WatchdogPipeLogSink(string pipeName = "NekoLib.Watchdog.logs")
        {
            _pipeName = pipeName;
            TryConnect();
        }

        private void TryConnect()
        {
            try
            {
                _client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                _client.Connect(200); // short timeout
                _writer = new StreamWriter(_client) { AutoFlush = true };
            }
            catch
            {
                DisposeInternal();
            }
        }

        public void Write(LogEntry entry)
        {
            try
            {
                if (_writer == null)
                    TryConnect();

                _writer?.WriteLine(entry.ToString());
            }
            catch
            {
                DisposeInternal();
            }
        }

        private void DisposeInternal()
        {
            try { _writer?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }

            _writer = null;
            _client = null;
        }

        public void Dispose() => DisposeInternal();
    }


}

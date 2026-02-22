using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    public sealed class PipeEventClient : IDisposable
#if NET9
    , IAsyncDisposable
#endif
    {
        private readonly string _pipeName;
        private NamedPipeClientStream? _pipe;
        private CancellationTokenSource? _cts;
        private volatile bool _running;

        public event Action<PipeMessage>? OnEvent;

        public PipeEventClient(string basePipeName)
        {
            _pipeName = basePipeName + ".events";
        }

        public void Start()
        {
            if (_running)
                throw new InvalidOperationException("Already started.");

            _running = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => ListenLoop(_cts.Token));
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            try
            {
                _pipe = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);

#if NET9
            await _pipe.ConnectAsync(ct).ConfigureAwait(false);
#else
                await Task.Run(() => _pipe.Connect()).ConfigureAwait(false);
#endif

                while (_running && _pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    PipeMessage msg;

#if NET9
                msg = await PipeFraming.ReadAsync(_pipe, ct).ConfigureAwait(false);
#else
                    msg = await PipeFraming.ReadAsync(_pipe).ConfigureAwait(false);
#endif

                    if (msg.Type == "evt")
                        OnEvent?.Invoke(msg);
                }
            }
            catch
            {
                // connection dropped or shutdown
            }
        }

        public void Dispose()
        {
            if (!_running)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }

            _cts?.Dispose();
        }

#if NET9
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
#endif
    }


}

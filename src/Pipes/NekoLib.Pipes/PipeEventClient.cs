using System;
using System.IO.Pipes;
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
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private volatile bool _running;
        private volatile NamedPipeClientStream? _pipe;

        /// <summary>Raised for each event frame received from the hub.</summary>
        public event Action<PipeMessage>? OnEvent;

        /// <summary>Raised after a (re)connection to the event hub is established.</summary>
        public event Action? OnConnected;

        /// <summary>Raised when the current connection ends (drop or shutdown).</summary>
        public event Action? OnDisconnected;

        /// <summary>When true (default) the client keeps reconnecting after a drop until disposed.</summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>Delay between reconnect attempts.</summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>Per-attempt connection timeout (so a missing hub doesn't block forever).</summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

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

            _loopTask = Task.Run(() => RunLoop(_cts.Token));
        }

        private async Task RunLoop(CancellationToken ct)
        {
            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndListen(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break; // disposed / shutting down
                }
                catch
                {
                    // connection failed or dropped — fall through to the reconnect gate
                }

                if (!_running || ct.IsCancellationRequested || !AutoReconnect)
                    break;

                try { await Task.Delay(ReconnectDelay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ConnectAndListen(CancellationToken ct)
        {
            var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.In,
                PipeOptions.Asynchronous);

            _pipe = pipe;

            try
            {
#if NET9
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connectCts.CancelAfter(ConnectTimeout);
                    await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
                }
#else
                await Task.Run(() => pipe.Connect((int)ConnectTimeout.TotalMilliseconds), ct).ConfigureAwait(false);
#endif

                Raise(OnConnected);

                while (_running && pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    var msg = await PipeFraming.TryReadAsync(pipe, ct).ConfigureAwait(false);
                    if (msg == null)
                        break;

                    if (msg.Type == "evt")
                        Dispatch(msg);
                }
            }
            finally
            {
                try { pipe.Dispose(); } catch { }
                Raise(OnDisconnected);
            }
        }

        // A throwing subscriber must not kill the listen loop or block the others.
        private void Dispatch(PipeMessage msg)
        {
            var handler = OnEvent;
            if (handler == null)
                return;

            foreach (var d in handler.GetInvocationList())
            {
                try { ((Action<PipeMessage>)d)(msg); }
                catch { /* isolate handler faults */ }
            }
        }

        private static void Raise(Action? evt)
        {
            try { evt?.Invoke(); } catch { /* notification callbacks must not break the loop */ }
        }

        public void Dispose()
        {
            if (!_running)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }   // unblock an in-flight connect/read
            try { _loopTask?.Wait(2000); } catch { }

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

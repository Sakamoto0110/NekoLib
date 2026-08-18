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
        private const int Created = 0;
        private const int Running = 1;
        private const int Shutdown = 2;

        private static readonly AsyncLocal<PipeEventClient?> CallbackOwner
            = new AsyncLocal<PipeEventClient?>();

        private readonly object _lifecycleGate = new object();
        private readonly string _pipeName;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private NamedPipeClientStream? _pipe;
        private int _lifecycleState;
        private int _autoReconnect = 1;
        private long _reconnectDelayTicks = TimeSpan.FromMilliseconds(500).Ticks;
        private long _connectTimeoutTicks = TimeSpan.FromSeconds(5).Ticks;
        private Task _shutdownCompletion = Task.CompletedTask;

        /// <summary>Raised for each event frame received from the hub.</summary>
        public event Action<PipeMessage>? OnEvent;

        /// <summary>Raised after a connection to the event hub is established.</summary>
        public event Action? OnConnected;

        /// <summary>Raised after an established connection ends.</summary>
        public event Action? OnDisconnected;

        /// <summary>
        /// Raised for connection, framing, parsing, or listen failures. Subscriber
        /// exceptions are isolated and never terminate the listen loop.
        /// </summary>
        public event Action<Exception>? OnError;

        /// <summary>
        /// When true (default), the client reconnects after an attempt or connection
        /// ends until terminal shutdown begins.
        /// </summary>
        public bool AutoReconnect
        {
            get => Volatile.Read(ref _autoReconnect) != 0;
            set => Volatile.Write(ref _autoReconnect, value ? 1 : 0);
        }

        /// <summary>Validated delay captured separately for each reconnect wait.</summary>
        public TimeSpan ReconnectDelay
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _reconnectDelayTicks));
            set
            {
                PipeConfiguration.RequireNonNegativeDelay(value, nameof(value));
                Interlocked.Exchange(ref _reconnectDelayTicks, value.Ticks);
            }
        }

        /// <summary>Validated timeout captured separately for each connection attempt.</summary>
        public TimeSpan ConnectTimeout
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _connectTimeoutTicks));
            set
            {
                PipeConfiguration.RequirePositiveTimeout(value, nameof(value));
                Interlocked.Exchange(ref _connectTimeoutTicks, value.Ticks);
            }
        }

        public PipeEventClient(string basePipeName)
        {
            _pipeName = PipeConfiguration.RequirePipeName(
                basePipeName,
                nameof(basePipeName)) + ".events";
        }

        public void Start()
        {
            lock (_lifecycleGate)
            {
                ThrowIfShutdown();
                if (_lifecycleState == Running)
                    throw new InvalidOperationException("PipeEventClient already started.");

                var cts = new CancellationTokenSource();
                _cts = cts;
                Volatile.Write(ref _lifecycleState, Running);
                _loopTask = Task.Run(() => RunLoop(cts.Token));
            }
        }

        private async Task RunLoop(CancellationToken cancellationToken)
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                await ConnectAndListen(cancellationToken).ConfigureAwait(false);

                if (!IsRunning || cancellationToken.IsCancellationRequested || !AutoReconnect)
                    break;

                var delay = ReconnectDelay;
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ConnectAndListen(CancellationToken cancellationToken)
        {
            var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.In,
                PipeOptions.Asynchronous);
            Interlocked.Exchange(ref _pipe, pipe);
            var connected = false;

            try
            {
                var connectTimeout = ConnectTimeout;
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken))
                {
                    connectCts.CancelAfter(connectTimeout);
#if NET9
                    await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
#else
                    var connectTask = Task.Run(
                        () => pipe.Connect(PipeConfiguration.ToTimeoutMilliseconds(connectTimeout)));
                    await PipeTaskCancellation.WithCancellation(
                        connectTask,
                        connectCts.Token).ConfigureAwait(false);
#endif
                }

                connected = true;
                Raise(OnConnected);

                while (IsRunning && pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var message = await PipeFraming.TryReadAsync(
                        pipe,
                        cancellationToken).ConfigureAwait(false);
                    if (message == null)
                        break;

                    if (message.Type == "evt")
                        Dispatch(message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Local shutdown is not reported as a transport error.
            }
            catch (Exception ex)
            {
                Raise(OnError, ex);
            }
            finally
            {
                Interlocked.CompareExchange(ref _pipe, null, pipe);
                try { pipe.Dispose(); } catch { }

                if (connected)
                    Raise(OnDisconnected);
            }
        }

        private void Dispatch(PipeMessage message)
        {
            var handler = OnEvent;
            if (handler == null)
                return;

            foreach (var callback in handler.GetInvocationList())
                Invoke(() => ((Action<PipeMessage>)callback)(message));
        }

        private void Raise(Action? handler)
        {
            if (handler == null)
                return;

            foreach (var callback in handler.GetInvocationList())
                Invoke(() => ((Action)callback)());
        }

        private void Raise(Action<Exception>? handler, Exception error)
        {
            if (handler == null)
                return;

            foreach (var callback in handler.GetInvocationList())
                Invoke(() => ((Action<Exception>)callback)(error));
        }

        private void Invoke(Action callback)
        {
            var previous = CallbackOwner.Value;
            CallbackOwner.Value = this;
            try
            {
                callback();
            }
            catch
            {
                // Consumer callbacks are isolated and serialized on the listen loop.
            }
            finally
            {
                CallbackOwner.Value = previous;
            }
        }

        /// <summary>
        /// Enters terminal shutdown, closes the current transport, and waits for
        /// the background reconnect/listen loop to finish.
        /// </summary>
        public Task ShutdownAsync()
            => BeginShutdown();

        public void Dispose()
        {
            var completion = BeginShutdown();
            if (ReferenceEquals(CallbackOwner.Value, this))
                return;

            try { completion.Wait(2000); } catch { }
        }

#if NET9
        public async ValueTask DisposeAsync()
        {
            await ShutdownAsync().ConfigureAwait(false);
        }
#endif

        private Task BeginShutdown()
        {
            CancellationTokenSource? cts;
            NamedPipeClientStream? pipe;
            Task completion;

            lock (_lifecycleGate)
            {
                if (_lifecycleState == Shutdown)
                    return _shutdownCompletion;

                Volatile.Write(ref _lifecycleState, Shutdown);
                cts = _cts;
                pipe = Interlocked.Exchange(ref _pipe, null);
                _shutdownCompletion = CompleteShutdownAsync(
                    _loopTask ?? Task.CompletedTask,
                    cts);
                completion = _shutdownCompletion;
            }

            try { cts?.Cancel(); } catch { }
            try { pipe?.Dispose(); } catch { }
            return completion;
        }

        private static async Task CompleteShutdownAsync(
            Task loopTask,
            CancellationTokenSource? cts)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            finally
            {
                cts?.Dispose();
            }
        }

        private bool IsRunning => Volatile.Read(ref _lifecycleState) == Running;

        private void ThrowIfShutdown()
        {
            if (_lifecycleState == Shutdown)
                throw new ObjectDisposedException(nameof(PipeEventClient));
        }
    }
}

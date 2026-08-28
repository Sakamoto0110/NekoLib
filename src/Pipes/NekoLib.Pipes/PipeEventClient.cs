using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Owns one background connection/reconnect loop for an event endpoint at the
    /// captured base pipe name plus <c>.events</c>. Callbacks are serialized on
    /// that loop, are not marshalled to a synchronization context, and are
    /// isolated individually from subscriber exceptions.
    /// </summary>
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

        /// <summary>
        /// Raised in wire order for each <c>evt</c> frame. The payload may contain
        /// application-sensitive data received from the local peer.
        /// </summary>
        public event Action<PipeMessage>? OnEvent;

        /// <summary>Raised after a connection is established and before events from that connection.</summary>
        public event Action? OnConnected;

        /// <summary>
        /// Raised after an established connection ends. A failed connection attempt
        /// raises <see cref="OnError"/> without raising this event.
        /// </summary>
        public event Action? OnDisconnected;

        /// <summary>
        /// Raised for connection, framing, parsing, or listen failures. Subscriber
        /// exceptions are isolated, are not forwarded here, and never terminate
        /// the listen loop. Clean remote EOF and local shutdown are not errors.
        /// </summary>
        public event Action<Exception>? OnError;

        /// <summary>
        /// When true (default), the client reconnects after an attempt or connection
        /// ends until terminal shutdown begins. This is the one live configuration
        /// switch and may be changed while the loop is running.
        /// </summary>
        public bool AutoReconnect
        {
            get => Volatile.Read(ref _autoReconnect) != 0;
            set => Volatile.Write(ref _autoReconnect, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the delay captured separately for each reconnect wait. The
        /// default is 500 milliseconds.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is negative or exceeds <see cref="int.MaxValue"/> milliseconds.
        /// </exception>
        public TimeSpan ReconnectDelay
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _reconnectDelayTicks));
            set
            {
                PipeConfiguration.RequireNonNegativeDelay(value, nameof(value));
                Interlocked.Exchange(ref _reconnectDelayTicks, value.Ticks);
            }
        }

        /// <summary>
        /// Gets or sets the positive timeout captured separately for each connection
        /// attempt. The default is five seconds.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is not positive or exceeds <see cref="int.MaxValue"/> milliseconds.
        /// </exception>
        public TimeSpan ConnectTimeout
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _connectTimeoutTicks));
            set
            {
                PipeConfiguration.RequirePositiveTimeout(value, nameof(value));
                Interlocked.Exchange(ref _connectTimeoutTicks, value.Ticks);
            }
        }

        /// <summary>Initializes an event client and captures the base pipe name.</summary>
        /// <param name="basePipeName">Nonblank base name; <c>.events</c> is appended.</param>
        /// <exception cref="ArgumentException"><paramref name="basePipeName"/> is blank.</exception>
        public PipeEventClient(string basePipeName)
        {
            _pipeName = PipeConfiguration.RequirePipeName(
                basePipeName,
                nameof(basePipeName)) + ".events";
        }

        /// <summary>Starts the one-shot background connection and listen loop.</summary>
        /// <exception cref="InvalidOperationException">The client was already started.</exception>
        /// <exception cref="ObjectDisposedException">Shutdown or disposal has begun.</exception>
        /// <remarks>
        /// When <see cref="AutoReconnect"/> is false and the loop ends, the client
        /// remains started and cannot be restarted. Shut it down to release resources.
        /// </remarks>
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
        /// <returns>An idempotent task representing definitive loop completion and resource cleanup.</returns>
        /// <remarks>
        /// A callback may initiate shutdown, but must not await this task from that
        /// same callback because the task includes completion of the callback loop.
        /// </remarks>
        public Task ShutdownAsync()
            => BeginShutdown();

        /// <summary>
        /// Initiates terminal shutdown and waits synchronously for at most two
        /// seconds. When called from this client's callback, it initiates shutdown
        /// and returns immediately to avoid waiting on the current callback.
        /// </summary>
        public void Dispose()
        {
            var completion = BeginShutdown();
            if (ReferenceEquals(CallbackOwner.Value, this))
                return;

            try { completion.Wait(2000); } catch { }
        }

#if NET9
        /// <summary>Initiates terminal shutdown and asynchronously waits for definitive completion.</summary>
        /// <returns>A value task representing the full shutdown operation.</returns>
        /// <remarks>Do not await this method from one of this client's callbacks.</remarks>
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

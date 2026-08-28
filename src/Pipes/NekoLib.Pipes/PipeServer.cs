using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Hosts instance-owned local named-pipe RPC handlers and an optional event
    /// hub. The server is one-shot: shutdown or disposal is terminal.
    /// </summary>
    public sealed class PipeServer : IDisposable
#if NET9
        , IAsyncDisposable
#endif
    {
        private const int Created = 0;
        private const int Running = 1;
        private const int Shutdown = 2;

        private readonly object _lifecycleGate = new object();
        private readonly string _pipeName;
        private readonly int _maxClients;
        private readonly TimeSpan _clientIdleTimeout;
        private readonly bool _enableEvents;
        private readonly int _maxEventSubscribers;
        private readonly int _eventSubscriberQueueCapacity;
        private readonly PipeEventQueueOverflowPolicy _eventQueueOverflowPolicy;
        private readonly PipeAccessPolicy _accessPolicy;
        private readonly int _maxMessageBytes;
        private readonly IPipeMetrics _metrics;
        private readonly SemaphoreSlim _clientLimiter;
        private readonly PipeOperationRegistry _clientOperations = new PipeOperationRegistry();
        private readonly ConcurrentDictionary<
            string,
            Func<PipeMessage, CancellationToken, Task<PipeMessage>>> _handlers
            = new ConcurrentDictionary<
                string,
                Func<PipeMessage, CancellationToken, Task<PipeMessage>>>();

        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private int _lifecycleState;
        private int _resourcesDisposed;
        private Task _shutdownCompletion = Task.CompletedTask;

        /// <summary>
        /// Gets the owned event hub after <see cref="Start"/> when events are
        /// enabled; otherwise null. The server shuts this hub down and disposes
        /// its resources.
        /// </summary>
        public PipeEventHub? Events { get; private set; }

        /// <summary>Gets the captured operating-system access policy used by RPC and event endpoints.</summary>
        public PipeAccessPolicy AccessPolicy => _accessPolicy;

        internal int ActiveClientOperationCount => _clientOperations.Count;

        internal Task ShutdownCompletion
        {
            get
            {
                lock (_lifecycleGate)
                    return _shutdownCompletion;
            }
        }

        /// <summary>
        /// Initializes a server and captures a validated snapshot of all options.
        /// The supplied options and metrics sink remain caller-owned.
        /// </summary>
        /// <param name="options">Server and optional event-hub configuration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">The configured pipe name is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A count, timeout, frame limit, access policy, or overflow policy is unsupported.
        /// All event settings are validated even when events are disabled.
        /// </exception>
        public PipeServer(PipeServerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _pipeName = PipeConfiguration.RequirePipeName(
                options.PipeName,
                nameof(PipeServerOptions.PipeName));
            _maxClients = PipeConfiguration.RequirePositive(
                options.MaxClients,
                nameof(PipeServerOptions.MaxClients));
            _clientIdleTimeout = PipeConfiguration.RequirePositiveTimeout(
                options.ClientIdleTimeout,
                nameof(PipeServerOptions.ClientIdleTimeout));
            _enableEvents = options.EnableEvents;
            _maxEventSubscribers = PipeConfiguration.RequirePositive(
                options.MaxEventSubscribers,
                nameof(PipeServerOptions.MaxEventSubscribers));
            _eventSubscriberQueueCapacity = PipeConfiguration.RequirePositive(
                options.EventSubscriberQueueCapacity,
                nameof(PipeServerOptions.EventSubscriberQueueCapacity));
            ValidateOverflowPolicy(options.EventQueueOverflowPolicy);
            _eventQueueOverflowPolicy = options.EventQueueOverflowPolicy;
            PipeServerStreamFactory.Validate(options.AccessPolicy);
            _accessPolicy = options.AccessPolicy;
            _maxMessageBytes = PipeConfiguration.RequirePositive(
                options.MaxMessageBytes,
                nameof(PipeServerOptions.MaxMessageBytes));
            _metrics = PipeMetricsGuard.Protect(options.Metrics ?? new SimplePipeMetrics());
            _clientLimiter = new SemaphoreSlim(_maxClients);
        }

        /// <summary>
        /// Adds or replaces the asynchronous handler for an operation name. The
        /// handler receives the request envelope and a token cancelled by server
        /// shutdown; client idle timeout is paused during handler execution.
        /// </summary>
        /// <param name="name">Nonblank operation name used for exact lookup.</param>
        /// <param name="handler">
        /// Handler invoked concurrently for independent clients. Its returned
        /// envelope is normalized to the request ID, <c>res</c> type, and operation name.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Shutdown has begun.</exception>
        /// <remarks>
        /// Handler exceptions are converted to a sanitized
        /// <see cref="PipeErrorCodes.Exception"/> response; the original exception
        /// is supplied only to local metrics. The server does not marshal handlers
        /// to a synchronization context.
        /// </remarks>
        public void Map(
            string name,
            Func<PipeMessage, CancellationToken, Task<PipeMessage>> handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Handler name required.", nameof(name));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lifecycleGate)
            {
                ThrowIfShutdown();
                _handlers[name] = handler;
            }
        }

        /// <summary>Starts the RPC accept loop and, when enabled, the owned event hub.</summary>
        /// <exception cref="InvalidOperationException">The server was already started.</exception>
        /// <exception cref="ObjectDisposedException">Shutdown or disposal has begun.</exception>
        public void Start()
        {
            lock (_lifecycleGate)
            {
                ThrowIfShutdown();
                if (_lifecycleState == Running)
                    throw new InvalidOperationException("PipeServer already started.");

                _cts = new CancellationTokenSource();
                if (_enableEvents)
                {
                    Events = new PipeEventHub(
                        _pipeName,
                        _maxEventSubscribers,
                        _accessPolicy,
                        _eventSubscriberQueueCapacity,
                        _eventQueueOverflowPolicy,
                        _metrics);
                }

                Volatile.Write(ref _lifecycleState, Running);
                Events?.Start();
                _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
            }
        }

        private async Task AcceptLoop(CancellationToken cancellationToken)
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _clientLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!_clientOperations.TryStart(async operation =>
                {
                    NamedPipeServerStream? pipe = null;
                    var connected = false;

                    try
                    {
                        pipe = PipeServerStreamFactory.Create(
                            _pipeName,
                            PipeDirection.InOut,
                            _accessPolicy);
                        if (!operation.SetPipe(pipe))
                            return;

#if NET9
                        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
#else
                        using (cancellationToken.Register(
                            () => { try { pipe?.Dispose(); } catch { } }))
                        {
                            await Task.Run(() =>
                            {
                                try { pipe.WaitForConnection(); }
                                catch when (cancellationToken.IsCancellationRequested) { }
                            }).ConfigureAwait(false);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
#endif

                        connected = true;
                        _metrics.OnServerClientConnected(_pipeName);

                        using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken))
                        {
                            await HandleClient(pipe, idleCts).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Normal shutdown.
                    }
                    catch (Exception) when (cancellationToken.IsCancellationRequested)
                    {
                        // The transport was closed to unblock shutdown.
                    }
                    catch (Exception ex)
                    {
                        _metrics.OnError(_pipeName, "accept_loop", ex);
                    }
                    finally
                    {
                        try { pipe?.Dispose(); } catch { }

                        if (connected)
                            _metrics.OnServerClientDisconnected(_pipeName);

                        _clientLimiter.Release();
                    }
                }))
                {
                    _clientLimiter.Release();
                    break;
                }
            }
        }

        private async Task HandleClient(
            NamedPipeServerStream pipe,
            CancellationTokenSource idleCts)
        {
            var cancellationToken = idleCts.Token;

            while (IsRunning && pipe.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                idleCts.CancelAfter(_clientIdleTimeout);

                PipeMessage? request;
                try
                {
                    request = await PipeFraming.TryReadAsync(
                        pipe,
                        cancellationToken,
                        _maxMessageBytes).ConfigureAwait(false);
                    if (request == null)
                        break;
                }
                catch
                {
                    break;
                }

                idleCts.CancelAfter(Timeout.InfiniteTimeSpan);

                if (request.Type != "req")
                    continue;

                _metrics.OnServerRequestReceived(_pipeName, request.Name);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                PipeMessage response;

                try
                {
                    response = await Dispatch(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _metrics.OnError(_pipeName, "dispatch:" + request.Name, ex);
                    response = new PipeMessage
                    {
                        Id = request.Id,
                        Type = "res",
                        Name = request.Name,
                        Ok = false,
                        Error = new PipeError
                        {
                            Code = PipeErrorCodes.Exception,
                            Message = "The handler failed."
                        }
                    };
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                var toSend = response;
                try
                {
                    await PipeFraming.WriteAsync(
                        pipe,
                        toSend,
                        cancellationToken,
                        _maxMessageBytes).ConfigureAwait(false);
                }
                catch (PipeFrameTooLargeException)
                {
                    toSend = new PipeMessage
                    {
                        Id = request.Id,
                        Type = "res",
                        Name = request.Name,
                        Ok = false,
                        Error = new PipeError
                        {
                            Code = PipeErrorCodes.ResponseTooLarge,
                            Message = "Response exceeded the maximum frame size."
                        }
                    };

                    try
                    {
                        await PipeFraming.WriteAsync(
                            pipe,
                            toSend,
                            cancellationToken,
                            _maxMessageBytes).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }
                finally
                {
                    stopwatch.Stop();
                    _metrics.OnServerResponseSent(
                        _pipeName,
                        request.Name,
                        toSend.Ok,
                        stopwatch.Elapsed);
                }
            }
        }

        private async Task<PipeMessage> Dispatch(
            PipeMessage request,
            CancellationToken cancellationToken)
        {
            if (!_handlers.TryGetValue(request.Name, out var handler))
            {
                return new PipeMessage
                {
                    Id = request.Id,
                    Type = "res",
                    Name = request.Name,
                    Ok = false,
                    Error = new PipeError
                    {
                        Code = PipeErrorCodes.NotFound,
                        Message = "Handler '" + request.Name + "' not registered."
                    }
                };
            }

            var result = await handler(request, cancellationToken).ConfigureAwait(false);
            result.Id = request.Id;
            result.Type = "res";
            result.Name = request.Name;
            return result;
        }

        /// <summary>
        /// Enters terminal shutdown, closes transports, and asynchronously waits
        /// for all admitted server and event-hub work to finish.
        /// </summary>
        /// <returns>
        /// An idempotent completion task for the accept loop, admitted client
        /// operations, event hub, and owned resource cleanup.
        /// </returns>
        /// <remarks>
        /// A handler may initiate shutdown, but must not await this task from
        /// inside that same handler because the task includes the handler.
        /// </remarks>
        public Task ShutdownAsync()
            => BeginShutdown();

        /// <summary>
        /// Initiates terminal shutdown and waits synchronously for at most two
        /// seconds. User handlers that ignore cancellation may outlive this call;
        /// use <see cref="ShutdownAsync"/> for definitive completion.
        /// </summary>
        public void Dispose()
        {
            var completion = BeginShutdown();
            try { completion.Wait(2000); } catch { }
        }

#if NET9
        /// <summary>Initiates terminal shutdown and asynchronously waits for definitive completion.</summary>
        /// <returns>A value task representing the full shutdown operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await ShutdownAsync().ConfigureAwait(false);
        }
#endif

        private Task BeginShutdown()
        {
            lock (_lifecycleGate)
            {
                if (_lifecycleState == Shutdown)
                    return _shutdownCompletion;

                Volatile.Write(ref _lifecycleState, Shutdown);
                try { _cts?.Cancel(); } catch { }
                _clientOperations.BeginStop();

                var acceptTask = _acceptTask ?? Task.CompletedTask;
                var eventShutdown = Events?.ShutdownAsync() ?? Task.CompletedTask;
                _shutdownCompletion = CompleteShutdownAsync(
                    acceptTask,
                    _clientOperations.Completion,
                    eventShutdown);
                return _shutdownCompletion;
            }
        }

        private async Task CompleteShutdownAsync(params Task[] tasks)
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                DisposeResources();
            }
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
                return;

            _clientLimiter.Dispose();
            _cts?.Dispose();
        }

        private bool IsRunning => Volatile.Read(ref _lifecycleState) == Running;

        private void ThrowIfShutdown()
        {
            if (_lifecycleState == Shutdown)
                throw new ObjectDisposedException(nameof(PipeServer));
        }

        private static void ValidateOverflowPolicy(PipeEventQueueOverflowPolicy overflowPolicy)
        {
            if (overflowPolicy != PipeEventQueueOverflowPolicy.DropNewest &&
                overflowPolicy != PipeEventQueueOverflowPolicy.DisconnectSubscriber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(PipeServerOptions.EventQueueOverflowPolicy),
                    overflowPolicy,
                    "Unsupported event queue overflow policy.");
            }
        }
    }
}

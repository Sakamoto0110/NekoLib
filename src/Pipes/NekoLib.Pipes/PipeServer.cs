using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
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

        public PipeEventHub? Events { get; private set; }

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
        public Task ShutdownAsync()
            => BeginShutdown();

        public void Dispose()
        {
            var completion = BeginShutdown();
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

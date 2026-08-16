using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    public sealed class PipeServer : IDisposable
    {
        private readonly PipeServerOptions _o;
        private readonly IPipeMetrics _metrics;

        private readonly SemaphoreSlim _clientLimiter;
        private readonly PipeOperationRegistry _clientOperations = new PipeOperationRegistry();
        // ConcurrentDictionary so Map() (typically before Start, but not enforced) can
        // never race the concurrent TryGetValue reads in Dispatch (audit M2).
        private readonly ConcurrentDictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>> _handlers
            = new ConcurrentDictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>>();

        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private volatile bool _running;
        private int _disposeStarted;
        private int _resourcesDisposed;
        private Task _shutdownCompletion = Task.CompletedTask;

        public PipeEventHub? Events { get; private set; }

        public PipeAccessPolicy AccessPolicy => _o.AccessPolicy;

        internal int ActiveClientOperationCount => _clientOperations.Count;
        internal Task ShutdownCompletion => _shutdownCompletion;

        public PipeServer(PipeServerOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            PipeServerStreamFactory.Validate(options.AccessPolicy);
            _metrics = options.Metrics ?? new SimplePipeMetrics();
            _clientLimiter = new SemaphoreSlim(options.MaxClients);
        }

        // ============================================================
        // Map handler
        // ============================================================

        public void Map(string name, Func<PipeMessage, CancellationToken, Task<PipeMessage>> handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Handler name required.", nameof(name));

            _handlers[name] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        // ============================================================
        // Start
        // ============================================================

        public void Start()
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                throw new ObjectDisposedException(nameof(PipeServer));
            if (_running)
                throw new InvalidOperationException("PipeServer already started.");

            _running = true;
            _cts = new CancellationTokenSource();

            if (_o.EnableEvents)
            {
                Events = new PipeEventHub(
                    _o.PipeName,
                    _o.MaxEventSubscribers,
                    _o.AccessPolicy,
                    _o.EventSubscriberQueueCapacity,
                    _o.EventQueueOverflowPolicy,
                    _metrics);

                Events.Start();
            }

            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
        }

        // ============================================================
        // Accept loop
        // ============================================================

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    await _clientLimiter.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!_clientOperations.TryStart(async operation =>
                {
                    NamedPipeServerStream? pipe = null;
                    bool connected = false;

                    try
                    {
                        pipe = PipeServerStreamFactory.Create(
                            _o.PipeName,
                            PipeDirection.InOut,
                            _o.AccessPolicy);
                        if (!operation.SetPipe(pipe))
                            return;

#if NET9
                        await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
#else
                        // net481 WaitForConnection can't observe ct; dispose the pipe on
                        // cancel so the blocked wait throws and releases its thread on
                        // shutdown instead of leaking until GC (audit M6). The throw is
                        // swallowed *inside* the delegate (only while cancelling) so it
                        // doesn't surface as a user-unhandled first-chance break in the
                        // debugger; we then bail cleanly via the token below.
                        using (ct.Register(() => { try { pipe?.Dispose(); } catch { } }))
                        {
                            await Task.Run(() =>
                            {
                                try { pipe.WaitForConnection(); }
                                catch when (ct.IsCancellationRequested) { /* pipe disposed on shutdown */ }
                            }).ConfigureAwait(false);
                        }

                        ct.ThrowIfCancellationRequested();
#endif

                        connected = true;
                        _metrics.OnServerClientConnected(_o.PipeName);

                        using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            await HandleClient(pipe, idleCts).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // normal shutdown
                    }
                    catch (Exception) when (ct.IsCancellationRequested)
                    {
                        // pipe disposed during shutdown — normal
                    }
                    catch (Exception ex)
                    {
                        _metrics.OnError(_o.PipeName, "accept_loop", ex);
                    }
                    finally
                    {
                        try { pipe?.Dispose(); } catch { }

                        if (connected)
                            _metrics.OnServerClientDisconnected(_o.PipeName);

                        _clientLimiter.Release();
                    }
                }))
                {
                    _clientLimiter.Release();
                    break;
                }
            }
        }

        // ============================================================
        // Client handler
        // ============================================================

        private async Task HandleClient(NamedPipeServerStream pipe, CancellationTokenSource idleCts)
        {
            var ct = idleCts.Token;

            while (_running && pipe.IsConnected && !ct.IsCancellationRequested)
            {
                // Arm the idle timer for the wait for the next request. ClientIdleTimeout
                // now measures inactivity *between* requests (a true idle timeout that
                // resets on activity) rather than capping the whole session.
                try { idleCts.CancelAfter(_o.ClientIdleTimeout); } catch { }

                PipeMessage? request;

                try
                {
                    request = await PipeFraming.TryReadAsync(pipe, ct, _o.MaxMessageBytes).ConfigureAwait(false);
                    if (request == null)
                        break;
                }
                catch
                {
                    break; // client disconnected or bad frame
                }

                // Activity received: pause the idle timer while we dispatch + reply so a
                // slow handler isn't mistaken for an idle client. Shutdown via the linked
                // outer token still cancels.
                try { idleCts.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan); } catch { }

                if (request.Type != "req")
                    continue;

                _metrics.OnServerRequestReceived(_o.PipeName, request.Name);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                PipeMessage response;

                try
                {
                    response = await Dispatch(request, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _metrics.OnError(_o.PipeName, "dispatch:" + request.Name, ex);

                    response = new PipeMessage
                    {
                        Id = request.Id,
                        Type = "res",
                        Name = request.Name,
                        Ok = false,
                        Error = new PipeError
                        {
                            Code = "exception",
                            Message = "The handler failed."
                        }
                    };
                }

                // On net481 a cancelled blocking write continues on its worker thread
                // until the pipe is disposed. Starting it during shutdown can emit only
                // part of the frame before disposal, so close cleanly instead.
                if (ct.IsCancellationRequested)
                    break;

                var toSend = response;
                try
                {
                    await PipeFraming.WriteAsync(pipe, toSend, ct, _o.MaxMessageBytes).ConfigureAwait(false);
                }
                catch (PipeFrameTooLargeException)
                {
                    // Reply with a structured error rather than dropping the connection.
                    // WriteCore validates size before emitting, so nothing was written.
                    toSend = new PipeMessage
                    {
                        Id = request.Id,
                        Type = "res",
                        Name = request.Name,
                        Ok = false,
                        Error = new PipeError
                        {
                            Code = "response_too_large",
                            Message = "Response exceeded the maximum frame size."
                        }
                    };

                    try { await PipeFraming.WriteAsync(pipe, toSend, ct, _o.MaxMessageBytes).ConfigureAwait(false); }
                    catch { break; }
                }
                catch
                {
                    break;
                }
                finally
                {
                    sw.Stop();
                    _metrics.OnServerResponseSent(
                        _o.PipeName,
                        request.Name,
                        toSend.Ok,
                        sw.Elapsed);
                }
            }
        }

        // ============================================================
        // Dispatch
        // ============================================================

        private async Task<PipeMessage> Dispatch(PipeMessage request, CancellationToken ct)
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
                        Code = "not_found",
                        Message = "Handler '" + request.Name + "' not registered."
                    }
                };
            }

            var result = await handler(request, ct).ConfigureAwait(false);

            result.Id = request.Id;
            result.Type = "res";
            result.Name = request.Name;

            return result;
        }

        // ============================================================
        // Dispose
        // ============================================================

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }
            _clientOperations.BeginStop();
            try { Events?.Dispose(); } catch { }

            var acceptTask = _acceptTask ?? Task.CompletedTask;
            _shutdownCompletion = Task.WhenAll(acceptTask, _clientOperations.Completion)
                .ContinueWith(
                    _ => DisposeResources(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

            try { _shutdownCompletion.Wait(2000); } catch { }
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
                return;

            _clientLimiter.Dispose();
            _cts?.Dispose();
        }
    }
}

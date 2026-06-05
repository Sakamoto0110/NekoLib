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
        // ConcurrentDictionary so Map() (typically before Start, but not enforced) can
        // never race the concurrent TryGetValue reads in Dispatch (audit M2).
        private readonly ConcurrentDictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>> _handlers
            = new ConcurrentDictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>>();

        private CancellationTokenSource? _cts;
        private volatile bool _running;

        public PipeEventHub? Events { get; private set; }

        public PipeServer(PipeServerOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
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
            if (_running)
                throw new InvalidOperationException("PipeServer already started.");

            _running = true;
            _cts = new CancellationTokenSource();

            if (_o.EnableEvents)
            {
                Events = new PipeEventHub(
                    _o.PipeName,
                    _o.MaxEventSubscribers,
                    _metrics);

                Events.Start();
            }

            Task.Run(() => AcceptLoop(_cts.Token));
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

                _ = Task.Run(async () =>
                {
                    NamedPipeServerStream? pipe = null;
                    bool connected = false;

                    try
                    {
                        pipe = new NamedPipeServerStream(
                            _o.PipeName,
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

#if NET9
                        await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
#else
                        // net481 WaitForConnection can't observe ct; dispose the pipe on
                        // cancel so the blocked wait throws and releases its thread on
                        // shutdown instead of leaking until GC (audit M6).
                        using (ct.Register(() => { try { pipe?.Dispose(); } catch { } }))
                        {
                            await Task.Run(() => pipe.WaitForConnection()).ConfigureAwait(false);
                        }
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

                }, ct);
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

                PipeMessage request;

                try
                {
                    request = await PipeFraming.ReadAsync(pipe, ct, _o.MaxMessageBytes).ConfigureAwait(false);
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
                            Message = ex.Message
                        }
                    };
                }

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
            if (!_running)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }
            try { Events?.Dispose(); } catch { }

            _clientLimiter.Dispose();
            _cts?.Dispose();
        }
    }
}

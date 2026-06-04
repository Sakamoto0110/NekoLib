using System;
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
        private readonly Dictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>> _handlers
            = new Dictionary<string, Func<PipeMessage, CancellationToken, Task<PipeMessage>>>();

        private CancellationTokenSource _cts;
        private volatile bool _running;

        public PipeEventHub Events { get; private set; }

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
                    NamedPipeServerStream pipe = null;
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
                        await Task.Run(() => pipe.WaitForConnection()).ConfigureAwait(false);
#endif

                        connected = true;
                        _metrics.OnServerClientConnected(_o.PipeName);

                        using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            idleCts.CancelAfter(_o.ClientIdleTimeout);
                            await HandleClient(pipe, idleCts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // normal shutdown
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

        private async Task HandleClient(NamedPipeServerStream pipe, CancellationToken ct)
        {
            while (_running && pipe.IsConnected && !ct.IsCancellationRequested)
            {
                PipeMessage request;

                try
                {
#if NET9
                    request = await PipeFraming.ReadAsync(pipe, ct).ConfigureAwait(false);
#else
                    request = await PipeFraming.ReadAsync(pipe, ct).ConfigureAwait(false);
#endif
                }
                catch
                {
                    break; // client disconnected or bad frame
                }

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

                try
                {
#if NET9
                    await PipeFraming.WriteAsync(pipe, response, ct).ConfigureAwait(false);
#else
                    await PipeFraming.WriteAsync(pipe, response, ct).ConfigureAwait(false);
#endif
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
                        response.Ok,
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

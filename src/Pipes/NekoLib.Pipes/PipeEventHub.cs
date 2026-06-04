using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Tasks;

#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif
namespace NekoLib.Pipes
{

    using System;
    using System.Collections.Concurrent;
    using System.IO.Pipes;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class PipeEventHub : IDisposable
#if NET9
    , IAsyncDisposable
#endif

    {
        private readonly string _pipeName;
        private readonly IPipeMetrics _metrics;
        private readonly SemaphoreSlim _subscriberLimiter;
        private readonly ConcurrentDictionary<Guid, NamedPipeServerStream> _subscribers
            = new();

        private CancellationTokenSource? _cts;
        private volatile bool _running;

        public int SubscriberCount => _subscribers.Count;

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            IPipeMetrics? metrics = null)
        {
            _pipeName = basePipeName + ".events";
            _subscriberLimiter = new SemaphoreSlim(maxSubscribers);
            _metrics = metrics ?? NoopPipeMetrics.Instance;
        }

        public void Start()
        {
            if (_running)
                throw new InvalidOperationException("EventHub already started.");

            _running = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    await _subscriberLimiter.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(async () =>
                {
                    NamedPipeServerStream? pipe = null;
                    var id = Guid.NewGuid();

                    try
                    {
                        pipe = new NamedPipeServerStream(
                            _pipeName,
                            PipeDirection.Out,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

#if NET9
                    await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
#else
                        // See PipeServer: dispose-on-cancel so the blocked net481 wait
                        // releases its thread on shutdown rather than leaking (audit M6).
                        using (ct.Register(() => { try { pipe.Dispose(); } catch { } }))
                        {
                            await Task.Run(() => pipe.WaitForConnection()).ConfigureAwait(false);
                        }
#endif

                        _subscribers[id] = pipe;
                        _metrics.OnServerClientConnected(_pipeName);

                        // Keep subscriber alive until disconnect
                        while (_running && pipe.IsConnected && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(500, ct).ConfigureAwait(false);
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
                        _metrics.OnError(_pipeName, "event_accept", ex);
                    }
                    finally
                    {
                        RemoveSubscriber(id);
                        // Dispose the accept pipe even if it never registered a subscriber
                        // (e.g. WaitForConnection cancelled on shutdown) so it doesn't
                        // linger and intercept a later client connect (audit M6).
                        try { pipe?.Dispose(); } catch { }
                        _subscriberLimiter.Release();
                    }

                }, ct);
            }
        }

        public async Task PublishAsync(
            string eventName,
            object? payload,
            CancellationToken ct = default)
        {
            if (!_running)
                return;

            PipeMessage msg;

#if NET9
        msg = new PipeMessage
        {
            Id = Guid.NewGuid(),
            Type = "evt",
            Name = eventName,
            Ok = true,
            Data = payload == null
                ? null
                : System.Text.Json.JsonSerializer.SerializeToElement(payload)
        };
#else
            msg = new PipeMessage
            {
                Id = Guid.NewGuid(),
                Type = "evt",
                Name = eventName,
                Ok = true,
                Data = payload == null
                    ? null
                    : Newtonsoft.Json.Linq.JToken.FromObject(payload)
            };
#endif

            int total = _subscribers.Count;
            int success = 0;
            int failed = 0;

            foreach (var kv in _subscribers.ToArray())
            {
                var id = kv.Key;
                var pipe = kv.Value;

                try
                {
                    if (!pipe.IsConnected)
                        throw new Exception("Disconnected subscriber.");

#if NET9
                await PipeFraming.WriteAsync(pipe, msg, ct).ConfigureAwait(false);
#else
                    await PipeFraming.WriteAsync(pipe, msg, ct).ConfigureAwait(false);
#endif

                    success++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _metrics.OnError(_pipeName, "event_publish", ex);
                    RemoveSubscriber(id);
                }
            }

            _metrics.OnServerEventPublished(
                _pipeName,
                eventName,
                total,
                success,
                failed);
        }

        private void RemoveSubscriber(Guid id)
        {
            if (_subscribers.TryRemove(id, out var pipe))
            {
                try { pipe.Dispose(); } catch { }
                _metrics.OnServerClientDisconnected(_pipeName);
            }
        }

        public void Dispose()
        {
            if (!_running)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }

            foreach (var kv in _subscribers)
            {
                try { kv.Value.Dispose(); } catch { }
            }

            _subscribers.Clear();
            _subscriberLimiter.Dispose();
            _cts?.Dispose();
        }
#if NET9
public ValueTask DisposeAsync()
{
    DisposeCore();
    return ValueTask.CompletedTask;
}
#endif
        private void DisposeCore()
        {
            if (!_running)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }

            foreach (var kv in _subscribers)
            {
                try { kv.Value.Dispose(); } catch { }
            }

            _subscribers.Clear();
            _subscriberLimiter.Dispose();
            _cts?.Dispose();
        }
    }

}
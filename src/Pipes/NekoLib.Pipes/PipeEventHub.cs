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
        private readonly PipeAccessPolicy _accessPolicy;
        private readonly SemaphoreSlim _subscriberLimiter;
        private readonly ConcurrentDictionary<Guid, NamedPipeServerStream> _subscribers
            = new();

        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private volatile bool _running;

        public int SubscriberCount => _subscribers.Count;

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            IPipeMetrics? metrics = null)
            : this(
                basePipeName,
                maxSubscribers,
                PipeAccessPolicy.PlatformDefault,
                metrics)
        {
        }

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            PipeAccessPolicy accessPolicy,
            IPipeMetrics? metrics = null)
        {
            PipeServerStreamFactory.Validate(accessPolicy);
            _pipeName = basePipeName + ".events";
            _subscriberLimiter = new SemaphoreSlim(maxSubscribers);
            _metrics = metrics ?? NoopPipeMetrics.Instance;
            _accessPolicy = accessPolicy;
        }

        public void Start()
        {
            if (_running)
                throw new InvalidOperationException("EventHub already started.");

            _running = true;
            _cts = new CancellationTokenSource();

            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
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
                        pipe = PipeServerStreamFactory.Create(
                            _pipeName,
                            PipeDirection.Out,
                            _accessPolicy);

#if NET9
                    await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
#else
                        // See PipeServer: dispose-on-cancel so the blocked net481 wait
                        // releases its thread on shutdown rather than leaking (audit M6).
                        // The throw is contained inside the delegate (only while cancelling)
                        // so it doesn't surface as a user-unhandled break; bail via the token.
                        using (ct.Register(() => { try { pipe.Dispose(); } catch { } }))
                        {
                            await Task.Run(() =>
                            {
                                try { pipe.WaitForConnection(); }
                                catch when (ct.IsCancellationRequested) { /* pipe disposed on shutdown */ }
                            }).ConfigureAwait(false);
                        }

                        ct.ThrowIfCancellationRequested();
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

            var subs = _subscribers.ToArray();
            int total = subs.Length;

            // Write to every subscriber concurrently so one slow/backed-up subscriber
            // can't stall delivery to the others (audit M3 — head-of-line blocking).
            var sends = new Task<bool>[subs.Length];
            for (int i = 0; i < subs.Length; i++)
                sends[i] = SendToSubscriber(subs[i].Key, subs[i].Value, msg, ct);

            bool[] results = await Task.WhenAll(sends).ConfigureAwait(false);

            int success = 0, failed = 0;
            foreach (var r in results)
            {
                if (r) success++;
                else failed++;
            }

            _metrics.OnServerEventPublished(
                _pipeName,
                eventName,
                total,
                success,
                failed);
        }

        private async Task<bool> SendToSubscriber(
            Guid id,
            System.IO.Pipes.NamedPipeServerStream pipe,
            PipeMessage msg,
            CancellationToken ct)
        {
            try
            {
                if (!pipe.IsConnected)
                    throw new InvalidOperationException("Disconnected subscriber.");

                await PipeFraming.WriteAsync(pipe, msg, ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _metrics.OnError(_pipeName, "event_publish", ex);
                RemoveSubscriber(id);
                return false;
            }
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
            try { _acceptTask?.Wait(2000); } catch { }

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
            try { _acceptTask?.Wait(2000); } catch { }

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

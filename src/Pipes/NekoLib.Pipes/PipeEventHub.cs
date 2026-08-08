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
        private readonly int _subscriberQueueCapacity;
        private readonly PipeEventQueueOverflowPolicy _overflowPolicy;
        private readonly SemaphoreSlim _subscriberLimiter;
        private readonly PipeOperationRegistry _subscriberOperations = new PipeOperationRegistry();
        private readonly ConcurrentDictionary<Guid, EventSubscriber> _subscribers
            = new();

        private const int DefaultSubscriberQueueCapacity = 64;

        private sealed class PendingEvent
        {
            public PipeMessage Message { get; }
            public EventDeliveryTracker Tracker { get; }

            public PendingEvent(PipeMessage message, EventDeliveryTracker tracker)
            {
                Message = message;
                Tracker = tracker;
            }
        }

        private sealed class EventDeliveryTracker
        {
            private readonly IPipeMetrics _metrics;
            private readonly string _pipeName;
            private readonly string _eventName;
            private readonly int _subscribers;
            private int _remaining;
            private int _success;
            private int _failed;

            public EventDeliveryTracker(
                IPipeMetrics metrics,
                string pipeName,
                string eventName,
                int subscribers)
            {
                _metrics = metrics;
                _pipeName = pipeName;
                _eventName = eventName;
                _subscribers = subscribers;
                _remaining = subscribers;

                if (subscribers == 0)
                    PublishMetrics();
            }

            public void Complete(bool success)
            {
                if (success)
                    Interlocked.Increment(ref _success);
                else
                    Interlocked.Increment(ref _failed);

                if (Interlocked.Decrement(ref _remaining) == 0)
                    PublishMetrics();
            }

            private void PublishMetrics()
            {
                _metrics.OnServerEventPublished(
                    _pipeName,
                    _eventName,
                    _subscribers,
                    Volatile.Read(ref _success),
                    Volatile.Read(ref _failed));
            }
        }

        private sealed class EventSubscriber
        {
            private readonly object _gate = new object();
            private readonly Queue<PendingEvent> _queue = new Queue<PendingEvent>();
            private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
            private readonly int _capacity;
            private bool _removed;

            public NamedPipeServerStream Pipe { get; }

            public EventSubscriber(NamedPipeServerStream pipe, int capacity)
            {
                Pipe = pipe;
                _capacity = capacity;
            }

            public bool TryEnqueue(PendingEvent pending)
            {
                lock (_gate)
                {
                    if (_removed || _queue.Count >= _capacity)
                        return false;

                    _queue.Enqueue(pending);
                    _available.Release();
                    return true;
                }
            }

            public async Task<PendingEvent?> DequeueAsync(CancellationToken cancellationToken)
            {
                await _available.WaitAsync(cancellationToken).ConfigureAwait(false);

                lock (_gate)
                {
                    if (_removed || _queue.Count == 0)
                        return null;

                    return _queue.Dequeue();
                }
            }

            public PendingEvent[] Remove()
            {
                lock (_gate)
                {
                    if (_removed)
                        return Array.Empty<PendingEvent>();

                    _removed = true;
                    var pending = _queue.ToArray();
                    _queue.Clear();
                    try { _available.Release(); } catch { }
                    return pending;
                }
            }

            public void Dispose()
            {
                _available.Dispose();
            }
        }

        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private volatile bool _running;
        private int _disposeStarted;
        private int _resourcesDisposed;
        private Task _shutdownCompletion = Task.CompletedTask;

        public int SubscriberCount => _subscribers.Count;
        internal int ActiveSubscriberOperationCount => _subscriberOperations.Count;
        internal Task ShutdownCompletion => _shutdownCompletion;

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            IPipeMetrics? metrics = null)
            : this(
                basePipeName,
                maxSubscribers,
                PipeAccessPolicy.PlatformDefault,
                DefaultSubscriberQueueCapacity,
                PipeEventQueueOverflowPolicy.DropNewest,
                metrics)
        {
        }

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            PipeAccessPolicy accessPolicy,
            IPipeMetrics? metrics = null)
            : this(
                basePipeName,
                maxSubscribers,
                accessPolicy,
                DefaultSubscriberQueueCapacity,
                PipeEventQueueOverflowPolicy.DropNewest,
                metrics)
        {
        }

        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            PipeAccessPolicy accessPolicy,
            int subscriberQueueCapacity,
            PipeEventQueueOverflowPolicy overflowPolicy,
            IPipeMetrics? metrics = null)
        {
            PipeServerStreamFactory.Validate(accessPolicy);
            if (subscriberQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(subscriberQueueCapacity));
            if (overflowPolicy != PipeEventQueueOverflowPolicy.DropNewest &&
                overflowPolicy != PipeEventQueueOverflowPolicy.DisconnectSubscriber)
            {
                throw new ArgumentOutOfRangeException(nameof(overflowPolicy));
            }

            _pipeName = basePipeName + ".events";
            _subscriberLimiter = new SemaphoreSlim(maxSubscribers);
            _metrics = metrics ?? NoopPipeMetrics.Instance;
            _accessPolicy = accessPolicy;
            _subscriberQueueCapacity = subscriberQueueCapacity;
            _overflowPolicy = overflowPolicy;
        }

        public void Start()
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                throw new ObjectDisposedException(nameof(PipeEventHub));
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

                if (!_subscriberOperations.TryStart(async operation =>
                {
                    NamedPipeServerStream? pipe = null;
                    var id = Guid.NewGuid();
                    EventSubscriber? subscriber = null;
                    Task? writerTask = null;

                    try
                    {
                        pipe = PipeServerStreamFactory.Create(
                            _pipeName,
                            PipeDirection.Out,
                            _accessPolicy);
                        if (!operation.SetPipe(pipe))
                            return;

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

                        subscriber = new EventSubscriber(pipe, _subscriberQueueCapacity);
                        _subscribers[id] = subscriber;
                        _metrics.OnServerClientConnected(_pipeName);
                        writerTask = DrainSubscriber(id, subscriber, ct);

                        // Keep subscriber alive until disconnect
                        while (_running &&
                               pipe.IsConnected &&
                               !ct.IsCancellationRequested &&
                               !writerTask.IsCompleted)
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
                        if (writerTask != null)
                        {
                            try { await writerTask.ConfigureAwait(false); } catch { }
                        }
                        try { subscriber?.Dispose(); } catch { }
                        // Dispose the accept pipe even if it never registered a subscriber
                        // (e.g. WaitForConnection cancelled on shutdown) so it doesn't
                        // linger and intercept a later client connect (audit M6).
                        try { pipe?.Dispose(); } catch { }
                        _subscriberLimiter.Release();
                    }
                }))
                {
                    _subscriberLimiter.Release();
                    break;
                }
            }
        }

        /// <summary>
        /// Queues an event for best-effort delivery. The returned task confirms
        /// the enqueue attempt; it does not wait for subscriber I/O.
        /// </summary>
        public Task PublishAsync(
            string eventName,
            object? payload,
            CancellationToken ct = default)
        {
            if (!_running)
                return Task.CompletedTask;

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
            var tracker = new EventDeliveryTracker(
                _metrics,
                _pipeName,
                eventName,
                total);

            foreach (var pair in subs)
            {
                if (ct.IsCancellationRequested)
                {
                    tracker.Complete(success: false);
                    continue;
                }

                var pending = new PendingEvent(msg, tracker);
                if (pair.Value.TryEnqueue(pending))
                    continue;

                tracker.Complete(success: false);
                if (_overflowPolicy == PipeEventQueueOverflowPolicy.DisconnectSubscriber)
                    RemoveSubscriber(pair.Key);
            }

            return Task.CompletedTask;
        }

        private async Task DrainSubscriber(
            Guid id,
            EventSubscriber subscriber,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PendingEvent? pending = null;
                try
                {
                    pending = await subscriber.DequeueAsync(cancellationToken).ConfigureAwait(false);
                    if (pending == null)
                        return;

                    if (!subscriber.Pipe.IsConnected)
                        throw new InvalidOperationException("Disconnected subscriber.");

                    await PipeFraming.WriteAsync(
                        subscriber.Pipe,
                        pending.Message,
                        cancellationToken).ConfigureAwait(false);
                    pending.Tracker.Complete(success: true);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (pending != null)
                        pending.Tracker.Complete(success: false);
                    if (!(_cts?.IsCancellationRequested ?? false))
                        _metrics.OnError(_pipeName, "event_publish", ex);
                    RemoveSubscriber(id);
                    return;
                }
            }
        }

        private void RemoveSubscriber(Guid id)
        {
            if (_subscribers.TryRemove(id, out var subscriber))
            {
                foreach (var pending in subscriber.Remove())
                    pending.Tracker.Complete(success: false);

                try { subscriber.Pipe.Dispose(); } catch { }
                _metrics.OnServerClientDisconnected(_pipeName);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return;

            _running = false;

            try { _cts?.Cancel(); } catch { }
            _subscriberOperations.BeginStop();

            foreach (var kv in _subscribers)
            {
                try { RemoveSubscriber(kv.Key); } catch { }
            }

            var acceptTask = _acceptTask ?? Task.CompletedTask;
            _shutdownCompletion = Task.WhenAll(acceptTask, _subscriberOperations.Completion)
                .ContinueWith(
                    _ => DisposeResources(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

            try { _shutdownCompletion.Wait(2000); } catch { }
        }
#if NET9
        public async ValueTask DisposeAsync()
        {
            Dispose();
            await _shutdownCompletion.ConfigureAwait(false);
        }
#endif

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
                return;

            _subscribers.Clear();
            _subscriberLimiter.Dispose();
            _cts?.Dispose();
        }
    }

}

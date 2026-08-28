using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Hosts a bounded best-effort local event channel at the captured base pipe
    /// name plus <c>.events</c>. Each subscriber owns an independent FIFO queue
    /// and writer; the hub is one-shot and terminal after shutdown.
    /// </summary>
    public sealed class PipeEventHub : IDisposable
#if NET9
        , IAsyncDisposable
#endif
    {
        private const int Created = 0;
        private const int Running = 1;
        private const int Shutdown = 2;
        private const int DefaultSubscriberQueueCapacity = 64;

        private readonly object _lifecycleGate = new object();
        private readonly string _pipeName;
        private readonly IPipeMetrics _metrics;
        private readonly PipeAccessPolicy _accessPolicy;
        private readonly int _subscriberQueueCapacity;
        private readonly PipeEventQueueOverflowPolicy _overflowPolicy;
        private readonly SemaphoreSlim _subscriberLimiter;
        private readonly PipeOperationRegistry _subscriberOperations = new PipeOperationRegistry();
        private readonly ConcurrentDictionary<Guid, EventSubscriber> _subscribers
            = new ConcurrentDictionary<Guid, EventSubscriber>();

        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private int _lifecycleState;
        private int _resourcesDisposed;
        private Task _shutdownCompletion = Task.CompletedTask;

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

        /// <summary>Gets the current number of established event subscribers.</summary>
        public int SubscriberCount => _subscribers.Count;

        internal int ActiveSubscriberOperationCount => _subscriberOperations.Count;

        internal Task ShutdownCompletion
        {
            get
            {
                lock (_lifecycleGate)
                    return _shutdownCompletion;
            }
        }

        /// <summary>
        /// Initializes a standalone event hub with platform-default pipe security,
        /// a queue capacity of 64, and <see cref="PipeEventQueueOverflowPolicy.DropNewest"/>.
        /// </summary>
        /// <param name="basePipeName">Nonblank base name; <c>.events</c> is appended.</param>
        /// <param name="maxSubscribers">Positive maximum number of concurrent subscribers.</param>
        /// <param name="metrics">Optional synchronous metrics sink, which remains caller-owned; null selects no-op metrics.</param>
        /// <exception cref="ArgumentException"><paramref name="basePipeName"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSubscribers"/> is not positive.</exception>
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

        /// <summary>
        /// Initializes a standalone event hub with an explicit access policy, a
        /// queue capacity of 64, and <see cref="PipeEventQueueOverflowPolicy.DropNewest"/>.
        /// </summary>
        /// <param name="basePipeName">Nonblank base name; <c>.events</c> is appended.</param>
        /// <param name="maxSubscribers">Positive maximum number of concurrent subscribers.</param>
        /// <param name="accessPolicy">Supported operating-system pipe access policy.</param>
        /// <param name="metrics">Optional synchronous metrics sink, which remains caller-owned; null selects no-op metrics.</param>
        /// <exception cref="ArgumentException"><paramref name="basePipeName"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maxSubscribers"/> is not positive or <paramref name="accessPolicy"/> is unsupported.
        /// </exception>
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

        /// <summary>Initializes a standalone event hub with explicit capacity, access, and overflow contracts.</summary>
        /// <param name="basePipeName">Nonblank base name; <c>.events</c> is appended.</param>
        /// <param name="maxSubscribers">Positive maximum number of concurrent subscribers.</param>
        /// <param name="accessPolicy">Supported operating-system pipe access policy.</param>
        /// <param name="subscriberQueueCapacity">Positive bounded FIFO capacity for each subscriber.</param>
        /// <param name="overflowPolicy">Supported action when one subscriber queue is full.</param>
        /// <param name="metrics">Optional synchronous metrics sink, which remains caller-owned; null selects no-op metrics.</param>
        /// <exception cref="ArgumentException"><paramref name="basePipeName"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A count, access policy, or overflow policy is unsupported.
        /// </exception>
        public PipeEventHub(
            string basePipeName,
            int maxSubscribers,
            PipeAccessPolicy accessPolicy,
            int subscriberQueueCapacity,
            PipeEventQueueOverflowPolicy overflowPolicy,
            IPipeMetrics? metrics = null)
        {
            var capturedBaseName = PipeConfiguration.RequirePipeName(
                basePipeName,
                nameof(basePipeName));
            PipeConfiguration.RequirePositive(maxSubscribers, nameof(maxSubscribers));
            PipeConfiguration.RequirePositive(
                subscriberQueueCapacity,
                nameof(subscriberQueueCapacity));
            PipeServerStreamFactory.Validate(accessPolicy);
            ValidateOverflowPolicy(overflowPolicy);

            _pipeName = capturedBaseName + ".events";
            _subscriberLimiter = new SemaphoreSlim(maxSubscribers);
            _metrics = PipeMetricsGuard.Protect(metrics ?? NoopPipeMetrics.Instance);
            _accessPolicy = accessPolicy;
            _subscriberQueueCapacity = subscriberQueueCapacity;
            _overflowPolicy = overflowPolicy;
        }

        /// <summary>Starts accepting event subscribers.</summary>
        /// <exception cref="InvalidOperationException">The hub was already started.</exception>
        /// <exception cref="ObjectDisposedException">Shutdown or disposal has begun.</exception>
        public void Start()
        {
            lock (_lifecycleGate)
            {
                ThrowIfShutdown();
                if (_lifecycleState == Running)
                    throw new InvalidOperationException("EventHub already started.");

                var cts = new CancellationTokenSource();
                _cts = cts;
                Volatile.Write(ref _lifecycleState, Running);
                _acceptTask = Task.Run(() => AcceptLoop(cts.Token));
            }
        }

        private async Task AcceptLoop(CancellationToken cancellationToken)
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _subscriberLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                            PipeDirection.InOut,
                            _accessPolicy);
                        if (!operation.SetPipe(pipe))
                            return;

#if NET9
                        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
#else
                        using (cancellationToken.Register(
                            () => { try { pipe.Dispose(); } catch { } }))
                        {
                            await Task.Run(() =>
                            {
                                try { pipe.WaitForConnection(); }
                                catch when (cancellationToken.IsCancellationRequested) { }
                            }).ConfigureAwait(false);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
#endif

                        subscriber = new EventSubscriber(pipe, _subscriberQueueCapacity);
                        _subscribers[id] = subscriber;
                        _metrics.OnServerClientConnected(_pipeName);
                        writerTask = DrainSubscriber(id, subscriber, cancellationToken);
                        await WaitForSubscriberDisconnect(pipe, cancellationToken)
                            .ConfigureAwait(false);
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

        private static async Task WaitForSubscriberDisconnect(
            NamedPipeServerStream pipe,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[1];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await pipe.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                        return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (System.IO.IOException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Queues an event for bounded best-effort delivery. The returned task
        /// confirms the enqueue attempt; it does not wait for subscriber I/O.
        /// Events larger than the fixed 1 MiB frame limit are rejected before
        /// any subscriber queue or publication metric is changed.
        /// </summary>
        /// <param name="eventName">
        /// Event name placed on the wire. Pipes does not validate this value;
        /// applications should define a stable nonblank vocabulary.
        /// </param>
        /// <param name="payload">
        /// Optional payload serialized immediately into the target-specific JSON
        /// DOM. Sensitive values are sent to every subscriber admitted for delivery.
        /// </param>
        /// <param name="ct">
        /// Token checked while attempting current subscriber enqueues. Cancellation
        /// marks remaining deliveries failed and does not fault the returned task.
        /// </param>
        /// <returns>An already-completed task after serialization and enqueue attempts finish.</returns>
        /// <exception cref="InvalidOperationException">
        /// The hub has not started or the serialized event exceeds the fixed 1 MiB frame limit.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Shutdown or disposal has begun.</exception>
        /// <remarks>
        /// Serialization failures propagate. Completion does not promise pipe I/O
        /// or delivery; terminal subscriber outcomes update metrics later.
        /// </remarks>
        public Task PublishAsync(
            string eventName,
            object? payload,
            CancellationToken ct = default)
        {
            lock (_lifecycleGate)
            {
                ThrowIfShutdown();
                if (_lifecycleState != Running)
                    throw new InvalidOperationException("EventHub has not been started.");
            }

            PipeMessage message;
#if NET9
            message = new PipeMessage
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
            message = new PipeMessage
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

            PipeFraming.ValidateMessageSize(message);

            var subscribers = _subscribers.ToArray();
            var tracker = new EventDeliveryTracker(
                _metrics,
                _pipeName,
                eventName,
                subscribers.Length);

            foreach (var pair in subscribers)
            {
                if (ct.IsCancellationRequested)
                {
                    tracker.Complete(success: false);
                    continue;
                }

                var pending = new PendingEvent(message, tracker);
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
                    pending = await subscriber.DequeueAsync(cancellationToken)
                        .ConfigureAwait(false);
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
            if (!_subscribers.TryRemove(id, out var subscriber))
                return;

            foreach (var pending in subscriber.Remove())
                pending.Tracker.Complete(success: false);

            try { subscriber.Pipe.Dispose(); } catch { }
            _metrics.OnServerClientDisconnected(_pipeName);
        }

        /// <summary>
        /// Enters terminal shutdown, closes all subscriber transports, and waits
        /// for the accept and writer operations admitted by this hub.
        /// </summary>
        /// <returns>An idempotent task representing definitive hub shutdown and resource cleanup.</returns>
        public Task ShutdownAsync()
            => BeginShutdown();

        /// <summary>
        /// Initiates terminal shutdown and waits synchronously for at most two
        /// seconds. Use <see cref="ShutdownAsync"/> when definitive completion matters.
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
            CancellationTokenSource? cts;
            Task completion;

            lock (_lifecycleGate)
            {
                if (_lifecycleState == Shutdown)
                    return _shutdownCompletion;

                Volatile.Write(ref _lifecycleState, Shutdown);
                cts = _cts;
                _shutdownCompletion = CompleteShutdownAsync(
                    _acceptTask ?? Task.CompletedTask,
                    _subscriberOperations.Completion);
                completion = _shutdownCompletion;
            }

            try { cts?.Cancel(); } catch { }
            _subscriberOperations.BeginStop();
            foreach (var pair in _subscribers)
            {
                try { RemoveSubscriber(pair.Key); } catch { }
            }

            return completion;
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

            _subscribers.Clear();
            _subscriberLimiter.Dispose();
            _cts?.Dispose();
        }

        private bool IsRunning => Volatile.Read(ref _lifecycleState) == Running;

        private void ThrowIfShutdown()
        {
            if (_lifecycleState == Shutdown)
                throw new ObjectDisposedException(nameof(PipeEventHub));
        }

        private static void ValidateOverflowPolicy(PipeEventQueueOverflowPolicy overflowPolicy)
        {
            if (overflowPolicy != PipeEventQueueOverflowPolicy.DropNewest &&
                overflowPolicy != PipeEventQueueOverflowPolicy.DisconnectSubscriber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(overflowPolicy),
                    overflowPolicy,
                    "Unsupported event queue overflow policy.");
            }
        }
    }
}

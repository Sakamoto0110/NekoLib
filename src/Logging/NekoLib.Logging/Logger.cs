using NekoLib.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Logging
{
    /// <summary>
    /// Synchronous ordered logging pipeline. Accepted entries are dispatched to
    /// sinks inline and in registration order; one failing sink never suppresses
    /// later sinks.
    /// <para/>
    /// Every sink observes the same delivery order, and each writer's own entries
    /// keep that writer's order. Entries are stamped before the dispatch lock is
    /// taken, so under concurrent writers <c>TimestampUtc</c> is not a
    /// delivery-order key.
    /// </summary>
    public sealed class Logger : ILogger, ILogSnapshotSource, ILogFlusher, IDisposable
    {
        private readonly object _gate = new object();
        private readonly ILogSink[] _sinks;
        private readonly LogLevel _minLevel;
        private readonly int _recentEntryCapacity;
        private readonly bool _disposeSinks;
        private readonly Queue<LogEntry> _recentEntries;
        private int _disposed;

        public Logger(LogLevel minLevel, params ILogSink[]? sinks)
            : this(new LoggerOptions { MinimumLevel = minLevel }, sinks)
        {
        }

        public Logger(LoggerOptions? options = null, params ILogSink[]? sinks)
        {
            options = options ?? new LoggerOptions();
            if (options.RecentEntryCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(options.RecentEntryCapacity));

            _minLevel = options.MinimumLevel;
            _recentEntryCapacity = options.RecentEntryCapacity;
            _disposeSinks = options.DisposeSinks;
            _sinks = CopySinks(sinks);
            _recentEntries = new Queue<LogEntry>(_recentEntryCapacity);
        }

        /// <summary>
        /// Takes the pipeline's own copy of the sink set. A caller that passes an
        /// explicitly constructed array keeps a reference to it, and swapping an
        /// element afterwards must not re-target dispatch or change what disposal
        /// flushes and disposes. Null elements are dropped once here rather than
        /// being re-checked on every write.
        /// </summary>
        private static ILogSink[] CopySinks(ILogSink[]? sinks)
        {
            if (sinks == null || sinks.Length == 0)
                return Array.Empty<ILogSink>();

            var accepted = new List<ILogSink>(sinks.Length);
            for (int i = 0; i < sinks.Length; i++)
            {
                var sink = sinks[i];
                if (sink != null)
                    accepted.Add(sink);
            }

            return accepted.Count == 0
                ? Array.Empty<ILogSink>()
                : accepted.ToArray();
        }

        public void Log(
            LogLevel level,
            string message,
            Exception? exception = null,
            string? category = null)
        {
            if (level < _minLevel || Volatile.Read(ref _disposed) != 0)
                return;

            var entry = new LogEntry(
                DateTime.UtcNow,
                level,
                message,
                exception,
                category);

            lock (_gate)
            {
                if (_disposed != 0)
                    return;

                _recentEntries.Enqueue(entry);
                while (_recentEntries.Count > _recentEntryCapacity)
                    _recentEntries.Dequeue();

                for (int i = 0; i < _sinks.Length; i++)
                {
                    try { _sinks[i].Write(entry); }
                    catch { /* logging must never break feature behavior */ }
                }
            }
        }

        /// <summary>
        /// Returns the newest retained entries in chronological order, bounded by
        /// <paramref name="maxEntries"/> and by the configured capacity. The
        /// result is a fresh collection that never aliases pipeline state, and it
        /// stays readable after disposal so an incident collector can still take a
        /// post-shutdown snapshot.
        /// </summary>
        public IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
        {
            if (maxEntries <= 0)
                return Array.Empty<LogEntry>();

            lock (_gate)
            {
                var all = _recentEntries.ToArray();
                var take = Math.Min(maxEntries, all.Length);
                var result = new LogEntry[take];
                Array.Copy(all, all.Length - take, result, 0, take);
                return result;
            }
        }

        /// <summary>
        /// Requests completion of pending sink work within
        /// <paramref name="timeout"/>. A sink that fails does not stop later
        /// sinks while budget remains; budget exhaustion stops further flush
        /// admission. <c>false</c> means completion was not confirmed for at
        /// least one sink inside the budget; it does not cancel that sink, which
        /// may still be running - and may therefore observe a later
        /// <c>Write</c> concurrently with its own <c>Flush</c>. Returns
        /// <c>true</c> after disposal completes, because disposal performs the
        /// final flush. A concurrent disposal still observes this timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="timeout"/> is negative, which includes
        /// <see cref="Timeout.InfiniteTimeSpan"/>. A bounded completion request
        /// has no unbounded form; use <see cref="Dispose"/> for that.
        /// </exception>
        public bool Flush(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            var watch = Stopwatch.StartNew();
            var timeoutMilliseconds = timeout.TotalMilliseconds > int.MaxValue
                ? int.MaxValue
                : (int)timeout.TotalMilliseconds;

            if (!Monitor.TryEnter(_gate, timeoutMilliseconds))
                return false;

            try
            {
                // Disposal owns the same gate and publishes its terminal state
                // before releasing it. Reaching this branch therefore proves the
                // final flush completed rather than merely started.
                if (_disposed != 0)
                    return true;

                var confirmed = true;

                for (int i = 0; i < _sinks.Length; i++)
                {
                    var flushable = _sinks[i] as IFlushableLogSink;
                    if (flushable == null)
                        continue;

                    var remaining = timeout - watch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    // One sink never decides the outcome for the others: the file
                    // sink must still be flushed when an unrelated sink fails.
                    if (!FlushSink(flushable, remaining))
                        confirmed = false;
                }

                return confirmed;
            }
            finally
            {
                Monitor.Exit(_gate);
            }
        }

        /// <summary>
        /// Requests one sink flush within the remaining budget. A sink that
        /// outlives the budget keeps running on its own thread; the caller only
        /// learns that completion was not confirmed.
        /// </summary>
        private static bool FlushSink(IFlushableLogSink sink, TimeSpan remaining)
        {
            Task task;
            try { task = Task.Run((Action)sink.Flush); }
            catch { return false; }

            // Reading the fault of an abandoned flush keeps
            // TaskScheduler.UnobservedTaskException - which NekoLib.Diagnostics
            // reports as a process crash - from turning a slow sink into an
            // incident long after the flush returned.
            task.ContinueWith(
                completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            try { return task.Wait(remaining); }
            catch { return false; }
        }

        /// <summary>
        /// Stops accepting entries, then performs one final best-effort flush of
        /// every flushable sink and disposes them when
        /// <see cref="LoggerOptions.DisposeSinks"/> is set. Borrowed sinks are
        /// flushed but never disposed, which is what lets two loggers share one
        /// sink. Idempotent, and never throws: sink failures are isolated.
        /// <para/>
        /// This final flush carries no time budget. Call
        /// <see cref="Flush(TimeSpan)"/> first when shutdown must be bounded.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed != 0)
                    return;

                // Publish disposal only after owning the pipeline gate. A
                // concurrent bounded Flush then either completes first, waits for
                // this final flush, or returns false when its budget expires.
                Volatile.Write(ref _disposed, 1);

                for (int i = 0; i < _sinks.Length; i++)
                {
                    try { (_sinks[i] as IFlushableLogSink)?.Flush(); }
                    catch { }

                    if (_disposeSinks)
                    {
                        try { (_sinks[i] as IDisposable)?.Dispose(); }
                        catch { }
                    }
                }
            }
        }
    }
}

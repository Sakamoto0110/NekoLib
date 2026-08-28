using NekoLib.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Forwards diagnostics <see cref="LogEntry"/> values to the watchdog over its
    /// control pipe.
    ///
    /// Entries are queued and flushed in batches on a background thread, so the
    /// calling application never pays a pipe round-trip per log call. One pipe
    /// connection is amortized over a whole batch instead of one per entry.
    ///
    /// The target pipe name is resolved from the current process path by
    /// <see cref="WatchdogController"/>; there is no pipe-name parameter.
    /// </summary>
    public sealed class WatchdogPipeLogSink : ILogSink, IDisposable
    {
        private readonly BlockingCollection<LogEntry> _queue;
        private readonly Thread _flushThread;
        private readonly int _maxBatch;
        private readonly int _flushIntervalMs;

        private long _dropped;
        private volatile bool _disposed;

        /// <summary>Creates a sink with a 1,000-entry queue, 250 ms coalescing window, and 64-entry batches.</summary>
        public WatchdogPipeLogSink()
            : this(maxQueued: 1000, flushIntervalMs: 250, maxBatch: 64)
        {
        }

        /// <summary>
        /// Creates a best-effort asynchronous forwarding sink. Invalid bounds are
        /// normalized to their minimums rather than rejected.
        /// </summary>
        /// <param name="maxQueued">Bounded in-memory queue size; values below 1 become 1 and entries are dropped when full.</param>
        /// <param name="flushIntervalMs">Coalescing window between batches so bursts amortize into one connect.</param>
        /// <param name="maxBatch">Maximum entries sent per pipe round-trip; values below 1 become 1.</param>
        public WatchdogPipeLogSink(int maxQueued = 1000, int flushIntervalMs = 250, int maxBatch = 64)
        {
            if (maxQueued < 1) maxQueued = 1;
            if (flushIntervalMs < 0) flushIntervalMs = 0;
            if (maxBatch < 1) maxBatch = 1;

            _flushIntervalMs = flushIntervalMs;
            _maxBatch = maxBatch;
            _queue = new BlockingCollection<LogEntry>(maxQueued);

            _flushThread = new Thread(FlushLoop)
            {
                IsBackground = true,
                Name = "WDG-LogFlush"
            };
            _flushThread.Start();
        }

        /// <summary>Total entries dropped because the queue was full.</summary>
        public long DroppedCount => Interlocked.Read(ref _dropped);

        /// <summary>
        /// Attempts to enqueue one entry without waiting for pipe I/O. Null entries
        /// and calls after disposal are ignored; a full queue increments
        /// <see cref="DroppedCount"/>.
        /// </summary>
        /// <param name="entry">Entry to forward.</param>
        public void Write(LogEntry entry)
        {
            if (entry == null || _disposed)
                return;

            // Never block the calling app: drop on a full queue and count it.
            if (!_queue.TryAdd(entry))
                Interlocked.Increment(ref _dropped);
        }

        private void FlushLoop()
        {
            var batch = new List<LogEntry>(_maxBatch);

            while (!_disposed)
            {
                LogEntry? first;
                try
                {
                    // Block until an entry arrives; returns false when the queue is
                    // completed (Dispose) and drained.
                    if (!_queue.TryTake(out first, Timeout.Infinite))
                        break;
                }
                catch (ObjectDisposedException) { break; }
                catch (InvalidOperationException) { break; }

                if (first == null)
                    continue;

                batch.Clear();
                batch.Add(first);

                // Greedily drain whatever is already queued, up to the batch cap.
                try
                {
                    while (batch.Count < _maxBatch && _queue.TryTake(out var next))
                        batch.Add(next);
                }
                catch (ObjectDisposedException) { }

                Flush(batch);

                // Dispose may have flipped while we were flushing (a flush can stall
                // up to the connect timeout). Drop the rest and exit fast — logging
                // is best-effort and must not delay shutdown.
                if (_disposed)
                    break;

                // Brief coalescing window so log bursts amortize into one connect.
                if (_flushIntervalMs > 0)
                {
                    try { Thread.Sleep(_flushIntervalMs); }
                    catch { }
                }
            }
        }

        private static void Flush(List<LogEntry> batch)
        {
            if (batch.Count == 0)
                return;

            try { WatchdogController.NotifyLogBatch(batch); }
            catch { }
        }

        /// <summary>
        /// Stops accepting entries and waits at most three seconds for the current
        /// background flush thread. Disposal is idempotent and does not guarantee
        /// delivery of queued entries.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _queue.CompleteAdding(); } catch { }

            // Join before disposing the queue: only dispose if the flush thread has
            // actually exited, otherwise it could touch a disposed collection on a
            // background thread (ObjectDisposedException is fatal there).
            bool joined = true;
            try { joined = _flushThread == null || _flushThread.Join(3000); } catch { }

            if (joined)
            {
                try { _queue.Dispose(); } catch { }
            }
        }
    }
}

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
            _sinks = sinks ?? Array.Empty<ILogSink>();
            _recentEntries = new Queue<LogEntry>(_recentEntryCapacity);
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
                    try { _sinks[i]?.Write(entry); }
                    catch { /* logging must never break feature behavior */ }
                }
            }
        }

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
                for (int i = 0; i < _sinks.Length; i++)
                {
                    var flushable = _sinks[i] as IFlushableLogSink;
                    if (flushable == null)
                        continue;

                    var remaining = timeout - watch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    try
                    {
                        var task = Task.Run((Action)flushable.Flush);
                        if (!task.Wait(remaining))
                            return false;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                Monitor.Exit(_gate);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_gate)
            {
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

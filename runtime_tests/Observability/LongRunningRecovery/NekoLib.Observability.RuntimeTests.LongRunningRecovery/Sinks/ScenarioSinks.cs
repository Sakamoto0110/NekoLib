#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks
{
    /// <summary>
    /// The message shape every scenario write uses, so a sink can verify
    /// ordering without keeping the entries.
    /// <para/>
    /// A sink that retained what it received would be a leak in a 16-hour soak,
    /// and a leak in the measuring instrument is worse than no measurement. The
    /// writer and sequence are encoded in the message and checked incrementally,
    /// which costs a fixed number of bytes however long the run is.
    /// </summary>
    internal static class TracedMessage
    {
        public static string Compose(int writer, long sequence) =>
            "w" + writer.ToString(CultureInfo.InvariantCulture) +
            "#" + sequence.ToString(CultureInfo.InvariantCulture) +
            " observability scenario traffic";

        /// <summary>Reads the writer and sequence back, or false if this is not scenario traffic.</summary>
        public static bool TryRead(string message, out int writer, out long sequence)
        {
            writer = 0;
            sequence = 0;

            if (string.IsNullOrEmpty(message) || message[0] != 'w') return false;

            int hash = message.IndexOf('#');
            if (hash < 2) return false;

            int space = message.IndexOf(' ', hash);
            if (space < 0) space = message.Length;

            return int.TryParse(
                       message.Substring(1, hash - 1),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out writer)
                   && long.TryParse(
                       message.Substring(hash + 1, space - hash - 1),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out sequence);
        }
    }

    /// <summary>
    /// A healthy sink that counts what it receives and verifies order without
    /// retaining it.
    /// <para/>
    /// <see cref="OrderFingerprint"/> is the part that makes the Logger's
    /// ordering contract checkable: it is a rolling hash of everything this sink
    /// saw, in the order it saw it, so two sinks agreeing on the fingerprint saw
    /// one identical delivery order. Comparing retained lists would prove the
    /// same thing and cost memory proportional to the run.
    /// </summary>
    internal sealed class CountingLogSink : ILogSink, IFlushableLogSink, IDisposable
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly object _gate = new object();
        private readonly Dictionary<int, long> _lastPerWriter = new Dictionary<int, long>();
        private readonly Dictionary<LogLevel, long> _byLevel = new Dictionary<LogLevel, long>();

        private ulong _fingerprint = FnvOffset;
        private DateTime _lastTimestamp = DateTime.MinValue;

        public CountingLogSink(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public long Received;
        public long PerWriterInversions;

        /// <summary>
        /// Entries whose timestamp is older than the previous entry's.
        /// <para/>
        /// This is an observation, not a pass criterion. <c>Logger</c> stamps an
        /// entry before taking its dispatch lock, so under concurrent writers
        /// the delivery order and the timestamp order are allowed to disagree.
        /// The documented contract is the delivery order, and that is what is
        /// asserted; this counter records the rest truthfully.
        /// </summary>
        public long TimestampInversions;

        public int FlushCount;
        public int DisposeCount;

        public ulong OrderFingerprint { get { lock (_gate) return _fingerprint; } }

        /// <summary>Entries accepted at exactly this level, for the filtering check.</summary>
        public long CountAt(LogLevel level)
        {
            lock (_gate)
            {
                long count;
                return _byLevel.TryGetValue(level, out count) ? count : 0;
            }
        }

        public void Write(LogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            lock (_gate)
            {
                Received++;

                long atLevel;
                _byLevel.TryGetValue(entry.Level, out atLevel);
                _byLevel[entry.Level] = atLevel + 1;

                if (entry.TimestampUtc < _lastTimestamp) TimestampInversions++;
                _lastTimestamp = entry.TimestampUtc;

                foreach (char c in entry.Message)
                {
                    _fingerprint ^= c;
                    _fingerprint *= FnvPrime;
                }

                int writer;
                long sequence;
                if (TracedMessage.TryRead(entry.Message, out writer, out sequence))
                {
                    long previous;
                    if (_lastPerWriter.TryGetValue(writer, out previous) && sequence <= previous)
                        PerWriterInversions++;

                    _lastPerWriter[writer] = sequence;
                }
            }
        }

        public void Flush() => Interlocked.Increment(ref FlushCount);

        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    /// <summary>
    /// A sink that throws on a seeded schedule, and can be armed and disarmed so
    /// a scheduled fault has something to act on.
    /// <para/>
    /// Which writes fail is a function of the seed alone, so a run that observes
    /// an isolation failure can be repeated exactly. The failure lives here
    /// rather than in <c>NekoLib.Logging</c> because the suite forbids a
    /// fault-injection API on a library.
    /// </summary>
    internal sealed class ScheduledFailingLogSink : ILogSink, IFlushableLogSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly DeterministicRandom _random;
        private readonly double _failureRate;
        private int _armed;

        public ScheduledFailingLogSink(int seed, double failureRate)
        {
            _random = new DeterministicRandom(unchecked((ulong)seed) ^ 0xD1B54A32D192ED03UL);
            _failureRate = failureRate;
        }

        public long Received;
        public long Thrown;
        public int FlushCount;
        public int DisposeCount;

        /// <summary>When false the sink behaves normally, which is what recovery looks like.</summary>
        public bool Armed
        {
            get { return Volatile.Read(ref _armed) != 0; }
            set { Volatile.Write(ref _armed, value ? 1 : 0); }
        }

        /// <summary>Throws from <see cref="Flush"/> as well, for the shutdown-after-failure check.</summary>
        public bool ThrowOnFlush;

        public void Write(LogEntry entry)
        {
            bool throwNow;
            lock (_gate)
            {
                Received++;
                throwNow = Armed && _random.NextDouble() < _failureRate;
                if (throwNow) Thrown++;
            }

            if (throwNow)
                throw new InvalidOperationException("scenario sink failure #" + Thrown);
        }

        public void Flush()
        {
            Interlocked.Increment(ref FlushCount);
            if (ThrowOnFlush) throw new InvalidOperationException("scenario sink flush failure");
        }

        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCount);
            if (ThrowOnFlush) throw new InvalidOperationException("scenario sink dispose failure");
        }
    }

    /// <summary>
    /// A sink whose <see cref="Flush"/> blocks until released, so
    /// <c>ILogFlusher.Flush(timeout)</c> can be proved bounded rather than
    /// merely successful.
    /// </summary>
    internal sealed class BlockingFlushLogSink : ILogSink, IFlushableLogSink, IDisposable
    {
        private readonly ManualResetEventSlim _release = new ManualResetEventSlim(true);

        public long Received;
        public int FlushCount;
        public int DisposeCount;

        public void Block() => _release.Reset();

        public void Release() => _release.Set();

        public void Write(LogEntry entry) => Interlocked.Increment(ref Received);

        public void Flush()
        {
            Interlocked.Increment(ref FlushCount);

            // Bounded even if nothing releases it, so a scenario defect cannot
            // hang the process it is supposed to be measuring.
            _release.Wait(TimeSpan.FromMinutes(2));
        }

        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCount);
            _release.Set();
            _release.Dispose();
        }
    }

    /// <summary>Counts completed operations, and can be made to throw.</summary>
    internal sealed class CountingTelemetrySink : ITelemetrySink, IDisposable
    {
        private readonly object _gate = new object();

        public long Received;
        public long Thrown;
        public int DisposeCount;
        public bool Armed;

        public void Write(TelemetryOperation operation)
        {
            lock (_gate)
            {
                Received++;

                if (Armed)
                {
                    Thrown++;
                    throw new InvalidOperationException("scenario telemetry sink failure #" + Thrown);
                }
            }
        }

        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }
}

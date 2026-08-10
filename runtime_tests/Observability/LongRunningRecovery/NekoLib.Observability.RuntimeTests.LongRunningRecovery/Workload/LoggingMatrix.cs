#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Logging;
using NekoLib.Logging;
using NekoLib.Logging.Sinks;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// Logging: ordering, filtering, bounded snapshots, sink-failure isolation,
    /// rotation and retention, bounded flush, and disposal.
    /// <para/>
    /// The normal-rate phase and the forced-rotation phase are reported as
    /// separate checks on purpose. A 4 KiB maximum file size proves the rotation
    /// and eviction mechanics in seconds; it says nothing about retention
    /// capacity at a production file size, and the two must not be read as one
    /// result.
    /// </summary>
    internal static class LoggingMatrix
    {
        private const string Phase = Phases.Logging;

        public static async Task RunAsync(PhaseContext context)
        {
            await SustainedOrderedWrites(context).ConfigureAwait(false);
            await ConcurrentWriterOrdering(context).ConfigureAwait(false);
            await MinimumLevelFiltering(context).ConfigureAwait(false);
            await BoundedRecentSnapshot(context).ConfigureAwait(false);
            await SnapshotBoundariesDuringWrites(context).ConfigureAwait(false);
            await SinkFailureIsolation(context).ConfigureAwait(false);
            await RotationMechanics(context).ConfigureAwait(false);
            await RetentionEviction(context).ConfigureAwait(false);
            await FlushDuringActivity(context).ConfigureAwait(false);
            await FlushIsBounded(context).ConfigureAwait(false);
            await DisposeSinksBothWays(context).ConfigureAwait(false);
            await ShutdownAfterSinkFailure(context).ConfigureAwait(false);
            await RepeatedLifecycle(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Sustained single-writer logging at the configured rate.
        /// <para/>
        /// It builds its own logger rather than using the shared workspace one,
        /// because in the soak and the sustained smoke the shared logger also
        /// carries background traffic, and an exact-count assertion against a
        /// shared instance is a scenario defect waiting to happen. The first
        /// sustained smoke proved it: this check expected 4000 and saw 4130.
        /// The shared logger is exercised by the steady traffic and by the
        /// scheduled faults; the assertions live on instances the check owns.
        /// </summary>
        private static Task SustainedOrderedWrites(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "sustained-ordered-writes",
                "sustained single-writer logging preserves delivery order across every healthy sink",
                async check =>
                {
                    CountingLogSink healthy = new CountingLogSink("sustained-healthy");
                    CountingLogSink secondary = new CountingLogSink("sustained-secondary");

                    using (Logger logger = new Logger(
                        new LoggerOptions
                        {
                            MinimumLevel = LogLevel.Debug,
                            RecentEntryCapacity = ObservabilityWorkspace.LogRecentCapacity,
                            DisposeSinks = false
                        },
                        healthy,
                        secondary))
                    {
                        const int writes = 4000;
                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

                        int delayEvery = context.LogRate > 0 ? Math.Max(1, context.LogRate / 20) : 0;

                        for (int i = 1; i <= writes; i++)
                        {
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));
                            context.Samples.LogEntriesWritten.Increment();
                            context.Counters.Success();

                            // Only throttled when a rate was asked for; the
                            // default is unthrottled, which is what the smoke
                            // uses to reach capacity quickly.
                            if (delayEvery > 0 && i % delayEvery == 0)
                                await Task.Delay(50, context.Ct).ConfigureAwait(false);
                        }

                        clock.Stop();

                        check.Equal(writes, Interlocked.Read(ref healthy.Received), "entries the healthy sink received");
                        check.Equal(writes, Interlocked.Read(ref secondary.Received), "entries the secondary sink received");
                        check.Equal(0, healthy.PerWriterInversions, "out-of-order deliveries");
                        check.Equal(0, secondary.PerWriterInversions, "out-of-order deliveries in the second sink");

                        check.That(healthy.OrderFingerprint == secondary.OrderFingerprint,
                            "the two sinks disagree on delivery order");

                        double achieved = clock.Elapsed.TotalSeconds > 0
                            ? writes / clock.Elapsed.TotalSeconds
                            : writes;

                        check.Note("a single writer produced " + writes + " entries with no inversion in either sink, " +
                                   "at " + achieved.ToString("F0", CultureInfo.InvariantCulture) + " entries/s" +
                                   (context.LogRate > 0
                                       ? " against a requested " + context.LogRate + "/s"
                                       : " (unthrottled)"));
                    }
                });
        }

        /// <summary>
        /// The Logger's documented contract is that accepted entries are
        /// dispatched inline, in registration order, under one lock. What that
        /// gives an application under concurrency is: every sink sees every
        /// entry, exactly once, in one identical order, and each writer's own
        /// entries stay in that writer's order. That is what is asserted.
        /// </summary>
        private static Task ConcurrentWriterOrdering(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "concurrent-writer-ordering",
                "concurrent writers see one identical delivery order in every sink, and per-writer order is preserved",
                async check =>
                {
                    const int writers = 8;
                    const int perWriter = 1500;

                    CountingLogSink first = new CountingLogSink("concurrent-a");
                    CountingLogSink second = new CountingLogSink("concurrent-b");

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 64 },
                        first,
                        second))
                    {
                        Task[] running = new Task[writers];
                        for (int w = 0; w < writers; w++)
                        {
                            int id = w;
                            running[w] = Task.Run(() =>
                            {
                                for (int i = 1; i <= perWriter; i++)
                                    logger.Log(LogLevel.Info, TracedMessage.Compose(id, i));
                            }, context.Ct);
                        }

                        await Task.WhenAll(running).ConfigureAwait(false);

                        long expected = writers * perWriter;
                        check.Equal(expected, Interlocked.Read(ref first.Received), "entries the first sink received");
                        check.Equal(expected, Interlocked.Read(ref second.Received), "entries the second sink received");

                        check.That(first.OrderFingerprint == second.OrderFingerprint,
                            "the two sinks disagree on delivery order: " +
                            first.OrderFingerprint.ToString("x16", CultureInfo.InvariantCulture) + " vs " +
                            second.OrderFingerprint.ToString("x16", CultureInfo.InvariantCulture));

                        check.Equal(0, first.PerWriterInversions, "per-writer inversions in the first sink");
                        check.Equal(0, second.PerWriterInversions, "per-writer inversions in the second sink");

                        // An observation, deliberately not an assertion. Logger
                        // stamps an entry before taking its dispatch lock, so
                        // under concurrency the timestamp order and the delivery
                        // order are allowed to disagree. Recording the number is
                        // how the run stays honest about it without inventing a
                        // contract the implementation does not claim.
                        check.Note(writers + " writers x " + perWriter + " entries delivered in one identical order " +
                                   "(fingerprint " + first.OrderFingerprint.ToString("x16", CultureInfo.InvariantCulture) + ")");
                        check.Note("timestamp inversions observed: " + first.TimestampInversions +
                                   " of " + expected + ". LogEntry is stamped before the dispatch lock is taken, " +
                                   "so TimestampUtc is not a delivery-order key under concurrent writers. " +
                                   "This is an observation about the current implementation, not a failed claim.");

                        context.Counters.Success();
                    }
                });
        }

        private static Task MinimumLevelFiltering(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "minimum-level-filtering",
                "entries below the configured minimum never reach a sink and never enter the snapshot",
                check =>
                {
                    CountingLogSink sink = new CountingLogSink("filtered");

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Warn, RecentEntryCapacity = 128 },
                        sink))
                    {
                        LogLevel[] levels =
                        {
                            LogLevel.Trace, LogLevel.Debug, LogLevel.Info,
                            LogLevel.Warn, LogLevel.Error, LogLevel.Fatal
                        };

                        const int each = 25;
                        foreach (LogLevel level in levels)
                        {
                            for (int i = 0; i < each; i++)
                                logger.Log(level, "level probe " + level);
                        }

                        check.Equal(0, sink.CountAt(LogLevel.Trace), "Trace entries accepted below the minimum");
                        check.Equal(0, sink.CountAt(LogLevel.Debug), "Debug entries accepted below the minimum");
                        check.Equal(0, sink.CountAt(LogLevel.Info), "Info entries accepted below the minimum");
                        check.Equal(each, sink.CountAt(LogLevel.Warn), "Warn entries accepted at the minimum");
                        check.Equal(each, sink.CountAt(LogLevel.Error), "Error entries accepted above the minimum");
                        check.Equal(each, sink.CountAt(LogLevel.Fatal), "Fatal entries accepted above the minimum");
                        check.Equal(each * 3, Interlocked.Read(ref sink.Received), "total entries accepted");

                        check.Equal(each * 3, logger.GetRecentEntries(int.MaxValue).Count,
                            "entries the snapshot retained");

                        context.Counters.Success();
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task BoundedRecentSnapshot(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "bounded-recent-snapshot",
                "the recent snapshot stays at capacity after several multiples of it and keeps the newest entries in order",
                check =>
                {
                    const int capacity = 200;
                    const int multiples = 7;

                    CountingLogSink sink = new CountingLogSink("bounded");
                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = capacity },
                        sink))
                    {
                        int total = capacity * multiples;
                        for (int i = 1; i <= total; i++)
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));

                        IReadOnlyList<LogEntry> snapshot = logger.GetRecentEntries(int.MaxValue);
                        check.Equal(capacity, snapshot.Count,
                            "snapshot size after writing " + multiples + "x capacity");

                        // The retained window must be the newest one, oldest
                        // first: dropping from the wrong end is the failure this
                        // check exists to catch.
                        long firstSequence;
                        long lastSequence;
                        int writer;
                        check.That(TracedMessage.TryRead(snapshot[0].Message, out writer, out firstSequence),
                            "the first retained entry is not scenario traffic");
                        check.That(TracedMessage.TryRead(snapshot[snapshot.Count - 1].Message, out writer, out lastSequence),
                            "the last retained entry is not scenario traffic");

                        check.Equal(total, lastSequence, "newest retained sequence");
                        check.Equal(total - capacity + 1, firstSequence, "oldest retained sequence");

                        for (int i = 1; i < snapshot.Count; i++)
                        {
                            long previous;
                            long current;
                            TracedMessage.TryRead(snapshot[i - 1].Message, out writer, out previous);
                            TracedMessage.TryRead(snapshot[i].Message, out writer, out current);
                            check.That(current == previous + 1,
                                "the snapshot is not contiguous at index " + i + ": " + previous + " then " + current);
                        }

                        check.Note(total + " entries written against a capacity of " + capacity +
                                   "; retained window is [" + firstSequence + ".." + lastSequence + "]");

                        check.Equal(total, Interlocked.Read(ref sink.Received), "entries the sink received");
                        context.Counters.Success();
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task SnapshotBoundariesDuringWrites(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "snapshot-max-entries-during-writes",
                "maxEntries boundaries hold and snapshots stay ordered while writers are active",
                async check =>
                {
                    const int capacity = 128;
                    CountingLogSink sink = new CountingLogSink("snapshot");

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = capacity },
                        sink))
                    {
                        using (CancellationTokenSource stop = new CancellationTokenSource())
                        {
                            Task writer = Task.Run(() =>
                            {
                                long i = 0;
                                while (!stop.IsCancellationRequested)
                                    logger.Log(LogLevel.Info, TracedMessage.Compose(0, ++i));
                            });

                            int[] requests = { 0, 1, capacity / 2, capacity - 1, capacity, capacity + 1, int.MaxValue };
                            int reads = 0;

                            for (int round = 0; round < 40; round++)
                            {
                                foreach (int request in requests)
                                {
                                    IReadOnlyList<LogEntry> snapshot = logger.GetRecentEntries(request);
                                    reads++;

                                    int expectedMax = request <= 0 ? 0 : Math.Min(request, capacity);
                                    check.That(snapshot.Count <= expectedMax,
                                        "GetRecentEntries(" + request + ") returned " + snapshot.Count +
                                        ", above the bound of " + expectedMax);

                                    long previous = -1;
                                    for (int i = 0; i < snapshot.Count; i++)
                                    {
                                        int who;
                                        long sequence;
                                        if (!TracedMessage.TryRead(snapshot[i].Message, out who, out sequence)) continue;

                                        check.That(sequence > previous,
                                            "a snapshot taken during writes was out of order at index " + i);
                                        previous = sequence;
                                    }
                                }

                                await Task.Delay(5, context.Ct).ConfigureAwait(false);
                            }

                            stop.Cancel();
                            await writer.ConfigureAwait(false);

                            check.Equal(0, logger.GetRecentEntries(0).Count, "GetRecentEntries(0)");
                            check.Equal(0, logger.GetRecentEntries(-1).Count, "GetRecentEntries(-1)");
                            check.Equal(capacity, logger.GetRecentEntries(int.MaxValue).Count,
                                "a settled snapshot at capacity");

                            check.Note(reads + " snapshots taken across seven maxEntries boundaries while a writer ran; " +
                                       "the sink received " + Interlocked.Read(ref sink.Received) + " entries");

                            context.Counters.Success();
                        }
                    }
                });
        }

        private static Task SinkFailureIsolation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "sink-failure-isolation",
                "a sink throwing on a seeded schedule never stops a healthy sink registered after it",
                check =>
                {
                    const int writes = 3000;

                    ScheduledFailingLogSink failing = new ScheduledFailingLogSink(context.Seed, 0.3) { Armed = true };
                    CountingLogSink after = new CountingLogSink("after-the-failure");
                    CountingLogSink before = new CountingLogSink("before-the-failure");

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 64 },
                        before,
                        failing,
                        after))
                    {
                        for (int i = 1; i <= writes; i++)
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));

                        check.Equal(writes, Interlocked.Read(ref before.Received),
                            "entries the sink before the failing one received");
                        check.Equal(writes, Interlocked.Read(ref after.Received),
                            "entries the sink after the failing one received");
                        check.Equal(writes, Interlocked.Read(ref failing.Received),
                            "entries the failing sink was offered");

                        check.That(failing.Thrown > 0,
                            "the seeded failure schedule never fired, so nothing was isolated");
                        check.Equal(0, after.PerWriterInversions, "inversions after an isolated failure");

                        check.Note(failing.Thrown + " of " + writes + " writes threw inside the failing sink; " +
                                   "both healthy sinks still received all " + writes);
                        check.Note("the failure rate is a function of the seed, so this count reproduces exactly");

                        context.Counters.ExpectedFailure();
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task RotationMechanics(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "rolling-file-rotation",
                "a deliberately small maximum size rotates, and the live file stays under the limit",
                check =>
                {
                    string path = context.UniqueWorkPath("rotation.log");
                    const long maximum = 4096;

                    RollingFileLogSink sink = new RollingFileLogSink(new RollingFileLogSinkOptions
                    {
                        FilePath = path,
                        MaximumFileBytes = maximum,
                        RetainedFileCount = 4
                    });

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 16, DisposeSinks = false },
                        sink))
                    {
                        for (int i = 1; i <= 400; i++)
                        {
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));
                            context.Samples.LogEntriesWritten.Increment();
                        }
                    }

                    check.That(File.Exists(path), "the live log file does not exist after writing");
                    check.That(File.Exists(path + ".1"), "no rotation happened at a 4 KiB maximum");

                    long liveLength = new FileInfo(path).Length;
                    check.That(liveLength <= maximum,
                        "the live file is " + liveLength + " bytes, above the " + maximum + "-byte maximum");

                    int rolled = 0;
                    for (int i = 1; i <= 8; i++)
                        if (File.Exists(path + "." + i)) rolled++;

                    context.Samples.LogFilesRolled.Set(rolled);
                    check.Note("400 entries at a 4 KiB maximum produced " + rolled +
                               " rolled file(s); the live file holds " + liveLength + " bytes");
                    check.Note("this proves rotation mechanics at a tiny size and says nothing about " +
                               "retention capacity at a production file size");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task RetentionEviction(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "retained-file-count",
                "exactly the configured number of rolled files survives, and eviction removes the oldest",
                check =>
                {
                    string path = context.UniqueWorkPath("retention.log");
                    const int retained = 3;

                    RollingFileLogSink sink = new RollingFileLogSink(new RollingFileLogSinkOptions
                    {
                        FilePath = path,
                        MaximumFileBytes = 2048,
                        RetainedFileCount = retained
                    });

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 16, DisposeSinks = false },
                        sink))
                    {
                        // Far more than enough rotations to push the earliest
                        // generations past the retention window.
                        for (int i = 1; i <= 2000; i++)
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));
                    }

                    for (int i = 1; i <= retained; i++)
                        check.That(File.Exists(path + "." + i), "expected rolled file " + path + "." + i + " to exist");

                    check.That(!File.Exists(path + "." + (retained + 1)),
                        "a rolled file survived beyond the retained count: " + path + "." + (retained + 1));

                    // Eviction must drop the oldest generation. The rolled files
                    // are ordered newest-first, so .1 must hold later sequences
                    // than .2, and so on.
                    long previousFirst = long.MaxValue;
                    for (int i = 1; i <= retained; i++)
                    {
                        long firstSequence = FirstSequenceIn(path + "." + i);
                        check.That(firstSequence < previousFirst,
                            "rolled file ." + i + " starts at sequence " + firstSequence +
                            ", which is not older than the previous file's " + previousFirst);
                        previousFirst = firstSequence;
                    }

                    long liveFirst = FirstSequenceIn(path);
                    check.That(liveFirst > previousFirst,
                        "the live file starts at " + liveFirst + ", which is not newer than the newest rolled file");

                    check.Note("with RetainedFileCount=" + retained + ", exactly " + retained +
                               " rolled files survived and each is strictly older than the one before it");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task FlushDuringActivity(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "flush-during-activity-and-after-incident",
                "flush succeeds during ordinary writes and immediately after an injected sink failure",
                async check =>
                {
                    string path = context.UniqueWorkPath("flush.log");
                    ScheduledFailingLogSink failing = new ScheduledFailingLogSink(context.Seed, 0.5);
                    CountingLogSink healthy = new CountingLogSink("flush-healthy");

                    RollingFileLogSink file = new RollingFileLogSink(new RollingFileLogSinkOptions
                    {
                        FilePath = path,
                        MaximumFileBytes = 64 * 1024,
                        RetainedFileCount = 2
                    });

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 32, DisposeSinks = false },
                        failing,
                        healthy,
                        file))
                    {
                        using (CancellationTokenSource stop = new CancellationTokenSource())
                        {
                            Task writer = Task.Run(() =>
                            {
                                long i = 0;
                                while (!stop.IsCancellationRequested)
                                {
                                    logger.Log(LogLevel.Info, TracedMessage.Compose(0, ++i));
                                    Thread.Sleep(1);
                                }
                            });

                            for (int i = 0; i < 10; i++)
                            {
                                check.That(logger.Flush(TimeSpan.FromSeconds(10)),
                                    "a flush during ordinary activity returned false on attempt " + (i + 1));
                            }

                            // The incident: the sink starts failing, and a flush
                            // taken immediately afterwards must still complete.
                            failing.Armed = true;
                            await Task.Delay(150, context.Ct).ConfigureAwait(false);

                            check.That(logger.Flush(TimeSpan.FromSeconds(10)),
                                "the flush immediately after the injected sink failure returned false");

                            failing.Armed = false;
                            stop.Cancel();
                            await writer.ConfigureAwait(false);

                            check.That(failing.Thrown > 0, "the incident never actually produced a failure");
                            check.Note("10 flushes during writes plus one immediately after " + failing.Thrown +
                                       " sink failures, all returning true");

                            context.Counters.ExpectedFailure();
                        }
                    }

                    check.That(File.Exists(path), "the flushed file does not exist");
                });
        }

        private static Task FlushIsBounded(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "flush-timeout-is-bounded",
                "Flush(timeout) returns false inside its bound when a sink blocks, and true once it is released",
                async check =>
                {
                    BlockingFlushLogSink blocking = new BlockingFlushLogSink();
                    CountingLogSink healthy = new CountingLogSink("bounded-flush");

                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, RecentEntryCapacity = 16, DisposeSinks = false },
                        healthy,
                        blocking))
                    {
                        logger.Log(LogLevel.Info, TracedMessage.Compose(0, 1));

                        check.That(logger.Flush(TimeSpan.FromSeconds(5)),
                            "the baseline flush failed before anything was blocked");

                        blocking.Block();

                        TimeSpan budget = TimeSpan.FromMilliseconds(500);
                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                        bool flushed = logger.Flush(budget);
                        clock.Stop();

                        check.That(!flushed, "a flush against a blocked sink reported success");

                        // The bound is what matters. A generous ceiling keeps
                        // this from becoming a latency assertion on a busy host.
                        check.That(clock.Elapsed < TimeSpan.FromSeconds(5),
                            "the bounded flush took " + clock.ElapsedMilliseconds +
                            "ms against a 500ms budget, which is not bounded");

                        check.Note("Flush(500ms) against a blocked sink returned false after " +
                                   clock.ElapsedMilliseconds + "ms");

                        blocking.Release();
                        await Task.Delay(50, context.Ct).ConfigureAwait(false);

                        check.That(logger.Flush(TimeSpan.FromSeconds(10)),
                            "the flush did not recover after the sink was released");

                        context.Counters.ExpectedFailure();
                    }

                    blocking.Dispose();
                });
        }

        private static Task DisposeSinksBothWays(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "dispose-sinks-enabled-and-disabled",
                "DisposeSinks decides whether the logger disposes its sinks, and flushes them either way",
                check =>
                {
                    CountingLogSink owned = new CountingLogSink("owned");
                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                        owned))
                    {
                        logger.Log(LogLevel.Info, "owned sink probe");
                    }

                    check.Equal(1, owned.DisposeCount, "disposals with DisposeSinks enabled");
                    check.That(owned.FlushCount >= 1, "the owned sink was never flushed on disposal");

                    CountingLogSink borrowed = new CountingLogSink("borrowed");
                    using (Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = false },
                        borrowed))
                    {
                        logger.Log(LogLevel.Info, "borrowed sink probe");
                    }

                    check.Equal(0, borrowed.DisposeCount, "disposals with DisposeSinks disabled");
                    check.That(borrowed.FlushCount >= 1, "the borrowed sink was never flushed on disposal");

                    check.Note("a borrowed sink is flushed but not disposed, which is what lets two loggers share one file sink");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task ShutdownAfterSinkFailure(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "shutdown-after-sink-failure",
                "disposing a logger whose sink throws from Flush and Dispose does not throw, and further writes are inert",
                check =>
                {
                    ScheduledFailingLogSink failing = new ScheduledFailingLogSink(context.Seed, 1.0)
                    {
                        Armed = true,
                        ThrowOnFlush = true
                    };

                    CountingLogSink healthy = new CountingLogSink("survivor");
                    Logger logger = new Logger(
                        new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                        failing,
                        healthy);

                    logger.Log(LogLevel.Error, "an entry that the failing sink will reject");
                    check.Equal(1, Interlocked.Read(ref healthy.Received), "entries the healthy sink received");

                    Exception? disposal = PhaseContext.Capture(logger.Dispose);
                    check.That(disposal == null,
                        "disposing a logger with a throwing sink threw " +
                        (disposal == null ? string.Empty : disposal.GetType().Name));

                    Exception? second = PhaseContext.Capture(logger.Dispose);
                    check.That(second == null, "the second disposal threw");

                    long afterDispose = Interlocked.Read(ref healthy.Received);
                    logger.Log(LogLevel.Error, "an entry written after disposal");
                    check.Equal(afterDispose, Interlocked.Read(ref healthy.Received),
                        "entries accepted after disposal");

                    check.Note("the sink threw from Write, Flush and Dispose; the logger absorbed all three and " +
                               "went inert rather than failing the caller");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task RepeatedLifecycle(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "repeated-create-use-dispose",
                "repeated logger lifecycles leave complete, readable files and no process-held handle",
                check =>
                {
                    const int cycles = 40;
                    string path = context.UniqueWorkPath("lifecycle.log");
                    long written = 0;

                    for (int cycle = 1; cycle <= cycles; cycle++)
                    {
                        RollingFileLogSink file = new RollingFileLogSink(new RollingFileLogSinkOptions
                        {
                            FilePath = path,
                            MaximumFileBytes = 4 * 1024 * 1024,
                            RetainedFileCount = 2
                        });

                        CountingLogSink sink = new CountingLogSink("cycle-" + cycle);
                        using (Logger logger = new Logger(
                            new LoggerOptions { MinimumLevel = LogLevel.Trace, DisposeSinks = true },
                            sink,
                            file))
                        {
                            for (int i = 1; i <= 25; i++)
                            {
                                logger.Log(LogLevel.Info, TracedMessage.Compose(cycle, i));
                                written++;
                            }
                        }

                        check.Equal(25, Interlocked.Read(ref sink.Received), "entries in cycle " + cycle);
                    }

                    // Opening the file with no sharing is the actual proof that
                    // no handle survived: on Windows this fails outright if any
                    // sink is still holding it.
                    long lines = 0;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        while (reader.ReadLine() != null) lines++;
                    }

                    check.Equal(written, lines,
                        "lines in the file after " + cycles + " create/use/dispose cycles");

                    check.Note(cycles + " lifecycles wrote " + written +
                               " entries; the file opened with FileShare.None afterwards, so no handle survived");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static long FirstSequenceIn(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int marker = line.IndexOf(" w", StringComparison.Ordinal);
                    if (marker < 0) continue;

                    int writer;
                    long sequence;
                    if (TracedMessage.TryRead(line.Substring(marker + 1), out writer, out sequence))
                        return sequence;
                }
            }

            return -1;
        }
    }
}

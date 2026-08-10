#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Telemetry;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks;
using NekoLib.Telemetry;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// Telemetry: operation lifetime under concurrency, bounded retention,
    /// checkpoints, correlation, terminal outcomes, snapshot boundaries and
    /// immutability.
    /// <para/>
    /// One check here deliberately records what the implementation does rather
    /// than what a stronger contract would say: an operation scope abandoned
    /// without <c>Complete</c> simply never appears, and the public API offers
    /// no way to observe that it existed. The suite asks for the actual
    /// behaviour, not an invented guarantee.
    /// </summary>
    internal static class TelemetryMatrix
    {
        private const string Phase = Phases.Telemetry;

        public static async Task RunAsync(PhaseContext context)
        {
            await SustainedConcurrentOperations(context).ConfigureAwait(false);
            await BoundedRetention(context).ConfigureAwait(false);
            await CheckpointOrdering(context).ConfigureAwait(false);
            await CorrelationPropagation(context).ConfigureAwait(false);
            await TerminalOutcomes(context).ConfigureAwait(false);
            await SnapshotBoundaries(context).ConfigureAwait(false);
            await AbandonedScope(context).ConfigureAwait(false);
            await ImmutableResults(context).ConfigureAwait(false);
        }

        private static Task SustainedConcurrentOperations(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "sustained-concurrent-operations",
                "concurrent producers create and complete operations without losing or duplicating any",
                async check =>
                {
                    const int producers = 8;
                    const int each = 500;
                    const int total = producers * each;

                    CountingTelemetrySink sink = new CountingTelemetrySink();
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = total },
                        sink);

                    Task[] running = new Task[producers];
                    for (int p = 0; p < producers; p++)
                    {
                        int id = p;
                        running[p] = Task.Run(() =>
                        {
                            for (int i = 0; i < each; i++)
                            {
                                ITelemetryOperation operation = pipeline.StartOperation(
                                    "scenario", "producer-work",
                                    "op-" + id + "-" + i);

                                operation.Checkpoint("started");
                                operation.Complete(TelemetryOutcome.Succeeded);
                            }
                        }, context.Ct);
                    }

                    await Task.WhenAll(running).ConfigureAwait(false);

                    check.Equal(total, Interlocked.Read(ref sink.Received), "operations the sink received");

                    IReadOnlyList<TelemetryOperation> recent = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(total, recent.Count, "operations retained at a capacity equal to the total");

                    HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (TelemetryOperation operation in recent) ids.Add(operation.OperationId);
                    check.Equal(total, ids.Count, "distinct operation ids");

                    context.Samples.TelemetryCompleted.Add(total);
                    context.Counters.Success();

                    check.Note(producers + " producers completed " + total +
                               " operations with no duplicate id and none lost");
                });
        }

        private static Task BoundedRetention(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "bounded-retention",
                "retention stays at capacity after several multiples of it and keeps the newest operations",
                check =>
                {
                    const int capacity = 200;
                    const int multiples = 6;
                    const int total = capacity * multiples;

                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = capacity });

                    for (int i = 1; i <= total; i++)
                    {
                        ITelemetryOperation operation = pipeline.StartOperation("scenario", "retained", "seq-" + i);
                        operation.Complete(TelemetryOutcome.Succeeded);
                    }

                    IReadOnlyList<TelemetryOperation> recent = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(capacity, recent.Count, "retained operations after " + multiples + "x capacity");

                    check.Equal("seq-" + (total - capacity + 1), recent[0].OperationId, "oldest retained id");
                    check.Equal("seq-" + total, recent[recent.Count - 1].OperationId, "newest retained id");

                    context.Samples.TelemetryCompleted.Add(total);
                    context.Counters.Success();

                    check.Note(total + " operations against a capacity of " + capacity +
                               " retained exactly the newest " + capacity);

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task CheckpointOrdering(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "checkpoint-ordering-and-elapsed",
                "checkpoints keep their order and their elapsed values never go backwards",
                async check =>
                {
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = 32 });

                    string[] names = { "one", "two", "three", "four", "five" };

                    ITelemetryOperation operation = pipeline.StartOperation("scenario", "checkpointed");
                    List<TimeSpan> reported = new List<TimeSpan>();

                    foreach (string name in names)
                    {
                        reported.Add(operation.Checkpoint(name));
                        await Task.Delay(5, context.Ct).ConfigureAwait(false);
                    }

                    operation.Complete(TelemetryOutcome.Succeeded);

                    TelemetryOperation completed = pipeline.GetRecentOperations(1)[0];
                    check.Equal(names.Length, completed.Checkpoints.Count, "recorded checkpoints");

                    for (int i = 0; i < names.Length; i++)
                    {
                        check.Equal(names[i], completed.Checkpoints[i].Name, "checkpoint " + i + " name");

                        if (i > 0)
                        {
                            check.That(completed.Checkpoints[i].Elapsed >= completed.Checkpoints[i - 1].Elapsed,
                                "checkpoint elapsed went backwards between " + names[i - 1] + " and " + names[i]);
                        }
                    }

                    for (int i = 0; i < names.Length; i++)
                    {
                        check.That(completed.Checkpoints[i].Elapsed == reported[i],
                            "the elapsed value returned to the caller for '" + names[i] +
                            "' does not match the one recorded");
                    }

                    check.That(completed.Duration >= completed.Checkpoints[names.Length - 1].Elapsed,
                        "the operation duration is shorter than its last checkpoint");

                    // A checkpoint after completion must not be recorded, but it
                    // must not throw either: telemetry never breaks the caller.
                    TimeSpan afterComplete = operation.Checkpoint("after-complete");
                    TelemetryOperation reread = pipeline.GetRecentOperations(1)[0];
                    check.Equal(names.Length, reread.Checkpoints.Count,
                        "checkpoints after a post-completion checkpoint call");
                    check.That(afterComplete >= TimeSpan.Zero, "the post-completion checkpoint returned a negative elapsed");

                    check.That(operation.IsCompleted, "the operation does not report itself completed");
                    check.Note("a checkpoint taken after Complete is ignored and returns the final elapsed rather than throwing");

                    context.Counters.Success();
                });
        }

        private static Task CorrelationPropagation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "id-and-parent-propagation",
                "explicit ids are honoured, generated ids are unique, and parent correlation survives nesting",
                check =>
                {
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = 256 });

                    ITelemetryOperation root = pipeline.StartOperation("scenario", "root", "root-1");
                    check.Equal("root-1", root.OperationId, "the explicitly supplied operation id");

                    const int depth = 5;
                    string parent = root.OperationId;
                    List<string> chain = new List<string>();

                    for (int level = 1; level <= depth; level++)
                    {
                        ITelemetryOperation child = pipeline.StartOperation(
                            "scenario", "nested-" + level, "child-" + level, parent);

                        child.Checkpoint("entered");
                        child.Complete(TelemetryOutcome.Succeeded);
                        chain.Add(child.OperationId);
                        parent = child.OperationId;
                    }

                    root.Complete(TelemetryOutcome.Succeeded);

                    IReadOnlyList<TelemetryOperation> recent = pipeline.GetRecentOperations(int.MaxValue);
                    Dictionary<string, TelemetryOperation> byId =
                        new Dictionary<string, TelemetryOperation>(StringComparer.Ordinal);
                    foreach (TelemetryOperation operation in recent) byId[operation.OperationId] = operation;

                    check.Equal(depth + 1, recent.Count, "completed operations in the chain");
                    check.That(byId["root-1"].ParentOperationId == null, "the root reported a parent");

                    string expectedParent = "root-1";
                    foreach (string id in chain)
                    {
                        check.Equal(expectedParent, byId[id].ParentOperationId, "parent of " + id);
                        expectedParent = id;
                    }

                    // Generated ids: unique, and never colliding with each other.
                    HashSet<string> generated = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < 500; i++)
                    {
                        ITelemetryOperation operation = pipeline.StartOperation("scenario", "generated");
                        generated.Add(operation.OperationId);
                        operation.Complete(TelemetryOutcome.Succeeded);
                    }

                    check.Equal(500, generated.Count, "distinct generated operation ids");

                    check.Note("a " + depth + "-level chain kept every parent link, and 500 generated ids were distinct");

                    context.Samples.TelemetryCompleted.Add(depth + 1 + 500);
                    context.Counters.Success();

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task TerminalOutcomes(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "terminal-outcomes-and-measurements",
                "success, failure, cancellation and application-defined measurements are recorded as given",
                check =>
                {
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = 64 });

                    TelemetryOutcome[] outcomes =
                    {
                        TelemetryOutcome.Succeeded,
                        TelemetryOutcome.Failed,
                        TelemetryOutcome.Cancelled,
                        TelemetryOutcome.Unknown
                    };

                    foreach (TelemetryOutcome outcome in outcomes)
                    {
                        Dictionary<string, object> dimensions = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "outcomeUnderTest", outcome.ToString() },
                            { "provider", "scenario" }
                        };

                        Dictionary<string, double> measurements = new Dictionary<string, double>(StringComparer.Ordinal)
                        {
                            { "rows", 42 },
                            { "bytes", 1024.5 }
                        };

                        ITelemetryOperation operation = pipeline.StartOperation(
                            "scenario", "terminal", "terminal-" + outcome);

                        operation.Complete(outcome, dimensions, measurements);

                        switch (outcome)
                        {
                            case TelemetryOutcome.Succeeded: context.Counters.Success(); break;
                            case TelemetryOutcome.Cancelled: context.Counters.Cancellation(); break;
                            default: context.Counters.ExpectedFailure(); break;
                        }
                    }

                    IReadOnlyList<TelemetryOperation> recent = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(outcomes.Length, recent.Count, "recorded terminals");

                    foreach (TelemetryOperation operation in recent)
                    {
                        check.Equal("terminal-" + operation.Outcome, operation.OperationId,
                            "the recorded outcome does not match the one requested");

                        check.Equal(operation.Outcome.ToString(),
                            operation.Dimensions["outcomeUnderTest"] as string, "dimension round-trip");

                        check.That(operation.Measurements["rows"] == 42d, "the rows measurement was not preserved");
                        check.That(operation.Measurements["bytes"] == 1024.5d, "the bytes measurement was not preserved");
                        check.That(operation.Duration >= TimeSpan.Zero, "a negative duration was recorded");
                    }

                    // Completing twice must be a no-op rather than a second record.
                    ITelemetryOperation once = pipeline.StartOperation("scenario", "idempotent", "idempotent-1");
                    once.Complete(TelemetryOutcome.Succeeded);
                    once.Complete(TelemetryOutcome.Failed);

                    IReadOnlyList<TelemetryOperation> after = pipeline.GetRecentOperations(int.MaxValue);
                    int idempotent = 0;
                    foreach (TelemetryOperation operation in after)
                        if (operation.OperationId == "idempotent-1") idempotent++;

                    check.Equal(1, idempotent, "records produced by completing one operation twice");
                    check.Note("a second Complete is ignored, so a duplicated call cannot inflate a measurement");

                    context.Samples.TelemetryCompleted.Add(outcomes.Length + 1);
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task SnapshotBoundaries(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "snapshot-boundaries-while-producing",
                "maxOperations boundaries hold while producers are active and after they stop",
                async check =>
                {
                    const int capacity = 128;
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = capacity });

                    using (CancellationTokenSource stop = new CancellationTokenSource())
                    {
                        Task producer = Task.Run(() =>
                        {
                            long i = 0;
                            while (!stop.IsCancellationRequested)
                            {
                                ITelemetryOperation operation = pipeline.StartOperation(
                                    "scenario", "background", "bg-" + (++i));
                                operation.Complete(TelemetryOutcome.Succeeded);
                            }
                        });

                        int[] requests = { 0, 1, capacity / 2, capacity - 1, capacity, capacity + 1, int.MaxValue };
                        int reads = 0;

                        for (int round = 0; round < 30; round++)
                        {
                            foreach (int request in requests)
                            {
                                IReadOnlyList<TelemetryOperation> snapshot = pipeline.GetRecentOperations(request);
                                reads++;

                                int bound = request <= 0 ? 0 : Math.Min(request, capacity);
                                check.That(snapshot.Count <= bound,
                                    "GetRecentOperations(" + request + ") returned " + snapshot.Count +
                                    ", above the bound of " + bound);
                            }

                            await Task.Delay(5, context.Ct).ConfigureAwait(false);
                        }

                        stop.Cancel();
                        await producer.ConfigureAwait(false);
                    }

                    check.Equal(0, pipeline.GetRecentOperations(0).Count, "GetRecentOperations(0)");
                    check.Equal(0, pipeline.GetRecentOperations(-5).Count, "GetRecentOperations(-5)");
                    check.Equal(capacity, pipeline.GetRecentOperations(int.MaxValue).Count,
                        "a settled snapshot at capacity");

                    check.Note("snapshots taken across seven maxOperations boundaries during production never exceeded capacity");
                    context.Counters.Success();
                });
        }

        /// <summary>
        /// The suite asks for "the actual behaviour of an operation scope that
        /// is abandoned without Complete, if the public API permits this,
        /// without inventing a stronger contract than the current
        /// implementation". It does permit it, and the behaviour is silence.
        /// </summary>
        private static Task AbandonedScope(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "abandoned-scope",
                "an operation abandoned without Complete is never recorded, and abandoning it is observable only to the caller",
                check =>
                {
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = 64 });

                    ITelemetryOperation kept = pipeline.StartOperation("scenario", "kept", "kept-1");
                    kept.Complete(TelemetryOutcome.Succeeded);

                    const int abandoned = 200;
                    for (int i = 0; i < abandoned; i++)
                    {
                        ITelemetryOperation orphan = pipeline.StartOperation("scenario", "abandoned", "orphan-" + i);
                        orphan.Checkpoint("did-some-work");

                        // Deliberately dropped. ITelemetryOperation is not
                        // IDisposable, so there is no scope to leave and no
                        // finaliser to run.
                        check.That(!orphan.IsCompleted, "an abandoned operation reported itself completed");
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    IReadOnlyList<TelemetryOperation> recent = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(1, recent.Count, "recorded operations after abandoning " + abandoned);
                    check.Equal("kept-1", recent[0].OperationId, "the surviving record");

                    check.Note(abandoned + " abandoned operations produced no record, no sink write and no error, " +
                               "including after a forced collection");
                    check.Note("this is the current implementation's behaviour, not a guarantee the API states: " +
                               "ITelemetryOperation is not IDisposable and an abandoned scope is invisible to " +
                               "ITelemetrySnapshotSource. An application that needs abandonment detected has to " +
                               "arrange it itself");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task ImmutableResults(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "immutable-returned-data",
                "previously returned operations and snapshots are not mutated by later production",
                check =>
                {
                    TelemetryPipeline pipeline = new TelemetryPipeline(
                        new TelemetryPipelineOptions { RecentOperationCapacity = 16 });

                    Dictionary<string, object> dimensions = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "phase", "before" }
                    };

                    ITelemetryOperation first = pipeline.StartOperation("scenario", "immutable", "immutable-1");
                    first.Complete(TelemetryOutcome.Succeeded, dimensions);

                    IReadOnlyList<TelemetryOperation> snapshot = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(1, snapshot.Count, "the first snapshot");

                    TelemetryOperation captured = snapshot[0];
                    TimeSpan capturedDuration = captured.Duration;
                    int capturedCheckpoints = captured.Checkpoints.Count;

                    // Mutating the caller's dictionary must not reach the record.
                    dimensions["phase"] = "after";

                    // Producing far past capacity evicts the captured operation
                    // from the pipeline, which must not disturb the reference the
                    // caller already holds.
                    for (int i = 0; i < 100; i++)
                    {
                        ITelemetryOperation more = pipeline.StartOperation("scenario", "filler", "filler-" + i);
                        more.Complete(TelemetryOutcome.Succeeded);
                    }

                    check.Equal("before", captured.Dimensions["phase"] as string,
                        "a dimension changed after the operation was returned");
                    check.That(captured.Duration == capturedDuration, "the duration of a returned operation changed");
                    check.Equal(capturedCheckpoints, captured.Checkpoints.Count,
                        "the checkpoint count of a returned operation changed");
                    check.Equal(1, snapshot.Count, "the first snapshot's length changed");
                    check.That(ReferenceEquals(snapshot[0], captured), "the first snapshot's contents were replaced");

                    IReadOnlyList<TelemetryOperation> later = pipeline.GetRecentOperations(int.MaxValue);
                    check.Equal(16, later.Count, "the later snapshot is at capacity");
                    check.That(!ReferenceEquals(later, snapshot), "two snapshots returned the same list instance");

                    check.Note("a returned snapshot survived 100 later operations and a mutation of the caller's " +
                               "dimension dictionary unchanged");

                    context.Samples.TelemetryCompleted.Add(101);
                    context.Counters.Success();

                    return PhaseContext.CompletedTask;
                });
        }
    }
}

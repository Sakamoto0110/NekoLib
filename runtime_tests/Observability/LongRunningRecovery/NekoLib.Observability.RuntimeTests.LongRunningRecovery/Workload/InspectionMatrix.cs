#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Inspection;
using NekoLib.Inspection;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Providers;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// Inspection: bounded operation retention, the four state-provider shapes,
    /// failure and timeout isolation, registration and runtime lifecycles, the
    /// process-wide slot, and concurrent recording.
    /// <para/>
    /// This scenario registers no action, and asserts that it registered none.
    /// Action invocation and the module-instrumentation rollout are frozen and
    /// explicitly out of scope, so a run that quietly grew an action count would
    /// be evidence of the wrong thing.
    /// </summary>
    internal static class InspectionMatrix
    {
        private const string Phase = Phases.Inspection;

        public static async Task RunAsync(PhaseContext context)
        {
            await RetentionAndEviction(context).ConfigureAwait(false);
            await ProviderShapes(context).ConfigureAwait(false);
            await ProviderIsolation(context).ConfigureAwait(false);
            await RegistrationCycles(context).ConfigureAwait(false);
            await LocalRuntimeCycles(context).ConfigureAwait(false);
            await GlobalEnableDisposeCycles(context).ConfigureAwait(false);
            await ConcurrentRecordAndCapture(context).ConfigureAwait(false);
            await NoActionsRegistered(context).ConfigureAwait(false);
            await OwnershipAfterDisposal(context).ConfigureAwait(false);
        }

        private static Task RetentionAndEviction(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "retention-and-eviction",
                "recording several multiples of capacity evicts the oldest and leaves the totals consistent",
                check =>
                {
                    const int capacity = 256;
                    const int multiples = 5;
                    const int total = capacity * multiples;

                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = capacity }))
                    {
                        for (int i = 1; i <= total; i++)
                            runtime.Record("scenario", "recorded", () => "payload");

                        InspectionRuntimeDiagnostics diagnostics = runtime.GetDiagnostics();

                        check.Equal(capacity, diagnostics.Capacity, "configured capacity");
                        check.Equal(capacity, diagnostics.RetainedCount, "retained operations");
                        check.Equal(total, diagnostics.TotalRecorded, "total recorded");
                        check.Equal(total - capacity, diagnostics.EvictedCount, "evicted operations");
                        check.Equal(0, diagnostics.ClearCount, "clear count before any clear");

                        check.That(diagnostics.OldestSequence.HasValue && diagnostics.NewestSequence.HasValue,
                            "the sequence bounds are missing on a non-empty runtime");

                        check.Equal(total - capacity + 1, diagnostics.OldestSequence!.Value, "oldest sequence");
                        check.Equal(total, diagnostics.NewestSequence!.Value, "newest sequence");

                        IReadOnlyList<InspectionOperation> operations = runtime.GetOperations();
                        check.Equal(capacity, operations.Count, "operations returned");
                        check.Equal(diagnostics.OldestSequence.Value, operations[0].Sequence, "first operation sequence");
                        check.Equal(diagnostics.NewestSequence.Value, operations[operations.Count - 1].Sequence,
                            "last operation sequence");

                        for (int i = 1; i < operations.Count; i++)
                        {
                            check.That(operations[i].Sequence == operations[i - 1].Sequence + 1,
                                "the retained sequence is not contiguous at index " + i);
                        }

                        // A snapshot must agree with the diagnostics it was taken beside.
                        InspectionSnapshot snapshot = runtime.CaptureSnapshot(int.MaxValue, TimeSpan.FromSeconds(5));
                        check.Equal(capacity, snapshot.Operations.Count, "operations in the snapshot");
                        check.Equal(total, snapshot.TotalRecorded, "snapshot total recorded");
                        check.Equal(total - capacity, snapshot.EvictedCount, "snapshot evicted count");

                        // maxOperations boundaries.
                        check.Equal(0, runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(5)).Operations.Count,
                            "CaptureSnapshot(0)");
                        check.Equal(1, runtime.CaptureSnapshot(1, TimeSpan.FromSeconds(5)).Operations.Count,
                            "CaptureSnapshot(1)");
                        check.Equal(capacity, runtime.CaptureSnapshot(capacity + 1, TimeSpan.FromSeconds(5)).Operations.Count,
                            "CaptureSnapshot(capacity + 1)");

                        runtime.ClearOperations();
                        InspectionRuntimeDiagnostics cleared = runtime.GetDiagnostics();
                        check.Equal(0, cleared.RetainedCount, "retained operations after a clear");
                        check.Equal(1, cleared.ClearCount, "clear count after one clear");
                        check.Equal(total, cleared.TotalRecorded, "total recorded is not reset by a clear");
                        check.That(!cleared.OldestSequence.HasValue, "an empty runtime still reports an oldest sequence");

                        context.Samples.InspectionRecorded.Add(total);
                        context.Counters.Success();

                        check.Note(total + " records against a capacity of " + capacity + " retained [" +
                                   diagnostics.OldestSequence.Value + ".." + diagnostics.NewestSequence.Value +
                                   "] and evicted " + diagnostics.EvictedCount);
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task ProviderShapes(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "state-provider-shapes",
                "providers that return normally, return null, throw, or exceed the budget each get their documented marker",
                check =>
                {
                    ScenarioStateProvider healthy = ScenarioStateProvider.Healthy("healthy");
                    ScenarioStateProvider empty = ScenarioStateProvider.ReturnsNull("null");
                    ScenarioStateProvider throws = ScenarioStateProvider.Throws("throws");
                    ScenarioStateProvider slow = ScenarioStateProvider.Slow("slow", TimeSpan.FromMilliseconds(600));

                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = 32 }))
                    using (runtime.RegisterStateProvider("scenario", healthy.Key, healthy.Snapshot))
                    using (runtime.RegisterStateProvider("scenario", empty.Key, empty.Snapshot))
                    using (runtime.RegisterStateProvider("scenario", throws.Key, throws.Snapshot))
                    using (runtime.RegisterStateProvider("scenario", slow.Key, slow.Snapshot))
                    {
                        runtime.Record("scenario", "before-the-snapshot");

                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                        InspectionSnapshot snapshot = runtime.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromMilliseconds(200));
                        clock.Stop();

                        check.Equal(4, snapshot.State.Count, "state slots in the snapshot");

                        object healthyValue = snapshot.State["scenario::healthy"];
                        check.That(healthyValue is string && ((string)healthyValue).StartsWith("healthy", StringComparison.Ordinal),
                            "the healthy provider's value is " + healthyValue);

                        check.Equal(ProviderMarkers.Null, snapshot.State["scenario::null"] as string,
                            "the marker for a provider returning null");

                        check.That(ProviderMarkers.IsThrown(snapshot.State["scenario::throws"]),
                            "the throwing provider's slot holds " + snapshot.State["scenario::throws"] +
                            " rather than a thrown marker");

                        check.Equal(ProviderMarkers.TimedOut, snapshot.State["scenario::slow"] as string,
                            "the marker for a provider that exceeded the budget");

                        // The budget is what makes a snapshot safe to take from a
                        // consumer thread, so the capture has to respect it even
                        // though one provider ignores it.
                        check.That(clock.Elapsed < TimeSpan.FromSeconds(3),
                            "the capture took " + clock.ElapsedMilliseconds +
                            "ms against a 200ms budget with one 600ms provider");

                        check.Equal(1, snapshot.Operations.Count, "operations alongside the misbehaving providers");

                        check.Note("markers observed: null -> " + ProviderMarkers.Null +
                                   ", throw -> " + snapshot.State["scenario::throws"] +
                                   ", over budget -> " + ProviderMarkers.TimedOut);
                        check.Note("the capture returned in " + clock.ElapsedMilliseconds + "ms");

                        // CaptureState is the budget-free convenience read. It
                        // isolates failures the same way but has no timeout, so
                        // the slow provider is genuinely waited for.
                        IReadOnlyDictionary<string, object> state = runtime.CaptureState();
                        check.Equal(4, state.Count, "slots from CaptureState");
                        check.Equal(ProviderMarkers.Null, state["scenario::null"] as string,
                            "CaptureState marker for null");
                        check.That(ProviderMarkers.IsThrown(state["scenario::throws"]),
                            "CaptureState did not isolate the throwing provider");
                        check.That(!string.Equals(state["scenario::slow"] as string, ProviderMarkers.TimedOut,
                                       StringComparison.Ordinal),
                            "CaptureState reported a timeout although it applies no budget");

                        check.Note("CaptureState applies no budget, so the slow provider completes there; " +
                                   "only the budgeted CaptureSnapshot reports it as timed out");

                        context.Counters.ExpectedFailure();
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task ProviderIsolation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "provider-failure-and-timeout-isolation",
                "healthy providers and recorded operations still appear in a snapshot taken beside failing ones",
                check =>
                {
                    const int healthyCount = 6;
                    List<ScenarioStateProvider> healthy = new List<ScenarioStateProvider>();
                    List<IDisposable> registrations = new List<IDisposable>();

                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = 64 }))
                    {
                        try
                        {
                            for (int i = 0; i < healthyCount; i++)
                            {
                                ScenarioStateProvider provider = ScenarioStateProvider.Healthy("healthy-" + i);
                                healthy.Add(provider);
                                registrations.Add(runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot));
                            }

                            ScenarioStateProvider throws = ScenarioStateProvider.Throws("broken");
                            ScenarioStateProvider slow = ScenarioStateProvider.Slow("late", TimeSpan.FromMilliseconds(500));
                            registrations.Add(runtime.RegisterStateProvider("scenario", throws.Key, throws.Snapshot));
                            registrations.Add(runtime.RegisterStateProvider("scenario", slow.Key, slow.Snapshot));

                            for (int i = 0; i < 20; i++)
                                runtime.Record("scenario", "operation-" + i);

                            InspectionSnapshot snapshot = runtime.CaptureSnapshot(
                                int.MaxValue, TimeSpan.FromMilliseconds(250));

                            check.Equal(healthyCount + 2, snapshot.State.Count, "state slots");
                            check.Equal(20, snapshot.Operations.Count, "operations in the partial snapshot");

                            int intact = 0;
                            foreach (ScenarioStateProvider provider in healthy)
                            {
                                object value = snapshot.State["scenario::" + provider.Key];
                                if (value is string && ((string)value).StartsWith("healthy", StringComparison.Ordinal))
                                    intact++;
                            }

                            check.Equal(healthyCount, intact,
                                "healthy providers that survived a snapshot containing a broken and a slow one");

                            check.Note("a partial snapshot kept all " + healthyCount +
                                       " healthy providers and all 20 operations while two providers misbehaved");

                            // Recovery: disarming both makes the next snapshot whole.
                            throws.Armed = false;
                            slow.Armed = false;

                            InspectionSnapshot recovered = runtime.CaptureSnapshot(
                                int.MaxValue, TimeSpan.FromSeconds(5));

                            check.That(!ProviderMarkers.IsThrown(recovered.State["scenario::broken"]),
                                "the recovered provider still reports a thrown marker");
                            check.That(!string.Equals(recovered.State["scenario::late"] as string,
                                           ProviderMarkers.TimedOut, StringComparison.Ordinal),
                                "the recovered provider still reports a timeout");

                            check.Note("after disarming both, the next snapshot carried real values in every slot");

                            context.Samples.InspectionRecorded.Add(20);
                            context.Counters.ExpectedFailure();
                        }
                        finally
                        {
                            foreach (IDisposable registration in registrations) registration.Dispose();
                        }
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task RegistrationCycles(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "register-unregister-cycles",
                "repeated registration cycles return the provider count to its baseline and reject duplicates",
                check =>
                {
                    const int cycles = 50;
                    const int perCycle = 8;

                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = 32 }))
                    {
                        int baseline = runtime.GetDiagnostics().ProviderCount;
                        check.Equal(0, baseline, "providers on a fresh runtime");

                        for (int cycle = 0; cycle < cycles; cycle++)
                        {
                            List<IDisposable> registrations = new List<IDisposable>();
                            for (int i = 0; i < perCycle; i++)
                            {
                                ScenarioStateProvider provider = ScenarioStateProvider.Healthy("cycle-" + i);
                                registrations.Add(runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot));
                            }

                            check.Equal(perCycle, runtime.GetDiagnostics().ProviderCount,
                                "providers registered in cycle " + cycle);

                            if (cycle == 0)
                            {
                                // The same module and key twice is a programming
                                // error rather than a silent replacement.
                                ScenarioStateProvider duplicate = ScenarioStateProvider.Healthy("cycle-0");
                                Exception? failure = PhaseContext.Capture(() =>
                                    runtime.RegisterStateProvider("scenario", duplicate.Key, duplicate.Snapshot).Dispose());

                                check.That(failure is InvalidOperationException,
                                    "a duplicate registration produced " +
                                    (failure == null ? "no exception" : failure.GetType().Name));

                                check.Equal(perCycle, runtime.GetDiagnostics().ProviderCount,
                                    "providers after a rejected duplicate");
                            }

                            foreach (IDisposable registration in registrations) registration.Dispose();

                            // Disposing twice must not remove someone else's later registration.
                            foreach (IDisposable registration in registrations) registration.Dispose();

                            check.Equal(baseline, runtime.GetDiagnostics().ProviderCount,
                                "providers after cycle " + cycle);
                        }

                        check.Equal(0, runtime.GetDiagnostics().ActionCount, "actions this scenario registered");
                        check.Note(cycles + " cycles of " + perCycle +
                                   " registrations returned to a provider count of " + baseline +
                                   ", including after double disposal");

                        context.Counters.Success();
                    }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task LocalRuntimeCycles(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "local-runtime-cycles",
                "repeated local runtime create/use/dispose cycles leave nothing owned",
                check =>
                {
                    const int cycles = 60;

                    for (int cycle = 0; cycle < cycles; cycle++)
                    {
                        InspectionRuntime runtime = new InspectionRuntime(new InspectionOptions { Capacity = 16 });
                        ScenarioStateProvider provider = ScenarioStateProvider.Healthy("cycle");
                        IDisposable registration = runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot);

                        for (int i = 0; i < 32; i++)
                            runtime.Record("scenario", "cycle-work");

                        InspectionSnapshot snapshot = runtime.CaptureSnapshot(int.MaxValue, TimeSpan.FromSeconds(2));
                        if (cycle == 0)
                        {
                            check.Equal(16, snapshot.Operations.Count, "operations in the first cycle's snapshot");
                            check.Equal(1, snapshot.State.Count, "state slots in the first cycle's snapshot");
                        }

                        registration.Dispose();
                        runtime.Dispose();
                        runtime.Dispose();

                        InspectionRuntimeDiagnostics after = runtime.GetDiagnostics();
                        check.That(!after.IsEnabled, "a disposed runtime in cycle " + cycle + " still reports enabled");
                        check.Equal(0, after.ProviderCount, "providers left after cycle " + cycle);
                        check.Equal(0, after.ActionCount, "actions left after cycle " + cycle);
                        check.Equal(0, after.RetainedCount, "operations left after cycle " + cycle);
                    }

                    check.Note(cycles + " local runtimes created, used and disposed; each ended with no provider, " +
                               "no action and no retained operation, and a second Dispose was harmless");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task GlobalEnableDisposeCycles(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "global-enable-dispose-cycles",
                "EnableGlobal owns the process-wide slot exclusively and restores it on disposal",
                check =>
                {
                    check.That(!InspectionProvider.Current.IsEnabled,
                        "the process-wide slot was already occupied before this check ran");

                    const int cycles = 25;

                    for (int cycle = 0; cycle < cycles; cycle++)
                    {
                        InspectionRuntime runtime = InspectionRuntime.EnableGlobal(
                            new InspectionOptions { Capacity = 32 });

                        try
                        {
                            check.That(ReferenceEquals(InspectionProvider.Current, runtime),
                                "the process-wide slot does not hold the runtime enabled in cycle " + cycle);

                            check.That(InspectionProvider.Current.IsEnabled,
                                "the installed recorder reports itself disabled in cycle " + cycle);

                            // A second global runtime must be refused while one is installed.
                            Exception? refused = PhaseContext.Capture(() =>
                                InspectionRuntime.EnableGlobal(new InspectionOptions { Capacity = 4 }).Dispose());

                            check.That(refused is InvalidOperationException,
                                "a second EnableGlobal produced " +
                                (refused == null ? "no exception" : refused.GetType().Name) + " in cycle " + cycle);

                            // Module-style recording through the slot, which is
                            // how an instrumented module would reach it.
                            for (int i = 0; i < 20; i++)
                                InspectionProvider.Current.Record("scenario", "through-the-slot");

                            check.Equal(20, runtime.GetDiagnostics().TotalRecorded,
                                "records reaching the runtime through the slot in cycle " + cycle);

                            context.Samples.InspectionRecorded.Add(20);
                        }
                        finally
                        {
                            runtime.Dispose();
                        }

                        check.That(!InspectionProvider.Current.IsEnabled,
                            "the process-wide slot was not restored after cycle " + cycle);

                        check.That(ReferenceEquals(InspectionProvider.Current, NullInspection.Instance),
                            "the process-wide slot holds something other than the null recorder after cycle " + cycle);

                        // Recording through the restored slot is a harmless no-op.
                        InspectionProvider.Current.Record("scenario", "after-teardown");

                        // The slot's static type is IInspectionRecorder, the
                        // push-only half. Reading requires the separate
                        // IInspectionSnapshotSource, which is the documented
                        // split between what a module gets and what a consumer
                        // gets: a module holding the process-wide slot cannot
                        // read the buffer back through it.
                        IInspectionSnapshotSource? restored =
                            InspectionProvider.Current as IInspectionSnapshotSource;

                        check.That(restored != null,
                            "the restored null recorder does not expose a snapshot surface in cycle " + cycle);

                        check.Equal(0, restored!.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromSeconds(1)).Operations.Count,
                            "the null recorder retained an operation after cycle " + cycle);
                    }

                    check.Note(cycles + " EnableGlobal/Dispose cycles each restored the slot to NullInspection, " +
                               "refused a concurrent second runtime, and left recording through the slot inert");
                    check.Note("InspectionProvider.Current is typed IInspectionRecorder, so the process-wide slot " +
                               "offers only the push surface; reading needs IInspectionSnapshotSource, which is the " +
                               "module/consumer split working as documented");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task ConcurrentRecordAndCapture(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "concurrent-record-and-capture",
                "recording and snapshot capture run together without loss, duplication or a torn sequence",
                async check =>
                {
                    const int capacity = 512;
                    const int recorders = 6;
                    const int each = 2000;
                    const int total = recorders * each;

                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = capacity }))
                    {
                        ScenarioStateProvider provider = ScenarioStateProvider.Healthy("concurrent");
                        using (runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot))
                        using (CancellationTokenSource stop = new CancellationTokenSource())
                        {
                            int captures = 0;
                            Task reader = Task.Run(() =>
                            {
                                while (!stop.IsCancellationRequested)
                                {
                                    InspectionSnapshot snapshot = runtime.CaptureSnapshot(
                                        int.MaxValue, TimeSpan.FromSeconds(2));

                                    captures++;

                                    if (snapshot.Operations.Count > capacity)
                                        throw new InvalidOperationException(
                                            "a snapshot exceeded capacity: " + snapshot.Operations.Count);

                                    for (int i = 1; i < snapshot.Operations.Count; i++)
                                    {
                                        if (snapshot.Operations[i].Sequence != snapshot.Operations[i - 1].Sequence + 1)
                                            throw new InvalidOperationException(
                                                "a snapshot had a torn sequence at index " + i);
                                    }
                                }
                            });

                            Task[] writers = new Task[recorders];
                            for (int r = 0; r < recorders; r++)
                            {
                                int id = r;
                                writers[r] = Task.Run(() =>
                                {
                                    for (int i = 0; i < each; i++)
                                        runtime.Record("scenario", "concurrent-" + id);
                                }, context.Ct);
                            }

                            await Task.WhenAll(writers).ConfigureAwait(false);
                            stop.Cancel();
                            await reader.ConfigureAwait(false);

                            InspectionRuntimeDiagnostics diagnostics = runtime.GetDiagnostics();
                            check.Equal(total, diagnostics.TotalRecorded, "total recorded under concurrency");
                            check.Equal(capacity, diagnostics.RetainedCount, "retained under concurrency");
                            check.Equal(total - capacity, diagnostics.EvictedCount, "evicted under concurrency");
                            check.Equal(total, diagnostics.NewestSequence!.Value, "newest sequence under concurrency");

                            check.Note(recorders + " recorders wrote " + total + " operations while " + captures +
                                       " snapshots were taken; every sequence was contiguous and none exceeded capacity");

                            context.Samples.InspectionRecorded.Add(total);
                            context.Counters.Success();
                        }
                    }
                });
        }

        private static Task NoActionsRegistered(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "no-actions-registered",
                "this scenario registers no action and its action count stays zero throughout",
                check =>
                {
                    using (InspectionRuntime runtime = new InspectionRuntime(
                        new InspectionOptions { Capacity = 16 }))
                    {
                        ScenarioStateProvider provider = ScenarioStateProvider.Healthy("no-actions");
                        using (runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot))
                        {
                            for (int i = 0; i < 50; i++)
                                runtime.Record("scenario", "work");

                            runtime.CaptureSnapshot(int.MaxValue, TimeSpan.FromSeconds(2));

                            check.Equal(0, runtime.GetDiagnostics().ActionCount, "registered actions");
                            check.Equal(0, runtime.ActionKeys().Count, "action keys");
                            check.Equal(1, runtime.StateKeys().Count, "state keys");

                            // Nothing to invoke, and asking must simply say so.
                            object? result;
                            check.That(!runtime.TryInvokeAction("scenario", "anything", null, out result),
                                "TryInvokeAction found an action this scenario never registered");
                            check.That(result == null, "a refused invocation returned a result");
                        }

                        check.Equal(0, runtime.GetDiagnostics().ActionCount, "actions after unregistering the provider");
                    }

                    check.Note("action invocation and the module-instrumentation rollout are frozen and out of scope; " +
                               "this check exists so a run cannot quietly start proving otherwise");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task OwnershipAfterDisposal(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "ownership-after-disposal",
                "a disposed runtime records nothing, retains nothing, and hands back inert registrations",
                check =>
                {
                    InspectionRuntime runtime = new InspectionRuntime(new InspectionOptions { Capacity = 32 });
                    ScenarioStateProvider provider = ScenarioStateProvider.Healthy("owned");
                    IDisposable registration = runtime.RegisterStateProvider("scenario", provider.Key, provider.Snapshot);

                    for (int i = 0; i < 10; i++)
                        runtime.Record("scenario", "before-disposal");

                    check.Equal(10, runtime.GetDiagnostics().RetainedCount, "operations before disposal");

                    runtime.Dispose();

                    check.That(!runtime.IsEnabled, "a disposed runtime reports itself enabled");
                    check.Equal(0, runtime.GetOperations().Count, "operations after disposal");
                    check.Equal(0, runtime.GetDiagnostics().ProviderCount, "providers after disposal");
                    check.Equal(0, runtime.GetDiagnostics().ActionCount, "actions after disposal");

                    long recordedBefore = runtime.GetDiagnostics().TotalRecorded;
                    runtime.Record("scenario", "after-disposal");
                    check.Equal(recordedBefore, runtime.GetDiagnostics().TotalRecorded,
                        "records accepted after disposal");

                    // Registering after disposal must give back something safe to
                    // dispose rather than a live registration or a throw.
                    IDisposable inert = runtime.RegisterStateProvider("scenario", "late", provider.Snapshot);
                    check.Equal(0, runtime.GetDiagnostics().ProviderCount, "providers after a late registration");

                    Exception? disposal = PhaseContext.Capture(inert.Dispose);
                    check.That(disposal == null, "disposing an inert registration threw");

                    Exception? original = PhaseContext.Capture(registration.Dispose);
                    check.That(original == null, "disposing a registration after its runtime threw");

                    check.Note("after disposal the runtime is inert in both directions: it accepts no record and " +
                               "hands back a no-op registration rather than throwing");

                    context.Counters.Success();
                    return PhaseContext.CompletedTask;
                });
        }
    }
}

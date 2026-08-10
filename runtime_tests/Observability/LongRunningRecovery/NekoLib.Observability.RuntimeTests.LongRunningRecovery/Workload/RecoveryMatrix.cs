#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.Inspection;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Providers;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// Dispatches the persisted seeded schedule and proves each fault's expected
    /// terminal and recovery.
    /// <para/>
    /// Faults are dispatched at their planned monotonic offset from the campaign
    /// start, never at a wall-clock time, so a run that starts late still
    /// produces the same relative plan. Each one is checked in three parts: the
    /// documented terminal while the fault is applied, a successful probe after
    /// it is withdrawn, and a resource count that returned to where it started.
    /// </summary>
    internal static class RecoveryMatrix
    {
        private const string Phase = Phases.Recovery;

        public static async Task RunAsync(PhaseContext context, FaultSchedule schedule, DateTime startedUtc)
        {
            if (schedule.Events.Count == 0)
            {
                context.Artifacts.Out("recovery   no faults planned for this mode");
                return;
            }

            foreach (FaultEvent planned in schedule.Events)
            {
                DateTime due = startedUtc.AddSeconds(planned.OffsetSeconds);
                TimeSpan wait = due - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    context.Artifacts.Out(
                        "recovery   waiting " + ((int)wait.TotalSeconds).ToString(CultureInfo.InvariantCulture) +
                        "s for " + planned.Kind + " at +" +
                        planned.OffsetSeconds.ToString("F0", CultureInfo.InvariantCulture) + "s");

                    await Task.Delay(wait, context.Ct).ConfigureAwait(false);
                }

                context.Sampler.Take(Phase, "pre-fault");

                context.Artifacts.Event("fault", json =>
                {
                    json.Prop("id", planned.Id);
                    json.Prop("kind", planned.Kind);
                    json.Prop("target", planned.Target);
                    json.Prop("plannedOffsetSeconds", planned.OffsetSeconds);
                    json.Prop("actualUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    json.Prop("expectedRecovery", planned.ExpectedRecovery);
                });

                // Exclusive: an assertion made while another fault is active
                // measures the other fault. The soak's background traffic holds
                // nothing and is free to fail and be counted.
                await context.ExclusiveAsync(() => DispatchAsync(context, planned)).ConfigureAwait(false);

                context.Sampler.Take(Phase, "post-recovery");
            }
        }

        private static Task DispatchAsync(PhaseContext context, FaultEvent planned)
        {
            switch (planned.Kind)
            {
                case FaultKinds.LogSinkThrows: return LogSinkThrows(context, planned);
                case FaultKinds.LogFileLocked: return LogFileLocked(context, planned);
                case FaultKinds.LogFlushBlocked: return LogFlushBlocked(context, planned);
                case FaultKinds.TelemetrySinkThrows: return TelemetrySinkThrows(context, planned);
                case FaultKinds.InspectionProviderThrows: return InspectionProviderThrows(context, planned);
                case FaultKinds.InspectionProviderTimesOut: return InspectionProviderTimesOut(context, planned);
                case FaultKinds.InspectionGlobalTeardown: return InspectionGlobalTeardown(context, planned);

                default:
                    context.Runner.Skip(Phase, planned.Kind, planned.ExpectedRecovery,
                        "no handler is registered for this fault kind");
                    return PhaseContext.CompletedTask;
            }
        }

        private static Task LogSinkThrows(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    ObservabilityWorkspace workspace = context.Workspace;
                    const int during = 800;

                    long healthyBefore = Interlocked.Read(ref workspace.Healthy.Received);
                    long offeredBefore = Interlocked.Read(ref workspace.Failing.Received);
                    long thrownBefore = Interlocked.Read(ref workspace.Failing.Thrown);

                    workspace.Failing.Armed = true;
                    try
                    {
                        for (int i = 1; i <= during; i++)
                        {
                            workspace.Logger.Log(LogLevel.Info, TracedMessage.Compose(9, i));
                            context.Samples.LogEntriesWritten.Increment();
                        }
                    }
                    finally
                    {
                        workspace.Failing.Armed = false;
                    }

                    long thrown = Interlocked.Read(ref workspace.Failing.Thrown) - thrownBefore;
                    long healthy = Interlocked.Read(ref workspace.Healthy.Received) - healthyBefore;
                    long offered = Interlocked.Read(ref workspace.Failing.Received) - offeredBefore;

                    check.That(thrown > 0, "the fault never produced a sink failure");

                    // The isolation claim stated so that background traffic
                    // cannot make it wrong: every entry the failing sink was
                    // offered also reached the healthy one. An exact count
                    // against the shared logger would be measuring the soak's
                    // steady traffic as much as the fault.
                    check.Equal(offered, healthy,
                        "entries offered to the failing sink versus entries the healthy sink received");

                    check.That(healthy >= during,
                        "the healthy sink received " + healthy + " entries, fewer than the " + during +
                        " this fault wrote");

                    // Recovery: with the sink disarmed, ordinary logging works.
                    long afterFault = Interlocked.Read(ref workspace.Healthy.Received);
                    long thrownAfter = Interlocked.Read(ref workspace.Failing.Thrown);

                    for (int i = 1; i <= 200; i++)
                        workspace.Logger.Log(LogLevel.Info, TracedMessage.Compose(9, during + i));

                    check.That(Interlocked.Read(ref workspace.Healthy.Received) - afterFault >= 200,
                        "fewer than 200 entries were delivered after the sink was disarmed");

                    check.Equal(thrownAfter, Interlocked.Read(ref workspace.Failing.Thrown),
                        "the sink threw again after it was disarmed");

                    check.That(workspace.Logger.Flush(TimeSpan.FromSeconds(10)),
                        "the logger would not flush after the sink recovered");

                    check.Note(thrown + " failures while " + offered + " entries passed through the logger " +
                               "(this fault wrote " + during + "; the rest is concurrent steady traffic); " +
                               "the healthy sink received every one of them");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task LogFileLocked(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    string path = context.UniqueWorkPath("locked.log");
                    var options = new NekoLib.Logging.Sinks.RollingFileLogSinkOptions
                    {
                        FilePath = path,
                        MaximumFileBytes = 256 * 1024,
                        RetainedFileCount = 2
                    };

                    NekoLib.Logging.Sinks.RollingFileLogSink file =
                        new NekoLib.Logging.Sinks.RollingFileLogSink(options);

                    CountingLogSink healthy = new CountingLogSink("locked-healthy");

                    using (NekoLib.Logging.Logger logger = new NekoLib.Logging.Logger(
                        new NekoLib.Logging.LoggerOptions
                        {
                            MinimumLevel = LogLevel.Trace,
                            RecentEntryCapacity = 32,
                            DisposeSinks = false
                        },
                        file,
                        healthy))
                    {
                        const int before = 100;
                        for (int i = 1; i <= before; i++)
                            logger.Log(LogLevel.Info, TracedMessage.Compose(0, i));

                        long linesBefore = CountLines(path);
                        check.Equal(before, linesBefore, "lines written before the lock");

                        // The fault: something else owns the file exclusively.
                        // The sink opens with FileShare.Read, so an exclusive
                        // holder makes every write throw inside the sink.
                        const int duringLock = 100;
                        using (FileStream exclusive = new FileStream(
                            path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            for (int i = 1; i <= duringLock; i++)
                                logger.Log(LogLevel.Info, TracedMessage.Compose(1, i));

                            check.Equal(before + duringLock, Interlocked.Read(ref healthy.Received),
                                "entries the healthy sink received while the file was locked");

                            check.That(exclusive.Length > 0, "the locked file is empty");
                        }

                        // Recovery: with the lock released, writes land again and
                        // the file is complete and readable.
                        const int after = 100;
                        for (int i = 1; i <= after; i++)
                            logger.Log(LogLevel.Info, TracedMessage.Compose(2, i));

                        long linesAfter = CountLines(path);
                        check.Equal(before + after, linesAfter,
                            "lines in the file after the lock was released");

                        check.Note("the " + duringLock + " entries written while the file was locked were lost to " +
                                   "the file sink and swallowed by the Logger, exactly as a logging failure should be; " +
                                   "the healthy sink received all " + (before + duringLock + after));

                        check.That(logger.Flush(TimeSpan.FromSeconds(10)), "the logger would not flush after recovery");
                        context.Counters.ExpectedFailure();
                    }

                    // No handle survived: this fails outright if one did.
                    using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }

                    return PhaseContext.CompletedTask;
                });
        }

        private static Task LogFlushBlocked(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                async check =>
                {
                    ObservabilityWorkspace workspace = context.Workspace;

                    check.That(workspace.Logger.Flush(TimeSpan.FromSeconds(10)),
                        "the baseline flush failed before the fault was applied");

                    workspace.Blocking.Block();
                    try
                    {
                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                        bool flushed = workspace.Logger.Flush(TimeSpan.FromMilliseconds(400));
                        clock.Stop();

                        check.That(!flushed, "a flush against a blocked sink reported success");
                        check.That(clock.Elapsed < TimeSpan.FromSeconds(5),
                            "the flush took " + clock.ElapsedMilliseconds + "ms, which is not bounded by its 400ms budget");

                        // Logging itself must keep working while a flush is
                        // refused: the two take the same lock, so this is worth
                        // proving rather than assuming.
                        long before = Interlocked.Read(ref workspace.Healthy.Received);
                        for (int i = 1; i <= 100; i++)
                            workspace.Logger.Log(LogLevel.Info, TracedMessage.Compose(8, i));

                        check.That(Interlocked.Read(ref workspace.Healthy.Received) - before >= 100,
                            "fewer than 100 entries were accepted while a flush was blocked");

                        check.Note("Flush(400ms) returned false after " + clock.ElapsedMilliseconds +
                                   "ms and writing continued unaffected");
                    }
                    finally
                    {
                        workspace.Blocking.Release();
                    }

                    await Task.Delay(50, context.Ct).ConfigureAwait(false);

                    check.That(workspace.Logger.Flush(TimeSpan.FromSeconds(10)),
                        "the flush did not recover after the sink was released");

                    context.Counters.ExpectedFailure();
                });
        }

        private static Task TelemetrySinkThrows(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    ObservabilityWorkspace workspace = context.Workspace;
                    const int during = 300;

                    long receivedBefore = Interlocked.Read(ref workspace.TelemetrySink.Received);

                    workspace.TelemetrySink.Armed = true;
                    try
                    {
                        for (int i = 0; i < during; i++)
                        {
                            ITelemetryOperation operation = workspace.Telemetry.StartOperation(
                                "scenario", "during-sink-failure", "fault-" + i);
                            operation.Complete(TelemetryOutcome.Succeeded);
                            context.Samples.TelemetryCompleted.Increment();
                        }
                    }
                    finally
                    {
                        workspace.TelemetrySink.Armed = false;
                    }

                    long offered = Interlocked.Read(ref workspace.TelemetrySink.Received) - receivedBefore;

                    check.That(offered >= during,
                        "the failing sink was offered " + offered + " operations, fewer than the " + during +
                        " this fault completed");

                    check.That(workspace.TelemetrySink.Thrown > 0, "the telemetry sink never actually threw");

                    // The pipeline's own retention must be unaffected by a sink
                    // that rejects everything.
                    IReadOnlyList<TelemetryOperation> retained =
                        workspace.Telemetry.GetRecentOperations(int.MaxValue);

                    check.Equal(ObservabilityWorkspace.TelemetryCapacity, retained.Count,
                        "operations retained while the sink was failing");

                    // Recovery. The probe is looked up by id rather than by
                    // position: concurrent steady traffic may record after it,
                    // and asserting on the newest slot would be measuring the
                    // traffic rather than the recovery.
                    ITelemetryOperation after = workspace.Telemetry.StartOperation(
                        "scenario", "after-sink-recovery", "recovered-" + planned.Id);
                    after.Complete(TelemetryOutcome.Succeeded);

                    bool found = false;
                    foreach (TelemetryOperation operation in workspace.Telemetry.GetRecentOperations(int.MaxValue))
                        if (operation.OperationId == "recovered-" + planned.Id) found = true;

                    check.That(found, "the operation recorded after the sink recovered was not retained");

                    check.Note(workspace.TelemetrySink.Thrown + " sink failures did not cost the pipeline a single " +
                               "record: " + offered + " operations passed through it, retention stayed at " +
                               retained.Count + " and the next operation recorded normally");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task InspectionProviderThrows(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    InspectionRuntime runtime = context.Workspace.Inspection;

                    ScenarioStateProvider healthy = ScenarioStateProvider.Healthy("fault-healthy");
                    ScenarioStateProvider broken = ScenarioStateProvider.Throws("fault-broken");

                    int baseline = runtime.GetDiagnostics().ProviderCount;

                    using (runtime.RegisterStateProvider("scenario", healthy.Key, healthy.Snapshot))
                    using (runtime.RegisterStateProvider("scenario", broken.Key, broken.Snapshot))
                    {
                        for (int i = 0; i < 50; i++)
                            runtime.Record("scenario", "beside-a-broken-provider");

                        context.Samples.InspectionRecorded.Add(50);

                        InspectionSnapshot snapshot = runtime.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromSeconds(2));

                        check.That(ProviderMarkers.IsThrown(snapshot.State["scenario::fault-broken"]),
                            "the broken provider's slot holds " + snapshot.State["scenario::fault-broken"]);

                        object healthyValue = snapshot.State["scenario::fault-healthy"];
                        check.That(healthyValue is string &&
                                   ((string)healthyValue).StartsWith("healthy", StringComparison.Ordinal),
                            "the healthy provider did not survive beside a broken one");

                        check.That(snapshot.Operations.Count >= 50,
                            "operations were lost from a snapshot containing a broken provider: " +
                            snapshot.Operations.Count);

                        // Recovery: disarmed, the provider reports normally again.
                        broken.Armed = false;
                        InspectionSnapshot recovered = runtime.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromSeconds(2));

                        check.That(!ProviderMarkers.IsThrown(recovered.State["scenario::fault-broken"]),
                            "the provider still reports a thrown marker after recovery");

                        check.Note("the broken provider failed " + broken.Calls +
                                   " time(s) without costing the snapshot its healthy provider or its operations");
                    }

                    check.Equal(baseline, runtime.GetDiagnostics().ProviderCount,
                        "providers after the fault's registrations were disposed");

                    check.Equal(0, runtime.GetDiagnostics().ActionCount, "actions after the fault");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task InspectionProviderTimesOut(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    InspectionRuntime runtime = context.Workspace.Inspection;

                    ScenarioStateProvider healthy = ScenarioStateProvider.Healthy("budget-healthy");
                    ScenarioStateProvider slow = ScenarioStateProvider.Slow("budget-slow", TimeSpan.FromMilliseconds(700));

                    int baseline = runtime.GetDiagnostics().ProviderCount;

                    using (runtime.RegisterStateProvider("scenario", healthy.Key, healthy.Snapshot))
                    using (runtime.RegisterStateProvider("scenario", slow.Key, slow.Snapshot))
                    {
                        for (int i = 0; i < 25; i++)
                            runtime.Record("scenario", "beside-a-slow-provider");

                        context.Samples.InspectionRecorded.Add(25);

                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                        InspectionSnapshot snapshot = runtime.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromMilliseconds(200));
                        clock.Stop();

                        check.Equal(ProviderMarkers.TimedOut, snapshot.State["scenario::budget-slow"] as string,
                            "the slow provider's marker");

                        object healthyValue = snapshot.State["scenario::budget-healthy"];
                        check.That(healthyValue is string &&
                                   ((string)healthyValue).StartsWith("healthy", StringComparison.Ordinal),
                            "the healthy provider did not survive beside a slow one");

                        check.That(snapshot.Operations.Count >= 25,
                            "operations were lost from a snapshot containing a slow provider");

                        check.That(clock.Elapsed < TimeSpan.FromSeconds(3),
                            "the capture took " + clock.ElapsedMilliseconds +
                            "ms against a 200ms budget, so the budget was not honoured");

                        check.Note("a 700ms provider against a 200ms budget returned a timed-out marker and the " +
                                   "capture finished in " + clock.ElapsedMilliseconds + "ms");

                        // Recovery.
                        slow.Armed = false;
                        InspectionSnapshot recovered = runtime.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromSeconds(5));

                        check.That(!string.Equals(recovered.State["scenario::budget-slow"] as string,
                                       ProviderMarkers.TimedOut, StringComparison.Ordinal),
                            "the provider still times out after recovery");
                    }

                    check.Equal(baseline, runtime.GetDiagnostics().ProviderCount,
                        "providers after the fault's registrations were disposed");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static Task InspectionGlobalTeardown(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(FaultKinds.Capability(planned.Kind), planned.Kind,
                planned.ExpectedRecovery,
                check =>
                {
                    check.That(!InspectionProvider.Current.IsEnabled,
                        "the process-wide slot was occupied before this fault ran");

                    InspectionRuntime global = InspectionRuntime.EnableGlobal(
                        new InspectionOptions { Capacity = 64 });

                    bool disposed = false;
                    try
                    {
                        for (int i = 0; i < 100; i++)
                            InspectionProvider.Current.Record("scenario", "through-the-global-slot");

                        check.Equal(100, global.GetDiagnostics().TotalRecorded,
                            "records reaching the global runtime");

                        context.Samples.InspectionRecorded.Add(100);

                        // The fault: the runtime is torn down while traffic is
                        // still flowing through the slot.
                        global.Dispose();
                        disposed = true;

                        check.That(!InspectionProvider.Current.IsEnabled,
                            "the slot was not restored after the global runtime was disposed");

                        check.That(ReferenceEquals(InspectionProvider.Current, NullInspection.Instance),
                            "the slot holds something other than the null recorder after teardown");

                        // Traffic that survives the teardown must be harmless.
                        for (int i = 0; i < 100; i++)
                            InspectionProvider.Current.Record("scenario", "after-the-teardown");

                        IInspectionSnapshotSource? restored =
                            InspectionProvider.Current as IInspectionSnapshotSource;

                        check.That(restored != null, "the restored recorder exposes no snapshot surface");
                        check.Equal(0, restored!.CaptureSnapshot(
                            int.MaxValue, TimeSpan.FromSeconds(1)).Operations.Count,
                            "the null recorder retained an operation");

                        IDisposable inert = InspectionProvider.Current.RegisterStateProvider(
                            "scenario", "after-teardown", () => "unused");

                        Exception? disposal = PhaseContext.Capture(inert.Dispose);
                        check.That(disposal == null, "disposing a registration from the null recorder threw");
                    }
                    finally
                    {
                        if (!disposed) global.Dispose();
                    }

                    // Recovery: the slot is free, so a fresh runtime installs.
                    InspectionRuntime replacement = InspectionRuntime.EnableGlobal(
                        new InspectionOptions { Capacity = 64 });

                    try
                    {
                        InspectionProvider.Current.Record("scenario", "after-recovery");
                        check.Equal(1, replacement.GetDiagnostics().TotalRecorded,
                            "records reaching the replacement runtime");

                        check.Note("100 records through the slot, teardown mid-traffic, 100 harmless records against " +
                                   "the restored null recorder, then a fresh EnableGlobal that installed cleanly");
                    }
                    finally
                    {
                        replacement.Dispose();
                    }

                    check.That(!InspectionProvider.Current.IsEnabled,
                        "the slot was left occupied after the fault finished");

                    context.Counters.ExpectedFailure();
                    return PhaseContext.CompletedTask;
                });
        }

        private static long CountLines(string path)
        {
            if (!File.Exists(path)) return 0;

            long lines = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (reader.ReadLine() != null) lines++;
            }

            return lines;
        }
    }
}

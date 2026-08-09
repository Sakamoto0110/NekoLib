#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Faults;
using NekoLib.Data.RuntimeTests.SqlServer.Model;
using NekoLib.Data.RuntimeTests.SqlServer.Reporting;
using NekoLib.Data.RuntimeTests.SqlServer.Server;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// What happens when the server goes away, and what has to be true when it
    /// comes back.
    /// <para/>
    /// Every fault here is applied to the adopted container from outside the
    /// library: nothing in <c>NekoLib.Data</c> is asked to simulate a failure,
    /// and nothing in it is expected to retry. Retry is the scenario's own
    /// bounded loop, deliberately, because inventing automatic retry in the
    /// gateway would be a product decision this evidence is not allowed to
    /// make on its own.
    /// <para/>
    /// The provider's error number is recorded for every failure. It is what
    /// makes a transport loss distinguishable from a login refusal months later,
    /// and it is exactly the kind of observation that only real execution
    /// produces.
    /// </summary>
    internal static class RecoveryMatrix
    {
        private const string Phase = "recovery";

        private static readonly TimeSpan ReadyBudget = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan DownBudget = TimeSpan.FromMinutes(2);

        public static async Task RunAsync(PhaseContext context, FaultSchedule schedule, DateTime campaignStartUtc)
        {
            if (!context.ContainerFaultsAllowed || context.Adopted == null)
            {
                foreach (string kind in FaultKinds.RecoveryRehearsalSet)
                {
                    if (!FaultKinds.NeedsContainerControl(kind)) continue;

                    context.Runner.Skip(Phase, kind,
                        "the transition this fault is supposed to prove",
                        context.Adopted == null
                            ? "no container engine was available to adopt"
                            : "container faults were disabled with --no-container-faults");
                }

                await SchemaRecreation(context).ConfigureAwait(false);
                return;
            }

            // Steady-state traffic runs on its own gateway, never the one the
            // matrices assert against. Several checks zero the workspace's
            // lifecycle counters and then make claims about them; background
            // traffic sharing that workspace silently inflates the counts, and
            // in the soak it made provider-error-propagation report two
            // dispatches and one success where it expected one and none.
            using (GatewayWorkspace background = new GatewayWorkspace(
                context.Endpoint.BuildConnectionString(
                    context.DatabaseName,
                    connectTimeoutSeconds: 60,
                    applicationName: "NekoLib.E4-SQL.steady"),
                GatewayWorkspace.DefaultOptions()))
            {
                await DispatchScheduleAsync(context, schedule, campaignStartUtc, background)
                    .ConfigureAwait(false);
            }
        }

        private static async Task DispatchScheduleAsync(
            PhaseContext context,
            FaultSchedule schedule,
            DateTime campaignStartUtc,
            GatewayWorkspace background)
        {
            foreach (FaultEvent planned in schedule.Events)
            {
                await WaitForOffsetAsync(context, planned, campaignStartUtc, background).ConfigureAwait(false);

                context.Sampler.Take(Phase, "pre-fault");
                context.Artifacts.Event("fault-dispatch", json =>
                {
                    json.Prop("eventId", planned.Id);
                    json.Prop("kind", planned.Kind);
                    json.Prop("plannedOffsetSeconds", planned.OffsetSeconds);
                    json.Prop("actualOffsetSeconds", (DateTime.UtcNow - campaignStartUtc).TotalSeconds);
                    json.Prop("expectedRecovery", planned.ExpectedRecovery);
                });

                // Exclusive for the whole fault, including its recovery
                // assertions: taking the server away while a matrix is running
                // is what killed the first soak.
                await context.ExclusiveAsync(() => DispatchAsync(context, planned)).ConfigureAwait(false);
                context.Sampler.Take(Phase, "post-recovery");
            }
        }

        private static async Task DispatchAsync(PhaseContext context, FaultEvent planned)
        {
            switch (planned.Kind)
            {
                case FaultKinds.ConnectWhileServerDown:
                    await ConnectWhileServerDown(context).ConfigureAwait(false);
                    break;
                case FaultKinds.TransportLossDuringCommand:
                    await TransportLossDuringCommand(context).ConfigureAwait(false);
                    break;
                case FaultKinds.TransportLossDuringTransaction:
                    await TransportLossDuringTransaction(context).ConfigureAwait(false);
                    break;
                case FaultKinds.TransportLossDuringStream:
                    await TransportLossDuringStream(context).ConfigureAwait(false);
                    break;
                case FaultKinds.StalePooledConnection:
                    await StalePooledConnection(context).ConfigureAwait(false);
                    break;
                case FaultKinds.ContainerRestart:
                    await ContainerRestart(context).ConfigureAwait(false);
                    break;
                case FaultKinds.SchemaRecreation:
                    await SchemaRecreation(context).ConfigureAwait(false);
                    break;
                default:
                    context.Runner.Skip(Phase, planned.Kind, planned.ExpectedRecovery,
                        "this scenario owns no handler for that fault kind");
                    break;
            }
        }

        private static Task ConnectWhileServerDown(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.ConnectWhileServerDown,
                "opening a connection while the server is down fails cleanly and works again once it is back",
                async check =>
                {
                    await StopServerAsync(context, check).ConfigureAwait(false);

                    Exception? failure = await PhaseContext.CaptureAsync(() =>
                        context.Workspace.Gateway.GetRaw("SELECT 1 AS Probe", null, context.Ct))
                        .ConfigureAwait(false);

                    check.That(failure != null, "a query succeeded against a stopped server");
                    check.Note("open attempt: " + PhaseContext.DescribeProviderFailure(failure!));
                    context.Counters.ExpectedFailure();

                    Exception? sessionFailure = await PhaseContext.CaptureAsync(async () =>
                    {
                        using (DbSession session = await context.Workspace.Gateway
                            .OpenSessionAsync(context.Ct).ConfigureAwait(false))
                        {
                        }
                    }).ConfigureAwait(false);

                    check.That(sessionFailure != null, "a session opened against a stopped server");
                    check.Note("session attempt: " + PhaseContext.DescribeProviderFailure(sessionFailure!));
                    context.Counters.ExpectedFailure();

                    await StartServerAsync(context, check).ConfigureAwait(false);
                    await RecoverAsync(context, check, clearPool: true).ConfigureAwait(false);
                });
        }

        private static Task TransportLossDuringCommand(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.TransportLossDuringCommand,
                "a command in flight when the transport drops fails and leaves the gateway usable after recovery",
                async check =>
                {
                    string marker = "NEKOLIB-RECOVERY-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    context.Workspace.ResetTerminals();

                    Task running = context.Workspace.Gateway.GetRaw(
                        "/*" + marker + "*/ WAITFOR DELAY '00:00:45'; SELECT 1 AS Probe",
                        null,
                        context.Ct);

                    bool started = await WaitForMarkerAsync(context, marker, running).ConfigureAwait(false);
                    check.That(started, "the server never reported the long command as executing");

                    await StopServerAsync(context, check).ConfigureAwait(false);

                    Exception? failure = await PhaseContext.CaptureAsync(() => running).ConfigureAwait(false);
                    check.That(failure != null, "the in-flight command completed although the server went away");
                    check.Note("in-flight failure: " + PhaseContext.DescribeProviderFailure(failure!));
                    context.Counters.ExpectedFailure();

                    check.Equal(0, context.Workspace.Succeeded, "success terminals for the interrupted command");

                    await StartServerAsync(context, check).ConfigureAwait(false);
                    await RecoverAsync(context, check, clearPool: true).ConfigureAwait(false);
                });
        }

        private static Task TransportLossDuringTransaction(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.TransportLossDuringTransaction,
                "an open transaction interrupted by transport loss commits nothing",
                async check =>
                {
                    const string note = "e4sql-recovery-transaction";
                    string before = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);

                    DbSession session = await context.Workspace.Gateway
                        .OpenSessionAsync(context.Ct).ConfigureAwait(false);

                    try
                    {
                        session.BeginTransaction();

                        await context.Workspace.Gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", new Dictionary<string, object?>
                            {
                                ["PartId"] = 1,
                                ["OccurredAt"] = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                                ["Kind"] = "Ajuste",
                                ["Quantity"] = 9,
                                ["Note"] = note
                            }),
                            session,
                            context.Ct).ConfigureAwait(false);

                        await StopServerAsync(context, check).ConfigureAwait(false);

                        Exception? commitFailure = null;
                        try
                        {
                            session.Commit();
                        }
                        catch (Exception ex)
                        {
                            commitFailure = ex;
                        }

                        check.That(commitFailure != null,
                            "the commit reported success although the server was gone");
                        check.Note("commit against a dead transport: " +
                                   PhaseContext.DescribeProviderFailure(commitFailure!));
                        context.Counters.ExpectedFailure();
                    }
                    finally
                    {
                        // Disposal must not throw even though the connection is
                        // broken; that is the property being checked, so it is
                        // observed rather than swallowed.
                        Exception? disposeFailure = null;
                        try { session.Dispose(); }
                        catch (Exception ex) { disposeFailure = ex; }

                        check.Note(disposeFailure == null
                            ? "disposing the session over a dead transport did not throw"
                            : "disposing the session threw " + PhaseContext.DescribeProviderFailure(disposeFailure));
                    }

                    await StartServerAsync(context, check).ConfigureAwait(false);
                    await RecoverAsync(context, check, clearPool: true).ConfigureAwait(false);

                    long survived = await CountNoteAsync(context, note).ConfigureAwait(false);
                    check.Equal(0, survived, "rows the interrupted transaction left behind");

                    string after = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);
                    check.Equal(before, after, "digest across the interrupted transaction");
                });
        }

        private static Task TransportLossDuringStream(PhaseContext context)
        {
#if NET6_0_OR_GREATER
            return context.Runner.RunAsync(Phase, FaultKinds.TransportLossDuringStream,
                "a stream interrupted by transport loss reports exactly one failed terminal",
                async check =>
                {
                    context.Workspace.ResetTerminals();
                    string marker = "NEKOLIB-STREAM-" + Guid.NewGuid().ToString("N").Substring(0, 8);

                    // The first batch returns immediately, so the consumer is
                    // genuinely mid-enumeration; the delay then holds the second
                    // result set open while the server is stopped.
                    string sql =
                        "/*" + marker + "*/ SELECT Id, Sku FROM Part; " +
                        "WAITFOR DELAY '00:00:45'; SELECT Id, Sku FROM Part";

                    Exception? failure = null;
                    int consumed = 0;

                    Task enumeration = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (Dictionary<string, RecordItem> _ in context.Workspace.Gateway
                                .StreamRaw(sql, null, context.Ct).ConfigureAwait(false))
                            {
                                consumed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            failure = ex;
                        }
                    }, context.Ct);

                    bool started = await WaitForMarkerAsync(context, marker, enumeration).ConfigureAwait(false);
                    check.Note(started
                        ? "the server confirmed the streaming batch as executing"
                        : "the streaming batch was not observed as executing; it may have buffered its first result set");

                    await StopServerAsync(context, check).ConfigureAwait(false);
                    await enumeration.ConfigureAwait(false);

                    check.That(failure != null, "the stream completed although the server went away");
                    check.Note("consumed " + consumed.ToString(CultureInfo.InvariantCulture) +
                               " row(s) before the loss; failure: " +
                               PhaseContext.DescribeProviderFailure(failure!));
                    context.Counters.ExpectedFailure();

                    IReadOnlyList<DbQueryStreamOutcome> terminals = context.Workspace.StreamTerminals;
                    check.Equal(1, terminals.Count, "stream terminals for the interrupted enumeration");
                    check.Note("terminal outcome: " + terminals[0]);
                    check.That(
                        terminals[0] == DbQueryStreamOutcome.Failed ||
                        terminals[0] == DbQueryStreamOutcome.Cancelled,
                        "expected a failed or cancelled terminal, got " + terminals[0]);

                    await StartServerAsync(context, check).ConfigureAwait(false);
                    await RecoverAsync(context, check, clearPool: true).ConfigureAwait(false);
                });
#else
            context.Runner.Skip(Phase, FaultKinds.TransportLossDuringStream,
                "a stream interrupted by transport loss reports exactly one failed terminal",
                "the streaming gateway does not exist on net481");

            return Task.FromResult(0);
#endif
        }

        private static Task StalePooledConnection(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.StalePooledConnection,
                "pooled handles from before an interruption do not silently poison later work",
                async check =>
                {
                    // Warm the pool so there is definitely something stale to
                    // find after the restart.
                    for (int i = 0; i < 4; i++)
                    {
                        await context.Workspace.Gateway
                            .GetDto<SessionIdRow>("SELECT @@SPID AS Spid", null, context.Ct).ConfigureAwait(false);
                        context.Counters.Success();
                    }

                    await StopServerAsync(context, check).ConfigureAwait(false);
                    await StartServerAsync(context, check).ConfigureAwait(false);

                    // Deliberately no pool clear here: the point is to measure
                    // what the caller sees when the provider hands back a handle
                    // that no longer has a server behind it.
                    int attempts = 0;
                    int failures = 0;
                    bool recovered = false;
                    List<string> observed = new List<string>();

                    while (attempts < 10 && !recovered)
                    {
                        attempts++;
                        Exception? failure = await PhaseContext.CaptureAsync(() =>
                            context.Workspace.Gateway.GetRaw("SELECT 1 AS Probe", null, context.Ct))
                            .ConfigureAwait(false);

                        if (failure == null)
                        {
                            recovered = true;
                            context.Counters.Success();
                            break;
                        }

                        failures++;
                        observed.Add(PhaseContext.DescribeProviderFailure(failure));
                        context.Counters.ExpectedFailure();
                        await Task.Delay(TimeSpan.FromSeconds(2), context.Ct).ConfigureAwait(false);
                    }

                    check.Note("the scenario's own bounded retry needed " +
                               attempts.ToString(CultureInfo.InvariantCulture) + " attempt(s), " +
                               failures.ToString(CultureInfo.InvariantCulture) + " of which failed");

                    if (observed.Count > 0)
                        check.Note("first stale-handle failure: " + observed[0]);
                    else
                        check.Note("no stale handle was ever handed back: the pool had already discarded them");

                    check.That(recovered,
                        "the gateway never recovered within 10 attempts after the server returned");
                    check.Note("recovery is the caller's loop, not the library's: NekoLib.Data has no retry policy " +
                               "and this run does not ask for one");
                });
        }

        private static Task ContainerRestart(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.ContainerRestart,
                "the scenario database survives a container restart and its contents are unchanged",
                async check =>
                {
                    string before = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);

                    bool restarted = context.Adopted!.Restart(out string diagnostic);
                    check.That(restarted, "docker restart failed: " + diagnostic);
                    context.Artifacts.Event("container", json =>
                    {
                        json.Prop("action", "restart");
                        json.Prop("result", diagnostic);
                    });

                    await WaitReadyAsync(context, check).ConfigureAwait(false);
                    context.Workspace.Factory.ClearProviderPool();

                    bool exists = await context.Probe
                        .DatabaseExistsAsync(context.DatabaseName, context.Ct).ConfigureAwait(false);

                    check.That(exists, "the scenario database did not survive the restart");
                    check.Note("the container carries no volume, but its writable layer survives a restart, " +
                               "so 'ephemeral' here means 'no named volume' and not 'wiped on restart'");

                    string after = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);

                    check.Equal(before, after, "digest across the container restart");
                    await RecoverAsync(context, check, clearPool: false).ConfigureAwait(false);
                });
        }

        private static Task SchemaRecreation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, FaultKinds.SchemaRecreation,
                "the schema can be dropped and rebuilt deterministically, and ordinary work follows",
                async check =>
                {
                    string before = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);

                    await Schema.ScenarioSchema.CreateAsync(context.Workspace.Gateway, context.Ct)
                        .ConfigureAwait(false);
                    await Schema.ScenarioSchema.SeedAsync(context.Workspace.Gateway, context.Seed, context.Ct)
                        .ConfigureAwait(false);
                    context.Counters.Success();

                    string after = await Schema.ScenarioSchema
                        .DigestAsync(context.Workspace.Gateway, context.Ct).ConfigureAwait(false);

                    check.Equal(before, after,
                        "the recreated schema does not hold the same data as the original seed");
                    check.Note("digest " + after + " - the same seed rebuilds the same database");

                    await RecoverAsync(context, check, clearPool: false).ConfigureAwait(false);
                });
        }

        private static async Task StopServerAsync(PhaseContext context, Check check)
        {
            bool stopped = context.Adopted!.Stop(out string diagnostic);
            check.That(stopped, "docker stop failed: " + diagnostic);

            context.Artifacts.Event("container", json =>
            {
                json.Prop("action", "stop");
                json.Prop("result", diagnostic);
            });

            DateTime deadline = DateTime.UtcNow + DownBudget;
            while (DateTime.UtcNow < deadline)
            {
                if (!await context.Probe.IsAvailableAsync(3, context.Ct).ConfigureAwait(false))
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500), context.Ct).ConfigureAwait(false);
            }

            throw new CheckFailure("the server was still answering " +
                DownBudget.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "s after docker stop");
        }

        private static async Task StartServerAsync(PhaseContext context, Check check)
        {
            bool started = context.Adopted!.Start(out string diagnostic);
            check.That(started, "docker start failed: " + diagnostic);

            context.Artifacts.Event("container", json =>
            {
                json.Prop("action", "start");
                json.Prop("result", diagnostic);
            });

            await WaitReadyAsync(context, check).ConfigureAwait(false);
        }

        private static async Task WaitReadyAsync(PhaseContext context, Check check)
        {
            ReadinessResult engine = await context.Probe
                .WaitUntilReadyAsync(ReadyBudget, context.Ct).ConfigureAwait(false);

            check.That(engine.Ready,
                "the engine did not become ready within " +
                ReadyBudget.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "s: " + engine.Detail);

            ReadinessResult database = await context.Probe
                .WaitUntilDatabaseReadyAsync(context.DatabaseName, ReadyBudget, context.Ct).ConfigureAwait(false);

            check.That(database.Ready,
                "the scenario database did not come online: " + database.Detail);

            check.Note("server ready after " + engine.Attempts + " probe(s); database ready after " +
                       database.Attempts + " probe(s)");
        }

        /// <summary>
        /// The post-recovery requirement every fault shares: ordinary work
        /// succeeds again, through the same gateway and through a connection the
        /// pool has to build fresh.
        /// </summary>
        private static async Task RecoverAsync(PhaseContext context, Check check, bool clearPool)
        {
            if (clearPool) context.Workspace.Factory.ClearProviderPool();

            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway
                .GetRaw("SELECT COUNT(*) AS Total FROM Part", null, context.Ct).ConfigureAwait(false);

            check.Equal(1, rows.Count, "recovery probe row count");
            check.Equal(Schema.ScenarioSchema.PartCount,
                long.Parse(rows[0]["Total"].Value, CultureInfo.InvariantCulture),
                "parts visible after recovery");

            context.Counters.Success();

            using (DbSession session = await context.Workspace.Gateway
                .OpenSessionAsync(context.Ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                await context.Workspace.Gateway
                    .GetRaw("SELECT 1 AS Probe", null, session, context.Ct).ConfigureAwait(false);
                session.Commit();
                context.Counters.Success();
            }
        }

        private static async Task<bool> WaitForMarkerAsync(PhaseContext context, string marker, Task running)
        {
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

            while (DateTime.UtcNow < deadline)
            {
                if (running.IsCompleted) return false;

                if (await context.Probe.IsMarkerExecutingAsync(marker, context.Ct).ConfigureAwait(false))
                    return true;

                await Task.Delay(TimeSpan.FromMilliseconds(100), context.Ct).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Holds until the planned monotonic offset, doing ordinary work so the
        /// gap between faults is measured traffic rather than an idle wait.
        /// </summary>
        private static async Task WaitForOffsetAsync(
            PhaseContext context,
            FaultEvent planned,
            DateTime campaignStartUtc,
            GatewayWorkspace background)
        {
            DateTime due = campaignStartUtc.AddSeconds(planned.OffsetSeconds);

            while (DateTime.UtcNow < due)
            {
                context.Ct.ThrowIfCancellationRequested();

                try
                {
                    await background.Gateway
                        .GetRaw("SELECT COUNT(*) AS Total FROM Movement", null, context.Ct).ConfigureAwait(false);
                    context.Counters.Success();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Steady-state traffic between faults is not itself an
                    // assertion; a failure here is counted and the schedule
                    // continues so the planned fault still happens on time.
                    context.Counters.UnexpectedFailure();
                }

                TimeSpan remaining = due - DateTime.UtcNow;
                TimeSpan pause = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
                if (pause > TimeSpan.Zero)
                    await Task.Delay(pause, context.Ct).ConfigureAwait(false);
            }
        }

        private static async Task<long> CountNoteAsync(PhaseContext context, string note)
        {
            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway.GetRaw(
                "SELECT COUNT(*) AS Total FROM Movement WHERE Note = @p1",
                new Dictionary<string, object?> { ["@p1"] = note },
                context.Ct).ConfigureAwait(false);

            return long.Parse(rows[0]["Total"].Value, CultureInfo.InvariantCulture);
        }
    }
}

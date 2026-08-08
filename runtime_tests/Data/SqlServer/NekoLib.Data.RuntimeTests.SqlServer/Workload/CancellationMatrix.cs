#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Model;
using NekoLib.Data.RuntimeTests.SqlServer.Reporting;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// Cancellation, in the two forms that are genuinely different claims.
    /// <para/>
    /// Refusing an already-cancelled token proves the token is consulted before
    /// the work starts. Interrupting a command the server has already begun
    /// proves something else entirely, and until now NekoLib had no evidence for
    /// it: a local file database finishes faster than a cancellation can be
    /// timed. A remote engine that can be told to wait is what makes the second
    /// question answerable.
    /// <para/>
    /// The start signal is server-visible on purpose. A wall-clock sleep before
    /// cancelling would prove only that time passed; asking
    /// <c>sys.dm_exec_requests</c> whether this exact batch is executing is what
    /// makes "cancelled after it started" a fact rather than an assumption.
    /// </summary>
    internal static class CancellationMatrix
    {
        private const string Phase = "cancellation";
        private const string WaitDuration = "00:00:20";

        private static readonly TimeSpan StartObservationBudget = TimeSpan.FromSeconds(20);

        public static async Task RunAsync(PhaseContext context)
        {
            // One recovery-probe workspace for the whole phase, not one per
            // check. Each distinct application name is a distinct provider
            // pool, and minting six of them made the scenario pay for six
            // separate connection establishments on a machine that was already
            // saturated - a cost this scenario invented for itself. Clearing
            // the pool before each use still guarantees a genuinely fresh
            // physical connection, which is what the check is about.
            using (GatewayWorkspace recoveryProbe = CreateRecoveryProbe(context))
            {
                await PreCancelledToken(context).ConfigureAwait(false);
                await MidFlight(context, recoveryProbe, "raw", RunRawAsync).ConfigureAwait(false);
                await MidFlight(context, recoveryProbe, "typed", RunTypedAsync).ConfigureAwait(false);
                await MidFlight(context, recoveryProbe, "dynamic", RunDynamicAsync, BlockingLock.TakeAsync)
                    .ConfigureAwait(false);
                await MidFlight(context, recoveryProbe, "callback", RunCallbackAsync).ConfigureAwait(false);
#if NET6_0_OR_GREATER
                await MidFlight(context, recoveryProbe, "streaming", RunStreamingAsync).ConfigureAwait(false);
#else
                context.Runner.Skip(Phase, "mid-flight-streaming",
                    "a stream interrupted after execution began reports a cancelled terminal",
                    "the streaming gateway does not exist on net481");
#endif
                await MidFlightInTransaction(context, recoveryProbe).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// A second gateway on its own pool, used only to prove that ordinary
        /// work reaches the server through a connection the cancelled call
        /// never touched.
        /// <para/>
        /// Its connect timeout is deliberately generous: this is a probe, not a
        /// measurement of how fast the server accepts logins, and a loaded host
        /// must not turn a slow login into a reported cancellation defect.
        /// </summary>
        private static GatewayWorkspace CreateRecoveryProbe(PhaseContext context)
        {
            return new GatewayWorkspace(
                context.Endpoint.BuildConnectionString(
                    context.DatabaseName,
                    connectTimeoutSeconds: 60,
                    applicationName: "NekoLib.E4-SQL.recovery"),
                GatewayWorkspace.DefaultOptions());
        }

        private static Task PreCancelledToken(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "pre-cancelled-token",
                "every entry point refuses a token that was already cancelled",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;
                    List<string> ignored = new List<string>();
                    int probes = 0;

                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        CancellationToken dead = source.Token;

                        probes++;
                        await Probe(ignored,"OpenSessionAsync", async () =>
                        {
                            using (DbSession session = await gateway.OpenSessionAsync(dead).ConfigureAwait(false))
                                return;
                        }).ConfigureAwait(false);

                        probes++;
                        await Probe(ignored,"GetRaw", () =>
                            gateway.GetRaw("SELECT 1 AS Probe", null, dead)).ConfigureAwait(false);

                        probes++;
                        await Probe(ignored,"GetDto", () =>
                            gateway.GetDto<PartRow>(SelectParts(), dead)).ConfigureAwait(false);

                        probes++;
                        await Probe(ignored,"GetDynamic", () =>
                            gateway.GetDynamic(SelectParts(), dead)).ConfigureAwait(false);

                        probes++;
                        await Probe(ignored,"ReadDto", () =>
                            gateway.ReadDto<PartRow>(SelectParts(), _ => { }, dead)).ConfigureAwait(false);

                        // A write, because refusing to read is cheap and
                        // refusing to write is the one that protects the data.
                        probes++;
                        await Probe(ignored,"Insert", () => gateway.Insert(
                            "INSERT INTO Movement (PartId, OccurredAt, Kind, Quantity, Note) " +
                            "VALUES (@p1, @p2, @p3, @p4, @p5)",
                            new Dictionary<string, object?>
                            {
                                ["@p1"] = 1,
                                ["@p2"] = DateTime.UtcNow,
                                ["@p3"] = "Ajuste",
                                ["@p4"] = 1,
                                ["@p5"] = "should never exist"
                            },
                            dead)).ConfigureAwait(false);

#if NET6_0_OR_GREATER
                        probes++;
                        await Probe(ignored,"StreamDto", async () =>
                        {
                            await foreach (PartRow _ in gateway.StreamDto<PartRow>(SelectParts(), dead)
                                .ConfigureAwait(false))
                            {
                            }
                        }).ConfigureAwait(false);
#endif
                    }

                    for (int i = 0; i < probes; i++) context.Counters.Cancellation();
                    check.Note(probes + " entry point(s) probed with a dead token on this target");

                    check.That(ignored.Count == 0,
                        "these entry points ran despite an already-cancelled token: " +
                        string.Join(", ", ignored.ToArray()));

                    long leaked = await CountNoteAsync(context, "should never exist").ConfigureAwait(false);
                    check.Equal(0, leaked, "rows written by the refused insert");
                });
        }

        private static async Task Probe(List<string> ignored, string name, Func<Task> call)
        {
            try
            {
                await call().ConfigureAwait(false);
                ignored.Add(name + " completed");
            }
            catch (Exception ex)
            {
                CancellationShape shape = PhaseContext.ClassifyCancellation(ex);
                if (!shape.IsCancellation) ignored.Add(name + " -> " + shape.Detail);
            }
        }

        /// <summary>
        /// The shared shape of a mid-flight check: start a command that the
        /// server is holding open, wait until the server confirms it is
        /// executing, cancel, then require a cancellation terminal and prove the
        /// gateway and the pool still work.
        /// <para/>
        /// <paramref name="prepare"/> exists for the builder-driven paths, which
        /// cannot emit <c>WAITFOR</c>. Those are held open by a lock taken from
        /// a control connection instead, and the lock has to be released before
        /// the recovery probe or the probe would queue behind it.
        /// </summary>
        private static Task MidFlight(
            PhaseContext context,
            GatewayWorkspace recoveryProbe,
            string shape,
            Func<PhaseContext, string, CancellationToken, Task> start,
            Func<PhaseContext, Task<BlockingLock>>? prepare = null)
        {
            return context.Runner.RunAsync(Phase, "mid-flight-" + shape,
                "a " + shape + " read interrupted after the server began executing reports a cancellation terminal",
                async check =>
                {
                    string marker = NewMarker(shape);
                    context.Workspace.ResetTerminals();

                    BlockingLock? held = prepare == null
                        ? null
                        : await prepare(context).ConfigureAwait(false);

                    try
                    {
                        using (CancellationTokenSource source = CancellationTokenSource
                            .CreateLinkedTokenSource(context.Ct))
                        {
                            Stopwatch clock = Stopwatch.StartNew();
                            Task running = start(context, marker, source.Token);

                            try
                            {
                                StartObservation observation = await WaitForStartAsync(context, marker, running)
                                    .ConfigureAwait(false);

                                check.That(observation.Started, observation.Detail);

                                check.Note((held == null ? "held open by WAITFOR; " : "held open by a row lock; ") +
                                           "server confirmed execution after " +
                                           clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms");

                                source.Cancel();

                                Exception? failure = await PhaseContext.CaptureAsync(() => running)
                                    .ConfigureAwait(false);
                                clock.Stop();

                                check.That(failure != null,
                                    "the call completed normally despite being cancelled mid-flight");

                                CancellationShape classified = PhaseContext.ClassifyCancellation(failure!);
                                check.Note("terminal: " + classified.Detail);
                                check.That(classified.IsCancellation,
                                    "expected a cancellation terminal, got " + classified.Detail);

                                check.That(clock.Elapsed < TimeSpan.FromSeconds(18),
                                    "the call took " +
                                    clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) +
                                    "s, which is long enough that it may have run to completion rather than " +
                                    "been interrupted");

                                context.Counters.Cancellation();
                            }
                            finally
                            {
                                // Whatever the check concluded, the command must
                                // not be left running: an unobserved task holds a
                                // pooled connection and its exception would
                                // surface in an unrelated phase.
                                source.Cancel();
                                await PhaseContext.CaptureAsync(() => running).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        if (held != null) await held.ReleaseAsync().ConfigureAwait(false);
                    }

                    check.Equal(0, context.Workspace.Succeeded, "success terminals for a cancelled call");
                    check.Note("lifecycle counters: " + context.Workspace.DescribeTerminals());

                    await AssertRecoveryAsync(context, recoveryProbe, check).ConfigureAwait(false);
                });
        }

        /// <summary>
        /// The same interruption, but with an open transaction underneath, so
        /// the question becomes what happened to the work already inside it.
        /// </summary>
        private static Task MidFlightInTransaction(PhaseContext context, GatewayWorkspace recoveryProbe)
        {
            return context.Runner.RunAsync(Phase, "mid-flight-in-transaction",
                "a cancelled command inside a transaction leaves no committed effect and the session disposes cleanly",
                async check =>
                {
                    string marker = NewMarker("transaction");
                    const string note = "e4sql-cancel-transaction";
                    var gateway = context.Workspace.Gateway;

                    await DeleteNoteAsync(context, note).ConfigureAwait(false);
                    context.Workspace.ResetTerminals();

                    using (CancellationTokenSource source = CancellationTokenSource
                        .CreateLinkedTokenSource(context.Ct))
                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();

                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", new Dictionary<string, object?>
                            {
                                ["PartId"] = 1,
                                ["OccurredAt"] = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc),
                                ["Kind"] = "Ajuste",
                                ["Quantity"] = 4,
                                ["Note"] = note
                            }),
                            session,
                            context.Ct).ConfigureAwait(false);

                        Task running = gateway.GetRaw(LongBatch(marker), null, session, source.Token);

                        StartObservation observation = await WaitForStartAsync(context, marker, running)
                            .ConfigureAwait(false);
                        check.That(observation.Started,
                            "the transactional batch was not observed executing: " + observation.Detail);

                        source.Cancel();

                        Exception? failure = await PhaseContext.CaptureAsync(() => running).ConfigureAwait(false);
                        check.That(failure != null, "the transactional call completed despite cancellation");

                        CancellationShape classified = PhaseContext.ClassifyCancellation(failure!);
                        check.Note("terminal: " + classified.Detail);
                        check.That(classified.IsCancellation,
                            "expected a cancellation terminal, got " + classified.Detail);

                        context.Counters.Cancellation();

                        // Whether the provider dooms the transaction is its
                        // decision to make; what must hold is that nothing
                        // reaches the database and that disposal does not throw.
                        Exception? rollbackFailure = null;
                        try
                        {
                            session.Rollback();
                        }
                        catch (Exception ex)
                        {
                            rollbackFailure = ex;
                        }

                        check.Note(rollbackFailure == null
                            ? "the transaction rolled back cleanly after the cancellation"
                            : "rollback after cancellation reported " +
                              PhaseContext.DescribeProviderFailure(rollbackFailure));
                    }

                    long survived = await CountNoteAsync(context, note).ConfigureAwait(false);
                    check.Equal(0, survived, "rows committed by the cancelled transaction");

                    await AssertRecoveryAsync(context, recoveryProbe, check).ConfigureAwait(false);
                });
        }

        /// <summary>
        /// After every cancellation: the same gateway must work, and so must a
        /// connection taken from a pool the cancelled call never touched.
        /// </summary>
        private static async Task AssertRecoveryAsync(
            PhaseContext context,
            GatewayWorkspace recoveryProbe,
            Check check)
        {
            List<Dictionary<string, RecordItem>> sameGateway = await context.Workspace.Gateway
                .GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);

            check.Equal(1, sameGateway.Count, "ordinary work through the same gateway after cancellation");
            context.Counters.Success();

            // Emptying the pool first is what makes the next call establish a
            // new physical connection rather than reuse the one the previous
            // check left warm.
            recoveryProbe.Factory.ClearProviderPool();

            List<PartRow> rows = await recoveryProbe.Gateway
                .GetDto<PartRow>(SelectParts(), context.Ct).ConfigureAwait(false);

            check.That(rows.Count > 0, "a newly acquired pooled connection returned no rows after cancellation");
            context.Counters.Success();
        }

        /// <summary>
        /// Polls the server until it reports the marked batch as executing, or
        /// until the call finishes on its own, or the budget expires.
        /// <para/>
        /// The two ways of failing are reported separately. "The call finished
        /// before anyone could cancel it" and "the server never admitted to
        /// running it" are different problems, and a single message covering
        /// both sent an earlier version of this scenario looking in the wrong
        /// place.
        /// </summary>
        private static async Task<StartObservation> WaitForStartAsync(
            PhaseContext context,
            string marker,
            Task running)
        {
            DateTime deadline = DateTime.UtcNow + StartObservationBudget;

            while (DateTime.UtcNow < deadline)
            {
                if (running.IsCompleted)
                {
                    Exception? outcome = await PhaseContext.CaptureAsync(() => running).ConfigureAwait(false);
                    return new StartObservation(false,
                        outcome == null
                            ? "the call finished on its own before it could be cancelled, so nothing was held open"
                            : "the call failed before it could be cancelled: " +
                              PhaseContext.DescribeProviderFailure(outcome));
                }

                if (await context.Probe.IsMarkerExecutingAsync(marker, context.Ct).ConfigureAwait(false))
                    return new StartObservation(true, "executing");

                await Task.Delay(TimeSpan.FromMilliseconds(100), context.Ct).ConfigureAwait(false);
            }

            return new StartObservation(false,
                "the call was still pending but the server never reported the marked batch as executing within " +
                StartObservationBudget.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "s");
        }

        /// <summary>Why the start observation succeeded or failed.</summary>
        private sealed class StartObservation
        {
            public StartObservation(bool started, string detail)
            {
                Started = started;
                Detail = detail;
            }

            public bool Started { get; }
            public string Detail { get; }
        }

        private static Task RunRawAsync(PhaseContext context, string marker, CancellationToken ct) =>
            context.Workspace.Gateway.GetRaw(LongBatch(marker), null, ct);

        private static Task RunTypedAsync(PhaseContext context, string marker, CancellationToken ct) =>
            context.Workspace.Gateway.GetDto<PartRow>(LongBatch(marker), null, ct);

        private static Task RunDynamicAsync(PhaseContext context, string marker, CancellationToken ct) =>
            context.Workspace.Gateway.GetDynamic(LongBatchBuilder(marker), ct);

        private static Task RunCallbackAsync(PhaseContext context, string marker, CancellationToken ct) =>
            context.Workspace.Gateway.ReadRaw(LongBatch(marker), _ => { }, ct);

#if NET6_0_OR_GREATER
        private static async Task RunStreamingAsync(PhaseContext context, string marker, CancellationToken ct)
        {
            await foreach (Dictionary<string, RecordItem> _ in context.Workspace.Gateway
                .StreamRaw(LongBatch(marker), null, ct).ConfigureAwait(false))
            {
            }
        }
#endif

        /// <summary>
        /// A batch that is known to be executing and known not to have finished:
        /// the marker comment makes it findable in the server's own view of
        /// running requests, and the delay holds it there long enough to cancel.
        /// </summary>
        private static string LongBatch(string marker)
        {
            return "/*" + marker + "*/ WAITFOR DELAY '" + WaitDuration + "'; " +
                   "SELECT Id, WarehouseId, Sku, Description, Quantity, UnitPrice, Weight, Serial, " +
                   "Discontinued, UpdatedAt FROM Part";
        }

        /// <summary>
        /// The builder-driven variant. <c>QueryBuilder</c> cannot emit
        /// <c>WAITFOR</c>, so this query is an ordinary read that is held open
        /// by a lock another connection is holding — the way a real stalled
        /// query looks. The marker rides in the <c>From</c> fragment so the
        /// batch is still findable in the server's list of running requests.
        /// </summary>
        private static QueryBuilder LongBatchBuilder(string marker)
        {
            return new QueryBuilder()
                .Select("Id", "Sku", "Quantity")
                .From("Part /*" + marker + "*/")
                .Where("Id = @p1", BlockingLock.LockedPartId);
        }

        private static QueryBuilder SelectParts()
        {
            return new QueryBuilder()
                .Select("Id", "WarehouseId", "Sku", "Description", "Quantity",
                        "UnitPrice", "Weight", "Serial", "Discontinued", "UpdatedAt")
                .From("Part");
        }

        private static string NewMarker(string shape) =>
            "NEKOLIB-CANCEL-" + shape + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        private static async Task<long> CountNoteAsync(PhaseContext context, string note)
        {
            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway.GetRaw(
                "SELECT COUNT(*) AS Total FROM Movement WHERE Note = @p1",
                new Dictionary<string, object?> { ["@p1"] = note },
                context.Ct).ConfigureAwait(false);

            return long.Parse(rows[0]["Total"].Value, CultureInfo.InvariantCulture);
        }

        private static Task DeleteNoteAsync(PhaseContext context, string note)
        {
            return context.Workspace.Gateway.Delete(
                "DELETE FROM Movement WHERE Note = @p1",
                new Dictionary<string, object?> { ["@p1"] = note },
                context.Ct);
        }
    }
}

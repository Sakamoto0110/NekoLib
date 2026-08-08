#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Connection;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Model;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// Connection, session, pool and ownership behaviour against the real
    /// provider.
    /// <para/>
    /// These are the claims that fake ADO.NET objects cannot settle. A fake
    /// connection has no pool, so "the gateway reuses pooled connections" and
    /// "bounded concurrency does not leak a checked-out connection" are only
    /// answerable here, and only by asking the server itself which session
    /// served each call.
    /// </summary>
    internal static class ConnectionMatrix
    {
        private const string Phase = "connection";

        public static async Task RunAsync(PhaseContext context)
        {
            await OpenCloseRepeated(context).ConfigureAwait(false);
            await FactoryOwnership(context).ConfigureAwait(false);
            await SessionAffinity(context).ConfigureAwait(false);
            await SessionUseAfterDispose(context).ConfigureAwait(false);
            await PoolReuseSequential(context).ConfigureAwait(false);
            await PoolBoundedConcurrency(context).ConfigureAwait(false);
        }

        private static Task OpenCloseRepeated(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "open-close-repeated",
                "repeated gateway-created sessions open, work, and dispose without leaving a server session behind",
                async check =>
                {
                    const int cycles = 20;
                    long before = context.Workspace.Factory.Created;

                    for (int i = 0; i < cycles; i++)
                    {
                        using (DbSession session = await context.Workspace.Gateway
                            .OpenSessionAsync(context.Ct).ConfigureAwait(false))
                        {
                            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway
                                .GetRaw("SELECT 1 AS Probe", null, session, context.Ct).ConfigureAwait(false);

                            check.Equal(1, rows.Count, "probe row count");
                            context.Counters.Success();
                        }
                    }

                    long created = context.Workspace.Factory.Created - before;
                    check.Equal(cycles, created, "connections the factory was asked to build");

                    // The pool keeps the physical connections; what must not
                    // survive is a session still attached to the database.
                    int sessions = await context.Probe
                        .CountScenarioSessionsAsync(context.DatabaseName, context.Ct).ConfigureAwait(false);

                    check.Note("server sessions attributed to this scenario after " + cycles +
                               " open/dispose cycles: " + sessions);
                    check.That(sessions <= 2,
                        "expected the pool to hold at most a couple of idle sessions, found " + sessions);
                });
        }

        private static Task FactoryOwnership(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "factory-ownership",
                "a context-owned factory is disposed with the context and an external one is not",
                async check =>
                {
                    using (GatewayWorkspace owned = new GatewayWorkspace(
                        context.Workspace.ConnectionString,
                        GatewayWorkspace.DefaultOptions(),
                        DbConnectionFactoryOwnership.ContextOwned))
                    {
                        await owned.Gateway.GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);
                        context.Counters.Success();

                        check.Equal(0, owned.Factory.DisposeCount, "factory disposals before the context is disposed");
                        owned.Context.Dispose();
                        check.Equal(1, owned.Factory.DisposeCount, "factory disposals after disposing a context-owned context");
                    }

                    GatewayWorkspace external = new GatewayWorkspace(
                        context.Workspace.ConnectionString,
                        GatewayWorkspace.DefaultOptions(),
                        DbConnectionFactoryOwnership.External);

                    try
                    {
                        await external.Gateway.GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);
                        context.Counters.Success();

                        external.Context.Dispose();
                        check.Equal(0, external.Factory.DisposeCount,
                            "factory disposals after disposing a context that does not own the factory");
                    }
                    finally
                    {
                        external.Dispose();
                    }

                    check.Note("ownership is honoured in both directions, which is what DATA-014 asked for");
                });
        }

        private static Task SessionAffinity(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "session-affinity",
                "a session opened by one context is refused by another",
                async check =>
                {
                    using (GatewayWorkspace other = new GatewayWorkspace(
                        context.Workspace.ConnectionString,
                        GatewayWorkspace.DefaultOptions()))
                    using (DbSession session = await context.Workspace.Gateway
                        .OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        Exception? failure = await PhaseContext.CaptureAsync(() =>
                            other.Gateway.GetRaw("SELECT 1 AS Probe", null, session, context.Ct))
                            .ConfigureAwait(false);

                        check.That(failure is InvalidOperationException,
                            "expected InvalidOperationException, got " +
                            (failure == null ? "no exception at all" : failure.GetType().Name));

                        context.Counters.ExpectedFailure();

                        // The session must still be usable by the context that
                        // owns it: a rejected borrow cannot poison it.
                        List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway
                            .GetRaw("SELECT 1 AS Probe", null, session, context.Ct).ConfigureAwait(false);

                        check.Equal(1, rows.Count, "rows through the owning context after the refusal");
                        context.Counters.Success();
                    }
                });
        }

        private static Task SessionUseAfterDispose(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "session-use-after-dispose",
                "a disposed session refuses further work and disposing twice is harmless",
                async check =>
                {
                    DbSession session = await context.Workspace.Gateway
                        .OpenSessionAsync(context.Ct).ConfigureAwait(false);

                    await context.Workspace.Gateway
                        .GetRaw("SELECT 1 AS Probe", null, session, context.Ct).ConfigureAwait(false);
                    context.Counters.Success();

                    session.Dispose();
                    session.Dispose();

                    Exception? failure = await PhaseContext.CaptureAsync(() =>
                        context.Workspace.Gateway.GetRaw("SELECT 1 AS Probe", null, session, context.Ct))
                        .ConfigureAwait(false);

                    check.That(failure != null, "expected the disposed session to refuse work");
                    check.Note("use after dispose reported " + failure!.GetType().Name +
                               ": " + Flatten(failure.Message));

                    check.That(failure is ObjectDisposedException || failure is InvalidOperationException,
                        "expected ObjectDisposedException or InvalidOperationException, got " + failure.GetType().Name);

                    context.Counters.ExpectedFailure();
                });
        }

        private static Task PoolReuseSequential(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "pool-reuse-sequential",
                "sequential gateway calls are served by one pooled connection",
                async check =>
                {
                    const int calls = 24;
                    HashSet<int> sessions = new HashSet<int>();

                    for (int i = 0; i < calls; i++)
                    {
                        List<SessionIdRow> rows = await context.Workspace.Gateway
                            .GetDto<SessionIdRow>("SELECT @@SPID AS Spid", null, context.Ct).ConfigureAwait(false);

                        check.Equal(1, rows.Count, "session id row count");
                        sessions.Add(rows[0].Spid);
                        context.Counters.Success();
                    }

                    check.Note(calls + " sequential calls used " + sessions.Count + " distinct server session(s)");
                    check.That(sessions.Count == 1,
                        "expected one pooled connection to serve every sequential call, saw " + sessions.Count);
                });
        }

        private static Task PoolBoundedConcurrency(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "pool-bounded-concurrency",
                "concurrency beyond the pool limit still completes and never exceeds it",
                async check =>
                {
                    const int poolLimit = 8;
                    const int workers = 32;

                    string connectionString = context.Endpoint.BuildConnectionString(
                        context.DatabaseName,
                        maxPoolSize: poolLimit,
                        minPoolSize: 0,
                        pooling: true,
                        connectTimeoutSeconds: 15,
                        applicationName: "NekoLib.E4-SQL.pool");

                    using (GatewayWorkspace bounded = new GatewayWorkspace(
                        connectionString,
                        GatewayWorkspace.DefaultOptions()))
                    {
                        HashSet<int> observed = new HashSet<int>();
                        object sync = new object();
                        Task[] running = new Task[workers];

                        for (int i = 0; i < workers; i++)
                        {
                            running[i] = Task.Run(async () =>
                            {
                                // A short server-side delay guarantees the calls
                                // actually overlap; without it the pool could
                                // serve them one after another and the check
                                // would prove nothing.
                                List<SessionIdRow> rows = await bounded.Gateway.GetDto<SessionIdRow>(
                                    "WAITFOR DELAY '00:00:00.150'; SELECT @@SPID AS Spid",
                                    null,
                                    context.Ct).ConfigureAwait(false);

                                lock (sync)
                                {
                                    foreach (SessionIdRow row in rows) observed.Add(row.Spid);
                                }

                                context.Counters.Success();
                            }, context.Ct);
                        }

                        await Task.WhenAll(running).ConfigureAwait(false);

                        check.Note(workers + " concurrent calls against a pool limit of " + poolLimit +
                                   " used " + observed.Count + " distinct server session(s)");

                        check.That(observed.Count <= poolLimit,
                            "the pool limit was exceeded: " + observed.Count + " sessions for a limit of " + poolLimit);

                        // A leaked checked-out connection shows up as the next
                        // burst timing out rather than as an error here, so the
                        // pool is exercised again immediately.
                        for (int i = 0; i < poolLimit * 2; i++)
                        {
                            await bounded.Gateway.GetDto<SessionIdRow>("SELECT @@SPID AS Spid", null, context.Ct)
                                .ConfigureAwait(false);
                            context.Counters.Success();
                        }

                        check.Note("a following burst of " + (poolLimit * 2) +
                                   " calls completed, so nothing stayed checked out");

                        bounded.Factory.ClearProviderPool();
                    }
                });
        }

        private static string Flatten(string text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }
}

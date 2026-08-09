#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// Parameterized DML, the four ways a transaction can end, and what the
    /// gateway reports when the provider refuses a statement.
    /// <para/>
    /// Every check here leaves the database exactly as it found it, and the
    /// phase proves that by comparing the seed digest before and after. A
    /// transaction test that changed the data would make every later phase
    /// depend on the order it ran in.
    /// </summary>
    internal static class TransactionMatrix
    {
        private const string Phase = "transaction";
        private const string Marker = "e4sql-transaction-probe";

        public static async Task RunAsync(PhaseContext context)
        {
            // Inside a check, not before one. Taken outside, a transport error
            // here escapes the whole assertion mechanism and ends the process -
            // which is exactly how the first soak died.
            string before = string.Empty;
            await context.Runner.RunAsync(Phase, "state-baseline",
                "the seeded data is readable before the transaction phase begins",
                async check =>
                {
                    before = await Schema.ScenarioSchema.DigestAsync(context.Workspace.Gateway, context.Ct)
                        .ConfigureAwait(false);
                    check.Note("digest " + before);
                }).ConfigureAwait(false);

            if (before.Length == 0)
            {
                context.Runner.Skip(Phase, "state-restored",
                    "the transaction phase leaves the seeded data exactly as it found it",
                    "no baseline digest was captured, so there is nothing to compare against");
                return;
            }

            await ParameterizedDml(context).ConfigureAwait(false);
            await CommitAndRollback(context).ConfigureAwait(false);
            await ExceptionRollback(context).ConfigureAwait(false);
            await DisposeWithoutCommit(context).ConfigureAwait(false);
            await TransactionAfterCommit(context).ConfigureAwait(false);
            await ProviderErrorPropagation(context).ConfigureAwait(false);

            await context.Runner.RunAsync(Phase, "state-restored",
                "the transaction phase leaves the seeded data exactly as it found it",
                async check =>
                {
                    string after = await Schema.ScenarioSchema.DigestAsync(context.Workspace.Gateway, context.Ct)
                        .ConfigureAwait(false);

                    check.Equal(before, after, "digest before and after the transaction phase");
                    check.Note("digest " + after);
                }).ConfigureAwait(false);
        }

        private static Task ParameterizedDml(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "parameterized-dml",
                "insert, update and delete carry their parameters and report the rows they touched",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;

                    int inserted = await gateway.Insert(
                        new QueryBuilder().InsertInto("Movement", Movement(1, 7, Marker)),
                        context.Ct).ConfigureAwait(false);

                    check.Equal(1, inserted, "rows reported by INSERT");
                    context.Counters.Success();

                    int updated = await gateway.Update(
                        "UPDATE Movement SET Quantity = @p1 WHERE Note = @p2",
                        new Dictionary<string, object?> { ["@p1"] = 11, ["@p2"] = Marker },
                        context.Ct).ConfigureAwait(false);

                    check.Equal(1, updated, "rows reported by UPDATE");
                    context.Counters.Success();

                    List<Dictionary<string, RecordItem>> read = await gateway.GetRaw(
                        "SELECT Quantity FROM Movement WHERE Note = @p1",
                        new Dictionary<string, object?> { ["@p1"] = Marker },
                        context.Ct).ConfigureAwait(false);

                    check.Equal(1, read.Count, "rows found after the update");
                    check.Equal("11", read[0]["Quantity"].Value, "the updated quantity");
                    context.Counters.Success();

                    int deleted = await gateway.Delete(
                        "DELETE FROM Movement WHERE Note = @p1",
                        new Dictionary<string, object?> { ["@p1"] = Marker },
                        context.Ct).ConfigureAwait(false);

                    check.Equal(1, deleted, "rows reported by DELETE");
                    context.Counters.Success();

                    check.Note("Delete has no QueryBuilder overload, so it is raw SQL by design");
                });
        }

        private static Task CommitAndRollback(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "commit-and-explicit-rollback",
                "a committed transaction persists and an explicitly rolled back one leaves nothing",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();
                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", Movement(2, 3, Marker + "-commit")),
                            session,
                            context.Ct).ConfigureAwait(false);
                        session.Commit();
                        context.Counters.Success();
                    }

                    check.Equal(1, await CountAsync(context, Marker + "-commit").ConfigureAwait(false),
                        "rows after commit");

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();
                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", Movement(2, 3, Marker + "-rollback")),
                            session,
                            context.Ct).ConfigureAwait(false);
                        session.Rollback();
                        context.Counters.Success();
                    }

                    check.Equal(0, await CountAsync(context, Marker + "-rollback").ConfigureAwait(false),
                        "rows after an explicit rollback");

                    await CleanupAsync(context, Marker + "-commit").ConfigureAwait(false);
                });
        }

        private static Task ExceptionRollback(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "exception-rollback",
                "a statement the engine rejects mid-transaction leaves the earlier statements unapplied",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;
                    context.Workspace.ResetTerminals();

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();
                        try
                        {
                            await gateway.Insert(
                                new QueryBuilder().InsertInto("Movement", Movement(3, 5, Marker + "-fault")),
                                session,
                                context.Ct).ConfigureAwait(false);

                            // Kind is NOT NULL in the schema, so this is the
                            // engine's own rejection rather than a fabricated
                            // exception. Rollback is the provider's
                            // implementation and a synthetic throw would never
                            // reach it.
                            Dictionary<string, object?> broken = Movement(3, 5, Marker + "-fault");
                            broken["Kind"] = null;

                            await gateway.Insert(
                                new QueryBuilder().InsertInto("Movement", broken),
                                session,
                                context.Ct).ConfigureAwait(false);

                            throw new CheckFailedSentinel();
                        }
                        catch (CheckFailedSentinel)
                        {
                            session.Rollback();
                            throw new Reporting.CheckFailure("the engine accepted a NULL into a NOT NULL column");
                        }
                        catch (Exception ex)
                        {
                            session.Rollback();
                            check.Note("the engine refused it: " + PhaseContext.DescribeProviderFailure(ex));
                            context.Counters.ExpectedFailure();
                        }
                    }

                    check.Equal(0, await CountAsync(context, Marker + "-fault").ConfigureAwait(false),
                        "rows left behind by the rolled back transaction");

                    check.That(context.Workspace.Failed >= 1,
                        "expected the failure to reach the query-lifecycle error event");
                    check.Note("lifecycle counters for this transaction: " + context.Workspace.DescribeTerminals());

                    // Ordinary work must follow every expected failure.
                    List<Dictionary<string, RecordItem>> probe = await gateway
                        .GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);
                    check.Equal(1, probe.Count, "ordinary work after the rejected statement");
                    context.Counters.Success();
                });
        }

        private static Task DisposeWithoutCommit(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "dispose-without-commit",
                "disposing a session with an open transaction rolls it back",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();
                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", Movement(4, 2, Marker + "-abandoned")),
                            session,
                            context.Ct).ConfigureAwait(false);
                        context.Counters.Success();
                        // No commit, no rollback: the using block is the whole test.
                    }

                    check.Equal(0, await CountAsync(context, Marker + "-abandoned").ConfigureAwait(false),
                        "rows left behind by an abandoned transaction");
                });
        }

        private static Task TransactionAfterCommit(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "transaction-after-commit",
                "a session accepts a new transaction after committing the previous one",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        session.BeginTransaction();
                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", Movement(5, 1, Marker + "-first")),
                            session,
                            context.Ct).ConfigureAwait(false);
                        session.Commit();

                        session.BeginTransaction();
                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", Movement(5, 1, Marker + "-second")),
                            session,
                            context.Ct).ConfigureAwait(false);
                        session.Rollback();

                        context.Counters.Success();
                    }

                    check.Equal(1, await CountAsync(context, Marker + "-first").ConfigureAwait(false),
                        "the committed row survives");
                    check.Equal(0, await CountAsync(context, Marker + "-second").ConfigureAwait(false),
                        "the second, rolled back row does not");

                    await CleanupAsync(context, Marker + "-first").ConfigureAwait(false);
                });
        }

        private static Task ProviderErrorPropagation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "provider-error-propagation",
                "a rejected statement surfaces once, as one error terminal and no success terminal",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;
                    context.Workspace.ResetTerminals();

                    Exception? failure = await PhaseContext.CaptureAsync(() =>
                        gateway.GetRaw("SELECT * FROM NoSuchTableHere", null, context.Ct))
                        .ConfigureAwait(false);

                    check.That(failure != null, "the invalid statement did not fail");
                    check.Note(PhaseContext.DescribeProviderFailure(failure!));
                    context.Counters.ExpectedFailure();

                    check.Equal(1, context.Workspace.Dispatched, "dispatch terminals");
                    check.Equal(0, context.Workspace.Succeeded, "success terminals");
                    check.Equal(1, context.Workspace.Failed, "error terminals");

                    context.Workspace.ResetTerminals();
                    List<Dictionary<string, RecordItem>> probe = await gateway
                        .GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);

                    check.Equal(1, probe.Count, "ordinary work after the provider error");
                    check.Equal(1, context.Workspace.Succeeded, "success terminals for the recovery probe");
                    check.Equal(0, context.Workspace.Failed, "error terminals for the recovery probe");
                    context.Counters.Success();
                });
        }

        private static Dictionary<string, object?> Movement(int partId, int quantity, string note)
        {
            return new Dictionary<string, object?>
            {
                ["PartId"] = partId,
                ["OccurredAt"] = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                ["Kind"] = "Ajuste",
                ["Quantity"] = quantity,
                ["Note"] = note
            };
        }

        private static async Task<long> CountAsync(PhaseContext context, string note)
        {
            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway.GetRaw(
                "SELECT COUNT(*) AS Total FROM Movement WHERE Note = @p1",
                new Dictionary<string, object?> { ["@p1"] = note },
                context.Ct).ConfigureAwait(false);

            return long.Parse(rows[0]["Total"].Value, CultureInfo.InvariantCulture);
        }

        private static async Task CleanupAsync(PhaseContext context, string note)
        {
            await context.Workspace.Gateway.Delete(
                "DELETE FROM Movement WHERE Note = @p1",
                new Dictionary<string, object?> { ["@p1"] = note },
                context.Ct).ConfigureAwait(false);
        }

        /// <summary>Marks the path where the engine should have refused and did not.</summary>
        private sealed class CheckFailedSentinel : Exception
        {
        }
    }
}

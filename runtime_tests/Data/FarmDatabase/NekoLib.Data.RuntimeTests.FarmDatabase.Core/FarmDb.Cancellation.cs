#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core
{
    /// <summary>How one call reacted to a token that was already cancelled.</summary>
    public sealed class CancellationResult
    {
        public CancellationResult(string name, bool honoured, string detail)
        {
            Name = name;
            Honoured = honoured;
            Detail = detail;
        }

        public string Name { get; }

        /// <summary>True when the call refused to run.</summary>
        public bool Honoured { get; }

        public string Detail { get; }
    }

    public sealed partial class FarmDb
    {
        /// <summary>
        /// Hands every entry point a token that is already cancelled and records
        /// whether the call refuses to run.
        /// <para/>
        /// Cancelling mid-flight cannot be arranged deterministically against a local
        /// file database — the queries finish in under a millisecond — so this asks the
        /// narrower question that can be answered exactly: <b>is the token consulted at
        /// all?</b> A call that completes normally with a cancelled token is ignoring
        /// it, and no amount of timing would make that safe.
        /// <para/>
        /// This matters for the fallback mode as much as for the token. The gateway only
        /// falls back to blocking calls when a provider throws
        /// <c>NotSupportedException</c> <i>and</i> the caller opted in; the default is
        /// <c>Disabled</c>. Whether either provider ever reaches that path is itself
        /// something to measure rather than assume.
        /// </summary>
        public async Task<List<CancellationResult>> CancellationProbeAsync()
        {
            var results = new List<CancellationResult>();

            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                CancellationToken dead = source.Token;

                results.Add(await ProbeAsync("OpenSessionAsync", async () =>
                {
                    using (DbSession session = await Gateway.OpenSessionAsync(dead).ConfigureAwait(false))
                        return 0;
                }).ConfigureAwait(false));

                results.Add(await ProbeAsync("GetRaw(sql)", async () =>
                    (await Gateway.GetRaw(
                        "SELECT [Id] FROM [Products]", null, dead).ConfigureAwait(false)).Count)
                    .ConfigureAwait(false));

                results.Add(await ProbeAsync("GetDto(builder)", async () =>
                    (await Gateway.GetDto<Product>(
                        new QueryBuilder().Select("*").From("[Products]"), dead)
                        .ConfigureAwait(false)).Count)
                    .ConfigureAwait(false));

                results.Add(await ProbeAsync("GetDynamic", async () =>
                    (await Gateway.GetDynamic(
                        new QueryBuilder().Select("*").From("[Products]"), dead)
                        .ConfigureAwait(false)).Count)
                    .ConfigureAwait(false));

                results.Add(await ProbeAsync("ReadDto(callback)", async () =>
                {
                    int seen = 0;
                    await Gateway.ReadDto<Product>(
                        new QueryBuilder().Select("*").From("[Products]"),
                        _ => seen++,
                        dead).ConfigureAwait(false);
                    return seen;
                }).ConfigureAwait(false));

                // A write, because refusing to read is cheap and refusing to write is
                // the one that protects the database.
                results.Add(await ProbeAsync("Insert(sql)", async () =>
                    await Gateway.Insert(
                        "INSERT INTO OperationLog (OccurredAt, EntityKind, EntityId, " +
                        "EntityName, Operation, Quantity, Reason) " +
                        "VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                        new Dictionary<string, object?>
                        {
                            ["@p1"] = DateTime.UtcNow.ToString("o"),
                            ["@p2"] = "Cancelamento",
                            ["@p3"] = 0,
                            ["@p4"] = "sonda",
                            ["@p5"] = Operations.Add,
                            ["@p6"] = 1,
                            ["@p7"] = "nao deveria existir"
                        },
                        dead).ConfigureAwait(false))
                    .ConfigureAwait(false));

#if NET6_0_OR_GREATER
                results.Add(await ProbeAsync("StreamDto", async () =>
                {
                    int seen = 0;
                    await foreach (Product _ in Gateway.StreamDto<Product>(
                        new QueryBuilder().Select("*").From("[Products]"), dead)
                        .ConfigureAwait(false))
                        seen++;

                    return seen;
                }).ConfigureAwait(false));
#endif
            }

            return results;
        }

        private static async Task<CancellationResult> ProbeAsync(
            string name,
            Func<Task<int>> call)
        {
            try
            {
                int outcome = await call().ConfigureAwait(false);
                return new CancellationResult(
                    name, false, "completou com " + outcome + " - token ignorado");
            }
            catch (OperationCanceledException)
            {
                // TaskCanceledException derives from this, so both land here.
                return new CancellationResult(name, true, "OperationCanceledException");
            }
            catch (Exception ex)
            {
                return new CancellationResult(
                    name, false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>The fallback mode this database was opened with.</summary>
        public string FallbackModeName => _ctx.Options.SynchronousFallbackMode.ToString();
    }
}

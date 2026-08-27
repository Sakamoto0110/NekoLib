#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core
{
    /// <summary>
    /// Persistence for the simulation, split out by concern the way
    /// <c>DatabaseGateway</c> itself is.
    /// <para/>
    /// This half is what makes the game a database test rather than a toy: nothing
    /// about a run lives in memory alone. Every tick that changes anything commits the
    /// change and its audit row in one transaction, so the process can be killed at
    /// any instant and the world has to come back exactly as it was.
    /// </summary>
    public sealed partial class FarmDb
    {
        /// <summary>The single state row's fixed key.</summary>
        private const int SimStateId = 1;

        private static readonly List<SimEvent> EmptyEvents = new List<SimEvent>();

        // -----------------------------------------------------------------
        // Schema
        // -----------------------------------------------------------------

        /// <summary>
        /// Adds the simulation tables to a database created before they existed. Same
        /// reasoning as <c>EnsureTagSequenceAsync</c>: these databases are disposable,
        /// but refusing to open one made last week is a worse first impression than a
        /// silent forward step.
        /// </summary>
        private async Task EnsureSimSchemaAsync(CancellationToken ct)
        {
            IReadOnlyList<string> tables = await ListTablesAsync(ct).ConfigureAwait(false);

            foreach (string table in tables)
                if (string.Equals(table, "SimState", StringComparison.OrdinalIgnoreCase))
                    return;

            foreach (string ddl in Profile.SchemaDdl())
                if (ddl.IndexOf("CREATE TABLE Sim", StringComparison.OrdinalIgnoreCase) >= 0)
                    await Gateway.Insert(ddl, null, ct).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------
        // Loading
        // -----------------------------------------------------------------

        /// <summary>
        /// Reads the run in progress, or returns null when there is none. This is the
        /// resume path - the one a restart depends on.
        /// </summary>
        public async Task<SimSnapshot?> LoadSimAsync(CancellationToken ct = default)
        {
            List<Dictionary<string, RecordItem>> stateRows = await Gateway.GetRaw(
                "SELECT [Tick], [Seed], [Gold], [Terrains], [Slots], [Workers] " +
                "FROM [SimState] WHERE [Id] = @p1",
                new Dictionary<string, object?> { ["@p1"] = SimStateId },
                ct).ConfigureAwait(false);

            if (stateRows.Count == 0)
                return null;

            var snapshot = new SimSnapshot();
            Dictionary<string, RecordItem> row = stateRows[0];

            snapshot.State.Tick = ReadLong(row["Tick"]);
            snapshot.State.Seed = (int)ReadLong(row["Seed"]);
            snapshot.State.Gold = ReadDouble(row["Gold"]);
            snapshot.State.Terrains = (int)ReadLong(row["Terrains"]);
            snapshot.State.Slots = (int)ReadLong(row["Slots"]);
            snapshot.State.Workers = (int)ReadLong(row["Workers"]);

            List<Dictionary<string, RecordItem>> tiles = await Gateway.GetRaw(
                "SELECT [Id], [Terrain], [Slot], [Crop], [PlantedAtTick], [HasWorker], " +
                "[NextActionTick] FROM [SimTiles] ORDER BY [Terrain], [Slot]",
                null,
                ct).ConfigureAwait(false);

            foreach (Dictionary<string, RecordItem> tile in tiles)
            {
                snapshot.Tiles.Add(new SimTile
                {
                    Id = (int)ReadLong(tile["Id"]),
                    Terrain = (int)ReadLong(tile["Terrain"]),
                    Slot = (int)ReadLong(tile["Slot"]),
                    Crop = tile["Crop"].ToString(),
                    PlantedAtTick = ReadLong(tile["PlantedAtTick"]),
                    HasWorker = ReadLong(tile["HasWorker"]) != 0,
                    NextActionTick = ReadLong(tile["NextActionTick"])
                });
            }

            List<Dictionary<string, RecordItem>> market = await Gateway.GetRaw(
                "SELECT [Crop], [Quantity] FROM [SimMarket] ORDER BY [Crop]",
                null,
                ct).ConfigureAwait(false);

            foreach (Dictionary<string, RecordItem> entry in market)
            {
                snapshot.Market.Add(new SimMarketRow
                {
                    Crop = entry["Crop"].ToString(),
                    Quantity = ReadDouble(entry["Quantity"])
                });
            }

            List<Dictionary<string, RecordItem>> inventory = await Gateway.GetRaw(
                "SELECT [Crop], [Quantity] FROM [SimInventory] ORDER BY [Crop]",
                null,
                ct).ConfigureAwait(false);

            foreach (Dictionary<string, RecordItem> entry in inventory)
            {
                snapshot.Inventory.Add(new SimInventoryRow
                {
                    Crop = entry["Crop"].ToString(),
                    Quantity = (int)ReadLong(entry["Quantity"])
                });
            }

            return snapshot;
        }

        // -----------------------------------------------------------------
        // Starting a run
        // -----------------------------------------------------------------

        /// <summary>
        /// Clears any run in progress and writes a fresh world for the seed, all in
        /// one transaction: a half-created run would resume into a world that never
        /// existed.
        /// </summary>
        public async Task<SimSnapshot> StartRunAsync(int seed, CancellationToken ct = default)
        {
            SimSnapshot snapshot = FarmSimulation.NewRun(seed);

            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    foreach (string table in new[] { "SimState", "SimTiles", "SimMarket", "SimInventory" })
                    {
                        await Gateway.Delete(
                            new QueryBuilder()
                                .DeleteFrom("[" + table + "]")
                                .AllowAllRowsDelete(),
                            session,
                            ct).ConfigureAwait(false);
                    }

                    await InsertRowAsync("SimState", new Dictionary<string, object?>
                    {
                        ["Id"] = SimStateId,
                        ["Tick"] = snapshot.State.Tick,
                        ["Seed"] = snapshot.State.Seed,
                        ["Gold"] = snapshot.State.Gold,
                        ["Terrains"] = snapshot.State.Terrains,
                        ["Slots"] = snapshot.State.Slots,
                        ["Workers"] = snapshot.State.Workers
                    }, ct, session).ConfigureAwait(false);

                    foreach (SimTile tile in snapshot.Tiles)
                    {
                        await InsertRowAsync("SimTiles", new Dictionary<string, object?>
                        {
                            ["Terrain"] = tile.Terrain,
                            ["Slot"] = tile.Slot,
                            ["Crop"] = tile.Crop,
                            ["PlantedAtTick"] = tile.PlantedAtTick,
                            ["HasWorker"] = tile.HasWorker ? 1 : 0,
                            ["NextActionTick"] = tile.NextActionTick
                        }, ct, session).ConfigureAwait(false);
                    }

                    foreach (SimMarketRow entry in snapshot.Market)
                    {
                        await InsertRowAsync("SimMarket", new Dictionary<string, object?>
                        {
                            ["Crop"] = entry.Crop,
                            ["Quantity"] = entry.Quantity
                        }, ct, session).ConfigureAwait(false);
                    }

                    foreach (SimInventoryRow entry in snapshot.Inventory)
                    {
                        await InsertRowAsync("SimInventory", new Dictionary<string, object?>
                        {
                            ["Crop"] = entry.Crop,
                            ["Quantity"] = entry.Quantity
                        }, ct, session).ConfigureAwait(false);
                    }

                    await WriteLogAsync(
                        session,
                        SimEntityKinds.Market,
                        0,
                        "Semente " + seed.ToString(CultureInfo.InvariantCulture),
                        Operations.Add,
                        1,
                        "Início de simulação",
                        ct).ConfigureAwait(false);

                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }

            // Tile identities were assigned by the engine on insert; read them back so
            // later updates address rows rather than re-deleting the board. The gateway
            // cannot return an inserted identity, so this is a second pass by position.
            SimSnapshot? reloaded = await LoadSimAsync(ct).ConfigureAwait(false);
            return reloaded ?? snapshot;
        }

        // -----------------------------------------------------------------
        // Saving a tick
        // -----------------------------------------------------------------

        /// <summary>
        /// Commits one tick. The state row moves every tick; the board, the market and
        /// the inventory are rewritten only when the tick actually changed them.
        /// <para/>
        /// All of it inside one transaction, with the audit rows: this is the same
        /// promise the herd book makes, held under a load no one is clicking.
        /// </summary>
        public Task SaveTickAsync(
            SimSnapshot snapshot,
            TickOutcome outcome,
            CancellationToken ct = default)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));

            return SaveBatchAsync(snapshot, outcome.TouchedState, outcome.Events, ct);
        }

        /// <summary>
        /// Commits a span of ticks as one transaction.
        /// <para/>
        /// At real time this is called once per tick and the two are the same thing. At
        /// speed it is called once per pulse, which is the difference between one
        /// transaction and five hundred - and five hundred round trips per second is
        /// more than either engine will do, so the accelerated modes were spending
        /// their whole budget on transaction overhead rather than on the work.
        /// <para/>
        /// The trade is explicit: a process killed mid-pulse loses that pulse. Real
        /// time and the two slow speeds still commit per tick, so the durability
        /// property is tested where it is claimed.
        /// </summary>
        public async Task SaveBatchAsync(
            SimSnapshot snapshot,
            bool boardChanged,
            List<SimEvent> events,
            CancellationToken ct = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    var state = new QueryBuilder()
                        .Update("SimState")
                        .Set("Tick", snapshot.State.Tick)
                        .Set("Gold", snapshot.State.Gold)
                        .Set("Terrains", snapshot.State.Terrains)
                        .Set("Slots", snapshot.State.Slots)
                        .Set("Workers", snapshot.State.Workers)
                        .Where("[Id]", QueryOperator.Equal, SimStateId);

                    await Gateway.Update(state, session, ct).ConfigureAwait(false);

                    if (boardChanged)
                    {
                        await SaveBoardAsync(snapshot, session, ct).ConfigureAwait(false);
                    }

                    foreach (SimEvent entry in events ?? EmptyEvents)
                    {
                        await WriteLogAsync(
                            session,
                            entry.Kind,
                            0,
                            entry.Name,
                            entry.Kind == SimEntityKinds.Sale ? Operations.Remove : Operations.Add,
                            entry.Quantity,
                            entry.Reason,
                            ct).ConfigureAwait(false);
                    }

                    session.Commit();

                    // Only after the commit: a rolled-back transaction has to leave the
                    // flags set so the change is still carried by the next attempt.
                    MarkClean(snapshot);
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Writes only what changed.
        /// <para/>
        /// This used to rewrite the whole board on any tick that touched anything,
        /// which measured 176 statements per tick at real time to persist a change that
        /// usually moved a single tile. The instrumentation is what made that visible.
        /// <para/>
        /// The flags are cleared by the caller after the commit, not here: a rolled-back
        /// transaction has to leave them set so the next attempt still carries the
        /// change.
        /// </summary>
        private async Task SaveBoardAsync(SimSnapshot snapshot, DbSession session, CancellationToken ct)
        {
            foreach (SimTile tile in snapshot.Tiles)
            {
                if (!tile.IsDirty) continue;

                var update = new QueryBuilder()
                    .Update("SimTiles")
                    .Set("Crop", tile.Crop)
                    .Set("PlantedAtTick", tile.PlantedAtTick)
                    .Set("HasWorker", tile.HasWorker ? 1 : 0)
                    .Set("NextActionTick", tile.NextActionTick)
                    .Where("[Id]", QueryOperator.Equal, tile.Id);

                await Gateway.Update(update, session, ct).ConfigureAwait(false);
            }

            foreach (SimMarketRow entry in snapshot.Market)
            {
                if (!entry.IsDirty) continue;

                var update = new QueryBuilder()
                    .Update("SimMarket")
                    .Set("Quantity", entry.Quantity)
                    .Where("[Crop]", QueryOperator.Equal, entry.Crop);

                await Gateway.Update(update, session, ct).ConfigureAwait(false);
            }

            foreach (SimInventoryRow entry in snapshot.Inventory)
            {
                if (!entry.IsDirty) continue;

                var update = new QueryBuilder()
                    .Update("SimInventory")
                    .Set("Quantity", entry.Quantity)
                    .Where("[Crop]", QueryOperator.Equal, entry.Crop);

                await Gateway.Update(update, session, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// How many audit rows the log holds.
        /// <para/>
        /// This is the number that verifies the scenario's central promise. Every
        /// audited change commits its row in the same transaction as the change itself,
        /// so the count must move by exactly the number of events the simulation
        /// emitted. A mismatch means a transaction landed by halves, which is the one
        /// failure the whole design exists to rule out.
        /// </summary>
        public async Task<long> CountOperationLogAsync(CancellationToken ct = default)
        {
            List<Dictionary<string, RecordItem>> rows = await Gateway.GetRaw(
                "SELECT COUNT(*) AS Total FROM [OperationLog]",
                null,
                ct).ConfigureAwait(false);

            return rows.Count == 0 ? 0 : ReadLong(rows[0]["Total"]);
        }

        /// <summary>Clears the change flags once the transaction that carried them landed.</summary>
        private static void MarkClean(SimSnapshot snapshot)
        {
            foreach (SimTile tile in snapshot.Tiles) tile.IsDirty = false;
            foreach (SimMarketRow entry in snapshot.Market) entry.IsDirty = false;
            foreach (SimInventoryRow entry in snapshot.Inventory) entry.IsDirty = false;
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// Raw mode hands every value back as an invariant-culture string, so numbers
        /// are parsed rather than cast. Parsing invariantly here is not optional: the
        /// machine's locale would otherwise decide whether "1234.5" is one number or
        /// twelve thousand.
        /// </summary>
        private static long ReadLong(RecordItem item)
        {
            string text = item.ToString();
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                return value;

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double asDouble)
                ? (long)asDouble
                : 0L;
        }

        private static double ReadDouble(RecordItem item)
        {
            return double.TryParse(
                item.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value) ? value : 0d;
        }
    }
}

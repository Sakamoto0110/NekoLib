#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>Something worth an audit row. Plant and harvest are deliberately not here.</summary>
    public sealed class SimEvent
    {
        public SimEvent(string kind, string name, int quantity, string reason)
        {
            Kind = kind;
            Name = name;
            Quantity = quantity;
            Reason = reason;
        }

        public string Kind { get; }
        public string Name { get; }
        public int Quantity { get; }
        public string Reason { get; }
    }

    /// <summary>What one tick did. Empty on most ticks.</summary>
    public sealed class TickOutcome
    {
        public int Planted { get; set; }
        public int Harvested { get; set; }
        public bool Sold { get; set; }
        public bool CycledMonth { get; set; }
        public bool Bought { get; set; }

        /// <summary>
        /// The world ate on this tick. Silent - it produces no audit row - but it
        /// moves every market quantity, so leaving it out of
        /// <see cref="TouchedState"/> loses the change.
        /// </summary>
        public bool WorldConsumed { get; set; }

        public List<SimEvent> Events { get; } = new List<SimEvent>();

        /// <summary>
        /// True when the tick changed anything worth writing beyond the tick counter.
        /// <para/>
        /// <see cref="WorldConsumed"/> belongs here and was missing at first. Day
        /// boundaries land on ticks where every tile is mid-growth, so nothing is
        /// planted or harvested and the tick looked idle - while the market had just
        /// moved. The state digest caught it as a memory/database mismatch on the
        /// second run: consumption that never reached disk.
        /// </summary>
        public bool TouchedState =>
            Planted > 0 || Harvested > 0 || Sold || CycledMonth || Bought || WorldConsumed;
    }

    /// <summary>
    /// The whole game, as a function from one snapshot to the next.
    /// <para/>
    /// Nothing in here reads a clock, generates a random number, or touches a
    /// database. The seed only sets the world's opening stock; from there every
    /// decision is derived from state. That is what lets the same seed produce the
    /// same farm on two different database engines - which is the comparison the
    /// scenario exists to make.
    /// </summary>
    public static class FarmSimulation
    {
        /// <summary>
        /// Builds the opening world for a seed: an empty farm with one worked tile,
        /// and a market already holding uneven stock so the first monthly choice is
        /// not a coin toss.
        /// </summary>
        public static SimSnapshot NewRun(int seed)
        {
            var snapshot = new SimSnapshot();
            snapshot.State.Seed = seed;
            snapshot.State.Tick = 0;
            snapshot.State.Gold = 0;
            snapshot.State.Terrains = 1;
            snapshot.State.Slots = 1;

            // The farm opens with one hand already on it, so the first tile is worked
            // from tick zero instead of waiting on the first thousand gold.
            snapshot.State.Workers = 1;

            for (int i = 0; i < SimRules.Crops.Count; i++)
            {
                // Spread the opening stock with the seed so different seeds start the
                // market in different shapes. Deterministic, and the only place the
                // seed is consulted at all.
                double opening = 120 + ((seed * 37 + i * 91) % 260);
                snapshot.Market.Add(new SimMarketRow
                {
                    Crop = SimRules.Crops[i].Name,
                    Quantity = opening
                });

                snapshot.Inventory.Add(new SimInventoryRow
                {
                    Crop = SimRules.Crops[i].Name,
                    Quantity = 0
                });
            }

            for (int terrain = 0; terrain < SimRules.MaxTerrains; terrain++)
            {
                for (int slot = 0; slot < SimRules.SlotsPerTerrain; slot++)
                {
                    snapshot.Tiles.Add(new SimTile
                    {
                        Terrain = terrain,
                        Slot = slot,
                        Crop = string.Empty,
                        PlantedAtTick = 0,

                        // The opening worker is on the first tile of the first terrain.
                        HasWorker = terrain == 0 && slot == 0,

                        // Everyone starts at the shed and has to walk out.
                        NextActionTick = SimRules.TravelTicks(slot)
                    });
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Advances exactly one tick. The order of the phases is part of the contract:
        /// changing it changes the outcome of a run, and two engines disagreeing about
        /// it would look exactly like a persistence bug.
        /// </summary>
        public static TickOutcome Advance(SimSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var outcome = new TickOutcome();
            SimState state = snapshot.State;

            state.Tick++;

            if (SimClock.IsMonthBoundary(state.Tick))
                CycleMonth(snapshot, outcome);

            Work(snapshot, outcome);

            if (SimClock.IsDayBoundary(state.Tick))
            {
                ConsumeWorld(snapshot);
                outcome.WorldConsumed = true;
            }

            if (SimClock.IsWeekBoundary(state.Tick))
                SellEverything(snapshot, outcome);

            Spend(snapshot, outcome);

            return outcome;
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// The world moves on. The prime for the new month reshapes demand, which
        /// changes which crop is scarcest, which changes what the farm plants next.
        /// </summary>
        private static void CycleMonth(SimSnapshot snapshot, TickOutcome outcome)
        {
            outcome.CycledMonth = true;

            IReadOnlyList<string> chosen = snapshot.ChosenCrops();
            string list = string.Join(", ", chosen);

            outcome.Events.Add(new SimEvent(
                SimEntityKinds.Market,
                "Mês " + snapshot.State.Month.ToString(CultureInfo.InvariantCulture) +
                " · ciclo " + snapshot.State.Prime.ToString(CultureInfo.InvariantCulture),
                chosen.Count,
                "Cultura escassa: " + list));
        }

        /// <summary>
        /// Workers and the player service tiles. A bound tile is handled by its own
        /// worker; the player round-robins whatever is left, which is what keeps the
        /// opening playable before the first hire.
        /// </summary>
        private static void Work(SimSnapshot snapshot, TickOutcome outcome)
        {
            SimState state = snapshot.State;

            IReadOnlyList<string> chosen = snapshot.ChosenCrops();
            if (chosen.Count == 0)
                return;

            // The player is one more worker, covering a tile nobody was hired for. It
            // picks by tick rather than by memory, so a restart resumes the same choice.
            int playerTarget = -1;
            var unbound = new List<int>();

            for (int i = 0; i < snapshot.Tiles.Count; i++)
            {
                SimTile tile = snapshot.Tiles[i];
                if (IsOwned(state, tile) && !tile.HasWorker)
                    unbound.Add(i);
            }

            if (unbound.Count > 0)
                playerTarget = unbound[(int)(state.Tick % unbound.Count)];

            for (int i = 0; i < snapshot.Tiles.Count; i++)
            {
                SimTile tile = snapshot.Tiles[i];
                if (!IsOwned(state, tile)) continue;
                if (!tile.HasWorker && i != playerTarget) continue;

                // Still walking, or still waiting at the shed for this one to be worth
                // the trip.
                if (state.Tick < tile.NextActionTick) continue;

                ServiceTile(snapshot, tile, chosen, outcome);
            }
        }

        /// <summary>
        /// One visit. The worker is at the tile when this runs; what it does next
        /// decides when it is back.
        /// <para/>
        /// The round trip is charged on every leg, which is what pulls a terrain out of
        /// step: the near corner cycles in a fraction of the time the far one does, so
        /// twenty-five tiles planted together never ripen together again.
        /// </summary>
        private static void ServiceTile(
            SimSnapshot snapshot,
            SimTile tile,
            IReadOnlyList<string> chosen,
            TickOutcome outcome)
        {
            long tick = snapshot.State.Tick;
            int roundTrip = tile.TravelTicks * 2;

            // Every path below moves this tile's schedule, so it is written once here
            // rather than at each return.
            tile.IsDirty = true;

            if (!tile.IsEmpty)
            {
                if (!tile.IsMature(tick))
                {
                    // Arrived early - wait here rather than walking home for nothing.
                    tile.NextActionTick = tile.PlantedAtTick +
                        SimRules.CropByName(tile.Crop).GrowthTicks;
                    return;
                }

                SimInventoryRow stock = snapshot.InventoryFor(tile.Crop);
                stock.Quantity += SimRules.YieldPerHarvest;
                stock.IsDirty = true;

                tile.Crop = string.Empty;
                outcome.Harvested++;

                // Carries the load home, then has to come back out to plant again.
                tile.NextActionTick = tick + roundTrip;
                return;
            }

            // Each terrain grows the crop chosen for it. With fewer chosen crops than
            // terrains the extra ones fall back to the first choice.
            string seed = chosen[tile.Terrain < chosen.Count ? tile.Terrain : 0];
            tile.Crop = seed;
            tile.PlantedAtTick = tick;
            outcome.Planted++;

            // Home and back, timed to arrive no earlier than the harvest is ready.
            int growth = SimRules.CropByName(seed).GrowthTicks;
            tile.NextActionTick = tick + Math.Max(growth, roundTrip);
        }

        /// <summary>
        /// The world eats. This is the recovery half of the price mechanic and the
        /// reason a flooded crop becomes worth growing again a few weeks later.
        /// </summary>
        private static void ConsumeWorld(SimSnapshot snapshot)
        {
            int prime = snapshot.State.Prime;

            for (int i = 0; i < snapshot.Market.Count; i++)
            {
                SimMarketRow row = snapshot.Market[i];

                // Appetite is read against the stock as it stands at the start of the
                // day, so the order of this loop cannot change the outcome.
                double appetite = SimRules.DailyAppetite(
                    SimRules.IndexOf(row.Crop), prime, row.Quantity);

                row.Quantity -= appetite;
                if (row.Quantity < 0)
                    row.Quantity = 0;

                row.IsDirty = true;
            }
        }

        /// <summary>
        /// Once a week everything harvested goes to market. Each unit sold pushes that
        /// crop's world quantity up, so a big week is paid at a falling price - the
        /// sale prices itself down as it lands.
        /// </summary>
        private static void SellEverything(SimSnapshot snapshot, TickOutcome outcome)
        {
            double earned = 0;

            foreach (SimInventoryRow row in snapshot.Inventory)
            {
                if (row.Quantity <= 0) continue;

                SimMarketRow market = snapshot.MarketFor(row.Crop);
                Crop crop = SimRules.CropByName(row.Crop);

                // Priced per unit as it is absorbed, not in one lump at the opening
                // price. Selling 300 units of something scarce should not all clear at
                // the scarce price.
                double take = 0;
                for (int unit = 0; unit < row.Quantity; unit++)
                {
                    take += SimRules.PriceOf(crop, market.Quantity);
                    market.Quantity += 1;
                }

                market.IsDirty = true;
                row.IsDirty = true;
                earned += take;

                outcome.Events.Add(new SimEvent(
                    SimEntityKinds.Sale,
                    row.Crop,
                    row.Quantity,
                    "Venda semanal · " + take.ToString("F2", CultureInfo.InvariantCulture) + " g"));

                row.Quantity = 0;
            }

            if (earned > 0)
            {
                snapshot.State.Gold += earned;
                outcome.Sold = true;
            }
        }

        /// <summary>
        /// The buying policy, as an ordered ladder - first rule that fits wins, one
        /// purchase per tick at most.
        /// <para/>
        /// Hiring comes before expanding on purpose: an unworked slot earns nothing,
        /// so widening the farm before staffing it is how a run stalls.
        /// </summary>
        private static void Spend(SimSnapshot snapshot, TickOutcome outcome)
        {
            SimState state = snapshot.State;

            int ownedTiles = 0;
            int workedTiles = 0;

            foreach (SimTile tile in snapshot.Tiles)
            {
                if (!IsOwned(state, tile)) continue;
                ownedTiles++;
                if (tile.HasWorker) workedTiles++;
            }

            // 1. Staff an owned but unworked slot.
            if (workedTiles < ownedTiles && state.Gold >= SimRules.WorkerPrice)
            {
                foreach (SimTile tile in snapshot.Tiles)
                {
                    if (!IsOwned(state, tile) || tile.HasWorker) continue;

                    tile.HasWorker = true;

                    // Hired at the shed, so the first thing it does is walk.
                    tile.NextActionTick = state.Tick + tile.TravelTicks;
                    tile.IsDirty = true;

                    state.Workers++;
                    state.Gold -= SimRules.WorkerPrice;
                    outcome.Bought = true;
                    outcome.Events.Add(new SimEvent(
                        SimEntityKinds.Worker,
                        "Worker #" + state.Workers.ToString(CultureInfo.InvariantCulture),
                        1,
                        "Alocado no terreno " + (tile.Terrain + 1) + ", slot " + (tile.Slot + 1)));
                    return;
                }
            }

            int capacity = state.Terrains * SimRules.SlotsPerTerrain;

            // 2. Buy the next slot in the current terrain.
            if (state.Slots < capacity && state.Gold >= SimRules.SlotPrice)
            {
                state.Slots++;
                state.Gold -= SimRules.SlotPrice;
                outcome.Bought = true;
                outcome.Events.Add(new SimEvent(
                    SimEntityKinds.Slot,
                    "Slot " + state.Slots.ToString(CultureInfo.InvariantCulture),
                    1,
                    "Terreno " + (((state.Slots - 1) / SimRules.SlotsPerTerrain) + 1)));
                return;
            }

            // 3. Terrain full and staffed - open the next one.
            if (state.Slots >= capacity &&
                workedTiles >= ownedTiles &&
                state.Terrains < SimRules.MaxTerrains &&
                state.Gold >= SimRules.TerrainPrice)
            {
                state.Terrains++;
                state.Gold -= SimRules.TerrainPrice;
                outcome.Bought = true;
                outcome.Events.Add(new SimEvent(
                    SimEntityKinds.Terrain,
                    "Terreno " + state.Terrains.ToString(CultureInfo.InvariantCulture),
                    1,
                    "Liberado"));
            }
        }

        /// <summary>
        /// Slots are bought in order, filling one terrain before the next, so
        /// ownership is a position test rather than a stored flag.
        /// </summary>
        public static bool IsOwned(SimState state, SimTile tile)
        {
            int index = (tile.Terrain * SimRules.SlotsPerTerrain) + tile.Slot;
            return index < state.Slots;
        }
    }

    /// <summary>Entity kinds this simulation writes into the shared operation log.</summary>
    public static class SimEntityKinds
    {
        public const string Sale = "Venda";
        public const string Worker = "Worker";
        public const string Slot = "Slot";
        public const string Terrain = "Terreno";
        public const string Market = "Mercado";
    }
}

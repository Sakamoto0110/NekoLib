#nullable enable
using System.Collections.Generic;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>
    /// The single row that says where a run is. Everything needed to resume lives
    /// here: kill the process and this is what has to bring it back to the same tick,
    /// the same month and the same money.
    /// </summary>
    public sealed class SimState
    {
        public long Tick { get; set; }
        public int Seed { get; set; }
        public double Gold { get; set; }
        public int Terrains { get; set; } = 1;

        /// <summary>Slots bought so far, counted across all unlocked terrains.</summary>
        public int Slots { get; set; } = 1;

        /// <summary>Hired workers. The player is not counted here - the player is free.</summary>
        public int Workers { get; set; }

        public int Month => SimClock.MonthOf(Tick);
        public int Prime => SimClock.PrimeForMonth(Month);
        public int Day => SimClock.DayOf(Tick);
        public int Week => SimClock.WeekOf(Tick);

        public SimState Clone() => new SimState
        {
            Tick = Tick,
            Seed = Seed,
            Gold = Gold,
            Terrains = Terrains,
            Slots = Slots,
            Workers = Workers
        };
    }

    /// <summary>One plantable square.</summary>
    public sealed class SimTile
    {
        public int Id { get; set; }

        /// <summary>Zero-based terrain this tile belongs to.</summary>
        public int Terrain { get; set; }

        /// <summary>Zero-based position inside its terrain, row-major over a 5×5 block.</summary>
        public int Slot { get; set; }

        /// <summary>Empty when nothing is planted.</summary>
        public string Crop { get; set; } = string.Empty;

        /// <summary>Tick the current crop went in. Meaningless when <see cref="Crop"/> is empty.</summary>
        public long PlantedAtTick { get; set; }

        /// <summary>A hired worker is bound to this tile. The player covers one unbound tile.</summary>
        public bool HasWorker { get; set; }

        /// <summary>
        /// The tick this tile's worker next reaches it. Between now and then the worker
        /// is walking, or waiting at the shed for the crop to be worth the trip.
        /// <para/>
        /// Persisted, because it is what keeps twenty-five tiles out of step. Losing it
        /// on a restart would put every worker at the tile at once and resynchronise a
        /// farm that had spent hours spreading out.
        /// </summary>
        public long NextActionTick { get; set; }

        /// <summary>Steps of walking from the terrain's shed to this tile, each way.</summary>
        public int TravelTicks => SimRules.TravelTicks(Slot);

        /// <summary>
        /// Changed since the last commit. Runtime only - never read from or written to
        /// the database.
        /// <para/>
        /// Rewriting all seventy-five tiles on any tick that touched anything cost 176
        /// statements per tick, measured. A tick usually moves one of them.
        /// </summary>
        public bool IsDirty { get; set; }

        public bool IsEmpty => string.IsNullOrEmpty(Crop);

        public bool IsMature(long tick)
        {
            if (IsEmpty) return false;
            return tick - PlantedAtTick >= SimRules.CropByName(Crop).GrowthTicks;
        }

        /// <summary>0..1 growth, for the renderer.</summary>
        public double Progress(long tick)
        {
            if (IsEmpty) return 0;

            double span = SimRules.CropByName(Crop).GrowthTicks;
            double done = (tick - PlantedAtTick) / span;
            return done < 0 ? 0 : (done > 1 ? 1 : done);
        }
    }

    /// <summary>
    /// How much of a crop the world already holds. This is the hidden half of the
    /// game: the player never sees the number, only the price it produces.
    /// </summary>
    public sealed class SimMarketRow
    {
        public string Crop { get; set; } = string.Empty;
        public double Quantity { get; set; }

        public double Price => SimRules.PriceOf(SimRules.CropByName(Crop), Quantity);

        /// <summary>Changed since the last commit. Runtime only.</summary>
        public bool IsDirty { get; set; }
    }

    /// <summary>Harvested but unsold. Emptied into the market once a week.</summary>
    public sealed class SimInventoryRow
    {
        public string Crop { get; set; } = string.Empty;
        public int Quantity { get; set; }

        /// <summary>Changed since the last commit. Runtime only.</summary>
        public bool IsDirty { get; set; }
    }

    /// <summary>Everything a run holds in memory, loaded and saved as a unit.</summary>
    public sealed class SimSnapshot
    {
        public SimState State { get; set; } = new SimState();
        public List<SimTile> Tiles { get; } = new List<SimTile>();
        public List<SimMarketRow> Market { get; } = new List<SimMarketRow>();
        public List<SimInventoryRow> Inventory { get; } = new List<SimInventoryRow>();

        public SimMarketRow MarketFor(string crop)
        {
            foreach (SimMarketRow row in Market)
                if (row.Crop == crop)
                    return row;

            var created = new SimMarketRow { Crop = crop, Quantity = 0 };
            Market.Add(created);
            return created;
        }

        public SimInventoryRow InventoryFor(string crop)
        {
            foreach (SimInventoryRow row in Inventory)
                if (row.Crop == crop)
                    return row;

            var created = new SimInventoryRow { Crop = crop, Quantity = 0 };
            Inventory.Add(created);
            return created;
        }

        /// <summary>
        /// Crops the farm is currently told to grow: one per unlocked terrain, the
        /// scarcest in the world market. Re-decided every month.
        /// </summary>
        public IReadOnlyList<string> ChosenCrops()
        {
            var ordered = new List<SimMarketRow>(Market);

            // Sort by scarcity, then by name. The name tie-break is not cosmetic: two
            // crops holding the same quantity is common early on, and without a stable
            // second key the choice would depend on list order and the run would stop
            // being reproducible.
            ordered.Sort((a, b) =>
            {
                int byQuantity = a.Quantity.CompareTo(b.Quantity);
                return byQuantity != 0 ? byQuantity : string.CompareOrdinal(a.Crop, b.Crop);
            });

            var chosen = new List<string>();
            for (int i = 0; i < State.Terrains && i < ordered.Count; i++)
                chosen.Add(ordered[i].Crop);

            return chosen;
        }
    }
}

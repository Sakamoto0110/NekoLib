#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>A rule that stopped holding, and the state that broke it.</summary>
    public sealed class SimViolation
    {
        public SimViolation(string rule, string detail)
        {
            Rule = rule;
            Detail = detail;
        }

        public string Rule { get; }
        public string Detail { get; }

        public override string ToString() => Rule + ": " + Detail;
    }

    /// <summary>
    /// What has to stay true for a run to mean anything.
    /// <para/>
    /// The state digest compares memory against the database, which catches a change
    /// that never reached disk — but it cannot tell whether either copy is
    /// <i>correct</i>. A simulation that computes gold wrongly and persists it faithfully
    /// passes the digest. These are the checks that say the numbers themselves are
    /// sane, and they run inside the loop so a break is reported at the tick it
    /// happened rather than at the end of a run.
    /// </summary>
    public static class SimInvariants
    {
        /// <summary>
        /// Every rule that can be judged from the snapshot alone. Cheap enough for
        /// every tick: it walks the board once.
        /// </summary>
        public static IReadOnlyList<SimViolation> Check(SimSnapshot snapshot)
        {
            var broken = new List<SimViolation>();
            if (snapshot == null) return broken;

            SimState state = snapshot.State;

            if (state.Gold < 0)
                broken.Add(new SimViolation("ouro", "negativo: " + Format(state.Gold)));

            if (state.Terrains < 1 || state.Terrains > SimRules.MaxTerrains)
                broken.Add(new SimViolation("terrenos", "fora de 1.." + SimRules.MaxTerrains + ": " + state.Terrains));

            int capacity = state.Terrains * SimRules.SlotsPerTerrain;
            if (state.Slots < 1 || state.Slots > capacity)
                broken.Add(new SimViolation("slots", state.Slots + " com capacidade " + capacity));

            int worked = 0;
            foreach (SimTile tile in snapshot.Tiles)
            {
                bool owned = FarmSimulation.IsOwned(state, tile);

                // A hired hand on ground the farm does not own would be paid for
                // nothing, and would keep producing after a terrain was lost.
                if (tile.HasWorker && !owned)
                {
                    broken.Add(new SimViolation(
                        "worker",
                        "terreno " + tile.Terrain + " slot " + tile.Slot + " tem worker sem o slot ser comprado"));
                }

                if (tile.HasWorker) worked++;

                if (!tile.IsEmpty)
                {
                    if (tile.PlantedAtTick > state.Tick)
                    {
                        broken.Add(new SimViolation(
                            "plantio",
                            "terreno " + tile.Terrain + " slot " + tile.Slot +
                            " plantado no futuro: " + tile.PlantedAtTick + " > " + state.Tick));
                    }

                    if (!KnownCrop(tile.Crop))
                    {
                        broken.Add(new SimViolation(
                            "cultura",
                            "terreno " + tile.Terrain + " slot " + tile.Slot +
                            " tem '" + tile.Crop + "', que não está no catálogo"));
                    }
                }
            }

            if (state.Workers != worked)
            {
                broken.Add(new SimViolation(
                    "contagem de workers",
                    "estado diz " + state.Workers + ", tabuleiro tem " + worked));
            }

            foreach (SimMarketRow row in snapshot.Market)
            {
                if (row.Quantity < 0)
                    broken.Add(new SimViolation("mercado", row.Crop + " negativo: " + Format(row.Quantity)));
            }

            foreach (SimInventoryRow row in snapshot.Inventory)
            {
                if (row.Quantity < 0)
                    broken.Add(new SimViolation("inventário", row.Crop + " negativo: " + row.Quantity));
            }

            return broken;
        }

        /// <summary>
        /// True when every crop has bottomed out at the hard price floor.
        /// <para/>
        /// This is not a defect in the code; it is the economy having stopped meaning
        /// anything, and a long run that reaches it spends its remaining hours trading
        /// worthless goods and proving nothing. It happened for real at month 58 with a
        /// flat world appetite, and it is worth stopping on rather than discovering in
        /// a screenshot.
        /// </summary>
        public static bool EconomyIsDead(SimSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Market.Count == 0)
                return false;

            foreach (SimMarketRow row in snapshot.Market)
            {
                Crop crop = SimRules.CropByName(row.Crop);
                double share = crop.BasePrice <= 0 ? 1 : row.Price / crop.BasePrice;

                // A hair above the floor so rounding does not trip it.
                if (share > SimRules.MinimumPriceFactor * 1.05)
                    return false;
            }

            return true;
        }

        private static bool KnownCrop(string name)
        {
            foreach (Crop crop in SimRules.Crops)
                if (crop.Name == name)
                    return true;

            return false;
        }

        private static string Format(double value) =>
            value.ToString("F2", CultureInfo.InvariantCulture);
    }
}

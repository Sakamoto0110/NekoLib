#nullable enable
using System;
using System.Collections.Generic;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>
    /// One plantable crop. Data only - growth time and base price are the whole
    /// definition, so adding a variety is a row here rather than a branch anywhere.
    /// </summary>
    public sealed class Crop
    {
        public Crop(string name, int growthTicks, double basePrice, string swatch)
        {
            Name = name;
            GrowthTicks = growthTicks;
            BasePrice = basePrice;
            Swatch = swatch;
        }

        public string Name { get; }

        /// <summary>Ticks from planting to harvestable.</summary>
        public int GrowthTicks { get; }

        /// <summary>Price with an empty world market. What is actually paid decays from here.</summary>
        public double BasePrice { get; }

        /// <summary>Hex colour the renderer paints this crop with.</summary>
        public string Swatch { get; }
    }

    /// <summary>
    /// Every tunable number in the simulation, in one place.
    /// <para/>
    /// These are first guesses, not measured values. The whole point of the first runs
    /// is to find out which of them are wrong - so they live together, named, instead
    /// of being scattered as literals.
    /// </summary>
    public static class SimRules
    {
        // --- economy ------------------------------------------------------
        public const int SlotPrice = 1000;
        public const int WorkerPrice = 1000;
        public const int TerrainPrice = 1000;

        /// <summary>Five by five. Three terrains therefore hold 75 tiles.</summary>
        public const int SlotsPerTerrain = 25;
        public const int TerrainSide = 5;
        public const int MaxTerrains = 3;

        /// <summary>Units harvested from one mature tile.</summary>
        public const int YieldPerHarvest = 3;

        /// <summary>Ticks of walking per step of distance between the shed and a tile.</summary>
        public const int TravelTicksPerStep = 1;

        /// <summary>
        /// How long a worker spends walking from its terrain's shed to a tile.
        /// <para/>
        /// This is a simulation cost, not an animation. The shed sits centred below the
        /// block, so distance is measured out from the middle column and up from the
        /// bottom row: the near tile is one step and the top corners are seven.
        /// <para/>
        /// Because the round trip is charged on every cycle, twenty-five tiles planted
        /// at the same instant come due at seven different times — which is the whole
        /// reason travel exists. Without it a terrain ripens in one block and the farm
        /// behaves like a single large tile.
        /// </summary>
        public static int TravelTicks(int slot)
        {
            int column = slot % TerrainSide;
            int row = slot / TerrainSide;

            int fromCentre = Math.Abs(column - (TerrainSide / 2));
            int fromBottom = TerrainSide - 1 - row;

            return 1 + ((fromCentre + fromBottom) * TravelTicksPerStep);
        }

        // --- market -------------------------------------------------------

        /// <summary>
        /// Selling this many units into the world market halves the price of that
        /// crop. Smaller means the farm moves prices harder.
        /// </summary>
        public const double MarketScale = 400.0;

        /// <summary>
        /// Multiplier on the market's daily appetite.
        /// <para/>
        /// This is the number that decides whether the farm can outrun world
        /// consumption at low stock, and the requirement is that a working farm must
        /// <b>always</b> be able to unbalance the market's input. Measured against the
        /// slowest crop, which is the tightest case: a terrain of Milho yields 3 units
        /// every 20 ticks per tile, so 25 tiles give about 262 a week. Peak base
        /// appetite is 7 a day, so weekly consumption on an empty market is
        /// <c>49 × AppetiteScale</c> and the constraint is <c>49 × scale &lt; 262</c>.
        /// <para/>
        /// The first run of 3000 ticks was made with this at 4, and the planted crops
        /// sat at zero stock and full price the whole time - the world ate everything
        /// the farm grew, so flooding never happened and the price mechanic never
        /// engaged. That is what this number being wrong looks like.
        /// </summary>
        public const int AppetiteScale = 1;

        /// <summary>Floor so a flooded crop still pays something and the economy never dies outright.</summary>
        public const double MinimumPriceFactor = 0.05;

        /// <summary>
        /// How strongly demand responds to abundance. Consumption is multiplied by
        /// <c>1 + quantity / DemandElasticity</c>, so a glutted crop is eaten faster
        /// than a scarce one.
        /// <para/>
        /// This exists because fixed consumption cannot hold a long run. With a flat
        /// appetite the farm outproduced the world in every crop at once, stock only
        /// ever accumulated, and by month 58 all six were pinned at
        /// <see cref="MinimumPriceFactor"/> - an economy where nothing is worth
        /// growing and the remaining hours trade worthless goods.
        /// <para/>
        /// Elastic demand keeps the farm's ability to unbalance the market, which is
        /// the requirement: at low stock the appetite is small, so selling still drives
        /// the price down. What it removes is the farm's ability to push a crop down
        /// <i>forever</i> - past a point the world simply eats more and the price finds
        /// a floor of its own well above the hard one.
        /// </summary>
        public const double DemandElasticity = 150.0;

        // --- catalogue ----------------------------------------------------

        /// <summary>
        /// Growth time and price rise together, so a slow crop is not simply worse.
        /// Order is stable and load-bearing: the market's per-crop appetite is derived
        /// from a crop's index, so reordering this list changes the world.
        /// </summary>
        public static readonly IReadOnlyList<Crop> Crops = new[]
        {
            new Crop("Alface",  4,  9.0, "#7FBF6A"),
            new Crop("Batata",  5, 12.0, "#C79A5B"),
            new Crop("Cenoura", 8, 18.0, "#D2803F"),
            new Crop("Tomate", 12, 28.0, "#C4544A"),
            new Crop("Trigo",  16, 36.0, "#D6B65C"),
            new Crop("Milho",  20, 45.0, "#E0C64A")
        };

        public static Crop CropByName(string name)
        {
            foreach (Crop crop in Crops)
                if (crop.Name == name)
                    return crop;

            return Crops[0];
        }

        public static int IndexOf(string cropName)
        {
            for (int i = 0; i < Crops.Count; i++)
                if (Crops[i].Name == cropName)
                    return i;

            return 0;
        }

        /// <summary>
        /// How much of a crop the world eats per day under the current prime.
        /// <para/>
        /// This is the recovery half of the price mechanic. Selling pushes a crop's
        /// market quantity up and its price down; consumption pulls the quantity back
        /// down and the price back up. Without it, diversifying would be a one-way
        /// street: every crop would bottom out in turn and the run would spend its
        /// last hours trading worthless goods.
        /// <para/>
        /// The prime is what makes the world change shape monthly. Because it is
        /// coprime with almost everything, <c>(prime * index) % 7</c> reshuffles which
        /// crops are in demand every cycle without ever settling into a pattern - some
        /// start climbing, others start falling, and the farm has to follow.
        /// </summary>
        public static double DailyAppetite(int cropIndex, int prime, double marketQuantity)
        {
            int baseAppetite = (1 + ((prime * (cropIndex + 1)) % 7)) * AppetiteScale;

            if (marketQuantity <= 0)
                return baseAppetite;

            return baseAppetite * (1.0 + (marketQuantity / DemandElasticity));
        }

        /// <summary>
        /// What one unit fetches right now. Falls as the world market fills up.
        /// </summary>
        public static double PriceOf(Crop crop, double marketQuantity)
        {
            if (marketQuantity < 0) marketQuantity = 0;

            double factor = 1.0 / (1.0 + (marketQuantity / MarketScale));
            if (factor < MinimumPriceFactor)
                factor = MinimumPriceFactor;

            return crop.BasePrice * factor;
        }
    }
}

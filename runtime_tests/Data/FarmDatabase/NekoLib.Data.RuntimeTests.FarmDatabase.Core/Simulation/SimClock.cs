#nullable enable
using System.Collections.Generic;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation
{
    /// <summary>
    /// Turns a tick count into farm calendar terms, and drives the market's monthly
    /// cycle through the prime sequence.
    /// <para/>
    /// Everything here is a pure function of the tick, which is what makes a run
    /// reproducible: no wall clock, no elapsed-time measurement, no drift. Restarting
    /// the process at tick 4000 puts the calendar exactly where it was.
    /// </summary>
    public static class SimClock
    {
        /// <summary>One tick is one second of wall time when running in real time.</summary>
        public const int TicksPerDay = 10;
        public const int DaysPerWeek = 7;
        public const int WeeksPerMonth = 4;

        public const int TicksPerWeek = TicksPerDay * DaysPerWeek;      // 70
        public const int TicksPerMonth = TicksPerWeek * WeeksPerMonth;  // 280

        public static int DayOf(long tick) => (int)(tick / TicksPerDay);
        public static int WeekOf(long tick) => (int)(tick / TicksPerWeek);
        public static int MonthOf(long tick) => (int)(tick / TicksPerMonth);

        public static bool IsDayBoundary(long tick) => tick > 0 && tick % TicksPerDay == 0;
        public static bool IsWeekBoundary(long tick) => tick > 0 && tick % TicksPerWeek == 0;
        public static bool IsMonthBoundary(long tick) => tick > 0 && tick % TicksPerMonth == 0;

        /// <summary>
        /// The sequence the market walks, one entry per month: <c>1, 2, 3, 5, 7, 11…</c>
        /// <para/>
        /// Kept as written rather than as strict primes - the leading 1 is deliberate,
        /// it gives the opening month a neutral appetite before the market starts
        /// pulling in different directions.
        /// </summary>
        public static int PrimeForMonth(int month)
        {
            if (month <= 0) return 1;

            int found = 0;
            int candidate = 1;

            while (found < month)
            {
                candidate++;
                if (IsPrime(candidate))
                    found++;
            }

            return candidate;
        }

        private static bool IsPrime(int value)
        {
            if (value < 2) return false;
            if (value % 2 == 0) return value == 2;

            for (int divisor = 3; divisor * divisor <= value; divisor += 2)
                if (value % divisor == 0)
                    return false;

            return true;
        }

        /// <summary>The first few entries, for display and for tests.</summary>
        public static IReadOnlyList<int> PrimeSeries(int count)
        {
            var series = new List<int>(count);
            for (int month = 0; month < count; month++)
                series.Add(PrimeForMonth(month));
            return series;
        }
    }
}

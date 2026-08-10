#nullable enable
using System;
using System.Globalization;

namespace NekoLib.RuntimeTests.Harness.Reporting
{
    /// <summary>
    /// How much per-check detail a run keeps in memory.
    /// <para/>
    /// This exists because the measuring instrument was distorting the
    /// measurement. <see cref="CheckRunner"/> used to hold every
    /// <see cref="CheckResult"/> alive until the process ended. A 15-minute soak
    /// produced 3848 of them; a four-hour soak projects to roughly 61 500, and
    /// the managed heap they occupy only ever grows. E3-OBS's own drift check
    /// fails a heap that rises at every periodic sample — so a long enough run
    /// would have reported the harness's accumulation as a leak in the library
    /// under test.
    /// <para/>
    /// The fix is not to measure less. Counts stay exact, failures and skips are
    /// kept in full, and the complete detail is still written — incrementally,
    /// to <c>checks.ndjson</c>, where it costs disk instead of heap. What is
    /// bounded is only the successful detail held in memory, and only for the
    /// modes where it would otherwise grow without limit.
    /// </summary>
    public sealed class CheckRetention
    {
        private CheckRetention(int successCapacity)
        {
            SuccessCapacity = successCapacity;
        }

        /// <summary>Distinct successful checks kept, or -1 for all of them.</summary>
        public int SuccessCapacity { get; }

        public bool RetainsEverything => SuccessCapacity < 0;

        /// <summary>
        /// Keeps every result. The default, and what every short mode uses, so
        /// their artifacts are byte-for-byte what they were before.
        /// </summary>
        public static readonly CheckRetention All = new CheckRetention(-1);

        /// <summary>
        /// Keeps the first success for each distinct phase and name, up to
        /// <paramref name="successCapacity"/> of them.
        /// <para/>
        /// Sampling by distinct name rather than by arrival is deliberate: a
        /// soak runs the same matrix thousands of times, so the first occurrence
        /// of each check carries its claim and its notes while the ten-thousandth
        /// carries nothing new. The retained set therefore still shows every
        /// check that ran at least once, and is bounded by how many checks the
        /// scenario defines rather than by how long the run lasted.
        /// </summary>
        public static CheckRetention Bounded(int successCapacity)
        {
            if (successCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(successCapacity));

            return new CheckRetention(successCapacity);
        }

        /// <summary>
        /// The default capacity for a bounded run. Comfortably above the number
        /// of distinct checks either current scenario defines - E3-OBS has about
        /// forty including its faults, E4-SQL about thirty - so the sample is
        /// complete in practice while the bound is still a real one.
        /// </summary>
        public const int DefaultSuccessCapacity = 256;

        /// <summary>
        /// The policy a mode should use.
        /// <para/>
        /// Only the soak is bounded. Smoke and rehearsal have a fixed amount of
        /// work and produce a few thousand results at most, so they keep the
        /// behaviour and the artifacts they already had.
        /// </summary>
        public static CheckRetention ForMode(ScenarioMode mode) =>
            mode == ScenarioMode.Soak ? Bounded(DefaultSuccessCapacity) : All;

        /// <summary>A one-line description, recorded in every result document.</summary>
        public string Describe() =>
            RetainsEverything
                ? "all: every result is retained"
                : "bounded: all failures and skips, plus the first success for each distinct check, up to " +
                  SuccessCapacity.ToString(CultureInfo.InvariantCulture) + " of them";
    }
}

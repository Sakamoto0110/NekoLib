#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// What this scenario adds to the suite's shared command line.
    /// <para/>
    /// The modes, the seed, the artifact root and the fault schedule come from
    /// <see cref="ScenarioOptionsBase"/> because the suite makes them uniform.
    /// Everything below is about the three capabilities and belongs to nobody
    /// else.
    /// </summary>
    internal sealed class ScenarioOptions : ScenarioOptionsBase
    {
        /// <summary>
        /// How long the smoke sustains its workload.
        /// <para/>
        /// The suite specifies 15 to 30 minutes. This scenario's checks complete
        /// in seconds, so without a window the smoke would prove the matrices
        /// and nothing about behaviour over time. A shorter value is accepted so
        /// the scenario can be developed, and any run below the window is
        /// flagged in its result so it cannot be cited as smoke evidence by
        /// accident.
        /// <para/>
        /// This lives here rather than in <see cref="ScenarioOptionsBase"/>
        /// deliberately: the harness takes nothing that does not already have
        /// two real consumers, and E4-SQL has no smoke-duration concept. It
        /// moves when a second scenario needs it.
        /// </summary>
        public TimeSpan SmokeDuration = TimeSpan.FromMinutes(15);

        public static readonly TimeSpan MinimumSpecifiedSmoke = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The expected write rate for the sustained logging phase, in entries
        /// per second. The suite asks for "a configurable expected PDV rate";
        /// zero means as fast as the process manages, which is what the smoke
        /// uses.
        /// </summary>
        public int LogRate;

        /// <summary>Leaves the scenario's working directory for post-mortem inspection.</summary>
        public bool KeepWorkingDirectory;

        /// <summary>
        /// Skips the checks that install into the process-wide Inspection slot.
        /// They cannot run beside another consumer of that slot in the same
        /// process, and saying so is better than failing obscurely.
        /// </summary>
        public bool NoGlobalInspection;

        public override string ScenarioId => "E3-OBS";

        protected override string CampaignPrefix => "e3obs";

        /// <summary>
        /// Identifies the schedule generator and is covered by the schedule
        /// hash. It is deliberately this scenario's own string: sharing one
        /// across scenarios would make any scenario's change invalidate every
        /// other scenario's recorded determinism evidence.
        /// </summary>
        public const string ScheduleGeneratorVersion = "e3obs-schedule-1";

        protected override IEnumerable<string> ScenarioUsage() => new[]
        {
            "  --smoke-duration <d>        smoke window, default 15m (the suite specifies 15-30m)",
            "  --log-rate <entries/s>      expected sustained write rate; 0 (default) means unthrottled",
            "  --keep-work                 leave the scenario working directory for inspection",
            "  --no-global-inspection      skip the checks that install the process-wide Inspection slot",
            string.Empty,
            "This scenario needs no external service, no container and no device.",
            "It writes only inside its own run directory."
        };

        protected override bool TryParseScenarioOption(
            string[] args,
            ref int index,
            string option,
            out string diagnostic)
        {
            diagnostic = string.Empty;

            switch (option)
            {
                case "--smoke-duration":
                    if (!TryTakeValue(args, ref index, "--smoke-duration", out string smoke, out diagnostic))
                        return false;
                    return TryParseDuration(smoke, out SmokeDuration, out diagnostic);

                case "--log-rate":
                    if (!TryTakeValue(args, ref index, "--log-rate", out string rate, out diagnostic)) return false;
                    if (!int.TryParse(rate, NumberStyles.Integer, CultureInfo.InvariantCulture, out LogRate) ||
                        LogRate < 0)
                    {
                        diagnostic = "--log-rate expects a non-negative integer, got '" + rate + "'";
                        return false;
                    }
                    return true;

                case "--keep-work":
                    KeepWorkingDirectory = true;
                    return true;

                case "--no-global-inspection":
                    NoGlobalInspection = true;
                    return true;

                default:
                    return false;
            }
        }

        public static string UsageText() =>
            new ScenarioOptions().Usage(
                "E3-OBS - NekoLib Logging, Telemetry and passive Inspection under load, failure and recovery");
    }
}

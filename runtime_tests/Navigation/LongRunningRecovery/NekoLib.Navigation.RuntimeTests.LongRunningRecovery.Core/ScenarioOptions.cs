#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    public sealed class ScenarioOptions : ScenarioOptionsBase
    {
        public const string ScheduleGeneratorVersion = "e3nav-schedule-1";
        public static readonly TimeSpan MinimumSpecifiedSmoke = TimeSpan.FromMinutes(15);

        private readonly string _platformId;

        public ScenarioOptions(string platformId)
        {
            if (string.IsNullOrWhiteSpace(platformId))
                throw new ArgumentException("A platform identifier is required.", nameof(platformId));

            _platformId = platformId;
        }

        public override string ScenarioId => "E3-NAV";
        protected override string CampaignPrefix => "e3nav-" + _platformId;

        public string PlatformId => _platformId;
        public TimeSpan SmokeDuration = TimeSpan.FromMinutes(20);
        public int IdleTimeoutMilliseconds = 120000;
        public int SwitchesPerCycle = 256;

        protected override IEnumerable<string> ScenarioUsage()
        {
            return new[]
            {
                "  --smoke-duration <d>        development override; specified window remains 15-30m",
                "  --idle-timeout-ms <n>       scenario idle interval, default 120000ms",
                "  --switches-per-cycle <n>    sustained switch batch, default 256"
            };
        }

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
                    if (!TryTakeValue(args, ref index, option, out string duration, out diagnostic))
                        return false;
                    return TryParseDuration(duration, out SmokeDuration, out diagnostic);

                case "--idle-timeout-ms":
                    if (!TryTakeValue(args, ref index, option, out string idle, out diagnostic))
                        return false;
                    if (!int.TryParse(idle, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out IdleTimeoutMilliseconds) || IdleTimeoutMilliseconds < 100)
                    {
                        diagnostic = "--idle-timeout-ms expects an integer of at least 100";
                        return false;
                    }
                    return true;

                case "--switches-per-cycle":
                    if (!TryTakeValue(args, ref index, option, out string switches, out diagnostic))
                        return false;
                    if (!int.TryParse(switches, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out SwitchesPerCycle) || SwitchesPerCycle < 32)
                    {
                        diagnostic = "--switches-per-cycle expects an integer of at least 32";
                        return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        public static string UsageText(string platformId) =>
            new ScenarioOptions(platformId).Usage(
                "E3-NAV " + platformId + " long-running and recovery scenario");
    }
}

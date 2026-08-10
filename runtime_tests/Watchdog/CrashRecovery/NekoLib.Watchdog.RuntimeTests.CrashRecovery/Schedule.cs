#nullable enable
using System;
using System.Collections.Generic;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal static class FaultKinds
    {
        public const string OrdinaryExit = "ordinary-child-exit";
        public const string UnhandledCrash = "unhandled-child-crash";
        public const string FastCrashLoop = "fast-crash-loop";
        public const string HostRestart = "host-shutdown-and-restart";
        public const string CleanShutdown = "clean-child-shutdown";
        public const string BootstrapRepeat = "repeat-bootstrap-attach";

        // The first crash can come from a generation whose uptime already
        // exceeds the runtime's three-second fast-crash threshold. Twelve
        // terminals still guarantee two complete groups of five fast restarted
        // generations and therefore two observable cooling windows.
        public const int FastCrashCount = 12;

        public static IReadOnlyList<string> ForMode(ScenarioMode mode)
        {
            List<string> kinds = new List<string>();

            int cycles = mode == ScenarioMode.Soak ? 4 : 1;
            for (int i = 0; i < cycles; i++)
            {
                kinds.Add(OrdinaryExit);
                kinds.Add(OrdinaryExit);
                kinds.Add(UnhandledCrash);
                if (mode != ScenarioMode.Smoke) kinds.Add(UnhandledCrash);
                if (mode != ScenarioMode.Smoke) kinds.Add(FastCrashLoop);
                kinds.Add(HostRestart);
                kinds.Add(CleanShutdown);
                kinds.Add(BootstrapRepeat);
            }

            return kinds;
        }

        public static bool IsChildOwned(string kind) =>
            kind == OrdinaryExit || kind == UnhandledCrash || kind == FastCrashLoop;

        public static bool IsKnown(string kind) =>
            IsChildOwned(kind) || kind == HostRestart || kind == CleanShutdown || kind == BootstrapRepeat;
    }

    internal sealed class WatchdogFaultVocabulary : IFaultVocabulary
    {
        public string DescribeTarget(string kind)
        {
            switch (kind)
            {
                case FaultKinds.OrdinaryExit: return "scenario application child";
                case FaultKinds.UnhandledCrash: return "scenario application child and pending crash input";
                case FaultKinds.FastCrashLoop: return "successive scenario application child generations";
                case FaultKinds.HostRestart: return "controller-owned deployed Watchdog Host";
                case FaultKinds.CleanShutdown: return "paused supervisor and scenario application child";
                default: return "fresh scenario application and deployed Host pair";
            }
        }

        public string DescribeParameters(string kind) =>
            kind == FaultKinds.FastCrashLoop
                ? FaultKinds.FastCrashCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "1";

        public string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.OrdinaryExit:
                    return "the Host observes exit code 0, starts exactly one replacement, and the new generation " +
                           "passes health and log-forwarding probes";
                case FaultKinds.UnhandledCrash:
                    return "the armed crash produces one final bundle, one replacement generation, and successful " +
                           "health and forwarding probes";
                case FaultKinds.FastCrashLoop:
                    return "twelve armed loop crashes produce no duplicate generation, the documented ten-second " +
                           "cooling is observed after each complete group of five fast restarted generations, and " +
                           "one healthy generation follows";
                case FaultKinds.HostRestart:
                    return "the exact owned Host and child stop, a fresh pair starts from the persisted arguments, " +
                           "and undocumented runtime counters are reset rather than carried across Hosts";
                case FaultKinds.CleanShutdown:
                    return "pause prevents an ordinary clean child exit from being restarted; the Host then stops " +
                           "and a fresh pair can bootstrap";
                default:
                    return "a second fresh bootstrap/attach cycle owns exactly one Host and one child and reaches " +
                           "health plus log forwarding";
            }
        }
    }

    internal static class ScheduleFactory
    {
        public static FaultSchedule Build(ScenarioOptions options)
        {
            FaultSchedule schedule;
            if (options.FaultSchedulePath != null)
                schedule = FaultSchedule.Load(options.FaultSchedulePath, options.ScenarioId);
            else
            {
                TimeSpan duration =
                    options.Mode == ScenarioMode.Smoke ? options.SmokeDuration :
                    options.Mode == ScenarioMode.RecoveryRehearsal ? options.RehearsalDuration :
                    options.SoakDuration;

                schedule = FaultSchedule.Generate(
                    options.CampaignId,
                    options.ScenarioId,
                    ScenarioOptions.ScheduleGeneratorVersion,
                    options.Mode == ScenarioMode.Smoke ? "smoke" :
                    options.Mode == ScenarioMode.RecoveryRehearsal ? "recovery-rehearsal" : "soak",
                    options.Seed,
                    duration,
                    FaultKinds.ForMode(options.Mode),
                    new WatchdogFaultVocabulary());
            }

            foreach (FaultEvent planned in schedule.Events)
            {
                if (!FaultKinds.IsKnown(planned.Kind))
                    throw new InvalidOperationException(
                        "The E3-WDOG schedule contains unknown fault kind '" + planned.Kind + "'.");
            }
            return schedule;
        }
    }

    internal static class SchedulePreview
    {
        public static int Print(ScenarioOptions options)
        {
            FaultSchedule schedule = ScheduleFactory.Build(options);
            Console.WriteLine(schedule.ToJson());
            Console.WriteLine();
            Console.WriteLine("normalized-hash " + schedule.Hash);
            return ExitCodes.Success;
        }
    }
}

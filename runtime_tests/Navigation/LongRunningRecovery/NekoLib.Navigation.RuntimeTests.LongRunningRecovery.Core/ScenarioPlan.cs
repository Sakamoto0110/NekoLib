#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    internal static class FaultKinds
    {
        public const string RegistryLookup = "registry-lookup-failure";
        public const string PageCreation = "page-creation-failure";
        public const string PageLoad = "page-load-failure";
        public const string EnterLifecycle = "enter-lifecycle-failure";
        public const string LeaveLifecycle = "leave-lifecycle-failure";
        public const string BackgroundLoad = "background-load-failure";
        public const string GuardTimeout = "guard-timeout";
        public const string GuardThrow = "guard-throws";
        public const string RedirectCycle = "redirect-cycle";
        public const string RedirectDepth = "redirect-depth";
        public const string SurfaceBinding = "surface-binding-failure";
        public const string SurfaceShow = "surface-show-failure";
        public const string SurfaceCleanup = "surface-cleanup-failure";
        public const string DispatcherUnavailable = "dispatcher-unavailable";

        public static readonly string[] All =
        {
            RegistryLookup,
            PageCreation,
            PageLoad,
            EnterLifecycle,
            LeaveLifecycle,
            BackgroundLoad,
            GuardTimeout,
            GuardThrow,
            RedirectCycle,
            RedirectDepth,
            SurfaceBinding,
            SurfaceShow,
            SurfaceCleanup,
            DispatcherUnavailable
        };
    }

    internal sealed class NavigationFaultVocabulary : IFaultVocabulary
    {
        public string DescribeTarget(string kind)
        {
            if (kind.StartsWith("surface-", StringComparison.Ordinal)) return "scenario-owned native surface";
            if (kind == FaultKinds.DispatcherUnavailable) return "scenario adapter dispatcher";
            if (kind.StartsWith("guard-", StringComparison.Ordinal) ||
                kind.StartsWith("redirect-", StringComparison.Ordinal)) return "scenario-owned guard";
            return "scenario-owned page";
        }

        public string DescribeParameters(string kind) =>
            kind == FaultKinds.GuardTimeout ? "31" : "0";

        public string DescribeExpectedRecovery(string kind) =>
            "the documented terminal is observed and an ordinary page switch succeeds afterwards";
    }

    internal static class ScenarioPlan
    {
        public static FaultSchedule Build(ScenarioOptions options)
        {
            if (options.FaultSchedulePath != null)
                return FaultSchedule.Load(options.FaultSchedulePath, options.ScenarioId);

            string mode;
            TimeSpan duration;
            IReadOnlyList<string> kinds;

            switch (options.Mode)
            {
                case ScenarioMode.RecoveryRehearsal:
                    mode = "recovery-rehearsal";
                    duration = options.RehearsalDuration;
                    kinds = FaultKinds.All;
                    break;

                case ScenarioMode.Soak:
                    mode = "soak";
                    duration = options.SoakDuration;
                    kinds = FaultKinds.All;
                    break;

                default:
                    mode = "smoke";
                    duration = options.SmokeDuration;
                    kinds = new string[0];
                    break;
            }

            return FaultSchedule.Generate(
                options.CampaignId,
                options.ScenarioId,
                ScenarioOptions.ScheduleGeneratorVersion,
                mode,
                options.Seed,
                duration,
                kinds,
                new NavigationFaultVocabulary());
        }

        public static void Persist(string path, FaultSchedule schedule)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, schedule.ToJson(), new System.Text.UTF8Encoding(false));
        }
    }
}

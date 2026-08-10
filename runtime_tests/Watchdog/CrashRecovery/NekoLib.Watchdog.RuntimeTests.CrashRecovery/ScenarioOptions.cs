#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal enum DeploymentLayout
    {
        Source = 0,
        DisposablePackage = 1,
        PublishedConsumer = 2
    }

    internal sealed class ScenarioOptions : ScenarioOptionsBase
    {
        public TimeSpan SmokeDuration = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan MinimumSpecifiedSmoke = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan MinimumSpecifiedSoak = TimeSpan.FromHours(4);

        public DeploymentLayout Layout = DeploymentLayout.Source;
        public string ApplicationRoot = string.Empty;
        public string PackageFile = string.Empty;
        public string PackageVersion = string.Empty;

        public override string ScenarioId => "E3-WDOG";
        protected override string CampaignPrefix => "e3wdog";

        public const string ScheduleGeneratorVersion = "e3wdog-schedule-1";

        protected override IEnumerable<string> ScenarioUsage() => new[]
        {
            "  --smoke-duration <d>        smoke window, default 15m (the suite specifies 15-30m)",
            "  --layout <kind>             source, disposable-package or published-consumer",
            "  --application-root <dir>    deployed child root; required outside the source layout",
            "  --package-file <nupkg>      exact immutable Host package used by a package-backed layout",
            "  --package-version <version> version paired with --package-file"
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
                    if (!TryTakeValue(args, ref index, option, out string smoke, out diagnostic)) return false;
                    return TryParseDuration(smoke, out SmokeDuration, out diagnostic);

                case "--layout":
                    if (!TryTakeValue(args, ref index, option, out string layout, out diagnostic)) return false;
                    switch (layout.ToLowerInvariant())
                    {
                        case "source": Layout = DeploymentLayout.Source; return true;
                        case "disposable-package": Layout = DeploymentLayout.DisposablePackage; return true;
                        case "published-consumer": Layout = DeploymentLayout.PublishedConsumer; return true;
                        default:
                            diagnostic = "--layout expects source, disposable-package or published-consumer, got '" +
                                         layout + "'";
                            return false;
                    }

                case "--application-root":
                    if (!TryTakeValue(args, ref index, option, out ApplicationRoot, out diagnostic)) return false;
                    if (!Path.IsPathRooted(ApplicationRoot))
                    {
                        diagnostic = "--application-root expects an absolute directory, got '" + ApplicationRoot + "'";
                        return false;
                    }
                    ApplicationRoot = Path.GetFullPath(ApplicationRoot);
                    return true;

                case "--package-file":
                    if (!TryTakeValue(args, ref index, option, out PackageFile, out diagnostic)) return false;
                    if (!Path.IsPathRooted(PackageFile))
                    {
                        diagnostic = "--package-file expects an absolute file, got '" + PackageFile + "'";
                        return false;
                    }
                    PackageFile = Path.GetFullPath(PackageFile);
                    return true;

                case "--package-version":
                    return TryTakeValue(args, ref index, option, out PackageVersion, out diagnostic);

                default:
                    return false;
            }
        }

        public static string UsageText() =>
            new ScenarioOptions().Usage(
                "E3-WDOG - deployed Watchdog Host supervision, crash recovery, forwarding and bundles");
    }
}

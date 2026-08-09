#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    /// <summary>
    /// What this scenario adds to the suite's shared command line.
    /// <para/>
    /// The modes, the seed, the artifact root and the fault schedule come from
    /// <see cref="ScenarioOptionsBase"/> because the suite makes them uniform.
    /// Everything below is about a SQL Server container and belongs to nobody
    /// else.
    /// </summary>
    internal sealed class ScenarioOptions : ScenarioOptionsBase
    {
        public string? ContainerNameOverride;
        public string Host = "127.0.0.1";
        public int? PortOverride;

        /// <summary>Leaves the scenario database in place for post-mortem inspection.</summary>
        public bool KeepDatabase;

        /// <summary>
        /// Runs without any container fault. Recovery checks that need to stop
        /// the server are skipped and reported as skipped, which is how a run on
        /// a machine whose container must stay up stays honest.
        /// </summary>
        public bool NoContainerFaults;

        public override string ScenarioId => "E4-SQL";

        protected override string CampaignPrefix => "e4sql";

        /// <summary>
        /// Identifies the schedule generator, and is covered by the schedule
        /// hash. It is deliberately this scenario's own string: generalising it
        /// when the generator moved into the shared harness would have silently
        /// invalidated every recorded determinism hash.
        /// </summary>
        public const string ScheduleGeneratorVersion = "e4sql-schedule-1";

        protected override IEnumerable<string> ScenarioUsage() => new[]
        {
            "  --container <name>          adopt a differently named container",
            "  --host <address>            default 127.0.0.1",
            "  --port <number>             default from container.json",
            "  --keep-database             leave the scenario database for inspection",
            "  --no-container-faults       skip checks that must stop the server",
            string.Empty,
            "The SA password is read from the environment variable named in container.json.",
            "It is never accepted on the command line."
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
                case "--container":
                    if (!TryTakeValue(args, ref index, "--container", out string container, out diagnostic))
                        return false;
                    ContainerNameOverride = container;
                    return true;

                case "--host":
                    if (!TryTakeValue(args, ref index, "--host", out string host, out diagnostic)) return false;
                    Host = host;
                    return true;

                case "--port":
                    if (!TryTakeValue(args, ref index, "--port", out string port, out diagnostic)) return false;
                    if (!int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        diagnostic = "--port expects an integer, got '" + port + "'";
                        return false;
                    }
                    PortOverride = parsed;
                    return true;

                case "--keep-database":
                    KeepDatabase = true;
                    return true;

                case "--no-container-faults":
                    NoContainerFaults = true;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// A SQL Server database name derived from the campaign. Only letters,
        /// digits and underscores survive, because the name reaches T-SQL as an
        /// identifier rather than as a parameter.
        /// </summary>
        public string BuildScenarioDatabaseName(string prefix)
        {
            StringBuilder name = new StringBuilder(prefix);
            foreach (char c in CampaignId)
            {
                if (char.IsLetterOrDigit(c)) name.Append(c);
                else if (c == '-' || c == '_') name.Append('_');
            }

            // SQL Server allows 128 characters; staying well inside that keeps
            // the name usable in messages and in sys.databases listings.
            string text = name.ToString();
            return text.Length <= 100 ? text : text.Substring(0, 100);
        }

        public static string UsageText() =>
            new ScenarioOptions().Usage("E4-SQL - NekoLib.Data against a local SQL Server container");
    }
}

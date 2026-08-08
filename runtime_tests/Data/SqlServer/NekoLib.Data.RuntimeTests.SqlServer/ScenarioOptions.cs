#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NekoLib.Data.RuntimeTests.SqlServer.Reporting;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    internal enum ScenarioMode
    {
        None = 0,
        Smoke = 1,
        RecoveryRehearsal = 2,
        Soak = 3
    }

    /// <summary>
    /// The command line, parsed.
    /// <para/>
    /// The option names come from the suite's common command contract so the
    /// orchestrator can drive every scenario the same way, and every mode is
    /// non-interactive: nothing here waits for a keypress and nothing asks a
    /// person to interpret output.
    /// </summary>
    internal sealed class ScenarioOptions
    {
        public ScenarioMode Mode = ScenarioMode.None;
        public TimeSpan SoakDuration = TimeSpan.Zero;

        /// <summary>
        /// How long the recovery rehearsal spreads its fault schedule over.
        /// <para/>
        /// The suite specifies 60 to 90 minutes. A shorter value is accepted so
        /// the scenario can be developed and debugged, and any run below the
        /// specified window is flagged in <c>result.json</c> as
        /// <c>belowSpecifiedWindow</c> so it can never be cited as rehearsal
        /// evidence by accident.
        /// </summary>
        public TimeSpan RehearsalDuration = TimeSpan.FromMinutes(60);

        public static readonly TimeSpan MinimumSpecifiedRehearsal = TimeSpan.FromMinutes(60);
        public int Seed = 20260808;
        public string ArtifactsRoot = string.Empty;
        public string? FaultSchedulePath;
        public string? ContainerNameOverride;
        public string Host = "127.0.0.1";
        public int? PortOverride;

        /// <summary>Leaves the scenario database in place for post-mortem inspection.</summary>
        public bool KeepDatabase;

        /// <summary>
        /// Generates the schedule for the selected mode, prints it, and exits
        /// without touching the server.
        /// <para/>
        /// This is how schedule determinism is checked: the same seed must
        /// produce the same normalized hash on both target frameworks, and that
        /// claim should be verifiable without a database, a container, or a
        /// password.
        /// </summary>
        public bool PrintScheduleOnly;

        /// <summary>
        /// Runs without any container fault. Recovery checks that need to stop
        /// the server are skipped and reported as skipped, which is how a run on
        /// a machine whose container must stay up stays honest.
        /// </summary>
        public bool NoContainerFaults;

        public string CampaignId = string.Empty;
        public string ScenarioId = "E4-SQL";

        public static bool TryParse(string[] args, out ScenarioOptions options, out string diagnostic)
        {
            options = new ScenarioOptions();
            diagnostic = string.Empty;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "--smoke":
                        if (!SetMode(options, ScenarioMode.Smoke, out diagnostic)) return false;
                        break;

                    case "--recovery-rehearsal":
                        if (!SetMode(options, ScenarioMode.RecoveryRehearsal, out diagnostic)) return false;
                        break;

                    case "--soak":
                        if (!SetMode(options, ScenarioMode.Soak, out diagnostic)) return false;
                        if (!TryTakeValue(args, ref i, "--soak", out string duration, out diagnostic)) return false;
                        if (!TryParseDuration(duration, out options.SoakDuration, out diagnostic)) return false;
                        break;

                    case "--seed":
                        if (!TryTakeValue(args, ref i, "--seed", out string seed, out diagnostic)) return false;
                        if (!int.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out options.Seed))
                        {
                            diagnostic = "--seed expects an integer, got '" + seed + "'";
                            return false;
                        }
                        break;

                    case "--artifacts":
                        if (!TryTakeValue(args, ref i, "--artifacts", out string artifacts, out diagnostic)) return false;
                        if (!Path.IsPathRooted(artifacts))
                        {
                            diagnostic = "--artifacts expects an absolute directory, got '" + artifacts + "'";
                            return false;
                        }
                        options.ArtifactsRoot = artifacts;
                        break;

                    case "--fault-schedule":
                        if (!TryTakeValue(args, ref i, "--fault-schedule", out string schedule, out diagnostic)) return false;
                        if (!Path.IsPathRooted(schedule))
                        {
                            diagnostic = "--fault-schedule expects an absolute file path, got '" + schedule + "'";
                            return false;
                        }
                        options.FaultSchedulePath = schedule;
                        break;

                    case "--container":
                        if (!TryTakeValue(args, ref i, "--container", out string container, out diagnostic)) return false;
                        options.ContainerNameOverride = container;
                        break;

                    case "--host":
                        if (!TryTakeValue(args, ref i, "--host", out string host, out diagnostic)) return false;
                        options.Host = host;
                        break;

                    case "--port":
                        if (!TryTakeValue(args, ref i, "--port", out string port, out diagnostic)) return false;
                        if (!int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort))
                        {
                            diagnostic = "--port expects an integer, got '" + port + "'";
                            return false;
                        }
                        options.PortOverride = parsedPort;
                        break;

                    case "--rehearsal-duration":
                        if (!TryTakeValue(args, ref i, "--rehearsal-duration", out string rehearsal, out diagnostic))
                            return false;
                        if (!TryParseDuration(rehearsal, out options.RehearsalDuration, out diagnostic)) return false;
                        break;

                    case "--keep-database":
                        options.KeepDatabase = true;
                        break;

                    case "--no-container-faults":
                        options.NoContainerFaults = true;
                        break;

                    case "--print-schedule":
                        options.PrintScheduleOnly = true;
                        break;

                    default:
                        diagnostic = "unknown option '" + arg + "'";
                        return false;
                }
            }

            if (options.Mode == ScenarioMode.None)
            {
                diagnostic = "no mode selected";
                return false;
            }

            if (options.ArtifactsRoot.Length == 0)
                options.ArtifactsRoot = DefaultArtifactsRoot();

            options.CampaignId = BuildCampaignId(options);
            return true;
        }

        /// <summary>
        /// A campaign identifier that is unique per run and readable in a
        /// directory listing. The target framework is part of it because the two
        /// builds are separate claims and their artifacts must never land in the
        /// same directory.
        /// </summary>
        private static string BuildCampaignId(ScenarioOptions options)
        {
            string mode =
                options.Mode == ScenarioMode.Smoke ? "smoke" :
                options.Mode == ScenarioMode.RecoveryRehearsal ? "recovery" : "soak";

            return "e4sql-" + mode +
                   "-" + RuntimeFacts.TargetFrameworkMoniker +
                   "-s" + options.Seed.ToString(CultureInfo.InvariantCulture) +
                   "-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
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

        private static bool SetMode(ScenarioOptions options, ScenarioMode mode, out string diagnostic)
        {
            if (options.Mode != ScenarioMode.None && options.Mode != mode)
            {
                diagnostic = "only one mode may be selected";
                return false;
            }

            options.Mode = mode;
            diagnostic = string.Empty;
            return true;
        }

        private static bool TryTakeValue(
            string[] args,
            ref int index,
            string name,
            out string value,
            out string diagnostic)
        {
            if (index + 1 >= args.Length)
            {
                value = string.Empty;
                diagnostic = name + " expects a value";
                return false;
            }

            index++;
            value = args[index];
            diagnostic = string.Empty;
            return true;
        }

        public static bool TryParseDuration(string text, out TimeSpan duration, out string diagnostic)
        {
            duration = TimeSpan.Zero;
            diagnostic = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                diagnostic = "a duration is required, for example 16h, 90m or 45s";
                return false;
            }

            string trimmed = text.Trim();
            char suffix = trimmed[trimmed.Length - 1];
            string numberPart = char.IsDigit(suffix) ? trimmed : trimmed.Substring(0, trimmed.Length - 1);

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) ||
                amount <= 0)
            {
                diagnostic = "'" + text + "' is not a positive duration";
                return false;
            }

            switch (char.ToLowerInvariant(suffix))
            {
                case 'h': duration = TimeSpan.FromHours(amount); return true;
                case 'm': duration = TimeSpan.FromMinutes(amount); return true;
                case 's': duration = TimeSpan.FromSeconds(amount); return true;
                default:
                    if (char.IsDigit(suffix)) { duration = TimeSpan.FromSeconds(amount); return true; }
                    diagnostic = "unknown duration suffix '" + suffix + "'; use h, m or s";
                    return false;
            }
        }

        /// <summary>
        /// The suite's artifact root, resolved by walking up from the executable
        /// until the repository is recognised. Falling back to the current
        /// directory keeps a copied binary runnable, and the chosen path is
        /// always printed so a run is never ambiguous about where it wrote.
        /// </summary>
        private static string DefaultArtifactsRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "NekoLib.sln")))
                    return Path.Combine(directory.FullName, "artifacts", "validation", "phase-e");

                directory = directory.Parent;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "validation", "phase-e");
        }

        public static string Usage()
        {
            List<string> lines = new List<string>
            {
                "E4-SQL - NekoLib.Data against a local SQL Server container",
                string.Empty,
                "  --smoke                     every workload class, no destructive fault density",
                "  --recovery-rehearsal        every enabled failure and recovery transition",
                "  --soak <duration>           sustained run, for example 16h",
                string.Empty,
                "  --rehearsal-duration <d>    rehearsal window, default 60m (the suite specifies 60-90m)",
                string.Empty,
                "  --seed <integer>            seeds the deterministic fault schedule",
                "  --artifacts <absolute-dir>  run directory root",
                "  --fault-schedule <file>     use a schedule generated elsewhere",
                "  --container <name>          adopt a differently named container",
                "  --host <address>            default 127.0.0.1",
                "  --port <number>             default from container.json",
                "  --keep-database             leave the scenario database for inspection",
                "  --no-container-faults       skip checks that must stop the server",
                "  --print-schedule            print the schedule for this seed and exit, touching nothing",
                string.Empty,
                "The SA password is read from the environment variable named in container.json.",
                "It is never accepted on the command line."
            };

            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}

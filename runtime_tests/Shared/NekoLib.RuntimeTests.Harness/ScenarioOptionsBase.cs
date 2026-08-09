#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.RuntimeTests.Harness
{
    public enum ScenarioMode
    {
        None = 0,
        Smoke = 1,
        RecoveryRehearsal = 2,
        Soak = 3
    }

    /// <summary>
    /// The part of a scenario's command line that the suite makes uniform.
    /// <para/>
    /// Only the shared contract lives here — the modes, the seed, the artifact
    /// root and the fault schedule. Everything a particular scenario needs, a
    /// container name or a COM port or a pipe, belongs to that scenario and is
    /// parsed by overriding <see cref="TryParseScenarioOption"/>. A base class
    /// that accumulated every scenario's private flags would be the wrong kind
    /// of sharing.
    /// </summary>
    public abstract class ScenarioOptionsBase
    {
        public ScenarioMode Mode = ScenarioMode.None;
        public TimeSpan SoakDuration = TimeSpan.Zero;

        /// <summary>
        /// How long the recovery rehearsal spreads its fault schedule over.
        /// <para/>
        /// The suite specifies 60 to 90 minutes. A shorter value is accepted so
        /// a scenario can be developed and debugged, and any run below the
        /// window is flagged in its result so it can never be cited as
        /// rehearsal evidence by accident.
        /// </summary>
        public TimeSpan RehearsalDuration = TimeSpan.FromMinutes(60);

        public static readonly TimeSpan MinimumSpecifiedRehearsal = TimeSpan.FromMinutes(60);

        public int Seed = 20260808;
        public string ArtifactsRoot = string.Empty;
        public string? FaultSchedulePath;

        /// <summary>Generates the schedule, prints it and exits, touching nothing.</summary>
        public bool PrintScheduleOnly;

        public string CampaignId = string.Empty;

        /// <summary>Identifies the scenario in artifacts and in schedule events.</summary>
        public abstract string ScenarioId { get; }

        /// <summary>Leading component of the generated campaign id.</summary>
        protected abstract string CampaignPrefix { get; }

        /// <summary>Usage lines for the options this scenario adds.</summary>
        protected virtual IEnumerable<string> ScenarioUsage() => new string[0];

        /// <summary>
        /// Handles an option the shared contract does not define. Return false
        /// with an empty diagnostic to mean "not mine"; return false with a
        /// diagnostic to reject it.
        /// </summary>
        protected virtual bool TryParseScenarioOption(
            string[] args,
            ref int index,
            string option,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            return false;
        }

        public bool TryParse(string[] args, out string diagnostic)
        {
            diagnostic = string.Empty;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "--smoke":
                        if (!SetMode(ScenarioMode.Smoke, out diagnostic)) return false;
                        break;

                    case "--recovery-rehearsal":
                        if (!SetMode(ScenarioMode.RecoveryRehearsal, out diagnostic)) return false;
                        break;

                    case "--soak":
                        if (!SetMode(ScenarioMode.Soak, out diagnostic)) return false;
                        if (!TryTakeValue(args, ref i, "--soak", out string duration, out diagnostic)) return false;
                        if (!TryParseDuration(duration, out SoakDuration, out diagnostic)) return false;
                        break;

                    case "--rehearsal-duration":
                        if (!TryTakeValue(args, ref i, "--rehearsal-duration", out string rehearsal, out diagnostic))
                            return false;
                        if (!TryParseDuration(rehearsal, out RehearsalDuration, out diagnostic)) return false;
                        break;

                    case "--seed":
                        if (!TryTakeValue(args, ref i, "--seed", out string seed, out diagnostic)) return false;
                        if (!int.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out Seed))
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
                        ArtifactsRoot = artifacts;
                        break;

                    case "--fault-schedule":
                        if (!TryTakeValue(args, ref i, "--fault-schedule", out string schedule, out diagnostic))
                            return false;
                        if (!Path.IsPathRooted(schedule))
                        {
                            diagnostic = "--fault-schedule expects an absolute file path, got '" + schedule + "'";
                            return false;
                        }
                        FaultSchedulePath = schedule;
                        break;

                    case "--print-schedule":
                        PrintScheduleOnly = true;
                        break;

                    default:
                        if (!TryParseScenarioOption(args, ref i, arg.ToLowerInvariant(), out string scenarioDiagnostic))
                        {
                            diagnostic = scenarioDiagnostic.Length > 0
                                ? scenarioDiagnostic
                                : "unknown option '" + arg + "'";
                            return false;
                        }
                        break;
                }
            }

            if (Mode == ScenarioMode.None)
            {
                diagnostic = "no mode selected";
                return false;
            }

            if (ArtifactsRoot.Length == 0) ArtifactsRoot = DefaultArtifactsRoot();

            CampaignId = BuildCampaignId();
            return true;
        }

        /// <summary>
        /// A campaign identifier unique per run and readable in a directory
        /// listing. The target framework is part of it because the two builds
        /// are separate claims whose artifacts must never share a directory,
        /// and the timestamp carries milliseconds because two runs started
        /// inside the same second would otherwise collide.
        /// </summary>
        private string BuildCampaignId()
        {
            string mode =
                Mode == ScenarioMode.Smoke ? "smoke" :
                Mode == ScenarioMode.RecoveryRehearsal ? "recovery" : "soak";

            return CampaignPrefix + "-" + mode +
                   "-" + RuntimeFacts.TargetFrameworkMoniker +
                   "-s" + Seed.ToString(CultureInfo.InvariantCulture) +
                   "-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        }

        private bool SetMode(ScenarioMode mode, out string diagnostic)
        {
            if (Mode != ScenarioMode.None && Mode != mode)
            {
                diagnostic = "only one mode may be selected";
                return false;
            }

            Mode = mode;
            diagnostic = string.Empty;
            return true;
        }

        protected static bool TryTakeValue(
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

        public string Usage(string title)
        {
            List<string> lines = new List<string>
            {
                title,
                string.Empty,
                "  --smoke                     every workload class, no destructive fault density",
                "  --recovery-rehearsal        every enabled failure and recovery transition",
                "  --soak <duration>           sustained run, for example 16h",
                string.Empty,
                "  --rehearsal-duration <d>    rehearsal window, default 60m (the suite specifies 60-90m)",
                "  --seed <integer>            seeds the deterministic fault schedule",
                "  --artifacts <absolute-dir>  run directory root",
                "  --fault-schedule <file>     use a schedule generated elsewhere",
                "  --print-schedule            print the schedule for this seed and exit, touching nothing"
            };

            List<string> extra = new List<string>(ScenarioUsage());
            if (extra.Count > 0)
            {
                lines.Add(string.Empty);
                lines.AddRange(extra);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}

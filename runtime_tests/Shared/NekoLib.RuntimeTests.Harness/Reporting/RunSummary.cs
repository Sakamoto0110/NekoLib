#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NekoLib.RuntimeTests.Harness.Reporting
{
    /// <summary>
    /// The scenario half of the result record: the versions and identifiers only
    /// the owning scenario can name.
    /// <para/>
    /// E4-SQL names a provider package, a server build and an image digest;
    /// E3-OBS names three module assemblies. Neither list means anything to the
    /// harness, which references no product module, so it asks rather than
    /// guesses.
    /// </summary>
    public interface IScenarioSummary
    {
        /// <summary>Rows added to the summary table, in order.</summary>
        IReadOnlyList<KeyValuePair<string, string>> Facts { get; }

        /// <summary>Extra top-level properties written into <c>result.json</c>.</summary>
        void WriteJson(JsonWriter json);
    }

    /// <summary>
    /// Resolves the exit code and writes <c>result.json</c>, <c>summary.json</c>
    /// and <c>summary.md</c>.
    /// <para/>
    /// This is the artifact contract the suite specifies rather than anything a
    /// scenario invented, which is why it is shared: two scenarios producing
    /// subtly different <c>summary.json</c> documents would defeat the point of
    /// having specified one. It writes; it does not run anything. The scenario
    /// decides when its run is over and what its own facts are.
    /// </summary>
    public sealed class RunSummary
    {
        public string CampaignId = string.Empty;
        public string ScenarioId = string.Empty;
        public string Mode = string.Empty;
        public int Seed;

        /// <summary>
        /// The schedule's normalized hash, on its own. It stays a bare hash in
        /// <c>result.json</c> so a later run can be compared to this one by
        /// equality; the readable "N fault(s), hash" form belongs in the
        /// markdown and is composed there.
        /// </summary>
        public string ScheduleHash = string.Empty;

        public int ScheduleFaultCount;
        public DateTime StartedUtc = DateTime.UtcNow;
        public DateTime FinishedUtc = DateTime.UtcNow;

        /// <summary>Ctrl+C or termination: everything after it is partial.</summary>
        public bool Interrupted;

        /// <summary>
        /// A rehearsal shorter than the window the suite specifies. It is
        /// recorded rather than rejected so a scenario can be debugged, and
        /// stated loudly enough that such a run cannot be cited by accident.
        /// </summary>
        public bool BelowSpecifiedWindow;

        /// <summary>Set when the run failed outside any check.</summary>
        public int ExplicitExitCode = ExitCodes.Success;

        public readonly List<string> SetupGaps = new List<string>();
        public readonly List<string> CleanupProblems = new List<string>();

        public double ElapsedSeconds => (FinishedUtc - StartedUtc).TotalSeconds;

        /// <summary>
        /// The exit code the process returns.
        /// <para/>
        /// The order is the meaning: an interrupt explains everything after it,
        /// a failure outside any check is worth more than the check count it
        /// produced, and cleanup that did not reconcile fails a run whose
        /// assertions all passed.
        /// </summary>
        public int Resolve(CheckRunner runner)
        {
            if (Interrupted) return ExitCodes.Interrupted;
            if (ExplicitExitCode != ExitCodes.Success) return ExplicitExitCode;
            if (runner.Failed > 0) return ExitCodes.CheckFailed;
            if (CleanupProblems.Count > 0) return ExitCodes.CleanupFailed;
            return ExitCodes.Success;
        }

        public static string Describe(int exitCode)
        {
            switch (exitCode)
            {
                case ExitCodes.Success: return "every selected check passed and cleanup reconciled";
                case ExitCodes.Usage: return "the command line could not be understood";
                case ExitCodes.PrerequisiteMissing: return "a prerequisite was missing; this is not a product finding";
                case ExitCodes.CheckFailed: return "at least one check observed a wrong outcome";
                case ExitCodes.Timeout: return "a bounded wait expired";
                case ExitCodes.CleanupFailed: return "cleanup did not reconcile";
                case ExitCodes.Interrupted: return "interrupted; the summary is partial";
                default: return "unexpected failure";
            }
        }

        /// <summary>
        /// Writes every result file and returns the resolved exit code.
        /// </summary>
        public int Write(
            RunArtifacts artifacts,
            CheckRunner runner,
            WorkloadCounters counters,
            IScenarioSummary? scenario = null)
        {
            int resolved = Resolve(runner);
            string json = BuildJson(runner, counters, scenario, resolved);

            artifacts.WriteText(artifacts.ResultPath, json);
            artifacts.WriteText(artifacts.SummaryJsonPath, json);
            artifacts.WriteText(
                artifacts.SummaryMarkdownPath,
                BuildMarkdown(runner, counters, scenario, resolved));

            artifacts.Out(string.Empty);
            artifacts.Out("checks     " + runner.Passed + " passed, " + runner.Failed + " failed, " +
                          runner.Skipped + " skipped in " +
                          ElapsedSeconds.ToString("F0", CultureInfo.InvariantCulture) + "s");

            if (SetupGaps.Count > 0)
                artifacts.Out("setup      " + SetupGaps.Count + " gap(s) recorded in environment.json");

            foreach (string problem in CleanupProblems)
                artifacts.Error("cleanup    " + problem);

            artifacts.Out("exit       " + resolved + "  " + Describe(resolved));
            return resolved;
        }

        private string BuildJson(
            CheckRunner runner,
            WorkloadCounters counters,
            IScenarioSummary? scenario,
            int resolved)
        {
            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("campaignId", CampaignId);
                json.Prop("scenarioId", ScenarioId);
                json.Prop("mode", Mode);
                json.Prop("seed", Seed);
                json.Prop("scheduleHash", ScheduleHash);
                json.Prop("scheduleFaultCount", ScheduleFaultCount);
                json.Prop("targetFramework", RuntimeFacts.TargetFrameworkMoniker);

                if (scenario != null) scenario.WriteJson(json);

                json.Prop("startedUtc", StartedUtc.ToString("o", CultureInfo.InvariantCulture));
                json.Prop("finishedUtc", FinishedUtc.ToString("o", CultureInfo.InvariantCulture));
                json.Prop("elapsedSeconds", ElapsedSeconds);
                json.Prop("interrupted", Interrupted);
                json.Prop("belowSpecifiedWindow", BelowSpecifiedWindow);
                json.Prop("exitCode", resolved);

                json.Object("counters", () =>
                {
                    json.Prop("operations", counters.Operations);
                    json.Prop("successes", counters.Successes);
                    json.Prop("expectedFailures", counters.ExpectedFailures);
                    json.Prop("unexpectedFailures", counters.UnexpectedFailures);
                    json.Prop("cancellations", counters.Cancellations);
                });

                json.Object("checks", () =>
                {
                    json.Prop("passed", runner.Passed);
                    json.Prop("failed", runner.Failed);
                    json.Prop("skipped", runner.Skipped);
                });

                // Per-phase totals, so a scenario whose phases are independent
                // capabilities can show one failing without the reader having to
                // decide from a flat list which claims survived.
                json.Array("checksByPhase", () =>
                {
                    foreach (PhaseTotals totals in Totals(runner))
                    {
                        json.Object(null, () =>
                        {
                            json.Prop("phase", totals.Phase);
                            json.Prop("passed", totals.Passed);
                            json.Prop("failed", totals.Failed);
                            json.Prop("skipped", totals.Skipped);
                        });
                    }
                });

                json.Array("setupGaps", () =>
                {
                    foreach (string gap in SetupGaps) json.Item(gap);
                });

                json.Array("cleanupProblems", () =>
                {
                    foreach (string problem in CleanupProblems) json.Item(problem);
                });

                json.Array("results", () =>
                {
                    foreach (CheckResult result in runner.Results)
                    {
                        json.Object(null, () =>
                        {
                            json.Prop("phase", result.Phase);
                            json.Prop("name", result.Name);
                            json.Prop("claim", result.Claim);
                            json.Prop("passed", result.Passed);
                            json.Prop("skipped", result.Skipped);
                            json.Prop("detail", result.Detail);
                            json.Prop("durationMs", result.DurationMs);
                            json.Array("notes", () =>
                            {
                                foreach (string note in result.Notes) json.Item(note);
                            });
                        });
                    }
                });
            });

            return json.ToString();
        }

        private string BuildMarkdown(
            CheckRunner runner,
            WorkloadCounters counters,
            IScenarioSummary? scenario,
            int resolved)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# " + ScenarioId + " " + Mode + " - " + CampaignId);
            text.AppendLine();
            text.AppendLine("| | |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Target | " + RuntimeFacts.TargetFrameworkMoniker + " |");
            text.AppendLine("| Runtime | " + RuntimeFacts.RuntimeDescription + " |");

            if (scenario != null)
            {
                foreach (KeyValuePair<string, string> fact in scenario.Facts)
                    text.AppendLine("| " + fact.Key + " | " + Escape(fact.Value) + " |");
            }

            text.AppendLine("| Seed | " + Seed.ToString(CultureInfo.InvariantCulture) + " |");
            text.AppendLine("| Schedule | " +
                            ScheduleFaultCount.ToString(CultureInfo.InvariantCulture) +
                            " fault(s), " + ScheduleHash + " |");
            text.AppendLine("| Elapsed | " + ElapsedSeconds.ToString("F0", CultureInfo.InvariantCulture) + "s |");
            text.AppendLine("| Exit code | " + resolved + " - " + Describe(resolved) + " |");
            text.AppendLine();

            if (BelowSpecifiedWindow)
            {
                text.AppendLine("> **This run is below the window the suite specifies for its mode.** It must not be " +
                                "cited as evidence for that mode.");
                text.AppendLine();
            }

            if (Interrupted)
            {
                text.AppendLine("> **This run was interrupted.** Everything below is partial.");
                text.AppendLine();
            }

            if (SetupGaps.Count > 0)
            {
                text.AppendLine("## Setup gaps");
                text.AppendLine();
                foreach (string gap in SetupGaps) text.AppendLine("- " + gap);
                text.AppendLine();
            }

            List<PhaseTotals> phases = Totals(runner);
            if (phases.Count > 1)
            {
                text.AppendLine("## Phases");
                text.AppendLine();
                text.AppendLine("| Phase | Passed | Failed | Skipped |");
                text.AppendLine("|---|---|---|---|");

                foreach (PhaseTotals totals in phases)
                {
                    text.AppendLine("| " + totals.Phase + " | " + totals.Passed + " | " +
                                    (totals.Failed > 0 ? "**" + totals.Failed + "**" : "0") + " | " +
                                    totals.Skipped + " |");
                }

                text.AppendLine();
            }

            text.AppendLine("## Checks");
            text.AppendLine();
            text.AppendLine("| Phase | Check | Result | Detail |");
            text.AppendLine("|---|---|---|---|");

            foreach (CheckResult result in runner.Results)
            {
                string outcome = result.Skipped ? "skipped" : result.Passed ? "pass" : "**FAIL**";
                text.AppendLine("| " + result.Phase + " | " + result.Name + " | " + outcome + " | " +
                                Escape(result.Detail) + " |");
            }

            text.AppendLine();
            text.AppendLine("## Counters");
            text.AppendLine();
            text.AppendLine("- operations: " + counters.Operations);
            text.AppendLine("- successes: " + counters.Successes);
            text.AppendLine("- expected failures: " + counters.ExpectedFailures);
            text.AppendLine("- unexpected failures: " + counters.UnexpectedFailures);
            text.AppendLine("- cancellations: " + counters.Cancellations);

            if (CleanupProblems.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Cleanup problems");
                text.AppendLine();
                foreach (string problem in CleanupProblems) text.AppendLine("- " + problem);
            }

            return text.ToString();
        }

        /// <summary>Totals per phase, in the order the phases first appeared.</summary>
        private static List<PhaseTotals> Totals(CheckRunner runner)
        {
            List<PhaseTotals> ordered = new List<PhaseTotals>();
            Dictionary<string, PhaseTotals> byPhase =
                new Dictionary<string, PhaseTotals>(StringComparer.Ordinal);

            foreach (CheckResult result in runner.Results)
            {
                PhaseTotals? totals;
                if (!byPhase.TryGetValue(result.Phase, out totals))
                {
                    totals = new PhaseTotals(result.Phase);
                    byPhase.Add(result.Phase, totals);
                    ordered.Add(totals);
                }

                if (result.Skipped) totals.Skipped++;
                else if (result.Passed) totals.Passed++;
                else totals.Failed++;
            }

            return ordered;
        }

        private static string Escape(string text) =>
            (text ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        private sealed class PhaseTotals
        {
            public PhaseTotals(string phase) { Phase = phase; }

            public string Phase;
            public int Passed;
            public int Failed;
            public int Skipped;
        }
    }
}

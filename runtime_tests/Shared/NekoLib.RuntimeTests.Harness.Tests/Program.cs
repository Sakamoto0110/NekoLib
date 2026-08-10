#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.RuntimeTests.Harness.Tests
{
    /// <summary>
    /// Automated checks for the harness itself, chiefly the long-run retention
    /// contract.
    /// <para/>
    /// The defect these guard against is not a wrong answer, it is a right
    /// answer that costs too much memory to produce: <c>CheckRunner</c> used to
    /// hold every result alive, and E3-OBS's drift check fails a heap that rises
    /// at every sample. Over four hours the harness would have been the leak it
    /// was looking for. Everything below therefore asserts the same thing from
    /// different sides — the counts stay exact while the detail stops growing.
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            Suite suite = new Suite();

            suite.Run("counts stay exact while retention is bounded", CountsStayExact);
            suite.Run("successes stop growing once the capacity is reached", SuccessesAreBounded);
            suite.Run("failures and skips are never sampled away", FailuresAndSkipsSurvive);
            suite.Run("per-phase totals are counted, not derived from the sample", PhaseTotalsAreExact);
            suite.Run("a failure after the cap still drives the exit code", ExitCodeSurvivesSampling);
            suite.Run("the incremental log holds every result in order", IncrementalLogIsComplete);
            suite.Run("short modes keep every result and write no log", ShortModesAreUnchanged);
            suite.Run("an interrupted run still leaves usable evidence", InterruptionLeavesEvidence);
            suite.Run("retention policy is chosen by mode", PolicyByMode);

            return suite.Report();
        }

        private const string Ok = "ok";

        // ---------------------------------------------------------------- tests

        private static void CountsStayExact(Assert assert)
        {
            CheckRunner runner = Runner(CheckRetention.Bounded(4));

            const int passes = 5000;
            for (int i = 0; i < passes; i++)
                Pass(runner, "phase", "check-" + (i % 10));

            for (int i = 0; i < 7; i++)
                Fail(runner, "phase", "broken-" + i);

            for (int i = 0; i < 3; i++)
                runner.Skip("phase", "skipped-" + i, "claim", "reason");

            assert.Equal(passes, runner.Passed, "passed");
            assert.Equal(7, runner.Failed, "failed");
            assert.Equal(3, runner.Skipped, "skipped");
            assert.Equal(passes + 10, runner.TotalRecorded, "total recorded");
            assert.That(runner.DetailTruncated, "the run should report its detail as truncated");
        }

        private static void SuccessesAreBounded(Assert assert)
        {
            // Ten distinct names against a capacity of four: the sample keeps
            // four of them, and the ten-thousandth repetition adds nothing.
            CheckRunner runner = Runner(CheckRetention.Bounded(4));

            for (int i = 0; i < 10000; i++)
                Pass(runner, "phase", "check-" + (i % 10));

            assert.Equal(4, runner.RetainedCount, "retained successes");
            assert.Equal(10000, runner.Passed, "passed");

            // And the bound holds however many distinct names arrive.
            CheckRunner wide = Runner(CheckRetention.Bounded(4));
            for (int i = 0; i < 500; i++)
                Pass(wide, "phase", "distinct-" + i);

            assert.Equal(4, wide.RetainedCount, "retained successes with 500 distinct names");
            assert.Equal(500, wide.Passed, "passed with 500 distinct names");
        }

        private static void FailuresAndSkipsSurvive(Assert assert)
        {
            // Capacity 1: successes are sampled almost entirely away, and every
            // failure and skip must still be present in full.
            CheckRunner runner = Runner(CheckRetention.Bounded(1));

            for (int i = 0; i < 2000; i++) Pass(runner, "phase", "green-" + (i % 50));
            for (int i = 0; i < 40; i++) Fail(runner, "phase", "red-" + i);
            for (int i = 0; i < 25; i++) runner.Skip("phase", "grey-" + i, "claim", "reason");

            int failures = 0;
            int skips = 0;
            foreach (CheckResult result in runner.Results)
            {
                if (result.Skipped) skips++;
                else if (!result.Passed) failures++;
            }

            assert.Equal(40, failures, "retained failures");
            assert.Equal(25, skips, "retained skips");
            assert.Equal(40, runner.Failed, "counted failures");
            assert.Equal(25, runner.Skipped, "counted skips");

            // The detail of a failure is what a reader acts on, so it must be intact.
            foreach (CheckResult result in runner.Results)
            {
                if (!result.Passed && !result.Skipped)
                    assert.That(result.Detail.Length > 0, "a retained failure lost its detail");
            }
        }

        private static void PhaseTotalsAreExact(Assert assert)
        {
            CheckRunner runner = Runner(CheckRetention.Bounded(2));

            for (int i = 0; i < 300; i++) Pass(runner, "logging", "log-" + (i % 9));
            for (int i = 0; i < 120; i++) Pass(runner, "telemetry", "tel-" + (i % 5));
            for (int i = 0; i < 4; i++) Fail(runner, "telemetry", "tel-broken-" + i);
            for (int i = 0; i < 6; i++) runner.Skip("inspection", "ins-" + i, "claim", "reason");

            Dictionary<string, CheckPhaseTotals> byPhase =
                new Dictionary<string, CheckPhaseTotals>(StringComparer.Ordinal);
            foreach (CheckPhaseTotals totals in runner.PhaseTotals) byPhase[totals.Phase] = totals;

            assert.Equal(3, runner.PhaseTotals.Count, "distinct phases");

            assert.Equal(300, byPhase["logging"].Passed, "logging passed");
            assert.Equal(0, byPhase["logging"].Failed, "logging failed");

            assert.Equal(120, byPhase["telemetry"].Passed, "telemetry passed");
            assert.Equal(4, byPhase["telemetry"].Failed, "telemetry failed");

            assert.Equal(6, byPhase["inspection"].Skipped, "inspection skipped");

            // Phase order is first-appearance order, which the summary relies on.
            assert.Equal("logging", runner.PhaseTotals[0].Phase, "first phase");
            assert.Equal("telemetry", runner.PhaseTotals[1].Phase, "second phase");
            assert.Equal("inspection", runner.PhaseTotals[2].Phase, "third phase");
        }

        private static void ExitCodeSurvivesSampling(Assert assert)
        {
            CheckRunner runner = Runner(CheckRetention.Bounded(2));

            // Exhaust the success sample first, so the failure below arrives
            // when nothing further would be retained under a naive policy.
            for (int i = 0; i < 1000; i++) Pass(runner, "phase", "green-" + (i % 20));
            assert.Equal(2, runner.RetainedCount, "the sample should be full before the failure");

            Fail(runner, "phase", "the-one-that-matters");

            assert.Equal(1, runner.Failed, "failed");
            assert.That(!runner.AllPassed, "AllPassed must be false once anything failed");

            RunSummary summary = new RunSummary();
            assert.Equal(ExitCodes.CheckFailed, summary.Resolve(runner), "resolved exit code");

            bool found = false;
            foreach (CheckResult result in runner.Results)
                if (result.Name == "the-one-that-matters") found = true;

            assert.That(found, "the failing result must still be retained");
        }

        private static void IncrementalLogIsComplete(Assert assert)
        {
            using (Scratch scratch = new Scratch())
            using (RunArtifacts artifacts = scratch.Artifacts())
            {
                CheckRunner runner = new CheckRunner(
                    _ => { }, CancellationToken.None, CheckRetention.Bounded(2), artifacts.AppendCheck);

                List<string> expected = new List<string>();
                for (int i = 0; i < 200; i++)
                {
                    string name = "check-" + i;
                    if (i % 17 == 0) Fail(runner, "phase", name);
                    else if (i % 23 == 0) runner.Skip("phase", name, "claim", "reason");
                    else Pass(runner, "phase", name);

                    expected.Add(name);
                }

                assert.That(artifacts.CheckLogPath != null, "a bounded run must write an incremental log");

                List<string> actual = ReadNames(artifacts.CheckLogPath!);
                assert.Equal(expected.Count, actual.Count, "logged results");

                for (int i = 0; i < expected.Count; i++)
                    assert.Equal(expected[i], actual[i], "logged result at index " + i);

                assert.That(runner.RetainedCount < runner.TotalRecorded,
                    "the point of the log is that memory holds less than the log does");
            }
        }

        private static void ShortModesAreUnchanged(Assert assert)
        {
            using (Scratch scratch = new Scratch())
            using (RunArtifacts artifacts = scratch.Artifacts())
            {
                CheckRunner runner = new CheckRunner(
                    _ => { }, CancellationToken.None, CheckRetention.All, artifacts.AppendCheck);

                for (int i = 0; i < 500; i++) Pass(runner, "phase", "check-" + (i % 10));
                Fail(runner, "phase", "broken");

                assert.Equal(501, runner.RetainedCount, "retained results under the default policy");
                assert.Equal(501, runner.TotalRecorded, "total recorded");
                assert.That(!runner.DetailTruncated, "nothing should be reported as truncated");
                assert.That(artifacts.CheckLogPath == null,
                    "a run that retains everything must not create checks.ndjson");
            }
        }

        private static void InterruptionLeavesEvidence(Assert assert)
        {
            using (Scratch scratch = new Scratch())
            using (RunArtifacts artifacts = scratch.Artifacts())
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                CheckRunner runner = new CheckRunner(
                    _ => { }, cancellation.Token, CheckRetention.Bounded(2), artifacts.AppendCheck);

                for (int i = 0; i < 100; i++) Pass(runner, "phase", "green-" + (i % 5));

                cancellation.Cancel();

                // A check caught mid-flight by the interrupt reaches no verdict.
                runner.RunAsync("phase", "in-flight", "claim",
                    _ => throw new OperationCanceledException()).GetAwaiter().GetResult();

                assert.Equal(1, runner.Skipped, "an interrupted check counts as skipped");
                assert.Equal(0, runner.Failed, "an interrupted check must not count as a failure");

                RunSummary summary = new RunSummary
                {
                    CampaignId = "c",
                    ScenarioId = "S",
                    Mode = "Soak",
                    Interrupted = true
                };

                int exit = summary.Write(artifacts, runner, new WorkloadCounters());
                assert.Equal(ExitCodes.Interrupted, exit, "exit code");

                assert.That(File.Exists(artifacts.ResultPath), "result.json must exist after an interrupt");
                assert.That(File.Exists(artifacts.SummaryMarkdownPath), "summary.md must exist after an interrupt");

                string json = File.ReadAllText(artifacts.ResultPath);
                assert.That(json.Contains("\"total\": 101"), "the total must survive the interrupt");
                assert.That(json.Contains("\"detailTruncated\": true"), "truncation must be declared");
                assert.That(json.Contains("checks.ndjson"), "the detail log must be named in the result");

                // The log is the evidence that survives when memory does not.
                assert.Equal(101, ReadNames(artifacts.CheckLogPath!).Count, "results in the incremental log");
            }
        }

        private static void PolicyByMode(Assert assert)
        {
            assert.That(CheckRetention.ForMode(ScenarioMode.Smoke).RetainsEverything,
                "smoke must keep the behaviour it had");
            assert.That(CheckRetention.ForMode(ScenarioMode.RecoveryRehearsal).RetainsEverything,
                "rehearsal must keep the behaviour it had");
            assert.That(!CheckRetention.ForMode(ScenarioMode.Soak).RetainsEverything,
                "soak is the mode that grows without limit");
            assert.Equal(CheckRetention.DefaultSuccessCapacity,
                CheckRetention.ForMode(ScenarioMode.Soak).SuccessCapacity, "soak capacity");
        }

        // ------------------------------------------------------------- helpers

        private static CheckRunner Runner(CheckRetention retention) =>
            new CheckRunner(_ => { }, CancellationToken.None, retention);

        private static void Pass(CheckRunner runner, string phase, string name) =>
            runner.RunAsync(phase, name, "claim", _ => Completed).GetAwaiter().GetResult();

        private static void Fail(CheckRunner runner, string phase, string name) =>
            runner.RunAsync(phase, name, "claim",
                check => { check.That(false, "deliberate failure in " + name); return Completed; })
                .GetAwaiter().GetResult();

        private static Task Completed
        {
            get
            {
#if NET6_0_OR_GREATER
                return Task.CompletedTask;
#else
                return Task.FromResult(0);
#endif
            }
        }

        /// <summary>
        /// Reads the incremental log while its writer still holds it.
        /// <para/>
        /// <c>File.ReadAllLines</c> cannot: it opens sharing only Read, which
        /// denies the writer's existing Write access, and the open fails. The
        /// point of the log is that it is readable during a long run, so reading
        /// it the way an observer would is part of what this asserts. The same
        /// applies to <c>events.jsonl</c> and <c>samples.csv</c>.
        /// </summary>
        private static List<string> ReadNames(string path)
        {
            List<string> names = new List<string>();

            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;

                    Dictionary<string, object?> entry =
                        JsonParser.AsObject(JsonParser.Parse(line), "a logged check");
                    names.Add(JsonParser.RequireString(entry, "name"));
                }
            }

            return names;
        }

        /// <summary>A throwaway artifact root that removes itself.</summary>
        private sealed class Scratch : IDisposable
        {
            public Scratch()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "nekolib-harness-tests-" + Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public RunArtifacts Artifacts() => RunArtifacts.Create(Root, "campaign", "S");

            public void Dispose()
            {
                try { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private sealed class Assert
        {
            public readonly List<string> Failures = new List<string>();

            public void That(bool condition, string message)
            {
                if (!condition) Failures.Add(message);
            }

            public void Equal(long expected, long actual, string what)
            {
                if (expected != actual)
                {
                    Failures.Add(what + ": expected " + expected.ToString(CultureInfo.InvariantCulture) +
                                 ", got " + actual.ToString(CultureInfo.InvariantCulture));
                }
            }

            public void Equal(string? expected, string? actual, string what)
            {
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    Failures.Add(what + ": expected '" + expected + "', got '" + actual + "'");
            }
        }

        private sealed class Suite
        {
            private int _passed;
            private int _failed;

            public void Run(string name, Action<Assert> body)
            {
                Assert assert = new Assert();
                string detail;

                try
                {
                    body(assert);
                    detail = assert.Failures.Count == 0 ? Ok : string.Join("; ", assert.Failures.ToArray());
                }
                catch (Exception ex)
                {
                    assert.Failures.Add("threw " + ex.GetType().Name + ": " + ex.Message);
                    detail = string.Join("; ", assert.Failures.ToArray());
                }

                if (assert.Failures.Count == 0)
                {
                    _passed++;
                    Console.WriteLine("ok  " + name);
                }
                else
                {
                    _failed++;
                    Console.WriteLine("!!  " + name);
                    foreach (string failure in assert.Failures)
                        Console.WriteLine("      . " + failure);
                }
            }

            public int Report()
            {
                Console.WriteLine();
                Console.WriteLine("harness tests  " + _passed + " passed, " + _failed + " failed  (" +
                                  RuntimeFacts.TargetFrameworkMoniker + ")");

                return _failed == 0 ? 0 : 1;
            }
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace NekoLib.RuntimeTests.Harness.Reporting
{
    /// <summary>Raised when a check observes an outcome it did not expect.</summary>
    public sealed class CheckFailure : Exception
    {
        public CheckFailure(string message) : base(message) { }
    }

    /// <summary>
    /// The assertion surface handed to each check.
    /// <para/>
    /// <see cref="Note"/> is as important as <see cref="That"/>: a good part of
    /// what this scenario is for is recording what the provider actually did —
    /// the exception number, which side dropped the connection, whether a
    /// pooled handle came back — and those are observations, not pass criteria.
    /// Turning an observation into an assertion before a baseline exists is how
    /// a scenario starts inventing thresholds.
    /// </summary>
    public sealed class Check
    {
        private readonly List<string> _notes = new List<string>();

        public IReadOnlyList<string> Notes => _notes;

        public void That(bool condition, string message)
        {
            if (!condition) throw new CheckFailure(message);
        }

        public void Equal(long expected, long actual, string what)
        {
            if (expected != actual)
            {
                throw new CheckFailure(
                    what + ": expected " + expected.ToString(CultureInfo.InvariantCulture) +
                    ", got " + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void Equal(string? expected, string? actual, string what)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new CheckFailure(what + ": expected '" + expected + "', got '" + actual + "'");
        }

        public void Note(string text) => _notes.Add(text);
    }

    /// <summary>One check's outcome, as it appears in <c>result.json</c>.</summary>
    public sealed class CheckResult
    {
        public string Phase = string.Empty;
        public string Name = string.Empty;
        public string Claim = string.Empty;
        public bool Passed;
        public bool Skipped;
        public string Detail = string.Empty;
        public double DurationMs;
        public List<string> Notes = new List<string>();
    }

    /// <summary>One phase's totals, counted rather than derived from what was retained.</summary>
    public sealed class CheckPhaseTotals
    {
        internal CheckPhaseTotals(string phase) { Phase = phase; }

        public string Phase { get; }
        public int Passed { get; internal set; }
        public int Failed { get; internal set; }
        public int Skipped { get; internal set; }
    }

    /// <summary>
    /// Runs checks, records their outcomes, and never lets one failure hide the
    /// rest.
    /// <para/>
    /// A run continues after a failed check by design. Stopping at the first
    /// one would make an unattended campaign report a single symptom per
    /// execution, and the whole value of a matrix is knowing which parts of it
    /// held.
    /// <para/>
    /// Counts are counters and detail is a sample. That split is what lets a
    /// four-hour soak report exactly what happened without the record of it
    /// growing until the record itself is what the run measures - see
    /// <see cref="CheckRetention"/>.
    /// </summary>
    public sealed class CheckRunner
    {
        private readonly List<CheckResult> _results = new List<CheckResult>();
        private readonly List<CheckPhaseTotals> _phases = new List<CheckPhaseTotals>();
        private readonly Dictionary<string, CheckPhaseTotals> _phaseIndex =
            new Dictionary<string, CheckPhaseTotals>(StringComparer.Ordinal);
        private readonly HashSet<string> _retainedNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string> _write;
        private readonly System.Threading.CancellationToken _ct;
        private readonly CheckRetention _retention;
        private readonly Action<CheckResult>? _onResult;

        private int _passed;
        private int _failed;
        private int _skipped;

        /// <summary>
        /// The run's cancellation token lets an interrupted check be told apart
        /// from a failing one. Without it, Ctrl+C during a soak reports every
        /// check that was in flight as a failure, and a summary reading
        /// "5 failed" for a run that was simply stopped is a false report.
        /// <para/>
        /// <paramref name="retention"/> bounds only what is kept in memory;
        /// every count below is a counter and stays exact whatever is discarded.
        /// <paramref name="onResult"/> receives every result in order and is
        /// invoked only when retention is bounded, so a short run neither writes
        /// nor pays for an incremental log it does not need.
        /// </summary>
        public CheckRunner(
            Action<string> write,
            System.Threading.CancellationToken ct = default,
            CheckRetention? retention = null,
            Action<CheckResult>? onResult = null)
        {
            _write = write;
            _ct = ct;
            _retention = retention ?? CheckRetention.All;
            _onResult = onResult;
        }

        /// <summary>
        /// The results still held in memory, which is every result unless
        /// retention is bounded. Never use this to count anything: use
        /// <see cref="Passed"/>, <see cref="Failed"/>, <see cref="Skipped"/> and
        /// <see cref="PhaseTotals"/>, which are exact by construction.
        /// </summary>
        public IReadOnlyList<CheckResult> Results => _results;

        public CheckRetention Retention => _retention;

        /// <summary>Every result the run produced, retained or not.</summary>
        public int TotalRecorded => _passed + _failed + _skipped;

        public int RetainedCount => _results.Count;

        /// <summary>True when detail was dropped and only the counts are complete.</summary>
        public bool DetailTruncated => _results.Count < TotalRecorded;

        /// <summary>Per-phase totals, in the order the phases first appeared.</summary>
        public IReadOnlyList<CheckPhaseTotals> PhaseTotals => _phases;

        public int Passed => _passed;

        public int Failed => _failed;

        public int Skipped => _skipped;

        public bool AllPassed => Failed == 0;

        public async Task<bool> RunAsync(string phase, string name, string claim, Func<Check, Task> body)
        {
            Check check = new Check();
            Stopwatch clock = Stopwatch.StartNew();

            CheckResult result = new CheckResult
            {
                Phase = phase,
                Name = name,
                Claim = claim
            };

            try
            {
                await body(check).ConfigureAwait(false);
                result.Passed = true;
                result.Detail = "ok";
            }
            catch (CheckFailure failure)
            {
                result.Passed = false;
                result.Detail = failure.Message;
            }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested)
            {
                // The run is winding down, so this check never reached a verdict.
                // Recorded as skipped rather than failed: an interrupted check
                // proves nothing, and calling it a failure would make Ctrl+C look
                // like a defect. A cancellation that escapes while the run is not
                // cancelling is a scenario bug and still falls through below.
                result.Passed = true;
                result.Skipped = true;
                result.Detail = "interrupted before the check reached a verdict";
            }
            catch (Exception ex)
            {
                // Anything that is not a CheckFailure is the scenario tripping
                // over itself until proven otherwise, so it is labelled that way
                // rather than reported as a library defect.
                result.Passed = false;
                result.Detail = "unexpected " + ex.GetType().Name + ": " + Flatten(ex.Message);
            }
            finally
            {
                clock.Stop();
                result.DurationMs = clock.Elapsed.TotalMilliseconds;
                result.Notes.AddRange(check.Notes);
                Record(result);
            }

            Report(result);
            return result.Passed;
        }

        public void Skip(string phase, string name, string claim, string reason)
        {
            CheckResult result = new CheckResult
            {
                Phase = phase,
                Name = name,
                Claim = claim,
                Passed = true,
                Skipped = true,
                Detail = reason
            };

            Record(result);
            Report(result);
        }

        /// <summary>
        /// The one place a result is counted, retained and streamed.
        /// <para/>
        /// Counting happens first and unconditionally, so no retention decision
        /// can ever change a total. Failures and skips are always kept: they are
        /// what a reader needs, and a run producing them without limit is
        /// already failing for a reason the counts will show.
        /// </summary>
        private void Record(CheckResult result)
        {
            if (result.Skipped) _skipped++;
            else if (result.Passed) _passed++;
            else _failed++;

            CheckPhaseTotals? totals;
            if (!_phaseIndex.TryGetValue(result.Phase, out totals))
            {
                totals = new CheckPhaseTotals(result.Phase);
                _phaseIndex.Add(result.Phase, totals);
                _phases.Add(totals);
            }

            if (result.Skipped) totals.Skipped++;
            else if (result.Passed) totals.Passed++;
            else totals.Failed++;

            if (!_retention.RetainsEverything && _onResult != null)
            {
                // Streamed before the retention decision, so the incremental log
                // holds every result in order even though memory does not.
                try { _onResult(result); } catch { }
            }

            if (ShouldRetain(result)) _results.Add(result);
        }

        private bool ShouldRetain(CheckResult result)
        {
            if (_retention.RetainsEverything) return true;

            // A failure or a skip is always evidence and is never sampled away.
            if (!result.Passed || result.Skipped) return true;

            if (_retainedNames.Count >= _retention.SuccessCapacity) return false;

            // The separator is a unit separator, which cannot occur in a phase
            // or a check name, so ("a","bc") and ("ab","c") never collide
            // into one sample slot.
            return _retainedNames.Add(result.Phase + "\u001f" + result.Name);
        }

        private void Report(CheckResult result)
        {
            string marker = result.Skipped ? "--" : result.Passed ? "ok" : "!!";
            _write(
                marker + "  " + result.Name.PadRight(38) +
                result.DurationMs.ToString("F0", CultureInfo.InvariantCulture).PadLeft(6) + "ms  " +
                result.Detail);

            foreach (string note in result.Notes)
                _write("      . " + note);
        }

        private static string Flatten(string text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }
}

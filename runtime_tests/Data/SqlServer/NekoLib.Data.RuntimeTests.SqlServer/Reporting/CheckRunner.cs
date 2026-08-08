#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace NekoLib.Data.RuntimeTests.SqlServer.Reporting
{
    /// <summary>Raised when a check observes an outcome it did not expect.</summary>
    internal sealed class CheckFailure : Exception
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
    internal sealed class Check
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
    internal sealed class CheckResult
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

    /// <summary>
    /// Runs checks, records their outcomes, and never lets one failure hide the
    /// rest.
    /// <para/>
    /// A run continues after a failed check by design. Stopping at the first
    /// one would make an unattended campaign report a single symptom per
    /// execution, and the whole value of a matrix is knowing which parts of it
    /// held.
    /// </summary>
    internal sealed class CheckRunner
    {
        private readonly List<CheckResult> _results = new List<CheckResult>();
        private readonly Action<string> _write;

        public CheckRunner(Action<string> write)
        {
            _write = write;
        }

        public IReadOnlyList<CheckResult> Results => _results;

        public int Passed
        {
            get
            {
                int count = 0;
                foreach (CheckResult result in _results)
                    if (result.Passed && !result.Skipped) count++;

                return count;
            }
        }

        public int Failed
        {
            get
            {
                int count = 0;
                foreach (CheckResult result in _results)
                    if (!result.Passed && !result.Skipped) count++;

                return count;
            }
        }

        public int Skipped
        {
            get
            {
                int count = 0;
                foreach (CheckResult result in _results)
                    if (result.Skipped) count++;

                return count;
            }
        }

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
                _results.Add(result);
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

            _results.Add(result);
            Report(result);
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

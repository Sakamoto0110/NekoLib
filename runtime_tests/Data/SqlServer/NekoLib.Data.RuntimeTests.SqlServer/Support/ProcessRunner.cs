#nullable enable
using System;
using System.Diagnostics;
using System.Text;

namespace NekoLib.Data.RuntimeTests.SqlServer.Support
{
    /// <summary>The outcome of one external command.</summary>
    internal sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError, bool timedOut)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
            TimedOut = timedOut;
        }

        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool TimedOut { get; }

        public bool Succeeded => !TimedOut && ExitCode == 0;

        public string Trimmed => StandardOutput.Trim();

        public string Diagnostic
        {
            get
            {
                if (TimedOut) return "timed out";
                string error = StandardError.Trim();
                if (error.Length > 0) return "exit " + ExitCode + ": " + Flatten(error);
                return "exit " + ExitCode + ": " + Flatten(StandardOutput.Trim());
            }
        }

        private static string Flatten(string text) =>
            text.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /// <summary>
    /// Runs an external command with a bounded wait and captures both streams.
    /// <para/>
    /// Arguments are passed as an already-built argument string, and the caller
    /// is responsible for never placing a secret in one: a command line is
    /// visible to every process on the machine, so the SQL Server password is
    /// never allowed to travel this way.
    /// </summary>
    internal static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, string arguments, TimeSpan timeout)
        {
            ProcessStartInfo start = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = start;
                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try { process.Kill(); } catch { /* already gone */ }
                    return new ProcessResult(-1, output.ToString(), error.ToString(), timedOut: true);
                }

                // WaitForExit(int) can return before the async readers drain.
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, output.ToString(), error.ToString(), timedOut: false);
            }
        }
    }
}

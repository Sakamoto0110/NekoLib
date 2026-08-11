#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal sealed class OwnedProcess
    {
        public int Id;
        public string Role = string.Empty;
        public string ExpectedPath = string.Empty;
        public DateTime StartTimeUtc;
    }

    /// <summary>
    /// Tracks only processes this run started or adopted from the exact
    /// Watchdog status identity. Cleanup never selects by a broad process name.
    /// </summary>
    internal sealed class OwnedProcesses : IDisposable
    {
        private readonly List<OwnedProcess> _owned = new List<OwnedProcess>();

        public Process StartChild(string path, string arguments, string workingDirectory)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(start) ??
                throw new InvalidOperationException("The E3-WDOG application child did not start.");
            Adopt(process.Id, "child", path);
            return process;
        }

        public OwnedProcess Adopt(int pid, string role, string expectedPath)
        {
            string expected = Path.GetFullPath(expectedPath);
            string actual;
            DateTime started;
            using (Process process = Process.GetProcessById(pid))
            {
                ReadIdentity(process, TimeSpan.FromSeconds(5), out actual, out started);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Refusing to adopt " + role + " pid " + pid + ": expected '" + expected +
                        "', observed '" + actual + "'.");
                }
            }

            OwnedProcess? existing = _owned.Find(item =>
                item.Id == pid &&
                string.Equals(item.ExpectedPath, expected, StringComparison.OrdinalIgnoreCase) &&
                item.StartTimeUtc == started);
            if (existing != null) return existing;

            OwnedProcess owned = new OwnedProcess
            {
                Id = pid,
                Role = role,
                ExpectedPath = expected,
                StartTimeUtc = started
            };
            _owned.Add(owned);
            return owned;
        }

        public int[] LiveIds(string expectedPath)
        {
            List<int> ids = new List<int>();
            string expected = Path.GetFullPath(expectedPath);
            string processName = Path.GetFileNameWithoutExtension(expected);

            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!process.HasExited &&
                        string.Equals(ReadPath(process), expected, StringComparison.OrdinalIgnoreCase))
                        ids.Add(process.Id);
                }
                catch { }
                finally { process.Dispose(); }
            }

            return ids.ToArray();
        }

        public bool IsLive(OwnedProcess owned)
        {
            try
            {
                using (Process process = Process.GetProcessById(owned.Id))
                {
                    return !process.HasExited && Matches(process, owned);
                }
            }
            catch { return false; }
        }

        public bool WaitForExit(OwnedProcess owned, TimeSpan timeout)
        {
            try
            {
                using (Process process = Process.GetProcessById(owned.Id))
                {
                    if (!Matches(process, owned)) return true;
                    return process.WaitForExit((int)Math.Min(int.MaxValue, timeout.TotalMilliseconds));
                }
            }
            catch { return true; }
        }

        public bool KillExact(OwnedProcess owned, out string diagnostic)
        {
            try
            {
                using (Process process = Process.GetProcessById(owned.Id))
                {
                    if (!Matches(process, owned))
                    {
                        diagnostic = "pid identity changed; no action taken";
                        return false;
                    }

                    if (process.HasExited)
                    {
                        diagnostic = "already exited";
                        return true;
                    }

                    process.Kill();
                    bool exited = process.WaitForExit(10000);
                    diagnostic = exited ? "forced exact pid" : "exact pid did not exit";
                    return exited;
                }
            }
            catch (ArgumentException)
            {
                diagnostic = "already exited";
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public IReadOnlyList<OwnedProcess> All => _owned;

        private static bool Matches(Process process, OwnedProcess owned)
        {
            if (!string.Equals(ReadPath(process), owned.ExpectedPath, StringComparison.OrdinalIgnoreCase))
                return false;

            return process.StartTime.ToUniversalTime() == owned.StartTimeUtc;
        }

        private static void ReadIdentity(
            Process process,
            TimeSpan timeout,
            out string path,
            out DateTime startedUtc)
        {
            Stopwatch wait = Stopwatch.StartNew();
            Exception? lastError = null;

            while (wait.Elapsed < timeout)
            {
                try
                {
                    process.Refresh();
                    if (process.HasExited)
                        throw new InvalidOperationException(
                            "The process exited before its exact identity could be read.");

                    path = ReadPath(process);
                    startedUtc = process.StartTime.ToUniversalTime();
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(25);
                }
            }

            throw new InvalidOperationException(
                "The exact process image path and start time were unavailable within " +
                timeout.TotalSeconds + " seconds.",
                lastError);
        }

        private static string ReadPath(Process process)
        {
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("The process image path is unavailable.");
            return Path.GetFullPath(path);
        }

        public void Dispose()
        {
            // Process handles are opened per operation. Ownership records remain
            // intentionally inert until the scenario explicitly reconciles them.
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal sealed class Counter
    {
        private long _value;
        public long Value => Interlocked.Read(ref _value);
        public void Increment() => Interlocked.Increment(ref _value);
        public void Set(long value) => Interlocked.Exchange(ref _value, value);
    }

    internal sealed class WatchdogSamples : IScenarioSamples
    {
        private static readonly string[] Columns =
        {
            "child_generations",
            "armed_faults",
            "crash_bundles_retained",
            "pending_crash_directories",
            "live_children",
            "live_hosts",
            "supervised_private_bytes",
            "supervised_threads",
            "supervised_handles"
        };

        private readonly string _runRoot;
        private readonly DeploymentLayoutEvidence _layout;
        private readonly OwnedProcesses _processes;

        public WatchdogSamples(
            string runRoot,
            DeploymentLayoutEvidence layout,
            OwnedProcesses processes)
        {
            _runRoot = runRoot;
            _layout = layout;
            _processes = processes;
        }

        public static IReadOnlyList<string> ColumnNamesForHeader => Columns;
        public IReadOnlyList<string> ColumnNames => Columns;

        public long[] Read()
        {
            long privateBytes = 0;
            long threads = 0;
            long handles = 0;

            foreach (OwnedProcess owned in _processes.All)
            {
                try
                {
                    if (!_processes.IsLive(owned)) continue;
                    using (Process process = Process.GetProcessById(owned.Id))
                    {
                        if (process.HasExited) continue;
                        process.Refresh();
                        privateBytes += process.PrivateMemorySize64;
                        threads += process.Threads.Count;
                        handles += process.HandleCount;
                    }
                }
                catch { }
            }

            return new[]
            {
                CountFiles(Path.Combine(_runRoot, "state", "generations"), "generation-*.json"),
                CountFiles(Path.Combine(_runRoot, "state", "armed"), "*.json"),
                CountDirectories(Path.Combine(_runRoot, "crash", "bundles"), "bundle-*"),
                CountDirectories(Path.Combine(_runRoot, "crash", "pending"), "crash-*"),
                (long)_processes.LiveIds(_layout.ChildPath).Length,
                (long)_processes.LiveIds(_layout.HostPath).Length,
                privateBytes,
                threads,
                handles
            };
        }

        private static long CountFiles(string root, string pattern) =>
            Directory.Exists(root) ? Directory.GetFiles(root, pattern).LongLength : 0;

        private static long CountDirectories(string root, string pattern) =>
            Directory.Exists(root) ? Directory.GetDirectories(root, pattern).LongLength : 0;
    }

    internal sealed class WatchdogScenarioSummary : IScenarioSummary
    {
        private readonly DeploymentLayoutEvidence _layout;
        private readonly IReadOnlyList<string> _boundaries;
        private readonly int _generations;
        private readonly int _armed;
        private readonly int _bundles;

        public WatchdogScenarioSummary(
            DeploymentLayoutEvidence layout,
            IReadOnlyList<string> boundaries,
            int generations,
            int armed,
            int bundles)
        {
            _layout = layout;
            _boundaries = boundaries;
            _generations = generations;
            _armed = armed;
            _bundles = bundles;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Deployment layout", _layout.Kind),
            new KeyValuePair<string, string>("Child", _layout.ChildVersion + " (" + _layout.ChildMachine + ")"),
            new KeyValuePair<string, string>("Watchdog Host", _layout.HostVersion + " (" + _layout.HostMachine + ")"),
            new KeyValuePair<string, string>("Package evidence", _layout.SupportsPackageClaim
                ? _layout.PackageVersion + " sha256:" + _layout.PackageSha256
                : "none; source layout")
        };

        public void WriteJson(JsonWriter json)
        {
            json.Object("deployment", () =>
            {
                json.Prop("layout", _layout.Kind);
                json.Prop("applicationRoot", _layout.ApplicationRoot);
                json.Prop("childPath", _layout.ChildPath);
                json.Prop("childVersion", _layout.ChildVersion);
                json.Prop("childMachine", _layout.ChildMachine);
                json.Prop("hostPath", _layout.HostPath);
                json.Prop("hostVersion", _layout.HostVersion);
                json.Prop("hostMachine", _layout.HostMachine);
                json.Prop("supportsPackageClaim", _layout.SupportsPackageClaim);
                if (_layout.SupportsPackageClaim)
                {
                    json.Prop("packageFile", _layout.PackageFile);
                    json.Prop("packageVersion", _layout.PackageVersion);
                    json.Prop("packageSha256", _layout.PackageSha256);
                    json.Prop("packagePayloadEntry", _layout.PackagePayloadEntry);
                }
            });

            json.Object("watchdogTotals", () =>
            {
                json.Prop("generations", _generations);
                json.Prop("armedFaults", _armed);
                json.Prop("bundlesRetained", _bundles);
            });

            json.Array("claimBoundaries", () =>
            {
                foreach (string boundary in _boundaries) json.Item(boundary);
            });
        }
    }
}

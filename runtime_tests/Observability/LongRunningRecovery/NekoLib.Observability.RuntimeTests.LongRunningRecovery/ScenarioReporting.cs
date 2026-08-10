#nullable enable
using System.Collections.Generic;
using System.Threading;
using NekoLib.Inspection;
using NekoLib.Logging;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Reporting;
using NekoLib.Telemetry;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// The versions only this scenario can name.
    /// <para/>
    /// The harness records the host, the runtime and the repository, but it
    /// references no product module, so which assemblies are under test is
    /// knowledge that has to live here. All three are named separately because
    /// the scenario's whole point is that they are three independent
    /// capabilities that happen to share a process.
    /// </summary>
    internal static class ScenarioFacts
    {
        public static string LoggingVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Logging", typeof(Logger));

        public static string TelemetryVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Telemetry", typeof(TelemetryPipeline));

        public static string InspectionVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Inspection", typeof(InspectionRuntime));

        public static string CoreVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Core", typeof(NekoLib.Core.Logging.LogEntry));

        public static string ScenarioVersion =>
            RuntimeFacts.AssemblyVersion(typeof(ScenarioFacts));
    }

    /// <summary>A counter that is cheap to read from the sampling path.</summary>
    internal sealed class Counter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);

        public void Increment() => Interlocked.Increment(ref _value);

        public void Add(long amount) => Interlocked.Add(ref _value, amount);

        public void Set(long value) => Interlocked.Exchange(ref _value, value);
    }

    /// <summary>
    /// The sample columns only this scenario can fill.
    /// <para/>
    /// The suite requires every sample to carry the active and retained counts
    /// of bounded components. For this scenario that means the three
    /// capabilities' bounded structures, which is why the shared sampler asks
    /// rather than defining columns of its own: E4-SQL's answer is a connection
    /// count and has nothing in common with these.
    /// </summary>
    internal sealed class ScenarioSamples : IScenarioSamples
    {
        private static readonly string[] Columns =
        {
            "log_entries_written",
            "log_files_rolled",
            "log_recent_retained",
            "telemetry_completed",
            "telemetry_retained",
            "inspection_recorded",
            "inspection_retained",
            "inspection_providers"
        };

        private readonly Workload.ObservabilityWorkspace _workspace;

        public ScenarioSamples(Workload.ObservabilityWorkspace workspace)
        {
            _workspace = workspace;
        }

        public static IReadOnlyList<string> ColumnNamesForHeader => Columns;

        public IReadOnlyList<string> ColumnNames => Columns;

        public readonly Counter LogEntriesWritten = new Counter();
        public readonly Counter LogFilesRolled = new Counter();
        public readonly Counter TelemetryCompleted = new Counter();
        public readonly Counter InspectionRecorded = new Counter();

        public long[] Read()
        {
            InspectionRuntimeDiagnostics inspection = _workspace.Inspection.GetDiagnostics();

            return new[]
            {
                LogEntriesWritten.Value,
                LogFilesRolled.Value,
                _workspace.Logger.GetRecentEntries(int.MaxValue).Count,
                TelemetryCompleted.Value,
                _workspace.Telemetry.GetRecentOperations(int.MaxValue).Count,
                InspectionRecorded.Value,
                inspection.RetainedCount,
                inspection.ProviderCount
            };
        }
    }

    /// <summary>
    /// The versions and per-capability totals this scenario adds to the shared
    /// result record.
    /// <para/>
    /// The capability sections are the reason this exists. The suite requires
    /// each capability to be able to fail independently "so a shared process
    /// does not turn them into one claimed feature", and a reader of
    /// <c>result.json</c> has to be able to see that at a glance rather than by
    /// grouping a flat list themselves.
    /// </summary>
    internal sealed class ObservabilitySummary : IScenarioSummary
    {
        private readonly IReadOnlyList<string> _boundaries;

        public ObservabilitySummary(IReadOnlyList<string> claimBoundaries)
        {
            _boundaries = claimBoundaries;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Logging", ScenarioFacts.LoggingVersion),
            new KeyValuePair<string, string>("Telemetry", ScenarioFacts.TelemetryVersion),
            new KeyValuePair<string, string>("Inspection", ScenarioFacts.InspectionVersion),
            new KeyValuePair<string, string>("Core", ScenarioFacts.CoreVersion)
        };

        public void WriteJson(JsonWriter json)
        {
            json.Object("capabilities", () =>
            {
                json.Prop("logging", ScenarioFacts.LoggingVersion);
                json.Prop("telemetry", ScenarioFacts.TelemetryVersion);
                json.Prop("inspection", ScenarioFacts.InspectionVersion);
                json.Prop("core", ScenarioFacts.CoreVersion);
            });

            json.Array("claimBoundaries", () =>
            {
                foreach (string boundary in _boundaries) json.Item(boundary);
            });
        }
    }
}

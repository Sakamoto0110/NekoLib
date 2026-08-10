#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Logging;
using NekoLib.Inspection;
using NekoLib.Logging;
using NekoLib.Logging.Sinks;
using NekoLib.Observability.RuntimeTests.LongRunningRecovery.Sinks;
using NekoLib.Telemetry;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Workload
{
    /// <summary>
    /// The three capabilities composed the way an application would compose
    /// them, plus the scenario-owned sinks the faults act on.
    /// <para/>
    /// This is the long-lived set. Individual checks that need a different
    /// capacity, a different minimum level or a fresh lifecycle build their own;
    /// this one carries the steady traffic and is what the samples describe.
    /// </summary>
    internal sealed class ObservabilityWorkspace : IDisposable
    {
        public const int LogRecentCapacity = 256;
        public const int TelemetryCapacity = 256;
        public const int InspectionCapacity = 512;

        private int _disposed;

        public ObservabilityWorkspace(string workingDirectory, int seed)
        {
            WorkingDirectory = workingDirectory;
            Directory.CreateDirectory(workingDirectory);

            LogFilePath = Path.Combine(workingDirectory, "steady.log");

            Healthy = new CountingLogSink("healthy");
            Secondary = new CountingLogSink("secondary");
            Failing = new ScheduledFailingLogSink(seed, 0.25);
            Blocking = new BlockingFlushLogSink();

            File = new RollingFileLogSink(new RollingFileLogSinkOptions
            {
                FilePath = LogFilePath,
                MaximumFileBytes = 512 * 1024,
                RetainedFileCount = 3
            });

            // The failing sink sits before the healthy ones on purpose: sink
            // isolation only means something if a sink registered after the
            // failure still receives the entry.
            Logger = new Logger(
                new LoggerOptions
                {
                    MinimumLevel = LogLevel.Debug,
                    RecentEntryCapacity = LogRecentCapacity,
                    DisposeSinks = true
                },
                Failing,
                Healthy,
                Secondary,
                Blocking,
                File);

            TelemetrySink = new CountingTelemetrySink();
            Telemetry = new TelemetryPipeline(
                new TelemetryPipelineOptions { RecentOperationCapacity = TelemetryCapacity },
                TelemetrySink);

            Inspection = new InspectionRuntime(new InspectionOptions { Capacity = InspectionCapacity });
        }

        public string WorkingDirectory { get; }
        public string LogFilePath { get; }

        public Logger Logger { get; }
        public CountingLogSink Healthy { get; }
        public CountingLogSink Secondary { get; }
        public ScheduledFailingLogSink Failing { get; }
        public BlockingFlushLogSink Blocking { get; }
        public RollingFileLogSink File { get; }

        public TelemetryPipeline Telemetry { get; }
        public CountingTelemetrySink TelemetrySink { get; }

        public InspectionRuntime Inspection { get; }

        /// <summary>Registrations the workspace owns and must give back at cleanup.</summary>
        public readonly List<IDisposable> Registrations = new List<IDisposable>();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            foreach (IDisposable registration in Registrations)
            {
                try { registration.Dispose(); } catch { }
            }

            Registrations.Clear();

            // Release the blocking sink before disposing the logger: disposal
            // flushes, and a flush that is still blocked would turn cleanup into
            // a two-minute wait.
            try { Blocking.Release(); } catch { }

            try { Logger.Dispose(); } catch { }
            try { Inspection.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// What every phase needs, in one place.
    /// </summary>
    internal sealed class PhaseContext
    {
        public ObservabilityWorkspace Workspace = null!;
        public CheckRunner Runner = null!;
        public WorkloadCounters Counters = null!;
        public ResourceSampler Sampler = null!;
        public RunArtifacts Artifacts = null!;
        public ScenarioSamples Samples = null!;
        public string WorkingDirectory = string.Empty;
        public int Seed;

        /// <summary>Requested sustained write rate, or 0 for unthrottled.</summary>
        public int LogRate;

        public CancellationToken Ct;

        /// <summary>
        /// Serialises assertion work against faults.
        /// <para/>
        /// The soak is the only mode where the two overlap, and E4-SQL's soak
        /// proved they must not: an assertion made while an injected fault is
        /// active measures the fault rather than the library. A matrix holds
        /// this while it runs and a fault holds it while it is applied and
        /// withdrawn; steady background traffic holds nothing and is free to be
        /// counted rather than asserted on.
        /// </summary>
        public readonly SemaphoreSlim ExclusiveAccess = new SemaphoreSlim(1, 1);

        public async Task ExclusiveAsync(Func<Task> work)
        {
            await ExclusiveAccess.WaitAsync(Ct).ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            finally { ExclusiveAccess.Release(); }
        }

        private int _scratch;

        /// <summary>
        /// A scratch path no earlier invocation has used.
        /// <para/>
        /// Every matrix runs once per soak cycle, so a check that wrote to a
        /// fixed file name would be reading its own previous run's contents on
        /// the second pass. The first sustained smoke found exactly that: a
        /// lifecycle check asserting 1000 lines saw 2000 on the second cycle.
        /// </summary>
        public string UniqueWorkPath(string name) =>
            Path.Combine(
                WorkingDirectory,
                Interlocked.Increment(ref _scratch).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "-" + name);

        /// <summary>Runs a call that is expected to throw and returns what it threw.</summary>
        public static async Task<Exception?> CaptureAsync(Func<Task> call)
        {
            try
            {
                await call().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static Exception? Capture(Action call)
        {
            try
            {
                call();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static Task CompletedTask
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

        public static string Flatten(string text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }
}

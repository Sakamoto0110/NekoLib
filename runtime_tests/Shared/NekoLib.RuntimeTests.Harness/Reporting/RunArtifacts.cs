#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.RuntimeTests.Harness.Reporting
{
    /// <summary>
    /// The run directory, laid out the way the Phase E suite specifies.
    /// <para/>
    /// Everything is written as it happens rather than assembled at the end. A
    /// campaign that is killed halfway must still leave the schedule it planned,
    /// the samples it took, and a summary saying it was interrupted — a result
    /// file that only exists on the happy path is not evidence, it is a report.
    /// </summary>
    public sealed class RunArtifacts : IDisposable
    {
        private readonly StreamWriter _stdout;
        private readonly StreamWriter _stderr;
        private readonly StreamWriter _samples;
        private readonly StreamWriter _events;
        private readonly object _sync = new object();
        private bool _disposed;

        private RunArtifacts(
            string aggregateCampaignDirectory,
            string campaignDirectory,
            string scenarioDirectory,
            string? workerId,
            StreamWriter stdout,
            StreamWriter stderr,
            StreamWriter samples,
            StreamWriter events)
        {
            AggregateCampaignDirectory = aggregateCampaignDirectory;
            CampaignDirectory = campaignDirectory;
            ScenarioDirectory = scenarioDirectory;
            WorkerId = workerId;
            _stdout = stdout;
            _stderr = stderr;
            _samples = samples;
            _events = events;
        }

        /// <summary>The orchestrator-owned campaign root, or the standalone campaign directory.</summary>
        public string AggregateCampaignDirectory { get; }

        /// <summary>The directory owned by this process.</summary>
        public string CampaignDirectory { get; }
        public string ScenarioDirectory { get; }
        public string? WorkerId { get; }
        public int ArtifactLayoutVersion => WorkerId == null ? 1 : 2;

        public string EnvironmentPath => Path.Combine(CampaignDirectory, "environment.json");
        public string SchedulePath => Path.Combine(CampaignDirectory, "schedule.json");
        public string SummaryJsonPath => Path.Combine(CampaignDirectory, "summary.json");
        public string SummaryMarkdownPath => Path.Combine(CampaignDirectory, "summary.md");
        public string ResultPath => Path.Combine(ScenarioDirectory, "result.json");

        /// <summary>
        /// Opens the run directory. <paramref name="scenarioColumns"/> are the
        /// scenario's own sample columns, appended to <c>samples.csv</c> after
        /// the ones every scenario shares.
        /// </summary>
        public static RunArtifacts Create(
            string artifactsRoot,
            string campaignId,
            string scenarioId,
            IReadOnlyList<string>? scenarioColumns = null,
            string? workerId = null)
        {
            ValidatePathSegment(campaignId, nameof(campaignId));
            ValidatePathSegment(scenarioId, nameof(scenarioId));
            if (workerId != null) ValidatePathSegment(workerId, nameof(workerId));

            string aggregateCampaign = Path.Combine(artifactsRoot, campaignId);
            string campaign = workerId == null
                ? aggregateCampaign
                : Path.Combine(aggregateCampaign, "workers", workerId);
            string scenario = Path.Combine(campaign, scenarioId);

            Directory.CreateDirectory(scenario);

            StreamWriter stdout = Open(Path.Combine(scenario, "stdout.log"));
            StreamWriter stderr = Open(Path.Combine(scenario, "stderr.log"));
            StreamWriter samples = Open(Path.Combine(scenario, "samples.csv"));
            StreamWriter events = Open(Path.Combine(campaign, "events.jsonl"));

            List<string> header = new List<string>(new[]
            {
                "utc", "phase", "marker",
                "private_bytes", "managed_heap_bytes", "thread_count", "handle_count",
                "operations", "successes",
                "expected_failures", "unexpected_failures", "cancellations",
                "seconds_since_progress"
            });

            if (scenarioColumns != null) header.AddRange(scenarioColumns);

            samples.WriteLine(string.Join(",", header.ToArray()));
            samples.Flush();

            return new RunArtifacts(
                aggregateCampaign,
                campaign,
                scenario,
                workerId,
                stdout,
                stderr,
                samples,
                events);
        }

        private static void ValidatePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ||
                value.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Expected one safe directory name.", parameterName);
            }
        }

        private static StreamWriter Open(string path)
        {
            FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
        }

        /// <summary>Writes one line to the console and to <c>stdout.log</c>.</summary>
        public void Out(string line)
        {
            lock (_sync)
            {
                Console.Out.WriteLine(line);
                if (!_disposed) { _stdout.WriteLine(line); _stdout.Flush(); }
            }
        }

        /// <summary>Writes one line to the error stream and to <c>stderr.log</c>.</summary>
        public void Error(string line)
        {
            lock (_sync)
            {
                Console.Error.WriteLine(line);
                if (!_disposed) { _stderr.WriteLine(line); _stderr.Flush(); }
            }
        }

        public void Event(string kind, Action<JsonWriter> body)
        {
            lock (_sync)
            {
                if (_disposed) return;

                JsonWriter json = new JsonWriter();
                json.Object(null, () =>
                {
                    json.Prop("utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    json.Prop("kind", kind);
                    body(json);
                });

                // One event per line, so a truncated file still parses up to the
                // last complete record.
                _events.WriteLine(json.ToString().Replace("\n", " ").Replace("  ", " "));
                _events.Flush();
            }
        }

        public void Sample(ResourceSample sample)
        {
            lock (_sync)
            {
                if (_disposed) return;

                _samples.WriteLine(sample.ToCsvLine());
                _samples.Flush();
            }
        }

        public void WriteText(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;

                Safely(() => { _stdout.Flush(); _stdout.Dispose(); });
                Safely(() => { _stderr.Flush(); _stderr.Dispose(); });
                Safely(() => { _samples.Flush(); _samples.Dispose(); });
                Safely(() => { _events.Flush(); _events.Dispose(); });
            }
        }

        private static void Safely(Action action)
        {
            try { action(); } catch (IOException) { } catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// The scenario half of a sample.
    /// <para/>
    /// The suite requires every sample to carry "active/retained item counts for
    /// bounded components" and "queue/gate depth", which is a real requirement
    /// with no shared answer: E4-SQL counts connections the factory built,
    /// E3-OBS counts retained log entries and registered state providers. The
    /// harness owns the columns every scenario has and asks the scenario for the
    /// rest, rather than accumulating one column per scenario.
    /// </summary>
    public interface IScenarioSamples
    {
        /// <summary>Column names, appended to <c>samples.csv</c> in this order.</summary>
        IReadOnlyList<string> ColumnNames { get; }

        /// <summary>
        /// Current values, in the same order and of the same length as
        /// <see cref="ColumnNames"/>. Called on the sampling path, so it must be
        /// cheap and must not block.
        /// </summary>
        long[] Read();
    }

    /// <summary>One row of <c>samples.csv</c>.</summary>
    public sealed class ResourceSample
    {
        public DateTime Utc = DateTime.UtcNow;
        public string Phase = string.Empty;

        /// <summary>Why the sample was taken: baseline, warm-up, periodic, pre-fault, post-recovery, final.</summary>
        public string Marker = string.Empty;

        public long PrivateBytes;
        public long ManagedHeapBytes;
        public int ThreadCount;
        public int HandleCount;
        public long Operations;
        public long Successes;
        public long ExpectedFailures;
        public long UnexpectedFailures;
        public long Cancellations;
        public double SecondsSinceProgress;

        /// <summary>The scenario's own columns, in the order it declared them.</summary>
        public long[] Scenario = new long[0];

        public string ToCsvLine()
        {
            List<string> fields = new List<string>(new[]
            {
                Utc.ToString("o", CultureInfo.InvariantCulture),
                Phase,
                Marker,
                PrivateBytes.ToString(CultureInfo.InvariantCulture),
                ManagedHeapBytes.ToString(CultureInfo.InvariantCulture),
                ThreadCount.ToString(CultureInfo.InvariantCulture),
                HandleCount.ToString(CultureInfo.InvariantCulture),
                Operations.ToString(CultureInfo.InvariantCulture),
                Successes.ToString(CultureInfo.InvariantCulture),
                ExpectedFailures.ToString(CultureInfo.InvariantCulture),
                UnexpectedFailures.ToString(CultureInfo.InvariantCulture),
                Cancellations.ToString(CultureInfo.InvariantCulture),
                SecondsSinceProgress.ToString("F1", CultureInfo.InvariantCulture)
            });

            foreach (long value in Scenario)
                fields.Add(value.ToString(CultureInfo.InvariantCulture));

            return string.Join(",", fields.ToArray());
        }
    }

    /// <summary>
    /// The counters every phase updates and every sample reads.
    /// <para/>
    /// Expected failures are counted separately from unexpected ones because a
    /// recovery rehearsal is supposed to produce hundreds of provider errors,
    /// and a single number covering both would make the healthiest run look like
    /// the worst one.
    /// </summary>
    public sealed class WorkloadCounters
    {
        private long _operations;
        private long _successes;
        private long _expectedFailures;
        private long _unexpectedFailures;
        private long _cancellations;
        private long _lastProgressTicks = DateTime.UtcNow.Ticks;

        public long Operations => System.Threading.Interlocked.Read(ref _operations);
        public long Successes => System.Threading.Interlocked.Read(ref _successes);
        public long ExpectedFailures => System.Threading.Interlocked.Read(ref _expectedFailures);
        public long UnexpectedFailures => System.Threading.Interlocked.Read(ref _unexpectedFailures);
        public long Cancellations => System.Threading.Interlocked.Read(ref _cancellations);

        public DateTime LastProgressUtc =>
            new DateTime(System.Threading.Interlocked.Read(ref _lastProgressTicks), DateTimeKind.Utc);

        public double SecondsSinceProgress => (DateTime.UtcNow - LastProgressUtc).TotalSeconds;

        public void Success()
        {
            System.Threading.Interlocked.Increment(ref _operations);
            System.Threading.Interlocked.Increment(ref _successes);
            Progress();
        }

        public void ExpectedFailure()
        {
            System.Threading.Interlocked.Increment(ref _operations);
            System.Threading.Interlocked.Increment(ref _expectedFailures);
            Progress();
        }

        public void UnexpectedFailure()
        {
            System.Threading.Interlocked.Increment(ref _operations);
            System.Threading.Interlocked.Increment(ref _unexpectedFailures);
            Progress();
        }

        public void Cancellation()
        {
            System.Threading.Interlocked.Increment(ref _operations);
            System.Threading.Interlocked.Increment(ref _cancellations);
            Progress();
        }

        private void Progress() =>
            System.Threading.Interlocked.Exchange(ref _lastProgressTicks, DateTime.UtcNow.Ticks);
    }

    /// <summary>Takes samples at the points the suite requires and hands them to the artifacts.</summary>
    public sealed class ResourceSampler
    {
        private readonly RunArtifacts _artifacts;
        private readonly WorkloadCounters _counters;
        private readonly IScenarioSamples? _scenario;
        private readonly int _scenarioColumnCount;
        private readonly List<ResourceSample> _taken = new List<ResourceSample>();

        public ResourceSampler(
            RunArtifacts artifacts,
            WorkloadCounters counters,
            IScenarioSamples? scenario = null)
        {
            _artifacts = artifacts;
            _counters = counters;
            _scenario = scenario;
            _scenarioColumnCount = scenario == null ? 0 : scenario.ColumnNames.Count;
        }

        public IReadOnlyList<ResourceSample> Taken => _taken;

        public ResourceSample Take(string phase, string marker)
        {
            RuntimeFacts.SampleProcess(
                out long privateBytes,
                out long managedHeap,
                out int threads,
                out int handles);

            ResourceSample sample = new ResourceSample
            {
                Phase = phase,
                Marker = marker,
                PrivateBytes = privateBytes,
                ManagedHeapBytes = managedHeap,
                ThreadCount = threads,
                HandleCount = handles,
                Operations = _counters.Operations,
                Successes = _counters.Successes,
                ExpectedFailures = _counters.ExpectedFailures,
                UnexpectedFailures = _counters.UnexpectedFailures,
                Cancellations = _counters.Cancellations,
                SecondsSinceProgress = _counters.SecondsSinceProgress,
                Scenario = ReadScenarioColumns()
            };

            _taken.Add(sample);
            _artifacts.Sample(sample);
            return sample;
        }

        /// <summary>
        /// Reads the scenario's columns, tolerating a reader that throws or
        /// returns the wrong arity. A sample is an observation; losing the whole
        /// run because one counter misbehaved during cleanup would be the wrong
        /// trade, and a row of zeroes is visibly different from a plausible one.
        /// </summary>
        private long[] ReadScenarioColumns()
        {
            if (_scenario == null || _scenarioColumnCount == 0) return new long[0];

            long[] values;
            try { values = _scenario.Read() ?? new long[0]; }
            catch { values = new long[0]; }

            if (values.Length == _scenarioColumnCount) return values;

            long[] fitted = new long[_scenarioColumnCount];
            for (int i = 0; i < _scenarioColumnCount && i < values.Length; i++)
                fitted[i] = values[i];

            return fitted;
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NekoLib.Data.RuntimeTests.SqlServer.Support;

namespace NekoLib.Data.RuntimeTests.SqlServer.Reporting
{
    /// <summary>
    /// The run directory, laid out the way the Phase E suite specifies.
    /// <para/>
    /// Everything is written as it happens rather than assembled at the end. A
    /// campaign that is killed halfway must still leave the schedule it planned,
    /// the samples it took, and a summary saying it was interrupted — a result
    /// file that only exists on the happy path is not evidence, it is a report.
    /// </summary>
    internal sealed class RunArtifacts : IDisposable
    {
        private readonly StreamWriter _stdout;
        private readonly StreamWriter _stderr;
        private readonly StreamWriter _samples;
        private readonly StreamWriter _events;
        private readonly object _sync = new object();
        private bool _disposed;

        private RunArtifacts(
            string campaignDirectory,
            string scenarioDirectory,
            StreamWriter stdout,
            StreamWriter stderr,
            StreamWriter samples,
            StreamWriter events)
        {
            CampaignDirectory = campaignDirectory;
            ScenarioDirectory = scenarioDirectory;
            _stdout = stdout;
            _stderr = stderr;
            _samples = samples;
            _events = events;
        }

        public string CampaignDirectory { get; }
        public string ScenarioDirectory { get; }

        public string EnvironmentPath => Path.Combine(CampaignDirectory, "environment.json");
        public string SchedulePath => Path.Combine(CampaignDirectory, "schedule.json");
        public string SummaryJsonPath => Path.Combine(CampaignDirectory, "summary.json");
        public string SummaryMarkdownPath => Path.Combine(CampaignDirectory, "summary.md");
        public string ResultPath => Path.Combine(ScenarioDirectory, "result.json");

        public static RunArtifacts Create(string artifactsRoot, string campaignId, string scenarioId)
        {
            string campaign = Path.Combine(artifactsRoot, campaignId);
            string scenario = Path.Combine(campaign, scenarioId);

            Directory.CreateDirectory(scenario);

            StreamWriter stdout = Open(Path.Combine(scenario, "stdout.log"));
            StreamWriter stderr = Open(Path.Combine(scenario, "stderr.log"));
            StreamWriter samples = Open(Path.Combine(scenario, "samples.csv"));
            StreamWriter events = Open(Path.Combine(campaign, "events.jsonl"));

            samples.WriteLine(string.Join(",", new[]
            {
                "utc", "phase", "marker",
                "private_bytes", "managed_heap_bytes", "thread_count", "handle_count",
                "connections_created", "operations", "successes",
                "expected_failures", "unexpected_failures", "cancellations",
                "server_sessions", "seconds_since_progress"
            }));
            samples.Flush();

            return new RunArtifacts(campaign, scenario, stdout, stderr, samples, events);
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

    /// <summary>One row of <c>samples.csv</c>.</summary>
    internal sealed class ResourceSample
    {
        public DateTime Utc = DateTime.UtcNow;
        public string Phase = string.Empty;

        /// <summary>Why the sample was taken: baseline, warm-up, periodic, pre-fault, post-recovery, final.</summary>
        public string Marker = string.Empty;

        public long PrivateBytes;
        public long ManagedHeapBytes;
        public int ThreadCount;
        public int HandleCount;
        public long ConnectionsCreated;
        public long Operations;
        public long Successes;
        public long ExpectedFailures;
        public long UnexpectedFailures;
        public long Cancellations;

        /// <summary>Sessions the server still attributes to this scenario, or -1 when it could not be asked.</summary>
        public int ServerSessions = -1;

        public double SecondsSinceProgress;

        public string ToCsvLine()
        {
            string[] fields =
            {
                Utc.ToString("o", CultureInfo.InvariantCulture),
                Phase,
                Marker,
                PrivateBytes.ToString(CultureInfo.InvariantCulture),
                ManagedHeapBytes.ToString(CultureInfo.InvariantCulture),
                ThreadCount.ToString(CultureInfo.InvariantCulture),
                HandleCount.ToString(CultureInfo.InvariantCulture),
                ConnectionsCreated.ToString(CultureInfo.InvariantCulture),
                Operations.ToString(CultureInfo.InvariantCulture),
                Successes.ToString(CultureInfo.InvariantCulture),
                ExpectedFailures.ToString(CultureInfo.InvariantCulture),
                UnexpectedFailures.ToString(CultureInfo.InvariantCulture),
                Cancellations.ToString(CultureInfo.InvariantCulture),
                ServerSessions.ToString(CultureInfo.InvariantCulture),
                SecondsSinceProgress.ToString("F1", CultureInfo.InvariantCulture)
            };

            return string.Join(",", fields);
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
    internal sealed class WorkloadCounters
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
    internal sealed class ResourceSampler
    {
        private readonly RunArtifacts _artifacts;
        private readonly WorkloadCounters _counters;
        private readonly Func<long> _connectionsCreated;
        private readonly List<ResourceSample> _taken = new List<ResourceSample>();

        public ResourceSampler(RunArtifacts artifacts, WorkloadCounters counters, Func<long> connectionsCreated)
        {
            _artifacts = artifacts;
            _counters = counters;
            _connectionsCreated = connectionsCreated;
        }

        public IReadOnlyList<ResourceSample> Taken => _taken;

        public ResourceSample Take(string phase, string marker, int serverSessions = -1)
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
                ConnectionsCreated = _connectionsCreated(),
                Operations = _counters.Operations,
                Successes = _counters.Successes,
                ExpectedFailures = _counters.ExpectedFailures,
                UnexpectedFailures = _counters.UnexpectedFailures,
                Cancellations = _counters.Cancellations,
                ServerSessions = serverSessions,
                SecondsSinceProgress = _counters.SecondsSinceProgress
            };

            _taken.Add(sample);
            _artifacts.Sample(sample);
            return sample;
        }
    }
}

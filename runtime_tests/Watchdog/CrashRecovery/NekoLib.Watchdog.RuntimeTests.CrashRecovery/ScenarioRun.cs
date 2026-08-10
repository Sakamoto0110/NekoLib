#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;
using NekoLib.Watchdog.RuntimeTests.CrashRecovery.Shared;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal sealed class ScenarioPrerequisiteException : Exception
    {
        public ScenarioPrerequisiteException(string message) : base(message) { }
    }

    internal sealed class ReadyRecord
    {
        public int Generation;
        public int Pid;
        public string ControlPipe = string.Empty;
        public string ArgumentsHash = string.Empty;
        public string LogToken = string.Empty;
        public long ReadyTimestamp;
    }

    internal sealed class ScenarioRun
    {
        private readonly ScenarioOptions _options;
        private readonly CancellationToken _ct;
        private readonly OwnedProcesses _processes = new OwnedProcesses();
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
        private DeploymentLayoutEvidence? _layout;
        private WatchdogSamples? _samples;
        private ResourceSampler? _sampler;
        private WorkloadCounters? _counters;
        private FaultSchedule? _schedule;
        private ChildPlan? _childPlan;
        private string _runRoot = string.Empty;
        private string _childArguments = string.Empty;
        private string _watchdogPipe = string.Empty;
        private DateTime _startedUtc = DateTime.UtcNow;
        private bool _interrupted;
        private int _exitCode = ExitCodes.Success;

        public ScenarioRun(ScenarioOptions options, CancellationToken ct)
        {
            _options = options;
            _ct = ct;
        }

        public int Execute()
        {
            using (RunArtifacts artifacts = RunArtifacts.Create(
                _options.ArtifactsRoot,
                _options.CampaignId,
                _options.ScenarioId,
                WatchdogSamples.ColumnNamesForHeader))
            {
                _artifacts = artifacts;
                _runner = new CheckRunner(
                    artifacts.Out,
                    _ct,
                    CheckRetention.ForMode(_options.Mode),
                    artifacts.AppendCheck);
                _counters = new WorkloadCounters();

                artifacts.Out("E3-WDOG  " + _options.CampaignId);
                artifacts.Out("target    " + RuntimeFacts.TargetFrameworkMoniker + "  " +
                              RuntimeFacts.RuntimeDescription);
                artifacts.Out("run       " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                try
                {
                    ExecuteWithArtifacts();
                }
                catch (OperationCanceledException)
                {
                    _interrupted = true;
                    artifacts.Error("interrupted before the selected Watchdog mode finished");
                }
                catch (FileNotFoundException ex)
                {
                    _exitCode = ExitCodes.PrerequisiteMissing;
                    _setupGaps.Add(ex.Message + " " + ex.FileName);
                    artifacts.Error("prerequisite missing: " + ex.Message + " " + ex.FileName);
                }
                catch (InvalidDataException ex)
                {
                    _exitCode = ExitCodes.PrerequisiteMissing;
                    _setupGaps.Add(ex.Message);
                    artifacts.Error("prerequisite invalid: " + ex.Message);
                }
                catch (ScenarioPrerequisiteException ex)
                {
                    _exitCode = ExitCodes.PrerequisiteMissing;
                    _setupGaps.Add(ex.Message);
                    artifacts.Error("prerequisite missing: " + ex.Message);
                }
                catch (InvalidOperationException ex) when (_layout == null)
                {
                    _exitCode = ExitCodes.PrerequisiteMissing;
                    _setupGaps.Add(ex.Message);
                    artifacts.Error("prerequisite missing: " + ex.Message);
                }
                catch (TimeoutException ex)
                {
                    _exitCode = ExitCodes.Timeout;
                    artifacts.Error("bounded wait expired: " + ex.Message);
                }
                catch (Exception ex)
                {
                    _exitCode = ExitCodes.Unexpected;
                    artifacts.Error("E3-WDOG failed outside a check: " + ex);
                }
                finally
                {
                    Cleanup();
                }

                return Finish();
            }
        }

        private void ExecuteWithArtifacts()
        {
            _layout = DeploymentLayoutEvidence.Resolve(_options);
            _watchdogPipe = WatchdogProtocol.PipeName(_layout.ChildPath);
            _runRoot = Path.Combine(_artifacts!.ScenarioDirectory, "work");

            if (Directory.Exists(_runRoot) && Directory.EnumerateFileSystemEntries(_runRoot).Any())
                throw new ScenarioPrerequisiteException("The campaign work directory is not empty: " + _runRoot);
            Directory.CreateDirectory(_runRoot);

            if (_processes.LiveIds(_layout.ChildPath).Length > 0 ||
                _processes.LiveIds(_layout.HostPath).Length > 0)
            {
                throw new ScenarioPrerequisiteException(
                    "An exact E3-WDOG child or deployed Host image is already running; use a fresh layout.");
            }

            if (!_layout.SupportsPackageClaim)
            {
                _setupGaps.Add(
                    "The source layout exercises the deployed path contract but cannot establish package provenance.");
            }

            _schedule = ScheduleFactory.Build(_options);
            _artifacts.WriteText(_artifacts.SchedulePath, _schedule.ToJson());

            _startedUtc = DateTime.UtcNow;
            _childPlan = BuildChildPlan(_schedule, Stopwatch.GetTimestamp());
            string planPath = Path.Combine(_runRoot, "child-plan.tsv");
            _childPlan.SaveDurably(planPath);
            _childArguments = BuildChildArguments(_runRoot, planPath);

            WriteEnvironment();
            _samples = new WatchdogSamples(_runRoot, _layout, _processes);
            _sampler = new ResourceSampler(_artifacts, _counters!, _samples);
            _sampler.Take("preflight", "baseline");

            _artifacts.Out("layout    " + _layout.Kind + "  " + _layout.ApplicationRoot);
            _artifacts.Out("schedule  " + _schedule.Events.Count + " fault(s), " + _schedule.Hash);
            _artifacts.Out("plan      persisted before the first application process");
            _artifacts.Out(string.Empty);

            StartFreshPair("bootstrap");
            RunInitialChecks();
            _sampler.Take("preflight", "post-warm-up");

            foreach (FaultEvent planned in _schedule.Events)
            {
                ReadyRecord? before = FaultKinds.IsChildOwned(planned.Kind) ? LatestReady() : null;
                WaitForOffset(planned.OffsetSeconds);
                _sampler.Take(planned.Kind, "pre-fault");
                Dispatch(planned, before);
                _sampler.Take(planned.Kind, "post-recovery");
            }

            WaitForModeWindow();
            RunFinalChecks();
            _sampler.Take("final", "before-cleanup");
        }

        private ChildPlan BuildChildPlan(FaultSchedule schedule, long origin)
        {
            ChildPlan plan = new ChildPlan
            {
                CampaignId = _options.CampaignId,
                ScheduleHash = schedule.Hash,
                OriginTimestamp = origin,
                TimestampFrequency = Stopwatch.Frequency
            };

            foreach (FaultEvent planned in schedule.Events)
            {
                if (!FaultKinds.IsChildOwned(planned.Kind)) continue;
                plan.Events.Add(new ChildPlanEvent
                {
                    Id = planned.Id,
                    Kind = planned.Kind,
                    OffsetSeconds = planned.OffsetSeconds,
                    Repetitions = planned.Kind == FaultKinds.FastCrashLoop ? FaultKinds.FastCrashCount : 1
                });
            }
            return plan;
        }

        private void StartFreshPair(string reason)
        {
            _ct.ThrowIfCancellationRequested();
            _artifacts!.Out("start     " + reason);
            _processes.StartChild(_layout!.ChildPath, _childArguments, _runRoot).Dispose();

            ReadyRecord ready = WaitForHealthyPair(TimeSpan.FromSeconds(30));
            _artifacts.Out("  pair    child pid " + ready.Pid + " generation " + ready.Generation);
        }

        private ReadyRecord WaitForHealthyPair(TimeSpan timeout)
        {
            ReadyRecord? ready = null;
            WatchdogStatus? status = null;
            int hostPid = 0;

            bool found = WatchdogProtocol.WaitUntil(() =>
            {
                status = WatchdogProtocol.ReadStatus(_watchdogPipe);
                if (status.ChildPid <= 0) return false;

                ready = ReadReady(status.ChildPid);
                if (ready == null) return false;

                string health = WatchdogProtocol.ChildHealth(ready.ControlPipe);
                if (!health.Contains("|" + ready.Generation.ToString(CultureInfo.InvariantCulture) + "|" +
                                     ready.Pid.ToString(CultureInfo.InvariantCulture) + "|ready|"))
                    return false;

                int[] hosts = _processes.LiveIds(_layout!.HostPath);
                int[] children = _processes.LiveIds(_layout.ChildPath);
                if (hosts.Length != 1 || children.Length != 1 || children[0] != ready.Pid) return false;
                hostPid = hosts[0];
                return true;
            }, timeout, _ct);

            if (!found || ready == null || status == null)
                throw new TimeoutException("A healthy single Host/child pair did not appear within " + timeout + ".");

            _processes.Adopt(hostPid, "host", _layout!.HostPath);
            _processes.Adopt(ready.Pid, "child", _layout.ChildPath);
            return ready;
        }

        private void RunInitialChecks()
        {
            ReadyRecord ready = LatestReady();
            RunCheck("bootstrap", "deployed-host-attach",
                "WatchdogBootstrap reaches an exact single deployed Host/child pair with a live health endpoint",
                check =>
                {
                    check.Equal(1, _processes.LiveIds(_layout!.HostPath).Length, "live deployed Hosts");
                    check.Equal(1, _processes.LiveIds(_layout.ChildPath).Length, "live child applications");
                    check.That(WatchdogProtocol.ReadStatus(_watchdogPipe).ChildPid == ready.Pid,
                        "Watchdog status did not name the ready child pid");
                });

            CheckHealthAndForwarding("bootstrap", ready);
        }

        private void Dispatch(FaultEvent planned, ReadyRecord? before)
        {
            _artifacts!.Event("fault-dispatch", json =>
            {
                json.Prop("id", planned.Id);
                json.Prop("faultKind", planned.Kind);
                json.Prop("offsetSeconds", planned.OffsetSeconds);
            });

            _artifacts.Out("fault     " + planned.Id + "  " + planned.Kind);
            if (FaultKinds.IsChildOwned(planned.Kind))
                ObserveChildFault(planned, before ?? throw new InvalidOperationException("Missing pre-fault generation."));
            else if (planned.Kind == FaultKinds.CleanShutdown)
                ExerciseCleanShutdown(planned);
            else
                ExerciseFreshBootstrap(planned);
        }

        private void ObserveChildFault(FaultEvent planned, ReadyRecord before)
        {
            int bundlesBefore = BundleDirectories().Length;
            int repetitions = planned.Kind == FaultKinds.FastCrashLoop ? FaultKinds.FastCrashCount : 1;
            TimeSpan timeout = planned.Kind == FaultKinds.FastCrashLoop
                ? TimeSpan.FromSeconds(75)
                : TimeSpan.FromSeconds(20);

            bool recovered = WatchdogProtocol.WaitUntil(() =>
            {
                ProbeUnacknowledgedGenerations();
                int armed = ArmedPaths(planned.Id).Length;
                ReadyRecord? current = TryLatestReady();
                return armed >= repetitions && current != null && current.Generation > before.Generation &&
                       IsHealthy(current);
            }, timeout, _ct);

            RunCheck(planned.Kind, planned.Id + "-replacement",
                planned.ExpectedRecovery,
                check =>
                {
                    check.That(recovered, "the expected replacement generation did not become healthy in time");
                    check.Equal(repetitions, ArmedPaths(planned.Id).Length, "durable armed records");
                    check.Equal(1, _processes.LiveIds(_layout!.HostPath).Length, "live deployed Hosts");
                    check.Equal(1, _processes.LiveIds(_layout.ChildPath).Length, "live child applications");
                });

            ReadyRecord after = LatestReady();
            if (planned.Kind == FaultKinds.OrdinaryExit)
            {
                RunCheck(planned.Kind, planned.Id + "-exit-code",
                    "an ordinary child exit is observed as code 0",
                    check => check.Equal(0, WatchdogProtocol.ReadStatus(_watchdogPipe).LastExitCode ?? int.MinValue,
                        "Watchdog last exit code"));
            }
            else
            {
                bool bundleAppeared = WatchdogProtocol.WaitUntil(
                    () => BundleDirectories().Length >= Math.Min(10, bundlesBefore + repetitions),
                    TimeSpan.FromSeconds(10), _ct);
                RunCheck(planned.Kind, planned.Id + "-bundle",
                    "each retained crash is finalized from a durable armed pending directory",
                    check => check.That(bundleAppeared, "the expected crash bundle count did not appear"));
            }

            if (planned.Kind == FaultKinds.FastCrashLoop)
                CheckCooling(planned);

            CheckHealthAndForwarding(planned.Kind, after);
            _counters!.Success();
        }

        private void CheckCooling(FaultEvent planned)
        {
            List<Dictionary<string, object?>> armed = ArmedPaths(planned.Id)
                .Select(ReadObject)
                .OrderBy(item => JsonParser.RequireInt(item, "generation"))
                .ToList();

            RunCheck(planned.Kind, planned.Id + "-cooling",
                "the documented ten-second cooling follows each complete group of five fast restarted generations",
                check =>
                {
                    check.Equal(FaultKinds.FastCrashCount, armed.Count, "fast-crash armed records");
                    // The initial loop terminal can belong to an older healthy
                    // generation and reset the runtime's internal fast count.
                    // The next five and the five after that are guaranteed to
                    // be below three seconds, so cooling follows records 6/11.
                    foreach (int index in new[] { 5, 10 })
                    {
                        int generation = (int)JsonParser.RequireInt(armed[index], "generation");
                        long armedTimestamp = JsonParser.RequireInt(armed[index], "armedTimestamp");
                        ReadyRecord next = ReadReadyGeneration(generation + 1);
                        double delay = (next.ReadyTimestamp - armedTimestamp) / (double)_childPlan!.TimestampFrequency;
                        check.That(delay >= 9.0,
                            "cooling after fast crash " + (index + 1) + " was only " +
                            delay.ToString("F2", CultureInfo.InvariantCulture) + "s");
                        check.Note("cooling after fast crash " + (index + 1) + ": " +
                                   delay.ToString("F2", CultureInfo.InvariantCulture) + "s");
                    }
                });
        }

        private void ExerciseFreshBootstrap(FaultEvent planned)
        {
            ReadyRecord before = LatestReady();
            StopCurrentPair();
            StartFreshPair(planned.Kind);
            ReadyRecord after = LatestReady();

            RunCheck(planned.Kind, planned.Id + "-fresh-pair",
                planned.ExpectedRecovery,
                check =>
                {
                    check.That(after.Generation > before.Generation, "a fresh generation was not claimed");
                    check.Equal(before.ArgumentsHash, after.ArgumentsHash, "persisted child arguments hash");
                    check.Equal(1, _processes.LiveIds(_layout!.HostPath).Length, "live deployed Hosts");
                    check.Equal(1, _processes.LiveIds(_layout.ChildPath).Length, "live child applications");
                    check.Equal(0, WatchdogProtocol.ReadStatus(_watchdogPipe).RestartCount,
                        "fresh Host restart count");
                });

            CheckHealthAndForwarding(planned.Kind, after);
            _counters!.Success();
        }

        private void ExerciseCleanShutdown(FaultEvent planned)
        {
            ReadyRecord before = LatestReady();
            WatchdogProtocol.SendText(_watchdogPipe, "pause");
            WatchdogProtocol.SendText(before.ControlPipe, "shutdown");

            bool stopped = WatchdogProtocol.WaitUntil(
                () => _processes.LiveIds(_layout!.ChildPath).Length == 0,
                TimeSpan.FromSeconds(10), _ct);
            _ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(3));

            RunCheck(planned.Kind, planned.Id + "-paused-no-restart",
                planned.ExpectedRecovery,
                check =>
                {
                    check.That(stopped, "the child did not accept bounded graceful shutdown");
                    check.Equal(0, _processes.LiveIds(_layout!.ChildPath).Length,
                        "children after three paused seconds");
                    check.Equal("paused", WatchdogProtocol.ReadStatus(_watchdogPipe).State,
                        "Watchdog state");
                });

            WatchdogProtocol.SendText(_watchdogPipe, "stop");
            WaitForNoPair(TimeSpan.FromSeconds(15));
            StartFreshPair(planned.Kind);
            CheckHealthAndForwarding(planned.Kind, LatestReady());
            _counters!.Success();
        }

        private void StopCurrentPair()
        {
            try { WatchdogProtocol.SendText(_watchdogPipe, "stop"); }
            catch (Exception ex) { _artifacts!.Out("  stop    RPC unavailable: " + ex.GetType().Name); }

            if (!WaitForNoPair(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("The current exact Host/child pair did not stop in time.");
        }

        private bool WaitForNoPair(TimeSpan timeout) => WatchdogProtocol.WaitUntil(
            () => _processes.LiveIds(_layout!.ChildPath).Length == 0 &&
                  _processes.LiveIds(_layout.HostPath).Length == 0,
            timeout,
            _ct);

        private void CheckHealthAndForwarding(string phase, ReadyRecord ready)
        {
            RunCheck(phase, "generation-" + ready.Generation + "-health-forwarding",
                "the recovered application answers health and its unique log token reaches the Host file",
                check =>
                {
                    check.That(ProbeGeneration(ready),
                        "the child health response or unique forwarded log token was unavailable");
                });
        }

        private void RunFinalChecks()
        {
            RunCheck("final", "single-live-pair",
                "the campaign ends with exactly one healthy Host/child pair",
                check =>
                {
                    check.Equal(1, _processes.LiveIds(_layout!.HostPath).Length, "live deployed Hosts");
                    check.Equal(1, _processes.LiveIds(_layout.ChildPath).Length, "live child applications");
                    check.That(IsHealthy(LatestReady()), "the final generation was not healthy");
                });

            RunCheck("final", "schedule-generation-accounting",
                "every planned terminal has exactly one armed identity and every generation has one controller health acknowledgement and one forwarded token",
                ValidateScheduleAccounting);

            RunCheck("final", "crash-bundle-integrity-retention",
                "retained bundles are unique and complete, retention is bounded at ten, and pending input is empty",
                ValidateBundles);
        }

        private void ValidateScheduleAccounting(Check check)
        {
            int childOwnedTerminals = 0;
            int expectedArmed = 0;
            int controllerRestarts = 0;
            foreach (FaultEvent planned in _schedule!.Events)
            {
                if (planned.Kind == FaultKinds.FastCrashLoop)
                {
                    childOwnedTerminals += FaultKinds.FastCrashCount;
                    expectedArmed += FaultKinds.FastCrashCount;
                }
                else if (planned.Kind == FaultKinds.OrdinaryExit || planned.Kind == FaultKinds.UnhandledCrash)
                {
                    childOwnedTerminals++;
                    expectedArmed++;
                }
                else
                {
                    controllerRestarts++;
                }
            }

            int expectedGenerations = 1 + childOwnedTerminals + controllerRestarts;
            string readyRoot = Path.Combine(_runRoot, "state", "ready");
            string probeRoot = Path.Combine(_runRoot, "state", "probes");
            string[] readyPaths = Directory.Exists(readyRoot)
                ? Directory.GetFiles(readyRoot, "generation-*.json")
                : new string[0];

            check.Equal(expectedGenerations,
                CountFiles(Path.Combine(_runRoot, "state", "generations"), "generation-*.json"),
                "durably claimed generations");
            check.Equal(expectedGenerations, readyPaths.Length, "ready generation records");
            check.Equal(expectedGenerations,
                Directory.Exists(probeRoot) ? Directory.GetFiles(probeRoot, "generation-*.ack").Length : 0,
                "controller health acknowledgements");
            check.Equal(expectedArmed, ArmedPaths(null).Length, "armed terminal records");

            string log = File.Exists(Path.Combine(_runRoot, "watchdog.log"))
                ? ReadSharedText(Path.Combine(_runRoot, "watchdog.log"))
                : string.Empty;
            foreach (string path in readyPaths)
            {
                ReadyRecord ready = ParseReady(path);
                check.Equal(1, CountOccurrences(log, ready.LogToken),
                    "forwarded occurrences for generation " + ready.Generation);
            }
        }

        private void ValidateBundles(Check check)
        {
            string[] bundles = BundleDirectories();
            List<Dictionary<string, object?>> crashRecords = ArmedPaths(null)
                .Select(ReadObject)
                .Where(item =>
                {
                    string kind = JsonParser.RequireString(item, "kind");
                    return kind == FaultKinds.UnhandledCrash || kind == FaultKinds.FastCrashLoop;
                })
                .OrderBy(item => JsonParser.RequireInt(item, "generation"))
                .ToList();
            int crashArmed = crashRecords.Count;

            check.Equal(Math.Min(crashArmed, 10), bundles.Length, "retained crash bundles");
            check.That(bundles.Length <= 10, "bundle retention exceeded the documented maximum of ten");

            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string bundle in bundles)
            {
                foreach (string name in new[]
                {
                    "application.json", "event.json", "watchdog-status.txt", "watchdog.log.tail", "manifest.json"
                })
                {
                    check.That(File.Exists(Path.Combine(bundle, name)),
                        Path.GetFileName(bundle) + " is missing " + name);
                }

                Dictionary<string, object?> eventData = ReadObject(Path.Combine(bundle, "event.json"));
                string identity = JsonParser.RequireString(eventData, "eventId") + "/" +
                                  JsonParser.RequireString(eventData, "generation");
                check.That(identities.Add(identity), "duplicate retained bundle identity " + identity);

                Dictionary<string, object?> application = ReadObject(Path.Combine(bundle, "application.json"));
                check.That(!string.IsNullOrWhiteSpace(JsonParser.RequireString(application, "version")),
                    Path.GetFileName(bundle) + " did not record the application version");
                check.Equal("armed", JsonParser.RequireString(application, "status"),
                    Path.GetFileName(bundle) + " application status");

                Dictionary<string, object?> watchdogStatus = JsonParser.AsObject(
                    JsonParser.Parse(File.ReadAllText(Path.Combine(bundle, "watchdog-status.txt"))),
                    Path.GetFileName(bundle) + " Watchdog status");
                check.That(JsonParser.RequireInt(watchdogStatus, "restartCount") >= 0,
                    Path.GetFileName(bundle) + " did not record a valid Watchdog restart count");

                Dictionary<string, object?> manifest = ReadObject(Path.Combine(bundle, "manifest.json"));
                check.That(manifest.TryGetValue("checksums", out object? enabled) && enabled is bool value && value,
                    Path.GetFileName(bundle) + " did not enable manifest checksums");

                Dictionary<string, object?> watchdog = JsonParser.AsObject(
                    manifest.TryGetValue("watchdog", out object? watchdogValue) ? watchdogValue : null,
                    Path.GetFileName(bundle) + " manifest Watchdog section");
                check.That(!string.IsNullOrWhiteSpace(JsonParser.RequireString(watchdog, "version")),
                    Path.GetFileName(bundle) + " did not record the Watchdog version");
                check.That(JsonParser.RequireInt(watchdog, "restartCount") >= 0,
                    Path.GetFileName(bundle) + " manifest restart count was negative");

                List<object?> files = JsonParser.AsArray(
                    manifest.TryGetValue("files", out object? filesValue) ? filesValue : null,
                    Path.GetFileName(bundle) + " manifest files");
                foreach (object? item in files)
                {
                    Dictionary<string, object?> file = JsonParser.AsObject(item, "manifest file entry");
                    string relative = JsonParser.RequireString(file, "path");
                    string expectedHash = JsonParser.RequireString(file, "sha256");
                    string payload = Path.Combine(bundle, relative);
                    check.That(File.Exists(payload), Path.GetFileName(bundle) + " manifest names missing " + relative);
                    check.Equal(expectedHash, Sha256(payload),
                        Path.GetFileName(bundle) + " checksum for " + relative);
                }
            }

            HashSet<string> expectedRetained = new HashSet<string>(
                crashRecords.Skip(Math.Max(0, crashRecords.Count - 10)).Select(item =>
                    JsonParser.RequireString(item, "eventId") + "/" +
                    JsonParser.RequireString(item, "generation")),
                StringComparer.Ordinal);
            check.That(expectedRetained.SetEquals(identities),
                "retained bundle identities were not the newest ten armed crash identities");

            string pending = Path.Combine(_runRoot, "crash", "pending");
            check.Equal(0, Directory.Exists(pending) ? Directory.GetDirectories(pending, "crash-*").Length : 0,
                "pending crash directories");
        }

        private void WaitForOffset(double offsetSeconds)
        {
            while (_childPlan!.ElapsedSeconds(Stopwatch.GetTimestamp()) < offsetSeconds)
            {
                _ct.ThrowIfCancellationRequested();
                _ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
            }
        }

        private void WaitForModeWindow()
        {
            double target = _schedule!.RequestedDurationSeconds;

            while (_childPlan!.ElapsedSeconds(Stopwatch.GetTimestamp()) < target)
            {
                _ct.ThrowIfCancellationRequested();
                double remaining = target - _childPlan.ElapsedSeconds(Stopwatch.GetTimestamp());
                _ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Min(10.0, remaining)));
                _sampler!.Take("steady", "periodic");
            }
        }

        private void Cleanup()
        {
            if (_artifacts == null) return;
            _artifacts.Out(string.Empty);
            _artifacts.Out("cleanup");

            try
            {
                if (_layout != null)
                {
                    try { WatchdogProtocol.SendText(_watchdogPipe, "stop"); }
                    catch { }

                    DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                    while (DateTime.UtcNow < deadline &&
                           (_processes.LiveIds(_layout.ChildPath).Length > 0 ||
                            _processes.LiveIds(_layout.HostPath).Length > 0))
                    {
                        Thread.Sleep(100);
                    }

                    foreach (OwnedProcess owned in _processes.All)
                    {
                        if (!_processes.IsLive(owned)) continue;
                        if (!_processes.KillExact(owned, out string diagnostic))
                            _cleanupProblems.Add(owned.Role + " pid " + owned.Id + " was not reconciled: " + diagnostic);
                        else
                            _artifacts.Out("  " + owned.Role + " pid " + owned.Id + " " + diagnostic);
                    }

                    if (_processes.LiveIds(_layout.ChildPath).Length > 0)
                        _cleanupProblems.Add("an exact scenario child remained live after cleanup");
                    if (_processes.LiveIds(_layout.HostPath).Length > 0)
                        _cleanupProblems.Add("an exact deployed Host remained live after cleanup");

                    try
                    {
                        string response = WatchdogProtocol.SendText(_watchdogPipe, "ping");
                        _cleanupProblems.Add(
                            "the Watchdog pipe still answered after cleanup with '" + response + "'");
                    }
                    catch { _artifacts.Out("  pipe    Watchdog endpoint released"); }

                    ReadyRecord? last = TryLatestReady();
                    if (last != null)
                    {
                        try
                        {
                            string response = WatchdogProtocol.ChildHealth(last.ControlPipe);
                            _cleanupProblems.Add(
                                "the final child health pipe still answered after cleanup with '" + response + "'");
                        }
                        catch { _artifacts.Out("  pipe    child health endpoint released"); }
                    }

                    string pending = Path.Combine(_runRoot, "crash", "pending");
                    if (Directory.Exists(pending) && Directory.GetDirectories(pending, "crash-*").Length > 0)
                        _cleanupProblems.Add("one or more pending crash directories remained after cleanup");

                    foreach (string bundle in BundleDirectories())
                    {
                        if (!File.Exists(Path.Combine(bundle, "manifest.json")))
                            _cleanupProblems.Add(Path.GetFileName(bundle) + " remained without manifest.json");
                    }
                }
            }
            catch (Exception ex)
            {
                _cleanupProblems.Add("cleanup inspection threw " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                if (_sampler != null) _sampler.Take("cleanup", "final");
            }
            catch { }

            _processes.Dispose();
        }

        private int Finish()
        {
            if (_artifacts == null || _runner == null || _counters == null)
                return _exitCode == ExitCodes.Success ? ExitCodes.Unexpected : _exitCode;

            DateTime finished = DateTime.UtcNow;
            RunSummary summary = new RunSummary
            {
                CampaignId = _options.CampaignId,
                ScenarioId = _options.ScenarioId,
                Mode = _options.Mode.ToString(),
                Seed = _options.Seed,
                ScheduleHash = _schedule?.Hash ?? string.Empty,
                ScheduleFaultCount = _schedule?.Events.Count ?? 0,
                StartedUtc = _startedUtc,
                FinishedUtc = finished,
                Interrupted = _interrupted,
                BelowSpecifiedWindow = BelowSpecifiedWindow(finished - _startedUtc),
                ExplicitExitCode = _exitCode
            };
            summary.SetupGaps.AddRange(_setupGaps);
            summary.CleanupProblems.AddRange(_cleanupProblems);

            DeploymentLayoutEvidence layout = _layout ?? new DeploymentLayoutEvidence { Kind = "unresolved" };
            return summary.Write(
                _artifacts,
                _runner,
                _counters,
                new WatchdogScenarioSummary(
                    layout,
                    ClaimBoundaries,
                    StateFileCount("generations", "generation-*.json"),
                    StateFileCount("armed", "*.json"),
                    BundleDirectories().Length));
        }

        private bool BelowSpecifiedWindow(TimeSpan elapsed)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke: return elapsed < ScenarioOptions.MinimumSpecifiedSmoke;
                case ScenarioMode.RecoveryRehearsal: return elapsed < ScenarioOptionsBase.MinimumSpecifiedRehearsal;
                default: return elapsed < ScenarioOptions.MinimumSpecifiedSoak;
            }
        }

        private void WriteEnvironment()
        {
            RuntimeFacts.ReadRepository(out string commit, out bool dirty, out string diagnostic);
            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("campaignId", _options.CampaignId);
                json.Prop("scenarioId", _options.ScenarioId);
                json.Prop("startedUtc", _startedUtc.ToString("o", CultureInfo.InvariantCulture));
                json.Object("repository", () =>
                {
                    json.Prop("commit", commit);
                    json.Prop("dirty", dirty);
                    if (diagnostic.Length > 0) json.Prop("diagnostic", diagnostic);
                });
                json.Object("host", () =>
                {
                    json.Prop("windowsVersion", RuntimeFacts.WindowsVersion);
                    json.Prop("machineArchitecture", RuntimeFacts.MachineArchitecture);
                    json.Prop("processArchitecture", RuntimeFacts.ProcessArchitecture);
                    json.Prop("logicalProcessors", RuntimeFacts.LogicalProcessorCount);
                    json.Prop("installedMemory", RuntimeFacts.DescribeBytes(RuntimeFacts.InstalledMemoryBytes));
                });
                json.Object("deployment", () =>
                {
                    json.Prop("layout", _layout!.Kind);
                    json.Prop("applicationRoot", _layout.ApplicationRoot);
                    json.Prop("childPath", _layout.ChildPath);
                    json.Prop("childVersion", _layout.ChildVersion);
                    json.Prop("childMachine", _layout.ChildMachine);
                    json.Prop("hostPath", _layout.HostPath);
                    json.Prop("hostVersion", _layout.HostVersion);
                    json.Prop("hostMachine", _layout.HostMachine);
                    json.Prop("watchdogPipe", _watchdogPipe);
                    json.Prop("supportsPackageClaim", _layout.SupportsPackageClaim);
                    if (_layout.SupportsPackageClaim)
                    {
                        json.Prop("packageFile", _layout.PackageFile);
                        json.Prop("packageVersion", _layout.PackageVersion);
                        json.Prop("packageSha256", _layout.PackageSha256);
                        json.Prop("packagePayloadEntry", _layout.PackagePayloadEntry);
                    }
                });
                json.Array("setupGaps", () =>
                {
                    foreach (string gap in _setupGaps) json.Item(gap);
                });
                json.Array("claimBoundaries", () =>
                {
                    foreach (string boundary in ClaimBoundaries) json.Item(boundary);
                });
            });
            _artifacts!.WriteText(_artifacts.EnvironmentPath, json.ToString());
        }

        private void RunCheck(string phase, string name, string claim, Action<Check> body)
        {
            bool passed = _runner!.RunAsync(phase, name, claim, check =>
            {
                body(check);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            if (!passed) _counters!.UnexpectedFailure();
        }

        private ReadyRecord LatestReady() => TryLatestReady() ??
            throw new InvalidOperationException("No E3-WDOG child generation has a ready record.");

        private ReadyRecord? TryLatestReady()
        {
            string root = Path.Combine(_runRoot, "state", "ready");
            if (!Directory.Exists(root)) return null;
            string? path = Directory.GetFiles(root, "generation-*.json")
                .OrderByDescending(item => item, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return path == null ? null : ParseReady(path);
        }

        private ReadyRecord? ReadReady(int pid)
        {
            string root = Path.Combine(_runRoot, "state", "ready");
            if (!Directory.Exists(root)) return null;
            foreach (string path in Directory.GetFiles(root, "generation-*.json"))
            {
                ReadyRecord ready = ParseReady(path);
                if (ready.Pid == pid) return ready;
            }
            return null;
        }

        private ReadyRecord ReadReadyGeneration(int generation)
        {
            string path = Path.Combine(
                _runRoot, "state", "ready",
                "generation-" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".json");
            if (!File.Exists(path))
                throw new CheckFailure("missing ready record for generation " + generation);
            return ParseReady(path);
        }

        private static ReadyRecord ParseReady(string path)
        {
            Dictionary<string, object?> data = ReadObject(path);
            return new ReadyRecord
            {
                Generation = (int)long.Parse(JsonParser.RequireString(data, "generation"), CultureInfo.InvariantCulture),
                Pid = (int)long.Parse(JsonParser.RequireString(data, "pid"), CultureInfo.InvariantCulture),
                ControlPipe = JsonParser.RequireString(data, "controlPipe"),
                ArgumentsHash = JsonParser.RequireString(data, "argumentsHash"),
                LogToken = JsonParser.RequireString(data, "logToken"),
                ReadyTimestamp = long.Parse(JsonParser.RequireString(data, "readyTimestamp"), CultureInfo.InvariantCulture)
            };
        }

        private bool IsHealthy(ReadyRecord ready)
        {
            try
            {
                int[] hosts = _processes.LiveIds(_layout!.HostPath);
                int[] children = _processes.LiveIds(_layout.ChildPath);
                if (hosts.Length != 1 || children.Length != 1 || children[0] != ready.Pid)
                    return false;

                _processes.Adopt(hosts[0], "host", _layout.HostPath);
                _processes.Adopt(ready.Pid, "child", _layout.ChildPath);

                string health = WatchdogProtocol.ChildHealth(ready.ControlPipe);
                return health.Contains("|" + ready.Pid.ToString(CultureInfo.InvariantCulture) + "|ready|");
            }
            catch { return false; }
        }

        private void ProbeUnacknowledgedGenerations()
        {
            string root = Path.Combine(_runRoot, "state", "ready");
            if (!Directory.Exists(root)) return;

            foreach (string path in Directory.GetFiles(root, "generation-*.json")
                         .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                ReadyRecord ready;
                try { ready = ParseReady(path); }
                catch { continue; }
                if (File.Exists(ProbePath(ready.Generation))) continue;
                ProbeGeneration(ready);
            }
        }

        private bool ProbeGeneration(ReadyRecord ready)
        {
            if (File.Exists(ProbePath(ready.Generation))) return true;

            try
            {
                string health = WatchdogProtocol.ChildHealth(ready.ControlPipe);
                if (!health.Contains("|ready|" + ready.ArgumentsHash)) return false;

                string logPath = Path.Combine(_runRoot, "watchdog.log");
                if (!File.Exists(logPath) || CountOccurrences(ReadSharedText(logPath), ready.LogToken) != 1)
                    return false;

                WriteDurably(
                    ProbePath(ready.Generation),
                    "campaign=" + _options.CampaignId + Environment.NewLine +
                    "generation=" + ready.Generation.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                    "pid=" + ready.Pid.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                    "acknowledgedUtc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine);
                return true;
            }
            catch { return false; }
        }

        private string ProbePath(int generation) => Path.Combine(
            _runRoot,
            "state",
            "probes",
            "generation-" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".ack");

        private string[] ArmedPaths(string? eventId)
        {
            string root = Path.Combine(_runRoot, "state", "armed");
            if (!Directory.Exists(root)) return new string[0];
            string pattern = eventId == null ? "*.json" : Safe(eventId) + "-g*.json";
            return Directory.GetFiles(root, pattern);
        }

        private string[] BundleDirectories()
        {
            if (string.IsNullOrWhiteSpace(_runRoot)) return new string[0];
            string root = Path.Combine(_runRoot, "crash", "bundles");
            return Directory.Exists(root) ? Directory.GetDirectories(root, "bundle-*") : new string[0];
        }

        private static Dictionary<string, object?> ReadObject(string path) =>
            JsonParser.AsObject(JsonParser.Parse(File.ReadAllText(path)), path);

        private static int CountFiles(string root, string pattern) =>
            Directory.Exists(root) ? Directory.GetFiles(root, pattern).Length : 0;

        private int StateFileCount(string stateDirectory, string pattern) =>
            string.IsNullOrWhiteSpace(_runRoot)
                ? 0
                : CountFiles(Path.Combine(_runRoot, "state", stateDirectory), pattern);

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static string ReadSharedText(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                return reader.ReadToEnd();
        }

        private static void WriteDurably(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                StringBuilder text = new StringBuilder(64);
                foreach (byte value in hash.ComputeHash(stream))
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static string BuildChildArguments(string runRoot, string planPath) =>
            "--scenario-child --run-root " + Quote(runRoot) + " --plan " + Quote(planPath);

        private static string Quote(string value)
        {
            if (value.Length > 0 && value.All(character => !char.IsWhiteSpace(character) && character != '"'))
                return value;

            StringBuilder result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { backslashes++; continue; }
                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1).Append('"');
                    backslashes = 0;
                    continue;
                }
                result.Append('\\', backslashes).Append(character);
                backslashes = 0;
            }
            result.Append('\\', backslashes * 2).Append('"');
            return result.ToString();
        }

        private static string Safe(string value)
        {
            StringBuilder safe = new StringBuilder(value.Length);
            foreach (char character in value)
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_') safe.Append(character);
            return safe.ToString();
        }

        private static readonly string[] ClaimBoundaries =
        {
            "A source-layout run exercises the deployed Host path and bootstrap contract but is not package evidence. " +
            "Only a disposable-package or published-consumer layout with exact package version and SHA-256 supports that claim.",
            "The scenario uses only public Watchdog and Pipes behavior. Its health/shutdown pipe and durable crash plan " +
            "are scenario-owned workload controls, not product TestControl or fault-injection APIs.",
            "All processes run on one Windows machine as one user. This does not establish cross-user ACL, elevation, " +
            "remote-host, physical-failure or power-loss behavior.",
            "The child writes application.json and event.json before a planned unhandled terminal. The Watchdog Host " +
            "owns restart, final bundle creation, manifest/checksum generation and retention.",
            "The required soak claim is four hours. A shorter soak remains useful for development but is marked below " +
            "the specified window and cannot be cited as soak evidence."
        };
    }
}

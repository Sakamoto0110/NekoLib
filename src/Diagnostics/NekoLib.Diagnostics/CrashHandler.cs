using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;


namespace NekoLib.Diagnostics
{
    /// <summary>
    /// Writes a crash dump to <paramref name="filePath"/> at the requested
    /// <paramref name="level"/>. Returns true on success. This is the extension
    /// point filled by the OS-specific package (e.g. NekoLib.Diagnostics.Windows
    /// wires the dbghelp.dll minidump writer here) so the cross-platform crash
    /// orchestration never depends on a Windows-only assembly.
    /// </summary>
    /// <param name="filePath">Reserved artifact path the writer must create on success.</param>
    /// <param name="level">Captured dump level requested by the application.</param>
    /// <returns><c>true</c> only when a complete artifact was written at <paramref name="filePath"/>.</returns>
    public delegate bool CrashDumpWriter(string filePath, CrashDumpLevel level);

    /// <summary>
    /// Composition-root configuration for <see cref="CrashHandler"/>. Every value
    /// is captured when the handler is constructed; mutating this object
    /// afterwards does not affect that handler.
    /// </summary>
    public sealed class CrashHandlerOptions
    {
        /// <summary>
        /// Gets or sets the crash-bundle root. It is required when
        /// <see cref="WriteCrashFolder"/> is <c>true</c> and is captured without creating it.
        /// </summary>
        public string? CrashRootDirectory { get; set; }
        /// <summary>Gets or sets the requested dump level. The default is <see cref="CrashDumpLevel.MiniDumpNormal"/>.</summary>
        public CrashDumpLevel DumpLevel { get; set; } = CrashDumpLevel.MiniDumpNormal;

        /// <summary>Gets or sets caller-owned file paths whose newest lines are copied into each bundle.</summary>
        public List<string> TailFiles { get; set; } = new List<string>();
        /// <summary>Gets or sets the maximum lines copied from each tail file. The default is 400; a non-positive value writes no tail content.</summary>
        public int TailLines { get; set; } = 400;

        /// <summary>
        /// Gets or sets whether crash folders are written. The default is
        /// <c>true</c>; when false, no bundle success or failure event is raised.
        /// </summary>
        public bool WriteCrashFolder { get; set; } = true;

        /// <summary>
        /// Optional caller-owned evidence. The application owns its size; unlike the
        /// snapshot sources below, this contributor is not bounded by Diagnostics.
        /// </summary>
        public Func<IEnumerable<string>>? ExtraLines { get; set; }

        /// <summary>
        /// Optional OS-specific dump writer. When null (no platform package wired),
        /// no dump is produced and the crash bundle still contains crash.txt + tails.
        /// </summary>
        public CrashDumpWriter? DumpWriter { get; set; }

        /// <summary>Optional fatal-event writer supplied by the composition root.</summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Optional flush capability. When omitted, Diagnostics uses
        /// <see cref="Logger"/> if it also implements <see cref="ILogFlusher"/>.
        /// </summary>
        public ILogFlusher? LogFlusher { get; set; }

        /// <summary>
        /// Optional recent-log reader. When omitted, Diagnostics uses
        /// <see cref="Logger"/> if it also implements <see cref="ILogSnapshotSource"/>.
        /// </summary>
        public ILogSnapshotSource? LogSnapshotSource { get; set; }

        /// <summary>Gets or sets the optional caller-owned source of recent completed telemetry operations.</summary>
        public ITelemetrySnapshotSource? TelemetrySnapshotSource { get; set; }
        /// <summary>Gets or sets the optional caller-owned read-only inspection snapshot source.</summary>
        public IInspectionSnapshotSource? InspectionSnapshotSource { get; set; }

        /// <summary>
        /// Cooperative budget handed to each optional crash-evidence contributor.
        /// A contributor that ignores it is abandoned shortly afterwards; see the
        /// module reference for the exact settle margin.
        /// </summary>
        public TimeSpan EvidenceCollectionTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>Gets or sets the locally enforced recent-log entry limit. The default is 200 and the value cannot be negative.</summary>
        public int MaxRecentLogEntries { get; set; } = 200;
        /// <summary>Gets or sets the locally enforced completed-telemetry limit. The default is 100 and the value cannot be negative.</summary>
        public int MaxRecentTelemetryOperations { get; set; } = 100;
        /// <summary>Gets or sets the locally enforced retained-inspection-operation limit. The default is 100 and the value cannot be negative.</summary>
        public int MaxInspectionOperations { get; set; } = 100;
        /// <summary>Gets or sets the maximum persisted evidence-line length. The default is 4096 and the minimum is 64.</summary>
        public int MaxEvidenceLineLength { get; set; } = 4096;

        /// <summary>
        /// Optional line-oriented redactor applied to dynamic crash evidence and
        /// configured file tails before it is persisted. It governs persisted
        /// artifacts only; subscribers and <see cref="ExternalNotifier"/> still
        /// receive the raw exception.
        /// </summary>
        public Func<string, string>? Redact { get; set; }

        /// <summary>
        /// Optional notification callback supplied by the application composition
        /// root. Diagnostics invokes it after crash artifacts are written. Leave it
        /// null when no external notification is required.
        /// </summary>
        public Action<CrashDetectedEventArgs>? ExternalNotifier { get; set; }
    }

    /// <summary>Describes one crash report accepted by a handler before evidence collection begins.</summary>
    public sealed class CrashDetectedEventArgs : EventArgs
    {
        /// <summary>Gets the process or adapter source that reported the crash.</summary>
        public string Source { get; }
        /// <summary>Gets the raw exception, or <c>null</c> when the reporting boundary supplied no exception object.</summary>
        public Exception? Exception { get; }
        /// <summary>Gets whether the reporting source identified the process event as terminating.</summary>
        public bool IsTerminating { get; }

        /// <summary>Creates crash-detected event data.</summary>
        /// <param name="source">Reporting source identifier.</param>
        /// <param name="ex">Raw reported exception, if available.</param>
        /// <param name="terminating">Whether the source identified the report as terminating.</param>
        public CrashDetectedEventArgs(string source, Exception? ex, bool terminating)
        {
            Source = source;
            Exception = ex;
            IsTerminating = terminating;
        }
    }

    /// <summary>Describes a crash bundle whose mandatory text artifact was written.</summary>
    public sealed class CrashBundleWrittenEventArgs : EventArgs
    {
        /// <summary>Gets the created bundle directory.</summary>
        public string BundleDirectory { get; }
        /// <summary>Gets the path of the written <c>crash.txt</c> artifact.</summary>
        public string CrashTextPath { get; }

        /// <summary>
        /// Reserved dump path inside the bundle. The file exists only when
        /// <see cref="DumpWritten"/> is true.
        /// </summary>
        public string DumpPath { get; }
        /// <summary>Gets whether the configured dump writer reported a completed dump artifact.</summary>
        public bool DumpWritten { get; }

        /// <summary>Creates crash-bundle success event data.</summary>
        /// <param name="dir">Created bundle directory.</param>
        /// <param name="crashTxt">Written crash-text path.</param>
        /// <param name="dump">Reserved dump path.</param>
        /// <param name="dumpWritten">Whether a complete dump was reported at the reserved path.</param>
        public CrashBundleWrittenEventArgs(string dir, string crashTxt, string dump, bool dumpWritten)
        {
            BundleDirectory = dir;
            CrashTextPath = crashTxt;
            DumpPath = dump;
            DumpWritten = dumpWritten;
        }
    }

    /// <summary>
    /// Raised when crash-bundle creation failed, so an unattended application can
    /// observe that incident evidence was lost instead of assuming it was written.
    /// </summary>
    public sealed class CrashBundleFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Directory the handler was writing to, or the configured crash root when
        /// the bundle directory itself could not be created.
        /// </summary>
        public string BundleDirectory { get; }

        /// <summary>Failure type and message, for logging or notification.</summary>
        public string Reason { get; }

        /// <summary>Creates crash-bundle failure event data.</summary>
        /// <param name="bundleDirectory">Attempted bundle directory or configured root.</param>
        /// <param name="reason">Failure type and message.</param>
        public CrashBundleFailedEventArgs(string bundleDirectory, string reason)
        {
            BundleDirectory = bundleDirectory;
            Reason = reason;
        }
    }

    /// <summary>
    /// Installs caller-owned crash reporting into process-wide exception sources
    /// and writes bounded incident evidence. Installation is idempotent; disposal
    /// is terminal and removes global hooks after the last installed handler leaves.
    /// </summary>
    public sealed class CrashHandler : IDisposable
    {
        /// <summary>
        /// Extra wall-clock time a contributor is given, beyond its own cooperative
        /// budget, purely so it can return an answer it has already computed.
        /// </summary>
        private static readonly TimeSpan ContributorSettleMargin = TimeSpan.FromMilliseconds(50);

        private static readonly object RegistryLock = new object();
        private static readonly List<CrashHandler> InstalledHandlers = new List<CrashHandler>();
        private static bool _globalHandlersInstalled;

        private readonly Stopwatch _uptime = Stopwatch.StartNew();

        // Captured once, so constructor validation actually holds and a caller
        // mutating its own options object cannot re-target a live handler.
        private readonly string? _crashRootDirectory;
        private readonly CrashDumpLevel _dumpLevel;
        private readonly string[] _tailFiles;
        private readonly int _tailLines;
        private readonly bool _writeCrashFolder;
        private readonly Func<IEnumerable<string>>? _extraLines;
        private readonly CrashDumpWriter? _dumpWriter;
        private readonly ILogger? _logger;
        private readonly ILogFlusher? _logFlusher;
        private readonly ILogSnapshotSource? _logSnapshotSource;
        private readonly ITelemetrySnapshotSource? _telemetrySnapshotSource;
        private readonly IInspectionSnapshotSource? _inspectionSnapshotSource;
        private readonly TimeSpan _evidenceCollectionTimeout;
        private readonly TimeSpan _contributorAbandonAfter;
        private readonly int _maxRecentLogEntries;
        private readonly int _maxRecentTelemetryOperations;
        private readonly int _maxInspectionOperations;
        private readonly int _maxEvidenceLineLength;
        private readonly Func<string, string>? _redact;
        private readonly Action<CrashDetectedEventArgs>? _externalNotifier;

        private int _installed;
        private int _disposed;
        private int _crashing;
        private int _redactorUnavailable;

        /// <summary>
        /// Occurs inline after a report is accepted and before evidence collection.
        /// Subscriber failures are isolated, but subscribers have no timeout.
        /// </summary>
        public event EventHandler<CrashDetectedEventArgs>? CrashDetected;
        /// <summary>
        /// Occurs inline after a crash folder is written. Exactly one bundle
        /// success or failure event occurs when folder writing is enabled.
        /// </summary>
        public event EventHandler<CrashBundleWrittenEventArgs>? CrashBundleWritten;
        /// <summary>
        /// Occurs inline when crash-folder creation or mandatory text persistence
        /// fails. Subscriber failures are isolated.
        /// </summary>
        public event EventHandler<CrashBundleFailedEventArgs>? CrashBundleFailed;

        /// <summary>
        /// Creates a handler from a defensive snapshot of the supplied options.
        /// Contributor, sink, and callback objects remain caller-owned references.
        /// </summary>
        /// <param name="options">Required composition-root configuration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Folder writing is enabled without a crash root.</exception>
        /// <exception cref="ArgumentOutOfRangeException">An evidence budget or bound is outside its supported range.</exception>
        public CrashHandler(CrashHandlerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (options.WriteCrashFolder && string.IsNullOrWhiteSpace(options.CrashRootDirectory))
                throw new ArgumentException("CrashRootDirectory is required.", nameof(options));
            if (options.EvidenceCollectionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), "EvidenceCollectionTimeout must be positive.");
            if (options.MaxRecentLogEntries < 0 ||
                options.MaxRecentTelemetryOperations < 0 ||
                options.MaxInspectionOperations < 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Evidence limits cannot be negative.");
            if (options.MaxEvidenceLineLength < 64)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxEvidenceLineLength must be at least 64.");

            _crashRootDirectory = options.CrashRootDirectory;
            _dumpLevel = options.DumpLevel;
            _tailFiles = options.TailFiles == null
                ? new string[0]
                : options.TailFiles.ToArray();
            _tailLines = options.TailLines;
            _writeCrashFolder = options.WriteCrashFolder;
            _extraLines = options.ExtraLines;
            _dumpWriter = options.DumpWriter;
            _logger = options.Logger;
            _logFlusher = options.LogFlusher ?? options.Logger as ILogFlusher;
            _logSnapshotSource = options.LogSnapshotSource ?? options.Logger as ILogSnapshotSource;
            _telemetrySnapshotSource = options.TelemetrySnapshotSource;
            _inspectionSnapshotSource = options.InspectionSnapshotSource;
            _evidenceCollectionTimeout = options.EvidenceCollectionTimeout;
            _contributorAbandonAfter = options.EvidenceCollectionTimeout + ContributorSettleMargin;
            _maxRecentLogEntries = options.MaxRecentLogEntries;
            _maxRecentTelemetryOperations = options.MaxRecentTelemetryOperations;
            _maxInspectionOperations = options.MaxInspectionOperations;
            _maxEvidenceLineLength = options.MaxEvidenceLineLength;
            _redact = options.Redact;
            _externalNotifier = options.ExternalNotifier;
        }

        // ============================================================
        // INSTALL
        // ============================================================

        /// <summary>
        /// Registers this handler for process-wide unhandled and unobserved-task
        /// exception sources. Repeated calls are inert.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The handler has been disposed.</exception>
        public void Install()
        {
            lock (RegistryLock)
            {
                // Disposal and registration must make one atomic lifecycle decision.
                // Checking before this lock allowed Dispose() to remove the handler
                // while Install() was waiting, after which Install() could add the
                // already-disposed instance to the process-wide registry.
                ThrowIfDisposed();

                if (_installed != 0)
                    return;

                _installed = 1;
                InstalledHandlers.Add(this);
                EnsureGlobalHandlersInstalled();
            }
        }

        private static void EnsureGlobalHandlersInstalled()
        {
            if (_globalHandlersInstalled)
                return;

            _globalHandlersInstalled = true;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        /// <summary>
        /// Restores the process to its prior exception semantics once no handler is
        /// installed. A library must not keep observing process-wide events after
        /// the application has disposed the last handler that asked for them.
        /// </summary>
        private static void RemoveGlobalHandlersIfUnused()
        {
            if (!_globalHandlersInstalled || InstalledHandlers.Count > 0)
                return;

            _globalHandlersInstalled = false;

            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }

        /// <summary>
        /// Feeds a crash from an external, OS-specific source (e.g. the WinForms
        /// <c>Application.ThreadException</c> hook installed by
        /// NekoLib.Diagnostics.Windows) into the installed handlers. Never throws.
        /// </summary>
        /// <param name="source">Reporting source identifier.</param>
        /// <param name="ex">Reported exception.</param>
        /// <param name="terminating">Whether the external source considers the report terminating.</param>
        public static void ReportExternalCrash(string source, Exception ex, bool terminating)
        {
            DispatchCrash(source, ex, terminating);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            DispatchCrash("AppDomain.UnhandledException",
                e.ExceptionObject as Exception,
                e.IsTerminating);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Suppress escalation only when a handler actually recorded the fault.
            // Marking an exception observed that nobody captured would silently
            // change process behaviour for code that never asked for it.
            if (DispatchCrash("TaskScheduler.UnobservedTaskException", e.Exception, false) > 0)
            {
                try { e.SetObserved(); } catch { }
            }
        }

        private static int DispatchCrash(string source, Exception? ex, bool terminating)
        {
            CrashHandler[] snapshot;
            lock (RegistryLock)
                snapshot = InstalledHandlers.ToArray();

            int handled = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    if (snapshot[i].HandleCrash(source, ex, terminating))
                        handled++;
                }
                catch { }
            }

            return handled;
        }

        // ============================================================
        // CRASH CORE
        // ============================================================

        /// <summary>
        /// Returns true when this handler actually processed the report, and false
        /// when it was dropped because the handler is disposed or already handling
        /// another crash.
        /// </summary>
        private bool HandleCrash(string source, Exception? ex, bool terminating)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return false;

            if (Interlocked.Exchange(ref _crashing, 1) == 1)
                return false;

            var args = new CrashDetectedEventArgs(source, ex, terminating);

            try
            {
                RaiseCrashDetected(args);

                var incidentNotes = RecordIncident(args);
                var evidence = CaptureEvidence();
                WriteCrashArtifacts(args, incidentNotes, evidence);

                if (_externalNotifier != null)
                    NotifyExternal(args);
            }
            catch
            {
                // never throw inside crash path
            }
            finally
            {
                // allow multiple non-terminating reports (WinForms)
                if (!terminating)
                    Interlocked.Exchange(ref _crashing, 0);
            }

            return true;
        }

        private void RaiseCrashDetected(CrashDetectedEventArgs args)
        {
            var handler = CrashDetected;
            if (handler == null)
                return;

            foreach (var d in handler.GetInvocationList())
            {
                try { ((EventHandler<CrashDetectedEventArgs>)d)(this, args); }
                catch { }
            }
        }

        private void NotifyExternal(CrashDetectedEventArgs args)
        {
            try { _externalNotifier?.Invoke(args); }
            catch { }
        }

        private List<string> RecordIncident(CrashDetectedEventArgs args)
        {
            var notes = new List<string>();
            if (_logger != null)
            {
                string? failure;
                if (!RunContributor(
                    () => _logger.Log(
                        LogLevel.Fatal,
                        "Unhandled incident captured from " + args.Source + ".",
                        args.Exception,
                        "Diagnostics"),
                    out failure))
                    notes.Add("Fatal log: " + failure);
            }

            if (_logFlusher != null)
            {
                var flusher = _logFlusher;
                string? failure;
                bool flushed = false;
                if (!RunContributor(
                    () => flushed = flusher.Flush(_evidenceCollectionTimeout),
                    out failure))
                    notes.Add("Logging flush: " + failure);
                else if (!flushed)
                    notes.Add("Logging flush: did not complete within its budget.");
            }

            return notes;
        }

        private List<EvidenceSection> CaptureEvidence()
        {
            var sections = new List<EvidenceSection>();

            if (_logSnapshotSource != null)
            {
                var logs = _logSnapshotSource;
                sections.Add(CaptureSection("Recent logs", () => FormatLogs(logs)));
            }
            if (_telemetrySnapshotSource != null)
            {
                var telemetry = _telemetrySnapshotSource;
                sections.Add(CaptureSection("Recent telemetry", () => FormatTelemetry(telemetry)));
            }
            if (_inspectionSnapshotSource != null)
            {
                var inspection = _inspectionSnapshotSource;
                sections.Add(CaptureSection("Inspection snapshot", () => FormatInspection(inspection)));
            }
            if (_extraLines != null)
            {
                var extra = _extraLines;
                sections.Add(CaptureSection("Extra", () => extra()));
            }

            return sections;
        }

        private EvidenceSection CaptureSection(
            string title,
            Func<IEnumerable<string>> capture)
        {
            string[]? lines = null;
            string? failure;
            bool completed = RunContributor(
                () =>
                {
                    var captured = capture();
                    if (captured == null)
                    {
                        lines = new string[0];
                        return;
                    }

                    var formatted = new List<string>();
                    foreach (var line in captured)
                        formatted.Add(SanitizeInline(line ?? string.Empty));
                    lines = formatted.ToArray();
                },
                out failure);

            return completed
                ? new EvidenceSection(title, lines ?? new string[0])
                : new EvidenceSection(title, new[] { "<contributor " + failure + ">" });
        }

        private IEnumerable<string> FormatLogs(ILogSnapshotSource source)
        {
            var entries = source.GetRecentEntries(_maxRecentLogEntries);
            if (entries == null)
                yield break;

            int emitted = 0;
            foreach (var entry in entries)
            {
                // The configured limit is enforced here as well: a supplied source
                // may ignore the argument, and an unbounded crash.txt is worse than
                // a truncated one.
                if (emitted >= _maxRecentLogEntries)
                {
                    yield return "<truncated at " + _maxRecentLogEntries + " log entries>";
                    yield break;
                }

                emitted++;
                yield return entry == null ? "<null log entry>" : SafeObjectText(entry);
            }
        }

        private IEnumerable<string> FormatTelemetry(ITelemetrySnapshotSource source)
        {
            var operations = source.GetRecentOperations(_maxRecentTelemetryOperations);
            if (operations == null)
                yield break;

            int emitted = 0;
            foreach (var operation in operations)
            {
                if (emitted >= _maxRecentTelemetryOperations)
                {
                    yield return "<truncated at " + _maxRecentTelemetryOperations + " telemetry operations>";
                    yield break;
                }

                emitted++;

                if (operation == null)
                {
                    yield return "<null telemetry operation>";
                    continue;
                }

                yield return FormatTelemetryOperation(operation);
            }
        }

        private static string FormatTelemetryOperation(TelemetryOperation operation)
        {
            try
            {
                return string.Format(
                    "[{0:O}] {1}/{2} id={3} parent={4} outcome={5} duration_ms={6:0.###} checkpoints={7} dimensions={8} measurements={9}",
                    operation.StartedUtc,
                    operation.Module,
                    operation.Name,
                    operation.OperationId,
                    operation.ParentOperationId ?? string.Empty,
                    operation.Outcome,
                    operation.Duration.TotalMilliseconds,
                    FormatCheckpoints(operation.Checkpoints),
                    FormatValues(operation.Dimensions),
                    FormatValues(operation.Measurements));
            }
            catch (Exception ex)
            {
                return "<telemetry operation threw: " + ex.GetType().Name + ">";
            }
        }

        private IEnumerable<string> FormatInspection(IInspectionSnapshotSource source)
        {
            var snapshot = source.CaptureSnapshot(
                _maxInspectionOperations,
                _evidenceCollectionTimeout);

            yield return string.Format(
                "captured_utc={0:O} capacity={1} total_recorded={2} evicted={3}",
                snapshot.CapturedUtc,
                snapshot.Capacity,
                snapshot.TotalRecorded,
                snapshot.EvictedCount);

            int emitted = 0;
            foreach (var operation in snapshot.Operations)
            {
                if (emitted >= _maxInspectionOperations)
                {
                    yield return "<truncated at " + _maxInspectionOperations + " inspection operations>";
                    break;
                }

                emitted++;
                yield return operation == null ? "<null inspection operation>" : SafeObjectText(operation);
            }

            foreach (var pair in snapshot.State)
                yield return "state " + pair.Key + "=" + SafeObjectText(pair.Value);
        }

        private static string FormatCheckpoints(IReadOnlyList<TelemetryCheckpoint> checkpoints)
        {
            if (checkpoints == null || checkpoints.Count == 0)
                return string.Empty;

            var values = new string[checkpoints.Count];
            for (int i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                values[i] = checkpoint.Name + "@" +
                    checkpoint.Elapsed.TotalMilliseconds.ToString("0.###") + "ms";
            }
            return string.Join(",", values);
        }

        private static string FormatValues<T>(IReadOnlyDictionary<string, T> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;

            var result = new List<string>(values.Count);
            foreach (var pair in values)
                result.Add(pair.Key + "=" + SafeObjectText(pair.Value));
            return string.Join(",", result);
        }

        private static string SafeObjectText(object? value)
        {
            try { return value == null ? "<null>" : value.ToString() ?? string.Empty; }
            catch (Exception ex) { return "<ToString threw: " + ex.GetType().Name + ">"; }
        }

        private bool RunContributor(Action action, out string? failure)
        {
            try
            {
                Exception? contributorException = null;
                var thread = new Thread(() =>
                {
                    try { action(); }
                    catch (Exception ex) { contributorException = ex; }
                })
                {
                    IsBackground = true,
                    Name = "NekoLib.Diagnostics contributor"
                };

                thread.Start();

                // The contributor already received EvidenceCollectionTimeout as its
                // own cooperative budget. Joining on exactly that value would race a
                // well-behaved contributor and report its correct partial answer as
                // a hang, so abandonment waits for the budget plus a settle margin.
                if (!thread.Join(_contributorAbandonAfter))
                {
                    failure = "timed out";
                    return false;
                }

                if (contributorException != null)
                {
                    failure = "failed: " + contributorException.GetType().Name;
                    return false;
                }

                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                failure = "failed: " + ex.GetType().Name;
                return false;
            }
        }

        private void WriteCrashArtifacts(
            CrashDetectedEventArgs args,
            IReadOnlyList<string> incidentNotes,
            IReadOnlyList<EvidenceSection> evidence)
        {
            if (!_writeCrashFolder)
                return;

            var attemptedDirectory = _crashRootDirectory ?? string.Empty;
            var crashTxt = string.Empty;
            var dumpPath = string.Empty;
            var dumpOk = false;

            try
            {
                var bundleDir = CreateCrashFolder();
                attemptedDirectory = bundleDir;
                crashTxt = Path.Combine(bundleDir, "crash.txt");
                dumpPath = Path.Combine(bundleDir, "crash.dmp");

                WriteCrashText(crashTxt, args, incidentNotes, evidence);

                var artifactNotes = new List<string>();
                if (_dumpWriter != null)
                {
                    var writer = _dumpWriter;
                    var level = _dumpLevel;
                    var path = dumpPath;
                    string? failure;
                    if (!RunContributor(
                        () => dumpOk = writer(path, level),
                        out failure))
                        artifactNotes.Add("Dump writer: " + failure);
                    else if (!dumpOk)
                        artifactNotes.Add("Dump writer: no dump was written.");
                }

                CaptureConfiguredFileTails(bundleDir, artifactNotes);
                FinishCrashText(crashTxt, artifactNotes);
            }
            catch (Exception ex)
            {
                RaiseCrashBundleFailed(new CrashBundleFailedEventArgs(
                    attemptedDirectory,
                    ex.GetType().Name + ": " + ex.Message));
                return;
            }

            RaiseCrashBundleWritten(new CrashBundleWrittenEventArgs(
                attemptedDirectory,
                crashTxt,
                dumpPath,
                dumpOk));
        }

        private void RaiseCrashBundleWritten(CrashBundleWrittenEventArgs args)
        {
            var handler = CrashBundleWritten;
            if (handler == null)
                return;

            foreach (var d in handler.GetInvocationList())
            {
                try { ((EventHandler<CrashBundleWrittenEventArgs>)d)(this, args); }
                catch { }
            }
        }

        private void RaiseCrashBundleFailed(CrashBundleFailedEventArgs args)
        {
            var handler = CrashBundleFailed;
            if (handler == null)
                return;

            foreach (var d in handler.GetInvocationList())
            {
                try { ((EventHandler<CrashBundleFailedEventArgs>)d)(this, args); }
                catch { }
            }
        }

        // ============================================================
        // FILE / DUMP
        // ============================================================

        private string CreateCrashFolder()
        {
            var ts = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ");
            var dir = Path.Combine(_crashRootDirectory!, "crash-" + ts);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteCrashText(
            string path,
            CrashDetectedEventArgs args,
            IReadOnlyList<string> incidentNotes,
            IReadOnlyList<EvidenceSection> evidence)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Every value that may carry sensitive data is collected first and
            // redacted as one bounded batch. Structural lines are literal and never
            // reach the redactor.
            var redactable = new List<string>
            {
                "TimestampUtc: " + DateTime.UtcNow.ToString("O"),
                "Source: " + args.Source,
                "Process: " + SafeGet(() => Process.GetCurrentProcess().ProcessName),
                "PID: " + SafeGet(() => Process.GetCurrentProcess().Id.ToString()),
                "BaseDir: " + AppDomain.CurrentDomain.BaseDirectory,
                "OS: " + Environment.OSVersion
            };

            const int headerCount = 6;
            var exceptionLines = SplitLines(args.Exception != null
                ? args.Exception.ToString()
                : "(null exception)");

            redactable.AddRange(exceptionLines);
            if (incidentNotes != null)
                redactable.AddRange(incidentNotes);

            var redacted = RedactLines(redactable);

            var sb = new StringBuilder(32 * 1024);

            sb.AppendLine("==== CRASH REPORT ====");
            sb.AppendLine(redacted[0]);
            sb.AppendLine(redacted[1]);
            sb.AppendLine("IsTerminating: " + args.IsTerminating);
            sb.AppendLine("UptimeMs: " + _uptime.ElapsedMilliseconds);

            // Managed, not the OS thread id a minidump indexes. See the module
            // reference for how to locate the faulting thread inside a dump.
            sb.AppendLine("ManagedThreadId: " + Thread.CurrentThread.ManagedThreadId);

            sb.AppendLine(redacted[2]);
            sb.AppendLine(redacted[3]);
            sb.AppendLine(redacted[4]);
            sb.AppendLine(redacted[5]);
            sb.AppendLine("64bitProc: " + Environment.Is64BitProcess);
            sb.AppendLine("CLR: " + Environment.Version);
            sb.AppendLine();

            sb.AppendLine("---- Exception ----");
            for (int i = 0; i < exceptionLines.Count; i++)
                sb.AppendLine(redacted[headerCount + i]);
            sb.AppendLine();

            if (incidentNotes != null && incidentNotes.Count > 0)
            {
                sb.AppendLine("---- Incident collection notes ----");
                for (int i = 0; i < incidentNotes.Count; i++)
                    sb.AppendLine(redacted[headerCount + exceptionLines.Count + i]);
                sb.AppendLine();
            }

            if (evidence != null)
            {
                foreach (var section in evidence)
                {
                    sb.AppendLine("---- " + section.Title + " ----");
                    foreach (var line in section.Lines)
                        sb.AppendLine(line);
                    sb.AppendLine();
                }
            }

            File.WriteAllText(path, sb.ToString());
        }

        private void CaptureConfiguredFileTails(
            string bundleDir,
            ICollection<string> artifactNotes)
        {
            if (_tailFiles.Length == 0)
                return;

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in _tailFiles)
            {
                if (string.IsNullOrWhiteSpace(f) || !File.Exists(f))
                    continue;

                var sourceName = Path.GetFileName(f);
                var destinationName = sourceName;
                int suffix = 2;

                // Two configured tails may share a file name. Overwriting one with
                // the other silently loses evidence, so later collisions are
                // disambiguated and recorded.
                while (!usedNames.Add(destinationName))
                {
                    destinationName = Path.GetFileNameWithoutExtension(sourceName) +
                        "-" + suffix + Path.GetExtension(sourceName);
                    suffix++;
                }

                if (!string.Equals(destinationName, sourceName, StringComparison.Ordinal))
                {
                    artifactNotes.Add(
                        "File tail " + sourceName + ": name collision, written as " +
                        destinationName + ".");
                }

                var source = f;
                var destination = Path.Combine(bundleDir, destinationName);
                string? failure;
                if (!RunContributor(
                    () => TailFileLines(source, destination, _tailLines),
                    out failure))
                    artifactNotes.Add("File tail " + sourceName + ": " + failure);
            }
        }

        private void TailFileLines(string src, string dst, int lines)
        {
            if (lines <= 0) return;

            var q = new Queue<string>(lines);

            foreach (var line in File.ReadLines(src))
            {
                if (q.Count == lines) q.Dequeue();
                q.Enqueue(SanitizeInline(line));
            }

            File.WriteAllLines(dst, q);
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f(); } catch { return "(unavailable)"; }
        }

        private static List<string> SplitLines(string value)
        {
            var result = new List<string>();
            using (var reader = new StringReader(value ?? string.Empty))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                    result.Add(line);
            }
            return result;
        }

        /// <summary>
        /// Applies the configured redactor to a whole block under one bounded
        /// contributor. A failing or hanging redactor fails closed: nothing from
        /// the block is persisted unredacted, and the redactor is not retried.
        /// </summary>
        private string[] RedactLines(IReadOnlyList<string> lines)
        {
            var source = new string[lines.Count];
            for (int i = 0; i < source.Length; i++)
                source[i] = lines[i] ?? string.Empty;

            var redactor = _redact;
            if (redactor == null)
                return TruncateAll(source);

            if (Volatile.Read(ref _redactorUnavailable) != 0)
                return FillAll(source.Length, "<redaction unavailable>");

            // The contributor writes into its own buffer. On timeout the abandoned
            // thread keeps running, and it must not be able to touch what is
            // persisted.
            var buffer = new string[source.Length];
            string? failure;
            if (!RunContributor(
                () =>
                {
                    for (int i = 0; i < buffer.Length; i++)
                        buffer[i] = redactor(source[i]) ?? string.Empty;
                },
                out failure))
            {
                Interlocked.Exchange(ref _redactorUnavailable, 1);
                return FillAll(
                    source.Length,
                    failure == "timed out" ? "<redaction timed out>" : "<redaction failed>");
            }

            return TruncateAll(buffer);
        }

        private string[] FillAll(int count, string value)
        {
            var truncated = TruncateEvidenceLine(value);
            var result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = truncated;
            return result;
        }

        private string[] TruncateAll(string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                values[i] = TruncateEvidenceLine(values[i] ?? string.Empty);
            return values;
        }

        private string SanitizeInline(string value)
        {
            var result = value ?? string.Empty;
            var redactor = _redact;
            if (redactor != null)
            {
                if (Volatile.Read(ref _redactorUnavailable) != 0)
                {
                    result = "<redaction unavailable>";
                }
                else
                {
                    try
                    {
                        result = redactor(result) ?? string.Empty;
                    }
                    catch
                    {
                        Interlocked.Exchange(ref _redactorUnavailable, 1);
                        result = "<redaction failed>";
                    }
                }
            }

            return TruncateEvidenceLine(result);
        }

        private string TruncateEvidenceLine(string value)
        {
            if (value.Length > _maxEvidenceLineLength)
                return value.Substring(0, _maxEvidenceLineLength) + "...<truncated>";
            return value;
        }

        private void FinishCrashText(string crashTextPath, IReadOnlyList<string> notes)
        {
            var redacted = notes != null && notes.Count > 0
                ? RedactLines(notes)
                : new string[0];

            using (var writer = File.AppendText(crashTextPath))
            {
                if (redacted.Length > 0)
                {
                    writer.WriteLine("---- Platform artifact notes ----");
                    foreach (var note in redacted)
                        writer.WriteLine(note);
                    writer.WriteLine();
                }

                writer.WriteLine("==== END ====");
            }
        }

        /// <summary>
        /// Terminal and idempotent. The handler stops receiving reports, and the
        /// process-wide hooks are removed when no handler remains installed.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            lock (RegistryLock)
            {
                _installed = 0;
                InstalledHandlers.Remove(this);
                RemoveGlobalHandlersIfUnused();
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(CrashHandler));
        }

        private sealed class EvidenceSection
        {
            public EvidenceSection(string title, IReadOnlyList<string> lines)
            {
                Title = title;
                Lines = lines;
            }

            public string Title { get; }
            public IReadOnlyList<string> Lines { get; }
        }
    }
}

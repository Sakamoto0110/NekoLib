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
    public delegate bool CrashDumpWriter(string filePath, CrashDumpLevel level);

    public sealed class CrashHandlerOptions
    {
        public string? CrashRootDirectory { get; set; }
        public CrashDumpLevel DumpLevel { get; set; } = CrashDumpLevel.MiniDumpNormal;

        public List<string> TailFiles { get; set; } = new List<string>();
        public int TailLines { get; set; } = 400;

        public bool WriteCrashFolder { get; set; } = true;

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

        public ITelemetrySnapshotSource? TelemetrySnapshotSource { get; set; }
        public IInspectionSnapshotSource? InspectionSnapshotSource { get; set; }

        /// <summary>Maximum wait for each optional crash-evidence contributor.</summary>
        public TimeSpan EvidenceCollectionTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

        public int MaxRecentLogEntries { get; set; } = 200;
        public int MaxRecentTelemetryOperations { get; set; } = 100;
        public int MaxInspectionOperations { get; set; } = 100;
        public int MaxEvidenceLineLength { get; set; } = 4096;

        /// <summary>
        /// Optional line-oriented redactor applied to dynamic crash evidence and
        /// configured file tails before it is persisted.
        /// </summary>
        public Func<string, string>? Redact { get; set; }

        // NEW
        public bool NotifyWatchdog { get; set; } = true;
        public Action<CrashDetectedEventArgs>? ExternalNotifier { get; set; }
    }

    public sealed class CrashDetectedEventArgs : EventArgs
    {
        public string Source { get; }
        public Exception? Exception { get; }
        public bool IsTerminating { get; }

        public CrashDetectedEventArgs(string source, Exception? ex, bool terminating)
        {
            Source = source;
            Exception = ex;
            IsTerminating = terminating;
        }
    }

    public sealed class CrashBundleWrittenEventArgs : EventArgs
    {
        public string BundleDirectory { get; }
        public string CrashTextPath { get; }
        public string DumpPath { get; }
        public bool DumpWritten { get; }

        public CrashBundleWrittenEventArgs(string dir, string crashTxt, string dump, bool dumpWritten)
        {
            BundleDirectory = dir;
            CrashTextPath = crashTxt;
            DumpPath = dump;
            DumpWritten = dumpWritten;
        }
    }

    public sealed class CrashHandler : IDisposable
    {
        private static readonly object RegistryLock = new object();
        private static readonly List<CrashHandler> InstalledHandlers = new List<CrashHandler>();
        private static bool _globalHandlersInstalled;

        private readonly CrashHandlerOptions _o;
        private readonly Stopwatch _uptime = Stopwatch.StartNew();

        private int _installed;
        private int _crashing;
        private int _redactorUnavailable;

        public event EventHandler<CrashDetectedEventArgs>? CrashDetected;
        public event EventHandler<CrashBundleWrittenEventArgs>? CrashBundleWritten;
        public CrashHandler(CrashHandlerOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));

            if (_o.WriteCrashFolder && string.IsNullOrWhiteSpace(_o.CrashRootDirectory))
                throw new ArgumentException("CrashRootDirectory is required.", nameof(options));
            if (_o.EvidenceCollectionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), "EvidenceCollectionTimeout must be positive.");
            if (_o.MaxRecentLogEntries < 0 ||
                _o.MaxRecentTelemetryOperations < 0 ||
                _o.MaxInspectionOperations < 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Evidence limits cannot be negative.");
            if (_o.MaxEvidenceLineLength < 64)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxEvidenceLineLength must be at least 64.");
        }

        // ============================================================
        // INSTALL
        // ============================================================

        public void Install()
        {
            if (Interlocked.Exchange(ref _installed, 1) == 1)
                return;

            lock (RegistryLock)
            {
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
        /// Feeds a crash from an external, OS-specific source (e.g. the WinForms
        /// <c>Application.ThreadException</c> hook installed by
        /// NekoLib.Diagnostics.Windows) into the installed handlers. Never throws.
        /// </summary>
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
            DispatchCrash("TaskScheduler.UnobservedTaskException", e.Exception, false);

            try { e.SetObserved(); } catch { }
        }

        private static void DispatchCrash(string source, Exception? ex, bool terminating)
        {
            CrashHandler[] snapshot;
            lock (RegistryLock)
                snapshot = InstalledHandlers.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i].HandleCrash(source, ex, terminating); }
                catch { }
            }
        }

        // ============================================================
        // CRASH CORE
        // ============================================================

        private void HandleCrash(string source, Exception? ex, bool terminating)
        {
            if (Interlocked.Exchange(ref _crashing, 1) == 1)
                return;

            var args = new CrashDetectedEventArgs(source, ex, terminating);

            try
            {
                RaiseCrashDetected(args);

                var incidentNotes = RecordIncident(args);
                var evidence = CaptureEvidence();
                WriteCrashArtifacts(args, incidentNotes, evidence);

                if (_o.NotifyWatchdog && IsUnderWatchdog())
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
            try { _o.ExternalNotifier?.Invoke(args); }
            catch { }
        }

        private List<string> RecordIncident(CrashDetectedEventArgs args)
        {
            var notes = new List<string>();
            if (_o.Logger != null)
            {
                string? failure;
                if (!RunContributor(
                    () => _o.Logger.Log(
                        LogLevel.Fatal,
                        "Unhandled incident captured from " + args.Source + ".",
                        args.Exception,
                        "Diagnostics"),
                    out failure))
                    notes.Add("Fatal log: " + failure);
            }

            var flusher = _o.LogFlusher ?? _o.Logger as ILogFlusher;
            if (flusher != null)
            {
                string? failure;
                bool flushed = false;
                if (!RunContributor(
                    () => flushed = flusher.Flush(_o.EvidenceCollectionTimeout),
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
            var logs = _o.LogSnapshotSource ?? _o.Logger as ILogSnapshotSource;

            if (logs != null)
                sections.Add(CaptureSection("Recent logs", () => FormatLogs(logs)));
            if (_o.TelemetrySnapshotSource != null)
                sections.Add(CaptureSection(
                    "Recent telemetry",
                    () => FormatTelemetry(_o.TelemetrySnapshotSource)));
            if (_o.InspectionSnapshotSource != null)
                sections.Add(CaptureSection(
                    "Inspection snapshot",
                    () => FormatInspection(_o.InspectionSnapshotSource)));
            if (_o.ExtraLines != null)
                sections.Add(CaptureSection("Extra", () => _o.ExtraLines()));

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
            var entries = source.GetRecentEntries(_o.MaxRecentLogEntries);
            if (entries == null)
                yield break;

            foreach (var entry in entries)
                yield return entry == null ? "<null log entry>" : entry.ToString();
        }

        private IEnumerable<string> FormatTelemetry(ITelemetrySnapshotSource source)
        {
            var operations = source.GetRecentOperations(_o.MaxRecentTelemetryOperations);
            if (operations == null)
                yield break;

            foreach (var operation in operations)
            {
                if (operation == null)
                {
                    yield return "<null telemetry operation>";
                    continue;
                }

                yield return string.Format(
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
        }

        private IEnumerable<string> FormatInspection(IInspectionSnapshotSource source)
        {
            var snapshot = source.CaptureSnapshot(
                _o.MaxInspectionOperations,
                _o.EvidenceCollectionTimeout);

            yield return string.Format(
                "captured_utc={0:O} capacity={1} total_recorded={2} evicted={3}",
                snapshot.CapturedUtc,
                snapshot.Capacity,
                snapshot.TotalRecorded,
                snapshot.EvictedCount);

            foreach (var operation in snapshot.Operations)
                yield return operation == null ? "<null inspection operation>" : operation.ToString();
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
                if (!thread.Join(_o.EvidenceCollectionTimeout))
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
            if (!_o.WriteCrashFolder)
                return;

            try
            {
                var bundleDir = CreateCrashFolder();
                var crashTxt = Path.Combine(bundleDir, "crash.txt");
                var dumpPath = Path.Combine(bundleDir, "crash.dmp");

                WriteCrashText(
                    crashTxt,
                    args.Source,
                    args.Exception,
                    args.IsTerminating,
                    incidentNotes,
                    evidence);

                bool dumpOk = false;
                var artifactNotes = new List<string>();
                if (_o.DumpWriter != null)
                {
                    string? failure;
                    if (!RunContributor(
                        () => dumpOk = _o.DumpWriter(dumpPath, _o.DumpLevel),
                        out failure))
                        artifactNotes.Add("Dump writer: " + failure);
                    else if (!dumpOk)
                        artifactNotes.Add("Dump writer: no dump was written.");
                }

                CaptureConfiguredFileTails(bundleDir, artifactNotes);
                FinishCrashText(crashTxt, artifactNotes);

                RaiseCrashBundleWritten(new CrashBundleWrittenEventArgs(
                    bundleDir,
                    crashTxt,
                    dumpPath,
                    dumpOk));
            }
            catch { }
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

        // ============================================================
        // WATCHDOG AUTO-DETECTION
        // ============================================================

        private static bool IsUnderWatchdog()
        {
            return Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG") != null;
        }

       

        // ============================================================
        // FILE / DUMP
        // ============================================================

        private string CreateCrashFolder()
        {
            var ts = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ");
            var dir = Path.Combine(_o.CrashRootDirectory!, "crash-" + ts);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteCrashText(
            string path,
            string source,
            Exception? ex,
            bool terminating,
            IReadOnlyList<string> incidentNotes,
            IReadOnlyList<EvidenceSection> evidence)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var sb = new StringBuilder(32 * 1024);

                sb.AppendLine("==== CRASH REPORT ====");
                sb.AppendLine("TimestampUtc: " + Sanitize(DateTime.UtcNow.ToString("O")));
                sb.AppendLine("Source: " + Sanitize(source));
                sb.AppendLine("IsTerminating: " + terminating);
                sb.AppendLine("UptimeMs: " + _uptime.ElapsedMilliseconds);
                sb.AppendLine("ThreadId: " + Thread.CurrentThread.ManagedThreadId);
                sb.AppendLine("Process: " + Sanitize(SafeGet(() => Process.GetCurrentProcess().ProcessName)));
                sb.AppendLine("PID: " + Sanitize(SafeGet(() => Process.GetCurrentProcess().Id.ToString())));
                sb.AppendLine("BaseDir: " + Sanitize(AppDomain.CurrentDomain.BaseDirectory));
                sb.AppendLine("OS: " + Sanitize(Environment.OSVersion.ToString()));
                sb.AppendLine("64bitProc: " + Environment.Is64BitProcess);
                sb.AppendLine("CLR: " + Environment.Version);
                sb.AppendLine();

                sb.AppendLine("---- Exception ----");
                AppendSanitizedBlock(sb, ex != null ? ex.ToString() : "(null exception)");
                sb.AppendLine();

                if (incidentNotes != null && incidentNotes.Count > 0)
                {
                    sb.AppendLine("---- Incident collection notes ----");
                    foreach (var note in incidentNotes)
                        sb.AppendLine(Sanitize(note));
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
            catch { }
        }

        private void CaptureConfiguredFileTails(
            string bundleDir,
            ICollection<string> artifactNotes)
        {
            if (_o.TailFiles == null || _o.TailFiles.Count == 0)
                return;

            foreach (var f in _o.TailFiles)
            {
                if (string.IsNullOrWhiteSpace(f) || !File.Exists(f))
                    continue;

                var source = f;
                var destination = Path.Combine(bundleDir, Path.GetFileName(f));
                string? failure;
                if (!RunContributor(
                    () => TailFileLines(source, destination, _o.TailLines),
                    out failure))
                    artifactNotes.Add("File tail " + Path.GetFileName(f) + ": " + failure);
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

        private string Sanitize(string value)
        {
            var result = value ?? string.Empty;
            var redactor = _o.Redact;
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
                        string? redacted = null;
                        string? failure;
                        if (!RunContributor(
                            () => redacted = redactor(result),
                            out failure))
                        {
                            Interlocked.Exchange(ref _redactorUnavailable, 1);
                            result = failure == "timed out"
                                ? "<redaction timed out>"
                                : "<redaction failed>";
                        }
                        else
                        {
                            result = redacted ?? string.Empty;
                        }
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

        private string SanitizeInline(string value)
        {
            var result = value ?? string.Empty;
            var redactor = _o.Redact;
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
            if (value.Length > _o.MaxEvidenceLineLength)
                return value.Substring(0, _o.MaxEvidenceLineLength) + "...<truncated>";
            return value;
        }

        private void AppendSanitizedBlock(StringBuilder builder, string value)
        {
            using (var reader = new StringReader(value ?? string.Empty))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                    builder.AppendLine(Sanitize(line));
            }
        }

        private void FinishCrashText(string crashTextPath, IReadOnlyList<string> notes)
        {
            try
            {
                using (var writer = File.AppendText(crashTextPath))
                {
                    if (notes != null && notes.Count > 0)
                    {
                        writer.WriteLine("---- Platform artifact notes ----");
                        foreach (var note in notes)
                            writer.WriteLine(Sanitize(note));
                        writer.WriteLine();
                    }

                    writer.WriteLine("==== END ====");
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _installed, 0) == 0)
                return;

            lock (RegistryLock)
                InstalledHandlers.Remove(this);
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

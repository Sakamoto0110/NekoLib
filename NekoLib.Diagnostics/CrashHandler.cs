using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if WINFORMS
using System.Windows.Forms;
#endif

namespace NekoLib.Runtime.Diagnostics
{
    public sealed class CrashHandlerOptions
    {
        public string CrashRootDirectory { get; set; } // e.g. BaseDir\crash\pending
        public CrashDumpLevel DumpLevel { get; set; } = CrashDumpLevel.MiniDumpNormal;

        /// <summary>Optional: tail these files into crash folder (app logs, etc.).</summary>
        public List<string> TailFiles { get; set; } = new List<string>();

        public int TailLines { get; set; } = 400;

        /// <summary>Write a bundle folder for each crash (timestamped).</summary>
        public bool WriteCrashFolder { get; set; } = true;

        /// <summary>Optional extra lines in crash.txt.</summary>
        public Func<IEnumerable<string>> ExtraLines { get; set; }
    }

    public sealed class CrashDetectedEventArgs : EventArgs
    {
        public string Source { get; }
        public Exception Exception { get; }
        public bool IsTerminating { get; }

        public CrashDetectedEventArgs(string source, Exception ex, bool terminating)
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

    /// <summary>
    /// Installs global exception handlers and writes crash artifacts into a crash folder.
    /// Best-effort, synchronous, defensive.
    /// </summary>
    public sealed class CrashHandler : IDisposable
    {
        private readonly CrashHandlerOptions _o;
        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private int _installed;
        private int _crashing;

        public event EventHandler<CrashDetectedEventArgs> CrashDetected;
        public event EventHandler<CrashBundleWrittenEventArgs> CrashBundleWritten;

        public CrashHandler(CrashHandlerOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_o.CrashRootDirectory))
                throw new ArgumentException("CrashRootDirectory is required.", nameof(options));
        }

        public void Install()
        {
            if (Interlocked.Exchange(ref _installed, 1) == 1)
                return;

#if WINFORMS
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    HandleCrash("Application.ThreadException", e.Exception, false);
                };
            }
            catch { }

            #endif

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                HandleCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleCrash("TaskScheduler.UnobservedTaskException", e.Exception, false);
                try { e.SetObserved(); } catch { }
            };
        }

        private void HandleCrash(string source, Exception ex, bool terminating)
        {
            // prevent multi-handler storms
            if (Interlocked.Exchange(ref _crashing, 1) == 1)
                return;

            try
            {
                try { CrashDetected?.Invoke(this, new CrashDetectedEventArgs(source, ex, terminating)); } catch { }

                if (_o.WriteCrashFolder)
                {
                    var bundleDir = CreateCrashFolder();
                    var crashTxt = Path.Combine(bundleDir, "crash.txt");
                    var dumpPath = Path.Combine(bundleDir, "crash.dmp");

                    WriteCrashText(crashTxt, source, ex, terminating);
                    bool dumpOk = MiniDumpWriter.TryWrite(dumpPath, _o.DumpLevel);

                    TailConfiguredFiles(bundleDir);

                    try { CrashBundleWritten?.Invoke(this, new CrashBundleWrittenEventArgs(bundleDir, crashTxt, dumpPath, dumpOk)); } catch { }
                }
            }
            catch
            {
                // swallow everything
            }
        }

        private string CreateCrashFolder()
        {
            var ts = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ");
            var dir = Path.Combine(_o.CrashRootDirectory, "crash-" + ts);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteCrashText(string path, string source, Exception ex, bool terminating)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new StringBuilder(32 * 1024);
                sb.AppendLine("==== CRASH REPORT ====");
                sb.AppendLine("TimestampUtc: " + DateTime.UtcNow.ToString("O"));
                sb.AppendLine("Source: " + source);
                sb.AppendLine("IsTerminating: " + terminating);
                sb.AppendLine("UptimeMs: " + _uptime.ElapsedMilliseconds);
                sb.AppendLine("ThreadId: " + Thread.CurrentThread.ManagedThreadId);
                sb.AppendLine("Process: " + SafeGet(() => Process.GetCurrentProcess().ProcessName));
                sb.AppendLine("PID: " + SafeGet(() => Process.GetCurrentProcess().Id.ToString()));
                sb.AppendLine("BaseDir: " + AppDomain.CurrentDomain.BaseDirectory);
                sb.AppendLine("OS: " + Environment.OSVersion);
                sb.AppendLine("64bitProc: " + Environment.Is64BitProcess);
                sb.AppendLine("CLR: " + Environment.Version);
                sb.AppendLine();

                sb.AppendLine("---- Exception ----");
                sb.AppendLine(ex != null ? ex.ToString() : "(null exception)");
                sb.AppendLine();

                if (_o.ExtraLines != null)
                {
                    sb.AppendLine("---- Extra ----");
                    try
                    {
                        var lines = _o.ExtraLines();
                        if (lines != null)
                        {
                            foreach (var line in lines)
                                sb.AppendLine(line);
                        }
                    }
                    catch { sb.AppendLine("(ExtraLines failed)"); }
                    sb.AppendLine();
                }

                sb.AppendLine("==== END ====");

                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }

        private void TailConfiguredFiles(string bundleDir)
        {
            try
            {
                if (_o.TailFiles == null || _o.TailFiles.Count == 0) return;

                foreach (var f in _o.TailFiles)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(f) || !File.Exists(f)) continue;

                        var dst = Path.Combine(bundleDir, Path.GetFileName(f));
                        TailFileLines(f, dst, _o.TailLines);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void TailFileLines(string src, string dst, int lines)
        {
            if (lines <= 0) return;

            var q = new Queue<string>(lines);

            foreach (var line in File.ReadLines(src))
            {
                if (q.Count == lines) q.Dequeue();
                q.Enqueue(line);
            }

            File.WriteAllLines(dst, q);
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f(); } catch { return "(unavailable)"; }
        }

        public void Dispose()
        {
            // We intentionally do not uninstall handlers (process lifetime).
        }
    }
}

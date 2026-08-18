using System;
using System.Threading;
using System.Windows.Forms;

namespace NekoLib.Diagnostics.Windows
{
    /// <summary>
    /// Wires the Windows-only crash facilities (dbghelp.dll minidump writer and the
    /// WinForms <c>Application.ThreadException</c> hook) into the cross-platform
    /// <see cref="NekoLib.Diagnostics.CrashHandler"/>. Keeping these here lets
    /// NekoLib.Diagnostics stay net9.0 (no -windows) while OS-specific behaviour
    /// is opt-in via this package.
    ///
    /// WER suppression is exposed separately by <see cref="CrashSuppressor"/>.
    /// </summary>
    public static class WindowsCrash
    {
        private static readonly object WinFormsHookLock = new object();
        private static bool _winFormsHookInstalled;

        /// <summary>
        /// Routes crash dumps through the dbghelp.dll minidump writer. Call before
        /// <b>constructing</b> the handler: <see cref="CrashHandlerOptions"/> values are
        /// captured by the <see cref="NekoLib.Diagnostics.CrashHandler"/> constructor, so
        /// applying this afterwards has no effect on that handler. Replaces any
        /// previously configured dump writer. Returns the same options for chaining.
        /// </summary>
        public static CrashHandlerOptions UseMiniDump(this CrashHandlerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.DumpWriter = MiniDumpWriter.TryWrite;
            return options;
        }

        /// <summary>
        /// Installs the WinForms <c>Application.ThreadException</c> hook, forwarding to
        /// <see cref="NekoLib.Diagnostics.CrashHandler.ReportExternalCrash"/>. Mirrors the
        /// legacy auto-hook that used to live inside CrashHandler. The hook is installed
        /// at most once for the process lifetime. Never throws.
        ///
        /// Call it at startup, <b>before creating any window</b>: setting the
        /// application-wide unhandled-exception mode throws once a window exists on the
        /// thread. That part is best effort, and the forwarding subscription is installed
        /// either way, so the hook still works when the mode could not be set.
        /// </summary>
        public static void HookWinForms()
        {
            lock (WinFormsHookLock)
            {
                if (_winFormsHookInstalled)
                    return;

                // Best effort and deliberately separate: this throws once a window
                // exists on the thread, and the subscription below does not depend on
                // it. Sharing one try block silently skipped the subscription and left
                // a WinForms application with no crash reporting at all.
                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                }
                catch { }

                try
                {
                    Application.ThreadException += OnThreadException;
                    _winFormsHookInstalled = true;
                }
                catch { }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            CrashHandler.ReportExternalCrash(
                "Application.ThreadException",
                e.Exception,
                false);
        }
    }
}

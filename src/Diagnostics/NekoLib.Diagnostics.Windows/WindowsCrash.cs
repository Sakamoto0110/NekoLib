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
        /// installing the handler. Returns the same options for chaining.
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
        /// at most once for the process lifetime. Call at startup, after handlers are
        /// installed. Never throws.
        /// </summary>
        public static void HookWinForms()
        {
            lock (WinFormsHookLock)
            {
                if (_winFormsHookInstalled)
                    return;

                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
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

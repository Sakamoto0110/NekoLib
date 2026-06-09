using System;
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
        /// legacy auto-hook that used to live inside CrashHandler. Call once at startup,
        /// after handlers are installed. Never throws.
        /// </summary>
        public static void HookWinForms()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                    CrashHandler.ReportExternalCrash("Application.ThreadException", e.Exception, false);
            }
            catch { }
        }
    }
}

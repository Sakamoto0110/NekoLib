using System;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Windows.Threading;

namespace NekoLib.Diagnostics.Windows
{
    /// <summary>
    /// Wires the Windows-only crash facilities (dbghelp.dll minidump writer and the
    /// WinForms and WPF exception hooks) into the cross-platform
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
        private static readonly Dictionary<Dispatcher, WpfHook> WpfHooks = new Dictionary<Dispatcher, WpfHook>();

        /// <summary>
        /// Observes one WPF dispatcher's unhandled exceptions. By default reports a
        /// terminating incident and leaves native exception propagation unchanged.
        /// Reporting does not itself terminate the process.
        /// </summary>
        /// <param name="dispatcher">Dispatcher to observe, called on its owning thread.</param>
        /// <param name="markHandled">Explicit recovery policy: report a nonterminating incident and mark it handled. Use only when continuing is safe.</param>
        /// <returns>A reference-counted subscription. Dispose on the owning thread to uninstall the last subscription.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Called off the owning thread, after shutdown, or with a policy conflicting with an existing subscription.</exception>
        /// <remarks>
        /// Install a CrashHandler separately. Repeated subscriptions with the same
        /// policy forward once per dispatcher event. A terminating report latches
        /// each installed handler, preventing a second AppDomain report of the
        /// same fatal incident. Prehandled events are ignored. Native termination,
        /// explicit exit codes and Watchdog notification remain application policy.
        /// Other subscribers must not reverse the chosen handling policy afterwards.
        /// </remarks>
        public static IDisposable HookWpf(Dispatcher dispatcher, bool markHandled = false)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            dispatcher.VerifyAccess();
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                throw new InvalidOperationException("Cannot observe a dispatcher that is shutting down.");

            lock (WpfHooks)
            {
                if (!WpfHooks.TryGetValue(dispatcher, out var hook))
                {
                    hook = new WpfHook(dispatcher, markHandled);
                    WpfHooks.Add(dispatcher, hook);
                }
                else if (hook.MarkHandled != markHandled)
                    throw new InvalidOperationException("The dispatcher already has a different exception handling policy.");

                hook.References++;
                return new WpfLease(hook);
            }
        }

        private sealed class WpfHook
        {
            internal readonly Dispatcher Dispatcher;
            internal readonly bool MarkHandled;
            internal int References;

            internal WpfHook(Dispatcher dispatcher, bool markHandled)
            {
                Dispatcher = dispatcher;
                MarkHandled = markHandled;
                dispatcher.UnhandledException += OnException;
                dispatcher.ShutdownFinished += OnShutdown;
            }

            private void OnException(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                if (e.Handled) return;
                CrashHandler.ReportExternalCrash("Dispatcher.UnhandledException", e.Exception, !MarkHandled);
                if (MarkHandled) e.Handled = true;
            }

            private void OnShutdown(object sender, EventArgs e)
            {
                lock (WpfHooks) Remove();
            }

            internal void Remove()
            {
                Dispatcher.UnhandledException -= OnException;
                Dispatcher.ShutdownFinished -= OnShutdown;
                WpfHooks.Remove(Dispatcher);
            }
        }

        private sealed class WpfLease : IDisposable
        {
            private WpfHook _hook;
            internal WpfLease(WpfHook hook) { _hook = hook; }
            public void Dispose()
            {
                var hook = _hook;
                if (hook == null) return;
                hook.Dispatcher.VerifyAccess();
                lock (WpfHooks)
                {
                    if (_hook == null) return;
                    _hook = null;
                    if (--hook.References == 0) hook.Remove();
                }
            }
        }

        /// <summary>
        /// Routes crash dumps through the dbghelp.dll minidump writer. Call before
        /// <b>constructing</b> the handler: <see cref="CrashHandlerOptions"/> values are
        /// captured by the <see cref="NekoLib.Diagnostics.CrashHandler"/> constructor, so
        /// applying this afterwards has no effect on that handler. Replaces any
        /// previously configured dump writer. Returns the same options for chaining.
        /// </summary>
        /// <param name="options">Options instance to mutate before handler construction.</param>
        /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
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

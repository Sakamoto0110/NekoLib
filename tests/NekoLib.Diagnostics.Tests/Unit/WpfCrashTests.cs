using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using NekoLib.Diagnostics.Windows;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class WpfCrashTests
    {
        [Fact]
        public void RecoveryPolicy_ReportsOnceForTwoLeases_AndUninstallsLastLease()
        {
            RunSta(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                using (var handler = new CrashHandler(new CrashHandlerOptions { WriteCrashFolder = false }))
                {
                    var reports = 0;
                    CrashDetectedEventArgs captured = null;
                    handler.CrashDetected += (_, e) => { captured = e; reports++; };
                    handler.Install();
                    var first = WindowsCrash.HookWpf(dispatcher, true);
                    var second = WindowsCrash.HookWpf(dispatcher, true);
                    Assert.Throws<InvalidOperationException>(() => WindowsCrash.HookWpf(dispatcher));
                    ThrowAndPump(dispatcher);
                    Assert.Equal(1, reports);
                    Assert.False(captured.IsTerminating);
                    first.Dispose();
                    first.Dispose();
                    ThrowAndPump(dispatcher);
                    Assert.Equal(2, reports);
                    second.Dispose();
                    DispatcherUnhandledExceptionEventHandler nativeRecovery = (_, e) => e.Handled = true;
                    dispatcher.UnhandledException += nativeRecovery;
                    ThrowAndPump(dispatcher);
                    dispatcher.UnhandledException -= nativeRecovery;
                    Assert.Equal(2, reports);
                }
                dispatcher.InvokeShutdown();
            });
        }

        [Fact]
        public void PropagationPolicy_LeavesEventUnhandled_AndSuppressesSecondFatalReport()
        {
            RunSta(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                using (var handler = new CrashHandler(new CrashHandlerOptions { WriteCrashFolder = false }))
                using (WindowsCrash.HookWpf(dispatcher))
                {
                    var reports = 0;
                    bool? nativeHandled = null;
                    CrashDetectedEventArgs captured = null;
                    handler.Install();
                    handler.CrashDetected += (_, e) => { captured = e; reports++; };
                    // Test-only subscriber prevents process termination after observing
                    // the library's native propagation decision.
                    DispatcherUnhandledExceptionEventHandler capture = (_, e) =>
                    {
                        nativeHandled = e.Handled;
                        CrashHandler.ReportExternalCrash("AppDomain duplicate", e.Exception, true);
                        e.Handled = true;
                    };
                    dispatcher.UnhandledException += capture;
                    ThrowAndPump(dispatcher);
                    dispatcher.UnhandledException -= capture;
                    Assert.False(nativeHandled);
                    Assert.Equal(1, reports);
                    Assert.True(captured.IsTerminating);
                }
                dispatcher.InvokeShutdown();
            });
        }

        [Fact]
        public void HookAndDispose_RejectWorkerThread_WithoutLosingSubscription()
        {
            RunSta(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var lease = WindowsCrash.HookWpf(dispatcher, true);
                Exception installFailure = null, disposeFailure = null;
                var worker = new Thread(() =>
                {
                    try { WindowsCrash.HookWpf(dispatcher, true); } catch (Exception e) { installFailure = e; }
                    try { lease.Dispose(); } catch (Exception e) { disposeFailure = e; }
                });
                worker.Start();
                Assert.True(worker.Join(5000));
                Assert.IsType<InvalidOperationException>(installFailure);
                Assert.IsType<InvalidOperationException>(disposeFailure);
                lease.Dispose();
                dispatcher.InvokeShutdown();
                Assert.Throws<InvalidOperationException>(() => WindowsCrash.HookWpf(dispatcher));
            });
        }

        private static void ThrowAndPump(Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke((Action)(() => throw new InvalidOperationException("dispatcher probe")));
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static void RunSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception e) { failure = e; } });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(15000), "WPF test did not finish.");
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

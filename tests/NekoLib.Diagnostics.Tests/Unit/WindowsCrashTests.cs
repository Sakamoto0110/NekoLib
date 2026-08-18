using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NekoLib.Diagnostics.Windows;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class WindowsCrashTests
    {
        [DllImport("kernel32.dll")]
        private static extern uint GetErrorMode();

        [Fact]
        public void HookWinForms_CalledRepeatedly_DispatchesExternalCrashOnce()
        {
            var handler = new CrashHandler(new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None
            });
            var dispatchCount = 0;
            CrashDetectedEventArgs captured = null;

            handler.CrashDetected += (_, args) =>
            {
                dispatchCount++;
                captured = args;
            };
            handler.Install();

            try
            {
                WindowsCrash.HookWinForms();
                WindowsCrash.HookWinForms();

                var exception = new InvalidOperationException("winforms-test");
                Application.OnThreadException(exception);

                Assert.Equal(1, dispatchCount);
                Assert.NotNull(captured);
                Assert.Equal("Application.ThreadException", captured.Source);
                Assert.Same(exception, captured.Exception);
                Assert.False(captured.IsTerminating);
            }
            finally
            {
                handler.Dispose();
            }
        }

        [Fact]
        public void HookWinForms_AfterAWindowExists_StillInstallsTheForwardingSubscription()
        {
            var installedField = typeof(WindowsCrash).GetField(
                "_winFormsHookInstalled",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(installedField);

            var previouslyInstalled = (bool)installedField.GetValue(null);

            var handler = new CrashHandler(new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None
            });
            var dispatchCount = 0;
            handler.CrashDetected += (_, __) => dispatchCount++;
            handler.Install();

            using (var window = new Form())
            {
                // Force window creation on this thread: from here on, setting the
                // application-wide unhandled-exception mode throws.
                var nativeHandle = window.Handle;
                GC.KeepAlive(nativeHandle);

                Assert.Throws<InvalidOperationException>(
                    () => Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException));

                // Re-arm the process-wide latch so the post-window path runs here.
                installedField.SetValue(null, false);

                try
                {
                    WindowsCrash.HookWinForms();

                    // The subscription must be installed even though the mode call
                    // failed: sharing one try block used to skip it silently.
                    Assert.True((bool)installedField.GetValue(null));

                    if (previouslyInstalled)
                        RemoveOneForwardingSubscription();

                    Application.OnThreadException(new InvalidOperationException("post-window"));
                    Assert.Equal(1, dispatchCount);
                }
                finally
                {
                    installedField.SetValue(null, previouslyInstalled);
                    handler.Dispose();
                }
            }
        }

        [Fact]
        public void UseMiniDump_InstallsTheDumpWriterAndReturnsTheSameOptions()
        {
            var options = new CrashHandlerOptions { WriteCrashFolder = false };

            var returned = options.UseMiniDump();

            Assert.Same(options, returned);
            Assert.NotNull(options.DumpWriter);
        }

        [Fact]
        public void UseMiniDump_ReplacesAPreviouslyConfiguredWriter()
        {
            CrashDumpWriter original = (path, level) => false;
            var options = new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpWriter = original
            };

            options.UseMiniDump();

            Assert.NotNull(options.DumpWriter);
            Assert.NotSame(original, options.DumpWriter);
        }

        [Fact]
        public void UseMiniDump_NullOptions_Throws()
        {
            CrashHandlerOptions options = null;
            Assert.Throws<ArgumentNullException>(() => options.UseMiniDump());
        }

        [Fact]
        public void CrashSuppressor_Enable_IsRepeatableAndPreservesExistingErrorModeFlags()
        {
            var before = GetErrorMode();

            CrashSuppressor.Enable();
            var afterFirst = GetErrorMode();

            CrashSuppressor.Enable();
            var afterSecond = GetErrorMode();

            // Merge, never replace: every flag the host already had survives.
            Assert.Equal(before, before & afterFirst);
            Assert.Equal(afterFirst, afterSecond);
        }

        private static void RemoveOneForwardingSubscription()
        {
            var method = typeof(WindowsCrash).GetMethod(
                "OnThreadException",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var forwarder = (ThreadExceptionEventHandler)Delegate.CreateDelegate(
                typeof(ThreadExceptionEventHandler),
                method);

            Application.ThreadException -= forwarder;
        }
    }
}

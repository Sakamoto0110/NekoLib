using System;
using System.Windows.Forms;
using NekoLib.Diagnostics.Windows;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class WindowsCrashTests
    {
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
    }
}

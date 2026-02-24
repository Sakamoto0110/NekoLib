using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NekoLib.Runtime.Diagnostics;
using NekoLib.Runtime.Watchdog;

#if WINFORMS
using System.Windows.Forms;
#endif

namespace NekoLib.Tests.Watchdog
{
    internal static class Program
    {
        private static Mutex _appMutex;

        [STAThread]
        static void Main()
        {
            CrashSuppressor.Enable();

            var crash = new CrashHandler(new CrashHandlerOptions
            {
                CrashRootDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "crash",
                    "pending"),
                DumpLevel = CrashDumpLevel.MiniDumpNormal,
                ExternalNotifier = e =>
                {
                    if (Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG") != null)
                    {
                        try
                        {
                            WatchdogController.NotifyException(
                                e.Exception?.GetType().Name,
                                e.Exception?.Message,
                                e.Source);
                        }
                        catch { }
                    }
                }
            });

             

            crash.Install();

            EnsureSupervised();

            if (!TryAcquireAppSingleInstance())
                return;

#if WINFORMS
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // IMPORTANT: ensure WinForms exceptions are routed

            Application.Run(new DummyForm());
#else
            Thread.Sleep(Timeout.Infinite);
#endif
        }

        private static bool TryAcquireAppSingleInstance()
        {
            _appMutex = new Mutex(true, @"Global\DummyForm.SingleInstance", out bool created);
            return created;
        }

        private static void EnsureSupervised()
        {
            if (Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG") != null)
                return;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var currentExe = Process.GetCurrentProcess().MainModule.FileName;
            var hostPath = Path.Combine(baseDir, "WatchdogHost.exe");

            if (!File.Exists(hostPath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"--target \"{currentExe}\"",
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }

        private static void LaunchProgram()
        {

        }

      
    }
}
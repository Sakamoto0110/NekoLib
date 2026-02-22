using System;
using System.Diagnostics;
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

        private static bool TryAcquireAppSingleInstance()
        {
            bool created;
            _appMutex = new Mutex(true, @"Global\DummyForm.SingleInstance", out created);
            return created;
        }

        [STAThread]
        static void Main(string[] args)
        {
            var mode = WatchdogEntrypoint.ResolveMode(args);

            WatchdogEntrypoint.BootstrapIfNeeded(
                mode,
                Process.GetCurrentProcess().MainModule.FileName,
                AppDomain.CurrentDomain.BaseDirectory);

            if (mode == WatchdogMode.WatchdogHost)
            {
                LaunchWatchdog();
                return;
            }

            if (mode == WatchdogMode.UnderWatchdog)
            {
                if (!TryAcquireAppSingleInstance())
                    return;

                LaunchProgram();
            }
        }

        private static void LaunchWatchdog()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var options = new WatchdogOptions
            {
                TargetPath = exePath,
                WorkingDirectory = baseDir,
                PipeName = "NekoLib.Watchdog",

                EnableFileLogging = true,
                LogPath = baseDir + "watchdog.log",

                GracefulKillTimeoutMs = 3000,
                ForceKillTimeoutMs = 2000,
                RestartDelayMs = 1000,
                MonitorPollMs = 250
            };

            using (var watchdog = new WatchdogRuntime(options))
            {
                watchdog.Start();
                Console.WriteLine("Watchdog started. Pipe ready.");

                watchdog.WaitForExit();
            }
        }

       

        private static void LaunchProgram()
        {
            CrashSuppressor.Enable();

            var crash = new CrashHandler(new CrashHandlerOptions
            {
                CrashRootDirectory =
                    AppDomain.CurrentDomain.BaseDirectory + @"crash\pending",
                DumpLevel = CrashDumpLevel.MiniDumpNormal
            });

            crash.Install();

#if WINFORMS
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DummyForm());
#else
            Thread.Sleep(Timeout.Infinite);
#endif
        }
    }
}

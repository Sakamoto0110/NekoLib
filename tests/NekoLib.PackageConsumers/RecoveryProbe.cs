using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Diagnostics;
using NekoLib.Watchdog;

namespace NekoLib.PackageConsumers
{
    // Qualification-only control. This is not an application fault-control API.
    internal sealed class RecoveryProbe : IDisposable
    {
        internal readonly string Mode;
        private readonly string _directory;
        private readonly CrashHandler _crash;
        private Process? _helper;

        internal static int TerminationHelper()
        {
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        internal static int Control(string command)
        {
            if (command == "status") { Console.WriteLine(WatchdogController.Status()); return WatchdogController.Ping() ? 0 : 1; }
            if (command == "stop") return WatchdogController.Stop() ? 0 : 1;
            if (command == "restart") return WatchdogController.Restart() ? 0 : 1;
            return 2;
        }

        internal RecoveryProbe(string[] args)
        {
            _directory = Path.GetFullPath(args[1]);
            Mode = args[2];
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "runtime.txt"), Environment.Version + ";bitness=" + IntPtr.Size * 8);
#if NET9_0_OR_GREATER
            ModuleProbe.Require(Environment.Version.Major == 9, "Recovery consumer executes on .NET 9");
#endif
            WatchdogBootstrap.EnsureStarted(args, 15000);
            _crash = new CrashHandler(new CrashHandlerOptions
            {
                CrashRootDirectory = Path.Combine(_directory, "incidents"), DumpLevel = CrashDumpLevel.None,
                ExternalNotifier = e =>
                {
                    File.AppendAllText(Path.Combine(_directory, "reports.txt"), e.Source + " terminating=" + e.IsTerminating + Environment.NewLine);
                    if (e.IsTerminating) Environment.Exit(71);
                }
            });
            _crash.Install();
        }

        internal void Shown(Action<Action> post, int windowCount)
        {
            ModuleProbe.Require(windowCount == 1, "Recovery starts exactly one main window");
            var first = !File.Exists(Path.Combine(_directory, "initial.ready"));
            var pid = Process.GetCurrentProcess().Id;
            if (Mode == "restart" || Mode == "clean-stop")
            {
                using (var current = Process.GetCurrentProcess())
                {
                    _helper = Process.Start(new ProcessStartInfo
                    {
                        FileName = current.MainModule!.FileName,
                        Arguments = "--termination-helper", UseShellExecute = false,
                        CreateNoWindow = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    }) ?? throw new InvalidOperationException("Cannot start the test-owned termination helper.");
                }
                File.WriteAllText(Path.Combine(_directory, first ? "initial.helper.pid" : "replacement.helper.pid"), _helper.Id.ToString());
            }
            File.WriteAllText(Path.Combine(_directory, first ? "initial.ready" : "replacement.ready"), pid.ToString());
            File.WriteAllText(Path.Combine(_directory, first ? "initial.windows" : "replacement.windows"), windowCount.ToString());
            if (!first) return;
            if (Mode == "ui-fatal" || Mode == "recoverable")
                Task.Delay(400).ContinueWith(_ =>
                {
                    post(() => throw new InvalidOperationException("qualification UI fault"));
                    if (Mode == "recoverable")
                        post(() => File.WriteAllText(Path.Combine(_directory, "recovered.signal"), "UI processed the next callback"));
                });
            if (Mode == "worker-fatal")
                Task.Delay(400).ContinueWith(_ => new Thread(() => throw new InvalidOperationException("qualification worker fault")) { IsBackground = true }.Start());
        }

        public void Dispose() { _crash.Dispose(); _helper?.Dispose(); }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using NekoLib.Watchdog;

internal static class WatchdogAutoProbe
{
    internal static string DirectoryPath;
    internal static string Mode;
    internal static bool Initial;
    private static bool _finished;
    private static bool _ready;

    internal static bool HandleControl(string[] args)
    {
        if (args.Length != 2 || args[0] != "--control") return false;
        if (args[1] == "status") Console.WriteLine(WatchdogController.Status());
        else if (args[1] == "stop") Console.WriteLine(WatchdogController.Stop());
        else if (args[1] == "restart") Console.WriteLine(WatchdogController.Restart());
        else if (args[1] == "entry") Console.WriteLine(Assembly.GetEntryAssembly().EntryPoint.DeclaringType.FullName);
        else throw new ArgumentException("Unknown qualification control.");
        return true;
    }

    internal static void Initialize(string[] args)
    {
        Mode = args[0];
        DirectoryPath = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(DirectoryPath);
        var initial = Path.Combine(DirectoryPath, "initial.pid");
        Initial = !File.Exists(initial);
        File.WriteAllText(Path.Combine(DirectoryPath, Initial ? "initial.pid" : "replacement.pid"), Process.GetCurrentProcess().Id.ToString());
        File.WriteAllText(Path.Combine(DirectoryPath, "runtime.txt"), Environment.Version + ";bitness=" + IntPtr.Size * 8);
        File.WriteAllLines(Path.Combine(DirectoryPath, Initial ? "initial.args" : "replacement.args"), Array.ConvertAll(args, x => Convert.ToBase64String(Encoding.UTF8.GetBytes(x))));
        WatchdogAutoBootstrap.Warning += warning => File.AppendAllText(Path.Combine(DirectoryPath, "warnings.txt"), warning + Environment.NewLine);
        if (Mode != "defaults")
        {
            WatchdogAutoBootstrap.Configure(options =>
            {
                options.HandshakeTimeoutMs = 15000;
                options.RuntimeOptions.LogPath = Path.Combine(DirectoryPath, "watchdog.log");
                options.RuntimeOptions.WorkingDirectory = DirectoryPath;
                options.RuntimeOptions.PendingCrashRoot = Path.Combine(DirectoryPath, "pending");
                options.RuntimeOptions.BundleRoot = Path.Combine(DirectoryPath, "bundles");
                options.RuntimeOptions.EnableCrashBundling = false;
                options.RuntimeOptions.MonitorPollMs = 50;
                options.RuntimeOptions.RestartDelayMs = 200;
                options.RuntimeOptions.HeartbeatIntervalMs = 100;
                if (Mode == "warning") options.HostingMode = WatchdogHostingMode.Sidecar;
            });
        }
        if (Mode == "disable-before") WatchdogAutoBootstrap.Disable();
    }

    internal static bool VetoClose => Initial && Mode == "restart";

    internal static void Tick(int windowCount, Action close)
    {
        if (_finished) return;
        if (_ready && Initial && (Mode == "kill" || Mode == "restart" || Mode == "restart-soft" || Mode == "stop")) return;
        var active = WatchdogAutoBootstrap.IsActive;
        if (Mode == "warning")
        {
            if (!File.Exists(Path.Combine(DirectoryPath, "warnings.txt"))) return;
            if (active) throw new InvalidOperationException("Failed activation claimed active supervision.");
        }
        else if (Mode == "disable-before")
        {
            if (active) throw new InvalidOperationException("Cancelled bootstrap activated.");
        }
        else if (!active) return;

        if (windowCount != 1) throw new InvalidOperationException("Expected one main window.");
        File.WriteAllText(Path.Combine(DirectoryPath, Initial ? "initial.windows" : "replacement.windows"), windowCount.ToString());
        File.WriteAllText(Path.Combine(DirectoryPath, Initial ? "initial.ready" : "replacement.ready"), Process.GetCurrentProcess().Id.ToString());
        _ready = true;
        if (Mode == "disable-after")
        {
            WatchdogAutoBootstrap.Disable();
            WatchdogAutoBootstrap.Disable();
            if (WatchdogAutoBootstrap.IsActive) throw new InvalidOperationException("Disable did not clear active supervision.");
            File.WriteAllText(Path.Combine(DirectoryPath, "disabled-alive.txt"), "Application remains alive after nonterminating detach.");
        }
        if (Initial && (Mode == "kill" || Mode == "restart" || Mode == "restart-soft" || Mode == "stop")) return;
        if (Initial && Mode == "crash") Environment.Exit(71);
        _finished = true;
        File.WriteAllText(Path.Combine(DirectoryPath, Initial ? "initial.done" : "replacement.done"), "Normal application return.");
        close();
    }
}

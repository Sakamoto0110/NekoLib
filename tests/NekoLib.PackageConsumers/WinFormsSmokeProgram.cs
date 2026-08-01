using System;

namespace NekoLib.PackageConsumers
{
    internal static class WinFormsSmokeProgram
    {
        [STAThread]
        private static void Main()
        {
            var packageTypes = new[]
            {
                typeof(NekoLib.Core.Logging.ILogger),
                typeof(NekoLib.Data.Query.DatabaseQuery),
                typeof(NekoLib.Inspection.InspectionRuntime),
                typeof(NekoLib.Devices.Core.Abstractions.SerialConfig),
                typeof(NekoLib.Diagnostics.CrashHandler),
                typeof(NekoLib.Diagnostics.Windows.WindowsCrash),
                typeof(NekoLib.Logging.Logger),
                typeof(NekoLib.Mvvm.ViewModelBase),
                typeof(NekoLib.Navigation.Bootstrap.PageNavBootstrap),
                typeof(NekoLib.Navigation.WinForms.Adapters.WinFormsPlatformAdapter),
                typeof(NekoLib.Pipes.PipeClient),
                typeof(NekoLib.Telemetry.TelemetryPipeline),
                typeof(NekoLib.Watchdog.WatchdogOptions)
            };

            Console.WriteLine("Loaded {0} NekoLib package surfaces.", packageTypes.Length);
        }
    }
}

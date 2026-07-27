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
                typeof(NekoLib.Core.Diagnostics.ILogger),
                typeof(NekoLib.Data.Query.DatabaseQuery),
                typeof(NekoLib.DebugUtils.DebugUtilsRuntime),
                typeof(NekoLib.Devices.Core.Abstractions.SerialConfig),
                typeof(NekoLib.Diagnostics.CrashHandler),
                typeof(NekoLib.Diagnostics.Windows.WindowsCrash),
                typeof(NekoLib.Logger.Logger),
                typeof(NekoLib.Mvvm.ViewModelBase),
                typeof(NekoLib.Navigation.Bootstrap.PageNavBootstrap),
                typeof(NekoLib.Navigation.WinForms.Adapters.WinFormsPlatformAdapter),
                typeof(NekoLib.Pipes.PipeClient),
                typeof(NekoLib.Watchdog.WatchdogOptions)
            };

            Console.WriteLine("Loaded {0} NekoLib package surfaces.", packageTypes.Length);
        }
    }
}

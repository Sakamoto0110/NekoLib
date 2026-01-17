using System;
using NekoLib.Runtime.Watchdog;

namespace WatchdogHostSample
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var options = WatchdogArgs.FromArgs(args);
            options.EnableHotkeys = true;

            using(var wdg = new WatchdogRuntime(options))
            {
                wdg.Start();

                // Either run the watchdog message loop (closest to legacy)
                // or use Application.Run() in your own host UI.
                wdg.RunMessageLoop();
            }
        }
    }
}

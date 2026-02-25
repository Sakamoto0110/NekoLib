using System;
using System.IO;
 

namespace NekoLib.Watchdog.Host
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var options = HostArgumentParser.Parse(args);

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    runtime.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    "watchdog_host_fatal.log",
                    $"[{DateTime.Now}] {ex}\n");
            }
        }
    }
}
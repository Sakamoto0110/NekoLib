using System;
using System.IO;
 

namespace NekoLib.Watchdog.Host
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                var options = HostArgumentParser.Parse(args);

                using (var runtime = new WatchdogRuntime(options))
                {
                    runtime.Start();
                    runtime.WaitForExit();
                }

                return 0;
            }
            catch (Exception ex)
            {
                TryWriteFatalLog(ex);
                return 1;
            }
        }

        internal static void TryWriteFatalLog(Exception exception)
        {
            try
            {
                File.AppendAllText(
                    "watchdog_host_fatal.log",
                    $"[{DateTime.Now}] {exception}\n");
            }
            catch
            {
                // Fatal reporting must never replace the original startup failure.
            }
        }
    }
}

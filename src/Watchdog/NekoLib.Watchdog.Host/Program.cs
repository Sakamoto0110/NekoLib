using System;

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
            => HostFatalLog.TryWrite(exception);
    }
}

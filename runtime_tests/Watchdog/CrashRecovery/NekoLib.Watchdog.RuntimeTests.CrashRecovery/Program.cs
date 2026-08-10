#nullable enable
using System;
using System.Threading;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions();
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-WDOG: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText());
                return ExitCodes.Usage;
            }

            if (options.PrintScheduleOnly)
                return SchedulePreview.Print(options);

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                bool interrupted = false;
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    if (interrupted) return;
                    interrupted = true;
                    eventArgs.Cancel = true;
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("interrupt received; winding down and writing a partial summary");
                    cancellation.Cancel();
                };

                try
                {
                    return new ScenarioRun(options, cancellation.Token).Execute();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("E3-WDOG failed outside the artifact boundary: " + ex);
                    return ExitCodes.Unexpected;
                }
            }
        }
    }
}

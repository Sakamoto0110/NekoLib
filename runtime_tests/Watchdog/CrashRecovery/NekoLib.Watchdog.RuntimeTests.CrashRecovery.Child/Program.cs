#nullable enable
using System;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ChildOptions options = ChildOptions.Parse(args);

            // A planned crash deliberately escapes Main. Catching it here would
            // turn the real unhandled-process terminal into a scenario return.
            return new ChildRuntime(options, args).Run();
        }
    }
}

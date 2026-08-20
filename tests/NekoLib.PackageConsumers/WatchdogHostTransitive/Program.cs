using System;

namespace NekoLib.PackageConsumers.WatchdogHostTransitive
{
    internal static class Program
    {
        private static void Main()
        {
            Console.WriteLine(
                typeof(NekoLib.PackageConsumers.WatchdogHostWrapper.WrapperMarker)
                    .FullName);
        }
    }
}

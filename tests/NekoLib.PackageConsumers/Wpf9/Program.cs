using System;

namespace NekoLib.PackageConsumers.Wpf9
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var packageTypes = new[]
            {
                typeof(NekoLib.Core.Diagnostics.ILogger),
                typeof(NekoLib.Mvvm.ViewModelBase),
                typeof(NekoLib.Navigation.Contracts.Pages.IPageView),
                typeof(NekoLib.Navigation.Wpf.Adapters.WpfPlatformAdapter)
            };

            Console.WriteLine("Loaded {0} WPF package surfaces.", packageTypes.Length);
        }
    }
}

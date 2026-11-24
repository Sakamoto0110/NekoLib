using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WatchdogAutoWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (WatchdogAutoProbe.HandleControl(e.Args)) { Shutdown(); return; }
            WatchdogAutoProbe.Initialize(e.Args);
            var window = new MainWindow();
            window.Closing += (sender, args) => { if (WatchdogAutoProbe.VetoClose) args.Cancel = true; };
            window.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (sender, args) => WatchdogAutoProbe.Tick(Windows.Count, window.Close);
            timer.Start();
        }
    }
    public sealed class QualificationLabel : TextBlock { }
}

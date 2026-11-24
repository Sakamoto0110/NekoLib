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
            window.Left = QualificationWindowPlacement.X(-20000);
            window.Top = QualificationWindowPlacement.Y(-20000);
            window.SourceInitialized += (sender, args) => QualificationWindowPlacement.Apply(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            window.Loaded += (sender, args) => QualificationWindowPlacement.Apply(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            window.Closing += (sender, args) => { if (WatchdogAutoProbe.VetoClose) args.Cancel = true; };
            window.Show();
            if (WatchdogAutoProbe.Mode.StartsWith("ui-", StringComparison.Ordinal))
            {
                // The native UI probe needs an observable HWND, not SW_HIDE.
                window.Hide();
                window.Show();
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (sender, args) => WatchdogAutoProbe.Tick(Windows.Count, window.Close, () =>
            {
                var modal = new Window { Owner = window, Title = "Responsive modal qualification", ShowInTaskbar = false, Left = QualificationWindowPlacement.X(-20000), Top = QualificationWindowPlacement.Y(-20000), Width = 200, Height = 100 };
                modal.SourceInitialized += (s, args2) => QualificationWindowPlacement.Apply(new System.Windows.Interop.WindowInteropHelper(modal).Handle);
                modal.Loaded += (s, args2) => QualificationWindowPlacement.Apply(new System.Windows.Interop.WindowInteropHelper(modal).Handle);
                var modalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3200) };
                modalTimer.Tick += (s, args2) => { modalTimer.Stop(); modal.Close(); };
                modalTimer.Start();
                modal.ShowDialog();
            });
            timer.Start();
        }
    }
    public sealed class QualificationLabel : TextBlock { }
}

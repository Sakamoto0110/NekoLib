using System;
using System.Drawing;
using System.Windows.Forms;

namespace WatchdogAutoForms
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (WatchdogAutoProbe.HandleControl(args)) return 0;
            WatchdogAutoProbe.Initialize(args);
            using (var window = new Form { Text = "Watchdog automatic qualification", StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
            using (var timer = new Timer { Interval = 100 })
            {
                window.FormClosing += (sender, e) => { if (WatchdogAutoProbe.VetoClose) e.Cancel = true; };
                timer.Tick += (sender, e) => WatchdogAutoProbe.Tick(Application.OpenForms.Count, window.Close, () =>
                {
                    using (var modal = new Form { Text = "Responsive modal qualification", ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
                    using (var modalTimer = new Timer { Interval = 3200 })
                    {
                        modalTimer.Tick += (s, args2) => { modalTimer.Stop(); modal.Close(); };
                        modalTimer.Start();
                        modal.ShowDialog(window);
                    }
                });
                timer.Start();
                if (WatchdogAutoProbe.Mode.StartsWith("ui-", StringComparison.Ordinal))
                {
                    // Consume the launcher's initial SW_HIDE before the message
                    // loop shows the observable, offscreen qualification window.
                    window.Show();
                    window.Hide();
                }
                Application.Run(window);
            }
            return 0;
        }
    }
}

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
                timer.Tick += (sender, e) => WatchdogAutoProbe.Tick(Application.OpenForms.Count, window.Close);
                timer.Start();
                Application.Run(window);
            }
            return 0;
        }
    }
}

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
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
            {
                Console.Error.WriteLine(e.Exception);
                Environment.Exit(79);
            };
            WatchdogAutoProbe.Initialize(args);
            using (var window = new Form { Text = "Watchdog automatic qualification", StartPosition = FormStartPosition.Manual, Location = new Point(QualificationWindowPlacement.X(-20000), QualificationWindowPlacement.Y(-20000)) })
            using (var timer = new Timer { Interval = 100 })
            {
                window.HandleCreated += (sender, e) => QualificationWindowPlacement.Apply(window.Handle);
                window.Shown += (sender, e) => QualificationWindowPlacement.Apply(window.Handle);
                window.FormClosing += (sender, e) => { if (WatchdogAutoProbe.VetoClose) e.Cancel = true; };
                timer.Tick += (sender, e) => WatchdogAutoProbe.Tick(Application.OpenForms.Count, window.Close, () =>
                {
                    using (var modal = new Form { Text = "Responsive modal qualification", ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(QualificationWindowPlacement.X(-20000), QualificationWindowPlacement.Y(-20000)) })
                    using (var modalTimer = new Timer { Interval = 3200 })
                    {
                        modal.HandleCreated += (s, args2) => QualificationWindowPlacement.Apply(modal.Handle);
                        modal.Shown += (s, args2) => QualificationWindowPlacement.Apply(modal.Handle);
                        modalTimer.Tick += (s, args2) => { modalTimer.Stop(); modal.Close(); };
                        modalTimer.Start();
                        modal.ShowDialog(window);
                    }
                });
                timer.Start();
                if (WatchdogAutoProbe.Mode.StartsWith("ui-", StringComparison.Ordinal))
                {
                    // Consume the launcher's initial SW_HIDE before the message
                    // loop shows the observable qualification window.
                    window.Show();
                    window.Hide();
                }
                Application.Run(window);
            }
            return 0;
        }
    }
}

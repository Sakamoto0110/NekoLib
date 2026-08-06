using System;
using System.Windows.Forms;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            // DPI awareness is configured differently per target family and there is
            // no shared API: modern .NET has Application.SetHighDpiMode, while .NET
            // Framework only reads it from app.config before the first window exists.
            // SystemAware rather than PerMonitorV2 on purpose - the ACE/OleDb path is
            // the subject here, and per-monitor rescaling would add a variable that has
            // nothing to do with the database behaviour being observed.
#if !NETFRAMEWORK
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (AppServices.Start())
            {
                Application.Run(new ShellForm());
            }
        }
    }
}

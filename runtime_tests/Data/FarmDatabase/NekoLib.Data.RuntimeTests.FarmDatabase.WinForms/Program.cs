using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    internal static class Program
    {
        private const int AttachParentProcess = -1;

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        [STAThread]
        internal static void Main(string[] args)
        {
            // Headless first, before any UI type is touched: a thirty-thousand tick
            // run cannot be driven through a window, and comparing the two engines on
            // the same seed needs the UI out of the way entirely.
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--headless", StringComparison.OrdinalIgnoreCase))
            {
                // A WinExe owns no console. Borrowing the caller's makes the output
                // visible when run from a terminal; when it is redirected to a file
                // this is simply a no-op.
                AttachConsole(AttachParentProcess);
                Environment.Exit(HeadlessRun.Execute(args));
                return;
            }

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

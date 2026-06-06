using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace NekoLib.Observability.RuntimeTests.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (TryRunSupervisedProbe(args))
                return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RuntimeHubForm());
        }

        private static bool TryRunSupervisedProbe(string[] args)
        {
            if (args == null || args.Length == 0 || args[0] != "--supervised-probe")
                return false;

            var path = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "nekolib-supervised-probe.txt");
            var seconds = 3;
            if (args.Length > 2)
                int.TryParse(args[2], out seconds);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path,
                    "pid=" + System.Diagnostics.Process.GetCurrentProcess().Id +
                    " utc=" + DateTime.UtcNow.ToString("O") +
                    " NEKO_UNDER_WATCHDOG=" + (Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG") ?? "(missing)") +
                    Environment.NewLine);
            }
            catch { }

            Thread.Sleep(Math.Max(1, seconds) * 1000);
            return true;
        }
    }
}

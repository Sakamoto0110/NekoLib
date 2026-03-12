using System;
using System.Windows.Forms;

namespace BundlerTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (Environment.OSVersion.Version.Major >= 6)
                Win32.SetProcessDPIAware();

            Application.Run(new MainForm());
        }
    }
}

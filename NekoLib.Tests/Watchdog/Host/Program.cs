using NekoLib.Runtime.Watchdog;

namespace NekoLib.Tests.Watchdog;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var options = new WatchdogOptions
        {
            TargetPath = @"C:\dev\NekoLib\NekoLib.Tests\Watchdog\DummyForm\bin\Debug\net9.0-windows\DummyForm.exe",
            PipeName = "NekoLib.Watchdog"
        };

        var watchdog = new WatchdogRuntime(options);
        watchdog.Start();

        Application.Run(new Form()); // message pump only
    }
}
using NekoLib.Runtime.Watchdog;
using System.Diagnostics;

namespace NekoLib.Tests.Watchdog;

public partial class Controller : Form
{
    public Controller()
    {
        InitializeComponent();
         

    }
    private void btnStart_Click(object sender, EventArgs e)
    {
        var options = new WatchdogOptions
        {
            TargetPath = @"C:\dev\NekoLib\NekoLib.Tests\Watchdog\DummyForm\bin\Debug\net9.0-windows\DummyForm.exe",
            PipeName = "NekoLib.Watchdog"
        };

        var watchdog = new WatchdogRuntime(options);
        watchdog.Start();
        WatchdogController.Start();
    }

    private void btnPause_Click(object sender, EventArgs e)
    {
        WatchdogController.Pause();
    }

    private void btnStop_Click(object sender, EventArgs e)
    {
        WatchdogController.Stop();
    }

    private void btnPing_Click(object sender, EventArgs e)
    {
        var ok = WatchdogController.Ping();
        rtbStatus.AppendText("Ping: " + ok + Environment.NewLine);
    }

    private void btnStatus_Click(object sender, EventArgs e)
    {
        var status = WatchdogController.Status();
        rtbStatus.AppendText(status + Environment.NewLine);
    }
}

using NekoLib.Runtime.Watchdog;
using System.Diagnostics;

namespace NekoLib.Tests.Watchdog;

public partial class DummyForm : Form
{
    public DummyForm()
    {
        InitializeComponent();
        Text = "Watchdog Dummy App";
        WatchdogLog.OnLog += msg =>
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => outputTop.AppendText(msg + '\n')));
            else
                outputTop.AppendText(msg + '\n');
        };

     }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Comment this out to test graceful close
        // e.Cancel = true;

        base.OnFormClosing(e);
    }

    private void btnExit0_Click(object sender, EventArgs e)
    {
        Environment.Exit(0);
    }

    private void btnExit1_Click(object sender, EventArgs e)
    {
        Environment.Exit(1);
    }

    private void btnCrash_Click(object sender, EventArgs e)
    {
        throw new InvalidOperationException("Intentional crash");
    }

    private void btnPause_Click(object sender, EventArgs e)
    {
        WatchdogController.Pause();

    }

    private void btnShutdown_Click(object sender, EventArgs e)
    {
        WatchdogController.Stop();
    }

    private void btnStatus_Click(object sender, EventArgs e)
    {
        Debug.WriteLine(WatchdogController.Status());
        outputBottom.AppendText(WatchdogController.Status() + '\n');
    }

    private void btnPing_Click(object sender, EventArgs e)
    {
        Debug.WriteLine(WatchdogController.Ping());

    }
}

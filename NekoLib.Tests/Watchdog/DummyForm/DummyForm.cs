namespace NekoLib.Tests.Watchdog;

public partial class DummyForm : Form
{
    public DummyForm()
    {
        InitializeComponent();
        Text = "Watchdog Dummy App";
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
}

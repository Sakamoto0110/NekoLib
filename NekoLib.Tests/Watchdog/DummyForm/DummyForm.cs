using NekoLib.Diagnostics.Sinks;
 using NekoLib.Runtime.Watchdog;
 
using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Runtime.Sinks;
using System.Diagnostics;
using System.Windows.Forms;
using System;
namespace NekoLib.Tests.Watchdog{

    public partial class DummyForm : Form
    {
        public DummyForm()
        {
            InitializeComponent();
            Text = "Watchdog Dummy App";
            var diagnostics = new Diagnostics.Diagnostics(new Logger(LogLevel.Info, new DebugLogSink(), new WatchdogPipeLogSink())
                 , new MemoryTelemetrySink());

            WatchdogController.SubscribeLogs(msg => outputBottom.AppendText($"[Watchdog Log] {msg}\n"));

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
            Console.WriteLine(WatchdogController.Status());
            //outputBottom.AppendText(WatchdogController.Status() + '\n');
        }

        private void btnPing_Click(object sender, EventArgs e)
        {
            Console.WriteLine(WatchdogController.Ping());

        }
    }
}
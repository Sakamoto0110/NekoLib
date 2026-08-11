#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions("winforms");
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E3-NAV WinForms: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText("winforms"));
                return ExitCodes.Usage;
            }

            if (options.PrintScheduleOnly) return NavigationScenarioRun.PrintSchedule(options);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (ScenarioForm form = new ScenarioForm(options))
            {
                bool interrupted = false;
                Console.CancelKeyPress += (_, e) =>
                {
                    if (interrupted) return;
                    interrupted = true;
                    e.Cancel = true;
                    form.RequestCancellation();
                };
                Application.Run(form);
                return form.ExitCode;
            }
        }
    }

    internal sealed class ScenarioForm : Form
    {
        private readonly ScenarioOptions _options;
        private readonly Panel _host = new Panel { Dock = DockStyle.Fill };
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private bool _complete;

        public ScenarioForm(ScenarioOptions options)
        {
            _options = options;
            Text = "NekoLib E3-NAV WinForms";
            Width = 960;
            Height = 640;
            Controls.Add(_host);
            Shown += OnShown;
            FormClosing += OnClosing;
        }

        public int ExitCode { get; private set; } = ExitCodes.Unexpected;
        public void RequestCancellation() => _cancellation.Cancel();

        private async void OnShown(object? sender, EventArgs e)
        {
            try
            {
                WinFormsScenarioPlatform platform = new WinFormsScenarioPlatform(_host);
                ExitCode = await new NavigationScenarioRun(
                    _options, platform, _cancellation.Token).ExecuteAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("E3-NAV WinForms failed outside the scenario: " + ex);
                ExitCode = ExitCodes.Unexpected;
            }
            finally
            {
                _complete = true;
                Close();
            }
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_complete) return;
            e.Cancel = true;
            _cancellation.Cancel();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cancellation.Dispose();
            base.Dispose(disposing);
        }
    }
}

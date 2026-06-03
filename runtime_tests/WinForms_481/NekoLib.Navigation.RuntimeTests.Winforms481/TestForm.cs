using System;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.WinForms.Adapters;
using HomeView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.Home.HomePage;

namespace NekoLib.Navigation.RuntimeTests.Winforms481
{
    /// <summary>
    /// Primary test window — hosts the page graph (HOME → A → {B, C, D, E, F},
    /// D → F, E → F) and owns the secondary <see cref="TestToolsForm"/>.
    /// Demo entry point (replaces the deleted legacy `Form1`/`ShellForm`).
    /// </summary>
    public partial class TestForm : Form
    {
        private TestToolsForm _toolsWindow;
        private bool _closingFromShutdown;

        public TestForm()
        {
            InitializeComponent();

            // Bootstrap the navigation framework against the designer-owned host panel.
            // Fully qualify `Diagnostics` to disambiguate between
            // `NekoLib.Diagnostics.Diagnostics` (the host class) and
            // `NekoLib.Navigation.Diagnostics` (a sub-namespace), both visible
            // from inside this project's NekoLib.Navigation.* namespace tree.
            var logger = new Logger(LogLevel.Debug, new DebugLogSink());
            var memory = new MemoryTelemetrySink();
            var diagnostics = new global::NekoLib.Diagnostics.Diagnostics(logger, memory);

            var ctx = PageNavBootstrap
                .Use<WinFormsPlatformAdapter>(hostPanel)
                .RegisterPagesFromAssembly(typeof(TestForm).Assembly)
                .UseDiagnostics(diagnostics)
                .ConfigurePages(cfg => { cfg.Page<HomeView>().AsHome(); })
                .Start();

            NavigationService.UseContext(ctx);

            _toolsWindow = new TestToolsForm();
            _toolsWindow.FormClosed += OnToolsClosed;

            Load += TestForm_Load;
            FormClosing += TestForm_FormClosing;
        }

        private async void TestForm_Load(object sender, EventArgs e)
        {
            _toolsWindow.Show(this);
            PositionToolsWindowBesideMain();
            await NavigationService.GoHomeAsync();
        }

        private void PositionToolsWindowBesideMain()
        {
            var screen = Screen.FromControl(this).WorkingArea;
            int desiredLeft = Right + 8;
            if (desiredLeft + _toolsWindow.Width > screen.Right)
                desiredLeft = screen.Right - _toolsWindow.Width;

            _toolsWindow.StartPosition = FormStartPosition.Manual;
            _toolsWindow.Location = new Point(desiredLeft, Top);
        }

        private void OnToolsClosed(object sender, FormClosedEventArgs e)
        {
            if (!_closingFromShutdown)
                Close();
        }

        private async void TestForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closingFromShutdown) return;

            _closingFromShutdown = true;
            e.Cancel = true;

            try
            {
                await NavigationService.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[TestForm] Shutdown error: " + ex);
            }

            if (_toolsWindow != null && !_toolsWindow.IsDisposed)
                _toolsWindow.Close();

            BeginInvoke((Action)Close);
        }
    }
}

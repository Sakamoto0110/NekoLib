using System;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.WinForms.Adapters;
using HomeView = NavigationDemo.Pages.Home.HomePage;

namespace NavigationDemo
{
    /// <summary>
    /// Primary test window — hosts the page graph (HOME → A → {B, C, D, E, F},
    /// D → F, E → F) and owns the secondary <see cref="TestToolsForm"/>.
    /// Demo entry point (replaces the deleted legacy `Form1`/`ShellForm`).
    /// </summary>
    public class TestForm : Form
    {
        private readonly TestToolsForm _toolsWindow;
        private readonly Panel _hostPanel;
        private bool _closingFromShutdown;

        public TestForm()
        {
            Text = "Navigation demo — primary";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            _hostPanel = new Panel
            {
                Name = "TestHostPanel",
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            Controls.Add(_hostPanel);

            // Bootstrap the navigation framework against the host panel.
            var logger = new Logger(LogLevel.Debug, new DebugLogSink());
            var memory = new MemoryTelemetrySink();
            var diagnostics = new Diagnostics(logger, memory);

            var ctx = PageNavBootstrap
                .Use<WinFormsPlatformAdapter>(_hostPanel)
                .RegisterPagesFromAssembly(typeof(TestForm).Assembly)
                .UseDiagnostics(diagnostics)
                .ConfigurePages(cfg => { cfg.Page<HomeView>().AsHome(); })
                .Start();

            NavigationService.UseContext(ctx);

            // Secondary tools window — opens alongside the primary.
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
            // Snap the tools window to the right of the primary window.
            var screen = Screen.FromControl(this).WorkingArea;
            int desiredLeft = Right + 8;
            if (desiredLeft + _toolsWindow.Width > screen.Right)
                desiredLeft = screen.Right - _toolsWindow.Width;

            _toolsWindow.StartPosition = FormStartPosition.Manual;
            _toolsWindow.Location = new Point(desiredLeft, Top);
        }

        private void OnToolsClosed(object sender, FormClosedEventArgs e)
        {
            // If the user closed the tools window, also close the primary.
            if (!_closingFromShutdown)
                Close();
        }

        private async void TestForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // First close: tear the framework down cleanly, then re-close.
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

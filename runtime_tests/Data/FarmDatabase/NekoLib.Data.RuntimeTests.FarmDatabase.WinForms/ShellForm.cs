using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    /// <summary>
    /// Application shell: a fixed left rail plus the navigation host, with a SQL
    /// console pinned underneath.
    /// <para/>
    /// The rail and the console sit OUTSIDE the navigation host, so they keep working
    /// while a modal prompt blocks the host - which is what makes the animal-removal
    /// prompt observable rather than a freeze.
    /// </summary>
    public partial class ShellForm : Form
    {
        private readonly List<SidebarButton> _navButtons = new List<SidebarButton>();
        private bool _navigationStarted;

        public ShellForm()
        {
            InitializeComponent();

            _navConnection.PageType = typeof(ConnectionPage);
            _navBrowse.PageType = typeof(BrowsePage);
            _navRawQuery.PageType = typeof(RawQueryPage);
            _navStock.PageType = typeof(StockPage);
            _navLog.PageType = typeof(LogPage);
            _navSimulation.PageType = typeof(SimulationPage);

            _navButtons.AddRange(new[]
            {
                _navConnection, _navBrowse, _navRawQuery, _navStock, _navLog, _navSimulation
            });

            foreach (SidebarButton button in _navButtons)
                button.Click += OnNavButtonClick;

            _consoleClear.Click += OnClearConsole;

            // The host must exist as a realized window before the runtime marshals
            // anything onto it, so bootstrap waits for Load rather than running here.
            Load += OnShellLoad;
            FormClosing += OnShellClosing;
        }

        // -----------------------------------------------------------------
        // Startup / teardown
        // -----------------------------------------------------------------

        private void OnShellLoad(object sender, EventArgs e)
        {
            FarmWorkspace workspace = AppServices.Workspace;
            workspace.ConnectionChanged += OnConnectionChanged;
            workspace.SqlTraced += OnSqlTraced;

            StartNavigation();
            UpdateConnectionIndicator();

            _ = NavigationService.SwitchPage<ConnectionPage>();
        }

        /// <summary>
        /// Every page is registered by attribute. There is deliberately no
        /// <c>ConfigurePages</c> call: role, presentation, reuse policy and load mode
        /// all come from the attributes on the page classes themselves, so this
        /// method never has to learn that a new page exists.
        /// </summary>
        private void StartNavigation()
        {
            PageNavBootstrap
                .Use<WinFormsPlatformAdapter>(_hostPanel)
                .RegisterPagesFromAssembly(typeof(ShellForm).Assembly)
                .Start();

            _navigationStarted = true;

            NavigationService.CurrentChanged += OnCurrentPageChanged;
            NavigationService.NavigationFailed += OnNavigationFailed;
        }

        private void OnShellClosing(object sender, FormClosingEventArgs e)
        {
            FarmWorkspace workspace = AppServices.IsRunning ? AppServices.Workspace : null;
            if (workspace != null)
            {
                workspace.ConnectionChanged -= OnConnectionChanged;
                workspace.SqlTraced -= OnSqlTraced;
            }

            if (!_navigationStarted) return;

            // Shutdown is awaited by the runtime on the UI thread; FormClosing still
            // has a live message pump, which is why teardown happens here and not in
            // FormClosed.
            try { _ = NavigationService.Shutdown(); }
            catch (Exception ex) { Trace("shutdown falhou: " + ex.Message); }
        }

        // -----------------------------------------------------------------
        // Navigation
        // -----------------------------------------------------------------

        private void OnNavButtonClick(object sender, EventArgs e)
        {
            var button = (SidebarButton)sender;
            if (button.PageType == null) return;

            _ = NavigationService.SwitchPage(button.PageType);
        }

        private void OnCurrentPageChanged(IPageView current)
        {
            if (IsDisposed || !IsHandleCreated) return;

            if (InvokeRequired)
            {
                BeginInvoke((Action<IPageView>)OnCurrentPageChanged, current);
                return;
            }

            Type currentType = current?.GetType();
            foreach (SidebarButton button in _navButtons)
                button.Active = button.PageType == currentType;
        }

        private void OnNavigationFailed(IPageView from, Type toType, Exception error)
        {
            Trace("navegação falhou -> " + toType.Name + ": " + error.Message);
        }

        // -----------------------------------------------------------------
        // Connection indicator
        // -----------------------------------------------------------------

        private void OnConnectionChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)UpdateConnectionIndicator);
                return;
            }

            UpdateConnectionIndicator();
        }

        private void UpdateConnectionIndicator()
        {
            FarmWorkspace workspace = AppServices.Workspace;

            if (!workspace.IsConnected)
            {
                _connectionPill.Text = "desconectado";
                _connectionPill.Tone = FarmTheme.TextFaint;
                _connectionPath.Text = string.Empty;
                return;
            }

            FarmDb db = workspace.Require();
            _connectionPill.Text = db.Profile.DisplayName;
            _connectionPill.Tone = FarmTheme.Accent;
            _connectionPath.Text = db.Profile.DatabasePath;
        }

        // -----------------------------------------------------------------
        // SQL console
        // -----------------------------------------------------------------

        private void OnSqlTraced(string line)
        {
            if (IsDisposed || !IsHandleCreated) return;

            if (InvokeRequired)
            {
                BeginInvoke((Action<string>)Trace, line);
                return;
            }

            Trace(line);
        }

        private void Trace(string line)
        {
            if (IsDisposed || _sqlTrace.IsDisposed) return;

            _sqlTrace.Items.Add(line);

            // Keep the newest line visible without stealing focus.
            _sqlTrace.TopIndex = Math.Max(0, _sqlTrace.Items.Count - VisibleTraceLines());

            if (_sqlTrace.Items.Count > 500)
                _sqlTrace.Items.RemoveAt(0);
        }

        private int VisibleTraceLines()
        {
            int itemHeight = Math.Max(1, _sqlTrace.ItemHeight);
            return Math.Max(1, _sqlTrace.ClientSize.Height / itemHeight);
        }

        private void OnClearConsole(object sender, EventArgs e)
        {
            _sqlTrace.Items.Clear();
            AppServices.Workspace.ClearTrace();
        }
    }
}

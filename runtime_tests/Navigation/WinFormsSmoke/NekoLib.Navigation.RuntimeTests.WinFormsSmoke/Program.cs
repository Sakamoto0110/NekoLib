using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Adapters;

namespace NekoLib.Navigation.RuntimeTests.WinFormsSmoke
{
    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    /// <summary>
    /// WinForms counterpart of the WPF smoke window: same controls, same labels and
    /// the same navigation host layout, so the two platforms can be compared step by
    /// step. The left column sits OUTSIDE the navigation host, so its buttons stay
    /// interactive while a modal blocks the host — and, deliberately, clicks there do
    /// not reset the idle timer, because the interaction observer only watches the
    /// host subtree.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const int LeftColumnWidth = 340;

        private readonly Panel _host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Gainsboro };
        private readonly ListBox _log = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Font = new Font("Consolas", 8.25F)
        };

        public MainForm()
        {
            Text = "NekoLib.Navigation — WinForms smoke test";
            ClientSize = new Size(1040, 800);
            StartPosition = FormStartPosition.CenterScreen;

            Controls.Add(BuildLayout());

            // The host must be realized before Navigation marshals anything to it, so
            // bootstrap runs on Load rather than in the constructor.
            Load += (_, __) =>
            {
                BuildNavigation();
                _ = NavigationService.GoIdleAsync();
            };

            // FormClosing keeps the message pump alive while the runtime tears down.
            FormClosing += (_, __) => { try { _ = NavigationService.Shutdown(); } catch { } };
        }

        // -----------------------------------------------------------------
        // Layout: left = controls + log, right = navigation host
        // -----------------------------------------------------------------
        private Control BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(10)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftColumnWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // --- left column: buttons (auto) + log (fill) ---
            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 10, 0)
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            buttons.Controls.Add(Header("Navegação"));
            buttons.Controls.Add(Btn("Ir: Dashboard", async (_, __) => await Try(() => NavigationService.SwitchPage<DashboardPage>())));
            buttons.Controls.Add(Btn("Ir: Idle", async (_, __) => await Try(() => NavigationService.GoIdleAsync())));
            buttons.Controls.Add(Btn("Voltar (Back)", async (_, __) => await Try(async () => Log("GoBack -> " + await NavigationService.GoBackAsync()))));

            buttons.Controls.Add(Header("Overlays (caixas, não tela cheia)"));
            buttons.Controls.Add(Btn("Dialog (modal, bool)", async (_, __) => await Try(async () => Log("Dialog -> " + await NavigationService.ShowDialogAsync<SampleDialog>()))));
            buttons.Controls.Add(Btn("Prompt (modal, texto)", async (_, __) => await Try(async () =>
            {
                var result = await NavigationService.ShowPromptAsync<SamplePrompt, string>();
                Log("Prompt -> " + (result == null ? "(cancelado)" : "\"" + result + "\""));
            })));
            buttons.Controls.Add(Btn("Toast (bottom-right, 3s)", (_, __) => Run(() => { Log("Toast"); NavigationService.ShowToast<SampleToast>(durationMs: 3000); })));
            buttons.Controls.Add(Btn("Popover (top-left)", async (_, __) => await Try(async () => Log("Popover -> " + await NavigationService.ShowPopoverAsync<SamplePopover>()))));

            buttons.Controls.Add(Header("Sessão / guards"));
            buttons.Controls.Add(Btn("SignIn(\"admin\")", (_, __) => Run(() => { NavigationService.Session.SignIn("admin"); Log("SignIn(admin) — auth=" + NavigationService.Session.IsAuthenticated); })));
            buttons.Controls.Add(Btn("SignOut", (_, __) => Run(() => { NavigationService.Session.SignOut(); Log("SignOut — auth=" + NavigationService.Session.IsAuthenticated); })));

            // Reset keeps the context alive; Shutdown unmounts the facade and Start
            // remounts a fresh one, which is how repeated mount/shutdown is exercised.
            buttons.Controls.Add(Header("Ciclo de vida"));
            buttons.Controls.Add(Btn("Reset (ResetAsync)", async (_, __) => await Try(async () =>
            {
                // ResetAsync deliberately does not navigate: it tears the shell down
                // and leaves the context alive so the application decides what comes
                // next. Going to Idle is what a real shell would do, and it keeps the
                // button from looking like a dead end.
                await NavigationService.ResetAsync();
                Log("ResetAsync concluído — indo para Idle");
                await NavigationService.GoIdleAsync();
            })));
            buttons.Controls.Add(Btn("Shutdown", async (_, __) => await Try(async () =>
            {
                await NavigationService.Shutdown();
                Log("Shutdown concluído — use Start para remontar");
            })));
            buttons.Controls.Add(Btn("Start (re-bootstrap)", (_, __) => Run(() =>
            {
                BuildNavigation();
                _ = NavigationService.GoIdleAsync();
            })));

            buttons.Controls.Add(Header("Log"));
            buttons.Controls.Add(Btn("Limpar log", (_, __) => _log.Items.Clear()));

            left.Controls.Add(buttons, 0, 0);

            var logBox = new GroupBox
            {
                Text = "Eventos",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(6)
            };
            logBox.Controls.Add(_log);
            left.Controls.Add(logBox, 0, 1);

            root.Controls.Add(left, 0, 0);

            // --- right column: navigation host, framed like the WPF scenario ---
            var hostFrame = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = Color.DarkGray,
                Margin = new Padding(0)
            };
            hostFrame.Controls.Add(_host);
            root.Controls.Add(hostFrame, 1, 0);

            return root;
        }

        // -----------------------------------------------------------------
        // Bootstrap + event logging
        // -----------------------------------------------------------------
        private void BuildNavigation()
        {
            PageNavBootstrap
                .Use<WinFormsPlatformAdapter>(_host)
                .RegisterPagesFromAssembly(typeof(MainForm).Assembly)
                .SetIdle<IdlePage>()                              // idle role + [PageTimeout(20)] on the page
                .ConfigurePages(cfg => cfg.Page<DashboardPage>().StrongSingleton())
                .Start();

            NavigationService.Navigating += (from, toType, _) => Log("Navigating:     " + PageName(from) + " -> " + toType.Name);
            NavigationService.Navigated += (from, to, _) => Log("Navigated:      " + PageName(from) + " -> " + PageName(to));
            NavigationService.NavigationFailed += (_, toType, ex) => Log("NavFailed:      " + toType.Name + " — " + ex.Message);
            NavigationService.CurrentChanged += cur => Log("CurrentChanged: " + PageName(cur));
            NavigationService.HistoryChanged += () => Log("HistoryChanged: CanGoBack=" + NavigationService.CanGoBack);
            NavigationService.Events.NavigationLogged += e => Log("   · diag: " + e);
            NavigationService.Events.GuardDenied += e => Log("   · guard denied: " + e);

            Log("Bootstrap pronto. Idle timeout = 20s (via [PageTimeout(20)] na IdlePage).");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private static string PageName(IPageView page) => page?.Name ?? "—";

        // Every handler is guarded: after Shutdown the facade is unmounted, so any
        // navigation, surface, or session call throws until Start remounts it.
        private async Task Try(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Log("ERRO: " + ex.Message); }
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { Log("ERRO: " + ex.Message); }
        }

        private void Log(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;

            if (!_log.IsHandleCreated || !_log.InvokeRequired)
                InsertLogLine(line);
            else
                _log.BeginInvoke((Action)(() => InsertLogLine(line)));
        }

        private void InsertLogLine(string line)
        {
            if (_log.IsDisposed)
                return;

            _log.Items.Insert(0, line);
        }

        private static Button Btn(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = LeftColumnWidth - 12,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 0, 5),
                UseVisualStyleBackColor = true
            };
            button.Click += onClick;
            return button;
        }

        private static Label Header(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 4)
        };
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Wpf.Adapters;

namespace NekoLib.Navigation.RuntimeTests.WpfSmoke
{
    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }

    internal sealed class MainWindow : Window
    {
        private readonly Grid _host = new Grid { ClipToBounds = true };
        private readonly ListBox _log = new ListBox { FontFamily = new FontFamily("Consolas"), FontSize = 11 };

        public MainWindow()
        {
            Title = "NekoLib.Navigation — WPF smoke test";
            Width = 1040;
            Height = 840;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = BuildLayout();
            BuildNavigation();

            Loaded += (_, __) => _ = NavigationService.GoIdleAsync();
            Closed += (_, __) => { try { _ = NavigationService.Shutdown(); } catch { } };
        }

        // -----------------------------------------------------------------
        // Layout: left = controls + log, right = navigation host
        // -----------------------------------------------------------------
        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(10) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // --- left column: buttons (top) + log (fill) ---
            var left = new Grid { Margin = new Thickness(0, 0, 10, 0) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var buttons = new StackPanel();
            buttons.Children.Add(Header("Navegação"));
            buttons.Children.Add(Btn("Ir: Dashboard", async (_, __) => await Try(() => NavigationService.SwitchPage<DashboardPage>())));
            buttons.Children.Add(Btn("Ir: Idle", async (_, __) => await Try(() => NavigationService.GoIdleAsync())));
            buttons.Children.Add(Btn("Voltar (Back)", async (_, __) => await Try(async () => Log("GoBack -> " + await NavigationService.GoBackAsync()))));

            buttons.Children.Add(Header("Overlays (caixas, não tela cheia)"));
            buttons.Children.Add(Btn("Dialog (modal, bool)", async (_, __) => await Try(async () => Log("Dialog -> " + await NavigationService.ShowDialogAsync<SampleDialog>()))));
            buttons.Children.Add(Btn("Prompt (modal, texto)", async (_, __) => await Try(async () =>
            {
                var r = await NavigationService.ShowPromptAsync<SamplePrompt, string>();
                Log("Prompt -> " + (r == null ? "(cancelado)" : "\"" + r + "\""));
            })));
            buttons.Children.Add(Btn("Toast (bottom-right, 3s)", (_, __) => Run(() => { Log("Toast"); NavigationService.ShowToast<SampleToast>(durationMs: 3000); })));
            buttons.Children.Add(Btn("Popover (top-left)", async (_, __) => await Try(async () => Log("Popover -> " + await NavigationService.ShowPopoverAsync<SamplePopover>()))));

            buttons.Children.Add(Header("Sessão / guards"));
            buttons.Children.Add(Btn("SignIn(\"admin\")", (_, __) => Run(() => { NavigationService.Session.SignIn("admin"); Log("SignIn(admin) — auth=" + NavigationService.Session.IsAuthenticated); })));
            buttons.Children.Add(Btn("SignOut", (_, __) => Run(() => { NavigationService.Session.SignOut(); Log("SignOut — auth=" + NavigationService.Session.IsAuthenticated); })));

            // Reset keeps the context alive; Shutdown unmounts the facade and Start
            // remounts a fresh one, which is how repeated mount/shutdown is exercised.
            buttons.Children.Add(Header("Ciclo de vida"));
            buttons.Children.Add(Btn("Reset (ResetAsync)", async (_, __) => await Try(async () =>
            {
                // ResetAsync deliberately does not navigate: it tears the shell down
                // and leaves the context alive so the application decides what comes
                // next. Going to Idle is what a real shell would do, and it keeps the
                // button from looking like a dead end.
                await NavigationService.ResetAsync();
                Log("ResetAsync concluído — indo para Idle");
                await NavigationService.GoIdleAsync();
            })));
            buttons.Children.Add(Btn("Shutdown", async (_, __) => await Try(async () =>
            {
                await NavigationService.Shutdown();
                Log("Shutdown concluído — use Start para remontar");
            })));
            buttons.Children.Add(Btn("Start (re-bootstrap)", (_, __) => Run(() =>
            {
                BuildNavigation();
                _ = NavigationService.GoIdleAsync();
            })));

            buttons.Children.Add(Header("Log"));
            buttons.Children.Add(Btn("Limpar log", (_, __) => _log.Items.Clear()));
            Grid.SetRow(buttons, 0);
            left.Children.Add(buttons);

            var logBox = new GroupBox { Header = "Eventos", Margin = new Thickness(0, 8, 0, 0), Content = _log };
            Grid.SetRow(logBox, 1);
            left.Children.Add(logBox);

            Grid.SetColumn(left, 0);
            root.Children.Add(left);

            // --- right column: navigation host ---
            _host.Background = Brushes.Gainsboro;
            var hostFrame = new Border
            {
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
                Child = _host
            };
            Grid.SetColumn(hostFrame, 1);
            root.Children.Add(hostFrame);

            return root;
        }

        // -----------------------------------------------------------------
        // Bootstrap + event logging (the "lots of feedback" part)
        // -----------------------------------------------------------------
        private void BuildNavigation()
        {
            PageNavBootstrap
                .Use<WpfPlatformAdapter>(_host)
                .RegisterPagesFromAssembly(typeof(MainWindow).Assembly)
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
        private static string PageName(IPageView p) => p?.Name ?? "—";

        // Every handler is guarded: after Shutdown the facade is unmounted, so any
        // navigation, surface, or session call throws until Start remounts it.
        private async System.Threading.Tasks.Task Try(Func<System.Threading.Tasks.Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Log("ERRO: " + ex.Message); }
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { Log("ERRO: " + ex.Message); }
        }

        private void Log(string msg)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
            if (_log.Dispatcher.CheckAccess())
                _log.Items.Insert(0, line);
            else
                _log.Dispatcher.BeginInvoke(new Action(() => _log.Items.Insert(0, line)));
        }

        private static Button Btn(string text, RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(8, 7, 8, 7),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            b.Click += onClick;
            return b;
        }

        private static TextBlock Header(string text) => new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 10, 0, 4)
        };
    }
}

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NekoLib.Diagnostics.Windows;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Wpf.Adapters;
using NekoLib.Navigation.Wpf.Hosting;
using NekoLib.Navigation.Wpf.Toolkit;

namespace NekoLib.PackageConsumers.Wpf9
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--termination-helper") return RecoveryProbe.TerminationHelper();
                if (args.Length == 2 && args[0] == "--control") return RecoveryProbe.Control(args[1]);
                if (args.Length == 3 && args[0] == "--recovery") return RunRecovery(args);
                ModuleProbe.RunAsync().GetAwaiter().GetResult();
                return RunUi();
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }

        private static int RunRecovery(string[] args)
        {
            using (var probe = new RecoveryProbe(args))
            {
                var app = new Application();
                using (WindowsCrash.HookWpf(app.Dispatcher, probe.Mode == "recoverable"))
                {
                    var window = NewWindow(new Grid());
                    if (probe.Mode == "restart" || probe.Mode == "clean-stop")
                        window.Closing += (_, e) => e.Cancel = true;
                    window.Loaded += (_, __) => probe.Shown(action => window.Dispatcher.BeginInvoke(action), app.Windows.Count);
                    return app.Run(window);
                }
            }
        }

        private static int RunUi()
        {
            var app = new Application();
            var host = new Grid();
            var window = NewWindow(host);
            var exit = 0;
            window.Loaded += async (_, __) =>
            {
                using (var activity = new WpfInteractionObserver(host, InteractionObservationMode.InputOnly))
                {
                    try
                    {
                        var owned = new OwnedObserver(activity);
                        var uiThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                        PageNavBootstrap.Use<WpfPlatformAdapter>(host)
                            .SetIdle<IdlePage>()
                            .ConfigurePages((PageBuilderConfigurator b) => b.Page<WorkPage>().Named("Work"))
                            .UseInteractionObserver(() => owned)
                            .Start();
                        await NavigationService.GoIdleAsync();
                        var result = await NavigationService.SwitchPage<WorkPage>();
                        ModuleProbe.Require(result.Succeeded && NavigationService.Current is WorkPage, "WPF navigates between native pages");
                        var page = (WorkPage)NavigationService.Current!;
                        var changes = 0;
                        activity.InteractionDetected += () => changes++;
                        page.Input.Text = "background update";
                        ModuleProbe.Require(changes == 0, "WPF input-only mode excludes background updates");

                        await Task.Run(() => new WpfNavigationToolkit(page.Input).FocusSurface());
                        await Task.Delay(30);
                        ModuleProbe.Require(page.Input.IsKeyboardFocused, "WPF worker focus reaches the UI owner");
                        NavigationService.ShowToast<ProbeToast>(durationMs: 100);
                        await Task.Delay(30);
                        host.UpdateLayout();
                        var toast = ProbeToast.Last!;
                        ModuleProbe.Require(toast.ActualWidth < host.ActualWidth && toast.HorizontalAlignment == HorizontalAlignment.Right && page.Input.IsKeyboardFocused, "WPF toast is anchored without stealing focus");
                        var position = toast.TranslatePoint(new Point(), host);
                        ModuleProbe.Require(Math.Abs(position.X + toast.ActualWidth + 20 - host.ActualWidth) < 2 && Math.Abs(position.Y + toast.ActualHeight + 20 - host.ActualHeight) < 2, "WPF toast uses a 20-DIP bottom-right inset");
                        await Task.Delay(180);
                        ModuleProbe.Require(toast.IsDisposed && !host.Children.Contains(toast), "WPF toast auto-dismisses and detaches");

                        var dialogTask = NavigationService.ShowDialogAsync<ProbeDialog>();
                        await Task.Delay(30);
                        ModuleProbe.Require(!page.IsEnabled && ProbeDialog.Last!.IsEnabled, "WPF modal blocks the background and enables its surface");
                        page.Model.Enabled = false;
                        page.Model.Enabled = true;
                        ModuleProbe.Require(!page.IsEnabled, "WPF binding refresh cannot re-enable a modal background");
                        page.Model.Enabled = false;
                        ProbeDialog.Last!.Finish();
                        ModuleProbe.Require(await dialogTask, "WPF dialog completes its shared result contract");
                        ModuleProbe.Require(!page.IsEnabled && BindingOperations.IsDataBound(page, UIElement.IsEnabledProperty), "WPF modal restores the current binding source");
                        page.Model.Enabled = true;
                        await Task.Delay(20);
                        ModuleProbe.Require(page.IsEnabled, "WPF binding remains live after modal cleanup");
                        ModuleProbe.Require(await NavigationService.ShowPromptAsync<ProbePrompt, string>() == "accepted", "WPF prompt returns its typed result");
                        ModuleProbe.Require(await NavigationService.ShowPopoverAsync<ProbePopover>(), "WPF popover completes and cleans up");
                        var autoPopover = NavigationService.ShowPopoverAsync<ProbeAutoPopover>();
                        await Task.Delay(30);
                        page.Input.Focus();
                        ModuleProbe.Require(!await ModuleProbe.Within(autoPopover), "WPF popover auto-dismisses when focus leaves its subtree");
                        await NavigationService.ResetAsync();
                        await NavigationService.Shutdown();
                        ModuleProbe.Require(owned.DisposedOnThread == uiThread, "WPF shutdown disposes its native observer on the UI owner");
                        ModuleProbe.Require(host.Children.Count == 0 && page.IsDisposed, "WPF awaited shutdown releases pages and overlays");
                    }
                    catch (Exception e) { Console.Error.WriteLine(e); exit = 1; }
                    finally
                    {
                        try { await NavigationService.Shutdown(); } catch (Exception e) { Console.Error.WriteLine(e); exit = 1; }
                    }
                }
                window.Close();
            };
            app.Run(window);
            return exit;
        }

        private static Window NewWindow(UIElement content) => new Window
        { Title = "NekoLib package qualification", Content = content, Width = 640, Height = 480, Left = -32000, Top = 0, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
    }

    public sealed class IdlePage : PageView { }
    public sealed class WorkPage : PageView
    {
        internal readonly TextBox Input = new TextBox();
        internal readonly EnabledModel Model = new EnabledModel();
        public WorkPage()
        {
            Content = Input;
            SetBinding(IsEnabledProperty, new Binding(nameof(EnabledModel.Enabled)) { Source = Model });
        }
    }
    public sealed class EnabledModel : INotifyPropertyChanged
    {
        private bool _enabled = true;
        public bool Enabled { get => _enabled; set { _enabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public sealed class ProbeToast : ToastViewBase
    {
        internal static ProbeToast? Last;
        public ProbeToast() { Last = this; Width = 180; Height = 60; Content = new TextBlock { Text = "qualification" }; }
    }
    public sealed class ProbeDialog : DialogViewBase
    {
        internal static ProbeDialog? Last;
        public ProbeDialog() { Last = this; Width = 220; Height = 100; Content = new Button { Content = "Confirm" }; }
        internal void Finish() => Confirm();
    }
    public sealed class ProbePrompt : PromptViewBase<string>
    {
        public ProbePrompt() { Width = 220; Height = 100; }
        protected override Task OnShownAsync(object? payload) { Dispatcher.BeginInvoke((Action)(() => CompletePrompt("accepted"))); return Task.CompletedTask; }
    }
    public sealed class ProbePopover : PopoverViewBase
    {
        public ProbePopover() { Width = 120; Height = 80; }
        protected override Task OnShownAsync(object? payload) { Dispatcher.BeginInvoke((Action)(() => Complete(true))); return Task.CompletedTask; }
    }
    public sealed class ProbeAutoPopover : AutoDismissPopoverBase
    {
        public ProbeAutoPopover() { Width = 120; Height = 80; Content = new TextBox(); }
    }
}

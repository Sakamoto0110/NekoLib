using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Diagnostics;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.WinForms.Hosting;
using NekoLib.Navigation.WinForms.Toolkit;

namespace NekoLib.PackageConsumers
{
    internal static class WinFormsUiProbe
    {
        internal static int Recovery(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            using (var probe = new RecoveryProbe(args))
            using (var form = NewForm())
            {
                ThreadExceptionEventHandler onException = (_, e) => CrashHandler.ReportExternalCrash("Application.ThreadException", e.Exception, probe.Mode != "recoverable");
                Application.ThreadException += onException;
                try
                {
                    if (probe.Mode == "restart" || probe.Mode == "clean-stop")
                        form.FormClosing += (_, e) => e.Cancel = true;
                    form.Shown += (_, __) => probe.Shown(action => form.BeginInvoke(action), Application.OpenForms.Count);
                    Application.Run(form);
                    return 0;
                }
                finally { Application.ThreadException -= onException; }
            }
        }

        internal static int Run()
        {
            var exit = 0;
            using (var form = NewForm())
            using (var host = new Panel { Dock = DockStyle.Fill })
            using (var activity = new WinFormsInteractionObserver(host, InteractionObservationMode.InputOnly))
            {
                form.Controls.Add(host);
                form.Shown += async (_, __) =>
                {
                    try
                    {
                        var owned = new OwnedObserver(activity);
                        var uiThread = Thread.CurrentThread.ManagedThreadId;
                        PageNavBootstrap.Use<WinFormsPlatformAdapter>(host)
                            .SetIdle<FormsIdlePage>()
                            .ConfigurePages((PageBuilderConfigurator b) => b.Page<FormsWorkPage>().Named("Work"))
                            .UseInteractionObserver(() => owned)
                            .Start();
                        await NavigationService.GoIdleAsync();
                        var result = await NavigationService.SwitchPage<FormsWorkPage>();
                        ModuleProbe.Require(result.Succeeded && NavigationService.Current is FormsWorkPage, "WinForms navigates between native pages");
                        var page = (FormsWorkPage)NavigationService.Current!;
                        var changes = 0;
                        activity.InteractionDetected += () => changes++;
                        page.Input.Text = "background update";
                        ModuleProbe.Require(changes == 0, "WinForms input-only mode excludes background updates");
                        await Task.Run(() => new WinFormsNavigationToolkit(page.Input).FocusSurface());
                        await Task.Delay(30);
                        ModuleProbe.Require(page.Input.Focused, "WinForms worker focus reaches the UI owner");
                        NavigationService.ShowToast<FormsToast>(durationMs: 100);
                        await Task.Delay(30);
                        var toast = FormsToast.Last!;
                        ModuleProbe.Require(toast.Width < host.Width && toast.Dock == DockStyle.None && page.Input.Focused, "WinForms toast is anchored without stealing focus");
                        var inset = (int)Math.Round(20 * new WinFormsNavigationSurface(host).Scale);
                        ModuleProbe.Require(toast.Left + toast.Width + inset == host.ClientSize.Width && toast.Top + toast.Height + inset == host.ClientSize.Height, "WinForms toast scales its bottom-right inset for native DPI");
                        await Task.Delay(180);
                        ModuleProbe.Require(toast.IsDisposed && !host.Controls.Contains(toast), "WinForms toast auto-dismisses and detaches");
                        var dialogTask = NavigationService.ShowDialogAsync<FormsDialog>();
                        await Task.Delay(30);
                        ModuleProbe.Require(!page.Enabled && FormsDialog.Last!.Enabled, "WinForms modal blocks the background and enables its surface");
                        ModuleProbe.Require(page.EnabledBinding.IsBinding, "WinForms enabled-state binding is live");
                        page.Model.Enabled = false;
                        page.EnabledBinding.ReadValue();
                        page.Model.Enabled = true;
                        page.EnabledBinding.ReadValue();
                        ModuleProbe.Require(!page.Enabled, "WinForms binding refresh cannot re-enable a modal background");
                        page.Model.Enabled = false;
                        page.EnabledBinding.ReadValue();
                        FormsDialog.Last!.Finish();
                        ModuleProbe.Require(await dialogTask && !page.Enabled, "WinForms modal restores the current enabled-state binding");
                        page.Model.Enabled = true;
                        page.EnabledBinding.ReadValue();
                        ModuleProbe.Require(page.Enabled, "WinForms enabled-state binding remains live after modal cleanup");
                        ModuleProbe.Require(await NavigationService.ShowPromptAsync<FormsPrompt, string>() == "accepted", "WinForms prompt returns its typed result");
                        ModuleProbe.Require(await NavigationService.ShowPopoverAsync<FormsPopover>(), "WinForms popover completes and cleans up");
                        var autoPopover = NavigationService.ShowPopoverAsync<FormsAutoPopover>();
                        await Task.Delay(30);
                        page.Input.Focus();
                        ModuleProbe.Require(!await ModuleProbe.Within(autoPopover), "WinForms popover auto-dismisses when focus leaves its subtree");
                        await NavigationService.ResetAsync();
                        await NavigationService.Shutdown();
                        ModuleProbe.Require(owned.DisposedOnThread == uiThread, "WinForms shutdown disposes its native observer on the UI owner");
                        ModuleProbe.Require(host.Controls.Count == 0 && page.IsDisposed, "WinForms awaited shutdown releases pages and overlays");
                    }
                    catch (Exception e) { Console.Error.WriteLine(e); exit = 1; }
                    finally
                    {
                        try { await NavigationService.Shutdown(); } catch (Exception e) { Console.Error.WriteLine(e); exit = 1; }
                    }
                    form.Close();
                };
                Application.Run(form);
            }
            return exit;
        }

        private static Form NewForm() => new Form
        { Text = "NekoLib package qualification", Size = new Size(640, 480), Location = new Point(-32000, 0), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
    }

    public sealed class FormsIdlePage : PageView { }
    public sealed class FormsWorkPage : PageView
    {
        internal readonly TextBox Input = new TextBox { Dock = DockStyle.Top };
        internal readonly FormsEnabledModel Model = new FormsEnabledModel();
        internal readonly Binding EnabledBinding;
        public FormsWorkPage()
        {
            Controls.Add(Input);
            EnabledBinding = DataBindings.Add(nameof(Enabled), Model, nameof(Model.Enabled), false, DataSourceUpdateMode.Never);
        }
    }
    public sealed class FormsEnabledModel : NekoLib.Mvvm.ViewModelBase
    {
        private bool _enabled = true;
        public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    }
    public sealed class FormsToast : ToastViewBase
    {
        internal static FormsToast? Last;
        public FormsToast() { Last = this; Size = new Size(180, 60); Controls.Add(new Label { Text = "qualification" }); }
    }
    public sealed class FormsDialog : DialogViewBase
    {
        internal static FormsDialog? Last;
        public FormsDialog() { Last = this; Size = new Size(220, 100); Controls.Add(new Button { Text = "Confirm" }); }
        internal void Finish() => Confirm();
    }
    public sealed class FormsPrompt : PromptViewBase<string>
    {
        public FormsPrompt() { Size = new Size(220, 100); }
        protected override Task OnShownAsync(object? payload) { BeginInvoke((Action)(() => CompletePrompt("accepted"))); return Task.CompletedTask; }
    }
    public sealed class FormsPopover : PopoverViewBase
    {
        public FormsPopover() { Size = new Size(120, 80); }
        protected override Task OnShownAsync(object? payload) { BeginInvoke((Action)(() => Complete(true))); return Task.CompletedTask; }
    }
    public sealed class FormsAutoPopover : AutoDismissPopoverBase
    {
        public FormsAutoPopover() { Size = new Size(120, 80); Controls.Add(new TextBox()); }
    }
}

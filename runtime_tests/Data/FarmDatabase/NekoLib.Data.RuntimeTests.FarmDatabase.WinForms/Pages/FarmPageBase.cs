using System;
using System.ComponentModel;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels;
using NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Theme;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    /// <summary>
    /// Shared base for the scenario's pages.
    /// <para/>
    /// Deliberately concrete and non-generic. The WinForms designer instantiates the
    /// base class of whatever it opens, so an abstract or generic base would make
    /// every page in this app undesignable - which is exactly what happens with the
    /// overlay bases, see <c>Overlays/ReasonPromptBase</c>.
    /// </summary>
    // No [DesignerCategory("Code")] here, deliberately: that attribute tells Visual
    // Studio the type is code-only and makes it open the editor instead of the design
    // surface. It belongs on the custom-painted controls in Theme/, not on pages.
    public class FarmPageBase : PageView
    {
        private FarmViewModelBase _boundViewModel;
        private StatusLine _boundStatus;

        public FarmPageBase()
        {
            BackColor = FarmTheme.Canvas;
            ForeColor = FarmTheme.TextPrimary;
            Font = FarmTheme.FontBody;
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// True whenever the page must not touch application state: inside the
        /// designer, or before <c>AppServices.Start()</c> has run.
        /// </summary>
        protected bool IsInert => DesignMode || !AppServices.IsRunning;

        /// <summary>
        /// Repaints the whole page, children included, after every layout pass.
        /// <para/>
        /// Without this, maximizing the shell produced controls that were visible in
        /// one place while actually living in another: the layout moved them, but the
        /// custom double-buffered surfaces they sit on never invalidated the region
        /// they vacated, so the old pixels stayed on screen. A button drawn at the
        /// stale position looks perfectly normal and silently swallows every click,
        /// because the real control is elsewhere.
        /// </summary>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Invalidate(true);
        }

        /// <summary>The shared view-model bundle. Never call this at design time.</summary>
        protected ViewModels App => AppServices.ViewModelsBundle;

        /// <summary>
        /// Mirrors a view-model's busy/status/error state onto the page footer and
        /// calls <paramref name="onChanged"/> for every property change so the page
        /// can refresh whatever it binds by hand.
        /// </summary>
        protected void Bind(FarmViewModelBase viewModel, StatusLine status, Action onChanged)
        {
            _boundViewModel = viewModel;
            _boundStatus = status;

            viewModel.PropertyChanged += (s, e) =>
            {
                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    BeginInvoke((Action<object, PropertyChangedEventArgs>)((s2, e2) =>
                        ApplyChange(onChanged)), s, e);
                    return;
                }

                ApplyChange(onChanged);
            };

            ApplyChange(onChanged);
        }

        private void ApplyChange(Action onChanged)
        {
            if (IsDisposed || _boundViewModel == null) return;

            if (_boundStatus != null)
            {
                _boundStatus.Busy = _boundViewModel.IsBusy;
                _boundStatus.Status = _boundViewModel.StatusMessage;
                _boundStatus.Error = _boundViewModel.ErrorMessage;
            }

            onChanged?.Invoke();
        }

        /// <summary>
        /// Wires a themed button to a command, keeping Enabled in sync with
        /// <c>CanExecute</c>. <c>RelayCommand</c> raises <c>CanExecuteChanged</c>
        /// only when asked, so the view-models call <c>RaiseCanExecuteChanged</c>
        /// explicitly - there is no automatic requery like WPF's CommandManager.
        /// </summary>
        protected static void Bind(FarmButton button, System.Windows.Input.ICommand command)
        {
            button.Click += (s, e) =>
            {
                if (command.CanExecute(null))
                    command.Execute(null);
            };

            command.CanExecuteChanged += (s, e) =>
            {
                if (!button.IsDisposed)
                    button.Enabled = command.CanExecute(null);
            };

            button.Enabled = command.CanExecute(null);
        }
    }
}

using System.Diagnostics;
using System.Drawing;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageE
{
    /// <summary>
    /// PageE doubles as the state-capture/restore runtime probe: it holds a counter
    /// that increments on a button click. Per the IPageStateful contract, the counter
    /// is captured into the back-stack entry when the user navigates away, and pushed
    /// back through <see cref="RestoreState"/> before <c>OnNavigatedToAsync</c> when
    /// the user returns via Back (validates Pass 4's N-2 fix at runtime).
    ///
    /// IMPORTANT — what the contract actually restores:
    ///   • Restored: A → E (+3) → F → Back → E   (counter shows "3 (restored ✓)")
    ///   • NOT restored: A → E (+3) → Back → A → click E   (forward re-nav creates a
    ///     fresh Transient PageE — counter starts at 0; only the BACK direction pops
    ///     the history entry that carries the captured state).
    /// </summary>
    public partial class PageE : PageView, IPageStateful
    {
        private readonly PageEViewModel _viewModel;
        private int _counter;

        public PageE()
        {
            InitializeComponent();

            _viewModel = new PageEViewModel();

            btnBack.Click      += (s, e) => _viewModel.GoBackCommand.Execute(null);
            btnIncrement.Click += (s, e) => Increment();
            btnPageF.Click     += (s, e) => _viewModel.GoToPageFCommand.Execute(null);
        }

        private void Increment()
        {
            _counter++;
            lblCounter.Text = "Counter: " + _counter;
            lblCounter.ForeColor = SystemColors.ControlText;
        }

        // -----------------------------------------------------------------
        // IPageStateful — Pass 4 contract
        // -----------------------------------------------------------------

        public object CaptureState()
        {
            Debug.WriteLine("[PageE] CaptureState() -> " + _counter);
            return _counter;
        }

        public void RestoreState(object state)
        {
            Debug.WriteLine("[PageE] RestoreState(" + (state?.ToString() ?? "null") + ")");
            if (state is int restored)
            {
                _counter = restored;
                lblCounter.Text = "Counter: " + _counter + "  (restored ✓)";
                lblCounter.ForeColor = Color.DarkGreen;
            }
        }
    }
}

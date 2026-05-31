using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
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
    ///   • Restored: A → E (+3) → F → Back → E   (counter shows "3 (restored)")
    ///   • NOT restored: A → E (+3) → Back → A → click E   (forward re-nav creates a
    ///     fresh Transient PageE — counter starts at 0; only the BACK direction pops
    ///     the history entry that carries the captured state).
    /// </summary>
    public class PageE : PageView, IPageStateful
    {
        private readonly PageEViewModel _viewModel;
        private readonly Label _lblCounter;
        private int _counter;

        public PageE()
        {
            _viewModel = new PageEViewModel();

            BackColor = Color.Plum;
            Name = nameof(PageE);

            var label = new Label
            {
                Text = "PAGE E",
                Font = new Font("Microsoft Sans Serif", 28F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(40, 40)
            };

            var btnBack = new Button
            {
                Text = "← Back",
                Size = new Size(140, 34),
                Location = new Point(40, 100)
            };
            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);

            var btnPageF = new Button
            {
                Text = "Go to PageF",
                Size = new Size(140, 34),
                Location = new Point(40, 250)
            };
            btnPageF.Click += (s, e) => _viewModel.GoToPageFCommand.Execute(null);

            // -- stateful counter --
            _lblCounter = new Label
            {
                Text = "Counter: 0",
                Font = new Font("Microsoft Sans Serif", 12F),
                AutoSize = true,
                Location = new Point(40, 160)
            };

            var btnIncrement = new Button
            {
                Text = "Increment +",
                Size = new Size(140, 34),
                Location = new Point(40, 188)
            };
            btnIncrement.Click += (s, e) =>
            {
                _counter++;
                _lblCounter.Text = "Counter: " + _counter;
            };

            Controls.Add(label);
            Controls.Add(btnBack);
            Controls.Add(_lblCounter);
            Controls.Add(btnIncrement);
            Controls.Add(btnPageF);
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
                _lblCounter.Text = "Counter: " + _counter + "  (restored ✓)";
                _lblCounter.ForeColor = Color.DarkGreen;
            }
        }
    }
}

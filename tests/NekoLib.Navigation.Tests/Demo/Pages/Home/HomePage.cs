using System;
using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.Home
{
    public partial class HomePage : PageView
    {
        private readonly HomePageViewModel _viewModel;

        public HomePage()
        {
            InitializeComponent();

            _viewModel = new HomePageViewModel();

            // Click anywhere on the page (or any descendant control) to go to PAGE A.
            // The designer owns label1 + lblClickHint; we wire the click affordance
            // here in user-code so the behavior travels with the page lifecycle, and
            // we react to ControlAdded so any control the designer adds later also
            // forwards its Click to the same handler.
            Cursor = Cursors.Hand;
            this.Click += OnPageClick;
            HookClickRecursive(this);
        }

        private void HookClickRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                c.Click += OnPageClick;
                if (c.HasChildren)
                    HookClickRecursive(c);
            }
            parent.ControlAdded += (s, e) =>
            {
                e.Control.Click += OnPageClick;
                if (e.Control.HasChildren)
                    HookClickRecursive(e.Control);
            };
        }

        private void OnPageClick(object sender, EventArgs e)
        {
            _viewModel.GoToACommand.Execute(null);
        }
    }
}

using System;
using System.Drawing;
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

            // Click anywhere on the page to go to PAGE A.
            Cursor = Cursors.Hand;
            this.Click += OnPageClick;
            HookClickRecursive(this);

            var hint = new Label
            {
                Text = "(click anywhere to go to PAGE A)",
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Location = new Point(225, 240),
            };
            hint.Click += OnPageClick;
            Controls.Add(hint);
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

using System;
using System.Windows.Forms;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Welcome
{
    /// <summary>
    /// Entry page ("click to continue"). Anonymous — anyone can land here.
    /// Clicking anywhere on the page navigates to Login.
    /// </summary>
    [AllowAnonymous]
    public partial class WelcomePage : PageView
    {
        private readonly WelcomeViewModel _viewModel;

        public WelcomePage()
        {
            InitializeComponent();

            _viewModel = new WelcomeViewModel();

            // Whole-page click affordance (mirrors the prototype's bare "click to
            // continue" screen). Forward Click from the page and every descendant.
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
            _viewModel.GoToLoginCommand.Execute(null);
        }
    }
}

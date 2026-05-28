using System.Windows.Forms;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageA
{
    public partial class PageA : PageView
    {
        private readonly PageAViewModel _viewModel;

        public PageA()
        {
            InitializeComponent();

            this.BackColor = System.Drawing.Color.LightBlue;

            _viewModel = new PageAViewModel();

            rtbDialogResult.DataBindings.Add(
                nameof(RichTextBox.Text),
                _viewModel,
                nameof(PageAViewModel.DialogResult),
                false,
                DataSourceUpdateMode.OnPropertyChanged);

            btnHome.Click   += (s, e) => _viewModel.GoHomeCommand.Execute(null);
            btnToast.Click  += (s, e) => _viewModel.SpawnToastCommand.Execute(null);
            btnDialog.Click += (s, e) => _viewModel.ShowDialogCommand.Execute(null);
            btnClear.Click  += (s, e) => _viewModel.ClearCommand.Execute(null);
        }
    }
}

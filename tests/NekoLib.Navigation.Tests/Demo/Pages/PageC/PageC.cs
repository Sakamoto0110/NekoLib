using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageC
{
    public partial class PageC : PageView
    {
        private readonly PageCViewModel _viewModel;

        public PageC()
        {
            InitializeComponent();

            _viewModel = new PageCViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
        }
    }
}

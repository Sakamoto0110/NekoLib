using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageE
{
    public partial class PageF : PageView
    {
        private readonly PageFViewModel _viewModel;

        public PageF()
        {
            InitializeComponent();

            _viewModel = new PageFViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
        }
    }
}

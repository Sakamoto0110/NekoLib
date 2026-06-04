using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageD
{
    public partial class PageD : PageView
    {
        private readonly PageDViewModel _viewModel;

        public PageD()
        {
            InitializeComponent();

            _viewModel = new PageDViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
            btnToF.Click  += (s, e) => _viewModel.GoToFCommand.Execute(null);
        }
    }
}

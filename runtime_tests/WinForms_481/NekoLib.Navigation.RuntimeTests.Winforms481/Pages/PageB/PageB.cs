using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageB
{
    public partial class PageB : PageView
    {
        private readonly PageBViewModel _viewModel;

        public PageB()
        {
            InitializeComponent();

            _viewModel = new PageBViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
        }
    }
}

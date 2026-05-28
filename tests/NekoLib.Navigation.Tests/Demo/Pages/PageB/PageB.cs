using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.PageB
{
    public partial class PageB : PageView
    {
        private readonly PageBViewModel _viewModel;

        public PageB()
        {
            InitializeComponent();

            _viewModel = new PageBViewModel();
        }
    }
}

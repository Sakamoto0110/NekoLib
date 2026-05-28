using NavigationDemo.Core;
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
        }
    }
}

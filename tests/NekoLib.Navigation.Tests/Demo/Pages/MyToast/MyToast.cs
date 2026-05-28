using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.MyToast
{
    public partial class MyToast : PopupView
    {
        private readonly MyToastViewModel _viewModel;

        public MyToast()
        {
            InitializeComponent();

            _viewModel = new MyToastViewModel();
        }
    }
}

using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.SimpleToast
{
    public partial class SimpleToastView : ToastViewBase
    {
        private readonly SimpleToastViewModel _viewModel;

        public SimpleToastView()
        {
            InitializeComponent();

            _viewModel = new SimpleToastViewModel();

            lblMessage.DataBindings.Add(
                nameof(System.Windows.Forms.Label.Text),
                _viewModel,
                nameof(SimpleToastViewModel.Message),
                false,
                System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged);
        }

        protected override void OnShown(object payload)
        {
            if (payload is string text && !string.IsNullOrEmpty(text))
                _viewModel.Message = text;
        }
    }
}

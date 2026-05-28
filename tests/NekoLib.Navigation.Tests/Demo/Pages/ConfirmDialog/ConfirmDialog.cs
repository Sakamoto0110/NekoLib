using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NavigationDemo.Pages.ConfirmDialog
{
    [PageMetadata(Name = "ConfirmDialog", Presentation = PagePresentationMode.ModalOverlay)]
    public partial class ConfirmDialog : PopupView, IPageOverlay<string>
    {
        private readonly ConfirmDialogViewModel _viewModel;

        public string Result { get; private set; }

        public bool HasResult => !(string.IsNullOrEmpty(Result) || string.IsNullOrWhiteSpace(Result));

        public ConfirmDialog()
        {
            InitializeComponent();

            _viewModel = new ConfirmDialogViewModel(result =>
            {
                SetResult(result);
                ClosePopup();
            });

            btnAccept.Click += (s, e) => _viewModel.AcceptCommand.Execute(null);
            btnCancel.Click += (s, e) => _viewModel.CancelCommand.Execute(null);
        }

        public void SetResult(string result)
        {
            Result = result;
        }
    }
}

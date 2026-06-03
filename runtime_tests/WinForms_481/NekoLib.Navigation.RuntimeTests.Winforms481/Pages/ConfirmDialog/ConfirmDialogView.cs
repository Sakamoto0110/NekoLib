using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.ConfirmDialog
{
    public partial class ConfirmDialogView : DialogViewBase
    {
        private readonly ConfirmDialogViewModel _viewModel;

        public ConfirmDialogView()
        {
            InitializeComponent();

            _viewModel = new ConfirmDialogViewModel(
                onConfirm: () => Confirm(),
                onCancel:  () => Cancel());

            btnAccept.Click += (s, e) => _viewModel.ConfirmCommand.Execute(null);
            btnCancel.Click += (s, e) => _viewModel.CancelCommand.Execute(null);
        }
    }
}

using System;
using System.Windows.Input;
using NekoLib.Mvvm;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.ConfirmDialog
{
    public sealed class ConfirmDialogViewModel : ViewModelBase
    {
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public ConfirmDialogViewModel(Action onConfirm, Action onCancel)
        {
            if (onConfirm == null) throw new ArgumentNullException("onConfirm");
            if (onCancel == null) throw new ArgumentNullException("onCancel");

            ConfirmCommand = new RelayCommand(_ => onConfirm());
            CancelCommand = new RelayCommand(_ => onCancel());
        }
    }
}

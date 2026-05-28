using System;
using System.Windows.Input;
using NavigationDemo.Core;

namespace NavigationDemo.Pages.ConfirmDialog
{
    public sealed class ConfirmDialogViewModel : ViewModelBase
    {
        private readonly Action<string> _closeWithResult;

        public ICommand AcceptCommand { get; }
        public ICommand CancelCommand { get; }

        public ConfirmDialogViewModel(Action<string> closeWithResult)
        {
            if (closeWithResult == null) throw new ArgumentNullException("closeWithResult");
            _closeWithResult = closeWithResult;

            AcceptCommand = new RelayCommand(_ => _closeWithResult("Confirmed"));
            CancelCommand = new RelayCommand(_ => _closeWithResult("Cancelled"));
        }
    }
}

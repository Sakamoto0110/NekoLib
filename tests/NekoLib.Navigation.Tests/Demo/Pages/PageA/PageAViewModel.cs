using System.Windows.Input;
using NavigationDemo.Core;
using NekoLib.Navigation;
using ConfirmDialogView = NavigationDemo.Pages.ConfirmDialog.ConfirmDialog;
using MyToastView = NavigationDemo.Pages.MyToast.MyToast;

namespace NavigationDemo.Pages.PageA
{
    public sealed class PageAViewModel : ViewModelBase
    {
        private string _dialogResult = string.Empty;

        public string DialogResult
        {
            get { return _dialogResult; }
            set { SetProperty(ref _dialogResult, value); }
        }

        public ICommand GoHomeCommand { get; }
        public ICommand SpawnToastCommand { get; }
        public ICommand ShowDialogCommand { get; }
        public ICommand ClearCommand { get; }

        public PageAViewModel()
        {
            GoHomeCommand = new RelayCommand(async _ => await NavigationService.GoHomeAsync());
            SpawnToastCommand = new RelayCommand(_ => NavigationService.ShowOverlay<MyToastView>());
            ShowDialogCommand = new RelayCommand(async _ =>
            {
                var result = await NavigationService.ShowDialogAsync<ConfirmDialogView, string>();
                DialogResult = DialogResult + result + "\n";
            });
            ClearCommand = new RelayCommand(_ => DialogResult = string.Empty);
        }
    }
}

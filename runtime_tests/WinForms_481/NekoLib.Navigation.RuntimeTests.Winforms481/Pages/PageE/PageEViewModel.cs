using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageE
{
    public sealed class PageEViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }
        public ICommand GoToPageFCommand { get; }
        public PageEViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
            GoToPageFCommand = new RelayCommand(async _ => await NavigationService.SwitchPage<PageF>());

        }
    }
}

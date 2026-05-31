using System.Windows.Input;
using NavigationDemo.Core;
using NekoLib.Navigation;

namespace NavigationDemo.Pages.PageE
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

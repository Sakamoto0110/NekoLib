using System.Windows.Input;
using NavigationDemo.Core;
using NekoLib.Navigation;
using PageFView = NavigationDemo.Pages.PageE.PageF;

namespace NavigationDemo.Pages.PageD
{
    public sealed class PageDViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }
        public ICommand GoToFCommand { get; }

        public PageDViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
            GoToFCommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<PageFView>());
        }
    }
}

using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using PageFView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageE.PageF;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageD
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

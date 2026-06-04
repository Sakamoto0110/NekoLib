using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using PageBView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageB.PageB;
using PageCView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageC.PageC;
using PageDView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageD.PageD;
using PageEView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageE.PageE;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageA
{
    public sealed class PageAViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }
        public ICommand GoToBCommand { get; }
        public ICommand GoToCCommand { get; }
        public ICommand GoToDCommand { get; }
        public ICommand GoToECommand { get; }

        public PageAViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
            GoToBCommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<PageBView>());
            GoToCCommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<PageCView>());
            GoToDCommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<PageDView>());
            GoToECommand  = new RelayCommand(async _ => await NavigationService.SwitchPage<PageEView>());
        }
    }
}

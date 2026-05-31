using System.Windows.Input;
using NavigationDemo.Core;
using NekoLib.Navigation;
using PageBView = NavigationDemo.Pages.PageB.PageB;
using PageCView = NavigationDemo.Pages.PageC.PageC;
using PageDView = NavigationDemo.Pages.PageD.PageD;
using PageEView = NavigationDemo.Pages.PageE.PageE;

namespace NavigationDemo.Pages.PageA
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

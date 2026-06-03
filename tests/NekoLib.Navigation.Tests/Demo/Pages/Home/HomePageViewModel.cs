using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using PageAView = NavigationDemo.Pages.PageA.PageA;

namespace NavigationDemo.Pages.Home
{
    public sealed class HomePageViewModel : ViewModelBase
    {
        public ICommand GoToACommand { get; }

        public HomePageViewModel()
        {
            GoToACommand = new RelayCommand(async _ => await NavigationService.SwitchPage<PageAView>());
        }
    }
}

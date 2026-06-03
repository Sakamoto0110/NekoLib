using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using PageAView = NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageA.PageA;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.Home
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

using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Login;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Welcome
{
    public sealed class WelcomeViewModel : ViewModelBase
    {
        public ICommand GoToLoginCommand { get; }

        public WelcomeViewModel()
        {
            GoToLoginCommand = new RelayCommand(async _ => await NavigationService.SwitchPage<LoginPage>());
        }
    }
}

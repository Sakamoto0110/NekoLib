using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.User
{
    /// <summary>
    /// Back button on the User destination — pops history to Login. Sign-out is
    /// owned by the bootstrap-configured idle timeout, not by the back action.
    /// </summary>
    public sealed class UserViewModel : ViewModelBase
    {
        public ICommand GoBackCommand { get; }

        public UserViewModel()
        {
            GoBackCommand = new RelayCommand(async _ => await NavigationService.GoBackAsync());
        }
    }
}

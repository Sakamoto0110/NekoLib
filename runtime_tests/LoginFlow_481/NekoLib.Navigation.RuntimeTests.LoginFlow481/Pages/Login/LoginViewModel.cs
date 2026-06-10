using System.Threading.Tasks;
using System.Windows.Input;
using NekoLib.Mvvm;
using NekoLib.Navigation;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Admin;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.User;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Login
{
    /// <summary>
    /// Validates credentials against an in-memory demo store and routes by role:
    /// <c>admin/admin</c> → Admin, <c>user/user</c> → User. Anything else surfaces an
    /// inline error and stays on the page. On success it flips the framework's
    /// built-in <c>NavigationService.Session</c> so the destination page's role
    /// guard passes.
    /// </summary>
    public sealed class LoginViewModel : ViewModelBase
    {
        private string _username;
        private string _password;
        private string _error;

        public string Username
        {
            get { return _username; }
            set { SetProperty(ref _username, value); }
        }

        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value); }
        }

        public string Error
        {
            get { return _error; }
            set { SetProperty(ref _error, value); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync());
        }

        private async Task ExecuteLoginAsync()
        {
            Error = string.Empty;

            var user = (Username ?? string.Empty).Trim();
            var pass = Password ?? string.Empty;

            if (user == "admin" && pass == "admin")
            {
                NavigationService.Session.SignIn("admin");
                await NavigationService.SwitchPage<AdminPage>();
            }
            else if (user == "user" && pass == "user")
            {
                NavigationService.Session.SignIn("user");
                await NavigationService.SwitchPage<UserPage>();
            }
            else
            {
                Error = "Invalid credentials. Try admin/admin or user/user.";
            }
        }
    }
}

using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Login;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.User
{
    /// <summary>
    /// User destination. The role guard only admits a session carrying the
    /// "user" role; otherwise the runtime redirects to Login.
    /// </summary>
    [RequireRole("user", RedirectTo = typeof(LoginPage))]
    public partial class UserPage : PageView
    {
        private readonly UserViewModel _viewModel;

        public UserPage()
        {
            InitializeComponent();

            _viewModel = new UserViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
        }
    }
}

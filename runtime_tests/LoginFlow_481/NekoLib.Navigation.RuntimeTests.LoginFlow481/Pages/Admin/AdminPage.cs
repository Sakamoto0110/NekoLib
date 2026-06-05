using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Login;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Admin
{
    /// <summary>
    /// Admin destination. The role guard only admits a session carrying the
    /// "admin" role; otherwise the runtime redirects to Login.
    /// </summary>
    [RequireRole("admin", RedirectTo = typeof(LoginPage))]
    public partial class AdminPage : PageView
    {
        private readonly AdminViewModel _viewModel;

        public AdminPage()
        {
            InitializeComponent();

            _viewModel = new AdminViewModel();

            btnBack.Click += (s, e) => _viewModel.GoBackCommand.Execute(null);
        }
    }
}

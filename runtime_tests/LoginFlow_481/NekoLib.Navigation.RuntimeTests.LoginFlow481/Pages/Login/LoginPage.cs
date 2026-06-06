using System.Windows.Forms;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Login
{
    /// <summary>
    /// Login screen (username / password / Login button). Anonymous so it is always
    /// reachable; on success the view model signs the session in and routes by role.
    /// TextBoxes and the error label are two-way bound to the view model.
    /// </summary>
    [AllowAnonymous]
    public partial class LoginPage : PageView
    {
        private readonly LoginViewModel _viewModel;

        public LoginPage()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();

            txtUsername.DataBindings.Add(
                nameof(TextBox.Text), _viewModel, nameof(LoginViewModel.Username),
                false, DataSourceUpdateMode.OnPropertyChanged);

            txtPassword.DataBindings.Add(
                nameof(TextBox.Text), _viewModel, nameof(LoginViewModel.Password),
                false, DataSourceUpdateMode.OnPropertyChanged);

            lblError.DataBindings.Add(
                nameof(Label.Text), _viewModel, nameof(LoginViewModel.Error),
                false, DataSourceUpdateMode.OnPropertyChanged);

            btnLogin.Click += (s, e) => _viewModel.LoginCommand.Execute(null);
        }
    }
}

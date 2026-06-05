using System;
using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.RuntimeTests.LoginFlow481.Pages.Welcome;

namespace NekoLib.Navigation.RuntimeTests.LoginFlow481
{
    /// <summary>
    /// Host window for the login-flow scenario reconstructed from the Miro
    /// "prototype" board: Welcome → Login → (Admin | User), each destination
    /// returning to Login via Back.
    ///
    /// The form owns only window lifecycle. The navigation framework owns
    /// pages, the session (`NavigationService.Session`), and the 30-second
    /// idle timeout — all configured by the bootstrap chain below.
    /// </summary>
    public partial class MainForm : Form
    {
        private bool _closingFromShutdown;

        public MainForm()
        {
            InitializeComponent();

            var ctx = PageNavBootstrap
                .Use<WinFormsPlatformAdapter>(hostPanel)
                .RegisterPagesFromAssembly(typeof(MainForm).Assembly)
                .SetHome<WelcomePage>()                                      // top-level home declaration
                .UseIdleTimeout(30000)                                       // framework resolves home + signs out
                .Start();

            NavigationService.UseContext(ctx);

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await NavigationService.GoHomeAsync();
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closingFromShutdown) return;

            _closingFromShutdown = true;
            e.Cancel = true;

            try
            {
                await NavigationService.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainForm] Shutdown error: " + ex);
            }

            BeginInvoke((Action)Close);
        }
    }
}

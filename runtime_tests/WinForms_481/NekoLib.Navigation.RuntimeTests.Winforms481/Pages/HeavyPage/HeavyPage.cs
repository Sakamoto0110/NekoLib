using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.HeavyPage
{
    [PageLoad(NekoLib.Navigation.Metadata.NavigationLoadMode.LoadBeforeShow)]
    public sealed class HeavyPage : PageView, IBackgroundLoadable
    {
        private readonly HeavyPageViewModel _viewModel;
        private Label label;

        public HeavyPage()
        {
            _viewModel = new HeavyPageViewModel();

            label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            InitializeComponent();
            Controls.Add(label);

            label.DataBindings.Add(
                nameof(Label.Text),
                _viewModel,
                nameof(HeavyPageViewModel.StatusText),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.BackColor = System.Drawing.Color.Firebrick;
            this.Name = "HeavyPage";
            this.ResumeLayout(false);
        }

        public async Task LoadInBackgroundAsync(object args)
        {
            await Task.Delay(2000);
        }

        public Task ApplyBackgroundResultAsync()
        {
            _viewModel.StatusText = "Heavy page loaded";
            return Task.CompletedTask;
        }
    }
}

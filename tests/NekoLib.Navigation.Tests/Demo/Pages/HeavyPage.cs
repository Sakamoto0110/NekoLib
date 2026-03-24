using NekoLib.Diagnostics;
using NekoLib.Navigation;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms;
using NekoLib.Navigation.WinForms.Hosting;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Demo.Pages
{
    [PageLoad(NekoLib.Navigation.Metadata.NavigationLoadMode.LoadBeforeShow)]
    public sealed class HeavyPage : PageView, IBackgroundLoadable
    {
        Label label;
        public HeavyPage()
        {
               label = new Label { Text = "Loading...", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };

            InitializeComponent();
            Controls.Add(label);

             
        }

        private async void LoadAsync()
        {
            
            
         }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // HeavyPage
            // 
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
            label.Text = "Heavy page loaded";
            return Task.CompletedTask;
        }
    }
}
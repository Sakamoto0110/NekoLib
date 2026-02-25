using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Diagnostics;
using NekoLib.Navigation.WinForms;

namespace Demo.Pages
{
    public sealed class HeavyPage : BasePage
    {
         
        public HeavyPage()
        {
            var label = new Label { Text = "Loading...", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            var back = new Button { Text = "Back", Dock = DockStyle.Bottom };
 
            Controls.Add(back);
            Controls.Add(label);

            LoadAsync(label);
        }

        private async void LoadAsync(Label label)
        {
             await Task.Delay(700);
            label.Text = "Heavy page loaded";
         }
    }
}
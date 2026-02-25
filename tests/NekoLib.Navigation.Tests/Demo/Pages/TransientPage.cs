using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.WinForms;

namespace Demo.Pages
{
    public sealed class TransientPage : BasePage
    {
 
        public TransientPage()
        {
            var back = new Button { Text = "Back", Dock = DockStyle.Fill };
           
            Controls.Add(back);
        }
    }
}
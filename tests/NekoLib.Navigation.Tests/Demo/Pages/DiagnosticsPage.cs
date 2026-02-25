using System.Windows.Forms;
using NekoLib.Diagnostics;
using NekoLib.Navigation;
using NekoLib.Navigation.WinForms;

namespace Demo.Pages
{
    public sealed class DiagnosticsPage : BasePage
    {
   

        public DiagnosticsPage( )
        {
            var list = new ListBox { Dock = DockStyle.Fill };
            var back = new Button { Text = "Back", Dock = DockStyle.Bottom };

            

             
            Controls.Add(back);
            Controls.Add(list);
        }
    }
}
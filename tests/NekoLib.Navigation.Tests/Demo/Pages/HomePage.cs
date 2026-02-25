using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.WinForms;


namespace Demo.Pages
{
    public sealed class HomePage : BasePage
    {
       
         

        public HomePage( )
        {
            

            var a = new Button { Text = "Cached Page", Dock = DockStyle.Top };
            var b = new Button { Text = "Transient Page", Dock = DockStyle.Top };
            var c = new Button { Text = "Heavy Page", Dock = DockStyle.Top };
            var d = new Button { Text = "Diagnostics", Dock = DockStyle.Top };
            a.Click += (_, __) => NavigationService.SwitchPage<CachedPage>();
            b.Click += (_, __) => NavigationService.SwitchPage<TransientPage>();
            c.Click += (_, __) => NavigationService.SwitchPage<HeavyPage>();
            d.Click += (_, __) => NavigationService.SwitchPage<DiagnosticsPage>();

             
             Controls.Add(d);
             Controls.Add(c);
             Controls.Add(b);
             Controls.Add(a);
            
        }
    }
}
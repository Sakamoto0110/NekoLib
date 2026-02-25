using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.WinForms;
namespace Demo.Pages
{
    public sealed class CachedPage : BasePage
    {
        private int _counter;
        
        public CachedPage()
        {
            var label = new Label { Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
            var back = new Button { Text = "Back", Dock = DockStyle.Bottom };

  
            Controls.Add(back);
            Controls.Add(label);

            Paint += (_,__) =>
            {
                _counter++;
                label.Text = $"Cached counter: {_counter}";
            };
        }
    }
}
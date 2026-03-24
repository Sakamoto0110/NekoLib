
 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.WinForms;
using NekoLib.Navigation.WinForms.Hosting;
using NekoLib.Navigation.WinForms.Pages;
namespace NavigationDemo.Pages {
     
    public partial class PageA : PageView
    {
        public PageA()
        {
           
            InitializeComponent();

            this.BackColor = Color.LightBlue;
            
        }

        private async void btnHome_Click(object sender, EventArgs e)
        {
            await NavigationService.GoHomeAsync();
        }
            
        private void btnPopup_Click(object sender, EventArgs e)
        {
            NavigationService.ShowOverlay<MyPopup>();
        }

        private async void btnDialog_Click(object sender, EventArgs e)
        {
            var result = await NavigationService.ShowDialogAsync<ConfirmDialog, string>();
            rtbDialogResult.AppendText(result+"\n");
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            rtbDialogResult.Text = "";
        }
    }
}


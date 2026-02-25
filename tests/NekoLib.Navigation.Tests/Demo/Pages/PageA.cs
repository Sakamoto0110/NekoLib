
 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Navigation.Attributes;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.WinForms;
namespace Demo.Pages {
    [PageBehavior( reusePolicy: PageReusePolicy.Singleton )]
    public partial class PageA : BasePage
    {
        public PageA()
        {
           
            InitializeComponent();

            this.BackColor = Color.LightBlue;
        }



        private void button1_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

    }
}


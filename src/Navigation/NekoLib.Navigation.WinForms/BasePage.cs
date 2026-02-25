using NekoLib.Navigation.Contracts.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms {
    public abstract partial class BasePage : UserControl, IPageView
    {
 
        public object NativeView => this;
        public virtual bool AllowBackNavigation => true;

        protected BasePage()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
        }
    }
}



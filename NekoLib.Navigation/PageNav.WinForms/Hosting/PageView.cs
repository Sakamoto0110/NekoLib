using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{

    public class PageView : UserControl, IPageView
    {
         
        public object NativeView => this;
        public new bool IsDisposed { get; private set; }
        public new  bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        protected PageView()
        {
            Name = GetType().FullName!;
            // absolutely nothing non-designer-safe here
        }

        public virtual Task OnNavigatedToAsync(NavigationArgs args)
            => Task.CompletedTask;

        public virtual Task OnNavigatedFromAsync()
            => Task.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }


  


}
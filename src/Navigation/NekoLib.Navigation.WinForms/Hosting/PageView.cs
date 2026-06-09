using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Base class for all WinForms pages: implements <see cref="IPageView"/> +
    /// <see cref="IPageLifecycle"/> and exposes a single designer-safe entry point.
    /// </summary>
    public class PageView : UserControl, IPageView, IPageLifecycle
    {
        public object NativeView => this;
        public new bool IsDisposed { get; private set; }

        /// <summary>
        /// Critical for visual inheritance. Prevents the designer from 
        /// executing framework code during design-time.
        /// </summary>
        public new bool DesignMode =>
            base.DesignMode ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        /// <summary>
        /// Lets individual pages opt out of the back-stack. Default: true.
        /// </summary>
        public virtual bool AllowBackNavigation => true;

        protected PageView()
        {
            // Keep the ctor designer-safe: the WinForms designer instantiates this
            // type, so it must not run navigation/runtime logic here. Seed the
            // IPageView.Name used for registration, lookup, and diagnostics.
            Name = GetType().FullName!;
        }

        // Optional lifecycle hooks — override to load/refresh state on entry and to
        // persist/flush on exit. The runtime invokes both on the UI thread.
        public virtual Task OnNavigatedToAsync(NavigationArgs args)
            => Task.CompletedTask;

        public virtual Task OnNavigatedFromAsync()
            => Task.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
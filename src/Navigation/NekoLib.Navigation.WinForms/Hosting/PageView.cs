using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Hosting
{
    /// <summary>
    /// Unified base class for all WinForms pages. 
    /// Merges legacy BasePage and PageView for a single, designer-safe entry point.
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
        /// Concept moved from legacy BasePage. Allows individual pages to 
        /// opt-out of the back-stack.
        /// </summary>
        public virtual bool AllowBackNavigation => true;

        protected PageView()
        {
            // Keep the constructor empty or designer-safe.
            Name = GetType().FullName!;
        }

        // Lifecycle hooks for your logic (e.g., loading Excel data)
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
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for navigated pages. Mirrors the WinForms PageView: implements
    /// IPageView + IPageLifecycle and exposes a design-time-safe DesignMode check.
    /// </summary>
    public class PageView : UserControl, IPageView, IPageLifecycle
    {
        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        /// <summary>
        /// Lets individual pages opt out of the back-stack. Default: true.
        /// </summary>
        public virtual bool AllowBackNavigation => true;

        protected PageView()
        {
            Name = GetType().Name;
        }

        public virtual Task OnNavigatedToAsync(NavigationArgs args) => Task.CompletedTask;
        public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;

        public virtual void Dispose()
        {
            IsDisposed = true;
        }
    }
}

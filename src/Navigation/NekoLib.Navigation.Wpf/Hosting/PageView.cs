using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// WPF base class for navigated pages. Mirrors the WinForms PageView: implements
    /// IPageView + IPageLifecycle + IPageVisibility and exposes a design-time-safe
    /// DesignMode check.
    /// </summary>
    public class PageView : UserControl, IPageView, IPageLifecycle, IPageVisibility
    {
        public object NativeView => this;
        public bool IsDisposed { get; private set; }

        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        /// <summary>
        /// Compatibility property retained from the original base class. The
        /// navigation runtime does not currently consult it; back/history policy
        /// must not depend on this value.
        /// </summary>
        public virtual bool AllowBackNavigation => true;

        protected PageView()
        {
            Name = GetType().Name;
        }

        public virtual Task OnNavigatedToAsync(NavigationArgs args) => Task.CompletedTask;
        public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;

        public virtual void ShowPage() => Visibility = System.Windows.Visibility.Visible;

        public virtual void HidePage() => Visibility = System.Windows.Visibility.Collapsed;

        public virtual void Dispose()
        {
            IsDisposed = true;
        }
    }
}

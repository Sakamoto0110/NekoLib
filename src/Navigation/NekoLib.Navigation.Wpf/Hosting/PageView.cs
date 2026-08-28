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
        /// <inheritdoc />
        public object NativeView => this;
        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <summary>Gets whether the control is running inside the WPF designer.</summary>
        public bool DesignMode =>
            base.GetValue(DesignerProperties.IsInDesignModeProperty) is bool b && b;

        /// <summary>
        /// Compatibility property retained from the original base class. The
        /// navigation runtime does not currently consult it; back/history policy
        /// must not depend on this value.
        /// </summary>
        public virtual bool AllowBackNavigation => true;

        /// <summary>Initializes a designer-safe WPF page view.</summary>
        protected PageView()
        {
            Name = GetType().Name;
        }

        /// <inheritdoc />
        public virtual Task OnNavigatedToAsync(NavigationArgs args) => Task.CompletedTask;
        /// <inheritdoc />
        public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;

        /// <inheritdoc />
        public virtual void ShowPage() => Visibility = System.Windows.Visibility.Visible;

        /// <inheritdoc />
        public virtual void HidePage() => Visibility = System.Windows.Visibility.Collapsed;

        /// <summary>Marks the view disposed; WPF owns the native control lifetime.</summary>
        public virtual void Dispose()
        {
            IsDisposed = true;
        }
    }
}
